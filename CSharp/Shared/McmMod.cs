using System;
using Barotrauma;
using Barotrauma.Networking;

namespace MultiplayerCrewManager {
    partial class McmMod : ACsMod {
        public static bool IsCampaign => GameMain.GameSession?.GameMode is MultiPlayerCampaign;
        public static bool IsRunning => GameMain.GameSession?.IsRunning ?? false;
        public McmMod() {
            #if SERVER
                InitServer();
            #elif CLIENT
                InitClient();
            #endif
        }

        public override void Stop() { }
    }
}