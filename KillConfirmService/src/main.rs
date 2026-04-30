#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

mod soundpack;
mod util;

use axum::http::StatusCode;
use axum::{
    Router,
    routing::{get, post},
};
use std::{
    env,
    fs::{self, OpenOptions},
    io::Write,
    path::{Path, PathBuf},
    sync::Arc,
    time::Duration,
};
use tokio::sync::{RwLock, broadcast};
use tower_http::{timeout::TimeoutLayer, trace::TraceLayer};
use tracing::info;
use tracing::level_filters::LevelFilter;
use tracing_subscriber::EnvFilter;
use tracing_subscriber::{layer::SubscriberExt, util::SubscriberInitExt};
use util::signal::shutdown_signal;
use util::state::{AppState, Mutable};

use util::Args;
use util::playback::{get_output_stream, list_host_devices};

use anyhow::{Context, Result};
use soundpack::Preset;
use util::event_stream::{cs2_root, events_ws, health, shutdown, test_event};
use util::handler::update;

const DEFAULT_LOG_LEVEL: LevelFilter = if cfg!(debug_assertions) {
    LevelFilter::DEBUG
} else {
    LevelFilter::INFO
};

#[tokio::main]
async fn main() {
    bootstrap_log("process entry");
    bootstrap_log(&format!("args: {:?}", env::args_os().collect::<Vec<_>>()));
    bootstrap_log(&format!(
        "current_exe: {}",
        env::current_exe()
            .map(|path| path.display().to_string())
            .unwrap_or_else(|_| "<unavailable>".to_string())
    ));
    bootstrap_log(&format!(
        "current_dir(before run): {}",
        env::current_dir()
            .map(|path| path.display().to_string())
            .unwrap_or_else(|_| "<unavailable>".to_string())
    ));

    if let Err(error) = run().await {
        bootstrap_log(&format!("fatal error before exit: {error:?}"));
        service_log(&format!("fatal error: {error:?}"));
        eprintln!("{error:?}");
        std::process::exit(1);
    }
}

async fn run() -> Result<()> {
    service_log("service starting");

    tracing_subscriber::registry()
        .with(
            EnvFilter::builder()
                .with_default_directive(DEFAULT_LOG_LEVEL.into())
                .from_env_lossy(),
        )
        .with(tracing_subscriber::fmt::layer().without_time())
        .init();

    let sanitized_args = Args::sanitized_runtime_args();
    bootstrap_log(&format!("sanitized args: {:?}", sanitized_args));
    let args = Args::parse_runtime();
    normalize_working_directory().context("failed to locate runtime assets")?;
    service_log(&format!(
        "working directory: {}",
        env::current_dir()
            .map(|path| path.display().to_string())
            .unwrap_or_else(|_| "<unknown>".to_string())
    ));

    if args.list_devices {
        list_host_devices()?;
        return Ok(());
    }

    if args.list_presets {
        soundpack::list()?;
        return Ok(());
    }

    // initialize the specified audio device
    let output_stream = get_output_stream(&args.device).context("failed to get output stream")?;
    service_log("audio output stream ready");

    let preset_name = if let Some(variant) = &args.variant {
        format!("{}_v_{}", args.preset, variant)
    } else {
        args.preset.clone()
    };

    let preset = Preset::load(&preset_name)
        .with_context(|| format!("failed to load preset '{}'", &preset_name))?;
    info!("preset '{}' loaded successfully", &preset_name);
    info!("variant: {}", args.variant.as_deref().unwrap_or("none"));
    service_log(&format!("preset '{preset_name}' loaded"));

    let (event_tx, _) = broadcast::channel(64);
    let (shutdown_tx, shutdown_rx) = broadcast::channel(1);

    let app_state = Arc::new(AppState {
        mutable: RwLock::new(Mutable {
            initialized: false,
            steamid: "".into(),
            ply_kills: 0,
            ply_hs_kills: 0,
            last_active_weapon_is_knife: false,
            last_active_weapon_seen_at: None,
            current_round: 0,
            last_round_phase: None,
            has_first_kill_in_round: false,
            pending_last_kill: None,
        }),
        stream_handle: output_stream,
        args,
        preset: RwLock::new(preset),
        event_tx,
        shutdown_tx,
    });

    let app = Router::new()
        .route("/", post(update))
        .route("/events", get(events_ws))
        .route("/health", get(health))
        .route("/cs2-root", get(cs2_root))
        .route("/shutdown", post(shutdown))
        .route("/soundpack", get(util::event_stream::soundpack).post(util::event_stream::set_soundpack))
        .route("/test/{kill_count}", get(test_event).post(test_event))
        .with_state(app_state)
        .layer((
            TraceLayer::new_for_http(),
            // Graceful shutdown will wait for outstanding requests to complete. Add a timeout so
            // requests don't hang forever.
            TimeoutLayer::with_status_code(StatusCode::REQUEST_TIMEOUT, Duration::from_secs(10)),
        ));

    // run our app with hyper, listening globally on port 3000
    let listener = tokio::net::TcpListener::bind("127.0.0.1:3000").await?;
    service_log("listening on 127.0.0.1:3000");
    axum::serve(listener, app)
        .with_graceful_shutdown(shutdown_signal(shutdown_rx))
        .await?;

    Ok(())
}

fn service_log(message: &str) {
    append_trace_log("service.log", message);
}

fn normalize_working_directory() -> Result<()> {
    if Path::new("sounds").is_dir() {
        return Ok(());
    }

    let exe_path = env::current_exe().context("failed to get current executable path")?;
    let Some(exe_dir) = exe_path.parent() else {
        return Ok(());
    };

    if exe_dir.join("sounds").is_dir() {
        env::set_current_dir(exe_dir).context("failed to switch to executable directory")?;
    }

    Ok(())
}

fn bootstrap_log(message: &str) {
    append_trace_log("bootstrap.log", message);
}

fn append_trace_log(file_name: &str, message: &str) {
    for log_dir in trace_log_directories() {
        if fs::create_dir_all(&log_dir).is_err() {
            continue;
        }

        let log_path = log_dir.join(file_name);
        let Ok(mut file) = OpenOptions::new().create(true).append(true).open(log_path) else {
            continue;
        };

        let _ = writeln!(file, "{message}");
    }
}

fn trace_log_directories() -> Vec<PathBuf> {
    let mut dirs = vec![env::temp_dir().join("KillConfirmGameBar")];

    if let Some(local_dir) = env::var_os("LOCALAPPDATA").map(PathBuf::from) {
        let local_trace_dir = local_dir.join("KillConfirmGameBar");
        if !dirs.iter().any(|dir| dir == &local_trace_dir) {
            dirs.push(local_trace_dir);
        }
    }

    dirs
}
