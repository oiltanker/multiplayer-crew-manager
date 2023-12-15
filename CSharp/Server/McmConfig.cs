using System;
using System.Xml.Serialization;
using Barotrauma;

namespace MultiplayerCrewManager
{
    public class McmConfig
    {
        public const int LoggingLevel_Trace = 4;
        public const int LoggingLevel_Info = 3;
        public const int LoggingLevel_Warn = 2;
        public const int LoggingLevel_Error = 1;
        public const int LoggingLevel_None = 0;
        public int LoggingLevel = LoggingLevel_Info;

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
        public static McmConfig GetConfig
        {
            get
            {
                if (Config == null)
                    LoadConfig();

                return Config;
            }
        }

        public static void LoadConfig()
        {
            var cfgFilePath = System.IO.Path.Combine(ACsMod.GetStoreFolder<McmMod>(), "McmConfig.xml");
            try
            {
                if (System.IO.File.Exists(cfgFilePath))
                {
                    var serializer = new XmlSerializer(typeof(McmConfig));
                    using (var fstream = System.IO.File.OpenRead(cfgFilePath))
                    {
                        Config = (McmConfig)serializer.Deserialize(fstream);
                        McmUtils.Trace($"Loaded config file:\n   [LoggingLevel={Config.LoggingLevel}]\n   [ServerUpdateFrequency={Config.ServerUpdateFrequency}]\n   [AllowSpawnNewClients={Config.AllowSpawnNewClients}]\n   [AllowRespawns={Config.AllowRespawns}]\n   [SecureEnabled={Config.SecureEnabled}]\n   [RespawnPenalty={Config.RespawnPenalty}]\n   [RespawnTime={Config.RespawnTime}]\n   [RespawnDelay={Config.RespawnDelay}]");
                    }
                }
                else
                {
                    Config = new McmConfig();
                    McmUtils.Info("Generating new MCM config file");
                    SaveConfig();
                }
            }
            catch (System.Exception e)
            {
                Config = new McmConfig();
                McmUtils.Error(e, "Error while loading config");
                SaveConfig();
            }
        }

        public static void SaveConfig()
        {
            var cfgFilePath = System.IO.Path.Combine(ACsMod.GetStoreFolder<McmMod>(), "McmConfig.xml");
            if (false == string.IsNullOrWhiteSpace(cfgFilePath))
            {
                try
                {
                    var serializer = new XmlSerializer(typeof(McmConfig));
                    using (var fstream = new System.IO.StreamWriter(cfgFilePath))
                    {
                        serializer.Serialize(fstream, Config);
                        McmUtils.Trace("Saved config file");
                    }
                }
                catch (Exception e)
                {
                    McmUtils.Error(e, "Error while saving");
                }
            }
        }
    }
}