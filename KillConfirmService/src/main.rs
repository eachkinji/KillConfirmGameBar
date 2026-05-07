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
    process::Command,
    sync::Arc,
    sync::atomic::{AtomicU32, AtomicU64},
    time::{Duration, SystemTime, UNIX_EPOCH},
};
use tokio::sync::{RwLock, broadcast};
use tokio::time::sleep;
use tower_http::{timeout::TimeoutLayer, trace::TraceLayer};
use tracing::info;
use tracing::level_filters::LevelFilter;
use tracing_subscriber::EnvFilter;
use tracing_subscriber::{layer::SubscriberExt, util::SubscriberInitExt};
use util::signal::shutdown_signal;
use util::state::{AppState, Mutable};

use util::Args;
use util::playback::{default_output_device_name, get_output_stream_with_name, list_host_devices};

use anyhow::{Context, Result};
use soundpack::Preset;
use util::event_stream::{
    audio_reload, audio_volume, cs2_root, events_ws, gsi_status, health, shutdown, test_event,
};
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

    if args.open_logs {
        open_runtime_log_folder();
        return Ok(());
    }

    if args.open_settings_launcher {
        launch_settings_launcher().context("failed to launch settings helper")?;
        return Ok(());
    }

    if let Some(port) = args.free_port {
        free_local_port(port).with_context(|| format!("failed to free port {port}"))?;
        return Ok(());
    }

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
    let (output_stream, output_device_name) =
        get_output_stream_with_name(&args.device).context("failed to get output stream")?;
    service_log("audio output stream ready");
    let initial_volume_percent = (args.volume.clamp(0.0, 2.0) * 100.0).round() as u32;

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
            last_active_weapon_badge_key: None,
            last_active_weapon_seen_at: None,
            current_round: 0,
            last_round_phase: None,
            has_first_kill_in_round: false,
            pending_last_kill: None,
        }),
        stream_handle: RwLock::new(output_stream),
        current_output_device_name: RwLock::new(output_device_name.clone()),
        args,
        preset: RwLock::new(preset),
        volume_percent: AtomicU32::new(initial_volume_percent),
        event_tx,
        shutdown_tx,
        gsi_posts: AtomicU64::new(0),
        gsi_parse_errors: AtomicU64::new(0),
        last_gsi_post_unix_ms: AtomicU64::new(0),
        last_gsi_parse_error_unix_ms: AtomicU64::new(0),
    });

    service_log(&format!("active audio device: {}", output_device_name));

    if app_state.args.device.eq_ignore_ascii_case("default") {
        let watcher_state = app_state.clone();
        tokio::spawn(async move {
            monitor_default_output_device(watcher_state).await;
        });
    }

    let app = Router::new()
        .route("/", post(update))
        .route("/events", get(events_ws))
        .route("/health", get(health))
        .route("/gsi-status", get(gsi_status))
        .route("/cs2-root", get(cs2_root))
        .route("/audio/reload", post(audio_reload))
        .route("/audio/volume", post(audio_volume))
        .route("/shutdown", post(shutdown))
        .route(
            "/soundpack",
            get(util::event_stream::soundpack).post(util::event_stream::set_soundpack),
        )
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

async fn monitor_default_output_device(app_state: Arc<AppState>) {
    service_log("default audio device watcher started");

    loop {
        sleep(Duration::from_secs(2)).await;

        if app_state.shutdown_tx.receiver_count() == 0 {
            break;
        }

        let detected_name = match default_output_device_name() {
            Ok(name) => name,
            Err(error) => {
                service_log(&format!("default audio watcher failed to read device: {error}"));
                continue;
            }
        };

        let current_name = {
            let current = app_state.current_output_device_name.read().await;
            current.clone()
        };

        if detected_name.eq_ignore_ascii_case(&current_name) {
            continue;
        }

        service_log(&format!(
            "default audio device changed: {} -> {}",
            current_name, detected_name
        ));

        match get_output_stream_with_name("default") {
            Ok((output_stream, resolved_name)) => {
                {
                    let mut stream_handle = app_state.stream_handle.write().await;
                    *stream_handle = output_stream;
                }
                {
                    let mut current = app_state.current_output_device_name.write().await;
                    *current = resolved_name.clone();
                }
                service_log(&format!(
                    "default audio device hot reloaded successfully -> {}",
                    resolved_name
                ));
            }
            Err(error) => {
                service_log(&format!(
                    "default audio device hot reload failed: {error}"
                ));
            }
        }
    }
}

fn service_log(message: &str) {
    append_trace_log("service.log", message);
}

fn open_runtime_log_folder() {
    let folder = runtime_log_dir();
    if let Err(error) = fs::create_dir_all(&folder) {
        service_log(&format!(
            "open logs failed to create folder {}: {error}",
            folder.display()
        ));
        return;
    }

    service_log(&format!("opening runtime log folder: {}", folder.display()));
    if let Err(error) = Command::new("explorer.exe").arg(&folder).spawn() {
        service_log(&format!("failed to open runtime log folder: {error}"));
    }
}

