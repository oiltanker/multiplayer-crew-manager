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
    /// MCM sub module to manage crew reserve in-game. Cause of hard to resolve some conflicts it's
    /// made as static.
    /// </summary>
    /// <seealso cref="https://evilfactory.github.io/LuaCsForBarotrauma/cs-docs/baro-server/html/namespace_barotrauma.html"/>
    static class McmReserve
    {
        /// <summary>
        /// Stores filepath to the file which keeps data about crew reserve. Unique for each campaign.
        /// Looks like '<campaign_name>_ReserveStock.xml'
        /// </summary>
        private static readonly string reserveFilepath = $"{Barotrauma.IO.Path.GetFullPath(GameMain.GameSession.SavePath).Substring(0, GameMain.GameSession.SavePath.Length-5) + "_ReserveStock.xml"}";

        /// <summary>
        /// Check and create data file if need at the first class call
        /// </summary>
        static McmReserve() {
            checkOrCreateFile(reserveFilepath);
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
                    LuaCsFile.Write(reserveFilepath, $"<!-- Multiplayer Crew Manager | reserve-crew file -->{Environment.NewLine}");
                    LuaCsSetup.PrintCsMessage($"[MCM-SERVER] Created reserve-crew file in: {reserveFilepath}");
                }
                else
                {
                    LuaCsSetup.PrintCsMessage($"[MCM-SERVER] Found reserve file in: {reserveFilepath}");
                }
            } 
            catch(Exception e)
            {
                LuaCsSetup.PrintCsMessage($"[MCM-SERVER] Failed to create reserve file: {e.Message}");
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

        public static void putCharacterToReserve(int charId, Client client) 
        {
            if (!McmMod.IsCampaign || !isReserveFileExists()) return;
            string msg = string.Empty;
            // Get character who need to reserve
            var character = Character.CharacterList.FirstOrDefault(c => charId == c.ID && c.TeamID == CharacterTeamType.Team1);
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
            // work flow for the new file
            // Get data file content for further manipulations
            String FileContent = LuaCsFile.Read(reserveFilepath);
            // Create xml structure and write to file
            XElement CharacterCampaignData = new XElement("CharacterCampaignData");
            CharacterCampaignData.Add(
                new XAttribute("name", character.Info.Name), 
                new XAttribute("address", "PIPE"), 
                new XAttribute("accountid", "STEAM64_0"));
            XElement CharData = character.Info.Save(CharacterCampaignData);
            FileContent += CharacterCampaignData.ToString();
            LuaCsFile.Write(reserveFilepath, FileContent);
            //TODO add work flow for the file which already have the data
            // Send chat msg to client
            sendChatMsg(msg: $"Character sent to reserve: '{character.Info.Name}' (ID: {character.ID})", messageType: ChatMessageType.Server, client: client);
            LuaCsSetup.PrintCsMessage($"[MCM-SERVER] Character sent to reserve: '{character.Info.Name}' (ID: {character.ID})");
            // Remove the character from the current game session and campaign
            Entity.Spawner.AddEntityToRemoveQueue(character);

            //TODO ensure consistency and uniqueness of data
        }

        //public static void getCharacterFromReserve(Character character) 
        //{
        //    //TODO
        //}

        //public static List<CharacterCampaignData> reserveList() 
        //{
        //    //TODO
        //}
    }
}