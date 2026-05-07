use anyhow::{Context, Result};
use std::collections::HashMap;
use std::fs;
use std::path::Path;

use super::lua_script::LuaScript;

/// Preset holds the loaded Lua script for a soundpack
pub struct Preset {
    pub lua_script: LuaScript,
    pub preset_name: String,
    pub display_name: String,
    pub master_name: String,
    pub variant: Option<String>,
    pub base_dir: String,
}

impl Preset {
    /// Load a preset from the sounds directory
    /// For variants like "crossfire_v_sex", loads Lua from master "crossfire"
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
            display_name: preset_name.to_string(),
            master_name: master_name.to_string(),
            variant: variant.map(|s| s.to_string()),
            base_dir: format!("sounds/{preset_name}"),
        })
    }

    pub fn load_custom(preset_name: &str, display_name: &str, folder_path: &str) -> Result<Self> {
        let script_path = format!("{folder_path}/sound.lua");
        let lua_script = if Path::new(&script_path).exists() {
            LuaScript::load(&script_path)
                .with_context(|| format!("failed to load Lua script for custom preset '{display_name}'"))?
        } else {
            let generated_script = build_generated_voice_lua(folder_path);
            LuaScript::from_source(&generated_script, &script_path).with_context(|| {
                format!("failed to generate Lua script for custom preset '{display_name}'")
            })?
        };

        Ok(Self {
            lua_script,
            preset_name: preset_name.to_string(),
            display_name: display_name.to_string(),
            master_name: preset_name.to_string(),
            variant: None,
            base_dir: folder_path.replace('\\', "/"),
        })
    }
}

fn build_generated_voice_lua(folder_path: &str) -> String {
    let known_files = [
        ("common_overlay.wav", "common_overlay"),
        ("common.wav", "common"),
        ("2.wav", "2"),
        ("3.wav", "3"),
        ("4.wav", "4"),
        ("5.wav", "5"),
        ("6.wav", "6"),
        ("7.wav", "7"),
        ("8.wav", "8"),
        ("headshot.wav", "headshot"),
        ("knife.wav", "knife"),
        ("firstandlast.wav", "firstandlast"),
    ];

    let available_entries = known_files
        .iter()
        .filter_map(|(file_name, key)| {
            let path = Path::new(folder_path).join(file_name);
            if path.exists() {
                Some(format!("[\"{key}\"] = true"))
            } else {
                None
            }
        })
        .collect::<Vec<_>>()
        .join(",\n    ");

    format!(
        "function get_sounds(ctx)\n\
         \tlocal sounds = {{}}\n\
         \tlocal base = ctx.base_dir .. \"/\"\n\
         \tlocal available = {{\n    {available_entries}\n\t}}\n\n\
         \tlocal common_overlay_played = false\n\n\
         \tlocal function add_if_present(name)\n\
         \t\tif available[name] then\n\
         \t\t\ttable.insert(sounds, base .. name .. \".wav\")\n\
         \t\tend\n\
         \tend\n\n\
         \tlocal function add_common_overlay_if_present()\n\
         \t\tif common_overlay_played then\n\
         \t\t\treturn\n\
         \t\tend\n\
         \t\tif available[\"common_overlay\"] then\n\
         \t\t\tcommon_overlay_played = true\n\
         \t\t\ttable.insert(sounds, base .. \"common_overlay.wav\")\n\
         \t\tend\n\
         \tend\n\n\
         \tif ctx.is_first_kill or ctx.is_last_kill then\n\
         \t\tadd_if_present(\"firstandlast\")\n\
         \t\tadd_common_overlay_if_present()\n\
         \tend\n\n\
         \tif ctx.play_main_audio and ctx.kill_count >= 2 then\n\
         \t\tlocal voiced_kill_count = math.min(ctx.kill_count, 8)\n\
         \t\tadd_if_present(tostring(voiced_kill_count))\n\
         \t\tadd_common_overlay_if_present()\n\
         \telseif ctx.is_knife_kill then\n\
         \t\tadd_if_present(\"knife\")\n\
         \t\tadd_common_overlay_if_present()\n\
         \telseif ctx.is_headshot then\n\
         \t\tadd_if_present(\"headshot\")\n\
         \t\tadd_common_overlay_if_present()\n\
         \telseif ctx.play_main_audio and ctx.kill_count == 1 then\n\
         \t\tadd_if_present(\"common\")\n\
         \t\tadd_common_overlay_if_present()\n\
         \tend\n\n\
         \treturn sounds\n\
         end\n"
    )
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
