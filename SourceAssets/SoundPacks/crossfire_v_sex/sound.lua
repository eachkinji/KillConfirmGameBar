-- sound.lua for crossfire_v_sex
-- This pack is self-contained and does not depend on the old crossfire master pack.

function get_sounds(ctx)
    local sounds = {}
    local base = "sounds/" .. ctx.preset_name .. "/"

    if ctx.play_main_audio then
        table.insert(sounds, base .. "common.wav")

        if ctx.kill_count >= 2 then
            local voiced_kill_count = math.min(ctx.kill_count, 8)
            table.insert(sounds, base .. voiced_kill_count .. ".wav")
        end
    end

    if ctx.is_headshot and ctx.kill_count == 1 then
        table.insert(sounds, base .. "headshot.wav")
    end

    if ctx.kill_count == 1 and ctx.is_knife_kill then
        table.insert(sounds, base .. "knife.wav")
    end

    if ctx.is_first_kill or ctx.is_last_kill then
        table.insert(sounds, base .. "firstandlast.wav")
    end

    return sounds
end
