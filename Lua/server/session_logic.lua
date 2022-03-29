mcm_allow_mission_end = true

function mcm_session_EndRound_before(this, args)
    if not mcm_isCampaignMode then return end

    local missions = this.missions

    for i,mission in pairs(missions) do
        mission.End()
    end
    mcm_allow_mission_end = false

    local chars = {}
    for i,char in pairs(Character.CharacterList) do
        if not char.IsDead and tostring(char.TeamID) == 'Team1' then
            table.insert(chars, char)
        end
    end

    for i,char in pairs(chars) do
        char.CheckTalents(18) -- AbilityEffectType.OnRoundEnd
    end

    if #(missions) > 0 then
        local completedCount = 0
        for i,mission in pairs(missions) do
            if mission.Completed then
                completedCount = completedCount + 1
            end
        end

        if completedCount > 0 then
            for i,char in pairs(chars) do
                char.CheckTalents(19) -- AbilityEffectType.OnAnyMissionCompleted
            end
        end

        if completedCount == #(missions) then
            for i,char in pairs(chars) do
                char.CheckTalents(20) -- AbilityEffectType.OnAllMissionsCompleted
            end
        end
    end
end

function mcm_session_EndRound_after(this, args)
    if not mcm_isCampaignMode then return end

    mcm_allow_mission_end = true
end


function mcm_cmode_End(this, args)
    if not mcm_isCampaignMode then return end

    for i,char in pairs(Character.CharacterList) do
        if char.IsDead and tostring(char.TeamID) == 'Team1' then
            char.DespawnNow(false)
            Entity.Spawner.AddToRemoveQueue(char)
        end
    end
    Entity.Spawner.Update()
end


function mcm_mission_GiveReward(this, args)
    if not mcm_isCampaignMode then return end

    local reward = this.GetReward(Submarine.MainSub)

    local baseExperienceGain = reward * 0.09;

    local difficultyMultiplier = 1 + this.level.Difficulty / 100
    baseExperienceGain = baseExperienceGain * difficultyMultiplier

    local chars = {}
    for i,char in pairs(Character.CharacterList) do
        if not char.IsDead and tostring(char.TeamID) == 'Team1' then
            table.insert(chars, char)
        end
    end

    -- use multipliers here so that we can easily add them together without introducing multiplicative XP stacking
    local experienceGainMultiplier = AbilityExperienceGainMultiplier.__new(1)
    for i,char in pairs(chars) do
        char.CheckTalents(25, experienceGainMultiplier) -- AbilityEffectType.OnAllyGainMissionExperience
    end
    for i,char in pairs(chars) do
        experienceGainMultiplier.Value = experienceGainMultiplier.Value + char.GetStatValue(42) -- StatTypes.MissionExperienceGainMultiplier
    end

    local experienceGain = math.floor(baseExperienceGain * experienceGainMultiplier.Value)
    -- give the experience to the stored characterinfo
    for i,char in pairs(chars) do
        if char.Info ~= nil then
            char.Info.GiveExperience(experienceGain, true)
        end
    end

    -- apply money gains afterwards to prevent them from affecting XP gains
    local missionMoneyGainMultiplier = AbilityMissionMoneyGainMultiplier.__new(this, 1);
    for i,char in pairs(chars) do
        char.CheckTalents(27, missionMoneyGainMultiplier) -- AbilityEffectType.OnGainMissionMoney
    end
    for i,char in pairs(chars) do
        missionMoneyGainMultiplier.Value = missionMoneyGainMultiplier.Value + char.GetStatValue(40) -- StatTypes.MissionMoneyGainMultiplier
    end

    local totalReward =  math.floor(reward * missionMoneyGainMultiplier.Value);
    local campaign = GameMain.GameSession.GameMode
    campaign.Money = campaign.Money + totalReward;

    -- GameAnalyticsManager.AddMoneyGainedEvent(totalReward, GameAnalyticsManager.MoneySource.MissionReward, Prefab.Identifier); -- not needed?

    for i,char in pairs(chars) do
        char.Info.MissionsCompletedSinceDeath = char.Info.MissionsCompletedSinceDeath + 1
    end

    for key,value in pairs(this.ReputationRewards) do
        if key:lower() == 'location' then
            this.Locations[1].Reputation.AddReputation(value);
            this.Locations[2].Reputation.AddReputation(value);
        else
            local faction = nil
            for i,fctn in pairs(campaign.Factions) do
                if fctn.Prefab.Identifier:lower() == key:lower() then
                    faction = fctn
                    break
                end
            end
            if faction ~= nil then
                faction.Reputation.AddReputation(value)
            end
        end
    end

    if this.Prefab.DataRewards ~= nil then
        -- not supported no default missions with metadata, so this should never cointain any tuples
        rprint(this.Prefab.DataRewards)
        for i,tuple in pairs(this.Prefab.DataRewards) do
            print('[MCM-SERVER] Unsoported mission type (has DataRewards)[' .. tostring(i) .. ']: ' .. rprint(tuple))
            SetDataAction.PerformOperation(campaign.CampaignMetadata, tuple[1], tuple[2], tuple[3])
        end
    end

    -- override
    return true
end


function mcm_reputation_AddReputation(this, args)
    if not mcm_isCampaignMode then return end

    local reputationChange = args.reputationChange
    if reputationChange > 0 then
        local reputationGainMultiplier = 1

        for i,char in pairs(Character.CharacterList) do
            if not char.IsDead and tostring(char.TeamID) == 'Team1' then
                reputationGainMultiplier = reputationGainMultiplier + char.GetStatValue(39) -- StatTypes.ReputationGainMultiplier
            end
        end
        reputationChange = reputationChange * reputationGainMultiplier
    end
    this.set_Value(this.Value + reputationChange)

    -- override
    return true
end


function mcm_cmode_GetAddedMissionCount(this, args) -- ruins CLR, cannot override, nor hook
    if not mcm_isCampaignMode then return end

    local count = 0
    for i,char in pairs(Character.CharacterList) do
        if not char.IsDead and tostring(char.TeamID) == 'Team1' then
            count = count + character.GetStatValue(43) -- StatTypes.ExtraMissionCount
        end
    end
    return count -- override
end


function mcm_location_GetExtraSpecialSalesCount(this, args)
    if not mcm_isCampaignMode then return end

    local chars = {}
    for i,char in pairs(Character.CharacterList) do
        if not char.IsDead and tostring(char.TeamID) == 'Team1' then
            table.insert(chars, char)
        end
    end

    if #(chars) == 0 then return 0 end
    local maxMissions = 0
    for i,char in pairs(chars) do
        local essc = math.floor(char.GetStatValue(44)) -- StatTypes.ExtraSpecialSalesCount
        if maxMissions < essc then maxMissions = essc end
    end

    --return maxMissions -- override
    return Convert.ToInt32(maxMissions) -- override
end

function mcm_location_Discover(this, args)
    if not mcm_isCampaignMode then return end

    local checkTalents = args.checkTalents

    if not this.Discovered then
        this.set_Discovered(true)
        if checkTalents then
            for i,char in pairs(Character.CharacterList) do
                if not char.IsDead and tostring(char.TeamID) == 'Team1' then
                    char.CheckTalents(28, AbilityLocation.__new(this)) -- AbilityEffectType.OnLocationDiscovered
                end
            end
        end
    end

    -- override
    return true
end
