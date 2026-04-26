use std::{io::BufReader, sync::Arc};

use anyhow::{Context, Result};
use rodio::{Source, mixer};
use tokio::fs::File as TokioFile;
use tokio::task::JoinSet;
use tracing::{error, info};

use crate::soundpack::SoundContext;
use crate::util::state::AppState;

const KNIFE_SOUND_GAIN: f32 = 0.16;
const FIRST_AND_LAST_SOUND_GAIN: f32 = 0.16;
const HEADSHOT_SOUND_GAIN: f32 = 1.8;
const MAX_STREAK_EVENT_GAIN: f32 = 1.35;

async fn add_file_to_mixer(file_name: &str, mixer: &mixer::Mixer, event_gain: f32) -> Result<()> {
    let file = TokioFile::open(file_name)
        .await
        .with_context(|| format!("failed to open file: {file_name}"))?;
    let sync_file = file.into_std().await;
    let source = rodio::Decoder::new(BufReader::new(sync_file))
        .with_context(|| format!("failed to decode file: {file_name:?}"))?;
    mixer.add(source.amplify(resolve_sound_gain(file_name, event_gain)));
    Ok(())
}

pub async fn play_audio(
    app_state_clone: Arc<AppState>,
    kill_count: u16,
    is_headshot: bool,
    is_first_kill: bool,
    is_knife_kill: bool,
    is_last_kill: bool,
    play_main_audio: bool,
) -> Result<()> {
    let args = &app_state_clone.args;
    let stream_handle = &app_state_clone.stream_handle;
    let volume = args.volume;

    let mixer = stream_handle.mixer().to_owned();

    let sound_files = {
        let preset = app_state_clone.preset.read().await;

        // Create context for Lua script
        let ctx = SoundContext {
            kill_count,
            is_headshot,
            is_first_kill,
            is_knife_kill,
            is_last_kill,
            play_main_audio,
            preset_name: preset.preset_name.clone(),
            master_name: preset.master_name.clone(),
            variant: preset.variant.clone(),
        };

        // Get sound files from Lua script
        preset
            .lua_script
            .get_sounds(&ctx)
            .with_context(|| "failed to get sounds from Lua script".to_string())?
    };

    info!(
        "Lua returned {} sound files: {:?}",
        sound_files.len(),
        sound_files
    );

    let event_gain = resolve_event_gain(kill_count, play_main_audio);

    let mut tasks = JoinSet::new();

    for file_path in sound_files {
        let mixer_clone = mixer.clone();
        tasks.spawn(async move { add_file_to_mixer(&file_path, &mixer_clone, event_gain).await });
    }

    tokio::task::spawn_blocking(move || {
        let sink = rodio::Sink::connect_new(&mixer);
        sink.set_volume(volume);
        sink.play();
        sink.sleep_until_end();
    });

    let results = tasks.join_all().await;

    results.iter().for_each(|result| {
        if let Err(e) = result {
            error!("Failed to add file to mixer: {}", e);
        }
    });

    Ok(())
}

fn resolve_sound_gain(file_name: &str, event_gain: f32) -> f32 {
    let normalized = file_name.replace('\\', "/").to_ascii_lowercase();

    if normalized.ends_with("/knife.wav") {
        return KNIFE_SOUND_GAIN * event_gain;
    }

    if normalized.ends_with("/firstandlast.wav") {
        return FIRST_AND_LAST_SOUND_GAIN * event_gain;
    }

    if normalized.ends_with("/headshot.wav") {
        return HEADSHOT_SOUND_GAIN * event_gain;
    }

    event_gain
}

fn resolve_event_gain(kill_count: u16, play_main_audio: bool) -> f32 {
    if !play_main_audio || kill_count <= 1 {
        return 1.0;
    }

    let streak_bonus = ((kill_count - 1) as f32) * 0.07;
    (1.0 + streak_bonus).min(MAX_STREAK_EVENT_GAIN)
}
