using System.Diagnostics;
using System.Xml.Linq;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using Barotrauma;
using Barotrauma.Networking;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace MultiplayerCrewManager
{
    partial class McmMod
    {
        private PropertyInfo endRoundTimerProperty = typeof(GameServer).GetProperty("EndRoundTimer");
        private float EndRoundTimer
        {
            get => GameMain.Server.EndRoundTimer;
            
            set => endRoundTimerProperty.SetValue(GameMain.Server, value);
        }

        protected Action UpdateAction = null;

        public McmClientManager Manager { get; private set; }
        public McmControl Control { get; private set; }
        public McmSession Session { get; private set; }
        public McmSave Save { get; private set; }
        public McmChat Chat { get; private set; }

        public void InitServer()
        {
            LoadConfig();
            McmUtils.Info("Initializing server...");

            Manager = new McmClientManager();
            Control = new McmControl(GameMain.Server?.RespawnManager, Manager);
            Session = new McmSession(GameMain.GameSession);
            Save = new McmSave(Control);
            Chat = new McmChat(this);

            // multiplayer bot talents & etc.
            GameMain.LuaCs.Hook.Add("getSessionCrewCharacters", "mcm_getSessionCrewCharacters", (object[] args) =>
            {
                if (!McmMod.IsCampaign) return null;
                return Character.CharacterList.Where(c => c.TeamID == CharacterTeamType.Team1 && !c.IsDead).ToArray();
            }, this);

            // networking
            GameMain.LuaCs.Networking.Receive("server-mcm", (object[] args) =>
            {
                (IReadMessage msg, Client client) = (args[0] as IReadMessage, args[1] as Client);
                Int32.TryParse(msg.ReadString(), out int characterID);
                Control.TryGiveControl(client, characterID, McmMod.Config.SecureEnabled);
            });


            // hooks

            // chat commands
            GameMain.LuaCs.Hook.Add("chatMessage", "mcm_ServerCommand", (args) => Chat.OnChatMessage(args[0] as string, args[1] as Client), this);
            // control loop
            GameMain.LuaCs.Hook.Add("think", "mcm_ServerLoop", (args) =>
            {
                if (UpdateAction != null) UpdateAction();
                return null;
            }, this);

            // loop stop
            GameMain.LuaCs.Hook.Add("roundEnd", "mcm_ServerStop", (args) =>
            {
                UpdateAction = null;
                Manager.Clear();
                Save.IsNewCampaign = false;
                McmSave.PipeIndex = 0;
                Save.IsCampaignLoaded = false;
                Control.Awaiting.Clear();
                return null;
            }, this);
            // loop start
            Action serverInit = () =>
            {
                if (!IsCampaign) return;

                Save.IsNewCampaign = false;
                Control.Awaiting.Clear();

                GameMain.LuaCs.Timer.Wait((args) =>
                {
                    Manager.Clear();
                    ClientListUpdate();
                    UpdateAction = Update;
                }, 500);
            };
            if (GameMain.GameSession?.IsRunning ?? false) serverInit();
            GameMain.LuaCs.Hook.Add("roundStart", "mcm_ServerStart", (args) =>
            {
                serverInit();
                Control.AssignAiCharacters();
                return null;
            }, this);

            // client disconnect
            GameMain.LuaCs.Hook.Add("clientDisconnected", "mcm_ServerDisconnect", (args) =>
            {
                Client client = args[0] as Client;
                if (!IsCampaign) return null;
                Manager.Set(client, null);
                return null;
            }, this);

            //respawn
            GameMain.LuaCs.Hook.Add("respawnManager.update", "mcm_RespawnManager_Update", (args) =>
            {
                Control.Update(GameMain.Server?.RespawnManager);
                return true;
            }, this);

            // method hooks
            InitMethodHooks();
            McmUtils.Info("Initialization complete.");
        }

        private void InitMethodHooks()
        {
            // multiplayer bot talents & etc.
            //2023-02-13 Methodhook removed. V1.0 pre-patch adds levelable bots and mission rewards can now be handled by base game

            // money ... money ...
            GameMain.LuaCs.Hook.Patch(
                "mcm_MoneyAction_Update",
                "Barotrauma.MoneyAction",
                "Update",
                new string[] { "System.Single" },
                (instance, ptable) =>
                {
                    object returnvalue = Session.OnMoneyActionUpdate(instance as MoneyAction, (float)ptable["deltaTime"]);
                    if (returnvalue is null)
                        ptable.PreventExecution = false;
                    else
                        ptable.PreventExecution = true;
                    return null;
                },
                LuaCsHook.HookMethodType.Before);

            // pirate missions
            GameMain.LuaCs.Hook.Patch(
                "mcm_PirateMission_InitPirates",
                "Barotrauma.PirateMission",
                "InitPirates",
                new string[] { },
                (instance, ptable) =>
                {
                    //object returnvalue = Session.OnPirateMissionInitPirates(instance as PirateMission);
                    Session.OnPirateMissionInitPirates(instance as PirateMission);
                    //if (returnvalue is null)
                    //    ptable.PreventExecution = false;
                    //else
                    //    ptable.PreventExecution = true;
                    ptable.PreventExecution = true;
                    return null;
                },
                LuaCsHook.HookMethodType.Before);

            // no round end with alive players
            GameMain.LuaCs.Hook.Patch(
                "mcm_GameServer_Update",
                "Barotrauma.Networking.GameServer",
                "Update",
                new string[] { "System.Single" },
                (instance, ptable) =>
                {
                    if (false == McmMod.IsCampaign)
                    {
                        return null;
                    }

                    if (McmMod.Config.RespawnMode!= RespawnMode.MidRound)
                    {
                        return null;
                    }


                    if ((instance as GameServer) != GameMain.Server)
                    {
                        GameMain.Server = instance as GameServer;
                    }

                    if (GameMain.Server.EndRoundTimer > 0.0f)
                    {
                        McmUtils.Trace($"Endround Timer [{EndRoundTimer}]");

                        var alliedChars = Character.CharacterList.Where(c => c.TeamID == CharacterTeamType.Team1);
                        if (alliedChars.Any(c => false == c.IsDead || false == c.IsIncapacitated))
                        {
                            EndRoundTimer = -5.0f;
                            McmUtils.Trace($"There are still alive characters in team, setting new end round timer [{EndRoundTimer}]");
                        }
                        else if (EndRoundTimer < 0.0f)
                        {
                            EndRoundTimer = 0.0f;
                            McmUtils.Trace("Clamping timer to 0");
                        }
                    }
                    return null;
                },
                LuaCsHook.HookMethodType.After);

            // cleanup dead crew bodies
            GameMain.LuaCs.Hook.Patch(
                "mcm_CampaignMode_End",
                "Barotrauma.CampaignMode",
                "End",
                new string[] { "Barotrauma.CampaignMode+TransitionType" },
                (instance, ptable) =>
                {
                    Session.OnCampaignModeEnd(instance as CampaignMode);
                    return null;
                },
                LuaCsHook.HookMethodType.Before);

            // do not save bots, saved in CharacterData under PIPE
            GameMain.LuaCs.Hook.Patch(
                "mcm_CrewManager_SaveMultiplayer",
                "Barotrauma.CrewManager",
                "SaveMultiplayer",
                new string[] { "System.Xml.Linq.XElement" },
                (instance, ptable) =>
                {
                    ptable.PreventExecution = true;
                    ptable.ReturnValue = McmSave.OnSaveMultiplayer(instance as CrewManager, ptable["parentElement"] as XElement);
                    return null;
                },
                LuaCsHook.HookMethodType.Before);

            // hook to save only existing players/bots
            GameMain.LuaCs.Hook.Patch(
                "mcm_MultiPlayerCampaign_SavePlayers_Before",
                "Barotrauma.MultiPlayerCampaign",
                "SavePlayers",
                new string[] { },
                (instance, ptable) =>
                {
                    Save.OnBeforeSavePlayers();
                    return null;
                },
                LuaCsHook.HookMethodType.Before);

            // hook to remove new 'wrong' clients, saved under PIPE
            GameMain.LuaCs.Hook.Patch(
                "mcm_MultiPlayerCampaign_SavePlayers_After",
                "Barotrauma.MultiPlayerCampaign",
                "SavePlayers",
                new string[] { },
                (instance, ptable) =>
                {
                    Save.OnAfterSavePlayers();
                    return null;
                },
                LuaCsHook.HookMethodType.After);


            // load campaign status
            GameMain.LuaCs.Hook.Patch(
                "mcm_MultiPlayerCampaign_LoadCampaign",
                "Barotrauma.MultiPlayerCampaign",
                "LoadCampaign",
                new string[] { "Barotrauma.CampaignDataPath", "Barotrauma.Networking.Client" },
                (instance, ptable) =>
                {
                    Save.OnLoadCampaign();
                    return null;
                },
                LuaCsHook.HookMethodType.After);

        }

        private int counter = -1;
        public void Update()
        {
            counter--;
            if (counter <= 0)
            {
                counter = McmMod.Config.ServerUpdateFrequency;
                ClientListUpdate();
            }
        }

        public void ClientListUpdate()
        {
            var crewManager = GameMain.GameSession.CrewManager;
            var toBeCreated = new List<Client>();
            // update client control status
            foreach (var client in GameMain.Server.ConnectedClients)
            {
                
                if (client.InGame && !client.SpectateOnly)
                {
                    Manager.Set(client, null);
                    // mark client in game registered
                    client.SpectateOnly = true;
                    McmUtils.Info($"New client - {client.CharacterID} | '{client.Name}'");
                    // if spawning is enabled then check if spawn is needed
                    // Avoid duplicate players at the start of a tour
                    if (client.InGame && client.Character == null)
                    {
                        
                        while (Character.CharacterList.Where(c => c.Name == client.Name).Count() >= 2){
                        Manager.Set(client, null);
                        var chr = Character.CharacterList.FirstOrDefault(c => c.Name == client.Name);
                        chr.Remove();
                        crewManager.RemoveCharacter(chr,true,true);

                        McmUtils.Info($"Delete Duplicate Player");
                        }
                        var character = Character.CharacterList.FirstOrDefault(c => c.Name == client.Name);
                        if (character != null) {Manager.Set(client, character);client.Character = character;}
                        else {
                            McmUtils.Info($"Creating client character - {client.Name} | '{client.Name}'");
                            bool rst = Character.CharacterList.Any(c => c.Name == client.Name);
                            McmUtils.Info($"Found - {rst}");
                            toBeCreated.Add(client);
                        };
                    }
                }
            }
            // spawn pending clients if any or is allowed
            TryCreateClientCharacters(toBeCreated);
            //toBeCreated.ForEach(c => TryCeateClientCharacter(c));
            OnlineStatResolver();
        }

        /// <summary>
        /// Due the asyncrinized network client-server interactions, player's character could be sometimes falling to endless ragdoll on the round start until the player reset himself to the character. This handler is designed to solve this problem
        /// </summary>
        public void OnlineStatResolver()
        {
            foreach (var client in Client.ClientList)
            {
                if (client.Character != null && !client.Character.IsDead && client.InGame && client.Character.ClientDisconnected == true)
                {
                    client.Character.ClientDisconnected = false;
                    client.Character.KillDisconnectedTimer = 0.0f;
                    client.Character.ResetNetState();
                    McmUtils.Trace($"Character netstate was refreshed: {client.Character.Name}");
                }
            }
        }

        public bool TryCeateClientCharacter(Client client)
        {
            return TryCreateClientCharacters(new List<Client> { client });
        }

        public bool TryCreateClientCharacters(List<Client> clients)
        {
            bool success = true;
            var crewManager = GameMain.GameSession?.CrewManager;
            var subSpawnPoints = WayPoint.WayPointList.FindAll(w =>
                        w.SpawnType == SpawnType.Human &&
                        w.Submarine == Submarine.MainSub &&
                        w.CurrentHull != null);

            var jobAssignedSpawnPoints = subSpawnPoints.FindAll(w => w.AssignedJob != null);
            var anyAssignedSpawnPoints = subSpawnPoints.FindAll(w => w.AssignedJob == null);

            var availableJobSpawnPoints = new List<Barotrauma.WayPoint>(jobAssignedSpawnPoints);
            var availableAnySpawnPoints = new List<Barotrauma.WayPoint>(anyAssignedSpawnPoints);

            foreach (var client in clients)
            {
                // fix client char info
                if (client.CharacterInfo == null) client.CharacterInfo = new CharacterInfo("human", client.Name);

                crewManager.AddCharacterInfo(client.CharacterInfo);
                client.AssignedJob = client.JobPreferences[0];
                client.CharacterInfo.Job = new Job(client.AssignedJob.Prefab, false, Rand.RandSync.Unsynced, client.AssignedJob.Variant);

                //Setting a waypoint
                Barotrauma.WayPoint waypoint = null;
                //Does client job have a specific spawn point?
                if (subSpawnPoints.Any(w => w.AssignedJob == client.CharacterInfo.Job.Prefab))
                {
                    waypoint = availableJobSpawnPoints.FirstOrDefault(w => w.AssignedJob == client.CharacterInfo.Job.Prefab);
                    if (waypoint == null) //No unique spawn point available anymore - pick one at random
                    {
                        waypoint = jobAssignedSpawnPoints[Rand.Int(jobAssignedSpawnPoints.Count, Rand.RandSync.ServerAndClient)];
                    }
                    else
                    {
                        availableJobSpawnPoints.Remove(waypoint);
                    }
                }
                else //no spawn point exists for clients job
                {
                    McmUtils.Warn($"Client [{client.Name}] has a job [{client.AssignedJob.Prefab.Name}] That does not have a dedicated spawn point");
                    if (anyAssignedSpawnPoints.Count == 0) //this sub does not have any "Any" spawnpoints
                    {
                        //Fail-Over... Pick any random job spawnpoint
                        waypoint = subSpawnPoints.FirstOrDefault();
                    }
                    else
                    {
                        if (availableAnySpawnPoints.Count == 0) //No unique spawn locations available anymore - pick one at random
                        {
                            waypoint = anyAssignedSpawnPoints[Rand.Int(anyAssignedSpawnPoints.Count, Rand.RandSync.ServerAndClient)];
                        }
                        else
                        {
                            waypoint = availableAnySpawnPoints.FirstOrDefault();
                            availableAnySpawnPoints.Remove(waypoint);
                        }
                    }
                }

                if (waypoint == null)
                {
                    McmUtils.Warn($"Failure attempting to create character for client [{client.Name}] - No spawn point found for job [{client.AssignedJob.Prefab.Name}]. Picking another job spawnpoint at random");
                    var failover = subSpawnPoints[Rand.Int(subSpawnPoints.Count, Rand.RandSync.ServerAndClient)];
                    if (failover == null)
                    {
                        McmUtils.Error("Unable to fail-over - Aborting");
                        return false;
                    }
                    waypoint = failover;
                    success = false;
                }

                // spawn character
                client.CharacterInfo.TeamID = CharacterTeamType.Team1;
                var character = Character.Create(client.CharacterInfo, waypoint.WorldPosition, client.CharacterInfo.Name, Entity.NullEntityID, true, true);
                crewManager.AddCharacter(character);

                Manager.Set(client, character);
                character.GiveJobItems(false, waypoint);
            }
            return success;
        }
    }
}
