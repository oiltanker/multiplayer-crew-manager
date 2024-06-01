using System;
using System.Reflection;
using System.Xml.Serialization;
using Barotrauma;
using Barotrauma.Networking;

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
        public bool SecureEnabled = false;
        //public float RespawnDelay = 5;

        private readonly PropertyInfo maxTransportTime;
        private readonly PropertyInfo respawnInterval;
        private readonly PropertyInfo skillLossPercentageOnDeath;

        public McmConfig()
        {
            maxTransportTime = typeof(ServerSettings).GetProperty("MaxTransportTime");
            if (maxTransportTime is null)
                throw new ApplicationException($"{typeof(McmConfig)} constructor. Could not perform GetProperty on ServerSettings.MaxTransportTime");

            respawnInterval = typeof(ServerSettings).GetProperty("RespawnInterval");
            if (respawnInterval is null)
                throw new ApplicationException($"{typeof(McmConfig)} constructor. Could not perform GetProperty on ServerSettings.RespawnInterval");

            skillLossPercentageOnDeath = typeof(ServerSettings).GetProperty("SkillLossPercentageOnDeath");
            if (skillLossPercentageOnDeath is null)
                throw new ApplicationException($"{typeof(McmConfig)} constructor. Could not perform GetProperty on ServerSettings.RespawnInterval");

        }

        public float MaxTransportTime
        {
            get => GameMain.Server.ServerSettings.MaxTransportTime;
            set => maxTransportTime.SetValue(GameMain.Server.ServerSettings, value);
        }

        public float RespawnInterval
        {
            get => GameMain.Server.ServerSettings.RespawnInterval;
            set => respawnInterval.SetValue(GameMain.Server.ServerSettings, value);
        }

        public bool AllowRespawn
        {
            get => GameMain.Server.ServerSettings.AllowRespawn;
            set => GameMain.Server.ServerSettings.AllowRespawn = value;
        }

        public float SkillLossPercentageOnDeath
        {
            get => GameMain.Server.ServerSettings.SkillLossPercentageOnDeath;
            set => skillLossPercentageOnDeath.SetValue(GameMain.Server.ServerSettings, value);
        }

        public override string ToString()
        {
            return
                $"{nameof(LoggingLevel)}: {LoggingLevel},\n " +
                $"{nameof(ServerUpdateFrequency)}: {ServerUpdateFrequency}s,\n " +
                $"{nameof(AllowSpawnNewClients)}: {AllowSpawnNewClients},\n " +
                $"{nameof(SecureEnabled)}: {SecureEnabled},\n " +
                $"{nameof(MaxTransportTime)}: {MaxTransportTime}s,\n " +
                $"{nameof(RespawnInterval)}: {RespawnInterval}s,\n " +
                $"{nameof(AllowRespawn)}: {AllowRespawn},\n " +
                $"{nameof(SkillLossPercentageOnDeath)}: {SkillLossPercentageOnDeath}%";
        }
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
                        McmUtils.Trace($"Loaded config file:\n   [{Config.ToString()}]");
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