using System;
using System.Collections.Generic;
using System.Runtime.Remoting.Channels;
using Barotrauma;
using Barotrauma.IO;

namespace MultiplayerCrewManager
{
    class McmReserve
    {
        private McmSave _mcmSave;
        private McmClientManager _clientManager;
        public McmReserve(McmSave mcmSave, McmClientManager clientManager)
        {
            _mcmSave = mcmSave;
            _clientManager = clientManager;
        }
        private readonly string reserveFilepath = $"{Path.GetFileNameWithoutExtension(GameMain.GameSession.SavePath)}" + "_ReserveStock.xml";

        public bool isReserveFileExists() => LuaCsFile.Exists(reserveFilepath) ? true : false;

        private void sendErrorChatMsg(string msg, Client client)
        {
            var cm = ChatMessage.Create("[Server]", msg, ChatMessageType.Error, null, client);
            cm.IconStyle = "StoreShoppingCrateIcon";
            GameMain.Server.SendDirectChatMessage(cm, client);
            return;
        }

        public void putCharacterToReserve(int charId, Client client) 
        {
            if (!McmMod.IsCampaign) return;
            string msg = string.Empty;
            var character = Character.CharacterList.FirstOrDefault(c => charId == c.ID && c.TeamID == CharacterTeamType.Team1);
            var crewManager = GameMain.GameSession.CrewManager;
            _mcmSave.SavedPlayers = _mcmSave.CharacterData.Where(c => c.ClientAddress.ToString() == "PIPE").ToList();
            _mcmSave.SavedPlayers.AddRange(CharacterData.Where(c => c.ClientAddress.ToString() == "Hidden"));
            if (character == null) 
            {
                msg = $"[MCM] Failed to send character to the reserve: Character ID [{charId}] not found";
                sendErrorChatMsg(msg, client);
                return;
            }
            if (_clientManager.IsCurrentlyControlled(character))
            {
                msg = $"[MCM] Failed to gain control: '{character.DisplayName}' is already in use";
                sendErrorChatMsg(msg, client);
                return;
            }
            //TODO
        }

        public static void getCharacterFromReserve(Character character) 
        {
            //TODO
        }

        public static List<CharacterCampaignData> reserveList() 
        {
            //TODO
        }
    }
}