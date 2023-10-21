using System;
using Barotrauma;

namespace MultiplayerCrewManager {
    partial class McmMod {
        public class McmConfig {
            public int ServerUpdateFrequency = 15;
            public bool AllowSpawnNewClients = false;
            public bool AllowRespawns = false;
            public bool SecureEnabled = false;
            public bool RespawnPenalty = true;
            public float RespawnTime = 180;
            public float RespawnDelay = 5;

            public McmConfig() { }
        }

        public static string GetConfigFilePath => System.IO.Path.Combine(ACsMod.GetStoreFolder<McmMod>(), "McmConfig.xml");
        public static McmConfig Config { get; private set; }

        public static void LoadConfig() {
            if (LuaCsFile.Exists(GetConfigFilePath))
                Config = LuaCsConfig.Load<McmConfig>(GetConfigFilePath);
            else Config = new McmConfig();
        }
        public static void SaveConfig() {
            LuaCsConfig.Save(GetConfigFilePath, Config);
        }
    }
}