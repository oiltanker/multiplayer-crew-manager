using System;
using System.Xml.Serialization;
using Barotrauma;

namespace MultiplayerCrewManager
{
    public class McmConfig
    {
        public int ServerUpdateFrequency = 15;
        public bool AllowSpawnNewClients = false;
        public bool AllowRespawns = false;
        public bool SecureEnabled = false;
        public bool RespawnPenalty = true;
        public float RespawnTime = 180;
        public float RespawnDelay = 5;

        public McmConfig() { }
    }

    partial class McmMod
    {

        public static McmConfig Config { get; private set; }

        public static void LoadConfig()
        {
            var cfgFilePath = System.IO.Path.Combine(ACsMod.GetStoreFolder<McmMod>(), "McmConfig.xml");
            try
            {
                var serializer = new XmlSerializer(typeof(McmConfig));
                using (var fstream = System.IO.File.OpenRead(cfgFilePath))
                {
                    Config = (McmConfig)serializer.Deserialize(fstream);
                }
                #region Old code disabled as LuaCsConfig kept crashing when attempting to load the config file
                //if (false == string.IsNullOrWhiteSpace(cfgFilePath) && LuaCsFile.Exists(cfgFilePath))
                //    Config = (McmConfig)LuaCsConfig.Load<object>(cfgFilePath);
                //else Config = new McmConfig();
                #endregion
            }
            catch (System.Exception e)
            {
                LuaCsSetup.PrintCsMessage($"[MCM-SERVER] Loading config ERROR : [{e.Message}]");
                Config = new McmConfig();
            }
        }
        public static void SaveConfig()
        {
            var cfgFilePath = System.IO.Path.Combine(ACsMod.GetStoreFolder<McmMod>(), "McmConfig.xml");
            if (false == string.IsNullOrWhiteSpace(cfgFilePath))
            {
                #region Directly serialize config file using xmlserializer instead of using LuaCsConfig as it caused problems when attempting to deserialize the config file afterwards
                //LuaCsConfig.Save(cfgFilePath, Config);
                #endregion
                try
                {
                    var serializer = new XmlSerializer(typeof(McmConfig));
                    using (var fstream = new System.IO.StreamWriter(cfgFilePath))
                    {
                        serializer.Serialize(fstream, Config);
                    }
                }
                catch (Exception e)
                {
                    LuaCsSetup.PrintCsMessage($"[MCM-SERVER] Saving config ERROR : [{e.Message}]");
                }
            }
        }
    }
}