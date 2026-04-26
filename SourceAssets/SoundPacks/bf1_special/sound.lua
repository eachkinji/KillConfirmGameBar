-- sound.lua for bf1_special soundpack
-- Has: common.wav, common_headshot.wav
-- Logic: Play common_headshot on headshot, else play common

function get_sounds(ctx)
    local sounds = {}
    local base = "sounds/" .. ctx.preset_name .. "/"
    
    if ctx.is_headshot then
        table.insert(sounds, base .. "common_headshot.wav")
    else
        table.insert(sounds, base .. "common.wav")
    end
    
    return sounds
end
