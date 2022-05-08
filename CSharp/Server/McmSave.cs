using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using Barotrauma;
using Barotrauma.Networking;

namespace MultiplayerCrewManager {
    class McmSave {
        private static ulong pipeIndex = 0;
        public static ulong PipeIndex {
            get {
                pipeIndex++;
                return pipeIndex;
            }
            set => pipeIndex = value;
        }

        private readonly static FieldInfo itemData = typeof(CharacterCampaignData).GetField("itemData", BindingFlags.NonPublic | BindingFlags.Instance);
        private readonly static FieldInfo healthData = typeof(CharacterCampaignData).GetField("healthData", BindingFlags.NonPublic | BindingFlags.Instance);
        private readonly static FieldInfo cdField = typeof(MultiPlayerCampaign).GetField("characterData", BindingFlags.NonPublic | BindingFlags.Instance);
        public static List<CharacterCampaignData> CharacterData {
            get => cdField.GetValue(GameMain.GameSession.GameMode as MultiPlayerCampaign) as List<CharacterCampaignData>;
            set => cdField.SetValue(GameMain.GameSession.GameMode as MultiPlayerCampaign, value);
        }
        public List<CharacterCampaignData> SavedPlayers { get; private set; }

        public bool IsCampaignLoaded { get; set; }
        public bool IsNewCampaign { get; set; }
        public McmControl Control { get; set; }

        public McmSave(McmControl control) {
            Control = control;

            SavedPlayers = null;
            IsCampaignLoaded = false;
            IsNewCampaign = false;
        }

        public void OnLoadCampaign() {
            if (!McmMod.IsCampaign) return;

            SavedPlayers = CharacterData.Where(c => c.ClientEndPoint == "PIPE").ToList();
            IsNewCampaign = SavedPlayers.Count <= 0;
            IsCampaignLoaded = true;
        }
        public void OnStartGame() {
            if (!McmMod.IsCampaign) return;
            if (!IsCampaignLoaded) OnLoadCampaign();

            var crewManager = GameMain.GameSession.CrewManager;
            foreach(var ci in crewManager.GetCharacterInfos().ToArray()) crewManager.RemoveCharacterInfo(ci);

            if (!IsNewCampaign) {
                SavedPlayers.ForEach(p => {
                    var charInfo = p.CharacterInfo;
                    charInfo.InventoryData = (XElement)itemData.GetValue(p);
                    charInfo.HealthData = (XElement)healthData.GetValue(p);
                    charInfo.OrderData = p.OrderData;
                    charInfo.IsNewHire = false;
                    crewManager.AddCharacterInfo(charInfo);
                });
            }
            else {
                var serverSettings = GameMain.Server.ServerSettings;
                var clientCount = 0;

                foreach(var client in Client.ClientList) {
                    if (client.CharacterInfo == null) client.CharacterInfo = new CharacterInfo("human", client.Name);
                    client.CharacterInfo.TeamID = CharacterTeamType.Team1;

                    client.AssignedJob = client.JobPreferences[0];
                    client.CharacterInfo.Job = new Job(client.AssignedJob.Prefab, Rand.RandSync.Unsynced, client.AssignedJob.Variant);
                    crewManager.AddCharacterInfo(client.CharacterInfo);

                    clientCount++;
                }

                var botCount = 0;
                if (serverSettings.BotSpawnMode == BotSpawnMode.Fill) botCount = Math.Max(serverSettings.BotCount - clientCount, 0); // fill bots
                else botCount = Math.Max(serverSettings.BotCount, 0); // add bots

                for (var i = 0; i < botCount; i++) {
                    var botInfo = new CharacterInfo("human");
                    botInfo.TeamID = CharacterTeamType.Team1;
                    crewManager.AddCharacterInfo(botInfo);
                }
            }
            crewManager.HasBots = true;

            foreach(var ci in crewManager.GetCharacterInfos()) LuaCsSetup.PrintCsMessage($"  --==  {ci.ID} - {ci.Name}  {ci.Job}");

            // do not spawn clients
            foreach (var client in Client.ClientList) client.SpectateOnly = true;
        }

