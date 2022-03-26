mcm_client_manager = {
    table = {}
}

function mcm_client_manager:isCurrentlyControlled(char)
    for client,ch in pairs(self.table) do
        if char == ch then
            return true
        end
    end
    return false
end

function mcm_client_manager:clear()
    self.table = {}
end

function mcm_client_manager:set(client, char)
    self.table[client] = char
    if client.Character ~= char then client.SetClientCharacter(char) end
end

function mcm_client_manager:get(client)
    for clnt,ch in pairs(self.table) do
        if client == clnt then return ch end
    end
    return nil
end


local function getRespawnPoints(manager, charInfos)
    local respawnSub = manager.RespawnShuttle
    if respawnSub == nil or (Level.Loaded ~= nil and tostring(Level.Loaded.Type) == 'Outpost') then
        respawnSub = Submarine.MainSub
    end
    return WayPoint.SelectCrewSpawnPoints(charInfos, respawnSub);
end

local function respawnCharacter(manager, charInfo, spawnPoint)
    charInfo.ClearCurrentOrders()
    charInfo.RemoveSavedStatValuesOnDeath()
    charInfo.CauseOfDeath = nil;
    if mcm_config.respawn_penalty then manager.ReduceCharacterSkills(charInfo) end

    local char = Character.Create(charInfo, spawnPoint.WorldPosition, charInfo.Name, 0, true, true);
    char.TeamID = 1 -- Team1
    if char.Info ~= charInfo then char.Info = charInfo end -- if not the same for some reason ?
    char.Info.Character = char

    char.LoadTalents()
    charInfo.SetExperience(charInfo.ExperiencePoints);

    if mcm_config.respawn_penalty then manager.GiveRespawnPenaltyAffliction(char) end
    char.GiveJobItems(spawnPoint)
    char.GiveIdCardTags(spawnPoint)
end

local function getRespawnSubChars(manager)
    local chars = {}
    for i,char in pairs(Character.CharacterList) do
        if tostring(char.TeamID) == 'Team1' and not char.IsDead and char.Submarine == manager.RespawnShuttle then
            table.insert(chars, char)
        end
    end
    return chars
end

local function tryDoRespawn(manager, doForceRespawn)
    if manager.RespawnShuttle ~= nil then
        -- get all chars on respawn sub
        local charsOnSub = getRespawnSubChars(manager);
        -- is respawn now possible
        if #(getRespawnSubChars(manager)) > 0 then
            if doForceRespawn then
                -- remove all chars on respawn sub
                for i,char in pairs(charsOnSub) do
                    Entity.Spawner.AddToRemoveQueue(char)
                    if not mcm_awaitingRespawn.infos[char.Info] then
                        mcm_awaitingRespawn.infos[char.Info] = true
                        mcm_awaitingRespawn.count = mcm_awaitingRespawn.count + 1
                    end
                end
                Entity.Spawner.Update()
            else return false end
        end
    end
    -- ToList(), sort of
    local charInfos = {}
    for charInfo,_ in pairs(mcm_awaitingRespawn.infos) do
        table.insert(charInfos, charInfo)
    end
    -- dispatch respawn shuttle if needed
    if manager.RespawnShuttle ~= nil and Level.Loaded ~= nil and tostring(Level.Loaded.Type) ~= 'Outpost' then
        local shuttle = manager.RespawnShuttle

        manager.ResetShuttle()
        if manager.shuttleSteering ~= null then
            manager.shuttleSteering.TargetVelocity = Vector2.Zero;
        end

        shuttle.SetPosition(manager.FindSpawnPos());
        shuttle.Velocity = Vector2.Zero;
        shuttle.NeutralizeBallast();
        shuttle.EnableMaintainPosition();
    end
    -- get spawn points
    local respawnPoints = getRespawnPoints(manager, charInfos)
    -- respawn chars
    for i,charInfo in pairs(charInfos) do
        respawnCharacter(manager, charInfo, respawnPoints[i])
    end
    -- all good
    return true
end

function mcm_updateAwaitingChars()
    local session = Game.GameSession
    local crewManager = session.CrewManager
    local characterInfos = crewManager.characterInfos
    -- get awaiting if any
    for i,charInfo in pairs(characterInfos) do
        if tostring(charInfo.TeamID) == 'Team1' and not charInfo.IsNewHire then
            local isDead = true
            for i,char in pairs(Character.CharacterList) do
                if not char.IsDead and char.Info == charInfo then
                    isDead = false
                    break
                end
            end
            if isDead and not mcm_awaitingRespawn.infos[charInfo] then
                mcm_awaitingRespawn.infos[charInfo] = true
                mcm_awaitingRespawn.count = mcm_awaitingRespawn.count + 1
            end
        end
    end
