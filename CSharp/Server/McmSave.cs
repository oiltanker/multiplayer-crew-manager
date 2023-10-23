using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using Barotrauma;
using Barotrauma.Networking;
using System.Security.Cryptography.X509Certificates;

namespace MultiplayerCrewManager
{
    class McmSave
    {
        private static ulong pipeIndex = 0;
        public static ulong PipeIndex
        {
            get
            {
                pipeIndex++;
                return pipeIndex;
            }
            set => pipeIndex = value;
        }

        private readonly static FieldInfo itemData = typeof(CharacterCampaignData).GetField("itemData", BindingFlags.NonPublic | BindingFlags.Instance);
        private readonly static FieldInfo healthData = typeof(CharacterCampaignData).GetField("healthData", BindingFlags.NonPublic | BindingFlags.Instance);
        private readonly static FieldInfo cdField = typeof(MultiPlayerCampaign).GetField("characterData", BindingFlags.NonPublic | BindingFlags.Instance);
        public static List<CharacterCampaignData> CharacterData
        {
            get => cdField.GetValue(GameMain.GameSession.GameMode as MultiPlayerCampaign) as List<CharacterCampaignData>;
            set => cdField.SetValue(GameMain.GameSession.GameMode as MultiPlayerCampaign, value);
        }
        public List<CharacterCampaignData> SavedPlayers { get; private set; }

        public bool IsCampaignLoaded { get; set; }
        public bool IsNewCampaign { get; set; }
        public bool PreserveExistingCharacters { get; set; }
        public McmControl Control { get; set; }
        public static bool RequireImportSaving { get; set; } = false;

        public McmSave(McmControl control)
        {
            Control = control;

            SavedPlayers = null;
            IsCampaignLoaded = false;
            IsNewCampaign = false;
            PreserveExistingCharacters = false;
        }

        public void OnLoadCampaign()
        {
            if (!McmMod.IsCampaign) return;

            LuaCsSetup.PrintCsMessage("[MCM-SERVER] Loading multiplayer campaign");
            SavedPlayers = CharacterData.Where(c => c.ClientAddress.ToString() == "PIPE").ToList();
            //Saved connections will have their enpoint represented by a new value "Hidden" - So we add all the "Hidden" connections to the list of saved players - this might cause som unintended bugs
            SavedPlayers.AddRange(CharacterData.Where(c => c.ClientAddress.ToString() == "Hidden"));

            //If the mod is added into an ongoing campaign we might have existing player toons that is not stored in our SavedPlayers
            if (SavedPlayers.Count == 0)
            {
                //var ExistingCharacters = CharacterData.Where(x => x.CharacterInfo.ExperiencePoints > 0).ToList(); //Add any character that has more than 0 XP
                var ExistingCharacters = CharacterData.ToList(); //Add all known CharacterCampaignData (this should only be player characters... right?)
                if (ExistingCharacters.Any())
                {
                    LuaCsSetup.PrintCsMessage("[MCM-SERVER] Warning! - No saved players was detected in mcm, But one or more \"non-zero\" EXP characters was found in save - Has this mod been added to a running campaign?");
                    foreach (var c in ExistingCharacters)
                    {
                        LuaCsSetup.PrintCsMessage($"[MCM-SERVER] Importing character. Name [{c.CharacterInfo.Name}] Job [{c.CharacterInfo.Job.Name}] Experience [{c.CharacterInfo.ExperiencePoints}] Level [{c.CharacterInfo.GetCurrentLevel()}]");
                        CharacterCampaignData charData = null;
                        using (var dummy = CreateDummy(null, c.CharacterInfo)) charData = new CharacterCampaignData(dummy);

                        CharacterData.Remove(c);
                        CharacterData.Add(charData);
                        SavedPlayers.Add(charData);
                    }
                }

                var crewManager = GameMain.GameSession.CrewManager;
                List<ushort> alreadySavedIds = SavedPlayers.Select(x => x.CharacterInfo.ID).ToList();
                List<CharacterInfo> preservedCharacters = crewManager.GetCharacterInfos().Where(c => false == c.IsNewHire).ToList();
                var removed = preservedCharacters.RemoveAll(x => alreadySavedIds.Contains(x.ID));
                if (removed > 0)
                    LuaCsSetup.PrintCsMessage($"[MCM-SERVER] Removed {removed} character infos that was already imported");
                if (preservedCharacters.Any())
                {
                    LuaCsSetup.PrintCsMessage($"[MCM-SERVER] Detected [{preservedCharacters.Count()}] bots");
                    foreach (var charInfo in preservedCharacters)
                    {
                        LuaCsSetup.PrintCsMessage($"[MCM-SERVER] Importing Bot [{charInfo.ID}] | {charInfo.Name}");

                        CharacterCampaignData charData = null;
                        using (var dummy = CreateDummy(null, charInfo)) charData = new CharacterCampaignData(dummy);

                        SavedPlayers.Add(charData);
                        CharacterData.Add(charData);
                    }
                }

                if (SavedPlayers.Count() > 0)
                {
                    //Players and bots have been imported
                    RequireImportSaving = true;
                    LuaCsSetup.PrintCsMessage($"[MCM-SERVER] Campaign has been imported");
                }
            }




            IsNewCampaign = SavedPlayers.Count <= 0;
            IsCampaignLoaded = true;
        }

