local function register(type, name)
    if name == nil then name = type:match('%.[^\n%.]*$'):sub(2, -1) end
    local nameType = 'Type_' .. name

    _G[nameType] = LuaUserData.RegisterType(type)
    _G[name] = LuaUserData.CreateStatic(type)
end

register('System.Type')
register('System.RuntimeType')
register('Barotrauma.AIController')
register('Barotrauma.HumanAIController')
register('Barotrauma.GameMode')
register('Barotrauma.MultiPlayerCampaign')
register('System.Reflection.RtFieldInfo')
register('System.ValueTuple`2[[System.Int32], [System.String]]', 'Typle_Int32_String')

if SERVER then
    register('System.Xml.Linq.XName')
    register('System.Xml.Linq.XElement')
    register('System.Xml.Linq.XAttribute')
    register('Barotrauma.CrewManager')
    register('Barotrauma.CampaignMode')
    register('Barotrauma.CharacterCampaignData')
    register('System.Collections.Generic.List`1[[Barotrauma.CharacterCampaignData]]', 'List_CharacterCampaignData')
    register('Barotrauma.Networking.PipeConnection')
    register('Barotrauma.GameModePreset')
    register('Barotrauma.CampaignSettings')
    register('Barotrauma.GameMain')
    register('Barotrauma.Networking.GameServer')
    register('Barotrauma.LevelData')
    register('Barotrauma.CampaignMode+TransitionType', 'TransitionType')
    register('Barotrauma.Networking.ChatMessage')
    register('Barotrauma.Networking.RespawnManager')
    register('Barotrauma.Items.Components.Steering')
    register('Barotrauma.Mission')
    register('System.Collections.Generic.HashSet`1[[System.String]]', 'HashSet_String')

    register('Barotrauma.GameSession')
    register('Barotrauma.AbilityExperienceGainMultiplier')
    register('Barotrauma.AbilityMissionMoneyGainMultiplier')
    register('Barotrauma.Location')
    register('Barotrauma.Reputation')
    register('Barotrauma.Faction')
    register('Barotrauma.SetDataAction')
    --register('Barotrauma.SetDataAction+OperationType', 'OperationType')
    register('Barotrauma.MissionPrefab')
    --register('Tuple`3[[System.String], [System.Object], [System.Enum]]', 'Tuple_String_Object_Enum')
    register('Barotrauma.CampaignMetadata')
    register('Barotrauma.Location+AbilityLocation', 'AbilityLocation')

    LuaUserData.MakeFieldAccessible(Type_MultiPlayerCampaign, "characterData")
    LuaUserData.MakeFieldAccessible(Type_CrewManager, "characterInfos")
    LuaUserData.MakeFieldAccessible(Type_CharacterCampaignData, "itemData")
    LuaUserData.MakeFieldAccessible(Type_CharacterCampaignData, "healthData")
    LuaUserData.MakeFieldAccessible(Type_GameServer, "endRoundTimer")
    LuaUserData.MakeMethodAccessible(Type_RespawnManager, "ResetShuttle")
    LuaUserData.MakeFieldAccessible(Type_RespawnManager, "shuttleSteering")
    LuaUserData.MakeFieldAccessible(Type_GameServer, "respawnManager")
    LuaUserData.MakeFieldAccessible(Type_Mission, "level")

    LuaUserData.MakeFieldAccessible(Type_GameSession, "missions")
    LuaUserData.MakeMethodAccessible(Type_Reputation, "set_Value")
    LuaUserData.MakeMethodAccessible(Type_Location, "set_Discovered")
end


function rprint(value)
    print(rscan(value))
end
function rscan(value)
    if type(value) == 'table' then
        local str = '{ '
        for k,v in pairs(value) do
            if type(k) == 'string' then
                str  = str .. '["' .. k .. '"] = ' .. rscan(v) .. ', '
            else
                str  = str .. '[' .. tostring(k) .. '] = ' .. rscan(v) .. ', '
            end
        end
        return str .. ' }'
    elseif type(value) == 'string' then
        return '"' .. value .. '"'
    elseif type(value) == 'number' then
        return tostring(value)
    else
        return tostring(value)
    end
end


function checkMultiPlayerCampaign(str)
    local session = Game.GameSession
    if session ~= nil then
        local gameMode = session.GameMode
        if gameMode.GetType() == MultiPlayerCampaign then
            if str ~= nil then print('[MCM-' .. str .. '] Multiplayer Campaign - initializing crew manager') end
            return true
        else
            if str ~= nil then print('[MCM' .. str .. '] Not a Multiplayer Campaign - crew manager WON\'T be initializing') end
            return false
        end
    end
end