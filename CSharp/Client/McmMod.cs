using System.Reflection;
using System.Collections.Generic;
using System;
using System.Linq;
using Barotrauma;
using Barotrauma.Networking;

namespace MultiplayerCrewManager
{
    partial class McmMod
    {
        protected Action<GUIListBox, object> UpdateAction = null;
        public void InitClient()
        {
            LuaCsSetup.PrintCsMessage("[MCM-CLIENT] Initializing...");
            GameMain.LuaCs.Hook.HookMethod("mcm_CrewManager_OnCrewListRearranged",
                typeof(CrewManager).GetMethod("OnCrewListRearranged", BindingFlags.Instance | BindingFlags.NonPublic),
                (object self, Dictionary<string, object> args) =>
                {
                    if (UpdateAction != null) UpdateAction(args["crewList"] as GUIListBox, args["draggedElementData"]);
                    return null;
                }, LuaCsHook.HookMethodType.After, this);

            GameMain.LuaCs.Hook.Add("roundEnd", "mcm_ClientStop", (args) =>
            {
                UpdateAction = null;
                return null;
            }, this);

            Func<object> clientInit = () =>
            {
                if (McmMod.IsCampaign) UpdateAction = OnCrewListUpdate;
                return null;
            };
            if (McmMod.IsRunning) GameMain.LuaCs.Timer.Wait((args) => clientInit(), 500);
            GameMain.LuaCs.Hook.Add("roundStart", "mcm_ClientStop", (args) => clientInit(), this);
            LuaCsSetup.PrintCsMessage("[MCM-CLIENT] Initialization complete");
        }
        public void OnCrewListUpdate(GUIListBox crewList, object draggedElementData) {
            if  (!crewList.HasDraggedElementIndexChanged) {
                var character = draggedElementData as Character;
                var msg = GameMain.LuaCs.Networking.Start("server-mcm");
                msg.WriteString(character.ID.ToString());
                GameMain.LuaCs.Networking.Send(msg);
            }
        }
    }
}