fn launch_settings_launcher() -> Result<()> {
    let exe_dir = env::current_exe()
        .context("failed to get current executable path")?
        .parent()
        .map(Path::to_path_buf)
        .context("failed to get executable directory")?;
    let launcher_path = exe_dir.join("killconfirm-settings-launcher.exe");
    service_log(&format!(
        "launching packaged settings helper: {}",
        launcher_path.display()
    ));

    let child = Command::new(&launcher_path)
        .spawn()
        .with_context(|| format!("failed to spawn {}", launcher_path.display()))?;
    service_log(&format!(
        "packaged settings helper spawned successfully. pid={}",
        child.id()
    ));
    Ok(())
}

fn free_local_port(port: u16) -> Result<()> {
    service_log(&format!("free-port requested for 127.0.0.1:{port}"));
    let pids = find_local_port_pids(port)?;

    if pids.is_empty() {
        service_log(&format!("free-port: no process owns port {port}"));
        return Ok(());
    }

    let current_pid = std::process::id();
    for pid in pids {
        if pid == current_pid {
            service_log(&format!("free-port: skipping helper pid {pid}"));
            continue;
        }

        service_log(&format!("free-port: terminating pid {pid}"));
        let output = Command::new("taskkill")
            .args(["/PID", &pid.to_string(), "/F"])
            .output()
            .with_context(|| format!("failed to run taskkill for pid {pid}"))?;

        service_log(&format!(
            "free-port: taskkill pid {pid} exit={:?} stdout={} stderr={}",
            output.status.code(),
            String::from_utf8_lossy(&output.stdout).trim(),
            String::from_utf8_lossy(&output.stderr).trim()
        ));
    }

    Ok(())
}

fn find_local_port_pids(port: u16) -> Result<Vec<u32>> {
    let output = Command::new("netstat")
        .args(["-ano", "-p", "tcp"])
        .output()
        .context("failed to run netstat")?;

    let stdout = String::from_utf8_lossy(&output.stdout);
    let port_suffix = format!(":{port}");
    let mut pids = Vec::new();

    for line in stdout.lines() {
        let parts: Vec<&str> = line.split_whitespace().collect();
        if parts.len() < 5 || !parts[0].eq_ignore_ascii_case("tcp") {
            continue;
        }

        let local_address = parts[1].to_ascii_lowercase();
        if !(local_address == format!("127.0.0.1:{port}")
            || local_address == format!("0.0.0.0:{port}")
            || local_address == format!("[::1]:{port}")
            || local_address == format!("[::]:{port}")
            || local_address.ends_with(&port_suffix))
        {
            continue;
        }

        if let Some(pid_text) = parts.last() {
            if let Ok(pid) = pid_text.parse::<u32>() {
                if !pids.contains(&pid) {
                    pids.push(pid);
                }
            }
        }
    }

    service_log(&format!("free-port: pids for port {port}: {pids:?}"));
    Ok(pids)
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
    let Some(log_path) = trace_log_path(file_name) else {
        return;
    };

    if let Some(parent) = log_path.parent() {
        let _ = fs::create_dir_all(parent);
    }

    rotate_trace_log_if_needed(&log_path);

    let timestamp_ms = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|duration| duration.as_millis())
        .unwrap_or(0);
    let pid = std::process::id();
    let line = format!("[unix_ms={timestamp_ms}] pid={pid} {message}\n");

    if let Ok(mut file) = OpenOptions::new().create(true).append(true).open(&log_path) {
        let _ = file.write_all(line.as_bytes());
    }
}

fn trace_log_path(file_name: &str) -> Option<PathBuf> {
    Some(runtime_log_dir().join(file_name))
}

fn runtime_log_dir() -> PathBuf {
    const PACKAGE_FAMILY_NAME: &str = "KillConfirmGameBar.Overlay_5jgcw66eyez0m";

    if let Ok(local_app_data) = env::var("LOCALAPPDATA") {
        return PathBuf::from(local_app_data)
            .join("Packages")
            .join(PACKAGE_FAMILY_NAME)
            .join("LocalState");
    }

    env::current_exe()
        .ok()
        .and_then(|path| path.parent().map(Path::to_path_buf))
        .unwrap_or_else(|| PathBuf::from("."))
}

fn rotate_trace_log_if_needed(log_path: &Path) {
    const MAX_LOG_BYTES: u64 = 512 * 1024;

    let Ok(metadata) = fs::metadata(log_path) else {
        return;
    };
    if metadata.len() <= MAX_LOG_BYTES {
        return;
    }

    let old_path = log_path.with_extension("log.old");
    let _ = fs::remove_file(&old_path);
    let _ = fs::rename(log_path, old_path);
}
