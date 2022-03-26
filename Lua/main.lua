--[[
    TODO:
        - test situational talents
        - test mission DataRewards errors, if they exist
]]--

local enabled = Game.GetEnabledContentPackages()
local isEnabled = false
for key, value in pairs(enabled) do
    if value.Name == "Multiplayer crew manager" then
        isEnabled = true
        break
    end
end
if not isEnabled then return end

dofile("Mods/Multiplayer crew manager/Lua/common.lua")

if SERVER then
    dofile("Mods/Multiplayer crew manager/Lua/server/config.lua")
    dofile("Mods/Multiplayer crew manager/Lua/server/chat_commands.lua")
    dofile("Mods/Multiplayer crew manager/Lua/server/save_impl.lua")
    dofile("Mods/Multiplayer crew manager/Lua/server/control.lua")
    dofile("Mods/Multiplayer crew manager/Lua/server/session_logic.lua")
    dofile("Mods/Multiplayer crew manager/Lua/server/server_side.lua")
else
    dofile("Mods/Multiplayer crew manager/Lua/client/client_side.lua")
end