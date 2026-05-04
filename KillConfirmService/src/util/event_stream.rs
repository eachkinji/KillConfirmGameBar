use std::{
    fs,
    path::PathBuf,
    process::Command,
    sync::Arc,
    sync::atomic::Ordering,
    time::{SystemTime, UNIX_EPOCH},
};

use axum::{
    Json,
    extract::{
        Path, Query, State,
        ws::{Message, WebSocket, WebSocketUpgrade},
    },
    response::IntoResponse,
};
use serde::{Deserialize, Serialize};
use tokio::sync::broadcast;
use tracing::{debug, error, warn};

use crate::soundpack::Preset;
use crate::soundpack::sound::play_audio;
use crate::util::logging::service_log;
use crate::util::playback::get_output_stream;

use super::state::{AppState, KillEvent};

#[derive(Debug, Deserialize)]
pub struct TestEventQuery {
    pub headshot: Option<bool>,
    pub knife: Option<bool>,
    pub first: Option<bool>,
    pub last: Option<bool>,
    pub main: Option<bool>,
    pub audio: Option<bool>,
    pub player_name: Option<String>,
    pub steamid: Option<String>,
}

#[derive(Debug, Serialize)]
pub struct HealthResponse {
    pub ok: bool,
    pub service: &'static str,
}

#[derive(Debug, Serialize)]
pub struct GsiStatusResponse {
    pub posts: u64,
    pub parse_errors: u64,
    pub last_post_unix_ms: Option<u64>,
    pub last_post_age_ms: Option<u64>,
    pub last_parse_error_unix_ms: Option<u64>,
}

#[derive(Debug, Deserialize)]
pub struct SoundPackRequest {
    pub preset: String,
}

#[derive(Debug, Deserialize)]
pub struct VolumeRequest {
    pub percent: u32,
}

#[derive(Debug, Serialize)]
pub struct SoundPackResponse {
    pub preset: String,
    pub display_name: String,
    pub available: Vec<SoundPackOption>,
}

#[derive(Debug, Serialize)]
pub struct Cs2RootResponse {
    pub found: bool,
    pub path: Option<String>,
}

#[derive(Clone, Copy, Debug, Serialize)]
pub struct SoundPackOption {
    pub preset: &'static str,
    pub display_name: &'static str,
}

const SOUND_PACK_OPTIONS: &[SoundPackOption] = &[
    SoundPackOption {
        preset: "crossfire_swat_gr",
        display_name: "swat GR",
    },
    SoundPackOption {
        preset: "crossfire_swat_bl",
        display_name: "swat BL",
    },
    SoundPackOption {
        preset: "crossfire_flying_tiger_gr",
        display_name: "tiger GR",
    },
    SoundPackOption {
        preset: "crossfire_flying_tiger_bl",
        display_name: "tiger BL",
    },
    SoundPackOption {
        preset: "crossfire_v_sex",
        display_name: "cfsex",
    },
    SoundPackOption {
        preset: "crossfire_women_gr",
        display_name: "women GR",
    },
    SoundPackOption {
        preset: "crossfire_women_bl",
        display_name: "women BL",
    },
];

pub async fn health() -> Json<HealthResponse> {
    Json(HealthResponse {
        ok: true,
        service: "kill-confirm-gamebar",
    })
}

pub async fn gsi_status(State(app_state): State<Arc<AppState>>) -> Json<GsiStatusResponse> {
    let now = unix_time_ms();
    let last_post = zero_to_none(app_state.last_gsi_post_unix_ms.load(Ordering::Relaxed));
    Json(GsiStatusResponse {
        posts: app_state.gsi_posts.load(Ordering::Relaxed),
        parse_errors: app_state.gsi_parse_errors.load(Ordering::Relaxed),
        last_post_unix_ms: last_post,
        last_post_age_ms: last_post.map(|value| now.saturating_sub(value)),
        last_parse_error_unix_ms: zero_to_none(
            app_state
                .last_gsi_parse_error_unix_ms
                .load(Ordering::Relaxed),
        ),
    })
}

pub async fn cs2_root() -> Json<Cs2RootResponse> {
    let path = detect_cs2_root();
    Json(Cs2RootResponse {
        found: path.is_some(),
        path: path.map(|value| value.display().to_string()),
    })
}

pub async fn shutdown(State(app_state): State<Arc<AppState>>) -> Json<HealthResponse> {
    let _ = app_state.shutdown_tx.send(());
    Json(HealthResponse {
        ok: true,
        service: "kill-confirm-gamebar",
    })
}

