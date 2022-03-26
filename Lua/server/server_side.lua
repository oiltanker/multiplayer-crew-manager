
mcm_IsNewCampaign = true
mcm_SavedPlayers = {}
mcm_CampaignLoadedProperly = false
mcm_isCampaignMode = false
mcm_awaitingRespawn = { infos = {}, count = 0 }

local mcmServerDelegate = {func = nil}


-- networking


Networking.Receive("server-mcm", function (msg, client)
    local characterID = tonumber(msg.ReadString())
    tryGiveControl(client, characterID)
end)



-- hooks


-- no round end with alive players
Hook.HookMethod("Barotrauma.Networking.GameServer", "Update", { "System.Single" }, function (this, args)
    if not mcm_isCampaignMode then return end

    if this.endRoundTimer ~= 0.0 then
        -- check client crew
        local clientsDead = true
        for i,client in pairs(Client.ClientList) do
            if client.Character ~= nil and not client.Character.IsDead and not client.Character.IsIncapacitated then
                clientsDead = false
                break
            end
        end
        if clientsDead then
            -- check all crew
            local crewDead = true
            for i,char in pairs(Character.CharacterList) do
                if tostring(char.TeamID) == 'Team1' and not char.IsDead then
                    crewDead = false
                    break
                end
            end
            if clientsDead and not crewDead then this.endRoundTimer = 0.0 end
        end
    end
end, Hook.HookMethodType.After)

-- end round skill issue
Hook.HookMethod("Barotrauma.GameSession", "EndRound", mcm_session_EndRound_before, Hook.HookMethodType.Before)
Hook.HookMethod("Barotrauma.GameSession", "EndRound", mcm_session_EndRound_after, Hook.HookMethodType.After)
Hook.HookMethod("Barotrauma.Mission", "End", function (this, args)
    if not mcm_allow_mission_end then return true end
end, Hook.HookMethodType.Before)
-- player mission exp
Hook.HookMethod("Barotrauma.Mission", "GiveReward", mcm_mission_GiveReward, Hook.HookMethodType.Before)
-- player reputation
Hook.HookMethod("Barotrauma.Reputation", "AddReputation", mcm_reputation_AddReputation, Hook.HookMethodType.Before)
---- player mission count
--Hook.HookMethod("Barotrauma.CampaignSettings", "GetAddedMissionCount", mcm_cmode_GetAddedMissionCount, Hook.HookMethodType.Before)
-- player special sales count
Hook.HookMethod("Barotrauma.Location", "GetExtraSpecialSalesCount", mcm_location_GetExtraSpecialSalesCount, Hook.HookMethodType.Before)
-- player location discovery
Hook.HookMethod("Barotrauma.Location", "Discover", mcm_location_Discover, Hook.HookMethodType.Before)

-- cleanup dead crew bodies
Hook.HookMethod("Barotrauma.CampaignMode", "End", mcm_cmode_End, Hook.HookMethodType.Before)

-- do not save bots, saved in CharacterData under PIPE
Hook.HookMethod("Barotrauma.CrewManager", "SaveMultiplayer", mcm_SaveMultiplayer, Hook.HookMethodType.Before)
-- hook to save only existing players/bots
Hook.HookMethod("Barotrauma.MultiPlayerCampaign", "SavePlayers", mcm_SavePlayers_before, Hook.HookMethodType.Before)
-- hook to remove new 'wrong' clients, saved under PIPE
Hook.HookMethod("Barotrauma.MultiPlayerCampaign", "SavePlayers", mcm_SavePlayers_after, Hook.HookMethodType.After)
-- load campaign status
Hook.HookMethod("Barotrauma.MultiPlayerCampaign", "LoadCampaign", mcm_LoadCampaign, Hook.HookMethodType.After)

-- init datas and clients, depending on campaign status
Hook.HookMethod("Barotrauma.Networking.GameServer", "StartGame", { "Barotrauma.SubmarineInfo", "Barotrauma.SubmarineInfo", "Barotrauma.GameModePreset", "Barotrauma.CampaignSettings" }, mcm_StartGame, Hook.HookMethodType.Before)
-- add player exp
Hook.HookMethod("Barotrauma.CrewManager", "InitRound", mcm_cmanager_InitRound, Hook.HookMethodType.After)

