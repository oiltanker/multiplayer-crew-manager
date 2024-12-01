using Barotrauma;

namespace MultiplayerCrewManager
{
    public static class McmUtils
    {
        public static bool IsPvP { get; } = GameMain.GameSession.GameMode is PvPMode;

        public static void Trace(object msg)
        {
            try
            {
                if (McmMod.GetConfig.LoggingLevel >= McmLoggingLevel.Trace) //LoggingLevel=4
                    ModUtils.Logging.PrintMessage($"[TRACE] [MCM] - {msg}");
            }
            catch (System.Exception e)
            {
                ModUtils.Logging.PrintError(e.Message);
            }
        }

        public static void Info(object msg)
        {
            try
            {
                if (McmMod.GetConfig.LoggingLevel >= McmLoggingLevel.Info) //LoggingLevel=3
                    ModUtils.Logging.PrintMessage($"[INFO] [MCM] - {msg}");
            }
            catch (System.Exception e)
            {
                ModUtils.Logging.PrintError(e.Message);
            }
        }

        public static void Warn(object msg)
        {
            try
            {
                if (McmMod.GetConfig.LoggingLevel >= McmLoggingLevel.Warning) //LoggingLevel=2
                    ModUtils.Logging.PrintWarning($"[WARN] [MCM] - {msg}");
            }
            catch (System.Exception e)
            {
                ModUtils.Logging.PrintError(e.Message);
            }
        }

        public static void Error(object msg)
        {
            try
            {
                if (McmMod.GetConfig.LoggingLevel >= McmLoggingLevel.Error) //LoggingLevel=1
                {
                    ModUtils.Logging.PrintError($"[ERROR] [MCM] - {msg}");
                }
            }
            catch (System.Exception e)
            {
                ModUtils.Logging.PrintError(e.Message);
            }
        }

        public static void Error(System.Exception e, object msg)
        {
            try
            {
                if (McmMod.GetConfig.LoggingLevel >= McmLoggingLevel.Error) //LoggingLevel=1
                {
                    ModUtils.Logging.PrintError($"[ERROR] [MCM] - {msg}, Exception {e.Message}");
                }
            }
            catch (System.Exception ex)
            {
                ModUtils.Logging.PrintError(ex.Message);
            }
        }
        
    }
}