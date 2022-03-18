local function writeConfig()
    local configStr = string.format(
[[-- Warning: this file is generated, avoid editing directly
mcm_config = {}

-- how fast server self-updates character control state (60 - 1 second, 1 - 1/60th of second)
mcm_config.server_update_frequency = %s
-- do spawn clients after a campaign start
mcm_config.allow_spawn_new_clients = %s
-- is respawing allowed
mcm_config.allow_respawns = %s
-- respawn penalty
mcm_config.respawn_penalty = %s
-- respawn delay in seconds
mcm_config.respawn_delay = %s
-- time to get to destination in respawn shuttle in seconds
mcm_config.respawn_time = %s
]],
    tostring(mcm_config.server_update_frequency),
    tostring(mcm_config.allow_spawn_new_clients),
    tostring(mcm_config.allow_respawns),
    tostring(mcm_config.respawn_penalty),
    tostring(mcm_config.respawn_delay),
    tostring(mcm_config.respawn_time))

    File.Write([[Mods/Multiplayer crew manager/Lua/server/config.lua]], configStr)
end


function mcm_chat_commands(msg, sender)
    local is multCampaign = Game.GameSession ~= nil and Game.GameSession.GameMode ~= nil and Game.GameSession.GameMode.GetType() == MultiPlayerCampaign
    if not multCampaign then return end

    local response = nil
    local messageType = ChatMessageType.ServerMessageBox

    if msg:match('^mcm.*$') then
        -- check for in-game commands
        local isInGame = multCampaign and Game.GameSession.IsRunning
        -- swich command type
        if msg:match('^mcm%s*$') or msg:match('^mcm%s+help%s*$') then
            response = [[
mcm [function] [args] - mcm Function syntax

— mcm [help] - show help for mcm function (help keyword is optional)
— mcm list - list all controllable characters
— mcm control <ID> - try gain control of character with provided ID
— mcm release [ID] - relese currently controlled character or character with ID, if provided (latter only for admin)
]]
            if sender.HasPermission(ClientPermissions.ConsoleCommands) then response = response .. [[

admin only commands

— mcm delete <ID> - delete character (with inventory) with provided ID
— mcm clientlist - list all the clients
— mcm spawn <ID> - spawn unique client character (his cosmetic presets)
— mcm setspawn <true/false> - trun automatic spawning for new connected clients on/off

— mcm respawn set <true/false> - trun respawning on/off
— mcm respawn penalty <true/false> - trun respawning on/off
— mcm respawn delay <number> - time to wait before respawning
— mcm respawn time <number> - time that respawnees have to catch up with the main sub
]]
            end
        elseif msg:match('^mcm%s+list%s*$') then
            if isInGame then
                response = 'Controllable characters'
                for i,char in pairs(Character.CharacterList) do
                    if tostring(char.TeamID) == 'Team1' and not char.IsDead then
                        -- Team1, meaning default crew
                        response = response .. '\n  ' .. char.ID .. ' / ' .. tostring(char.TeamID) .. ' / ' .. char.SpeciesName ..  ' | ' .. char.Name .. ' (' .. char.DisplayName .. ')'
                    end
                end
            else
                response = '[MCM] Command can be used only in game and multiplayer campaign'
                messageType = ChatMessageType.Error
            end
        elseif msg:match('^mcm%s+clientlist%s*$') then
            if sender.HasPermission(ClientPermissions.ConsoleCommands) then
                response = 'Current clients'
                for i,client in pairs(Client.ClientList) do
                    response = response .. '\n  ' .. client.ID .. ' | ' .. tostring(client.SteamID) .. ' - '.. tostring(client.Name)
                end
            else
                response = '[MCM] Not enough privileges to use this command'
                messageType = ChatMessageType.Error
            end
        elseif msg:match('^mcm%s+control%s+%d+%s*$') then
            if isInGame then
                tryGiveControl(sender, tonumber(msg:match('%d+%s*$')))
            else
                response = '[MCM] Command can be used only in game and multiplayer campaign'
                messageType = ChatMessageType.Error
            end
        elseif msg:match('^mcm%s+release%s*$') or msg:match('^mcm%s+release%s+%d+%s*$') then
            if isInGame then
                local id = tonumber(msg:match('%d+%s*$'))
                if id == nil then
                    if sender.Character == nil then
                        response = '[MCM] Not currently controlling any characters'
                        messageType = ChatMessageType.Error
                    else
                        mcm_client_manager:set(sender, nil)
                        response = '[MCM] Current character released'
                        messageType = ChatMessageType.Server
                    end
                else
                    if sender.HasPermission(ClientPermissions.ConsoleCommands) then
                        response = '[MCM] Character with ID ' .. tostring(id) .. ' not found'
                        messageType = ChatMessageType.Error
                        for i,client in pairs(Client.ClientList) do
                            if client.Character ~= nil and client.Character.ID == id then
                                mcm_client_manager:set(client, nil)
                                response = '[MCM] Character ID - ' .. tostring(id) .. ' released'
                                messageType = ChatMessageType.Server
                                break
                            end
                        end
                    else
                        response = '[MCM] Not enough privileges to use this command'
                        messageType = ChatMessageType.Error
                    end
                end
            else
                response = '[MCM] Command can be used only in game and multiplayer campaign'
                messageType = ChatMessageType.Error
            end
        elseif msg:match('^mcm%s+delete%s+%d+%s*$') then
            if isInGame then
                local id = tonumber(msg:match('%d+%s*$'))
                if sender.HasPermission(ClientPermissions.ConsoleCommands) then
                    response = '[MCM] Character with ID ' .. tostring(id) .. ' not found'
                    messageType = ChatMessageType.Error

                    for i,char in pairs(Character.CharacterList) do
                        if tostring(char.TeamID) == 'Team1' and char.ID == id then
                            char.IsDead = true
                            Entity.Spawner.AddToRemoveQueue(char)
                            response = '[MCM] Character ID - ' .. tostring(id) .. ' was removed'
                            messageType = ChatMessageType.Server
                            break
                        end
                    end
                else
                    response = '[MCM] Not enough privileges to use this command'
                    messageType = ChatMessageType.Error
                end
            else
                response = '[MCM] Command can be used only in game and multiplayer campaign'
                messageType = ChatMessageType.Error
            end
        elseif msg:match('^mcm%s+spawn%s+%d+%s*$') then
            if isInGame then
                if sender.HasPermission(ClientPermissions.ConsoleCommands) then
                    local clientId = tonumber(msg:match('%d+%s*$'))

                    response = '[MCM] Client with ID ' .. tostring(clientId) .. ' not found'
                    messageType = ChatMessageType.Error

                    for i,client in pairs(Client.ClientList) do
                        if (client.ID == clientId) then
                            if tryCeateClientCharacter(client) then
                                response = '[MCM] Character spawned for client ID - ' .. tostring(clientId)
                                messageType = ChatMessageType.Server
                            else
                                response = '[MCM] Could not spawn character for client ID - ' .. tostring(clientId) .. ' , spawn waypoint not found'
                            end
                            break
                        end
                    end
                else
                    response = '[MCM] Not enough privileges to use this command'
                    messageType = ChatMessageType.Error
                end
            else
                response = '[MCM] Command can be used only in game and multiplayer campaign'
                messageType = ChatMessageType.Error
            end
        elseif msg:match('^mcm%s+setspawn%s+true%s*$') or msg:match('^mcm%s+setspawn%s+false%s*$') then
            if sender.HasPermission(ClientPermissions.ConsoleCommands) then
                messageType = ChatMessageType.Server
                if msg:match('true%s*$') then
                    response = '[MCM] Automatic new client character spawn is turned ON'
                    mcm_config.allow_spawn_new_clients = true
                else
                    response = '[MCM] Automatic new client character spawn is turned OFF'
                    mcm_config.allow_spawn_new_clients = false
                end
                writeConfig()
            else
                response = '[MCM] Not enough privileges to use this command'
                messageType = ChatMessageType.Error
            end
        elseif msg:match('^mcm%s+respawn%s+set%s+true%s*$') or msg:match('^mcm%s+respawn%s+set%s+false%s*$') then
            if sender.HasPermission(ClientPermissions.ConsoleCommands) then
                messageType = ChatMessageType.Server
                if msg:match('true%s*$') then
                    response = '[MCM] Respawning is turned ON'
                    mcm_config.allow_respawns = true
                else
                    response = '[MCM] Respawning is turned OFF'
                    mcm_config.allow_respawns = false
                end
                writeConfig()
            else
                response = '[MCM] Not enough privileges to use this command'
                messageType = ChatMessageType.Error
            end
        elseif msg:match('^mcm%s+respawn%s+penalty%s+true%s*$') or msg:match('^mcm%s+respawn%s+penalty%s+false%s*$') then
            if sender.HasPermission(ClientPermissions.ConsoleCommands) then
                messageType = ChatMessageType.Server
                if msg:match('true%s*$') then
                    response = '[MCM] Respawn penalty is turned ON'
                    mcm_config.respawn_penalty = true
                else
                    response = '[MCM] Respawn penalty is turned OFF'
                    mcm_config.respawn_penalty = false
                end
                writeConfig()
            else
                response = '[MCM] Not enough privileges to use this command'
                messageType = ChatMessageType.Error
            end
        elseif msg:match('^mcm%s+respawn%s+delay%s+%d+%s*$') then
            if sender.HasPermission(ClientPermissions.ConsoleCommands) then
                local delay = tonumber(msg:match('%d+%s*$'))
                mcm_config.respawn_delay = delay
                writeConfig()
                messageType = ChatMessageType.Server
                response = '[MCM] Respawn delay is set to ' .. delay .. ' seconds'
            else
                response = '[MCM] Not enough privileges to use this command'
                messageType = ChatMessageType.Error
            end
        elseif msg:match('^mcm%s+respawn%s+time%s+%d+%s*$') then
            if sender.HasPermission(ClientPermissions.ConsoleCommands) then
                local time = tonumber(msg:match('%d+%s*$'))
                mcm_config.respawn_time = time
                writeConfig()
                messageType = ChatMessageType.Server
                response = '[MCM] Respawn catch-up time is set to ' .. time .. ' seconds'
            else
                response = '[MCM] Not enough privileges to use this command'
                messageType = ChatMessageType.Error
            end
        else
            response = '[MCM] error: Incorrect function or argument.'
            messageType = ChatMessageType.Error
        end
        -- send message
        if response ~= nil then
            Game.SendDirectChatMessage(
                '[Server]', response,
                senderCharacter, messageType,
                sender, '')
        end
        -- no chat display
        return true
    end
end