-- chat commands
Hook.Add("chatMessage", 'mcmServerCommand', mcm_chat_commands)
-- control loop
Hook.Add("think", "mcmServerLoop", function()
    if mcmServerDelegate.func ~= nil then mcmServerDelegate.func() end
end)

-- loop stop
Hook.Add("roundEnd", "mcmServerStop", function()
    mcmServerDelegate.func = nil
    mcm_client_manager:clear()
    mcm_IsNewCampaign = false
    mcm_SavedPlayers = {}
    mcm_pipeIndex = 0
    mcm_CampaignLoadedProperly = false
    mcm_isCampaignMode = false
    mcm_awaitingRespawn = { infos = {}, count = 0 }
end)
-- loop start
local serverInit = function()
    -- if not multi-player campaign the quit
    if not checkMultiPlayerCampaign('SERVER') then return end
    mcm_IsNewCampaign = false
    mcm_isCampaignMode = true
    mcm_awaitingRespawn = { infos = {}, count = 0 }

    Timer.Wait(function()
        mcm_client_manager:clear()
        clientListUpadte()
        mcmServerDelegate.func = mcmServerUpdate
    end, 500)
end
if Game.GameSession ~= nil and Game.GameSession.IsRunning then serverInit() end
Hook.Add("roundStart", "mcmServerStart", function()
    assignAiCharacters()
    serverInit()
end)

-- respawning
Hook.HookMethod("Barotrauma.Networking.RespawnManager", "Update", mcm_respawn_Update, Hook.HookMethodType.Before)
-- client disconnect
Hook.Add("clientDisconnected", "mcmServerDisconnect", function(client)
    if not mcm_isCampaignMode then return end
    mcm_client_manager:set(client, nil)
end)



-- functions


local counter = mcm_config.server_update_frequency
function mcmServerUpdate()
    counter = counter - 1
    if counter <= 0 then
        counter = mcm_config.server_update_frequency
        clientListUpadte()
    end
end

function clientListUpadte()
    local toBeCreated = {}
    -- update client control status
    for i,client in pairs(Client.ClientList) do
        mcm_client_manager:set(client, client.Character)
        if client.InGame and not client.SpectateOnly then
            -- mark client in game registered
            client.SpectateOnly = true
            print('[MCM-SEVER] New client  ' .. client.ID .. '|' .. client.Name)
            -- if spawning is enabled then check if spawn is needed
            print(mcm_config.allow_spawn_new_clients)
            if
                mcm_config.allow_spawn_new_clients and
                client.InGame and
                client.Character == nil
            then
                client.SpectateOnly = true
                local charExists = false
                for i,char in pairs(Character.CharacterList) do
                    if char.Name == client.Name then
                        charExists = true
                        break
                    end
                end
                if not charExists then table.insert(toBeCreated, client) end
            end
        end
    end
    -- spawn pending clients if any or is allowed
    for i,client in pairs(toBeCreated) do
        if client.Character == nil and client.ReadyToStart then
            tryCeateClientCharacter(client)
        end
    end
end

function tryCeateClientCharacter(client)
    local session = Game.GameSession
    local crewManager = session.CrewManager

    -- fix client char info
    if client.CharacterInfo == nil then client.CharacterInfo = CharacterInfo.__new('human', client.Name) end

    local charInfo = client.CharacterInfo
    crewManager.AddCharacterInfo(charInfo)
    client.AssignedJob = client.JobPreferences[1]
    client.CharacterInfo.Job = Job.__new(client.AssignedJob.First, -1, client.AssignedJob.Second);

    -- find waypoint
    local waypoint = nil
    for i,wp in pairs(WayPoint.WayPointList) do
        if
            wp.SpawnType == SpawnType.Human and
            wp.Submarine == Submarine.MainSub and
            wp.CurrentHull ~= nil
        then
            if client.CharacterInfo.Job.Prefab == wp.AssignedJob then
                waypoint = wp
                break
            end
        end
    end
    if waypoint == nil then return false end

    -- spawn character
    client.CharacterInfo.TeamID = 1;
    local char = Character.Create(client.CharacterInfo, waypoint.WorldPosition, client.CharacterInfo.Name, 0, true, true);
    crewManager.AddCharacter(char)

    mcm_client_manager:set(client, char)
    char.GiveJobItems(waypoint);

    return true
end