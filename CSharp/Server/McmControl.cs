using System.Reflection;
using System.Collections;
using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Barotrauma;
using Barotrauma.Networking;
using Barotrauma.Items.Components;

namespace MultiplayerCrewManager {
    class McmClientManager {
        private Dictionary<Client, Character> mapping;
        public McmClientManager() {
            mapping = new Dictionary<Client, Character>();
        }
        public bool IsCurrentlyControlled(Character character) {
            foreach ((var client, var _char) in mapping) {
                if (character == _char) return true;
            }
            return false;
        }

        public void Clear() => mapping.Clear();
        public void Set(Client client, Character character) {
            if (mapping.ContainsKey(client)) mapping[client] = character;
            else mapping.Add(client, character);
            if (client.Character != character) client.SetClientCharacter(character);
        }
        public Character? Get(Client client) {
            foreach ((var _client, var _char) in mapping) {
                if (client == _client) return _char;
            }
            return null;
        }
    }

    class McmControl {
        private int counter = 0;
        private double respawnTimeBegin = 0; // from LuaCsTimer.Time
        private double respawnTimer = 0; // difference
        private double respawnTimerForce = 0;
        private MethodInfo resetShuttleMethod = typeof(RespawnManager).GetMethod("ResetShuttle", BindingFlags.Instance | BindingFlags.NonPublic);

        public McmClientManager ClientManager { get; private set; }
        public RespawnManager RespawnManager { get; private set; }
        public HashSet<CharacterInfo> Awaiting { get; private set; }

        public McmControl(RespawnManager respawnManager, McmClientManager clientManager) {
            RespawnManager = respawnManager;
            ClientManager = clientManager;
            Awaiting = new HashSet<CharacterInfo>();
        }

        public void ResetShuttle() {
            if (RespawnManager != null) resetShuttleMethod?.Invoke(RespawnManager, null);
        }

        public WayPoint[] GetRespawnPoints(IEnumerable<CharacterInfo> crew) {
            var respawnSub = RespawnManager.RespawnShuttle;
            if (respawnSub == null || Level.IsLoadedOutpost) respawnSub = Submarine.MainSub;
            return WayPoint.SelectCrewSpawnPoints(crew.ToList(), respawnSub);
        }

        public void ReduceCharacterSkills(CharacterInfo charInfo) {
            if (charInfo.Job == null) return;

            foreach (var skill in charInfo.Job.GetSkills()) {
                var prefab = charInfo.Job.Prefab.Skills.FirstOrDefault(s => skill.Identifier == s.Identifier);
                if (prefab != null) skill.Level = Math.Max(prefab.LevelRange.Start, skill.Level * 0.9f);
            }
        }

        public void RespawnCharacter(CharacterInfo charInfo, WayPoint spawnPoint) {
            charInfo.ClearCurrentOrders();
            charInfo.RemoveSavedStatValuesOnDeath();
            charInfo.CauseOfDeath = null;
            if (McmMod.Config.RespawnPenalty) ReduceCharacterSkills(charInfo);

            var character = Character.Create(charInfo, spawnPoint.WorldPosition, charInfo.Name, 0, true, true);
            character.TeamID = CharacterTeamType.Team1;
            if (character.Info != charInfo) character.Info = charInfo; // if not the same for some reason ?
            character.Info.Character = character;

            character.LoadTalents();
            charInfo.SetExperience(charInfo.ExperiencePoints);

            if (McmMod.Config.RespawnPenalty) RespawnManager.GiveRespawnPenaltyAffliction(character);
            character.GiveJobItems(spawnPoint);
            character.GiveIdCardTags(spawnPoint);
        }

        public IEnumerable<Character> GetRespawnSubChars() {
            return Character.CharacterList.Where(c => c.TeamID == CharacterTeamType.Team1 && !c.IsDead && c.Submarine == RespawnManager.RespawnShuttle);
        }

        public bool TryRespawn(bool forceRespawn = false) {
            if (RespawnManager.RespawnShuttle != null) {
                // get all chars on respawn sub
                var charsOnSub = GetRespawnSubChars();
                // is respawn now possible
                if (charsOnSub.Count() > 0) {
                    if (forceRespawn) {
                        // remove all chars on respawn sub
                        foreach(var character in charsOnSub) {
                            Entity.Spawner.AddEntityToRemoveQueue(character);
                            if (!Awaiting.Contains(character.Info)) Awaiting.Add(character.Info);
                        }
                        Entity.Spawner.Update();
                    }
                    else return false;
                }
            }

            // dispatch respawn shuttle if needed
            if (RespawnManager.RespawnShuttle != null && !Level.IsLoadedOutpost) {
                var shuttle = RespawnManager.RespawnShuttle;
                var shuttleSteering = Item.ItemList.FirstOrDefault(i => i.Submarine == shuttle)?.GetComponent<Steering>();

                ResetShuttle();
                if (shuttleSteering != null) shuttleSteering.TargetVelocity = Vector2.Zero;

                shuttle.SetPosition(RespawnManager.FindSpawnPos());
                shuttle.Velocity = Vector2.Zero;
                shuttle.NeutralizeBallast();
                shuttle.EnableMaintainPosition();
            }
            // get spawn points
            var respawnPoints = GetRespawnPoints(Awaiting);
            // respawn chars
            Awaiting.Zip(respawnPoints, (ci, rp) => (ci, rp)).ToList().ForEach(t => RespawnCharacter(t.Item1, t.Item2));
            Awaiting.Clear();
            // all good
            return true;
        }

