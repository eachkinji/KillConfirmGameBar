-- sound.lua for crossfire soundpack (handles variants too)
-- ctx.preset_name = full name like "crossfire" or "crossfire_v_fhd"
-- ctx.variant = nil for master, or "fhd"/"sex" for variants
--
-- Logic:
--   common.wav always from MASTER (sounds/crossfire/)
--   numbered + headshot from preset_name folder (variant or master)
--   knife + first/last badge sounds from MASTER

function get_sounds(ctx)
    local sounds = {}
    
    -- Base path for variant-specific files (numbered, headshot)
    local base = "sounds/" .. ctx.preset_name .. "/"
    
    -- Master base for common.wav
    local master_base = "sounds/" .. ctx.master_name .. "/"
    
    if ctx.play_main_audio then
        -- Always play common sound from MASTER
        table.insert(sounds, master_base .. "common.wav")
        
        -- Play kill number sound (2-8) from preset folder
        if ctx.kill_count >= 2 then
            local voiced_kill_count = math.min(ctx.kill_count, 8)
            table.insert(sounds, base .. voiced_kill_count .. ".wav")
        end
    end

    -- All 1-kill headshots use the headshot voice, including silver and gold.
    if ctx.is_headshot and ctx.kill_count == 1 then
        table.insert(sounds, base .. "headshot.wav")
    end

    if ctx.kill_count == 1 and ctx.is_knife_kill then
        table.insert(sounds, master_base .. "knife.wav")
    end

    if ctx.is_first_kill or ctx.is_last_kill then
        table.insert(sounds, master_base .. "firstandlast.wav")
    end
    
    return sounds
end