pub async fn audio_reload(
    State(app_state): State<Arc<AppState>>,
) -> Result<Json<HealthResponse>, (axum::http::StatusCode, String)> {
    service_log("audio reload requested");
    let output_stream = get_output_stream(&app_state.args.device).map_err(|error| {
        (
            axum::http::StatusCode::INTERNAL_SERVER_ERROR,
            error.to_string(),
        )
    })?;

    {
        let mut stream_handle = app_state.stream_handle.write().await;
        *stream_handle = output_stream;
    }

    service_log("audio output stream reloaded");
    Ok(Json(HealthResponse {
        ok: true,
        service: "kill-confirm-gamebar",
    }))
}

pub async fn audio_volume(
    State(app_state): State<Arc<AppState>>,
    Json(request): Json<VolumeRequest>,
) -> Json<HealthResponse> {
    let percent = request.percent.min(200);
    app_state.volume_percent.store(percent, Ordering::Relaxed);
    service_log(&format!("audio volume set to {percent}%"));

    Json(HealthResponse {
        ok: true,
        service: "kill-confirm-gamebar",
    })
}

pub async fn soundpack(State(app_state): State<Arc<AppState>>) -> Json<SoundPackResponse> {
    let preset = app_state.preset.read().await;
    Json(soundpack_response(&preset.preset_name))
}

pub async fn set_soundpack(
    State(app_state): State<Arc<AppState>>,
    Json(request): Json<SoundPackRequest>,
) -> Result<Json<SoundPackResponse>, (axum::http::StatusCode, String)> {
    let preset_name = resolve_soundpack_alias(&request.preset).ok_or_else(|| {
        (
            axum::http::StatusCode::BAD_REQUEST,
            "unsupported sound pack".to_string(),
        )
    })?;
    let preset = Preset::load(preset_name)
        .map_err(|error| (axum::http::StatusCode::BAD_REQUEST, error.to_string()))?;

    {
        let mut current = app_state.preset.write().await;
        *current = preset;
    }

    Ok(Json(soundpack_response(preset_name)))
}

pub async fn events_ws(
    ws: WebSocketUpgrade,
    State(app_state): State<Arc<AppState>>,
) -> impl IntoResponse {
    let rx = app_state.event_tx.subscribe();
    ws.on_upgrade(move |socket| send_events(socket, rx))
}

pub async fn test_event(
    Path(kill_count): Path<u16>,
    Query(query): Query<TestEventQuery>,
    State(app_state): State<Arc<AppState>>,
) -> Json<KillEvent> {
    let event = KillEvent {
        kill_count,
        is_headshot: query.headshot.unwrap_or(false),
        is_knife_kill: query.knife.unwrap_or(false),
        is_first_kill: query.first.unwrap_or(false),
        is_last_kill: query.last.unwrap_or(false),
        play_main_animation: query.main.unwrap_or(true),
        player_name: query
            .player_name
            .unwrap_or_else(|| "Test Player".to_string()),
        steamid: query.steamid.unwrap_or_else(|| "test".to_string()),
    };

    let _ = app_state.event_tx.send(event.clone());

    if query.audio.unwrap_or(false) {
        let app_state_clone = app_state.clone();
        let event_clone = event.clone();
        tokio::spawn(async move {
            let result = play_audio(
                app_state_clone,
                event_clone.kill_count,
                event_clone.is_headshot,
                event_clone.is_first_kill,
                event_clone.is_knife_kill,
                event_clone.is_last_kill,
                event_clone.play_main_animation,
            )
            .await;

            if let Err(error) = result {
                error!("failed to play test audio: {error}");
            }
        });
    }

    Json(event)
}

fn resolve_soundpack_alias(value: &str) -> Option<&'static str> {
    let normalized = value.trim().to_ascii_lowercase();
    match normalized.as_str() {
        "cf" | "crossfire" | "swatgr" | "swat_gr" | "crossfire_swat_gr" => {
            Some("crossfire_swat_gr")
        }
        "swatbl" | "swat_bl" | "crossfire_swat_bl" => Some("crossfire_swat_bl"),
        "cfftgr"
        | "ftgr"
        | "tiger_gr"
        | "flying_tiger_gr"
        | "crossfire_flying_tiger_gr"
        | "cffhd"
        | "cf_fhd"
        | "crossfire_fhd"
        | "crossfire_v_fhd" => Some("crossfire_flying_tiger_gr"),
        "cfsex" | "cf_sex" | "crossfire_sex" | "crossfire_v_sex" => Some("crossfire_v_sex"),
        "cfftbl" | "ftbl" | "tiger_bl" | "flying_tiger_bl" | "crossfire_flying_tiger_bl" => {
            Some("crossfire_flying_tiger_bl")
        }
        "cwgr" | "women_gr" | "crossfire_women_gr" | "kkgr" | "knifegr" | "knifekill_gr" => {
            Some("crossfire_women_gr")
        }
        "cwbl" | "women_bl" | "crossfire_women_bl" | "kkbl" | "knifebl" | "knifekill_bl" => {
            Some("crossfire_women_bl")
        }
        _ => None,
    }
}

