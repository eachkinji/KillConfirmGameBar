-- sound.lua for crossfire_flying_tiger_gr
-- Has: common.wav, 2.wav through 8.wav, headshot.wav, knife.wav, grenade.wav
-- Always plays common.wav, plus one highest-priority event voice.
-- Priority: first/last > knife > streak > headshot.
-- grenade.wav is shared by first kill and last kill.

function get_sounds(ctx)
    local sounds = {}
    local base = "sounds/" .. ctx.preset_name .. "/"

    if ctx.play_main_audio then
        table.insert(sounds, base .. "common.wav")

        if ctx.is_first_kill or ctx.is_last_kill then
            table.insert(sounds, base .. "grenade.wav")
        elseif ctx.is_knife_kill then
            table.insert(sounds, base .. "knife.wav")
        elseif ctx.kill_count >= 2 then
            local voiced_kill_count = math.min(ctx.kill_count, 8)
            table.insert(sounds, base .. voiced_kill_count .. ".wav")
        elseif ctx.is_headshot then
            table.insert(sounds, base .. "headshot.wav")
        end
    end

    return sounds
end
