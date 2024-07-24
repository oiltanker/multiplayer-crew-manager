using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;
using Barotrauma;
using Barotrauma.Networking;

namespace MultiplayerCrewManager
{
    public enum McmLoggingLevel
    {
        None = 0,
        Error = 1,
        Warning = 2,
        Info = 3,
        Trace = 4
    }
    public class McmConfig
    {
        public McmLoggingLevel LoggingLevel = McmLoggingLevel.Info;

        public int ServerUpdateFrequency = 15;
        public bool AutoSpawn = false;
        public bool SecureEnabled = false;
        //public float RespawnDelay = 5;

        private readonly PropertyInfo maxTransportTime;
        private readonly PropertyInfo respawnInterval;
        private readonly PropertyInfo skillLossPercentageOnDeath;
        private readonly PropertyInfo useRespawnShuttle;


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

            useRespawnShuttle = typeof(ServerSettings).GetProperty("UseRespawnShuttle");
            if (useRespawnShuttle is null)
                throw new ApplicationException($"{typeof(McmConfig)} constructor. Could not perform GetProperty on ServerSettings.UseRespawnShuttle");

        }

        public float ShuttleTransportTime
        {
            get => GameMain.Server.ServerSettings.MaxTransportTime;
            set => maxTransportTime.SetValue(GameMain.Server.ServerSettings, value);
        }

        public float RespawnInterval
        {
            get => GameMain.Server.ServerSettings.RespawnInterval;
            set => respawnInterval.SetValue(GameMain.Server.ServerSettings, value);
        }

        public RespawnMode RespawnMode
        {
            get => GameMain.Server.ServerSettings.RespawnMode;
            set => GameMain.Server.ServerSettings.RespawnMode = value;
        }

        public float RespawnPenalty
        {
            get => GameMain.Server.ServerSettings.SkillLossPercentageOnDeath;
            set => skillLossPercentageOnDeath.SetValue(GameMain.Server.ServerSettings, value);
        }

        public bool UseShuttle
        {
            get => GameMain.Server.ServerSettings.UseRespawnShuttle;
            set => useRespawnShuttle.SetValue(GameMain.Server.ServerSettings, value);
        }

        public string AvailableShuttles()
        {
            return SubmarineInfo.SavedSubmarines.Where(s => s.IsPlayer && s.HasTag(SubmarineTag.Shuttle)).Aggregate(
                    new StringBuilder(),
                    (current, next) => current.Append(current.Length == 0 ? "" : " | ").Append(next.DisplayName.Value))
                .ToString();
        }

        /// <summary>
        /// The Display name of the shuttle.
        /// Little trick = we manipulate the shuttle using the display name, but in the settings we convert it to the actual system shuttle name
        /// </summary>
        public string RespawnShuttle
        {
            get
            {
                if (GameMain.Server.ServerSettings.SelectedShuttle.IsNullOrEmpty())
                    return "None Selected";

                SubmarineInfo shuttle = SubmarineInfo.SavedSubmarines.FirstOrDefault(s => 
                    IsPlayerShuttle(s) 
                    && s.Name == GameMain.Server.ServerSettings.SelectedShuttle);

                if (shuttle == null)
                    return
                        $"Error, couldn't find the designated shuttle '{GameMain.Server.ServerSettings.SelectedShuttle}' in the list of available submarines. " +
                        $"Have you deleted the mod or local submarine file?\n" +
                        $"Check the MCM commands list to display the list of available shuttles.";

                return shuttle.DisplayName.Value;
            }
            set
            {
                // We use the friendly DisplayName when choosing a shuttle
                SubmarineInfo shuttle = SubmarineInfo.SavedSubmarines.FirstOrDefault(s => 
                    IsPlayerShuttle(s) 
                    && string.Equals(s.DisplayName.Value, value, StringComparison.InvariantCultureIgnoreCase));

                GameMain.Server.ServerSettings.SelectedShuttle = shuttle != null 
                    ? shuttle.Name 
                    : "";
            }
        }
        private bool IsPlayerShuttle(SubmarineInfo submarineInfo)
        {
            return submarineInfo.IsPlayer && submarineInfo.HasTag(SubmarineTag.Shuttle);
        }

        public override string ToString()
        {
            return
                $"New Clients {nameof(AutoSpawn)}: {AutoSpawn}\n" +
                $"{nameof(RespawnMode)}: {RespawnMode}\n" +
                $"{nameof(RespawnInterval)}: {RespawnInterval}s\n" +
                $"{nameof(RespawnPenalty)}: {RespawnPenalty}\n" +
                "---------------------------\n" +
                $"{nameof(UseShuttle)}: {UseShuttle}\n" +
                $"{nameof(RespawnShuttle)}: {RespawnShuttle}\n" +
                $"{nameof(ShuttleTransportTime)}: {ShuttleTransportTime}s\n" +
                "---------------------------\n" +
                $"{nameof(SecureEnabled)}: {SecureEnabled},\n" +
                $"{nameof(LoggingLevel)}: {(int)LoggingLevel} - {LoggingLevel}\n" +
                $"{nameof(ServerUpdateFrequency)}: {ServerUpdateFrequency}s";

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