        public void OnStartGame()
        {
            if (!McmMod.IsCampaign) return;
            if (!IsCampaignLoaded) OnLoadCampaign();

            var crewManager = GameMain.GameSession.CrewManager;
            foreach (var ci in crewManager.GetCharacterInfos().ToArray()) crewManager.RemoveCharacterInfo(ci);


            if (false == IsNewCampaign) //Restore saved characters
            {
                SavedPlayers.ForEach(p =>
                {
                    var charInfo = p.CharacterInfo;
                    XElement inventoryData = (XElement)itemData.GetValue(p); //Attempt to read from file
                    XElement hpData = (XElement)healthData.GetValue(p); //Attempt to read from file
                    if (inventoryData == null) //If unable to read from file; attempt to fall back on current inventory data
                    {
                        LuaCsSetup.PrintCsMessage($"[MCM-SERVER] Unit [{p.CharacterInfo.Name}] was missing inventorydata, attmepting to default");
                        inventoryData = charInfo.InventoryData;
                    }
                    if (hpData == null) //If unable to read from file; attempt to fall back on current health data
                    {
                        LuaCsSetup.PrintCsMessage($"[MCM-SERVER] Unit [{p.CharacterInfo.Name}] was missing healthdata, attmepting to default");
                        hpData = charInfo.HealthData;
                    }
                    charInfo.InventoryData = inventoryData;
                    charInfo.HealthData = hpData;

                    charInfo.OrderData = p.OrderData;
                    charInfo.IsNewHire = false;
                    crewManager.AddCharacterInfo(charInfo);
                });
            }
            else
            {
                var serverSettings = GameMain.Server.ServerSettings;
                var clientCount = 0;

                foreach (var client in Client.ClientList)
                {
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

                for (var i = 0; i < botCount; i++)
                {
                    var botInfo = new CharacterInfo("human");
                    botInfo.TeamID = CharacterTeamType.Team1;
                    crewManager.AddCharacterInfo(botInfo);
                }
            }
            crewManager.HasBots = true;


            // do not spawn clients
            foreach (var client in Client.ClientList) client.SpectateOnly = true;
        }

        public static XElement OnSaveMultiplayer(CrewManager self, XElement root)
        {
            LuaCsSetup.PrintCsMessage("[MCM-SERVER] Saving multiplayer campaign");
            var saveElement = new XElement("bots", new XAttribute("hasbots", self.HasBots));
            root?.Add(saveElement);
            return saveElement;
        }

        public static void ImportSaveProtection()
        {
            if (RequireImportSaving)
            {
                LuaCsSetup.PrintCsMessage("[MCM-SERVER] Saving imported characters");
                foreach (var characterData in CharacterData)
                {
                    var character = Character.CharacterList.Find(x => x.Info == characterData.CharacterInfo);
                    if (character != null)
                    {
                        LuaCsSetup.PrintCsMessage($"[MCM-SERVER] Refreshing character [{characterData.CharacterInfo.ID}] | [{characterData.CharacterInfo.Name}]");
                        characterData.Refresh(character, true);
                    }
                }

                Barotrauma.SaveUtil.SaveGame(GameMain.GameSession.SavePath);
                RequireImportSaving = false;
            }
        }

        public static Client CreateDummy(Character character, CharacterInfo charInfo)
        {
            var dummyC = new Client(charInfo.Name, 127);

            //due to changes in LUA/CS base-lib we need to do some funky magic here to be able to create a new dummy client
            var steamAcc = new Barotrauma.Networking.SteamId(pipeIndex);
            var conn = new PipeConnection(steamAcc as AccountId);
            dummyC.AccountInfo = new Barotrauma.Networking.AccountInfo(steamAcc);

            dummyC.CharacterInfo = charInfo;
            dummyC.Character = character;
            dummyC.Connection = conn; //insert pipe connection here

            //dummyC.SteamID = PipeIndex; <-- this value can no longer be set like this since SteamID has become an exclusive "Get" property

            return dummyC;
        }

        public void OnBeforeSavePlayers()
        {
            LuaCsSetup.PrintCsMessage("[MCM-SERVER] Saving players (pre-process)");
            var crewManager = GameMain.GameSession?.CrewManager;
            var str = "Saving crew characters:";

            Control.UpdateAwaiting();
            // remove actual
            CharacterData.Clear();
            PipeIndex = 0;
            // add dummy
            foreach (var character in Character.CharacterList.Where(c => c.TeamID == CharacterTeamType.Team1 && c.Info != null))
            {
                // Team1, meaning default crew

                if (crewManager.GetCharacterInfos().Any(ci => character.Info == ci))
                { // if not fired
                    if (!character.IsDead)
                    {
                        character.Info.TeamID = character.TeamID;
                        CharacterCampaignData charData = null;
                        using (var dummy = CreateDummy(character, character.Info)) charData = new CharacterCampaignData(dummy);
                        CharacterData.Add(charData);
                        str += $"\n    {character.ID} | {character.Name} - OK";
                    }
                    else if (!McmMod.Config.AllowRespawns)
                    {
                        str += $"\n    {character.ID} | {character.Name} - Dead";
                    }
                }
            }
            // add dead
            if (McmMod.Config.AllowRespawns)
            {
                foreach (var charInfo in Control.Awaiting)
                {
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
            foreach (var charInfo in crewManager.GetCharacterInfos().Where(c => c.IsNewHire))
            {
                CharacterCampaignData charData = null;
                using (var dummy = CreateDummy(null, charInfo)) charData = new CharacterCampaignData(dummy);

                CharacterData.Add(charData);
                str += $"\n    {charInfo.ID} | {charInfo.Name} - New Hire";
            }
            // log status
            LuaCsSetup.PrintCsMessage(str);
        }

        public void OnAfterSavePlayers()
        {
            LuaCsSetup.PrintCsMessage("[MCM-SERVER] Saving players (post-process)");

            // remove actual new
            foreach (var character in Character.CharacterList.Where(c => c.TeamID == CharacterTeamType.Team1))
            {
                // Team1, meaning default crew
                CharacterData = CharacterData.Where(cd => cd.ClientAddress.ToString() == "PIPE").ToList();
            }
        }

        public void RestoreCharactersWallets()
        {
            foreach (var charData in CharacterData)
            {
                if (charData.WalletData != null)
                {
                    var character = Character.CharacterList?.FirstOrDefault(c => charData.CharacterInfo.GetIdentifier() == c.Info.GetIdentifier());
                    if (character != null)
                    {
                        character.Wallet = new Wallet(Option<Character>.Some(character), charData.WalletData);
                    }
                }
            }
        }
    }
}