use anyhow::{Context, Result};
use mlua::{Lua, LuaSerdeExt, Value};
use serde::Serialize;
use std::fs;

/// Context passed to Lua script for sound selection
#[derive(Serialize, Clone, Debug)]
pub struct SoundContext {
    pub kill_count: u16,
    pub is_headshot: bool,
    pub is_first_kill: bool,
    pub is_knife_kill: bool,
    pub is_last_kill: bool,
    pub play_main_audio: bool,
    pub preset_name: String,
    pub master_name: String,
    pub variant: Option<String>,
    pub base_dir: String,
}

/// Holds a compiled Lua script for a soundpack
pub struct LuaScript {
    lua: Lua,
    script_path: String,
}

impl LuaScript {
    /// Load a Lua script from the given path
    pub fn load(script_path: &str) -> Result<Self> {
        let lua = Lua::new();
        let script_content = fs::read_to_string(script_path)
            .with_context(|| format!("failed to read Lua script: {script_path}"))?;

        lua.load(&script_content)
            .exec()
            .with_context(|| format!("failed to execute Lua script: {script_path}"))?;

        Ok(Self {
            lua,
            script_path: script_path.to_string(),
        })
    }

    /// Call the get_sounds function in the Lua script with the given context
    pub fn get_sounds(&self, ctx: &SoundContext) -> Result<Vec<String>> {
        let globals = self.lua.globals();
        let get_sounds: mlua::Function = globals
            .get("get_sounds")
            .with_context(|| format!("get_sounds function not found in {}", self.script_path))?;

        let ctx_value = self
            .lua
            .to_value(ctx)
            .context("failed to convert context to Lua value")?;

        let result: Value = get_sounds
            .call(ctx_value)
            .with_context(|| format!("failed to call get_sounds in {}", self.script_path))?;

        // Convert Lua table to Vec<String>
        let sounds = match result {
            Value::Table(table) => {
                let mut sounds = Vec::new();
                for sound in table.sequence_values::<String>() {
                    sounds.push(sound.context("invalid sound path in Lua return value")?);
                }
                sounds
            }
            Value::Nil => Vec::new(),
            _ => anyhow::bail!("get_sounds must return a table or nil"),
        };

        Ok(sounds)
    }
}
