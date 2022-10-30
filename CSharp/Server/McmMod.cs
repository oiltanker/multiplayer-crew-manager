using System.Reflection.PortableExecutable;
using System.Diagnostics;
using System.Xml.Linq;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using Barotrauma;
using Barotrauma.Networking;

namespace MultiplayerCrewManager {
    partial class McmMod {
        private GameServer server = GameMain.Server;
        private FieldInfo endRoundTimerFiled = typeof(GameServer).GetField("endRoundTimer", BindingFlags.Instance | BindingFlags.NonPublic);
        private float endRoundTimer {
            get => (endRoundTimerFiled.GetValue(server) as float?).Value;
            set => endRoundTimerFiled.SetValue(server, value);
        }
        protected Action UpdateAction = null;

        public McmClientManager Manager { get; private set; }
        public McmControl Control { get; private set; }
        public McmSession Session { get; private set; }
        public McmSave Save { get; private set; }
        public McmChat Chat { get; private set;}

        public void InitServer() {
            LoadConfig();

            Manager = new McmClientManager();
            Control = new McmControl(GameMain.Server?.RespawnManager, Manager);
            Session = new McmSession(GameMain.GameSession);
            Save = new McmSave(Control);
            Chat = new McmChat(this);

            // multiplayer bot talents & etc.
            GameMain.LuaCs.Hook.Add("getSessionCrewCharacters", "mcm_getSessionCrewCharacters", (object[] args) => {
                if (!McmMod.IsCampaign) return null;
                return Character.CharacterList.Where(c => c.TeamID == CharacterTeamType.Team1 && !c.IsDead).ToArray();
            }, this);

            // networking
            GameMain.LuaCs.Networking.Receive("server-mcm", (object[] args) => {
                (IReadMessage msg, Client client) = (args[0] as IReadMessage, args[1] as Client);
                Int32.TryParse(msg.ReadString(), out int characterID);
                Control.TryGiveControl(client, characterID);
            });


            // hooks

            // chat commands
            GameMain.LuaCs.Hook.Add("chatMessage", "mcm_ServerCommand", (args) => Chat.OnChatMessage(args[0] as string, args[1] as Client), this);
            // control loop
            GameMain.LuaCs.Hook.Add("think", "mcm_ServerLoop", (args) => {
                if (UpdateAction != null) UpdateAction();
                return null;
            }, this);

            // loop stop
            GameMain.LuaCs.Hook.Add("roundEnd", "mcm_ServerStop", (args) => {
                UpdateAction = null;
                Manager.Clear();
                Save.IsNewCampaign = false;
                McmSave.PipeIndex = 0;
                Save.IsCampaignLoaded = false;
                Control.Awaiting.Clear();
                return null;
            }, this);
            // loop start
            Action serverInit = () => {
                if (!IsCampaign) return;

                Save.IsNewCampaign = false;
                Control.Awaiting.Clear();

                GameMain.LuaCs.Timer.Wait((args) => {
                    Manager.Clear();
                    ClientListUpadte();
                    UpdateAction = Update;
                }, 500);
            };
            if (GameMain.GameSession?.IsRunning ?? false) serverInit();
            GameMain.LuaCs.Hook.Add("roundStart", "mcm_ServerStart", (args) => {
                serverInit();
                Control.AssignAiCharacters();
                return null;
            }, this);

            // client disconnect
            GameMain.LuaCs.Hook.Add("clientDisconnected", "mcm_ServerDisconnect", (args) => {
                Client client = args[0] as Client;
                if (!IsCampaign) return null;
                Manager.Set(client, null);
                return null;
            }, this);

            //respawn
            GameMain.LuaCs.Hook.Add("respawnManager.update", "mcm_RespawnManager_Update", (args) => {
                Control.Update(GameMain.Server?.RespawnManager);
                return true;
            }, this);

            // methos hooks
            InitMethodHooks();
        }

