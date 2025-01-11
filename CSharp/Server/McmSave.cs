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
        private MethodInfo reduceCharacterSkillsMethod = typeof(RespawnManager).GetMethod("ReduceCharacterSkills", BindingFlags.Static | BindingFlags.Public);
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

            McmUtils.Info("Loading multiplayer campaign");
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
                    McmUtils.Warn("One or more characters was found in save but are unknown to MCM - Has this mod been added to a running campaign?");
                    foreach (var c in ExistingCharacters)
                    {
                        McmUtils.Trace($"Importing character. Name [{c.CharacterInfo.Name}] Job [{c.CharacterInfo.Job.Name}] Experience [{c.CharacterInfo.ExperiencePoints}] Level [{c.CharacterInfo.GetCurrentLevel()}]");

                        #region
                        //Unsure why we need to set these fields here - but without them the resulting dummy lacks both fields... Maybe this data isn't loaded at this point?
                        //Bots seem unaffected by this bug; maybe due in fact to them not having CharacterCampaignData before being imported?
                        if (c.CharacterInfo.InventoryData == null)
                            c.CharacterInfo.InventoryData = (XElement)itemData.GetValue(c);
                        if (c.CharacterInfo.HealthData == null)
                            c.CharacterInfo.HealthData = (XElement)healthData.GetValue(c);

                        #endregion

                        CharacterCampaignData charData = null;
                        using (var dummy = CreateDummy(null, c.CharacterInfo)) charData = new CharacterCampaignData(dummy);

                        //Migrate wallet data to new character
                        McmUtils.Trace($"[{c.CharacterInfo.Name}] - Transferring wallet data [{c.WalletData}]");
                        charData.WalletData = c.WalletData;



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
                    McmUtils.Trace($"Removed {removed} character infos that was already imported");
                if (preservedCharacters.Any())
                {
                    McmUtils.Trace($"Detected [{preservedCharacters.Count()}] bots");
                    foreach (var charInfo in preservedCharacters)
                    {
                        McmUtils.Trace($"Importing Bot [{charInfo.ID}] | {charInfo.Name}");

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
                    McmUtils.Trace("Campaign has been imported.");
                }
            }




            IsNewCampaign = SavedPlayers.Count <= 0;
            IsCampaignLoaded = true;
        }

        public void OnStartGame()
        {
            if (!McmMod.IsCampaign) return;
            if (!IsCampaignLoaded) OnLoadCampaign();
            McmUtils.Info("Loading campaign...");
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
                        //Potential Fix described in Pull Request 28
                        //https://github.com/oiltanker/multiplayer-crew-manager/pull/28

                        //if (charInfo.StartItemsGiven) 
                        //{
                        //}
                        McmUtils.Warn($"Unit [{p.CharacterInfo.Name}] was missing inventory-data - Defaulting to CharacterInfo.InventoryData");
                        inventoryData = charInfo.InventoryData;
                    }
                    if (hpData == null) //If unable to read from file; attempt to fall back on current health data
                    {
                        McmUtils.Warn($"Unit [{p.CharacterInfo.Name}] was missing health-data, Defaulting to CharacterInfo.HealthData");
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
                    client.CharacterInfo.Job = new Job(client.AssignedJob.Prefab, false, Rand.RandSync.Unsynced, client.AssignedJob.Variant);
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
            McmUtils.Info("Saving multiplayer campaign");
            var saveElement = new XElement("bots", new XAttribute("hasbots", self.HasBots));
            root?.Add(saveElement);
            return saveElement;
        }

        public static void ImportSaveProtection()
        {
            if (RequireImportSaving)
            {
                McmUtils.Info("Saving imported characters");
                foreach (var characterData in CharacterData)
                {
                    var character = Character.CharacterList.Find(x => x.Info == characterData.CharacterInfo);
                    if (character != null)
                    {
                        McmUtils.Info($"Refreshing character [{characterData.CharacterInfo.ID}] | [{characterData.CharacterInfo.Name}]");
                        characterData.Refresh(character, true);
                    }
                }

                Barotrauma.SaveUtil.SaveGame(GameMain.GameSession.DataPath);
                RequireImportSaving = false;
            }
        }

        public static Client CreateDummy(Character character, CharacterInfo charInfo)
        {
            var dummyC = new Client(charInfo.Name, 127);

            //due to changes in LUA/CS base-lib we need to do some funky magic here to be able to create a new dummy client
            var steamAcc = new Barotrauma.Networking.SteamId(pipeIndex);
            var conn = new PipeConnection(Barotrauma.Option.Some<AccountId>(steamAcc as AccountId));
            dummyC.AccountInfo = new Barotrauma.Networking.AccountInfo(steamAcc);

            dummyC.CharacterInfo = charInfo;
            dummyC.Character = character;
            dummyC.Connection = conn; //insert pipe connection here

            //dummyC.SteamID = PipeIndex; <-- this value can no longer be set like this since SteamID has become an exclusive "Get" property

            return dummyC;
        }

        public void OnBeforeSavePlayers()
        {
            McmUtils.Info("Saving players (pre-process)");
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

                if (crewManager.GetCharacterInfos().Any(ci => character.Info == ci) || character.IsRemotePlayer)
                { // if not fired
                    if (false == character.IsDead)
                    {
                        character.Info.TeamID = character.TeamID;
                        CharacterCampaignData charData = null;
                        using (var dummy = CreateDummy(character, character.Info)) charData = new CharacterCampaignData(dummy);
                        CharacterData.Add(charData);
                        str += $"\n    {character.ID} | {character.Name} - OK";
                    }
                    else if (McmMod.Config.RespawnMode != RespawnMode.MidRound)
                    {
                        str += $"\n    {character.ID} | {character.Name} - Dead";
                    }
                }
            }
            // add dead
            if (McmMod.Config.RespawnMode is RespawnMode.MidRound or RespawnMode.BetweenRounds)
            {
                // Added dead character data to the waiting list in BetweenRounds respawn mode
                foreach (var a in Control.BetweenRoundsAwaiting)
                {
                    Control.Awaiting.Add(a);
                }
                foreach (var charInfo in Control.Awaiting)
                {
                    charInfo.ClearCurrentOrders();
                    charInfo.RemoveSavedStatValuesOnDeath();
                    charInfo.CauseOfDeath = null;

                    var skillLoss = GameMain.Server.ServerSettings.SkillLossPercentageOnDeath;
                    if (skillLoss > 0f)
                    {
                        if (reduceCharacterSkillsMethod != null)
                        {
                            McmUtils.Trace($"Inflicting skill reduction [{skillLoss}%] on charaacter \"{charInfo.Name}\"");
                            reduceCharacterSkillsMethod.Invoke(null, new object[] { charInfo });
                        }
                    }
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
            McmMod.Instance.Control.BetweenRoundsAwaiting.Clear();
            // log status
            McmUtils.Info(str);
        }

        public void OnAfterSavePlayers()
        {
            McmUtils.Info("Saving players (post-process)");

            // remove actual new
            foreach (var character in Character.CharacterList.Where(c => c.TeamID == CharacterTeamType.Team1))
            {
                // Team1, meaning default crew
                CharacterData = CharacterData.Where(cd => cd.ClientAddress.ToString() == "PIPE").ToList();
            }
        }

        public void RestoreCharactersWallets()
        {
            McmUtils.Info("Restoring Wallets ...");
            var toCheckAgainst = Character.CharacterList.ToList(); //Get a copy from the entire CharacterList
            toCheckAgainst.RemoveAll(x => x.Info == null); //Remove all entries that doesn't have a valid CharacterInfo. (Can this even happen?)

            foreach (var charData in CharacterData)
            {
                if (charData.CharacterInfo == null) //No need to check characters that can't be identified
                {
                    McmUtils.Error($"Character Data [{charData.Name}] was missing CharacterInfo. Unable to restore wallet for this character");
                    continue;
                }

                if (charData.WalletData == null) //Character missing a wallet?
                {
                    McmUtils.Error($"Could not find any Wallet-data for character [{charData.Name}] - Unable to restore wallet for this character");
                    continue;
                }

                var character = toCheckAgainst?.FirstOrDefault(c => c.Info.GetIdentifier() == charData.CharacterInfo.GetIdentifier()); //Attempt to find our character based on CharacterInfo-matching

                if (character == null) //Failed to find a matching character
                {
                    McmUtils.Error($"Unable to find character information matching [{charData.CharacterInfo?.GetIdentifier().ToString() ?? "UNAVAILABLE"}]");
                    continue;
                }


                character.Wallet = new Wallet(Option<Character>.Some(character), charData.WalletData); //Wallet restored
                McmUtils.Trace($"Restoring wallet for character : [{charData.Name}] : [{charData.WalletData}]");

            }
            McmUtils.Info("Wallets restored.");
        }
    }
}