fn soundpack_response(preset_name: &str) -> SoundPackResponse {
    SoundPackResponse {
        preset: preset_name.to_string(),
        display_name: soundpack_display_name(preset_name).to_string(),
        available: SOUND_PACK_OPTIONS.to_vec(),
    }
}

fn soundpack_display_name(preset_name: &str) -> &'static str {
    SOUND_PACK_OPTIONS
        .iter()
        .find(|option| option.preset == preset_name)
        .map(|option| option.display_name)
        .unwrap_or("custom")
}

fn detect_cs2_root() -> Option<PathBuf> {
    for library_root in steam_library_roots() {
        let cs2_root = library_root
            .join("steamapps")
            .join("common")
            .join("Counter-Strike Global Offensive");
        if cs2_root.join("game").join("csgo").join("cfg").is_dir() {
            return Some(cs2_root);
        }
    }

    None
}

fn steam_library_roots() -> Vec<PathBuf> {
    let mut roots = Vec::new();

    for root in steam_roots() {
        push_unique_path(&mut roots, root.clone());

        let library_folders = root.join("steamapps").join("libraryfolders.vdf");
        if let Ok(text) = fs::read_to_string(library_folders) {
            for path in parse_steam_library_paths(&text) {
                push_unique_path(&mut roots, path);
            }
        }
    }

    roots
}

fn steam_roots() -> Vec<PathBuf> {
    let mut roots = Vec::new();

    for value_name in ["SteamPath", "InstallPath"] {
        for key in [
            r"HKCU\Software\Valve\Steam",
            r"HKLM\Software\WOW6432Node\Valve\Steam",
            r"HKLM\Software\Valve\Steam",
        ] {
            if let Some(path) = query_registry_string(key, value_name) {
                push_unique_path(&mut roots, PathBuf::from(path.replace('/', "\\")));
            }
        }
    }

    if let Some(program_files_x86) = std::env::var_os("ProgramFiles(x86)") {
        push_unique_path(&mut roots, PathBuf::from(program_files_x86).join("Steam"));
    }

    roots
}

fn query_registry_string(key: &str, value_name: &str) -> Option<String> {
    let output = Command::new("reg")
        .args(["query", key, "/v", value_name])
        .output()
        .ok()?;

    if !output.status.success() {
        return None;
    }

    let text = String::from_utf8_lossy(&output.stdout);
    for line in text.lines() {
        let trimmed = line.trim();
        if !trimmed.starts_with(value_name) {
            continue;
        }

        if let Some(index) = trimmed.find("REG_SZ") {
            let value = trimmed[index + "REG_SZ".len()..].trim();
            if !value.is_empty() {
                return Some(value.to_string());
            }
        }
    }

    None
}

fn parse_steam_library_paths(text: &str) -> Vec<PathBuf> {
    let mut paths = Vec::new();

    for line in text.lines() {
        let trimmed = line.trim();
        if !(trimmed.starts_with("\"path\"") || starts_with_quoted_number(trimmed)) {
            continue;
        }

        let quoted: Vec<&str> = trimmed.split('"').collect();
        if quoted.len() >= 4 {
            let value = quoted[3].replace("\\\\", "\\");
            if !value.is_empty() {
                paths.push(PathBuf::from(value));
            }
        }
    }

    paths
}

fn starts_with_quoted_number(value: &str) -> bool {
    let Some(rest) = value.strip_prefix('"') else {
        return false;
    };
    let Some((number, _)) = rest.split_once('"') else {
        return false;
    };
    !number.is_empty() && number.chars().all(|ch| ch.is_ascii_digit())
}

fn push_unique_path(paths: &mut Vec<PathBuf>, path: PathBuf) {
    if !path.exists() {
        return;
    }

    let normalized = path.to_string_lossy();
    if !paths
        .iter()
        .any(|existing| existing.to_string_lossy().eq_ignore_ascii_case(&normalized))
    {
        paths.push(path);
    }
}

fn unix_time_ms() -> u64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|value| value.as_millis() as u64)
        .unwrap_or(0)
}

fn zero_to_none(value: u64) -> Option<u64> {
    if value == 0 { None } else { Some(value) }
}

async fn send_events(mut socket: WebSocket, mut rx: broadcast::Receiver<KillEvent>) {
    debug!("kill event websocket connected");

    loop {
        let event = match rx.recv().await {
            Ok(event) => event,
            Err(broadcast::error::RecvError::Lagged(skipped)) => {
                warn!("kill event websocket skipped {skipped} stale events");
                continue;
            }
            Err(broadcast::error::RecvError::Closed) => break,
        };

        let payload = match serde_json::to_string(&event) {
            Ok(payload) => payload,
            Err(error) => {
                warn!("failed to serialize kill event: {error}");
                continue;
            }
        };

        if socket.send(Message::Text(payload.into())).await.is_err() {
            break;
        }
    }

    debug!("kill event websocket disconnected");
}