        private void InitMethodHooks() {
            // multiplayer bot talents & etc.
             GameMain.LuaCs.Hook.HookMethod("mcm_Mission_GiveReward",
                typeof(Mission).GetMethod("GiveReward"),
                (object self, Dictionary<string, object> args) => Session.OnMissionGiveReward(self as Mission),
                LuaCsHook.HookMethodType.Before, this);
            // money ... money ...
            GameMain.LuaCs.Hook.HookMethod("mcm_MoneyAction_Update",
                typeof(MoneyAction).GetMethod("Update"),
                (object self, Dictionary<string, object> args) => Session.OnMoneyActionUpdate(self as MoneyAction, (args["deltaTime"] as float?).Value),
                LuaCsHook.HookMethodType.Before, this);
            // pirate missions
            GameMain.LuaCs.Hook.HookMethod("mcm_PirateMission_InitPirates",
                typeof(PirateMission).GetMethod("InitPirates", BindingFlags.Instance | BindingFlags.NonPublic),
                (object self, Dictionary<string, object> args) => Session.OnPirateMissionInitPirates(self as PirateMission),
                LuaCsHook.HookMethodType.Before, this);
            
            // no round end with alive players
            GameMain.LuaCs.Hook.HookMethod("mcm_GameServer_Update",
                typeof(GameServer).GetMethod("Update", new[] { typeof(System.Single) }),
                (object self, Dictionary<string, object> args) => {
                    if (!McmMod.IsCampaign) return null;

                    if ((self as GameServer) != server) server = self as GameServer;
                    if (endRoundTimer > 0.0f) {
                        if (Character.CharacterList.Any(c => c.TeamID == CharacterTeamType.Team1 && !(c.IsDead || c.IsIncapacitated)))
                            endRoundTimer = -5.0f;
                        else if (endRoundTimer < 0.0f) endRoundTimer = 0.0f;
                    }
                    return null;
                },
                LuaCsHook.HookMethodType.After, this);
            // cleanup dead crew bodies
            GameMain.LuaCs.Hook.HookMethod("mcm_CampaignMode_End",
                typeof(CampaignMode).GetMethod("End"),
                (object self, Dictionary<string, object> args) => {
                    Session.OnCampaignModeEnd(self as CampaignMode);
                    return null;
                }, LuaCsHook.HookMethodType.Before, this);

            // do not save bots, saved in CharacterData under PIPE
            GameMain.LuaCs.Hook.HookMethod("mcm_CrewManager_SaveMultiplayer",
                typeof(CrewManager).GetMethod("SaveMultiplayer"), 
                (object self, Dictionary<string, object> args) => Save.OnSaveMultiplayer(self as CrewManager, args["root"] as XElement),
                LuaCsHook.HookMethodType.Before, this);
            // hook to save only existing players/bots
            GameMain.LuaCs.Hook.HookMethod("mcm_MultiPlayerCampaign_SavePlayers",
                typeof(MultiPlayerCampaign).GetMethod("SavePlayers"),
                (object self, Dictionary<string, object> args) => {
                    Save.OnBeforeSavePlayers();
                    return null;
                }, LuaCsHook.HookMethodType.Before, this);
            // hook to remove new 'wrong' clients, saved under PIPE
            GameMain.LuaCs.Hook.HookMethod("mcm_MultiPlayerCampaign_SavePlayers",
                typeof(MultiPlayerCampaign).GetMethod("SavePlayers"),
                (object self, Dictionary<string, object> args) => {
                    Save.OnAfterSavePlayers();
                    return null;
                }, LuaCsHook.HookMethodType.After, this);
            // load campaign status
            GameMain.LuaCs.Hook.HookMethod("mcm_MultiPlayerCampaign_LoadCampaign",
                typeof(MultiPlayerCampaign).GetMethod("LoadCampaign"),
                (object self, Dictionary<string, object> args) => {
                    Save.OnLoadCampaign();
                    return null;
                }, LuaCsHook.HookMethodType.After, this);

            // init datas and clients, depending on campaign status
            GameMain.LuaCs.Hook.HookMethod("mcm_GameServer_StartGame",
                typeof(GameServer).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic).FirstOrDefault(m => m.Name == "StartGame" && m.GetParameters().Count() == 4),
                (object self, Dictionary<string, object> args) => {
                    Save.OnStartGame();
                    return null;
                }, LuaCsHook.HookMethodType.Before, this);
            // add player exp
            GameMain.LuaCs.Hook.HookMethod("mcm_CrewManager_InitRound",
                typeof(CrewManager).GetMethod("InitRound"),
                (object self, Dictionary<string, object> args) => {
                    Control.OnInitRound(self as CrewManager);
                    Save.RestoreCharactersWallets();
                    return null;
                }, LuaCsHook.HookMethodType.After, this);
        }

        private int counter = -1;
        public void Update() {
            counter--;
            if (counter <= 0) {
                counter = McmMod.Config.ServerUpdateFrequency;
                ClientListUpadte();
            }
        }

        public void ClientListUpadte() {
            var toBeCreated = new List<Client>();
            // update client control status
            foreach (var client in Client.ClientList) {
                Manager.Set(client, client.Character);
                if (client.InGame && !client.SpectateOnly) {
                    // mark client in game registered
                    client.SpectateOnly = true;
                    LuaCsSetup.PrintCsMessage($"[MCM-SEVER] New client - {client.CharacterID} | '{client.Name}'");
                    // if spawning is enabled then check if spawn is needed
                    if (McmMod.Config.AllowSpawnNewClients && client.InGame && client.Character == null) {
                        var character = Character.CharacterList.FirstOrDefault(c => c.TeamID == CharacterTeamType.Team1 && c.Name == client.Name);
                        if (character != null && !Manager.IsCurrentlyControlled(character)) Manager.Set(client, character);
                        else toBeCreated.Add(client);
                    }
                }
            }
            // spawn pending clients if any or is allowed
            toBeCreated.ForEach(c => TryCeateClientCharacter(c));
        }

        public bool TryCeateClientCharacter(Client client) {
            var crewManager = GameMain.GameSession?.CrewManager;

            // fix client char info
            if (client.CharacterInfo == null) client.CharacterInfo = new CharacterInfo("human", client.Name);

            crewManager.AddCharacterInfo(client.CharacterInfo);
            client.AssignedJob = client.JobPreferences[0];
            client.CharacterInfo.Job = new Job(client.AssignedJob.Prefab, Rand.RandSync.Unsynced, client.AssignedJob.Variant);

            // find waypoint
            var waypoint = WayPoint.WayPointList.Where(w =>
                    w.SpawnType == SpawnType.Human &&
                    w.Submarine == Submarine.MainSub &&
                    w.CurrentHull != null
                ).FirstOrDefault(w => client.CharacterInfo.Job.Prefab == w.AssignedJob);
            if (waypoint == null) return false;

            // spawn character
            client.CharacterInfo.TeamID = CharacterTeamType.Team1;
            var character = Character.Create(client.CharacterInfo, waypoint.WorldPosition, client.CharacterInfo.Name, Entity.NullEntityID, true, true);
            crewManager.AddCharacter(character);

            Manager.Set(client, character);
            character.GiveJobItems(waypoint);

            return true;
        }
    }
}