using System;
using System.Collections.Generic;
using Barotrauma;
using Barotrauma.IO;

namespace MultiplayerCrewManager
{
    partial class McmMod
    {
        class McmReserve
        {
            public McmReserve() { }
            private static readonly string reserveFilepath = $"{Path.GetFileNameWithoutExtension(GameMain.GameSession.SavePath)}" + "_ReserveStock";

            public static bool isReserveFileExists() {
                if (LuaCsFile.Exists(reserveFilepath)) {
                    return true;
                } else {
                    return false;
                }
            }
            public static void putCharacterToReserve(Character character) {
                //TODO
            }

            public static void getCharacterFromReserve(Character character) {
                //TODO
            }

            public static List<CharacterCampaignData> reserveList() {
                //TODO
            }
        }
    }
}