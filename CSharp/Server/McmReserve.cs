using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Barotrauma;
using Barotrauma.IO;
using Barotrauma.Networking;

namespace MultiplayerCrewManager
{
    /// <summary>
    /// MCM sub module to manage crew reserve in-game. Cause of hard to resolve some conflicts it's made as static
    /// </summary>
    /// <seealso cref="https://evilfactory.github.io/LuaCsForBarotrauma/cs-docs/baro-server/html/namespace_barotrauma.html"/>
    static class McmReserve
    {
        /// <summary>
        /// Stores filepath to the file which keeps data about crew reserve. Unique for each campaign.
        /// Looks like '[campaign_name]_ReserveStock.xml'
        /// </summary>
        private static readonly string reserveFilepath = $"{Barotrauma.IO.Path.GetFullPath(GameMain.GameSession.SavePath).Substring(0, GameMain.GameSession.SavePath.Length-5) + "_ReserveStock.xml"}";

        /// <summary>
        /// Check and create data file if need at the first class call
        /// </summary>
        static McmReserve() {
            checkOrCreateFile(reserveFilepath: reserveFilepath);
        }

        /// <summary>
        /// Create an instance of McmClientManager for some chekcs
        /// </summary>
        private static McmClientManager _clientManager = new McmClientManager();

        /// <summary>
        /// Create data file if it's not exist
        /// </summary>
        /// <param name="reserveFilepath"></param>
        private static void checkOrCreateFile(string reserveFilepath)
        {
            try
            {
                if (!isReserveFileExists())
                {
                    string text = 
                    $"<!-- Multiplayer crew manager data file -->{Environment.NewLine}" +
                    $"<!-- Do NOT edit this file by yourself! -->{Environment.NewLine}" +
                    "<CrewReserve></CrewReserve>";
                    LuaCsFile.Write(reserveFilepath, text);
                    McmUtils.Trace($"Created reserve-crew file in: {reserveFilepath}");
                }
                else
                {
                    McmUtils.Trace($"Found reserve file in: {reserveFilepath}");
                }
            } 
            catch(Exception e)
            {
                McmUtils.Error(e, "Failed to create reserve file");
            }
        }

        /// <summary>
        /// Wrapper for LuaCsFile checker
        /// </summary>
        private static bool isReserveFileExists() => LuaCsFile.Exists(reserveFilepath) ? true : false;

        /// <summary>
        /// Send in-game message directly to the client who initialize mcm command
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="messageType">
        /// This data type coming from LuaCsBarotrauma overhead
        /// </param>
        /// <param name="client">
        /// This data type coming from LuaCsBarotrauma overhead
        /// </param>
        private static void sendChatMsg(string msg, ChatMessageType messageType, Client client)
        {
            var cm = ChatMessage.Create("[Server]", msg, messageType, null, client);
            cm.IconStyle = "StoreShoppingCrateIcon";
            GameMain.Server.SendDirectChatMessage(cm, client);
            return;
        }

        /// <summary>
        /// Send message to server log and to the client who initialize mcm command 
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="messageType">
        /// This data type coming from LuaCsBarotrauma overhead
        /// </param>
        /// <param name="client">
        /// This data type coming from LuaCsBarotrauma overhead
        /// </param>
        private static void sendMsgToBoth(string msg, ChatMessageType messageType, Client client)
        {
            sendChatMsg(msg, messageType, client);
            McmUtils.Info(msg);
            return;
        }

        /// <summary>
        /// Provide the feature to save and hide character immediately in the mid-round.
        /// </summary>
        /// <param name="charId"></param>
        /// <param name="client"></param>
        /// <param name="isForce"></param>
        public static void putCharacterToReserve(int charId, Client client, bool isForce = false) 
        {
            if (!McmMod.IsCampaign || !isReserveFileExists()) return;
            string msg = string.Empty;
            // Get character who need to reserve
            var character = Character.CharacterList.FirstOrDefault(c => charId == c.ID && c.TeamID == CharacterTeamType.Team1); // Character class coming from LuaCsBarotrauma overhead
            // Fail if ID is wrong
            if (character == null) 
            {
                msg = $"[MCM] Failed to send character to the reserve: Character ID [{charId}] not found";
                sendChatMsg(msg: msg, messageType: ChatMessageType.Error, client: client);
                return;
            }
            // Fail if targeted character controlled by another client
            if (_clientManager.IsCurrentlyControlled(character: character))
            {
                msg = $"[MCM] Failed to send character to the reserve: '{character.DisplayName}' is already in use";
                sendChatMsg(msg: msg, messageType: ChatMessageType.Error, client: client);
                return;
            }
            // Get xml body from the file
            XDocument xmlFile = XDocument.Load(reserveFilepath);
            // Check if character was written in file before
            var nameCheck = xmlFile.Descendants("CharacterCampaignData").FirstOrDefault(elem => (string)elem.Attribute("name") == $"{character.Info.Name}");
            if (nameCheck != null) {
                if (!isForce) {
                    msg = $"[MCM] Character with name '{character.Info.Name}' is already saved in reserve. If you want to overwrite character data add 'force' keyword to the end of mcm command expression.";
                    sendChatMsg(msg: msg, messageType: ChatMessageType.Error, client: client);
                    return;
                } else {
                    // if 'force' keyword was used remove character data to add the new one later
                    nameCheck.Remove();
                }
            }
            //// Create xml structure and write to file
            // Create campaign character root element
            XElement CharacterCampaignData = new XElement("CharacterCampaignData",
                new XAttribute("name", character.Info.Name), 
                new XAttribute("address", "PIPE"), 
                new XAttribute("accountid", "STEAM64_0"));
            // Grab wallet data from the character as xml element
            XElement characterWalletData = character.Wallet.Save();
            // Grab inventory data from the character as xml element
            XElement characterInventoryData = new XElement("inventory");
            Barotrauma.Character.SaveInventory(character.Inventory, characterInventoryData);
            // Grab health data from the character as xml element
            XElement characterHealthData = new XElement("health");
            character.CharacterHealth.Save(characterHealthData);
            character.Info.Save(CharacterCampaignData); // Character (appearance, job, skills, talents, etc)
            CharacterCampaignData.Add(characterInventoryData); // Inventory
            CharacterCampaignData.Add(characterHealthData); // Health
            CharacterCampaignData.Add(characterWalletData); // Wallet
            xmlFile.Root.Add(CharacterCampaignData);
            // Write updated data to the file
            xmlFile.Save(reserveFilepath);
            // Send notice to the game chat and server log
            msg = $"[MCM] Character sent to reserve: '{character.Info.Name}' (ID: {character.ID})";
            sendMsgToBoth(msg: msg, messageType: ChatMessageType.Error, client: client);
            // Remove the character from the current game session (also from campaign too)
            Entity.Spawner.AddEntityToRemoveQueue(character);
            GameMain.GameSession.CrewManager.RemoveCharacter(character, true, true);
        }

