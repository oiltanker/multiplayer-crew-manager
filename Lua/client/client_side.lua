local mcmClientDelegate = {func = nil}


-- hooks

Hook.HookMethod("Barotrauma.CrewManager", "AddCharacter", function(this, args)
    local char = args.character -- Character
    local sortCrewList = args.sortCrewList -- bool
    print('[MCM-CLIENT] [Barotrauma.CrewManager.AddCharacter] (' .. char.ID .. '|"' .. char.Name .. '", ' .. tostring(sortCrewList) .. ')')
    -- print(Environment.StackTrace) ass from ReadSpawnData <= Entity.Spawner.ClientRead
end, Hook.HookMethodType.Before)
Hook.HookMethod("Barotrauma.CrewManager", "FireCharacter", function(this, args)
    local chInfo = args.characterInfo
    print('[MCM-CLIENT] [Barotrauma.CrewManager.FireCharacter] ' .. chInfo.ID .. ' | ' .. chInfo.Name)
end, Hook.HookMethodType.Before)
-- crew manager ui click*
Hook.HookMethod("Barotrauma.CrewManager", "OnCrewListRearranged", function(instance, ptable)
    print('[Barotrauma.CrewManager.OnCrewListRearranged]')
    print(mcmClientDelegate.func)
    if mcmClientDelegate.func ~= nil then mcmClientDelegate.func(instance, ptable) end
end, Hook.HookMethodType.After)
-- ui handle stop
Hook.Add("roundEnd", "mcmClientStop", function()
    mcmClientDelegate.func = nil
end)
-- ui handle start
local clientInit = function()
    if not checkMultiPlayerCampaign('CLIENT') then return end
    mcmClientDelegate.func = mcmClientUpdate
end
if Game.GameSession ~= nil and Game.GameSession.IsRunning then Timer.Wait(clientInit, 500)
else Hook.Add("roundStart", "mcmClientStart", clientInit) end


--functions

function mcmClientUpdate(instance, ptable)
    if not ptable.crewList.HasDraggedElementIndexChanged then
        local char = ptable.draggedElementData
        local msg = Networking.Start("server-mcm")
        msg.Write(tostring(char.ID))
        Networking.Send(msg)
    end
end