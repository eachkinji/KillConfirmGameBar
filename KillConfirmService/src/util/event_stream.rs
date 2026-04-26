use std::sync::Arc;

use axum::{
    extract::{
        Path, Query,
        State,
        ws::{Message, WebSocket, WebSocketUpgrade},
    },
    Json,
    response::IntoResponse,
};
use serde::{Deserialize, Serialize};
use tokio::sync::broadcast;
use tracing::{debug, error, warn};

use crate::soundpack::Preset;
use crate::soundpack::sound::play_audio;

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

#[derive(Debug, Deserialize)]
pub struct SoundPackRequest {
    pub preset: String,
}

#[derive(Debug, Serialize)]
pub struct SoundPackResponse {
    pub preset: String,
    pub display_name: String,
    pub available: Vec<SoundPackOption>,
}

#[derive(Clone, Copy, Debug, Serialize)]
pub struct SoundPackOption {
    pub preset: &'static str,
    pub display_name: &'static str,
}

const SOUND_PACK_OPTIONS: &[SoundPackOption] = &[
    SoundPackOption {
        preset: "crossfire",
        display_name: "cf",
    },
    SoundPackOption {
        preset: "crossfire_v_fhd",
        display_name: "cffhd",
    },
    SoundPackOption {
        preset: "crossfire_v_sex",
        display_name: "cfsex",
    },
];

pub async fn health() -> Json<HealthResponse> {
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
    let preset_name = resolve_soundpack_alias(&request.preset)
        .ok_or_else(|| (axum::http::StatusCode::BAD_REQUEST, "unsupported sound pack".to_string()))?;
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
        "cf" | "crossfire" => Some("crossfire"),
        "cffhd" | "cf_fhd" | "crossfire_fhd" | "crossfire_v_fhd" => Some("crossfire_v_fhd"),
        "cfsex" | "cf_sex" | "crossfire_sex" | "crossfire_v_sex" => Some("crossfire_v_sex"),
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
