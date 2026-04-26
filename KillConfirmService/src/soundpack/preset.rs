use anyhow::{Context, Result};
use std::collections::HashMap;
use std::fs;

use super::lua_script::LuaScript;

/// Preset holds the loaded Lua script for a soundpack
pub struct Preset {
    pub lua_script: LuaScript,
    pub preset_name: String,
    pub master_name: String,
    pub variant: Option<String>,
}

impl Preset {
    /// Load a preset from the sounds directory
    /// For variants like "crossfire_v_fhd", loads Lua from master "crossfire"
    pub fn load(preset_name: &str) -> Result<Self> {
        // Check if this is a variant (format: master_v_variant)
        let parts: Vec<&str> = preset_name.split("_v_").collect();
        let (master_name, variant) = if parts.len() > 1 {
            (parts[0], Some(parts[1..].join("_v_")))
        } else {
            (preset_name, None)
        };

        // Load Lua script from the selected soundpack when present. Variants may still fall back
        // to the master pack for older packages that only shipped one shared script.
        let own_script_path = format!("sounds/{preset_name}/sound.lua");
        let master_script_path = format!("sounds/{master_name}/sound.lua");
        let script_path = if fs::metadata(&own_script_path).is_ok() {
            own_script_path
        } else {
            master_script_path
        };
        let lua_script = LuaScript::load(&script_path)
            .with_context(|| format!("failed to load Lua script for preset '{preset_name}'"))?;

        Ok(Self {
            lua_script,
            preset_name: preset_name.to_string(),
            master_name: master_name.to_string(),
            variant: variant.map(|s| s.to_string()),
        })
    }
}

pub fn list() -> Result<()> {
    let path = fs::read_dir("sounds")?;

    let mut mp: HashMap<String, Vec<String>> = HashMap::new();

    for path in path {
        let path = path?;
        let file_name = path.file_name().to_string_lossy().to_string();

        let preset: Vec<&str> = file_name.split("_v_").collect();

        let preset_name = preset[0].to_string();
        let variant = preset.get(1);

        if !mp.contains_key(preset_name.as_str()) {
            mp.insert(preset_name.clone(), vec![]);
        }

        if let Some(variant) = variant {
            mp.get_mut(preset_name.as_str())
                .unwrap()
                .push(variant.to_string());
        }
    }

    let mut keys: Vec<&String> = mp.keys().collect();
    keys.sort();

    for key in keys {
        let variants = mp.get(key).unwrap();
        if variants.is_empty() {
            println!("{key}");
            continue;
        }

        println!("{}: [{}]", key, variants.join(", "));
    }

    Ok(())
}
