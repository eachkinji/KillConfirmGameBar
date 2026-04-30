use std::sync::atomic::AtomicU64;
use std::time::Instant;

use rodio::OutputStream;
use serde::Serialize;
use tokio::sync::{RwLock, broadcast};

use crate::soundpack::Preset;

use super::Args;

pub struct Mutable {
    pub initialized: bool,
    pub steamid: String,
    pub ply_kills: u16,
    pub ply_hs_kills: u64,
    pub last_active_weapon_is_knife: bool,
    pub last_active_weapon_seen_at: Option<Instant>,
    pub current_round: u8,
    pub last_round_phase: Option<TrackedRoundPhase>,
    pub has_first_kill_in_round: bool,
    pub pending_last_kill: Option<PendingLastKill>,
}

#[derive(Clone, Debug, Serialize)]
pub struct KillEvent {
    pub kill_count: u16,
    pub is_headshot: bool,
    pub is_knife_kill: bool,
    pub is_first_kill: bool,
    pub is_last_kill: bool,
    pub play_main_animation: bool,
    pub player_name: String,
    pub steamid: String,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum TrackedRoundPhase {
    FreezeTime,
    Live,
    Over,
}

#[derive(Clone, Copy, Debug)]
pub struct PendingLastKill {
    pub recorded_at: Instant,
    pub kill_count: u16,
    pub is_headshot: bool,
    pub is_first_kill: bool,
    pub is_knife_kill: bool,
}

pub struct AppState {
    pub mutable: RwLock<Mutable>,
    pub stream_handle: OutputStream,
    pub args: Args,
    pub preset: RwLock<Preset>,
    pub event_tx: broadcast::Sender<KillEvent>,
    pub shutdown_tx: broadcast::Sender<()>,
    pub gsi_posts: AtomicU64,
    pub gsi_parse_errors: AtomicU64,
    pub last_gsi_post_unix_ms: AtomicU64,
    pub last_gsi_parse_error_unix_ms: AtomicU64,
}
