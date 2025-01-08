using Barotrauma;
using HarmonyLib;
using System.Reflection;
using System.Linq;
using Barotrauma.Items.Components;
using System.Collections.Generic;
using System;
using Microsoft.Xna.Framework;
using System.ComponentModel;
using Barotrauma.Networking;
using MultiplayerCrewManager;

namespace Mcm_mod
{
    partial class McmHarmony : ACsMod
    {
        const string harmony_id = "com.Mcm.Harmony";
		private readonly Harmony harmony;

        public override void Stop()
		{
			harmony.UnpatchAll(harmony_id);
		}

		public McmHarmony()
		{
			harmony = new Harmony(harmony_id);
			harmony.PatchAll(Assembly.GetExecutingAssembly());
			Barotrauma.DebugConsole.AddWarning("Loaded McmHarmony");
		}

        [HarmonyPatch(typeof(Barotrauma.Networking.GameServer), nameof(Barotrauma.Networking.GameServer.StartGame))]
        class Patch_StartGame{
        static void Postfix(
            SubmarineInfo selectedSub, 
            SubmarineInfo selectedShuttle, 
            Option<SubmarineInfo> selectedEnemySub, 
            GameModePreset selectedMode, 
            CampaignSettings settings,
            ref IEnumerable<CoroutineStatus> __result
        )
            {
                McmClientManager Manager = new McmClientManager();
                McmControl Control = new McmControl(GameMain.Server?.RespawnManager, Manager);
                McmSave instance = new McmSave(Control);
                instance.OnStartGame();
            }

        }
    }
}