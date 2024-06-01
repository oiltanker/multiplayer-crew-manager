using Barotrauma;
using Barotrauma.Networking;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace MultiplayerCrewManager
{
    class McmChat {

        public McmMod Mod { get; private set; }



        public McmChat(McmMod mod)
        {
            Mod = mod;

        }


        private static readonly Regex rMaskGlobal = new Regex(@"^mcm.*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex rMaskHelp1 = new Regex(@"^mcm\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex rMaskHelp2 = new Regex(@"^mcm\s+help\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex rMaskList = new Regex(@"^mcm\s+list\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex rMaskControl = new Regex(@"^mcm\s+control\s+\d+\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex rMaskReleaseGeneral = new Regex(@"^mcm\s+release\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex rMaskReleaseId = new Regex(@"^mcm\s+release\s+\d+\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex rMaskDelete = new Regex(@"^mcm\s+delete\s+\d+\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex rMaskClientList = new Regex(@"^mcm\s+clientlist\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex rMaskSpawn = new Regex(@"^mcm\s+spawn\s+\d+\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex rMaskClientAutospawn = new Regex(@"^mcm\s+client\s+autospawn\s+(true|false)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex rMaskRespawn = new Regex(@"^mcm\s+respawn\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex rMaskRespawnSet = new Regex(@"^mcm\s+respawn\s+set\s+(true|false)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex rMaskRespawnPenalty = new Regex(@"^mcm\s+respawn\s+penalty\s+(true|false)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex rMaskRespawninterval = new Regex(@"^mcm\s+respawn\s+interval\s+\d+\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex rMaskRespawnMaxTransportTime = new Regex(@"^mcm\s+respawn\s+maxtransporttime\s+\d+\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex rMaskSecure = new Regex(@"^mcm\s+secure\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex rMaskSecureEnabled = new Regex(@"^mcm\s+secure\s+(true|false)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex rMaskReserve = new Regex(@"^mcm\s+reserve\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex rMaskReservePut = new Regex(@"^mcm\s+reserve\s+put\s+\d+\s?(force)?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex rMaskReserveGet = new Regex(@"^mcm\s+reserve\s+get\s+\d+\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex rMaskLoggingLevelSet = new Regex(@"^mcm\slogging\s\d$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex rMaskLoggingLevelGet = new Regex(@"^mcm\slogging$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex rMaskIntValue = new Regex(@"\s+\d+\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex rMaskBoolValue = new Regex(@"\s+true\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);


        public bool? OnChatMessage(string message, Client sender) {
            if (!McmMod.IsCampaign || !rMaskGlobal.IsMatch(message)) return null;

            string response = null;
            ChatMessageType messageType = ChatMessageType.ServerMessageBox;


            switch (message)
            {
                // mcm [help]
                case var _ when rMaskHelp1.IsMatch(message):
                case var _ when rMaskHelp2.IsMatch(message):
                    (messageType, response) = ChatHelp(sender);
                    break;
                // mcm list
                case var _ when rMaskList.IsMatch(message):
                    (messageType, response) = ChatList();
                    break;
                // mcm clientlist
                case var _ when rMaskClientList.IsMatch(message):
                    (messageType, response) = ChatClientList(sender);
                    break;
                // mcm control <ID>
                case var _ when rMaskControl.IsMatch(message):
                    (messageType, response) = ChatControl(message, sender);
                    break;
                // mcm release
                case var _ when rMaskReleaseGeneral.IsMatch(message):
                    (response, messageType) = ChatRelease(sender);
                    break;
                case var _ when rMaskReleaseId.IsMatch(message):
                {
                    // mcm release <ID>
                    (response, messageType) = ChatReleaseId(message, sender);
                }
                    break;
                case var _ when rMaskDelete.IsMatch(message):
                {
                    // mcm delete <ID>
                    (response, messageType) = ChatDeleteId(message, sender);
                }
                    break;
                case var _ when rMaskSpawn.IsMatch(message):
                {
                    // mcm spawn <ID>
                    (messageType, response) = ChatSpawnId(message, sender);
                }
                    break;
                case var _ when rMaskClientAutospawn.IsMatch(message):
                {
                    // mcm client autospawn
                    (messageType, response) = ChatAutospawn(message, sender);
                }
                    break;
                case var _ when rMaskRespawnSet.IsMatch(message):
                {
                    // mcm respawn set <true/false> 
                    (messageType, response) = ChatRespawnSet(message, sender);
                }
                    break;
                case var _ when rMaskRespawninterval.IsMatch(message):
                {
                    // mcm respawn interval <number>
                    (messageType, response) = ChatRespawnInterval(message, sender);
                }
                    break;
                case var _ when rMaskRespawnPenalty.IsMatch(message):
                {
                    // mcm respawn penalty <percentage>
                    (messageType, response) = ChatRespawnPenalty(message, sender);
                }
                    break;
                case var _ when rMaskRespawnMaxTransportTime.IsMatch(message):
                {
                    // mcm respawn shuttletime <number>
                    (messageType, response) = ChatRespawnShuttleTime(message, sender);
                }
                    break;
                case var _ when rMaskRespawn.IsMatch(message):
                {
                    // mcm respawn
                    (messageType, response) = ChatRespawn(sender);
                }
                    break;
                case var _ when rMaskSecure.IsMatch(message):
                {
                    // mcm secure
                    (messageType, response) = ChatSecure(sender);
                }
                    break;
                case var _ when rMaskSecureEnabled.IsMatch(message):
                {
                    // mcm secure <true/false>
                    (messageType, response) = ChatSecureBool(message, sender);
                }
                    break;
                case var _ when rMaskReserve.IsMatch(message):
                {
                    // mcm reserve
                    (response, messageType) = ChatReserve(sender);
                }
                    break;
                case var _ when rMaskReservePut.IsMatch(message):
                {
                    // mcm reserve put <ID>
                    (response, messageType) = ChatReservePut(message, sender);
                }
                    break;
                case var _ when rMaskReserveGet.IsMatch(message):
                {
                    // mcm reserve get <ID>
                    (response, messageType) = ChatReserveGet(message, sender);
                }
                    break;
                case var _ when rMaskLoggingLevelGet.IsMatch(message):
                {
                    // mcm logging
                    (messageType, response) = ChatLogging(sender);
                }
                    break;
                case var _ when rMaskLoggingLevelSet.IsMatch(message):
                {
                    // mcm logging 4
                    (messageType, response) = ChatLoggingSet(message, sender);
                }
                    break;
                default:
                    response = "[MCM] error: Incorrect function or argument.";
                    messageType = ChatMessageType.Error;
                    break;
            }

            // send message
            if (response != null) {
                var cm = ChatMessage.Create("[Server]", response, messageType, null, sender);
                cm.IconStyle = "StoreShoppingCrateIcon";
                GameMain.Server.SendDirectChatMessage(cm, sender);
            }

            // no chat display
            return true;
        }

        private (ChatMessageType messageType, string response) ChatRespawnPenalty(string message, Client sender)
        {
            ChatMessageType messageType;
            string response;
            if (sender.HasPermission(ClientPermissions.ConsoleCommands))
            {
                messageType = ChatMessageType.Server;
                Int32.TryParse(rMaskIntValue.Match(message).Value, out int penalty);
                response = $"[MCM] Respawn penalty is set to {penalty}%";
                McmMod.Config.SkillLossPercentageOnDeath = Math.Max((float)penalty, 0);
                McmMod.SaveConfig();
            }
            else (messageType, response) = SetPrivilegeError();

            return (messageType, response);
        }

        private (ChatMessageType messageType, string response) ChatLoggingSet(string message, Client sender)
        {
            ChatMessageType messageType;
            string response;
            // mcm logging 4
            if (sender.HasPermission(ClientPermissions.ConsoleCommands))
            {
                messageType = ChatMessageType.Server;
                if (Int32.TryParse(rMaskIntValue.Match(message).Value, out int value))
                {
                    //Clamp value between 0-4
                    if (value > 4)
                        value = 4;
                    if (value < 0)
                        value = 0;
                    McmMod.Config.LoggingLevel = value;
                    McmMod.SaveConfig();
                    string correspondingLoggingLevel = "";
                    switch (value)
                    {
                        case 0:
                            correspondingLoggingLevel = "None";
                            break;
                        case 1:
                            correspondingLoggingLevel = "Error";
                            break;
                        case 2:
                            correspondingLoggingLevel = "Warn";
                            break;
                        case 3:
                            correspondingLoggingLevel = "Info";
                            break;
                        default:
                            correspondingLoggingLevel = "Trace";
                            break;
                    }
                    response = $"[MCM] - Set logging level to [{correspondingLoggingLevel}]";
                }
                else
                {
                    response = $"ERROR - Could not parse integer from message [{message}]";
                    McmUtils.Warn($"Could not parse integer from message [{message}]");
                }
            }
            else (messageType, response) = SetPrivilegeError();

            return (messageType, response);
        }

        private (ChatMessageType messageType, string response) ChatLogging(Client sender)
        {
            ChatMessageType messageType;
            string response;
            if (sender.HasPermission(ClientPermissions.ConsoleCommands))
            {
                messageType = ChatMessageType.Server;
                var loggingLevel = McmMod.Config.LoggingLevel;
                string correspondingLoggingLevel = "";
                switch (loggingLevel)
                {
                    case 0:
                        correspondingLoggingLevel = "None";
                        break;
                    case 1:
                        correspondingLoggingLevel = "Error";
                        break;
                    case 2:
                        correspondingLoggingLevel = "Warn";
                        break;
                    case 3:
                        correspondingLoggingLevel = "Info";
                        break;
                    default:
                        correspondingLoggingLevel = "Trace";
                        break;
                }
                response = $"[MCM] - Current logging level set to [{correspondingLoggingLevel}]";
            }
            else (messageType, response) = SetPrivilegeError();

            return (messageType, response);
        }

        private (string response, ChatMessageType messageType) ChatReserveGet(string message, Client sender)
        {
            string response;
            ChatMessageType messageType;
            if (sender.HasPermission(ClientPermissions.ConsoleCommands))
            {
                response = null;
                messageType = ChatMessageType.Server;
                Int32.TryParse(rMaskIntValue.Match(message).Value, out int value);
                McmReserve.getCharacterFromReserve(ordinal: value, client: sender);
            }
            else (messageType, response) = SetPrivilegeError();

            return (response, messageType);
        }

        private (string response, ChatMessageType messageType) ChatReservePut(string message, Client sender)
        {
            string response;
            ChatMessageType messageType;
            if (sender.HasPermission(ClientPermissions.ConsoleCommands))
            {
                response = null;
                messageType = ChatMessageType.Server;
                string digits = new string(message.Where(d => char.IsDigit(d)).ToArray());
                Int32.TryParse(digits, out int value);
                bool isForce = message.Contains("force");
                McmReserve.putCharacterToReserve(charId: value, client: sender, isForce: isForce);
            }
            else (messageType, response) = SetPrivilegeError();

            return (response, messageType);
        }

        private (string response, ChatMessageType messageType) ChatReserve(Client sender)
        {
            string response;
            ChatMessageType messageType;
            if (sender.HasPermission(ClientPermissions.ConsoleCommands))
            {
                response = null;
                messageType = ChatMessageType.Server;
                McmReserve.showReserveList(client: sender);
            }
            else (messageType, response) = SetPrivilegeError();

            return (response, messageType);
        }

        private (ChatMessageType messageType, string response) ChatSecureBool(string message, Client sender)
        {
            ChatMessageType messageType;
            string response;
            if (sender.HasPermission(ClientPermissions.ConsoleCommands))
            {
                messageType = ChatMessageType.Server;
                Boolean.TryParse(rMaskBoolValue.Match(message).Value, out bool value);
                if (value) response = "[MCM] Secure mode is turned ON";
                else response = "[MCM] Secure mode is turned OFF";
                McmMod.Config.SecureEnabled = value;
                McmMod.SaveConfig();
            }
            else (messageType, response) = SetPrivilegeError();

            return (messageType, response);
        }

        private (ChatMessageType messageType, string response) ChatSecure(Client sender)
        {
            ChatMessageType messageType;
            string response;
            if (sender.HasPermission(ClientPermissions.ConsoleCommands))
            {
                messageType = ChatMessageType.ServerMessageBox;
                var confStr = new[]{
                    ("Secure Mode", $"{McmMod.Config.SecureEnabled}")
                }.Select(s => $"\n— {s.Item1}:    {s.Item2}").Aggregate((s1, s2) => $"{s1}{s2}");
                response = $"Secure Mode:{confStr}";
            }
            else (messageType, response) = SetPrivilegeError();

            return (messageType, response);
        }

        private (ChatMessageType messageType, string response) ChatRespawn(Client sender)
        {
            ChatMessageType messageType;
            string response;
            if (sender.HasPermission(ClientPermissions.ConsoleCommands))
            {
                messageType = ChatMessageType.ServerMessageBox;
                response = $"Respawn Config:\n{McmMod.Config.ToString()}";
            }
            else (messageType, response) = SetPrivilegeError();

            return (messageType, response);
        }

        private (ChatMessageType messageType, string response) ChatRespawnShuttleTime(string message, Client sender)
        {
            ChatMessageType messageType;
            string response;
            if (sender.HasPermission(ClientPermissions.ConsoleCommands))
            {
                messageType = ChatMessageType.Server;
                Int32.TryParse(rMaskIntValue.Match(message).Value, out int time);
                response = $"[MCM] Respawn shuttle catch-up time is set to {time} seconds";
                McmMod.Config.MaxTransportTime = (float)time;

            }
            else (messageType, response) = SetPrivilegeError();

            return (messageType, response);
        }

        private (ChatMessageType messageType, string response) ChatRespawnInterval(string message, Client sender)
        {
            ChatMessageType messageType;
            string response;
            if (sender.HasPermission(ClientPermissions.ConsoleCommands))
            {
                messageType = ChatMessageType.Server;
                Int32.TryParse(rMaskIntValue.Match(message).Value, out int interval);
                response = $"[MCM] Respawn interval is set to {interval} seconds";
                McmMod.Config.RespawnInterval = (float)interval;
                McmMod.SaveConfig();
            }
            else (messageType, response) = SetPrivilegeError();

            return (messageType, response);
        }

        private (ChatMessageType messageType, string response) ChatRespawnSet(string message, Client sender)
        {
            ChatMessageType messageType;
            string response;
            if (sender.HasPermission(ClientPermissions.ConsoleCommands))
            {
                messageType = ChatMessageType.Server;
                Boolean.TryParse(rMaskBoolValue.Match(message).Value, out bool value);
                if (value) response = "[MCM] Respawning is turned ON";
                else response = "[MCM] Respawning is turned OFF";
                McmMod.Config.AllowRespawn = value;
                McmMod.SaveConfig();
            }
            else (messageType, response) = SetPrivilegeError();

            return (messageType, response);
        }

        private (ChatMessageType messageType, string response) ChatAutospawn(string message, Client sender)
        {
            ChatMessageType messageType;
            string response;
            if (sender.HasPermission(ClientPermissions.ConsoleCommands))
            {
                messageType = ChatMessageType.Server;
                Boolean.TryParse(rMaskBoolValue.Match(message).Value, out bool value);
                if (value) response = "[MCM] Automatic new client character spawn is turned ON";
                else response = "[MCM] Automatic new client character spawn is turned OFF";
                McmMod.Config.AllowSpawnNewClients = value;
                McmMod.SaveConfig();
            }
            else (messageType, response) = SetPrivilegeError();

            return (messageType, response);
        }

        private (ChatMessageType messageType, string response) ChatSpawnId(string message, Client sender)
        {
            ChatMessageType messageType;
            string response;
            if (McmMod.IsRunning)
            {
                if (sender.HasPermission(ClientPermissions.ConsoleCommands))
                {
                    Int32.TryParse(rMaskIntValue.Match(message).Value, out int clientId);

                    messageType = ChatMessageType.Error;
                    var client = Client.ClientList.FirstOrDefault(c => clientId == c.Character.ID);
                    if (client != null)
                    {
                        if (Mod.TryCeateClientCharacter(client))
                        {
                            response = $"[MCM] Character spawned for client ID - {clientId}";
                            messageType = ChatMessageType.Server;
                        }
                        else
                        {
                            response = $"[MCM] Could not spawn character for client ID - {clientId} , spawn waypoint not found";
                        }
                    }
                    else response = $"[MCM] Client with ID {clientId} not found";
                }
                else (messageType, response) = SetPrivilegeError();
            }
            else (messageType, response) = SetInGameError();

            return (messageType, response);
        }

        private (string response, ChatMessageType messageType) ChatDeleteId(string message, Client sender)
        {
            string response;
            ChatMessageType messageType;
            if (McmMod.IsRunning)
            {
                Int32.TryParse(rMaskIntValue.Match(message).Value, out int id);
                if (sender.HasPermission(ClientPermissions.ConsoleCommands))
                {
                    var character = Character.CharacterList.FirstOrDefault(c => c.TeamID == CharacterTeamType.Team1 && c.ID == id);
                    if (character != null)
                    {
                        Entity.Spawner.AddEntityToRemoveQueue(character);
                        GameMain.GameSession.CrewManager.RemoveCharacter(character, true, true);
                        response = $"[MCM] Character ID - {id} ({character.Name}) was removed";
                        messageType = ChatMessageType.Server;
                    }
                    else
                    {
                        response = $"[MCM] Character with ID {id} not found";
                        messageType = ChatMessageType.Error;
                    }
                }
                else (messageType, response) = SetPrivilegeError();
            }
            else (messageType, response) = SetInGameError();

            return (response, messageType);
        }

        private (string response, ChatMessageType messageType) ChatReleaseId(string message, Client sender)
        {
            string response;
            ChatMessageType messageType;
            if (McmMod.IsRunning)
            {
                Int32.TryParse(rMaskIntValue.Match(message).Value, out int id);
                if (sender.HasPermission(ClientPermissions.ConsoleCommands))
                {
                    var client = Client.ClientList.FirstOrDefault(c => c.Character != null && c.Character.ID == id);
                    if (client != null)
                    {
                        response = $"[MCM] Character ID - {id} ({client.Character.Name}) released";
                        messageType = ChatMessageType.Server;
                        Mod.Manager.Set(client, null);
                    }
                    else
                    {
                        response = $"[MCM] Clinet with the character {id} not found";
                        messageType = ChatMessageType.Error;
                    }
                }
                else (messageType, response) = SetPrivilegeError();
            }
            else (messageType, response) = SetInGameError();

            return (response, messageType);
        }

        private (string response, ChatMessageType messageType) ChatRelease(Client sender)
        {
            string response;
            ChatMessageType messageType;
            if (McmMod.IsRunning)
            {
                if (sender.Character == null)
                {
                    response = "[MCM] Not currently controlling any characters";
                    messageType = ChatMessageType.Error;
                }
                else
                {
                    Mod.Manager.Set(sender, null);
                    response = "[MCM] Current character released";
                    messageType = ChatMessageType.Server;
                }
            }
            else (messageType, response) = SetInGameError();

            return (response, messageType);
        }

        private (ChatMessageType messageType, string response) ChatControl(string message, Client sender)
        {
            ChatMessageType messageType;
            if (McmMod.IsRunning)
            {
                messageType = ChatMessageType.ServerMessageBox;
                Int32.TryParse(rMaskIntValue.Match(message).Value, out int id);
                Mod.Control.TryGiveControl(sender, id, McmMod.Config.SecureEnabled);
            }
            else return SetInGameError();

            return (messageType, null);
        }

        private (ChatMessageType messageType, string response) ChatClientList(Client sender)
        {
            if (sender.HasPermission(ClientPermissions.ConsoleCommands))
            {
                var response = "Current clients";
                foreach (var client in Client.ClientList)
                    response += $"\n  {client.CharacterID} | {client.SteamID} - {client.Name}";
                return (ChatMessageType.ServerMessageBox, response);
            }
            return SetPrivilegeError();

        }

        private (ChatMessageType messageType, string response) ChatList()
        {
            if (McmMod.IsRunning)
            {
                var response = "Controllable characters:";
                foreach (var character in Character.CharacterList.Where(c => c.TeamID == CharacterTeamType.Team1 && !c.IsDead))
                {
                    // Team1, meaning default crew
                    response += $"\n{character.ID} / {character.TeamID.ToString("G")} / {character.SpeciesName} / {character.IsBot} | {character.Name} ({character.DisplayName})";
                }
                return (ChatMessageType.ServerMessageBox, response);
            }
            
            return SetInGameError();
        }

        private (ChatMessageType, string) SetPrivilegeError()
        {
            return (ChatMessageType.Error, "[MCM] Not enough privileges to use this command");
        }

        private (ChatMessageType, string) SetInGameError()
        {
            return (ChatMessageType.Error, "[MCM] Command can be used only in game and multiplayer campaign");
        }

        private static (ChatMessageType Server, string response) ChatHelp(Client sender)
        {
            var response = @"
mcm [function] [args] - mcm Function syntax

— mcm [help] - show help for mcm function (help keyword is optional)
— mcm list - list all controllable characters
— mcm control <ID> - try gain control of character with provided ID (only for admins/moderators if SM enabled)
— mcm release [ID] - release currently controlled character or character with ID, if provided (later only for admins/moderators)
";
            if (sender.HasPermission(ClientPermissions.ConsoleCommands)) response += @"

admin/moderator only commands

— mcm delete <ID> - delete character (with inventory) with provided ID
— mcm clientlist - list all the clients
— mcm spawn <ID> - spawn unique client character (his cosmetic presets)
— mcm client autospawn <true/false> - trun automatic spawning for new connected clients on/off

— mcm respawn - list respawn config
— mcm respawn set <true/false> - turn respawning on/off
— mcm respawn penalty <percentage> - set respawning penalty. Disabled if inferior or equal to 0
— mcm respawn interval <number> - time to wait before respawning
— mcm respawn shuttletime <number> - time that respawnees have to catch up with the main sub
— mcm secure - show the current secure mode status
— mcm secure <true/false> - secure mode to allow only admins/moderators to gain control on/off

- mcm reserve - show characters stocked in reserve
- mcm reserve put <ID> - put character in reserve with provided ID
- mcm reserve get <ID> - get character from reserve with provided ID
- mcm logging - Gets the current logging level
- mcm logging <0-4> sets the current logging level (0 - None, 1 - Error, 2 - Warn, 3 - Info, 4 - Trace)
";
            return (ChatMessageType.ServerMessageBox, response);
        }
    }
}