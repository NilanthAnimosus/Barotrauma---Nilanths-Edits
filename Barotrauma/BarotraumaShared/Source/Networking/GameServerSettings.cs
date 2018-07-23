using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Barotrauma.Networking
{
    enum SelectionMode
    {
        Manual = 0, Random = 1, Vote = 2
    }

    enum YesNoMaybe
    {
        No = 0, Maybe = 1, Yes = 2
    }

    partial class GameServer : NetworkMember, ISerializableEntity
    {
        private class SavedClientPermission
        {
            public readonly string IP;
            public readonly string Name;

            //Slots
            public Boolean TrustedSlot = false;
            public Boolean AdministratorSlot = false;
            public Boolean OwnerSlot = false;

            //Extras
            public Boolean AllowInGamePM = false;
            public Boolean GlobalChatSend = false;
            public Boolean GlobalChatReceive = false;
            public Boolean KarmaImmunity = false;
            public Boolean PrioritizeJob = false;
            public Boolean IgnoreJobMinimum = false;
            public Boolean KickImmunity = false;
            public Boolean BanImmunity = false;

            //Admin Features
            public Boolean HideJoin = false;
            public Boolean AccessDeathChatAlive = false;
            public Boolean AdminPrivateMessage = false;
            public Boolean AdminChannelSend = false;
            public Boolean AdminChannelReceive = false;
            public Boolean SendServerConsoleInfo = false;
            public Boolean CanBroadcast = false;

            public List<DebugConsole.Command> PermittedCommands;

            public ClientPermissions Permissions;

            public SavedClientPermission(string name, string ip, ClientPermissions permissions, List<DebugConsole.Command> permittedCommands)
            {
                this.Name = name;
                this.IP = ip;

                this.Permissions = permissions;
                this.PermittedCommands = permittedCommands;
            }
        }

        public const string SettingsFile = "serversettings.xml";
        public static readonly string PermissionPresetFile = "Data" + Path.DirectorySeparatorChar + "permissionpresets.xml";
        public static readonly string VanillaClientPermissionsFile = "Data" + Path.DirectorySeparatorChar + "clientpermissions.xml";
        public static readonly string NilmodClientPermissionsFile = "Data" + Path.DirectorySeparatorChar + "NilMod" + Path.DirectorySeparatorChar + "clientpermissions.xml";

        public Dictionary<string, SerializableProperty> SerializableProperties
        {
            get;
            private set;
        }

        public Dictionary<ItemPrefab, int> extraCargo;

        public bool ShowNetStats;
        //Nil Mod Diagnostics
        public bool ShowLagDiagnostics;

        private TimeSpan refreshMasterInterval = new TimeSpan(0, 0, 30);
        private TimeSpan sparseUpdateInterval = new TimeSpan(0, 0, 0, 3);

        private SelectionMode subSelectionMode, modeSelectionMode;

        private bool registeredToMaster;

        private WhiteList whitelist;
        private BanList banList;

        public string password;

        public float AutoRestartTimer;

        private bool autoRestart;

        public bool isPublic;

        public int maxPlayers;

        private List<SavedClientPermission> clientPermissions = new List<SavedClientPermission>();

        private SavedClientPermission defaultpermission = new SavedClientPermission("","",ClientPermissions.None, new List<DebugConsole.Command>());

        [Serialize(true, true)]
        public bool RandomizeSeed
        {
            get;
            set;
        }

        [Serialize(300.0f, true)]
        public float RespawnInterval
        {
            get;
            private set;
        }

        [Serialize(180.0f, true)]
        public float MaxTransportTime
        {
            get;
            private set;
        }

        [Serialize(0.2f, true)]
        public float MinRespawnRatio
        {
            get;
            private set;
        }


        [Serialize(60.0f, true)]
        public float AutoRestartInterval
        {
            get;
            set;
        }

        [Serialize(true, true)]
        public bool AllowSpectating
        {
            get;
            private set;
        }

        [Serialize(true, true)]
        public bool EndRoundAtLevelEnd
        {
            get;
            private set;
        }

        [Serialize(true, true)]
        public bool SaveServerLogs
        {
            get;
            private set;
        }

        [Serialize(true, true)]
        public bool AllowRagdollButton
        {
            get;
            private set;
        }

        [Serialize(true, true)]
        public bool AllowFileTransfers
        {
            get;
            private set;
        }

        [Serialize(800, true)]
        private int LinesPerLogFile
        {
            get
            {
                return log.LinesPerFile;
            }
            set
            {
                log.LinesPerFile = value;
            }
        }

        public bool AutoRestart
        {
            get { return autoRestart; }
            set
            {
                autoRestart = value;

                AutoRestartTimer = autoRestart ? AutoRestartInterval : 0.0f;
            }
        }

        [Serialize(true, true)]
        public bool AllowRespawn
        {
            get;
            set;
        }

        public YesNoMaybe TraitorsEnabled
        {
            get;
            set;
        }

        public SelectionMode SubSelectionMode
        {
            get { return subSelectionMode; }
        }

        public SelectionMode ModeSelectionMode
        {
            get { return modeSelectionMode; }
        }

        public BanList BanList
        {
            get { return banList; }
        }

        [Serialize(true, true)]
        public bool AllowVoteKick
        {
            get;
            private set;
        }

        [Serialize(0.6f, true)]
        public float EndVoteRequiredRatio
        {
            get;
            private set;
        }

        [Serialize(0.6f, true)]
        public float KickVoteRequiredRatio
        {
            get;
            private set;
        }

        [Serialize(true, true)]
        public bool TraitorUseRatio
        {
            get;
            private set;
        }

        [Serialize(0.2f, true)]
        public float TraitorRatio
        {
            get;
            private set;
        }

        [Serialize(false, true)]
        public bool KarmaEnabled
        {
            get;
            set;
        }

        [Serialize("Sandbox", true)]
        public string GameMode
        {
            get;
            set;
        }

        [Serialize("Random", true)]
        public string MissionType
        {
            get;
            set;
        }

        public List<string> AllowedRandomMissionTypes
        {
            get;
            set;
        }

        [Serialize(60f, true)]
        public float AutoBanTime
        {
            get;
            private set;
        }

        [Serialize(360f, true)]
        public float MaxAutoBanTime
        {
            get;
            private set;
        }

        private void SaveSettings()
        {
            XDocument doc = new XDocument(new XElement("serversettings"));

            SerializableProperty.SerializeProperties(this, doc.Root, true);

            doc.Root.SetAttributeValue("name", name);
            doc.Root.SetAttributeValue("public", isPublic);
            doc.Root.SetAttributeValue("port", config.Port);
            doc.Root.SetAttributeValue("maxplayers", maxPlayers);
            doc.Root.SetAttributeValue("enableupnp", config.EnableUPnP);

            doc.Root.SetAttributeValue("autorestart", autoRestart);

            doc.Root.SetAttributeValue("SubSelection", subSelectionMode.ToString());
            doc.Root.SetAttributeValue("ModeSelection", modeSelectionMode.ToString());

            doc.Root.SetAttributeValue("TraitorsEnabled", TraitorsEnabled.ToString());

            doc.Root.SetAttributeValue("AllowedRandomMissionTypes", string.Join(",", AllowedRandomMissionTypes));

#if SERVER
            doc.Root.SetAttributeValue("password", password);
#endif

            if (GameMain.NetLobbyScreen != null
#if CLIENT
                && GameMain.NetLobbyScreen.ServerMessage != null
#endif
                )
            {
                doc.Root.SetAttributeValue("ServerMessage", GameMain.NetLobbyScreen.ServerMessageText);
            }

            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.NewLineOnAttributes = true;

            using (var writer = XmlWriter.Create(SettingsFile, settings))
            {
                doc.Save(writer);
            }
        }

        private void LoadSettings()
        {
            XDocument doc = null;
            if (File.Exists(SettingsFile))
            {
                doc = XMLExtensions.TryLoadXml(SettingsFile);
            }

            if (doc == null || doc.Root == null)
            {
                doc = new XDocument(new XElement("serversettings"));
            }

            SerializableProperties = SerializableProperty.DeserializeProperties(this, doc.Root);

#if SERVER
            AutoRestart = doc.Root.GetAttributeBool("autorestart", true);
#endif

#if CLIENT
            AutoRestart = doc.Root.GetAttributeBool("autorestart", false);

            if (autoRestart)
            {
                GameMain.NetLobbyScreen.SetAutoRestart(autoRestart, AutoRestartInterval);
            }
#endif

            subSelectionMode = SelectionMode.Manual;
            Enum.TryParse(doc.Root.GetAttributeString("SubSelection", "Manual"), out subSelectionMode);
            Voting.AllowSubVoting = subSelectionMode == SelectionMode.Vote;

            modeSelectionMode = SelectionMode.Manual;
            Enum.TryParse(doc.Root.GetAttributeString("ModeSelection", "Manual"), out modeSelectionMode);
            Voting.AllowModeVoting = modeSelectionMode == SelectionMode.Vote;

            var traitorsEnabled = TraitorsEnabled;
            Enum.TryParse(doc.Root.GetAttributeString("TraitorsEnabled", "No"), out traitorsEnabled);
            TraitorsEnabled = traitorsEnabled;
            GameMain.NetLobbyScreen.SetTraitorsEnabled(traitorsEnabled);

            AllowedRandomMissionTypes = doc.Root.GetAttributeStringArray(
                "AllowedRandomMissionTypes",
                MissionPrefab.MissionTypes.ToArray()).ToList();

            if (GameMain.NetLobbyScreen != null
#if CLIENT
                && GameMain.NetLobbyScreen.ServerMessage != null
#endif
                )
            {
#if SERVER
                GameMain.NetLobbyScreen.ServerName = doc.Root.GetAttributeString("name", "");
                GameMain.NetLobbyScreen.SelectedModeName = GameMode;
                GameMain.NetLobbyScreen.MissionTypeName = MissionType;
#endif
                GameMain.NetLobbyScreen.ServerMessageText = doc.Root.GetAttributeString("ServerMessage", "");
            }

#if CLIENT
            showLogButton.Visible = SaveServerLogs;
#endif

            List<string> monsterNames = GameMain.Config.SelectedContentPackage.GetFilesOfType(ContentType.Character);
            for (int i = 0; i < monsterNames.Count; i++)
            {
                monsterNames[i] = Path.GetFileName(Path.GetDirectoryName(monsterNames[i]));
            }
            monsterEnabled = new Dictionary<string, bool>();
            foreach (string s in monsterNames)
            {
                if (!monsterEnabled.ContainsKey(s)) monsterEnabled.Add(s, true);
            }
            extraCargo = new Dictionary<ItemPrefab, int>();

            AutoBanTime = doc.Root.GetAttributeFloat("autobantime", 60);
            MaxAutoBanTime = doc.Root.GetAttributeFloat("maxautobantime", 360);
        }

        public void LoadClientPermissions()
        {
            clientPermissions.Clear();

            int Errorcount = 0;

            if (!File.Exists(NilmodClientPermissionsFile))
            {
                if (File.Exists(VanillaClientPermissionsFile))
                {
                    LoadVanillaClientPermissions();
                }
                else if (File.Exists("Data/clientpermissions.txt"))
                {
                    LoadClientPermissionsOld("Data/clientpermissions.txt");
                }
                SaveClientPermissions();
                return;
            }

            XDocument doc = XMLExtensions.TryLoadXml(NilmodClientPermissionsFile);

            //load defaultpermission
            XElement Xdefaultperms = doc.Root.Element("None");
            defaultpermission = new SavedClientPermission("", "", ClientPermissions.None, new List<DebugConsole.Command>());
            if (Xdefaultperms != null)
            {
                string permissionsStr = Xdefaultperms.GetAttributeString("permissions", "");
                ClientPermissions permissions;
                if (!Enum.TryParse(permissionsStr, out permissions))
                {
                    DebugConsole.ThrowError("Error in " + NilmodClientPermissionsFile + " - \"" + permissionsStr + "\" is not a valid client permission.");
                    DebugConsole.ThrowError("Valid permissions are: Kick, Ban, EndRound, SelectSub, SelectMode, ManageCampaign, ConsoleCommands.");
                    Errorcount += 1;
                }

                List<DebugConsole.Command> permittedCommands = new List<DebugConsole.Command>();
                if (permissions.HasFlag(ClientPermissions.ConsoleCommands))
                {
                    foreach (XElement commandElement in Xdefaultperms.Elements())
                    {
                        if (commandElement.Name.ToString().ToLowerInvariant() != "command") continue;

                        string commandName = commandElement.GetAttributeString("name", "");
                        DebugConsole.Command command = DebugConsole.FindCommand(commandName);
                        if (command == null)
                        {
                            DebugConsole.ThrowError("Error in " + NilmodClientPermissionsFile + " - \"" + commandName + "\" is not a valid console command.");
                            continue;
                        }

                        permittedCommands.Add(command);
                    }
                }

                defaultpermission = new SavedClientPermission("", "", permissions, permittedCommands);

                defaultpermission.AllowInGamePM = Xdefaultperms.GetAttributeBool("AllowInGamePM", false);
                defaultpermission.GlobalChatSend = Xdefaultperms.GetAttributeBool("GlobalChatSend", false);
                defaultpermission.GlobalChatReceive = Xdefaultperms.GetAttributeBool("GlobalChatReceive", false);
                defaultpermission.IgnoreJobMinimum = Xdefaultperms.GetAttributeBool("IgnoreJobMinimum", false);

                defaultpermission.HideJoin = Xdefaultperms.GetAttributeBool("HideJoin", false);
                defaultpermission.AccessDeathChatAlive = Xdefaultperms.GetAttributeBool("AccessDeathChatAlive", false);
                defaultpermission.AdminChannelSend = Xdefaultperms.GetAttributeBool("AdminChannelSend", true);
            }
            else
            {
                defaultpermission.AdminChannelSend = true;
            }

            foreach (XElement clientElement in doc.Root.Elements())
            {
                if (clientElement.Name != "Client") continue;
                string clientName = clientElement.GetAttributeString("name", "");
                string clientIP = clientElement.GetAttributeString("ip", "");
                if (string.IsNullOrWhiteSpace(clientName) || string.IsNullOrWhiteSpace(clientIP))
                {
                    DebugConsole.ThrowError("Error in " + NilmodClientPermissionsFile + " - all clients must have a name and an IP address.");
                    Errorcount += 1;
                    continue;
                }

                string permissionsStr = clientElement.GetAttributeString("permissions", "");
                ClientPermissions permissions;
                if (!Enum.TryParse(permissionsStr, out permissions))
                {
                    DebugConsole.ThrowError("Error in " + NilmodClientPermissionsFile + " - \"" + permissionsStr + "\" is not a valid client permission.");
                    DebugConsole.ThrowError("Valid permissions are: Kick, Ban, EndRound, SelectSub, SelectMode, ManageCampaign, ConsoleCommands.");
                    permissions = ClientPermissions.None;
                    Errorcount += 1;
                }

                List<DebugConsole.Command> permittedCommands = new List<DebugConsole.Command>();
                if (permissions.HasFlag(ClientPermissions.ConsoleCommands))
                {
                    foreach (XElement commandElement in clientElement.Elements())
                    {
                        if (commandElement.Name.ToString().ToLowerInvariant() != "command") continue;

                        string commandName = commandElement.GetAttributeString("name", "");
                        DebugConsole.Command command = DebugConsole.FindCommand(commandName);
                        if (command == null)
                        {
                            DebugConsole.ThrowError("Error in " + NilmodClientPermissionsFile + " - \"" + commandName + "\" is not a valid console command.");
                            Errorcount += 1;
                        }

                        permittedCommands.Add(command);
                    }
                }

                SavedClientPermission newsavedpermission = new SavedClientPermission(clientName, clientIP, permissions, permittedCommands);


                //Nilmod slots
                newsavedpermission.OwnerSlot = clientElement.GetAttributeBool("OwnerSlot", false);
                newsavedpermission.AdministratorSlot = clientElement.GetAttributeBool("AdministratorSlot", false);
                newsavedpermission.TrustedSlot = clientElement.GetAttributeBool("TrustedSlot", false);

                newsavedpermission.AllowInGamePM = Xdefaultperms.GetAttributeBool("AllowInGamePM", false);
                newsavedpermission.GlobalChatSend = clientElement.GetAttributeBool("GlobalChatSend", false);
                newsavedpermission.GlobalChatReceive = clientElement.GetAttributeBool("GlobalChatReceive", false);
                newsavedpermission.KarmaImmunity = clientElement.GetAttributeBool("KarmaImmunity", false);
                newsavedpermission.PrioritizeJob = clientElement.GetAttributeBool("PrioritizeJob", false);
                newsavedpermission.IgnoreJobMinimum = clientElement.GetAttributeBool("IgnoreJobMinimum", false);
                newsavedpermission.KickImmunity = clientElement.GetAttributeBool("KickImmunity", false);
                newsavedpermission.BanImmunity = clientElement.GetAttributeBool("BanImmunity", false);

                newsavedpermission.HideJoin = clientElement.GetAttributeBool("HideJoin", false);
                newsavedpermission.AccessDeathChatAlive = clientElement.GetAttributeBool("AccessDeathChatAlive", false);
                newsavedpermission.AdminChannelSend = clientElement.GetAttributeBool("AdminChannelSend", true);
                newsavedpermission.AdminChannelReceive = clientElement.GetAttributeBool("AdminChannelReceive", false);
                newsavedpermission.SendServerConsoleInfo = clientElement.GetAttributeBool("SendServerConsoleInfo", false);
                newsavedpermission.CanBroadcast = clientElement.GetAttributeBool("CanBroadcast", false);

                clientPermissions.Add(newsavedpermission);
            }

            if (Errorcount == 0)
            {
                SaveClientPermissions();
            }
        }

        /// <summary>
        /// Method for loading old .txt client permission files to provide backwards compatibility
        /// </summary>
        private void LoadClientPermissionsOld(string file)
        {
            if (!File.Exists(file)) return;

            string[] lines;
            try
            {
                lines = File.ReadAllLines(file);
            }
            catch (Exception e)
            {
                DebugConsole.ThrowError("Failed to open client permission file " + file, e);
                return;
            }

            clientPermissions.Clear();

            foreach (string line in lines)
            {
                string[] separatedLine = line.Split('|');
                if (separatedLine.Length < 3) continue;

                string name = string.Join("|", separatedLine.Take(separatedLine.Length - 2));
                string ip = separatedLine[separatedLine.Length - 2];

                ClientPermissions permissions = ClientPermissions.None;
                if (Enum.TryParse(separatedLine.Last(), out permissions))
                {
                    clientPermissions.Add(new SavedClientPermission(name, ip, permissions, new List<DebugConsole.Command>()));
                }
            }
        }

        public void LoadVanillaClientPermissions()
        {
            clientPermissions.Clear();

            if (!File.Exists(VanillaClientPermissionsFile))
            {
                if (File.Exists("Data/clientpermissions.txt"))
                {
                    LoadClientPermissionsOld("Data/clientpermissions.txt");
                }
                return;
            }

            XDocument doc = XMLExtensions.TryLoadXml(VanillaClientPermissionsFile);
            foreach (XElement clientElement in doc.Root.Elements())
            {
                string clientName = clientElement.GetAttributeString("name", "");
                string clientIP = clientElement.GetAttributeString("ip", "");
                if (string.IsNullOrWhiteSpace(clientName) || string.IsNullOrWhiteSpace(clientIP))
                {
                    DebugConsole.ThrowError("Error in " + VanillaClientPermissionsFile + " - all clients must have a name and an IP address.");
                    continue;
                }

                string permissionsStr = clientElement.GetAttributeString("permissions", "");
                ClientPermissions permissions;
                if (!Enum.TryParse(permissionsStr, out permissions))
                {
                    DebugConsole.ThrowError("Error in " + VanillaClientPermissionsFile + " - \"" + permissionsStr + "\" is not a valid client permission.");
                    continue;
                }

                List<DebugConsole.Command> permittedCommands = new List<DebugConsole.Command>();
                if (permissions.HasFlag(ClientPermissions.ConsoleCommands))
                {
                    foreach (XElement commandElement in clientElement.Elements())
                    {
                        if (commandElement.Name.ToString().ToLowerInvariant() != "command") continue;

                        string commandName = commandElement.GetAttributeString("name", "");
                        DebugConsole.Command command = DebugConsole.FindCommand(commandName);
                        if (command == null)
                        {
                            DebugConsole.ThrowError("Error in " + VanillaClientPermissionsFile + " - \"" + commandName + "\" is not a valid console command.");
                            continue;
                        }

                        permittedCommands.Add(command);
                    }
                }

                clientPermissions.Add(new SavedClientPermission(clientName, clientIP, permissions, permittedCommands));
            }
        }

        public void SaveClientPermissions()
        {
            //delete old client permission file
            if (File.Exists("Data/clientpermissions.txt"))
            {
                File.Delete("Data/clientpermissions.txt");
            }

            Log("Saving client permissions", ServerLog.MessageType.ServerMessage);

            XDocument doc = new XDocument(new XElement("ClientPermissions"));

            //save defaultpermission first

            XElement DefaultPermissionElement = new XElement("None",
                new XAttribute("AllowInGamePM", defaultpermission.AllowInGamePM),
                new XAttribute("GlobalChatSend", defaultpermission.GlobalChatSend),
                new XAttribute("GlobalChatReceive", defaultpermission.GlobalChatReceive),
                new XAttribute("IgnoreJobMinimum", defaultpermission.IgnoreJobMinimum),
                //new XText(""),
                //"\r\n\r\n ",
                new XAttribute("HideJoin", defaultpermission.HideJoin),
                new XAttribute("AccessDeathChatAlive", defaultpermission.AccessDeathChatAlive),
                new XAttribute("AdminChannelSend", defaultpermission.AdminChannelSend),
                //new XText(""),
                //"\r\n\r\n ",
                new XAttribute("permissions", defaultpermission.Permissions.ToString()));

            if (defaultpermission.Permissions.HasFlag(ClientPermissions.ConsoleCommands))
            {
                foreach (DebugConsole.Command command in defaultpermission.PermittedCommands)
                {
                    DefaultPermissionElement.Add(new XElement("command", new XAttribute("name", command.names[0])));
                }
            }

            doc.Root.Add(DefaultPermissionElement);

            foreach (SavedClientPermission clientPermission in clientPermissions)
            {
                /*
                    XElement clientElement = new XElement("Client",
                    new XAttribute("name", clientPermission.Name),
                    new XAttribute("ip", clientPermission.IP),
                    //new XText(""),
                    "\r\n\r\n ",
                    new XAttribute("OwnerSlot", clientPermission.OwnerSlot),
                    new XAttribute("AdministratorSlot", clientPermission.AdministratorSlot),
                    new XAttribute("TrustedSlot", clientPermission.TrustedSlot),
                    //new XText(""),
                    ////"\r\n\r\n ",
                    //new XAttribute("GlobalChat", clientPermission.GlobalChat),
                    //new XAttribute("PrioritizeJob", clientPermission.PrioritizeJob),
                    //new XAttribute("KickImmunity", clientPermission.KickImmunity),
                    //new XAttribute("BanImmunity", clientPermission.BanImmunity),
                    //new XText(""),
                    ////"\r\n\r\n ",
                    //new XAttribute("HideJoin", clientPermission.HideJoin),
                    //new XAttribute("AccessDeathChatAlive", clientPermission.AccessDeathChatAlive),
                    //new XAttribute("AdminChannelSend", clientPermission.AdminChannelSend),
                    //new XAttribute("AdminChannelReceive", clientPermission.AdminChannelReceive),
                    //new XAttribute("CanBroadcast", clientPermission.CanBroadcast),
                    //new XText(""),
                    ////"\r\n\r\n ",
                    new XAttribute("permissions", clientPermission.Permissions.ToString()));
                    */

                XElement clientElement = new XElement("Client");
                clientElement.Add(new XAttribute("name", clientPermission.Name));
                clientElement.Add(new XAttribute("ip", clientPermission.IP));
                clientElement.Add(new XAttribute("OwnerSlot", clientPermission.OwnerSlot));
                clientElement.Add(new XAttribute("AdministratorSlot", clientPermission.AdministratorSlot));
                clientElement.Add(new XAttribute("TrustedSlot", clientPermission.TrustedSlot));
                clientElement.Add(new XAttribute("AllowInGamePM", clientPermission.AllowInGamePM));
                clientElement.Add(new XAttribute("GlobalChatSend", clientPermission.GlobalChatSend));
                clientElement.Add(new XAttribute("GlobalChatReceive", clientPermission.GlobalChatReceive));
                clientElement.Add(new XAttribute("KarmaImmunity", clientPermission.KarmaImmunity));
                clientElement.Add(new XAttribute("PrioritizeJob", clientPermission.PrioritizeJob));
                clientElement.Add(new XAttribute("IgnoreJobMinimum", clientPermission.IgnoreJobMinimum));
                clientElement.Add(new XAttribute("KickImmunity", clientPermission.KickImmunity));
                clientElement.Add(new XAttribute("BanImmunity", clientPermission.BanImmunity));
                clientElement.Add(new XAttribute("HideJoin", clientPermission.HideJoin));
                clientElement.Add(new XAttribute("AccessDeathChatAlive", clientPermission.AccessDeathChatAlive));
                clientElement.Add(new XAttribute("AdminPrivateMessage", clientPermission.AdminPrivateMessage));
                clientElement.Add(new XAttribute("AdminChannelSend", clientPermission.AdminChannelSend));
                clientElement.Add(new XAttribute("AdminChannelReceive", clientPermission.AdminChannelReceive));
                clientElement.Add(new XAttribute("SendServerConsoleInfo", clientPermission.SendServerConsoleInfo));
                clientElement.Add(new XAttribute("CanBroadcast", clientPermission.CanBroadcast));
                clientElement.Add(new XAttribute("permissions", clientPermission.Permissions.ToString()));

                if (clientPermission.Permissions.HasFlag(ClientPermissions.ConsoleCommands))
                {
                    foreach (DebugConsole.Command command in clientPermission.PermittedCommands)
                    {
                        clientElement.Add(new XElement("command", new XAttribute("name", command.names[0])));
                    }
                }

                doc.Root.Add(clientElement);
            }

            try
            {
                XmlWriterSettings settings = new XmlWriterSettings();
                settings.Indent = true;
                settings.NewLineOnAttributes = true;

                using (var writer = XmlWriter.Create(NilmodClientPermissionsFile, settings))
                {
                    doc.Save(writer);
                }
            }
            catch (Exception e)
            {
                DebugConsole.ThrowError("Saving client permissions to " + NilmodClientPermissionsFile + " failed", e);
            }
        }

        public void StateServerInfo()
        {
            DebugConsole.NewMessage("Server is hosted on: " + GameMain.NilMod.ExternalIP + ":" + Port, Color.Cyan);
            DebugConsole.NewMessage((isPublic ? @"Publicly Under the name: """ + name + @"""" : @"Privately Under the name: """ + name + @"""") + " with UPNP " + (config.EnableUPnP ? "enabled." : "disabled."), Color.Cyan);
            DebugConsole.NewMessage("With max players: " + maxPlayers + ".", Color.Cyan);

            DebugConsole.NewMessage(" ", Color.Cyan);

            if (password != "")
            {
                DebugConsole.NewMessage(@"Server is using the password """ + password + (whitelist.Enabled ? @""" and has an active white list with " + whitelist.WhiteListedPlayers.Count() + " whitelisted players." : "with its whitelist disabled."), Color.Cyan);
            }
            else if (whitelist.Enabled)
            {
                DebugConsole.NewMessage("Server is not using a password but has an active white list with " + whitelist.WhiteListedPlayers.Count() + " whitelisted players.", Color.Cyan);
            }
            else
            {
                DebugConsole.NewMessage("Server has no active password or whitelist.", Color.Cyan);
            }
        }
    }
}