        public void  UpdateAwaiting() {
            var crewManager = GameMain.GameSession.CrewManager;
            // get awaiting if any
            foreach (var charInfo in crewManager.GetCharacterInfos()
                .Where(c => c.TeamID == CharacterTeamType.Team1 && !c.IsNewHire))
            {
                if (!Awaiting.Contains(charInfo) && !Character.CharacterList.Any(c => charInfo == c.Info && !c.IsDead))
                    Awaiting.Add(charInfo);
            }
        }

        public void UpdateRespawns() {
            // do respawn
            if (respawnTimeBegin != 0 && LuaCsTimer.Time - respawnTimer >= respawnTimeBegin) {
                if (TryRespawn(LuaCsTimer.Time - respawnTimerForce > respawnTimeBegin)) {
                    // reset respawn state
                    respawnTimeBegin = 0;
                    respawnTimer = 0;
                    respawnTimerForce = 0;
                    Awaiting.Clear();
                }
                else {
                    // post-pone respawn
                    respawnTimer += McmMod.Config.RespawnDelay;
                }
            }
            // update dead characters awaiting respawn
            UpdateAwaiting();
            // start timer if needed
            if (Awaiting.Count > 0 && respawnTimeBegin == 0) {
                respawnTimeBegin = LuaCsTimer.Time;
                respawnTimer = McmMod.Config.RespawnDelay;
                respawnTimerForce = McmMod.Config.RespawnTime;
            }
        }

        public bool? Update(RespawnManager self) {
            if (!McmMod.IsCampaign) return null;
            if (RespawnManager != self) RespawnManager = self;

            if (McmMod.Config.AllowRespawns) {
                counter--;
                if (counter <= 0) {
                    counter = McmMod.Config.ServerUpdateFrequency;
                    UpdateRespawns();
                }
            }
            // override
            return true;
        }

        public void OnInitRound(CrewManager self) {
            if (!McmMod.IsCampaign) return;

            foreach(var charInfo in self.GetCharacterInfos()) {
                charInfo.SetExperience(charInfo.ExperiencePoints);
                // LoadTalents he is prohibited, removes all the talents
            }
        }

        public void TryGiveControl(Client client, int charId, bool secure) {
            var character = Character.CharacterList.FirstOrDefault(c => charId == c.ID && c.TeamID == CharacterTeamType.Team1); // Team1, meaning default crew
            ChatMessage cm = null;

            if (character == null) {
                // character with provided ID not found => send client FAIL
                cm = ChatMessage.Create("[Server]",
                    $"[MCM] Failed to gain control: Character ID [{charId}] not found",
                    ChatMessageType.Error, null, client);
			    cm.IconStyle = "StoreShoppingCrateIcon";

			    GameMain.Server.SendDirectChatMessage(cm, client);
                return;
            }

            if (client.Character == character) return;
            if (ClientManager.IsCurrentlyControlled(character)) {
                // if is busy => send client FAIL
                cm = ChatMessage.Create("[Server]",
                    $"[MCM] Failed to gain control: '{character.DisplayName}' is already in use",
                    ChatMessageType.Error, null, client);
			    cm.IconStyle = "StoreShoppingCrateIcon";

			    GameMain.Server.SendDirectChatMessage(cm, client);
                return;
            }

            if (secure == true && !client.HasPermission(ClientPermissions.ConsoleCommands)) {
                // secure mode is enabled and client nor an admin nor moder => send client FAIL
                cm = ChatMessage.Create("[Server]",
                    $"[MCM] Failed to gain control: secure mode is enabled. Only admins or moderators can gain control (provided that permission presets still on default)",
                    ChatMessageType.Error, null, client);
                cm.IconStyle = "StoreShoppingCrateIcon";

                GameMain.Server.SendDirectChatMessage(cm, client);
                return;
            }

            // is free or original original
            ClientManager.Set(client, character);

            // send client OK
            cm = ChatMessage.Create("[Server]",
                $"[MCM] Gained control of '{character.DisplayName}'",
                ChatMessageType.Server, null, client);
            cm.IconStyle = "StoreShoppingCrateIcon";
            GameMain.Server.SendDirectChatMessage(cm, client);
        }

        public void AssignAiCharacters() {
            // assign old
            var clients = Client.ClientList.Where(c => c.Connection.Endpoint.ToString() != "PIPE" && c.Character == null);
            var unassigned = new Queue<Character>();
            foreach (var character in Character.CharacterList.Where(c => c.TeamID == CharacterTeamType.Team1)) {
                var client = clients.FirstOrDefault(c => c.Name == character.Name);
                if (client != null) ClientManager.Set(client, character);
                else unassigned.Enqueue(character);
            }
            // assign unassigned
            clients = Client.ClientList.Where(c => c.Connection.Endpoint.ToString() != "PIPE" && c.Character == null);
            foreach (var client in clients) {
                if (unassigned.Count <= 0) break;
                else ClientManager.Set(client, unassigned.Dequeue());
            }
        }
    }
}