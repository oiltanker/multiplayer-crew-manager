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
    //TODO still have issues with writeaccess
    static class McmReserve
    {
        private static readonly string reserveFilepath = $"{Barotrauma.IO.Path.GetFullPath(GameMain.GameSession.SavePath).Substring(0, GameMain.GameSession.SavePath.Length-5) + "_ReserveStock.xml"}";
        // private static readonly string reserveFilepath = savegameFilepath.Substring(0,savegameFilepath.Length-5) + "_ReserveStock.xml";

        // .save_ReserveStock.xml
        // /home/xardion/.local/share/Daedalic Entertainment GmbH/Barotrauma/Multiplayer/test1.save
        //TODO need fullpath without extension

        private static McmClientManager _clientManager = new McmClientManager();

        private static void createReserveFileIfNeed(string reserveFilepath)
        {
            try
            {
                LuaCsSetup.PrintCsMessage($"[MCM-SERVER] reserveFilepath: {reserveFilepath}");
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

        private static bool isReserveFileExists() => LuaCsFile.Exists(reserveFilepath) ? true : false;

        private static void sendErrorChatMsg(string msg, Client client)
        {
            var cm = ChatMessage.Create("[Server]", msg, ChatMessageType.Error, null, client);
            cm.IconStyle = "StoreShoppingCrateIcon";
            GameMain.Server.SendDirectChatMessage(cm, client);
            return;
        }

        public static void putCharacterToReserve(int charId, Client client) 
        {
            createReserveFileIfNeed(reserveFilepath);
            if (!McmMod.IsCampaign || !isReserveFileExists()) return;
            var msg = string.Empty;
            var character = Character.CharacterList.FirstOrDefault(c => charId == c.ID && c.TeamID == CharacterTeamType.Team1);
            var crewManager = GameMain.GameSession.CrewManager;
            var CharacterData = McmSave.CharacterData;
            if (character == null) 
            {
                msg = $"[MCM] Failed to send character to the reserve: Character ID [{charId}] not found";
                sendErrorChatMsg(msg, client);
                return;
            }
            if (_clientManager.IsCurrentlyControlled(character))
            {
                msg = $"[MCM] Failed to send character to the reserve: '{character.DisplayName}' is already in use";
                sendErrorChatMsg(msg, client);
                return;
            }
            var targetCharacterDataElem = CharacterData.Where(c => c.CharacterInfo.ID == charId).ToString();
            string fileContents = LuaCsFile.Read(reserveFilepath);
            fileContents.Concat(targetCharacterDataElem);
            LuaCsFile.Write(path:reserveFilepath, text:fileContents);
            LuaCsSetup.PrintCsMessage($"[MCM-SERVER] Character saved");
            //TODO
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