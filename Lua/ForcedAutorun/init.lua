if CLIENT then
    Game.AddCommand("mcm_reload", "reloads config", function ()
        dofile("Mods/Multiplayer crew manager/Lua/main.lua")
    end)

    dofile("Mods/Multiplayer crew manager/Lua/main.lua")
end