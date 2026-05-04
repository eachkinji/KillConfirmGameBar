use anyhow::Result;
use axum::body::Bytes;
use std::sync::Arc;
use std::sync::atomic::Ordering;
use std::time::{Duration, Instant, SystemTime, UNIX_EPOCH};

use axum::{extract::State, http::StatusCode, response::IntoResponse};
use gsi_cs2::Body;
use gsi_cs2::round::RoundPhase;
use gsi_cs2::weapon::{WeaponState, WeaponType};
use thiserror::Error;
use tracing::{error, info, warn};

use crate::soundpack::sound::play_audio;
use crate::util::logging::service_log;

use super::state::{AppState, KillEvent, PendingLastKill, TrackedRoundPhase};

// GSI is throttled to 100ms, so knife kills need a short history window.
const KNIFE_KILL_GRACE_WINDOW: Duration = Duration::from_millis(750);
const FINAL_KILL_GRACE_WINDOW: Duration = Duration::from_millis(1500);

#[derive(Error, Debug)]
pub enum ApiError {}

impl IntoResponse for ApiError {
    fn into_response(self) -> axum::response::Response {
        StatusCode::INTERNAL_SERVER_ERROR.into_response()
    }
}

pub async fn update(
    State(app_state): State<Arc<AppState>>,
    body: Bytes,
) -> Result<StatusCode, ApiError> {
    app_state.gsi_posts.fetch_add(1, Ordering::Relaxed);
    app_state
        .last_gsi_post_unix_ms
        .store(unix_time_ms(), Ordering::Relaxed);

    let data: Body = match parse_gsi_body(&body) {
        Ok(data) => data,
        Err(error) => {
            app_state.gsi_parse_errors.fetch_add(1, Ordering::Relaxed);
            app_state
                .last_gsi_parse_error_unix_ms
                .store(unix_time_ms(), Ordering::Relaxed);
            service_log(&format!(
                "GSI parse error: {error}; payload bytes={}",
                body.len()
            ));
            warn!("failed to parse GSI payload: {error}");
            return Ok(StatusCode::BAD_REQUEST);
        }
    };

    let map = data.map.as_ref();
    let player_data = data.player.as_ref();
    let round = data.round.as_ref();

    if map.is_none() || player_data.is_none() {
        warn!("map or player data is missing");
        return Ok(StatusCode::OK);
    }

    if let Some(whitelist) = &app_state.args.steamid {
        let steamid = player_data
            .as_ref()
            .unwrap()
            .steam_id
            .as_deref()
            .unwrap_or("");
        if steamid != whitelist {
            return Ok(StatusCode::OK);
        }
    }

    let ply = player_data.unwrap();
    let ply_state = ply.state.as_ref().unwrap();
    let now = Instant::now();
    let current_round = map.unwrap().round;
    let current_round_phase = round
        .map(|value| map_round_phase(&value.phase))
        .or_else(|| infer_round_phase_from_kills(ply_state.round_kills));
    let current_active_weapon_is_knife = ply
        .weapons
        .values()
        .find(|weapon| matches!(weapon.state, WeaponState::Active))
        .map(|weapon| matches!(weapon.r#type, Some(WeaponType::Knife)));

    let binding = app_state.mutable.read().await;
    let current_kills = ply_state.round_kills;
    let original_kills = binding.ply_kills;

    let current_hs_kills = ply_state.round_killhs;
    let origin_hs_kills = binding.ply_hs_kills;

    let is_initialized = binding.initialized;
    let original_steamid = binding.steamid.clone();
    let previous_round = binding.current_round;
    let previous_round_phase = binding.last_round_phase;
    let had_first_kill_in_round = binding.has_first_kill_in_round;
    let pending_last_kill = binding.pending_last_kill;
    let recent_weapon_is_knife = binding.last_active_weapon_is_knife
        && binding
            .last_active_weapon_seen_at
            .map(|seen_at| now.saturating_duration_since(seen_at) <= KNIFE_KILL_GRACE_WINDOW)
            .unwrap_or(false);
    drop(binding);

    let steamid = ply.steam_id.as_deref().unwrap_or("");
    let player_name = ply.name.as_deref().unwrap_or("").to_string();

    let round_changed = previous_round != current_round;
    let round_reset =
        round_changed || matches!(current_round_phase, Some(TrackedRoundPhase::FreezeTime));
    let phase_transition_to_over = previous_round_phase == Some(TrackedRoundPhase::Live)
        && current_round_phase == Some(TrackedRoundPhase::Over);
    let can_emit_kill = current_kills > original_kills
        && (steamid == original_steamid || original_steamid.is_empty());
    let first_kill_already_seen = if round_reset {
        false
    } else {
        had_first_kill_in_round
    };

    let should_clear_pending_last_kill = round_reset && !phase_transition_to_over;
    let mut pending_last_kill_for_next = if should_clear_pending_last_kill {
        None
    } else {
        pending_last_kill
    };
    let mut kill_event_to_send = None;
    let mut badge_only_event_to_send = None;

    if is_initialized && can_emit_kill {
        let is_headshot = current_hs_kills > origin_hs_kills;
        // Do not use the current frame here: players often switch to knife right after a gun kill.
        let is_knife_kill = recent_weapon_is_knife;
        let is_last_kill = phase_transition_to_over;
        let is_first_kill = !is_last_kill && !first_kill_already_seen;

        if is_last_kill {
            pending_last_kill_for_next = None;
        } else {
            pending_last_kill_for_next = Some(PendingLastKill {
                recorded_at: now,
                kill_count: current_kills,
                is_headshot,
                is_knife_kill,
            });
        }

        kill_event_to_send = Some(KillEvent {
            kill_count: current_kills,
            is_headshot,
            is_knife_kill,
            is_first_kill,
            is_last_kill,
            play_main_animation: true,
            player_name: player_name.clone(),
            steamid: steamid.to_string(),
        });

        let app_state_clone = app_state.clone();

        tokio::spawn(async move {
            let result = play_audio(
                app_state_clone,
                current_kills,
                is_headshot,
                is_first_kill,
                is_knife_kill,
                is_last_kill,
                true,
            )
            .await;

            if let Err(e) = result {
                error!("Failed to play audio: {}", e);
            }
        });
        info!(
            "player: {}, kills: {}, headshot: {}, knife: {}, first: {}, last: {}",
            ply.name.as_deref().unwrap_or(""),
            current_kills,
            is_headshot,
            is_knife_kill,
            is_first_kill,
            is_last_kill
        );
    } else if is_initialized && phase_transition_to_over {
        if let Some(pending_last_kill) = pending_last_kill {
            if now.saturating_duration_since(pending_last_kill.recorded_at)
                <= FINAL_KILL_GRACE_WINDOW
            {
                badge_only_event_to_send = Some(KillEvent {
                    kill_count: pending_last_kill.kill_count,
                    is_headshot: pending_last_kill.is_headshot,
                    is_knife_kill: pending_last_kill.is_knife_kill,
                    is_first_kill: false,
                    is_last_kill: true,
                    play_main_animation: pending_last_kill.kill_count == 1
                        && pending_last_kill.is_headshot,
                    player_name: player_name.clone(),
                    steamid: steamid.to_string(),
                });
                info!(
                    "player: {}, resolved delayed final kill for round kill {}",
                    ply.name.as_deref().unwrap_or(""),
                    pending_last_kill.kill_count
                );

                let app_state_clone = app_state.clone();
                let kill_count = pending_last_kill.kill_count;
                tokio::spawn(async move {
                    let result = play_audio(
                        app_state_clone,
                        kill_count,
                        pending_last_kill.is_headshot,
                        false,
                        pending_last_kill.is_knife_kill,
                        true,
                        false,
                    )
                    .await;

                    if let Err(e) = result {
                        error!("Failed to play audio: {}", e);
                    }
                });
            }

            pending_last_kill_for_next = None;
        }
    }

    let mut binding = app_state.mutable.write().await;

    if !binding.initialized {
        binding.initialized = true;
    }

    binding.ply_kills = current_kills;
    binding.ply_hs_kills = current_hs_kills;
    binding.steamid = steamid.to_string();
    binding.current_round = current_round;
    binding.last_round_phase = current_round_phase;
    binding.has_first_kill_in_round =
        current_kills > 0 || (!round_reset && had_first_kill_in_round) || can_emit_kill;
    binding.pending_last_kill = pending_last_kill_for_next;
    if let Some(is_knife) = current_active_weapon_is_knife {
        binding.last_active_weapon_is_knife = is_knife;
        binding.last_active_weapon_seen_at = Some(now);
    }

    drop(binding);

    if let Some(kill_event) = kill_event_to_send {
        let _ = app_state.event_tx.send(kill_event);
    }

    if let Some(badge_only_event) = badge_only_event_to_send {
        let _ = app_state.event_tx.send(badge_only_event);
    }

    Ok(StatusCode::OK)
}

fn parse_gsi_body(body: &[u8]) -> serde_json::Result<Body> {
    match serde_json::from_slice(body) {
        Ok(data) => return Ok(data),
        Err(error) if !error.to_string().contains("missing field `auth`") => return Err(error),
        Err(_) => {}
    }

    let mut value: serde_json::Value = serde_json::from_slice(body)?;

    if let Some(object) = value.as_object_mut() {
        object.entry("auth").or_insert_with(|| {
            serde_json::json!({
                "token": "killconfirm"
            })
        });
    }

    serde_json::from_value(value)
}

fn unix_time_ms() -> u64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|value| value.as_millis() as u64)
        .unwrap_or(0)
}

fn map_round_phase(phase: &RoundPhase) -> TrackedRoundPhase {
    match phase {
        RoundPhase::Live => TrackedRoundPhase::Live,
        RoundPhase::FreezeTime => TrackedRoundPhase::FreezeTime,
        RoundPhase::Over => TrackedRoundPhase::Over,
    }
}

fn infer_round_phase_from_kills(current_kills: u16) -> Option<TrackedRoundPhase> {
    if current_kills == 0 {
        return Some(TrackedRoundPhase::FreezeTime);
    }

    None
}