end

local respawnTimeBegin = 0 -- from Timer.Time
local respawnTimer = 0 -- difference
local respawnTimerForce = 0
local function updateRespawns(manager)
    -- do respawn
    if respawnTimeBegin ~= 0 and Timer.Time - respawnTimer >= respawnTimeBegin then
        if tryDoRespawn(manager, Timer.Time - respawnTimerForce > respawnTimeBegin) then
            -- reset respawn state
            respawnTimeBegin = 0
            respawnTimer = 0
            respawnTimerForce = 0
            mcm_awaitingRespawn = { infos = {}, count = 0 }
        else
            -- post-pone respawn
            respawnTimer = respawnTimer + mcm_config.respawn_delay
        end
    end
    -- update dead characters awaiting respawn
    mcm_updateAwaitingChars()
    -- start timer if needed
    if mcm_awaitingRespawn.count > 0 and respawnTimeBegin == 0 then
        respawnTimeBegin = Timer.Time
        respawnTimer = mcm_config.respawn_delay
        respawnTimerForce = mcm_config.respawn_time
    end
end

local counter = mcm_config.server_update_frequency
function mcm_respawn_Update(this, args)
    if not mcm_isCampaignMode then return end
    if mcm_config.allow_respawns then
        counter = counter - 1
        if counter <= 0 then
            counter = mcm_config.server_update_frequency
            updateRespawns(this)
        end
    end
    -- override
    return true
end


function mcm_cmanager_InitRound(this, args)
    if not mcm_isCampaignMode then return end

    local crewManager = this
    local characterInfos = crewManager.characterInfos
    for i,charInfo in pairs(characterInfos) do
        charInfo.SetExperience(charInfo.ExperiencePoints);
        -- LoadTalents he is prohibited, removes all the talents
    end
end


function tryGiveControl(client, characterID)
    local character = nil

    for i,ch in pairs(Character.CharacterList) do
        if ch.ID == characterID and tostring(ch.TeamID) == 'Team1' then
            -- Team1, meaning default crew
            character = ch
        end
    end

    if character == nil then
        -- character with provided ID not found => send client FAIL
        local msg = 'Failed to gain control: Character ID [' .. characterID .. '] not found'
        Game.SendDirectChatMessage(
            '[Server]', msg,
            senderCharacter, ChatMessageType.Error,
            client, 'StoreShoppingCrateIcon')
        return
    end

    if mcm_client_manager:isCurrentlyControlled(character) then
        -- if is busy => send client FAIL
        local msg = 'Failed to gain control: "' .. character.DisplayName .. '" is already in use'
        Game.SendDirectChatMessage(
            '[Server]', msg,
            senderCharacter, ChatMessageType.Error,
            client, 'StoreShoppingCrateIcon')
        return
    end

    -- is free or original original
    mcm_client_manager:set(client, character)

    -- send client OK
    local msg = 'Gained control of "' .. character.DisplayName .. '"'
    Game.SendDirectChatMessage(
        '[Server]', msg,
        senderCharacter, ChatMessageType.Server,
        client, 'StoreShoppingCrateIcon')
end


function assignAiCharacters()
    local spawned = {}
    for i,char in pairs(Character.CharacterList) do
        if tostring(char.TeamID) == 'Team1' then
            table.insert(spawned, char)
        end
    end
    -- assign old
    local unassigned = {}
    for i,char in pairs(spawned) do
        local isAssigned = false
        for i,client in pairs(Client.ClientList) do
            if client.Connection.EndPointString ~= 'PIPE' and client.Character == nil then
                if spawned.Name == client.Name then
                    mcm_client_manager:set(client, char)
                    isAssigned = true
                    break
                end
            end
        end
        if not isAssigned then table.insert(unassigned, char) end
    end
    -- assign unassigned
    for i,client in pairs(Client.ClientList) do
        if client.Character == nil then
            if #(unassigned) == 0 then break end
            local char = unassigned[1]
            table.remove(unassigned, 1)
            mcm_client_manager:set(client, char)
        end
    end
end