        public bool OnSaveMultiplayer(CrewManager self, XElement root) {
            LuaCsSetup.PrintCsMessage("[MCM-SERVER] Saving multiplayer campaign");
            var saveElement = new XElement("bots", new XAttribute("hasbots", self.HasBots));
            root.Add(saveElement);

            return true;
        }

        public static Client CreateDummy(Character character, CharacterInfo charInfo) {
            var dummyC = new Client(charInfo.Name, 127);
            dummyC.CharacterInfo = charInfo;
            dummyC.Character = character;
            dummyC.Connection = new PipeConnection(0);
            dummyC.SteamID = PipeIndex;

            return dummyC;
        }

        public void OnBeforeSavePlayers() {
            LuaCsSetup.PrintCsMessage("[MCM-SERVER] Saving players (pre-process)");
            var crewManager = GameMain.GameSession?.CrewManager;
            var str = "Saving crew characters:";

            Control.UpdateAwaiting();
            // remove actual
            CharacterData.Clear();
            PipeIndex = 0;
            // add dummy
            foreach (var character in Character.CharacterList.Where(c => c.TeamID == CharacterTeamType.Team1 && c.Info != null)) {
                // Team1, meaning default crew

                if (crewManager.GetCharacterInfos().Any(ci => character.Info == ci)) { // if not fired
                    if (!character.IsDead) {
                        character.Info.TeamID = character.TeamID;
                        CharacterCampaignData charData = null;
                        using (var dummy = CreateDummy(character, character.Info)) charData = new CharacterCampaignData(dummy);
                        CharacterData.Add(charData);
                        str += $"\n    {character.ID} | {character.Name} - OK";
                    }
                    else if (!McmMod.Config.AllowRespawns) {
                        str += $"\n    {character.ID} | {character.Name} - Dead";
                    }
                }
            }
            // add dead
            if (McmMod.Config.AllowRespawns) {
                foreach (var charInfo in Control.Awaiting) {
                    charInfo.ClearCurrentOrders();
                    charInfo.RemoveSavedStatValuesOnDeath();
                    charInfo.CauseOfDeath = null;

                    if (McmMod.Config.RespawnPenalty) RespawnManager.ReduceCharacterSkills(charInfo);
                    charInfo.StartItemsGiven = false;

                    CharacterCampaignData charData = null;
                    using (var dummy = CreateDummy(null, charInfo)) charData = new CharacterCampaignData(dummy);

                    CharacterData.Add(charData);
                    str += $"\n    {charInfo.ID} | {charInfo.Name} - Respawning";
                }
            }
            // add new hires
            foreach (var charInfo in crewManager.GetCharacterInfos().Where(c => c.IsNewHire)) {
                CharacterCampaignData charData = null;
                using (var dummy = CreateDummy(null, charInfo)) charData = new CharacterCampaignData(dummy);

                CharacterData.Add(charData);
                str += $"\n    {charInfo.ID} | {charInfo.Name} - New Hire";
            }
            // log status
            LuaCsSetup.PrintCsMessage(str);
        }

        public void OnAfterSavePlayers() {
            LuaCsSetup.PrintCsMessage("[MCM-SERVER] Saving players (post-process)");

            // remove actual new
            foreach (var character in Character.CharacterList.Where(c => c.TeamID == CharacterTeamType.Team1)) {
                // Team1, meaning default crew
                CharacterData = CharacterData.Where(cd => cd.ClientEndPoint == "PIPE").ToList();
            }
        }

        public void RestoreCharactersWallets() {
            foreach(var charData in CharacterData) {
                if (charData.WalletData != null) {
                    var character = Character.CharacterList.FirstOrDefault(c => charData.CharacterInfo == c.Info);
                    if (character != null) {
                        character.Wallet = new Wallet(Option<Character>.Some(character), charData.WalletData);
                    }
                }
            }
        }
    }
}