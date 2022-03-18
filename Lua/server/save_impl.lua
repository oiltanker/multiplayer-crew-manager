mcm_pipeIndex = 0


function mcm_LoadCampaign()
    if not checkMultiPlayerCampaign() then return false end
    mcm_isCampaignMode = true

    print('[MCM-SERVER] Loading campaign')
    local session = Game.GameSession
    local campaign = session.GameMode
    local characterData = campaign.characterData

    mcm_SavedPlayers = {}

    local pipes = 0
    for i = 0,#(characterData)-1 do
        local cd = characterData[i]
        if cd.ClientEndPoint == 'PIPE' then
            table.insert(mcm_SavedPlayers, cd)
            pipes = pipes + 1
        end
    end
    if pipes == 0 then
        mcm_IsNewCampaign = true
    else
        mcm_IsNewCampaign = false
    end
    mcm_CampaignLoadedProperly = true

    return true
end


function mcm_StartGame_Begin()
    if not mcm_CampaignLoadedProperly then
        if not mcm_LoadCampaign() then return end
    end

    local session = Game.GameSession
    local crewManager = session.CrewManager
    local gameServer = GameMain.Server
    local serverSettings = Game.ServerSettings

    if not mcm_IsNewCampaign then
        for i,charInfo in pairs(crewManager.CharacterInfos) do
            crewManager.RemoveCharacterInfo(charInfo);
        end
        for i,cd in pairs(mcm_SavedPlayers) do
            local charInfo = cd.CharacterInfo
            charInfo.InventoryData = cd.itemData
            charInfo.HealthData = cd.healthData
            charInfo.OrderData = cd.OrderData
            charInfo.IsNewHire = false
            crewManager.AddCharacterInfo(charInfo)
        end
    else
        local clientCount = 0

        for i,client in pairs(Client.ClientList) do
            if client.CharacterInfo == nil then client.CharacterInfo = CharacterInfo.__new('human', client.Name) end

            local charInfo = client.CharacterInfo
            crewManager.AddCharacterInfo(charInfo)
            client.AssignedJob = client.JobPreferences[1]
            client.CharacterInfo.Job = Job.__new(client.AssignedJob.First, -1, client.AssignedJob.Second);

            clientCount = clientCount + 1
        end

        local botCount = 0
        if serverSettings.BotSpawnMode == 1 then -- fill bots
            botCount = math.max(serverSettings.BotCount - clientCount, 0)
        else -- add bots
            botCount = math.max(serverSettings.BotCount, 0)
        end

        local bots = {}
        if botCount > 0 then
            for i = 1,botCount do
                local botInfo = CharacterInfo.__new('human')
                botInfo.TeamID = 1 -- Team1
                table.insert(bots, botInfo)
            end
            gameServer.AssignBotJobs(bots, 1) -- Team1
        end

        for i,bot in pairs(bots) do crewManager.AddCharacterInfo(bot) end
    end
    crewManager.HasBots = true

    -- do not spawn clients
    for i,client in pairs(Client.ClientList) do
        client.SpectateOnly = true
    end
end


function mcm_SaveMultiplayer(this, args)
    print('[MCM-SERVER] Saving multiplayer campaign')
    local xRoot = args['root']
    local saveElement = XElement.__new(XName.Get("bots"), XAttribute.__new(XName.Get("hasbots"), this.HasBots));
    xRoot.Add(saveElement)

    return true
end


local function getDymmyC(char, charInfo)
    local dummyC = Client.__new(charInfo.Name, 127)
    dummyC.CharacterInfo = charInfo
    dummyC.Character = char
    dummyC.Connection = PipeConnection.__new(0)
    mcm_pipeIndex = mcm_pipeIndex + 1
    dummyC.SteamID = mcm_pipeIndex

    return dummyC
end

function mcm_before_SavePlayers()
    print('[MCM-SERVER] Saving players (pre-process)')
    local session = Game.GameSession
    local characterData = session.GameMode.characterData

    local crewManager = session.CrewManager
    local characterInfos = crewManager.characterInfos
    local respawnManager = GameMain.Server.respawnManager
    mcm_updateAwaitingChars()

    local str = 'Saving crew characters:'
    -- remove actual
    characterData.Clear()

    mcm_pipeIndex = 0
    -- add dummy
    for i,char in pairs(Character.CharacterList) do
        if tostring(char.TeamID) == 'Team1' and char.Info ~= nil then
            -- Team1, meaning default crew

            -- check if fired
            local isFired = true
            for i,chInfo in pairs(characterInfos) do
                if char.Info == chInfo then
                    isFired = false
                    break
                end
            end

            -- add to save
            if not isFired then
                -- add if alive
                if not char.IsDead then
                    char.Info.TeamID = char.TeamID

                    local dummyC = getDymmyC(char, char.Info)
                    local charData = CharacterCampaignData.__new(dummyC)
                    dummyC.Dispose()

                    characterData.Add(charData)
                    str = str .. '\n    ' .. tostring(char.ID) .. ' | ' .. char.Name .. ' - '  .. 'OK'
                elseif not mcm_config.allow_respawns then
                    str = str .. '\n    ' .. tostring(char.ID) .. ' | ' .. char.Name .. ' - '  .. 'Dead'
                end
            else
                str = str .. '\n    ' .. tostring(char.ID) .. ' | ' .. char.Name .. ' - '  .. 'Fired'
            end
        end
    end

    -- add dead
    if mcm_config.allow_respawns then
        for charInfo,_ in pairs(mcm_awaitingRespawn.infos) do
            charInfo.ClearCurrentOrders()
            charInfo.RemoveSavedStatValuesOnDeath()
            charInfo.CauseOfDeath = nil
            if mcm_config.respawn_penalty and respawnManager ~= nil then respawnManager.ReduceCharacterSkills(charInfo) end
            charInfo.StartItemsGiven = false

            local dummyC = getDymmyC(nil, charInfo)
            local charData = CharacterCampaignData.__new(dummyC)
            dummyC.Dispose()

            characterData.Add(charData)
            str = str .. '\n    ' .. tostring(charInfo.ID) .. ' | ' .. charInfo.Name .. ' - '  .. 'Respawning'
        end
    end

    -- add new hires
    for i,charInfo in pairs(characterInfos) do
        if charInfo.IsNewHire then
            local dummyC = getDymmyC(nil, charInfo)
            local charData = CharacterCampaignData.__new(dummyC)
            dummyC.Dispose()

            characterData.Add(charData)
            str = str .. '\n    ' .. tostring(charInfo.ID) .. ' | ' .. charInfo.Name .. ' - '  .. 'New Hire'
        end
    end
    -- print status
    print(str)
end


function mcm_after_SavePlayers()
    print('[MCM-SERVER] Saving players (post-process)')
    local session = Game.GameSession

    local characterData = session.GameMode.characterData
    -- remove actual new
    for i,char in pairs(Character.CharacterList) do
        if tostring(char.TeamID) == 'Team1' and char.Info ~= nil then
            -- Team1, meaning default crew
            local i = 0
            local maxIdx = #(characterData)
            while i ~= maxIdx do
                if characterData[i].ClientEndPoint ~= 'PIPE' then
                    characterData.RemoveAt(i)
                    maxIdx = #(characterData)
                else
                    i = i + 1
                end
            end
        end
    end
end