        /// <summary>
        /// Provide feature to get character from the reserve and include it in campaign
        /// </summary>
        /// <param name="ordinal"></param>
        /// <param name="client"></param>
        public static void getCharacterFromReserve(int ordinal, Client client) 
        {
            XDocument xmlFile = XDocument.Load(reserveFilepath);
            int ccdQty = xmlFile.Descendants("CharacterCampaignData").Count();
            string msg = string.Empty;
            if ((ordinal > ccdQty) || (ordinal == 0)) {
                msg = $"[MCM] An invalid id data was input: {ordinal}. Use 'mcm reserve' command to check the valid IDs.";
                sendMsgToBoth(msg: msg, messageType: ChatMessageType.Error, client);
                return;
            }
            ushort k = 0; // index
            foreach(var CCD in xmlFile.Descendants("CharacterCampaignData")) {
                k++;
                if (k == ordinal) {
                    CharacterCampaignData ccdObj = new CharacterCampaignData(CCD);
                    Barotrauma.WayPoint waypoint = null;
                    waypoint = WayPoint.GetRandom(spawnType: SpawnType.Human, assignedJob: ccdObj.CharacterInfo.Job.Prefab, sub: Submarine.MainSub);
                    if (waypoint == null) { // if job is not recognized meaning a custom job, let spawn character at random point inside the sub
                        waypoint = WayPoint.GetRandom(spawnType: SpawnType.Human, assignedJob: null, sub: Submarine.MainSub);
                    }
                    // spawn character
                    var character = Character.Create(characterInfo: ccdObj.CharacterInfo, position: waypoint.WorldPosition, seed: ccdObj.CharacterInfo.Name, isRemotePlayer: false, hasAi: true, spawnInitialItems: false);
                    character.TeamID = CharacterTeamType.Team1;
                    ccdObj.ApplyHealthData(character);
                    ccdObj.ApplyWalletData(character);
                    ccdObj.SpawnInventoryItems(character, character.Inventory);
                    character.LoadTalents();
                    var crewManager = GameMain.GameSession.CrewManager;
                    crewManager.AddCharacter(character); // needed for make character as a part of campaign
                    msg = $"[MCM] Character {ccdObj.Name} loaded.";
                    sendMsgToBoth(msg, ChatMessageType.Server, client);
                    // Remove character from the reserve
                    CCD.Remove();
                    // Write updated data to the file
                    xmlFile.Save(reserveFilepath);
                    break; 
                }
           }          
        }

        /// <summary>
        /// Showing list of reserved characters directly in chat. Maybe rework it to show in message box later.
        /// </summary>
        /// <param name="client">
        /// </param>
        public static void showReserveList(Client client)
        {
            var output = ToString();
            var cm = ChatMessage.Create("[Server]", output, ChatMessageType.Server, null, client);
           cm.IconStyle = "StoreShoppingCrateIcon";
           GameMain.Server.SendDirectChatMessage(cm, client);
        }

        public new static string ToString()
        {
            ushort k = 0;
            XDocument xmlFile = XDocument.Load(reserveFilepath);
            string output = "-= Crew reserve list =-" + Environment.NewLine;
            foreach (var CCD in xmlFile.Descendants("CharacterCampaignData"))
            {
                k++;
                string name = CCD.Attribute("name").Value;
                string job = CCD.Element("Character").Element("job").Attribute("name").Value;
                string wallet = CCD.Element("Wallet").Attribute("balance").Value;
                output += $"#{k} | {job} | {name} | {wallet} credits" + Environment.NewLine;
            }
            if (k == 0)
            {
                output += "No characters in reserve";
            }

            return output;
        }
    }
}