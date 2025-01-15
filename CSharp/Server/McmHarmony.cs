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
        class Patch_StartGame
        {
            static void Postfix(
                SubmarineInfo selectedSub,
                SubmarineInfo selectedShuttle,
                Option<SubmarineInfo> selectedEnemySub,
                GameModePreset selectedMode,
                CampaignSettings settings,
                ref IEnumerable<CoroutineStatus> __result
            )
            {
                McmMod.Instance.Save.OnStartGame();
            }

        }
        [HarmonyPatch(typeof(Barotrauma.MultiPlayerCampaign), nameof(Barotrauma.MultiPlayerCampaign.End))]
        class Patch_End
        {
            static void Prefix()
            {
                var crewManager = GameMain.GameSession.CrewManager;
                if (McmMod.Config.RespawnMode == RespawnMode.BetweenRounds)
                {
                    foreach (var deadCharacters in Character.CharacterList.FindAll(c => c.IsDead && c.TeamID == CharacterTeamType.Team1))
                    {
                        McmMod.Instance.Control.BetweenRoundsAwaiting.Add(deadCharacters.info);
                    }
                }
            }

        }
    }
}