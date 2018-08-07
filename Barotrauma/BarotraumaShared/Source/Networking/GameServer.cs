﻿using Barotrauma.Items.Components;
using Lidgren.Network;
using Microsoft.Xna.Framework;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Barotrauma.Networking
{
    partial class GameServer : NetworkMember
    {
        private List<Client> connectedClients = new List<Client>();

        //for keeping track of disconnected clients in case the reconnect shortly after
        private List<Client> disconnectedClients = new List<Client>();

        private int roundStartSeed;

        //is the server running
        private bool started;

        public NetServer server;
        public NetPeerConfiguration config;

        private DateTime refreshMasterTimer;

        private DateTime roundStartTime;

        private RestClient restClient;
        private bool masterServerResponded;
        private IRestResponse masterServerResponse;
        private int FailedCount;

        private ServerLog log;

        private bool initiatedStartGame;
        private CoroutineHandle startGameCoroutine;

        public TraitorManager TraitorManager;

        private ServerEntityEventManager entityEventManager;

        private FileSender fileSender;

        public override List<Client> ConnectedClients
        {
            get
            {
                return connectedClients;
            }
        }


        public ServerEntityEventManager EntityEventManager
        {
            get { return entityEventManager; }
        }

        public ServerLog ServerLog
        {
            get { return log; }
        }

        public TimeSpan UpdateInterval
        {
            get { return updateInterval; }
        }

        public GameServer(string name, int port, bool isPublic = false, string password = "", bool attemptUPnP = false, int maxPlayers = 10, NetServer prevserver = null, NetPeerConfiguration prevconfig = null)
        {
            name = name.Replace(":", "");
            name = name.Replace(";", "");

            //AdminAuthPass = "";

            //Nilmod AdminAuthPass
            //AdminAuthPass = GameMain.NilMod.AdminAuth;

            this.name = name;
            this.isPublic = isPublic;
            this.maxPlayers = maxPlayers;
            this.password = "";
            if (password.Length > 0)
            {
                SetPassword(password);
            }

            if (prevconfig == null)
            {
                config = new NetPeerConfiguration("barotrauma");
            }
            else
            {
                config = prevconfig;
            }

#if CLIENT
            netStats = new NetStats();
#endif

            /*
            #if DEBUG
            
                config.SimulatedLoss = 0.05f;
                config.SimulatedRandomLatency = 0.05f;
                config.SimulatedDuplicatesChance = 0.05f;
                config.SimulatedMinimumLatency = 0.1f;

                config.ConnectionTimeout = 60.0f;

            #endif 
            */

            //NilMod DebugLagActive
            if (GameMain.NilMod.DebugLag)
            {
                config.SimulatedLoss = GameMain.NilMod.DebugLagSimulatedPacketLoss;
                config.SimulatedRandomLatency = GameMain.NilMod.DebugLagSimulatedRandomLatency;
                config.SimulatedDuplicatesChance = GameMain.NilMod.DebugLagSimulatedDuplicatesChance;
                config.SimulatedMinimumLatency = GameMain.NilMod.DebugLagSimulatedMinimumLatency;

                config.ConnectionTimeout = GameMain.NilMod.DebugLagConnectionTimeout;
            }

            //NetIdUtils.Test();

            if (prevconfig == null)
            {
                config.Port = port;
                Port = port;
                
                config.MaximumConnections = maxPlayers * 2; //double the lidgren connections for unauthenticated players
            }
            else
            {
                Port = config.Port;
            }

            if (attemptUPnP)
            {
                config.EnableUPnP = true;
            }

            config.DisableMessageType(NetIncomingMessageType.DebugMessage |
                NetIncomingMessageType.WarningMessage | NetIncomingMessageType.Receipt |
                NetIncomingMessageType.ErrorMessage |
                NetIncomingMessageType.UnconnectedData);

            config.EnableMessageType(NetIncomingMessageType.Error);

            config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);

            log = new ServerLog(name);

            InitProjSpecific();

            entityEventManager = new ServerEntityEventManager(this);

            whitelist = new WhiteList();
            banList = new BanList();

            LoadSettings();
            PermissionPreset.LoadAll(PermissionPresetFile);
            LoadClientPermissions();

            CoroutineManager.StartCoroutine(StartServer(isPublic,prevserver,prevconfig));
        }

        public void SetPassword(string password)
        {
            this.password = Encoding.UTF8.GetString(NetUtility.ComputeSHAHash(Encoding.UTF8.GetBytes(password)));
        }

        private IEnumerable<object> StartServer(bool isPublic, NetServer prevserver = null, NetPeerConfiguration prevconfig = null)
        {
            bool error = false;
            try
            {
                if(prevserver == null)
                {
                    Log("Starting the server...", ServerLog.MessageType.ServerMessage);
                    server = new NetServer(config);
                    netPeer = server;

                    fileSender = new FileSender(this);
                    fileSender.OnEnded += FileTransferChanged;
                    fileSender.OnStarted += FileTransferChanged;
                    server.Start();
                }
                else
                {
                    Log("Restarting the server...", ServerLog.MessageType.ServerMessage);
                    server = prevserver;
                    netPeer = server;

                    fileSender = new FileSender(this);
                    fileSender.OnEnded += FileTransferChanged;
                    fileSender.OnStarted += FileTransferChanged;
                    server.Start();
                }
            }
            catch (Exception e)
            {
                Log("Error while starting the server (" + e.Message + ")", ServerLog.MessageType.Error);

                System.Net.Sockets.SocketException socketException = e as System.Net.Sockets.SocketException;

#if CLIENT
                if (socketException != null && socketException.SocketErrorCode == System.Net.Sockets.SocketError.AddressAlreadyInUse)
                {
                    new GUIMessageBox("Starting the server failed", e.Message + ". Are you trying to run multiple servers on the same port?");
                }
                else
                {
                    new GUIMessageBox("Starting the server failed", e.Message);
                }
#endif

                error = true;
            }

            if (error)
            {
                if (server != null) server.Shutdown("Error while starting the server");

#if CLIENT
                GameMain.NetworkMember = null;
#elif SERVER
                Environment.Exit(-1);
#endif
                yield return CoroutineStatus.Success;
            }

            if (config.EnableUPnP)
            {
                InitUPnP();

                //DateTime upnpTimeout = DateTime.Now + new TimeSpan(0,0,5);
                while (DiscoveringUPnP())// && upnpTimeout>DateTime.Now)
                {
                    yield return null;
                }

                FinishUPnP();
            }

            if (isPublic)
            {
                CoroutineManager.StartCoroutine(RegisterToMasterServer());
            }

            updateInterval = new TimeSpan(0, 0, 0, 0, 150);

            Log("Server started", ServerLog.MessageType.ServerMessage);

            GameMain.NilMod.serverruntime = new Stopwatch();
            GameMain.NilMod.serverruntime.Start();

            GameMain.NetLobbyScreen.Select();

#if CLIENT
            GameMain.NetLobbyScreen.DefaultServerStartupSubSelect();
            GameSession.inGameInfo.Initialize();
#endif

            GameMain.NilMod.GameInitialize(true);
            GameMain.NetLobbyScreen.RandomizeSettings();
            started = true;

            GameAnalyticsManager.AddDesignEvent("GameServer:Start");

            yield return CoroutineStatus.Success;
        }

        private IEnumerable<object> RegisterToMasterServer()
        {
            if (restClient == null)
            {
                restClient = new RestClient(NetConfig.MasterServerUrl);
            }

            GameMain.NilMod.RecheckPlayerCounts();

            var request = new RestRequest("masterserver3.php", Method.GET);
            request.AddParameter("action", "addserver");
            request.AddParameter("servername", name);
            request.AddParameter("serverport", Port);
            request.AddParameter("currplayers", GameMain.NilMod.CurrentPlayers);
            request.AddParameter("maxplayers", maxPlayers);
            request.AddParameter("password", string.IsNullOrWhiteSpace(password) ? 0 : 1);
            request.AddParameter("version", GameMain.Version.ToString());
            if (GameMain.Config.SelectedContentPackage != null)
            {
                request.AddParameter("contentpackage", GameMain.Config.SelectedContentPackage.Name);
            }

            masterServerResponded = false;
            masterServerResponse = null;
            var restRequestHandle = restClient.ExecuteAsync(request, response => MasterServerCallBack(response));

            DateTime timeOut = DateTime.Now + new TimeSpan(0, 0, 15);
            while (!masterServerResponded)
            {
                if (DateTime.Now > timeOut)
                {
                    restRequestHandle.Abort();
                    DebugConsole.NewMessage("Couldn't register to master server (request timed out)", Color.Red);
                    Log("Couldn't register to master server (request timed out)", ServerLog.MessageType.Error);

                    FailedCount += 1;
                    if (FailedCount >= 10)
                    {
                        restClient = new RestClient(NetConfig.MasterServerUrl);
                        masterServerResponded = false;
                        masterServerResponse = null;

                        DebugConsole.NewMessage("Excessive timeouts detected in masterserver - Attempting reset of RestClient.", Color.Red);
                        Log("Over 10 timeouts detected in masterserver - Resetting the RestClient.", ServerLog.MessageType.Error);
                        FailedCount = 0;
                    }

                    yield return CoroutineStatus.Success;
                }

                yield return CoroutineStatus.Running;
            }

            if (masterServerResponse.StatusCode != System.Net.HttpStatusCode.OK)
            {
                DebugConsole.ThrowError("Error while connecting to master server (" + masterServerResponse.StatusCode + ": " + masterServerResponse.StatusDescription + ")");
            }
            else if (masterServerResponse != null && !string.IsNullOrWhiteSpace(masterServerResponse.Content))
            {
                DebugConsole.ThrowError("Error while connecting to master server (" + masterServerResponse.Content + ")");
            }
            else
            {
                registeredToMaster = true;
                refreshMasterTimer = DateTime.Now + refreshMasterInterval;
            }

            yield return CoroutineStatus.Success;
        }

        private IEnumerable<object> RefreshMaster()
        {
            if (restClient == null)
            {
                restClient = new RestClient(NetConfig.MasterServerUrl);
            }

            GameMain.NilMod.RecheckPlayerCounts();

            var request = new RestRequest("masterserver3.php", Method.GET);
            request.AddParameter("action", "refreshserver");
            request.AddParameter("serverport", Port);
            request.AddParameter("gamestarted", gameStarted ? 1 : 0);
            request.AddParameter("currplayers", GameMain.NilMod.CurrentPlayers);
            request.AddParameter("maxplayers", maxPlayers);

            if (GameMain.NilMod.ShowMasterServerSuccess)
            {
                Log("Refreshing connection with master server...", ServerLog.MessageType.ServerMessage);
            }

            var sw = new Stopwatch();
            sw.Start();

            masterServerResponded = false;
            masterServerResponse = null;
            var restRequestHandle = restClient.ExecuteAsync(request, response => MasterServerCallBack(response));

            DateTime timeOut = DateTime.Now + new TimeSpan(0, 0, 15);
            while (!masterServerResponded)
            {
                if (DateTime.Now > timeOut)
                {
                    restRequestHandle.Abort();
                    DebugConsole.NewMessage("Couldn't connect to master server (request timed out)", Color.Red);
                    Log("Couldn't connect to master server (request timed out)", ServerLog.MessageType.Error);

                    FailedCount += 1;
                    if (FailedCount >= 10)
                    {
                        restClient = new RestClient(NetConfig.MasterServerUrl);
                        masterServerResponded = false;
                        masterServerResponse = null;

                        DebugConsole.NewMessage("Excessive timeouts detected in masterserver - Attempting reset of RestClient.", Color.Red);
                        Log("Over 10 timeouts detected in masterserver - Resetting the RestClient.", ServerLog.MessageType.Error);
                        FailedCount = 0;
                    }
                    yield return CoroutineStatus.Success;
                }

                yield return CoroutineStatus.Running;
            }

            if (masterServerResponse.Content == "Error: server not found")
            {
                Log("Not registered to master server, re-registering...", ServerLog.MessageType.Error);
                CoroutineManager.StartCoroutine(RegisterToMasterServer());
            }
            else if (masterServerResponse.ErrorException != null)
            {
                DebugConsole.NewMessage("Error while registering to master server (" + masterServerResponse.ErrorException + ")", Color.Red);
                Log("Error while registering to master server (" + masterServerResponse.ErrorException + ")", ServerLog.MessageType.Error);
            }
            else if (masterServerResponse.StatusCode != System.Net.HttpStatusCode.OK)
            {
                DebugConsole.NewMessage("Error while reporting to master server (" + masterServerResponse.StatusCode + ": " + masterServerResponse.StatusDescription + ")", Color.Red);
                Log("Error while reporting to master server (" + masterServerResponse.StatusCode + ": " + masterServerResponse.StatusDescription + ")", ServerLog.MessageType.Error);
            }
            else
            {
                if (GameMain.NilMod.ShowMasterServerSuccess)
                {
                    Log("Master server responded", ServerLog.MessageType.ServerMessage);
                }
            }

            System.Diagnostics.Debug.WriteLine("took " + sw.ElapsedMilliseconds + " ms");

            yield return CoroutineStatus.Success;
        }

        private void MasterServerCallBack(IRestResponse response)
        {
            masterServerResponse = response;
            masterServerResponded = true;
        }

        public override void Update(float deltaTime)
        {
#if CLIENT
            if (ShowNetStats) netStats.Update(deltaTime);
            if (settingsFrame != null) settingsFrame.Update(deltaTime);
            if (log.LogFrame != null) log.LogFrame.Update(deltaTime);
#endif

            if (!started) return;

            base.Update(deltaTime);

            foreach (UnauthenticatedClient unauthClient in unauthenticatedClients)
            {
                unauthClient.AuthTimer -= deltaTime;
                if (unauthClient.AuthTimer <= 0.0f)
                {
                    unauthClient.Connection.Disconnect("Connection timed out");
                }
            }

            unauthenticatedClients.RemoveAll(uc => uc.AuthTimer <= 0.0f);

            fileSender.Update(deltaTime);

            if (gameStarted)
            {
                if (respawnManager != null) respawnManager.Update(deltaTime);

                entityEventManager.Update(connectedClients);

                foreach (Character character in Character.CharacterList)
                {
                    if (character.IsDead || !character.ClientDisconnected) continue;

                    character.KillDisconnectedTimer += deltaTime;
                    character.SetStun(1.0f);
                    if (character.KillDisconnectedTimer > KillDisconnectedTime)
                    {
                        character.Kill(CauseOfDeath.Disconnected);
                        continue;
                    }

                    Client owner = connectedClients.Find(c =>
                        c.InGame && !c.NeedsMidRoundSync &&
                        c.Name == character.OwnerClientName &&
                        c.Connection.RemoteEndPoint.Address.ToString() == character.OwnerClientIP);
                    if (owner != null && (!AllowSpectating || !owner.SpectateOnly))
                    {
                        SetClientCharacter(owner, character);
                    }
                }

#if CLIENT
                if (GameMain.NilMod.ActiveClickCommand)
                {
                    ClickCommandUpdate(deltaTime);
                }
#endif

                bool isCrewDead =
                    connectedClients.All(c => c.Character == null || c.Character.IsDead || c.Character.IsUnconscious) &&
                    (myCharacter == null || myCharacter.IsDead || myCharacter.IsUnconscious);

                //restart if all characters are dead or submarine is at the end of the level
                if ((autoRestart && isCrewDead)
                    ||
                    (EndRoundAtLevelEnd && Submarine.MainSub != null && Submarine.MainSub.AtEndPosition && Submarine.MainSubs[1] == null))
                {
                    if (AutoRestart && isCrewDead)
                    {
                        GameMain.NilMod.RoundEnded = true;
                        Log("Ending round (entire crew dead)", ServerLog.MessageType.ServerMessage);
                    }
                    else
                    {
                        GameMain.NilMod.RoundEnded = false;
                        Log("Ending round (submarine reached the end of the level)", ServerLog.MessageType.ServerMessage);
                    }

                    EndGame();
                    return;
                }
            }
            else if (initiatedStartGame)
            {
                //tried to start up the game and StartGame coroutine is not running anymore
                // -> something wen't wrong during startup, re-enable start button and reset AutoRestartTimer
                if (startGameCoroutine != null && !CoroutineManager.IsCoroutineRunning(startGameCoroutine))
                {
                    if (autoRestart) AutoRestartTimer = Math.Max(AutoRestartInterval, 5.0f);
                    GameMain.NetLobbyScreen.StartButtonEnabled = true;

                    GameMain.NetLobbyScreen.LastUpdateID++;

                    startGameCoroutine = null;
                    initiatedStartGame = false;
                }
            }
            else if (autoRestart && Screen.Selected == GameMain.NetLobbyScreen && connectedClients.Count > 0)
            {
                AutoRestartTimer -= deltaTime;
                if (AutoRestartTimer < 0.0f && !initiatedStartGame)
                {
                    StartGame();
                }
            }

            /*
            for (int i = disconnectedClients.Count - 1; i >= 0; i--)
            {
                disconnectedClients[i].DeleteDisconnectedTimer -= deltaTime;
                if (disconnectedClients[i].DeleteDisconnectedTimer > 0.0f) continue;

                if (gameStarted && disconnectedClients[i].Character != null)
                {
                    if (!GameMain.NilMod.AllowReconnect)
                    {
                        disconnectedClients[i].Character.Kill(CauseOfDeath.Damage, true);
                    }
                    else
                    {
                        disconnectedClients[i].Character.ClearInputs();
                        disconnectedClients[i].Character.ResetNetState();
                    }
                    disconnectedClients[i].Character = null;
                }

                disconnectedClients.RemoveAt(i);
            }
            */

            foreach (Client c in connectedClients)
            {
                //slowly reset spam timers
                c.ChatSpamTimer = Math.Max(0.0f, c.ChatSpamTimer - deltaTime);
                c.ChatSpamSpeed = Math.Max(0.0f, c.ChatSpamSpeed - deltaTime);
            }

            NetIncomingMessage inc = null;
            while ((inc = server.ReadMessage()) != null)
            {
                try
                {
                    switch (inc.MessageType)
                    {
                        case NetIncomingMessageType.Data:
                            ReadDataMessage(inc);
                            break;
                        case NetIncomingMessageType.StatusChanged:
                            switch (inc.SenderConnection.Status)
                            {
                                case NetConnectionStatus.Disconnected:
                                    var connectedClient = connectedClients.Find(c => c.Connection == inc.SenderConnection);
                                    /*if (connectedClient != null && !disconnectedClients.Contains(connectedClient))
                                    {
                                        connectedClient.deleteDisconnectedTimer = NetConfig.DeleteDisconnectedTime;
                                        disconnectedClients.Add(connectedClient);
                                    }
                                    */
                                    DisconnectClient(inc.SenderConnection,
                                        connectedClient != null ? connectedClient.Name + " has disconnected" : "");
                                    break;
                            }
                            break;
                        case NetIncomingMessageType.ConnectionApproval:
                            /*
                            if (banList.IsBanned(inc.SenderEndPoint.Address.ToString()))
                            {
                                DebugConsole.NewMessage("Banned Player tried to join the server (" + inc.SenderEndPoint.Address.ToString() + ")", Color.Red);
                                inc.SenderConnection.Deny("You have been banned from the server");
                            }
                            else */

                            GameMain.NilMod.RecheckPlayerCounts();

                            var precheckPermissions = clientPermissions.Find(cp => cp.IP == inc.SenderConnection.RemoteEndPoint.Address.ToString());

                            if ((GameMain.NilMod.CurrentPlayers + unauthenticatedClients.Count) >= maxPlayers)
                            {
                                if (precheckPermissions.OwnerSlot || precheckPermissions.AdministratorSlot || precheckPermissions.TrustedSlot)
                                {
                                    if ((ClientPacketHeader)inc.SenderConnection.RemoteHailMessage.ReadByte() == ClientPacketHeader.REQUEST_AUTH)
                                    {
                                        inc.SenderConnection.Approve();
                                        ClientAuthRequest(inc.SenderConnection);
                                    }
                                }
                                else
                                {
                                    //server is full, can't allow new connection
                                    inc.SenderConnection.Deny("Server full");
                                    return;
                                }
                            }
                            else
                            {
                                if ((ClientPacketHeader)inc.SenderConnection.RemoteHailMessage.ReadByte() == ClientPacketHeader.REQUEST_AUTH)
                                {
                                    inc.SenderConnection.Approve();
                                    ClientAuthRequest(inc.SenderConnection);
                                }
                            }
                            break;
                    }
                }

                catch (Exception e)
                {
                    if (GameSettings.VerboseLogging)
                    {
                        DebugConsole.ThrowError("Failed to read an incoming message. {" + e + "}\n" + e.StackTrace);
                    }
                }
            }

            // if 30ms has passed
            if (updateTimer < DateTime.Now)
            {
                for (int i = Character.CharacterList.Count - 1; i >= 0; i--)
                {
                    //Character.CharacterList[i].CheckForStatusEvent();
                }


                if (server.ConnectionsCount > 0)
                {
                    foreach (Client c in ConnectedClients)
                    {
                        try
                        {
                            ClientWrite(c);
                        }
                        catch (Exception e)
                        {
                            DebugConsole.ThrowError("Failed to write a network message for the client \"" + c.Name + "\"!", e);
                            GameAnalyticsManager.AddErrorEventOnce("GameServer.Update:ClientWriteFailed" + e.StackTrace, GameAnalyticsSDK.Net.EGAErrorSeverity.Error,
                                "Failed to write a network message for the client \"" + c.Name + "\"! (MidRoundSyncing: " + c.NeedsMidRoundSync + ")\n"
                                + e.Message + "\n" + e.StackTrace);
                        }
                    }


                    //Reset the send timer
                    if (GameMain.NilMod.SyncResendTimer <= 0f)
                    {
                        GameMain.NilMod.SyncResendTimer = NilMod.SyncResendInterval;
                    }

                    foreach (Item item in Item.ItemList)
                    {
                        item.NeedsPositionUpdate = false;
                    }
                }

                if (GameMain.NilMod.AutoRestartServer && new TimeSpan(GameMain.NilMod.serverruntime.Elapsed.Hours,
                    GameMain.NilMod.serverruntime.Elapsed.Minutes,
                    GameMain.NilMod.serverruntime.Elapsed.Seconds) > GameMain.NilMod.ServerRestartInterval)
                {
                    if (!CoroutineManager.IsCoroutineRunning("serverrestart") && !GameStarted && !initiatedStartGame)
                    {
                        CoroutineManager.StartCoroutine(PerformRestart(), "serverrestart");
                        GameMain.NilMod.serverruntime.Restart();
                    }
                }
                //Check if server needs restart


                updateTimer = DateTime.Now + updateInterval;
            }

            if (!registeredToMaster || refreshMasterTimer >= DateTime.Now) return;

            CoroutineManager.StartCoroutine(RefreshMaster());
            refreshMasterTimer = DateTime.Now + refreshMasterInterval;
        }

        private void ReadDataMessage(NetIncomingMessage inc)
        {
            /*if (banList.IsBanned(inc.SenderEndPoint.Address.ToString()))
            {
                KickClient(inc.SenderConnection, "You have been banned from the server.");
                return;
            }*/

            ClientPacketHeader header = (ClientPacketHeader)inc.ReadByte();
            switch (header)
            {
                case ClientPacketHeader.REQUEST_AUTH:
                    ClientAuthRequest(inc.SenderConnection);
                    break;
                case ClientPacketHeader.REQUEST_INIT:
                    ClientInitRequest(inc);
                    break;

                case ClientPacketHeader.RESPONSE_STARTGAME:
                    if (banList.IsBanned(inc.SenderEndPoint.Address.ToString()))
                    {
                        if (BanList.GetBanReason(inc.SenderEndPoint.Address.ToString()) != null)
                        {
                            KickBannedClient(inc.SenderConnection, "\nReason: "
                                + BanList.GetBanReason(inc.SenderEndPoint.Address.ToString()));

                            //DisconnectClient(inc.SenderConnection,"", "You have been banned from the server." + "\nReason: "
                            //    + BanList.GetBanReason(inc.SenderEndPoint.Address.ToString()));

                            //KickClient(inc.SenderConnection, "You have been banned from the server." + "\nReason: " 
                            //    + BanList.GetBanReason(inc.SenderEndPoint.Address.ToString()));
                        }
                        else
                        {
                            KickBannedClient(inc.SenderConnection, "");
                            //KickClient(inc.SenderConnection, "You have been banned from the server.");
                        }

                        //KickClient(inc.SenderConnection, "You have been banned from the server.");
                        return;
                    }
                    var connectedClient = connectedClients.Find(c => c.Connection == inc.SenderConnection);
                    if (connectedClient != null)
                    {
                        connectedClient.ReadyToStart = inc.ReadBoolean();
                        UpdateCharacterInfo(inc, connectedClient);

                        //game already started -> send start message immediately
                        if (gameStarted)
                        {
                            SendStartMessage(roundStartSeed, Submarine.MainSub, GameMain.GameSession.GameMode.Preset, connectedClient);
                        }
                    }
                    break;
                case ClientPacketHeader.UPDATE_LOBBY:
                    if (banList.IsBanned(inc.SenderEndPoint.Address.ToString()))
                    {
                        if (BanList.GetBanReason(inc.SenderEndPoint.Address.ToString()) != null)
                        {
                            KickBannedClient(inc.SenderConnection, "\nReason: "
                                + BanList.GetBanReason(inc.SenderEndPoint.Address.ToString()));

                            //DisconnectClient(inc.SenderConnection,"", "You have been banned from the server." + "\nReason: "
                            //    + BanList.GetBanReason(inc.SenderEndPoint.Address.ToString()));

                            //KickClient(inc.SenderConnection, "You have been banned from the server." + "\nReason: " 
                            //    + BanList.GetBanReason(inc.SenderEndPoint.Address.ToString()));
                        }
                        else
                        {
                            KickBannedClient(inc.SenderConnection, "");
                            //KickClient(inc.SenderConnection, "You have been banned from the server.");
                        }

                        //KickClient(inc.SenderConnection, "You have been banned from the server.");
                        return;
                    }
                    ClientReadLobby(inc);
                    break;
                case ClientPacketHeader.UPDATE_INGAME:
                    if (!gameStarted) return;
                    if (banList.IsBanned(inc.SenderEndPoint.Address.ToString()))
                    {
                        if (BanList.GetBanReason(inc.SenderEndPoint.Address.ToString()) != null)
                        {
                            KickBannedClient(inc.SenderConnection, "\nReason: "
                                + BanList.GetBanReason(inc.SenderEndPoint.Address.ToString()));

                            //DisconnectClient(inc.SenderConnection,"", "You have been banned from the server." + "\nReason: "
                            //    + BanList.GetBanReason(inc.SenderEndPoint.Address.ToString()));

                            //KickClient(inc.SenderConnection, "You have been banned from the server." + "\nReason: " 
                            //    + BanList.GetBanReason(inc.SenderEndPoint.Address.ToString()));
                        }
                        else
                        {
                            KickBannedClient(inc.SenderConnection, "");
                            //KickClient(inc.SenderConnection, "You have been banned from the server.");
                        }

                        return;
                    }
                    ClientReadIngame(inc);
                    break;
                case ClientPacketHeader.SERVER_COMMAND:
                    if (banList.IsBanned(inc.SenderEndPoint.Address.ToString()))
                    {
                        if (BanList.GetBanReason(inc.SenderEndPoint.Address.ToString()) != null)
                        {
                            KickBannedClient(inc.SenderConnection, "\nReason: "
                                + BanList.GetBanReason(inc.SenderEndPoint.Address.ToString()));

                            //DisconnectClient(inc.SenderConnection,"", "You have been banned from the server." + "\nReason: "
                            //    + BanList.GetBanReason(inc.SenderEndPoint.Address.ToString()));

                            //KickClient(inc.SenderConnection, "You have been banned from the server." + "\nReason: " 
                            //    + BanList.GetBanReason(inc.SenderEndPoint.Address.ToString()));
                        }
                        else
                        {
                            KickBannedClient(inc.SenderConnection, "");
                            //KickClient(inc.SenderConnection, "You have been banned from the server.");
                        }

                        //KickClient(inc.SenderConnection, "You have been banned from the server.");
                        return;
                    }
                    ClientReadServerCommand(inc);
                    break;
                case ClientPacketHeader.FILE_REQUEST:
                    if (AllowFileTransfers)
                    {
                        if (banList.IsBanned(inc.SenderEndPoint.Address.ToString()))
                        {
                            if (BanList.GetBanReason(inc.SenderEndPoint.Address.ToString()) != null)
                            {
                                KickBannedClient(inc.SenderConnection, "\nReason: "
                                + BanList.GetBanReason(inc.SenderEndPoint.Address.ToString()));

                                //DisconnectClient(inc.SenderConnection,"", "You have been banned from the server." + "\nReason: "
                                //    + BanList.GetBanReason(inc.SenderEndPoint.Address.ToString()));

                                //KickClient(inc.SenderConnection, "You have been banned from the server." + "\nReason: " 
                                //    + BanList.GetBanReason(inc.SenderEndPoint.Address.ToString()));
                            }
                            else
                            {
                                KickBannedClient(inc.SenderConnection, "");
                                //KickClient(inc.SenderConnection, "You have been banned from the server.");
                            }

                            //KickClient(inc.SenderConnection, "You have been banned from the server.");
                            return;
                        }
                        fileSender.ReadFileRequest(inc);
                    }
                    break;
                case ClientPacketHeader.NILMODSYNCRECEIVED:
                    var syncreceivedclient = connectedClients.Find(c => c.Connection == inc.SenderConnection);
                    Byte NilModSyncState = inc.ReadByte();

                    //Version is Correct
                    if (NilModSyncState == 0)
                    {
                        if (syncreceivedclient != null)
                        {
                            syncreceivedclient.RequiresNilModSync = false;
                        }
                    }
                    //Version is Earlier
                    else if (NilModSyncState == 1)
                    {
                        if (syncreceivedclient != null)
                        {
                            syncreceivedclient.IsNilModClient = false;
                            syncreceivedclient.RequiresNilModSync = false;
                        }
                    }
                    //Version is later
                    else if (NilModSyncState == 2)
                    {
                        syncreceivedclient.IsNilModClient = false;
                        syncreceivedclient.RequiresNilModSync = false;
                    }

                    inc.ReadPadBits();
                    break;
            }
        }

        public void CreateEntityEvent(IServerSerializable entity, object[] extraData = null)
        {
            entityEventManager.CreateEvent(entity, extraData);
        }

        private byte GetNewClientID()
        {
            byte userID = 1;
            while (connectedClients.Any(c => c.ID == userID))
            {
                userID++;
            }
            return userID;
        }

        private void ClientReadLobby(NetIncomingMessage inc)
        {
            Client c = ConnectedClients.Find(x => x.Connection == inc.SenderConnection);
            if (c == null)
            {
                inc.SenderConnection.Disconnect("You're not a connected client.");
                return;
            }

            ClientNetObject objHeader;
            while ((objHeader = (ClientNetObject)inc.ReadByte()) != ClientNetObject.END_OF_MESSAGE)
            {
                switch (objHeader)
                {
                    case ClientNetObject.SYNC_IDS:
                        //TODO: might want to use a clever class for this
                        c.LastRecvGeneralUpdate = NetIdUtils.Clamp(inc.ReadUInt16(), c.LastRecvGeneralUpdate, GameMain.NetLobbyScreen.LastUpdateID);
                        c.LastRecvChatMsgID = NetIdUtils.Clamp(inc.ReadUInt16(), c.LastRecvChatMsgID, c.LastChatMsgQueueID);

                        c.LastRecvCampaignSave = inc.ReadUInt16();
                        if (c.LastRecvCampaignSave > 0)
                        {
                            byte campaignID = inc.ReadByte();
                            c.LastRecvCampaignUpdate = inc.ReadUInt16();

                            if (GameMain.GameSession?.GameMode is MultiPlayerCampaign)
                            {
                                //the client has a campaign save for another campaign  
                                //(the server started a new campaign and the client isn't aware of it yet?) 
                                if (((MultiPlayerCampaign)GameMain.GameSession.GameMode).CampaignID != campaignID)
                                {
                                    c.LastRecvCampaignSave = 0;
                                    c.LastRecvCampaignUpdate = 0;
                                }
                            }
                        }
                        break;
                    case ClientNetObject.CHAT_MESSAGE:
                        ChatMessage.ServerRead(inc, c);
                        break;
                    case ClientNetObject.VOTE:
                        Voting.ServerRead(inc, c);
                        break;
                    default:
                        return;
                }

                //don't read further messages if the client has been disconnected (kicked due to spam for example)
                if (!connectedClients.Contains(c)) break;
            }
        }

        private void ClientReadIngame(NetIncomingMessage inc)
        {
            Client c = ConnectedClients.Find(x => x.Connection == inc.SenderConnection);
            if (c == null)
            {
                inc.SenderConnection.Disconnect("You're not a connected client.");
                return;
            }

            if (gameStarted)
            {
                if (!c.InGame)
                {
                    //check if midround syncing is needed due to missed unique events
                    entityEventManager.InitClientMidRoundSync(c);
                    c.InGame = true;
                }
            }

            ClientNetObject objHeader;
            while ((objHeader = (ClientNetObject)inc.ReadByte()) != ClientNetObject.END_OF_MESSAGE)
            {
                switch (objHeader)
                {
                    case ClientNetObject.SYNC_IDS:
                        //TODO: might want to use a clever class for this

                        UInt16 lastRecvChatMsgID = inc.ReadUInt16();
                        UInt16 lastRecvEntityEventID = inc.ReadUInt16();

                        //last msgs we've created/sent, the client IDs should never be higher than these
                        UInt16 lastEntityEventID = entityEventManager.Events.Count == 0 ? (UInt16)0 : entityEventManager.Events.Last().ID;

                        if (c.NeedsMidRoundSync)
                        {
                            //received all the old events -> client in sync, we can switch to normal behavior
                            if (lastRecvEntityEventID >= c.UnreceivedEntityEventCount - 1 ||
                                c.UnreceivedEntityEventCount == 0)
                            {
                                c.NeedsMidRoundSync = false;
                                lastRecvEntityEventID = (UInt16)(c.FirstNewEventID - 1);
                                c.LastRecvEntityEventID = lastRecvEntityEventID;

                                /*
                                if (GameMain.NilMod.AllowReconnect)
                                {
                                    DisconnectedCharacter disconnectedcharcheck = GameMain.NilMod.DisconnectedCharacters.Find(dc => dc.character != null && dc.character.Name == c.Name && c.Connection.RemoteEndPoint.Address.ToString() == dc.IPAddress);

                                    if (disconnectedcharcheck != null)
                                    {
                                        GameMain.Server.SetClientCharacter(c, disconnectedcharcheck.character);
                                        disconnectedcharcheck.TimeUntilKill = GameMain.NilMod.ReconnectTimeAllowed * 1.5f;
                                    }
                                }
                                */
                            }
                            else
                            {
                                lastEntityEventID = (UInt16)(c.UnreceivedEntityEventCount - 1);
                            }
                        }

                        if (NetIdUtils.IdMoreRecent(lastRecvChatMsgID, c.LastRecvChatMsgID) &&   //more recent than the last ID received by the client
                            !NetIdUtils.IdMoreRecent(lastRecvChatMsgID, c.LastChatMsgQueueID)) //NOT more recent than the latest existing ID
                        {
                            c.LastRecvChatMsgID = lastRecvChatMsgID;
                        }
                        else if (lastRecvChatMsgID != c.LastRecvChatMsgID && GameSettings.VerboseLogging)
                        {
                            DebugConsole.ThrowError(
                                "Invalid lastRecvChatMsgID  " + lastRecvChatMsgID +
                                " (previous: " + c.LastChatMsgQueueID + ", latest: " + c.LastChatMsgQueueID + ")");
                        }

                        if (NetIdUtils.IdMoreRecent(lastRecvEntityEventID, c.LastRecvEntityEventID) &&
                            !NetIdUtils.IdMoreRecent(lastRecvEntityEventID, lastEntityEventID))
                        {
                            c.LastRecvEntityEventID = lastRecvEntityEventID;
                        }
                        else if (lastRecvEntityEventID != c.LastRecvEntityEventID && GameSettings.VerboseLogging)
                        {
                            DebugConsole.ThrowError(
                                "Invalid lastRecvEntityEventID  " + lastRecvEntityEventID +
                                " (previous: " + c.LastRecvEntityEventID + ", latest: " + lastEntityEventID + ")");
                        }
                        break;
                    case ClientNetObject.CHAT_MESSAGE:
                        ChatMessage.ServerRead(inc, c);
                        break;
                    case ClientNetObject.CHARACTER_INPUT:
                        if (c.Character != null)
                        {
                            c.Character.ServerRead(objHeader, inc, c);
                        }
                        break;
                    case ClientNetObject.ENTITY_STATE:
                        entityEventManager.Read(inc, c);
                        break;
                    case ClientNetObject.VOTE:
                        Voting.ServerRead(inc, c);
                        break;
                    default:
                        return;
                }

                //don't read further messages if the client has been disconnected (kicked due to spam for example)
                if (!connectedClients.Contains(c)) break;
            }
        }

        private void ClientReadServerCommand(NetIncomingMessage inc)
        {
            Client sender = ConnectedClients.Find(x => x.Connection == inc.SenderConnection);
            if (sender == null)
            {
                inc.SenderConnection.Disconnect("You're not a connected client.");
                return;
            }

            ClientPermissions command = ClientPermissions.None;
            try
            {
                command = (ClientPermissions)inc.ReadByte();
            }

            catch
            {
                return;
            }

            if (!sender.HasPermission(command))
            {
                Log("Client \"" + sender.Name + "\" sent a server command \"" + command + "\". Permission denied.", ServerLog.MessageType.ServerMessage);
                return;
            }

            switch (command)
            {
                case ClientPermissions.Kick:
                    string kickedName = inc.ReadString().ToLowerInvariant();
                    string kickReason = inc.ReadString();
                    var kickedClient = connectedClients.Find(cl => cl != sender && cl.Name.ToLowerInvariant() == kickedName);
                    if (kickedClient != null && !kickedClient.KickImmunity)
                    {
                        Log("Client \"" + sender.Name + "\" kicked \"" + kickedClient.Name + "\".", ServerLog.MessageType.ServerMessage);
                        KickClient(kickedClient, string.IsNullOrEmpty(kickReason) ? "Kicked by " + sender.Name : kickReason, GameMain.NilMod.AdminKickStateNameTimer, GameMain.NilMod.AdminKickDenyRejoinTimer);
                    }
                    break;
                case ClientPermissions.Ban:
                    string bannedName = inc.ReadString().ToLowerInvariant();
                    string banReason = inc.ReadString();
                    bool range = inc.ReadBoolean();
                    double durationSeconds = inc.ReadDouble();

                    var bannedClient = connectedClients.Find(cl => cl != sender && cl.Name.ToLowerInvariant() == bannedName);
                    if (bannedClient != null && !bannedClient.BanImmunity)
                    {
                        Log("Client \"" + sender.Name + "\" banned \"" + bannedClient.Name + "\".", ServerLog.MessageType.ServerMessage);
                        if (durationSeconds > 0)
                        {
                            BanClient(bannedClient, string.IsNullOrEmpty(banReason) ? "Banned by " + sender.Name : banReason, range, TimeSpan.FromSeconds(durationSeconds));
                        }
                        else
                        {
                            BanClient(bannedClient, string.IsNullOrEmpty(banReason) ? "Banned by " + sender.Name : banReason, range);
                        }
                    }
                    break;
                case ClientPermissions.EndRound:
                    if (gameStarted)
                    {
                        Log("Client \"" + sender.Name + "\" ended the round.", ServerLog.MessageType.ServerMessage);
                        GameMain.NilMod.RoundEnded = true;
                        EndGame();
                    }
                    break;
                case ClientPermissions.SelectSub:
                    UInt16 subIndex = inc.ReadUInt16();
                    var subList = GameMain.NetLobbyScreen.GetSubList();
                    if (subIndex >= subList.Count)
                    {
                        DebugConsole.NewMessage("Client \"" + sender.Name + "\" attempted to select a sub, index out of bounds (" + subIndex + ")", Color.Red);
                    }
                    else
                    {
                        GameMain.NetLobbyScreen.SelectedSub = subList[subIndex];
                    }
                    break;
                case ClientPermissions.SelectMode:
                    UInt16 modeIndex = inc.ReadUInt16();
                    var modeList = GameMain.NetLobbyScreen.SelectedModeIndex = modeIndex;
                    break;
                case ClientPermissions.ManageCampaign:
                    MultiPlayerCampaign campaign = GameMain.GameSession.GameMode as MultiPlayerCampaign;
                    if (campaign != null)
                    {
                        campaign.ServerRead(inc, sender);
                    }
                    break;
                case ClientPermissions.ConsoleCommands:
                    string consoleCommand = inc.ReadString();
                    Vector2 clientCursorPos = new Vector2(inc.ReadSingle(), inc.ReadSingle());
                    DebugConsole.ExecuteClientCommand(sender, clientCursorPos, consoleCommand);
                    break;
            }

            inc.ReadPadBits();
        }


        private void ClientWrite(Client c)
        {
            //Send a packet
            if (c.NilModSyncResendTimer >= 0f && c.RequiresNilModSync)
            {
                GameMain.NilMod.ServerSyncWrite(c);
                c.NilModSyncResendTimer = NilMod.SyncResendInterval;
            }

            if (gameStarted && c.InGame)
            {
                if (GameMain.NilMod.UseAlternativeNetworking)
                {
                    ClientWriteIngamenew(c);
                }
                else
                {
                    ClientWriteIngame(c);
                }
            }
            else
            {
                /*
                //if 30 seconds have passed since the round started and the client isn't ingame yet,
                //kill the client's character
                if (gameStarted && c.Character != null && (DateTime.Now - roundStartTime).Seconds > (GameMain.NilMod.AllowReconnect ? Math.Max(GameMain.NilMod.ReconnectTimeAllowed, 30f) : 30.0f))
                {
                    if (GameMain.NilMod.DisconnectedCharacters.Count > 0)
                    {
                        if (GameMain.NilMod.DisconnectedCharacters.Find(dc => dc.character == c.Character) == null) c.Character.Kill(CauseOfDeath.Disconnected);
                    }
                    else
                    {
                        c.Character.Kill(CauseOfDeath.Disconnected);
                    }
                    //c.Character = null;
                }
                */

                //if 30 seconds have passed since the round started and the client isn't ingame yet,
                //kill the client's character
                if (gameStarted && c.Character != null && (DateTime.Now - roundStartTime).Seconds > 30f)
                {
                    if (KillDisconnectedTime > 30f)
                    {
                        c.Character.ClientDisconnected = true;
                        c.Character.OwnerClientIP = c.Connection.RemoteEndPoint.Address.ToString();
                        c.Character.OwnerClientName = c.Name;
                        c.Character.ClearInputs();
                        c.Character = null;
                    }
                    else
                    {
                        c.Character.ClientDisconnected = true;
                    }
                }
                ClientWriteLobby(c);

                MultiPlayerCampaign campaign = GameMain.GameSession?.GameMode as MultiPlayerCampaign;
                if (campaign != null && NetIdUtils.IdMoreRecent(campaign.LastSaveID, c.LastRecvCampaignSave))
                {
                    if (!fileSender.ActiveTransfers.Any(t => t.Connection == c.Connection && t.FileType == FileTransferType.CampaignSave))
                    {
                        fileSender.StartTransfer(c.Connection, FileTransferType.CampaignSave, GameMain.GameSession.SavePath);
                    }
                }
            }
        }

        /// <summary>
        /// Write info that the client needs when joining the server
        /// </summary>
        private void ClientWriteInitial(Client c, NetBuffer outmsg)
        {
            if (GameSettings.VerboseLogging)
            {
                DebugConsole.NewMessage("Sending initial lobby update", Color.Gray);
            }

            outmsg.Write(c.ID);

            var subList = GameMain.NetLobbyScreen.GetSubList();
            outmsg.Write((UInt16)subList.Count);
            for (int i = 0; i < subList.Count; i++)
            {
                outmsg.Write(subList[i].Name);
                outmsg.Write(subList[i].MD5Hash.ToString());
            }

            //Nilmod Sync joining client code
            //if(c.IsNilModClient)
            //{
            //    c.RequiresNilModSync = true;
            //    c.SyncResendTimer = 4f;
            //}

            outmsg.Write(GameStarted);
            outmsg.Write(AllowSpectating);

            WritePermissions(outmsg, c);
        }

        private void ClientWriteIngame(Client c)
        {
            //don't send position updates to characters who are still midround syncing
            //characters or items spawned mid-round don't necessarily exist at the client's end yet
            if (!c.NeedsMidRoundSync)
            {
                foreach (Character character in Character.CharacterList)
                {
                    if (!character.Enabled) continue;
                    if (c.Character != null &&
                        (Vector2.DistanceSquared(character.WorldPosition, c.Character.WorldPosition) >=
                        NetConfig.CharacterIgnoreDistanceSqr) && (!character.IsRemotePlayer && !c.Character.IsDead))
                    {
                        continue;
                    }
                    if (!c.PendingPositionUpdates.Contains(character)) c.PendingPositionUpdates.Enqueue(character);
                }

                foreach (Submarine sub in Submarine.Loaded)
                {
                    //if docked to a sub with a smaller ID, don't send an update
                    //  (= update is only sent for the docked sub that has the smallest ID, doesn't matter if it's the main sub or a shuttle)
                    if (sub.DockedTo.Any(s => s.ID < sub.ID)) continue;
                    if (!c.PendingPositionUpdates.Contains(sub)) c.PendingPositionUpdates.Enqueue(sub);
                }

                foreach (Item item in Item.ItemList)
                {
                    if (!item.NeedsPositionUpdate) continue;
                    if (!c.PendingPositionUpdates.Contains(item)) c.PendingPositionUpdates.Enqueue(item);
                }
            }

            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)ServerPacketHeader.UPDATE_INGAME);

            outmsg.Write((float)NetTime.Now);

            outmsg.Write((byte)ServerNetObject.SYNC_IDS);
            outmsg.Write(c.LastSentChatMsgID); //send this to client so they know which chat messages weren't received by the server
            outmsg.Write(c.LastSentEntityEventID);

            entityEventManager.Write(c, outmsg);

            WriteChatMessages(outmsg, c);

            //write as many position updates as the message can fit
            while (outmsg.LengthBytes < config.MaximumTransmissionUnit - 20 &&
                c.PendingPositionUpdates.Count > 0)
            {
                var entity = c.PendingPositionUpdates.Dequeue();
                if (entity == null || entity.Removed) continue;

                outmsg.Write((byte)ServerNetObject.ENTITY_POSITION);
                if (entity is Item)
                {
                    ((Item)entity).ServerWritePosition(outmsg, c);
                }
                else
                {
                    ((IServerSerializable)entity).ServerWrite(outmsg, c);
                }
                outmsg.WritePadBits();
            }

            outmsg.Write((byte)ServerNetObject.END_OF_MESSAGE);

            if (outmsg.LengthBytes > config.MaximumTransmissionUnit)
            {
                if (GameMain.NilMod.ShowPacketMTUErrors)
                {
                    DebugConsole.NewMessage("Maximum packet size exceeded (" + outmsg.LengthBytes + " > " + config.MaximumTransmissionUnit + ") in GameServer.ClientWriteLobby()", Color.Red);
                }
            }

            server.SendMessage(outmsg, c.Connection, NetDeliveryMethod.Unreliable);
        }

        private void ClientWriteLobby(Client c)
        {
            bool isInitialUpdate = false;

            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)ServerPacketHeader.UPDATE_LOBBY);

            outmsg.Write((byte)ServerNetObject.SYNC_IDS);

            if (NetIdUtils.IdMoreRecent(GameMain.NetLobbyScreen.LastUpdateID, c.LastRecvGeneralUpdate))
            {
                outmsg.Write(true);
                outmsg.WritePadBits();

                outmsg.Write(GameMain.NetLobbyScreen.LastUpdateID);
                outmsg.Write(GameMain.NetLobbyScreen.GetServerName());
                outmsg.Write(GameMain.NetLobbyScreen.ServerMessageText);

                outmsg.Write(c.LastRecvGeneralUpdate < 1);
                if (c.LastRecvGeneralUpdate < 1)
                {
                    isInitialUpdate = true;
                    ClientWriteInitial(c, outmsg);
                }
                outmsg.Write(GameMain.NetLobbyScreen.SelectedSub.Name);
                outmsg.Write(GameMain.NetLobbyScreen.SelectedSub.MD5Hash.ToString());
                outmsg.Write(GameMain.NetLobbyScreen.UsingShuttle);
                outmsg.Write(GameMain.NetLobbyScreen.SelectedShuttle.Name);
                outmsg.Write(GameMain.NetLobbyScreen.SelectedShuttle.MD5Hash.ToString());

                outmsg.Write(Voting.AllowSubVoting);
                outmsg.Write(Voting.AllowModeVoting);

                outmsg.Write(AllowSpectating);

                outmsg.WriteRangedInteger(0, 2, (int)TraitorsEnabled);

                outmsg.WriteRangedInteger(0, MissionPrefab.MissionTypes.Count - 1, (GameMain.NetLobbyScreen.MissionTypeIndex));

                outmsg.Write((byte)GameMain.NetLobbyScreen.SelectedModeIndex);
                outmsg.Write(GameMain.NetLobbyScreen.LevelSeed);

                outmsg.Write(AutoRestart);
                if (autoRestart)
                {
                    outmsg.Write(AutoRestartTimer);
                }

                outmsg.Write((byte)connectedClients.Count);
                foreach (Client client in connectedClients)
                {
                    outmsg.Write(client.ID);
                    outmsg.Write(client.Name);
                    outmsg.Write(client.Character == null || !gameStarted ? (ushort)0 : client.Character.ID);
                }
            }
            else
            {
                outmsg.Write(false);
                outmsg.WritePadBits();
            }

            var campaign = GameMain.GameSession?.GameMode as MultiPlayerCampaign;
            if (campaign != null)
            {
                if (NetIdUtils.IdMoreRecent(campaign.LastUpdateID, c.LastRecvCampaignUpdate))
                {
                    outmsg.Write(true);
                    outmsg.WritePadBits();
                    campaign.ServerWrite(outmsg, c);
                }
                else
                {
                    outmsg.Write(false);
                    outmsg.WritePadBits();
                }
            }
            else
            {
                outmsg.Write(false);
                outmsg.WritePadBits();
            }

            outmsg.Write(c.LastSentChatMsgID); //send this to client so they know which chat messages weren't received by the server

            WriteChatMessages(outmsg, c);

            outmsg.Write((byte)ServerNetObject.END_OF_MESSAGE);

            if (isInitialUpdate)
            {
                //the initial update may be very large if the host has a large number
                //of submarine files, so the message may have to be fragmented

                //unreliable messages don't play nicely with fragmenting, so we'll send the message reliably
                server.SendMessage(outmsg, c.Connection, NetDeliveryMethod.ReliableUnordered);

                //and assume the message was received, so we don't have to keep resending
                //these large initial messages until the client acknowledges receiving them
                c.LastRecvGeneralUpdate++;

                //Nilmod Rules code

                if (NilMod.NilModEventChatter.NilModRules.Count() > 0 && NilMod.NilModEventChatter.ChatModServerJoin)
                {
                    foreach (string message in NilMod.NilModEventChatter.NilModRules)
                    {
                        NilMod.NilModEventChatter.SendServerMessage(message, c);
                    }

                }

                SendVoteStatus(new List<Client>() { c });
            }
            else
            {
                if (outmsg.LengthBytes > config.MaximumTransmissionUnit)
                {
                    if (GameMain.NilMod.ShowPacketMTUErrors)
                    {
                        DebugConsole.NewMessage("Maximum packet size exceeded (" + outmsg.LengthBytes + " > " + config.MaximumTransmissionUnit + ") in GameServer.ClientWriteLobby()", Color.Red);
                    }
                }

                server.SendMessage(outmsg, c.Connection, NetDeliveryMethod.Unreliable);
            }
        }

        private void WriteChatMessages(NetOutgoingMessage outmsg, Client c)
        {
            c.ChatMsgQueue.RemoveAll(cMsg => !NetIdUtils.IdMoreRecent(cMsg.NetStateID, c.LastRecvChatMsgID));
            for (int i = 0; i < c.ChatMsgQueue.Count && i < ChatMessage.MaxMessagesPerPacket; i++)
            {
                if (outmsg.LengthBytes + c.ChatMsgQueue[i].EstimateLengthBytesServer(c) > config.MaximumTransmissionUnit - 5)
                {
                    //not enough room in this packet
                    return;
                }
                c.ChatMsgQueue[i].ServerWrite(outmsg, c);
            }
        }

        public bool StartGame()
        {

            GameMain.NilMod.SubmarineVoters = null;
            Submarine selectedSub = null;
            Submarine selectedShuttle = GameMain.NetLobbyScreen.SelectedShuttle;
            bool usingShuttle = GameMain.NetLobbyScreen.UsingShuttle;

            if (Voting.AllowSubVoting)
            {
                selectedSub = Voting.HighestVoted<Submarine>(VoteType.Sub, connectedClients);

                if (selectedSub != null)
                {
                    //record the voters
                    GameMain.NilMod.SubmarineVoters = selectedSub + " Voted by:";
                    foreach (Client c in ConnectedClients)
                    {
                        if (c.GetVote<Submarine>(VoteType.Sub) == selectedSub)
                        {

                            GameMain.NilMod.SubmarineVoters += " " + c.Name + ",";
                        }
                    }

                    //remove the comma
                    GameMain.NilMod.SubmarineVoters = GameMain.NilMod.SubmarineVoters.Substring(0, GameMain.NilMod.SubmarineVoters.Length - 1);
                }

                if (GameMain.NilMod.SubmarineVoters != null)
                {
                    if (GameMain.NilMod.SubVotingAnnounce)
                    {
                        foreach (Client c in ConnectedClients)
                        {
                            if (GameMain.NilMod.SubmarineVoters.Length > 160)
                            {
                                NilMod.NilModEventChatter.SendServerMessage(GameMain.NilMod.SubmarineVoters.Substring(0, 157) + "...", c);
                            }
                            else
                            {
                                NilMod.NilModEventChatter.SendServerMessage(GameMain.NilMod.SubmarineVoters, c);
                            }

                        }
                    }
                    GameServer.LogToClientconsole(GameMain.NilMod.SubmarineVoters);
                    if (GameMain.NilMod.SubVotingConsoleLog)
                    {
                        DebugConsole.NewMessage(GameMain.NilMod.SubmarineVoters, Color.White);
                    }
                }

                if (selectedSub == null)
                {
                    if (GameMain.NilMod.SubVotingConsoleLog)
                    {
                        DebugConsole.NewMessage("No clients voted a submarine, choosing default submarine: " + GameMain.NetLobbyScreen.SelectedSub, Color.White);
                    }
                    selectedSub = GameMain.NetLobbyScreen.SelectedSub;
                }
            }
            else
            {
                selectedSub = GameMain.NetLobbyScreen.SelectedSub;
            }

            if (selectedSub == null)
            {
#if CLIENT
                GameMain.NetLobbyScreen.SubList.Flash();
#endif
                return false;
            }

            if (selectedShuttle == null)
            {
#if CLIENT
                GameMain.NetLobbyScreen.ShuttleList.Flash();
#endif
                return false;
            }

            GameModePreset selectedMode = Voting.HighestVoted<GameModePreset>(VoteType.Mode, connectedClients);
            if (selectedMode == null) selectedMode = GameMain.NetLobbyScreen.SelectedMode;

            if (selectedMode == null)
            {
#if CLIENT
                GameMain.NetLobbyScreen.ModeList.Flash();
#endif
                return false;
            }

            CoroutineManager.StartCoroutine(InitiateStartGame(selectedSub, selectedShuttle, usingShuttle, selectedMode), "InitiateStartGame");

            return true;
        }

        private IEnumerable<object> InitiateStartGame(Submarine selectedSub, Submarine selectedShuttle, bool usingShuttle, GameModePreset selectedMode)
        {
            initiatedStartGame = true;
            GameMain.NetLobbyScreen.StartButtonEnabled = false;

            if (connectedClients.Any())
            {
                NetOutgoingMessage msg = server.CreateMessage();
                msg.Write((byte)ServerPacketHeader.QUERY_STARTGAME);

                msg.Write(selectedSub.Name);
                msg.Write(selectedSub.MD5Hash.Hash);

                msg.Write(usingShuttle);
                msg.Write(selectedShuttle.Name);
                msg.Write(selectedShuttle.MD5Hash.Hash);

                connectedClients.ForEach(c => c.ReadyToStart = false);

                server.SendMessage(msg, connectedClients.Select(c => c.Connection).ToList(), NetDeliveryMethod.ReliableUnordered, 0);

                //give the clients a few seconds to request missing sub/shuttle files before starting the round
                float waitForResponseTimer = 5.0f;
                while (connectedClients.Any(c => !c.ReadyToStart) && waitForResponseTimer > 0.0f)
                {
                    waitForResponseTimer -= CoroutineManager.UnscaledDeltaTime;
                    yield return CoroutineStatus.Running;
                }

                if (fileSender.ActiveTransfers.Count > 0)
                {
#if CLIENT
                    var msgBox = new GUIMessageBox("", "Waiting for file transfers to finish before starting the round...", new string[] { "Start now" });
                    msgBox.Buttons[0].OnClicked += msgBox.Close;
#endif

                    float waitForTransfersTimer = 20.0f;
                    while (fileSender.ActiveTransfers.Count > 0 && waitForTransfersTimer > 0.0f)
                    {
                        waitForTransfersTimer -= CoroutineManager.UnscaledDeltaTime;

#if CLIENT
                        //message box close, break and start the round immediately
                        if (!GUIMessageBox.MessageBoxes.Contains(msgBox))
                        {
                            break;
                        }
#endif

                        yield return CoroutineStatus.Running;
                    }
                }
            }
#if CLIENT
            LoadingScreen.loadType = LoadType.Server;
#endif
            startGameCoroutine = GameMain.Instance.ShowLoading(StartGame(selectedSub, selectedShuttle, usingShuttle, selectedMode), false);

            yield return CoroutineStatus.Success;
        }

        private IEnumerable<object> StartGame(Submarine selectedSub, Submarine selectedShuttle, bool usingShuttle, GameModePreset selectedMode)
        {
            entityEventManager.Clear();

            GameMain.NetLobbyScreen.StartButtonEnabled = false;

#if CLIENT
            GUIMessageBox.CloseAll();
#endif

            roundStartSeed = DateTime.Now.Millisecond;
            Rand.SetSyncedSeed(roundStartSeed);

            int teamCount = 1;
            byte hostTeam = 1;

            //Saves the log into a file
            if (GameMain.NilMod.ClearLogRoundStart)
            {
                ServerLog.ClearLog();
            }

            //Reload the banlist on round starts
            BanList.load();

            LoadClientPermissions();

            MultiPlayerCampaign campaign = GameMain.NetLobbyScreen.SelectedMode == GameMain.GameSession?.GameMode.Preset ?
                GameMain.GameSession?.GameMode as MultiPlayerCampaign : null;

            //don't instantiate a new gamesession if we're playing a campaign
            if (campaign == null || GameMain.GameSession == null)
            {
                GameMain.GameSession = new GameSession(selectedSub, "", selectedMode, MissionPrefab.MissionTypes[GameMain.NetLobbyScreen.MissionTypeIndex]);
            }

            if (GameMain.GameSession.GameMode.Mission != null &&
                GameMain.GameSession.GameMode.Mission.AssignTeamIDs(connectedClients, out hostTeam))
            {
                teamCount = 2;
            }
            else
            {
                connectedClients.ForEach(c => c.TeamID = hostTeam);
            }

            //Initialize server defaults
            GameMain.NilMod.GameInitialize(false);

            if (campaign != null)
            {
#if CLIENT
                if (GameMain.GameSession?.CrewManager != null) GameMain.GameSession.CrewManager.Reset();
#endif
                GameMain.GameSession.StartRound(campaign.Map.SelectedConnection.Level, true, teamCount > 1);
            }
            else
            {
                GameMain.GameSession.StartRound(GameMain.NetLobbyScreen.LevelSeed, teamCount > 1);
            }

            if (GameMain.NilMod.SubVotingServerLog && GameMain.NilMod.SubmarineVoters != null)
            {
                GameServer.Log(GameMain.NilMod.SubmarineVoters, ServerLog.MessageType.ServerMessage);
            }

            GameServer.Log("Starting a new round...", ServerLog.MessageType.ServerMessage);
            GameServer.Log("Submarine: " + selectedSub.Name, ServerLog.MessageType.ServerMessage);
            GameServer.Log("Game mode: " + selectedMode.Name, ServerLog.MessageType.ServerMessage);
            GameServer.Log("Level seed: " + GameMain.NetLobbyScreen.LevelSeed, ServerLog.MessageType.ServerMessage);

            bool missionAllowRespawn = campaign == null &&
                (!(GameMain.GameSession.GameMode is MissionMode) ||
                ((MissionMode)GameMain.GameSession.GameMode).Mission.AllowRespawn);

            if (AllowRespawn && missionAllowRespawn) respawnManager = new RespawnManager(this, usingShuttle ? selectedShuttle : null);

            //assign jobs and spawnpoints separately for each team
            for (int teamID = 1; teamID <= teamCount; teamID++)
            {
                //find the clients in this team
                List<Client> teamClients = teamCount == 1 ? new List<Client>(connectedClients) : connectedClients.FindAll(c => c.TeamID == teamID);
                if (AllowSpectating)
                {
                    teamClients.RemoveAll(c => c.SpectateOnly);
                }

                if (!teamClients.Any() && teamID > 1) continue;

                AssignJobs(teamClients, teamID == hostTeam);

                List<CharacterInfo> characterInfos = new List<CharacterInfo>();
                foreach (Client client in teamClients)
                {
                    client.NeedsMidRoundSync = false;

                    client.PendingPositionUpdates.Clear();
                    client.EntityEventLastSent.Clear();
                    client.LastSentEntityEventID = 0;
                    client.LastRecvEntityEventID = 0;
                    client.UnreceivedEntityEventCount = 0;

                    if (client.CharacterInfo == null)
                    {
                        client.CharacterInfo = new CharacterInfo(Character.HumanConfigFile, client.Name);
                    }
                    characterInfos.Add(client.CharacterInfo);
                    client.CharacterInfo.Job = new Job(client.AssignedJob);
                }

                //host's character
                if (characterInfo != null && hostTeam == teamID)
                {
                    characterInfo.Job = new Job(GameMain.NetLobbyScreen.JobPreferences[0]);
                    characterInfos.Add(characterInfo);
                }

                WayPoint[] assignedWayPoints = WayPoint.SelectCrewSpawnPoints(characterInfos, Submarine.MainSubs[teamID - 1]);
                for (int i = 0; i < teamClients.Count; i++)
                {
                    Character spawnedCharacter = Character.Create(teamClients[i].CharacterInfo, assignedWayPoints[i].WorldPosition, true, false);
                    spawnedCharacter.AnimController.Frozen = true;
                    spawnedCharacter.TeamID = (byte)teamID;
                    spawnedCharacter.GiveJobItems(assignedWayPoints[i]);

                    if (teamClients[i].BypassSkillRequirements)
                    {
                        foreach (Skill skill in teamClients[i].CharacterInfo.Job.Skills)
                        {
                            skill.Level = 100;
                        }
                    }

                    //Spawn protection
                    if (GameMain.NilMod.PlayerSpawnProtectStart)
                    {
                        spawnedCharacter.SpawnProtectionHealth = GameMain.NilMod.PlayerSpawnProtectHealth;
                        spawnedCharacter.SpawnProtectionOxygen = GameMain.NilMod.PlayerSpawnProtectOxygen;
                        spawnedCharacter.SpawnProtectionPressure = GameMain.NilMod.PlayerSpawnProtectPressure;
                        spawnedCharacter.SpawnProtectionStun = GameMain.NilMod.PlayerSpawnProtectStun;
                        spawnedCharacter.SpawnRewireWaitTimer = GameMain.NilMod.PlayerSpawnRewireWaitTimer;
                    }

                    teamClients[i].Character = spawnedCharacter;
                    spawnedCharacter.OwnerClientIP = teamClients[i].Connection.RemoteEndPoint.Address.ToString();
                    spawnedCharacter.OwnerClientName = teamClients[i].Name;

#if CLIENT
                    GameSession.inGameInfo.UpdateClientCharacter(teamClients[i], spawnedCharacter, false);
#endif

#if CLIENT
                    GameMain.GameSession.CrewManager.AddCharacter(spawnedCharacter);
#endif
                }

#if CLIENT
                if (characterInfo != null && hostTeam == teamID)
                {
                    if (GameMain.NilMod.PlayYourselfName.Length > 0)
                    {
                        if (GameMain.NilMod.PlayYourselfName != "")
                        {
                            characterInfo.Name = GameMain.NilMod.PlayYourselfName;
                        }
                    }

                    myCharacter = Character.Create(characterInfo, assignedWayPoints[assignedWayPoints.Length - 1].WorldPosition, false, false);
                    myCharacter.TeamID = (byte)teamID;
                    myCharacter.GiveJobItems(assignedWayPoints.Last());

                    //Spawn protection
                    if (GameMain.NilMod.PlayerSpawnProtectStart)
                    {
                        myCharacter.SpawnProtectionHealth = GameMain.NilMod.PlayerSpawnProtectHealth;
                        myCharacter.SpawnProtectionOxygen = GameMain.NilMod.PlayerSpawnProtectOxygen;
                        myCharacter.SpawnProtectionPressure = GameMain.NilMod.PlayerSpawnProtectPressure;
                        myCharacter.SpawnProtectionStun = GameMain.NilMod.PlayerSpawnProtectStun;
                        myCharacter.SpawnRewireWaitTimer = GameMain.NilMod.PlayerSpawnRewireWaitTimer;
                    }

                    Character.Controlled = myCharacter;
                    Character.SpawnCharacter = myCharacter;

                    GameSession.inGameInfo.AddNoneClientCharacter(myCharacter, true);

                    GameMain.GameSession.CrewManager.AddCharacter(myCharacter);
                }
#endif
                if (teamID == 1)
                {
                    GameServer.Log("Spawning initial Crew: Coalition", ServerLog.MessageType.Spawns);

                    //Log the hosts character which is always team #1
                    if (Character.Controlled != null && Character.Controlled.TeamID == 1)
                    {
                        GameServer.Log("spawn: " + Character.Controlled.Name + " As " + Character.Controlled.Info.Job.Name + " As Host", ServerLog.MessageType.Spawns);
                    }
                }
                if (teamID == 2)
                {
                    GameServer.Log("Spawning initial Crew: Renegades", ServerLog.MessageType.Spawns);

                    //Log the hosts character which is always team #1
                    if (Character.Controlled != null && Character.Controlled.TeamID == 2)
                    {
                        GameServer.Log("spawn: " + Character.Controlled.Name + " As " + Character.Controlled.Info.Job.Name + " As Host", ServerLog.MessageType.Spawns);
                    }
                }

                //List the players for the given team
                foreach (Client client in teamClients)
                {
                    string spawnlogentry = "spawn: " + client.CharacterInfo.Name + " As " + client.CharacterInfo.Job.Name;
                    if (KarmaEnabled)
                    {
                        if (client.Karma < 1f)
                        {
                            spawnlogentry = spawnlogentry + " with " + Math.Round(client.Karma, 2) + "% Karma";
                        }
                    }
                    spawnlogentry = spawnlogentry + " On " + client.Connection.RemoteEndPoint.Address;
                    GameServer.Log(spawnlogentry, ServerLog.MessageType.Spawns);
                }

            }
            //Locks the wiring if its set to.
            if (!GameMain.NilMod.CanRewireMainSubs)
            {
                foreach (Item item in Item.ItemList)
                {
                    //lock all wires to prevent the players from messing up the electronics
                    var connectionPanel = item.GetComponent<ConnectionPanel>();
                    if (connectionPanel != null && (item.Submarine == Submarine.MainSubs[0] || ((Submarine.MainSubs.Count() > 1 && item.Submarine == Submarine.MainSubs[1]))))
                    {
                        foreach (Connection connection in connectionPanel.Connections)
                        {
                            Array.ForEach(connection.Wires, w => { if (w != null) w.Locked = true; });
                        }
                    }
                }
            }

            foreach (Submarine sub in Submarine.MainSubs)
            {
                if (sub == null) continue;

                List<PurchasedItem> spawnList = new List<PurchasedItem>();
                foreach (KeyValuePair<ItemPrefab, int> kvp in extraCargo)
                {
                    spawnList.Add(new PurchasedItem(kvp.Key, kvp.Value));
                }

                CargoManager.CreateItems(spawnList);
            }

            TraitorManager = null;
            if (TraitorsEnabled == YesNoMaybe.Yes ||
                (TraitorsEnabled == YesNoMaybe.Maybe && Rand.Range(0.0f, 1.0f) < 0.5f))
            {
                List<Character> characters = new List<Character>();
                foreach (Client client in ConnectedClients)
                {
                    if (client.Character != null)
                        characters.Add(client.Character);
                }

                //Count the 
                if (Character != null) characters.Add(Character);

                int max = TraitorUseRatio ? (int)Math.Round(characters.Count * TraitorRatio, 0) : 1;
                var traitorCount = Math.Max(1, TraitorsEnabled == YesNoMaybe.Maybe ? Rand.Int(max) : max);

                TraitorManager = new TraitorManager(this, traitorCount);

                if (TraitorManager.TraitorList.Count > 0)
                {
                    for (int i = 0; i < TraitorManager.TraitorList.Count; i++)
                    {
                        Log(TraitorManager.TraitorList[i].Character.Name + " is the traitor and the target is " + TraitorManager.TraitorList[i].TargetCharacter.Name, ServerLog.MessageType.ServerMessage);
                    }
                }
            }

            GameAnalyticsManager.AddDesignEvent("Traitors:" + (TraitorManager == null ? "Disabled" : "Enabled"));

            SendStartMessage(roundStartSeed, Submarine.MainSub, GameMain.GameSession.GameMode.Preset, connectedClients);

            yield return CoroutineStatus.Running;

            GameMain.GameScreen.Cam.TargetPos = Vector2.Zero;
            GameMain.GameScreen.Select();

            AddChatMessage("Press TAB to chat. Use \"r;\" to talk through the radio.", ChatMessageType.Server);

            GameMain.NetLobbyScreen.StartButtonEnabled = true;

            gameStarted = true;
            initiatedStartGame = false;

            roundStartTime = DateTime.Now;

            //Custom Nilmod Roundstart Messages for other players
            if (GameMain.NilMod.EnableEventChatterSystem)
            {
                foreach (Client receivingclient in ConnectedClients)
                {
                    NilMod.NilModEventChatter.RoundStartClientMessages(receivingclient);
                }

                NilMod.NilModEventChatter.SendHostMessages();
            }

#if CLIENT
            GameSession.inGameInfo.UpdateGameInfoGUIList();
#endif

            GameServer.Log("Debug: Round start complete.", ServerLog.MessageType.ServerMessage);

            yield return CoroutineStatus.Success;
        }

        private void SendStartMessage(int seed, Submarine selectedSub, GameModePreset selectedMode, List<Client> clients)
        {
            foreach (Client client in clients)
            {
                SendStartMessage(seed, selectedSub, selectedMode, client);
            }
        }

        private void SendStartMessage(int seed, Submarine selectedSub, GameModePreset selectedMode, Client client)
        {
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)ServerPacketHeader.STARTGAME);

            msg.Write(seed);

            msg.Write(GameMain.GameSession.Level.Seed);

            msg.Write((byte)GameMain.NetLobbyScreen.MissionTypeIndex);

            msg.Write(selectedSub.Name);
            msg.Write(selectedSub.MD5Hash.Hash);
            msg.Write(GameMain.NetLobbyScreen.UsingShuttle);
            msg.Write(GameMain.NetLobbyScreen.SelectedShuttle.Name);
            msg.Write(GameMain.NetLobbyScreen.SelectedShuttle.MD5Hash.Hash);

            msg.Write(selectedMode.Name);
            msg.Write((short)(GameMain.GameSession.GameMode?.Mission == null ?
                -1 : MissionPrefab.List.IndexOf(GameMain.GameSession.GameMode.Mission.Prefab)));

            MultiPlayerCampaign campaign = GameMain.GameSession?.GameMode as MultiPlayerCampaign;

            bool missionAllowRespawn = campaign == null &&
                (!(GameMain.GameSession.GameMode is MissionMode) ||
                ((MissionMode)GameMain.GameSession.GameMode).Mission.AllowRespawn);

            msg.Write(AllowRespawn && missionAllowRespawn);
            msg.Write(Submarine.MainSubs[1] != null); //loadSecondSub

            Traitor traitor = null;
            if (TraitorManager != null && TraitorManager.TraitorList.Count > 0)
                traitor = TraitorManager.TraitorList.Find(t => t.Character == client.Character);
            if (traitor != null)
            {
                msg.Write(true);
                msg.Write(traitor.TargetCharacter.Name);
            }
            else
            {
                msg.Write(false);
            }

            //monster spawn settings
            List<string> monsterNames = monsterEnabled.Keys.ToList();
            foreach (string s in monsterNames)
            {
                msg.Write(monsterEnabled[s]);
            }
            msg.WritePadBits();

            server.SendMessage(msg, client.Connection, NetDeliveryMethod.ReliableUnordered);
        }

        public void EndGame()
        {
            if (!gameStarted) return;

            string endMessage = "The round has ended." + '\n';

#if CLIENT
            ClearClickCommand();
            GameSession.inGameInfo.ResetGUIListData();
#endif

            if (TraitorManager != null)
            {
                endMessage += TraitorManager.GetEndMessage();
            }

            Mission mission = GameMain.GameSession.Mission;
            GameMain.GameSession.GameMode.End(endMessage);

            if (autoRestart)
            {
                AutoRestartTimer = AutoRestartInterval;
                //send a netlobby update to get the clients' autorestart timers up to date
                GameMain.NetLobbyScreen.LastUpdateID++;
            }

            //NilMod Logging changes - Allow logs to save and end + clear potentially at round starts, not at round ends, and thus change saving to that.
            //Also makes all chat after a previous round go into the end of the last rounds log file.
            if (!GameMain.NilMod.ClearLogRoundStart)
            {
                if (SaveServerLogs) log.Save();
            }

            Character.Controlled = null;

            GameMain.GameScreen.Cam.TargetPos = Vector2.Zero;
#if CLIENT
            myCharacter = null;
            Character.Spied = null;
            Character.LastControlled = null;
            GameMain.LightManager.LosEnabled = false;
#endif

            entityEventManager.Clear();
            foreach (Client c in connectedClients)
            {
                c.EntityEventLastSent.Clear();
                c.PendingPositionUpdates.Clear();
            }

#if DEBUG
            messageCount.Clear();
#endif

            respawnManager = null;
            gameStarted = false;

            if (connectedClients.Count > 0)
            {
                NetOutgoingMessage msg = server.CreateMessage();
                msg.Write((byte)ServerPacketHeader.ENDGAME);
                msg.Write(endMessage);
                msg.Write(mission != null && mission.Completed);
                if (server.ConnectionsCount > 0)
                {
                    server.SendMessage(msg, server.Connections, NetDeliveryMethod.ReliableOrdered, 0);
                }

                foreach (Client client in connectedClients)
                {
                    client.Character = null;
                    client.HasSpawned = false;
                    client.InGame = false;
                }
            }

            CoroutineManager.StartCoroutine(EndCinematic(), "EndCinematic");
            BanList.load();

            GameMain.NetLobbyScreen.RandomizeSettings();
        }

        public IEnumerable<object> EndCinematic()
        {
            float endPreviewLength = 10.0f;

            var cinematic = new TransitionCinematic(Submarine.MainSub, GameMain.GameScreen.Cam, endPreviewLength);

            do
            {
                yield return CoroutineStatus.Running;
            } while (cinematic.Running);

            Submarine.Unload();
            entityEventManager.Clear();

            GameMain.NetLobbyScreen.Select();

            yield return CoroutineStatus.Success;
        }

        public override void KickPlayer(string playerName, string reason, float Expiretime = 0f, float Rejointime = 0f)
        {
            playerName = playerName.ToLowerInvariant();

            Client client = connectedClients.Find(c =>
                c.Name.ToLowerInvariant() == playerName ||
                (c.Character != null && c.Character.Name.ToLowerInvariant() == playerName));

            KickClient(client, reason, Expiretime, Rejointime);
        }

        public void KickClient(NetConnection conn, string reason, float Expiretime = 0f, float Rejointime = 0f)
        {
            Client client = connectedClients.Find(c => c.Connection == conn);
            KickClient(client, reason, Expiretime, Rejointime);
        }

        public void KickClient(Client client, string reason, float Expiretime = 0f, float Rejointime = 0f)
        {
            if (client == null) return;

            string msg = "You have been kicked from the server.";
            if (!string.IsNullOrWhiteSpace(reason)) msg += "\nReason: " + reason;
            DisconnectKickClient(client, client.Name + " has been kicked from the server.", msg, Expiretime, Rejointime);
        }

        public override void BanPlayer(string playerName, string reason, bool range = false, TimeSpan? duration = null)
        {
            playerName = playerName.ToLowerInvariant();

            Client client = connectedClients.Find(c =>
                c.Name.ToLowerInvariant() == playerName ||
                (c.Character != null && c.Character.Name.ToLowerInvariant() == playerName));

            if (client == null)
            {
                DebugConsole.ThrowError("Client \"" + playerName + "\" not found.");
                return;
            }

            BanClient(client, reason, range, duration);
        }

        public void KickBannedClient(NetConnection conn, string reason)
        {
            Client client = connectedClients.Find(c => c.Connection == conn);
            if (client != null)
            {
                if (gameStarted && client.Character != null)
                {
                    client.Character.ClearInputs();
                    client.Character.Kill(CauseOfDeath.Disconnected, true);
                }
                client.Connection.Disconnect("You have been banned from the server\n" + reason);
                //conn.Disconnect("You have been banned from the server\n" + reason);
                SendChatMessage(client.Name + " has been banned from the server", ChatMessageType.Server);
            }
            else
            {
                conn.Disconnect("You have been banned from the server" + "\nReason: " + reason);
            }
        }

        public void KickVPNClient(NetConnection conn, string reason, string clname)
        {
            /*
            Client client = connectedClients.Find(c => c.Connection == conn);
            if (client != null)
            {
                if (gameStarted && client.Character != null)
                {
                    client.Character.ClearInputs();
                    client.Character.Kill(CauseOfDeath.Disconnected, true);
                }
                conn.Disconnect("You have been banned from the server\n" + reason);
                SendChatMessage(client.name + " has been VPN Blacklisted from the server", ChatMessageType.Server);
                
            }
            else
            {
                conn.Disconnect("You have been banned from the server" + "\nReason: " + reason);
                SendChatMessage(clname + " has been banned from the server", ChatMessageType.Server);
            }
            */

            Client client = connectedClients.Find(c => c.Connection == conn);
            if (client != null)
            {
                if (gameStarted && client.Character != null)
                {
                    client.Character.ClearInputs();
                    client.Character.Kill(CauseOfDeath.Disconnected, true);
                }
                GameServer.Log("VPN Blacklisted player: " + clname + " (" + client.Connection.RemoteEndPoint.Address.ToString() + ") attempted to join the server.", ServerLog.MessageType.Connection);
                DebugConsole.NewMessage("VPN Blacklisted player: " + clname + " (" + client.Connection.RemoteEndPoint.Address.ToString() + ") attempted to join the server.", Color.Red);
                if (GameMain.NilMod.BansInfoAddCustomString)
                {
                    client.Connection.Disconnect(reason + "\n\n" + GameMain.NilMod.BansInfoCustomtext);
                }
                else
                {
                    client.Connection.Disconnect(reason);
                }

                //conn.Disconnect("You have been banned from the server\n" + reason);
                SendChatMessage("VPN Blacklisted player: " + clname + " attempted to join the server.", ChatMessageType.Server);
#if CLIENT
                GameMain.NetLobbyScreen.RemovePlayer(client.Name);
#endif
                ConnectedClients.Remove(client);
            }
            else
            {
                conn.Disconnect("You have been banned from the server" + "\nReason: " + reason);
                DebugConsole.NewMessage("VPN Blacklisted player: " + clname + " (" + conn.RemoteEndPoint.Address.ToString() + ") attempted to join the server.", Color.Red);
                SendChatMessage("VPN Blacklisted player: " + clname + " attempted to join the server.", ChatMessageType.Server);
            }
        }

        public void BanClient(Client client, string reason, bool range = false, TimeSpan? duration = null)
        {
            if (client == null || client.BanImmunity) return;

            string msg = "You have been banned from the server.";
            if (!string.IsNullOrWhiteSpace(reason)) msg += "\nReason: " + reason;
            DisconnectKickClient(client, client.Name + " has been banned from the server.", msg);
            string ip = client.Connection.RemoteEndPoint.Address.ToString();
            if (range) { ip = banList.ToRange(ip); }
            banList.BanPlayer(client.Name, ip, reason, duration);
        }

        public void DisconnectClient(NetConnection senderConnection, string msg = "", string targetmsg = "")
        {
            Client client = connectedClients.Find(x => x.Connection == senderConnection);
            if (client == null) return;

            DisconnectClient(client, msg, targetmsg);
        }

        public void DisconnectKickClient(Client client, string msg = "", string targetmsg = "", float expiretime = 0f, float rejointime = 0f)
        {
            if (client == null) return;

            if (expiretime > 0f || rejointime > 0f)
            {
                KickedClient kickedclient = null;

                if (GameMain.NilMod.KickedClients.Count > 0)
                {
                    kickedclient = GameMain.NilMod.KickedClients.Find(kc => kc.IPAddress == client.Connection.RemoteEndPoint.Address.ToString());
                }

                if (kickedclient != null)
                {
                    if (kickedclient.RejoinTimer < rejointime) kickedclient.RejoinTimer = rejointime;
                    if (kickedclient.ExpireTimer < expiretime) kickedclient.ExpireTimer = expiretime;
                }
                else
                {
                    kickedclient = new KickedClient();
                    kickedclient.clientname = client.Name;
                    kickedclient.IPAddress = client.Connection.RemoteEndPoint.Address.ToString();
                    kickedclient.RejoinTimer = rejointime;
                    kickedclient.ExpireTimer = expiretime;
                    kickedclient.KickReason = targetmsg;
                    GameMain.NilMod.KickedClients.Add(kickedclient);
                }
            }

            if (gameStarted && client.Character != null)
            {
                client.Character.ClearInputs();
                client.Character.Kill(CauseOfDeath.Disconnected, true);
            }

            client.Character = null;
            client.HasSpawned = false;
            client.InGame = false;

            if (string.IsNullOrWhiteSpace(msg)) msg = client.Name + " has left the server";
            if (string.IsNullOrWhiteSpace(targetmsg)) targetmsg = "You have left the server";

            Log(msg, ServerLog.MessageType.ServerMessage);

            client.Connection.Disconnect(targetmsg);
            connectedClients.Remove(client);

#if CLIENT
            GameSession.inGameInfo.RemoveClient(client);
            GameMain.NetLobbyScreen.RemovePlayer(client.Name);
            Voting.UpdateVoteTexts(connectedClients, VoteType.Sub);
            Voting.UpdateVoteTexts(connectedClients, VoteType.Mode);
#endif

            UpdateVoteStatus();

            SendChatMessage(msg, ChatMessageType.Server);

            UpdateCrewFrame();

            refreshMasterTimer = DateTime.Now;
        }

        public void DisconnectClient(Client client, string msg = "", string targetmsg = "")
        {
            if (client == null) return;
            /*
            if (gameStarted && client.Character != null && GameMain.NilMod.AllowReconnect)
            {
                client.Character.ClearInputs();
                DisconnectedCharacter disconnectedchar = new DisconnectedCharacter();
                disconnectedchar.clientname = client.Name;
                disconnectedchar.IPAddress = client.Connection.RemoteEndPoint.Address.ToString();
                disconnectedchar.DisconnectStun = client.Character.Stun;
                disconnectedchar.character = client.Character;
                disconnectedchar.TimeUntilKill = GameMain.NilMod.ReconnectTimeAllowed;
                disconnectedchar.ClientSetCooldown = 1.0f;
                GameMain.NilMod.DisconnectedCharacters.Add(disconnectedchar);
            }
            else
            */
            if (gameStarted && client.Character != null)
            {
                client.Character.ClientDisconnected = true;
                client.Character.ClearInputs();
            }

            client.Character = null;
            client.InGame = false;

            if (string.IsNullOrWhiteSpace(msg)) msg = client.Name + " has left the server";
            if (string.IsNullOrWhiteSpace(targetmsg)) targetmsg = "You have left the server";

            Log(msg, ServerLog.MessageType.ServerMessage);

            client.Connection.Disconnect(targetmsg);
            connectedClients.Remove(client);

#if CLIENT
            GameMain.NetLobbyScreen.RemovePlayer(client.Name);
            Voting.UpdateVoteTexts(connectedClients, VoteType.Sub);
            Voting.UpdateVoteTexts(connectedClients, VoteType.Mode);
            GameSession.inGameInfo.RemoveClient(client);
#endif

            UpdateVoteStatus();

            SendChatMessage(msg, ChatMessageType.Server);

            UpdateCrewFrame();

            refreshMasterTimer = DateTime.Now;
        }

        private void UpdateCrewFrame()
        {
            foreach (Client c in connectedClients)
            {
                if (c.Character == null || !c.InGame) continue;
            }
        }

        public void SendChatMessage(string txt, Client recipient)
        {
            ChatMessage msg = ChatMessage.Create("", txt, ChatMessageType.Server, null);
            SendChatMessage(msg, recipient);
        }

        public void SendConsoleMessage(string txt, Client recipient)
        {
            ChatMessage msg = ChatMessage.Create("", txt, ChatMessageType.Console, null);
            SendChatMessage(msg, recipient);
        }

        public void SendChatMessage(ChatMessage msg, Client recipient)
        {
            msg.NetStateID = recipient.ChatMsgQueue.Count > 0 ?
                (ushort)(recipient.ChatMsgQueue.Last().NetStateID + 1) :
                (ushort)(recipient.LastRecvChatMsgID + 1);

            recipient.ChatMsgQueue.Add(msg);
            recipient.LastChatMsgQueueID = msg.NetStateID;
        }

        /// <summary>
        /// Add the message to the chatbox and pass it to all clients who can receive it
        /// </summary>
        public void SendChatMessage(string message, ChatMessageType? type = null, Client senderClient = null)
        {
            Boolean IsHostPM = false;
            Character senderCharacter = null;
            string senderName = "";

            string command = "";

            if (senderClient != null)
            {
                if (!GameMain.NilMod.AllowCyrillicText && Regex.IsMatch(message, @"\p{IsCyrillic}"))
                {
                    SendChatMessage(senderClient.Name + " has been kicked (Server does not permit cyrillic alphabet).", ChatMessageType.Server, null);
                    KickClient(senderClient, "Server does not allow for cyrillic alphabet", 0f, 60f);
                    return;
                }

                if (!GameMain.NilMod.AllowEnglishText && Regex.IsMatch(message, "^[a-zA-Z]*$"))
                {
                    SendChatMessage(senderClient.Name + " has been kicked (Server does not permit english alphabet).", ChatMessageType.Server, null);
                    KickClient(senderClient, "Server does not allow for cyrillic alphabet", 0f, 60f);
                    return;
                }
            }

            Client targetClient = null;

            if (type == null)
            {
                string tempStr;
                command = ChatMessage.GetChatMessageCommand(message, out tempStr);
                switch (command.ToLowerInvariant())
                {
                    case "r":
                    case "radio":
                        type = ChatMessageType.Radio;
                        break;
                    case "d":
                    case "dead":
                        type = ChatMessageType.Dead;
                        break;
                    //NilMod Help Commands
                    case "h":
                    case "help":
                        type = ChatMessageType.Help;
                        NilMod.NilModHelpCommands.ReadHelpRequest(tempStr, senderClient);
                        return;
                        //DebugConsole.NewMessage("Received 'Help' Request of help command: " + tempStr + " from " + (senderClient != null ? senderClient.name : "Host (You)"), Color.White);
                    case "a":
                    case "admin":
                        type = ChatMessageType.Admin;
                        break;
                    case "g":
                    case "global":
                        type = ChatMessageType.Global;
                        break;
                    case "b":
                    case "broadcast":
                        type = ChatMessageType.MessageBox;
                        break;
                    default:
                        if (command != "")
                        {
                            if (command.ToLowerInvariant() == name.ToLowerInvariant()
                                || command.ToLowerInvariant() == GameMain.NilMod.PlayYourselfName.ToLowerInvariant()
                                || (Character != null && command.ToLowerInvariant() == Character.Name))
                            {
                                //Don't send messages to yourself
                                if (senderClient == null) return;
                                //a private message to the host
                                IsHostPM = true;
                                type = ChatMessageType.Private;
                            }
                            else
                            {
                                targetClient = connectedClients.Find(c =>
                                    command == c.Name.ToLowerInvariant() ||
                                    (c.Character != null && (command == c.Character.Name.ToLowerInvariant()
                                    || Homoglyphs.Compare(command.ToLowerInvariant(), c.Name.ToLowerInvariant()))));

                                if (targetClient == null)
                                {
                                    if (senderClient != null)
                                    {
                                        var chatMsg = ChatMessage.Create(
                                            "", "Player \"" + command + "\" not found!",
                                            ChatMessageType.Error, null);

                                        chatMsg.NetStateID = senderClient.ChatMsgQueue.Count > 0 ?
                                            (ushort)(senderClient.ChatMsgQueue.Last().NetStateID + 1) :
                                            (ushort)(senderClient.LastRecvChatMsgID + 1);

                                        senderClient.ChatMsgQueue.Add(chatMsg);
                                        senderClient.LastChatMsgQueueID = chatMsg.NetStateID;
                                    }
                                    else
                                    {
                                        AddChatMessage("Player \"" + command + "\" not found!", ChatMessageType.Error);
                                    }

                                    return;
                                }
                            }

                            type = ChatMessageType.Private;
                        }
                        else
                        {
                            type = ChatMessageType.Default;
                        }
                        break;
                }

                message = tempStr;
            }

            if (gameStarted)
            {
                //msg sent by the server
                if (senderClient == null)
                {
                    //senderCharacter = myCharacter;
                    //senderName = myCharacter == null ? name : myCharacter.Name;
                    senderCharacter = Character.Controlled;
                    senderName = Character.Controlled == null ? name : Character.Controlled.Name;

                    if (type == ChatMessageType.Admin)
                    {
                        senderName = "[A]" + senderName;
                    }
                    else if (type == ChatMessageType.Global)
                    {
                        senderName = "[G]" + senderName;
                    }
                }
                else //msg sent by a client
                {
                    senderCharacter = senderClient.Character;
                    senderName = senderCharacter == null ? senderClient.Name : senderCharacter.Name;

                    //private and adminprivate messages
                    if (type == ChatMessageType.Private)
                    {
                        //sender has an alive character, sending private messages not allowed
                        if (!senderClient.AdminPrivateMessage && !senderClient.AllowInGamePM)
                        {
                            SendChatMessage(ChatMessage.Create(
                                null,
                                "You do not have permission to send ingame private messages",
                                ChatMessageType.Server,
                                null), senderClient);
                            return;
                        }
                    }
                    //Admin channel
                    else if (type == ChatMessageType.Admin)
                    {
                        if (!senderClient.AdminChannelSend)
                        {
                            SendChatMessage(ChatMessage.Create(
                                null,
                                "You do not have permission to send admin messages",
                                ChatMessageType.Server,
                                null), senderClient);
                            return;
                        }
                        senderName = "[A]" + senderName;
                    }
                    //Global channel
                    else if (type == ChatMessageType.Global)
                    {
                        if (!senderClient.GlobalChatSend)
                        {
                            SendChatMessage(ChatMessage.Create(
                                null,
                                "You do not have permission to send global messages",
                                ChatMessageType.Server,
                                null), senderClient);
                            return;
                        }
                        senderName = "[G]" + senderName;
                    }
                    //Broadcasting
                    else if (type == ChatMessageType.MessageBox)
                    {
                        if (!senderClient.CanBroadcast)
                        {
                            SendChatMessage(ChatMessage.Create(
                                null,
                                "You do not have permission to send broadcasts",
                                ChatMessageType.Server,
                                null), senderClient);
                            return;
                        }
                        senderName = "Admin Broadcast";
                    }
                    else if (senderCharacter == null || senderCharacter.IsDead)
                    {
                        //sender doesn't have an alive character -> only ChatMessageType.Dead allowed
                        type = ChatMessageType.Dead;
                    }
                }
            }
            else
            {
                //msg sent by the server
                if (senderClient == null)
                {
                    senderName = GameMain.Server.CharacterInfo != null ? GameMain.NilMod.PlayYourselfName : name;

                    if (type == ChatMessageType.Admin)
                    {
                        senderName = "[A]" + senderName;
                    }

                    if (type != ChatMessageType.Server
                        && type != ChatMessageType.Private
                        && type != ChatMessageType.Admin) type = ChatMessageType.Default;
                }
                else //msg sent by a client          
                {
                    //game not started -> clients can only send normal, private and admin chatmessages
                    if (type != ChatMessageType.Private && type != ChatMessageType.Admin) type = ChatMessageType.Default;
                    senderName = senderClient.Name;

                    if (type == ChatMessageType.Admin)
                    {
                        senderName = "[A]" + senderName;
                    }
                }
            }

            //check if the client is allowed to send the message
            WifiComponent senderRadio = null;
            switch (type)
            {
                case ChatMessageType.Radio:
                    if (senderCharacter == null) return;

                    //return if senderCharacter doesn't have a working radio
                    var radio = senderCharacter.Inventory.Items.FirstOrDefault(i => i != null && i.GetComponent<WifiComponent>() != null);
                    if (radio == null || !senderCharacter.HasEquippedItem(radio))
                    {
                        type = ChatMessageType.Default;
                    }

                    senderRadio = radio.GetComponent<WifiComponent>();
                    if (!senderRadio.CanTransmit())
                    {
                        type = ChatMessageType.Default;
                    }
                    break;
                case ChatMessageType.Dead:
                    //character still alive -> not allowed
                    if ((senderClient != null && senderCharacter != null && !senderCharacter.IsDead) && !senderClient.AccessDeathChatAlive)
                    {
                        return;
                    }
                    //Host has a character
                    if (senderClient == null && Character.Controlled != null && !Character.Controlled.IsDead && !GameMain.NilMod.ShowDeadChat) return;
                    break;
            }

            if (type == ChatMessageType.Server)
            {
                senderName = null;
                senderCharacter = null;
            }

            if(senderClient == null && type == ChatMessageType.Default && (Character.Controlled == null && GameMain.Server.CharacterInfo == null && GameMain.Server.Character == null))
            {
                type = ChatMessageType.Server;
                senderName = GameMain.Server.Name;
            }

            //check which clients can receive the message and apply distance effects
            foreach (Client client in ConnectedClients)
            {
                string modifiedMessage = message;
                string senderNameFinal = senderName;
                Character senderCharacterFinal = senderCharacter;
                ChatMessageType FinalType = (ChatMessageType)type;

                switch (type)
                {
                    case ChatMessageType.Default:
                    case ChatMessageType.Radio:
                        if (senderCharacter != null &&
                            client.Character != null && !client.Character.IsDead)
                        {
                            modifiedMessage = ApplyChatMsgDistanceEffects(message, (ChatMessageType)type, senderCharacter, client.Character);

                            //too far to hear the msg -> don't send
                            if (string.IsNullOrWhiteSpace(modifiedMessage)) continue;
                        }
                        break;
                    case ChatMessageType.Dead:
                        //character still alive -> don't send
                        if (client.Character != null && !client.Character.IsDead && !client.AccessDeathChatAlive) continue;
                        break;
                    case ChatMessageType.Private:
                        //private msg sent to someone else than this client -> don't send
                        if ((client != targetClient && client != senderClient)) continue;
                        break;
                    case ChatMessageType.Global:
                        //Client is not allowed to use global chat
                        if (!client.GlobalChatReceive) continue;
                        break;
                    case ChatMessageType.Admin:
                        if (!client.AdminChannelReceive) continue;
                        break;
                        //This should not be sending to anyone
                    case ChatMessageType.Help:
                        continue;
                }

                ChatMessage chatMsg;

                if (senderClient == client)
                {
                    if (type == ChatMessageType.MessageBox)
                    {
                        FinalType = ChatMessageType.Server;
                        chatMsg = ChatMessage.Create(
                        null,
                        "[B]" + modifiedMessage,
                        FinalType,
                        null);

                        SendChatMessage(chatMsg, client);
                        continue;
                    }
                    else if(type == ChatMessageType.Private)
                    {
                        string Hostname = (targetClient != null ? targetClient.Name : name);
                        if (GameMain.Server.CharacterInfo != null
                            && command.ToLowerInvariant() == GameMain.NilMod.PlayYourselfName.ToLowerInvariant()) Hostname = GameMain.NilMod.PlayYourselfName;
                        senderNameFinal = "to " + Hostname;
                    }
                }
                else if(targetClient == client)
                {
                    if (type == ChatMessageType.Private)
                    {
                        if (senderClient != null)
                        {
                            if (gameStarted && senderClient.AdminPrivateMessage && !client.AllowInGamePM)
                            {
                                FinalType = ChatMessageType.Admin;
                                senderNameFinal = "[ADMIN]";
                            }
                            else
                            {
                                senderNameFinal = "from " + senderName;
                            }
                        }
                    }
                }
                else if(type == ChatMessageType.MessageBox)
                {
                    if(client.CanBroadcast && senderClient != null)
                    {
                        modifiedMessage = "Broadcast from " + senderClient.Name + "\n\n" + modifiedMessage;
                    }
                    else if(client.CanBroadcast) modifiedMessage = "Host Broadcast\n\n" + modifiedMessage;
                    else modifiedMessage = "Admin Broadcast\n\n" + modifiedMessage;
                }

                //Convert the messages to valid types if the receiver is not using a modified client
                if (!client.IsNilModClient)
                {
                    if (FinalType == ChatMessageType.Admin) FinalType = ChatMessageType.Error;
                    switch (type)
                    {
                        case ChatMessageType.Global:
                            FinalType = ChatMessageType.Server;
                            senderCharacterFinal = null;
                            break;
                        case ChatMessageType.Admin:
                            FinalType = ChatMessageType.Error;
                            senderCharacterFinal = null;
                            break;
                        default:
                            break;
                    }
                }

                chatMsg = ChatMessage.Create(
                    senderNameFinal,
                    modifiedMessage,
                    FinalType,
                    senderCharacterFinal);

                SendChatMessage(chatMsg, client);
            }

            string myReceivedMessage = message;
            if (gameStarted && Character.Controlled != null && senderCharacter != null)
            {
                if (type == ChatMessageType.Dead && Character.Controlled != null && !Character.Controlled.IsDead && !GameMain.NilMod.ShowDeadChat) return;
                myReceivedMessage = ApplyChatMsgDistanceEffects(message, (ChatMessageType)type, senderCharacter, Character.Controlled);
            }

            if (!string.IsNullOrWhiteSpace(myReceivedMessage) &&
                (targetClient == null || senderClient == null))
            {
                string senderNameFinal = senderName;
                if (type == ChatMessageType.Private)
                {
                    if(senderClient != null) senderNameFinal = "from " + senderClient.Name;
                    else if(targetClient != null) senderNameFinal = "to " + targetClient.Name;
                }


                if(type == ChatMessageType.MessageBox)
                {
#if CLIENT
                    if(senderClient != null) new GUIMessageBox("Broadcast from " + senderClient.Name, myReceivedMessage);
                    else AddChatMessage("Broadcast:" + myReceivedMessage, ChatMessageType.Server, null, null);

#else
                    if(senderClient != null) AddChatMessage("Broadcast from " + senderClient.Name + ":" + myReceivedMessage, ChatMessageType.Server, null, null);
                    AddChatMessage(senderNameFinal + "\n\n" + myReceivedMessage, ChatMessageType.Server, null, null);
#endif
                    return;
                }
                else
                {
                    AddChatMessage(myReceivedMessage, (ChatMessageType)type, senderNameFinal, senderCharacter);
                }
            }
        }

        private string ApplyChatMsgDistanceEffects(string message, ChatMessageType type, Character sender, Character receiver)
        {
            if (sender == null) return "";

            switch (type)
            {
                case ChatMessageType.Default:
                    if (!receiver.IsDead)
                    {
                        return ChatMessage.ApplyDistanceEffect(receiver, sender, message, ChatMessage.SpeakRange, 3.0f);
                    }
                    break;
                case ChatMessageType.Radio:
                    if (!receiver.IsDead)
                    {
                        if (receiver.Inventory != null)
                        {
                            var receiverItem = receiver.Inventory.Items.FirstOrDefault(i => i?.GetComponent<WifiComponent>() != null);
                            //client doesn't have a radio -> don't send
                            if (receiverItem == null || !receiver.HasEquippedItem(receiverItem)) return "";

                            var senderItem = sender.Inventory.Items.FirstOrDefault(i => i?.GetComponent<WifiComponent>() != null);
                            if (senderItem == null || !sender.HasEquippedItem(senderItem)) return "";

                            var receiverRadio = receiverItem.GetComponent<WifiComponent>();
                            var senderRadio = senderItem.GetComponent<WifiComponent>();

                            if (!receiverRadio.CanReceive(senderRadio)) return "";

                            return ChatMessage.ApplyDistanceEffect(receiverItem, senderItem, message, senderRadio.Range);
                        }
                        else
                        {
                            return ChatMessage.ApplyDistanceEffect(receiver, sender, message, ChatMessage.SpeakRange, 3.0f);
                        }
                    }
                    break;
            }

            return message;
        }

        private void FileTransferChanged(FileSender.FileTransferOut transfer)
        {
            Client recipient = connectedClients.Find(c => c.Connection == transfer.Connection);
#if CLIENT
            UpdateFileTransferIndicator(recipient);
#endif
        }

        public void SendCancelTransferMsg(FileSender.FileTransferOut transfer)
        {
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)ServerPacketHeader.FILE_TRANSFER);
            msg.Write((byte)FileTransferMessageType.Cancel);
            msg.Write((byte)transfer.SequenceChannel);
            server.SendMessage(msg, transfer.Connection, NetDeliveryMethod.ReliableOrdered, transfer.SequenceChannel);
        }

        public void UpdateVoteStatus()
        {
            if (server.Connections.Count == 0 || connectedClients.Count == 0) return;

            GameMain.NetworkMember.EndVoteCount = GameMain.Server.ConnectedClients.Count(c => c.Character != null && c.GetVote<bool>(VoteType.EndRound));
            GameMain.NetworkMember.EndVoteMax = GameMain.Server.ConnectedClients.Count(c => c.Character != null);

            Client.UpdateKickVotes(connectedClients);

            var clientsToKick = connectedClients.FindAll(c => c.KickVoteCount >= connectedClients.Count * KickVoteRequiredRatio);
            foreach (Client c in clientsToKick)
            {
                SendChatMessage(c.Name + " has been kicked from the server.", ChatMessageType.Server, null);
                if (AutoBanTime > 0 && GameMain.NilMod.VoteKickDenyRejoinTimer < 1f)
                {
                    BanClient(c, "Kicked by vote (auto ban)", duration: TimeSpan.FromSeconds(AutoBanTime));
                }
                else
                {
                    KickClient(c, "Kicked by vote", GameMain.NilMod.VoteKickStateNameTimer, GameMain.NilMod.VoteKickDenyRejoinTimer);
                }
            }

            GameMain.NetLobbyScreen.LastUpdateID++;

            SendVoteStatus(connectedClients);

            if (Voting.AllowEndVoting && EndVoteMax > 0 &&
                ((float)EndVoteCount / (float)EndVoteMax) >= EndVoteRequiredRatio)
            {
                Log("Ending round by votes (" + EndVoteCount + "/" + (EndVoteMax - EndVoteCount) + ")", ServerLog.MessageType.ServerMessage);
                GameMain.NilMod.RoundEnded = true;

                //Custom Nilmod End Vote Messages for other players whom are spectating the round or playing.
                foreach (Client client in ConnectedClients)
                {
                    if (client.InGame)
                    {
                        if (NilMod.NilModEventChatter.NilVoteEnd.Count() > 0 && NilMod.NilModEventChatter.ChatVoteEnd)
                        {
                            foreach (string message in NilMod.NilModEventChatter.NilVoteEnd)
                            {
                                NilMod.NilModEventChatter.SendServerMessage(message, client);
                            }
                        }
                    }
                }

                EndGame();
            }
        }

        public void SendVoteStatus(List<Client> recipients)
        {
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)ServerPacketHeader.UPDATE_LOBBY);
            msg.Write((byte)ServerNetObject.VOTE);
            Voting.ServerWrite(msg);
            msg.Write((byte)ServerNetObject.END_OF_MESSAGE);

            server.SendMessage(msg, recipients.Select(c => c.Connection).ToList(), NetDeliveryMethod.ReliableUnordered, 0);
        }

        public void UpdateClientPermissions(Client client)
        {
            clientPermissions.RemoveAll(cp => cp.IP == client.Connection.RemoteEndPoint.Address.ToString());

            if (client.Permissions != ClientPermissions.None)
            {
                clientPermissions.Add(new SavedClientPermission(
                    client.Name,
                    client.Connection.RemoteEndPoint.Address.ToString(),
                    client.Permissions,
                    client.PermittedConsoleCommands));
            }

            var msg = server.CreateMessage();
            msg.Write((byte)ServerPacketHeader.PERMISSIONS);
            WritePermissions(msg, client);

            server.SendMessage(msg, client.Connection, NetDeliveryMethod.ReliableUnordered);

            SaveClientPermissions();
        }

        private void WritePermissions(NetBuffer msg, Client client)
        {
            msg.Write((byte)client.Permissions);
            if (client.Permissions.HasFlag(ClientPermissions.ConsoleCommands))
            {
                msg.Write((UInt16)client.PermittedConsoleCommands.Sum(c => c.names.Length));
                foreach (DebugConsole.Command command in client.PermittedConsoleCommands)
                {
                    foreach (string commandName in command.names)
                    {
                        msg.Write(commandName);
                    }
                }
            }
        }

        public void SetClientCharacter(Client client, Character newCharacter)
        {
            if (client == null) return;
            Character oldcharacter = null;

            //the client's previous character is no longer a remote player
            if (client.Character != null)
            {
                client.Character.IsRemotePlayer = false;
                oldcharacter = client.Character;
                client.Character.OwnerClientIP = null;
                client.Character.OwnerClientName = null;
                client.Character.ClientDisconnected = false;
                client.Character.KillDisconnectedTimer = 0.0f;
            }
            if (newCharacter == null)
            {
                if (client.Character != null) //removing control of the current character
                {
                    CreateEntityEvent(client.Character, new object[] { NetEntityEvent.Type.Control, null });
                    client.Character = null;
#if CLIENT
                    GameSession.inGameInfo.UpdateClientCharacter(client, client.Character, true);
#endif
                }
            }
            else //taking control of a new character
            {
                newCharacter.ClientDisconnected = false;
                newCharacter.KillDisconnectedTimer = 0.0f;
                newCharacter.ResetNetState();
                if (client.Character != null)
                {
                    newCharacter.LastNetworkUpdateID = client.Character.LastNetworkUpdateID;
                }

                newCharacter.OwnerClientIP = client.Connection.RemoteEndPoint.Address.ToString();
                newCharacter.OwnerClientName = client.Name;
                newCharacter.IsRemotePlayer = true;
                newCharacter.Enabled = true;
                client.Character = newCharacter;
                CreateEntityEvent(newCharacter, new object[] { NetEntityEvent.Type.Control, client });
#if CLIENT
                GameSession.inGameInfo.UpdateClientCharacter(client, client.Character, true);
#endif
            }

            if (oldcharacter != null && oldcharacter != client.Character)
            {
                if (oldcharacter.AIController != null)
                {
                    oldcharacter.ResetNetState();
                    oldcharacter.AIController.Enabled = true;
                    oldcharacter.Enabled = true;

                    CoroutineManager.StartCoroutine(FinalizeSetclientCharacter(oldcharacter), "finalizesetclientcharacter");
                }
                else
                {
                    oldcharacter.Kill(CauseOfDeath.Disconnected);
                    oldcharacter.Health -= 99999f;
                    oldcharacter.Oxygen -= 99999f;
                }
            }
        }

        private IEnumerable<object> FinalizeSetclientCharacter(Character ResetChar)
        {
            float X = 0f;
            while(X < 1f)
            {
                X += CoroutineManager.DeltaTime;
                yield return CoroutineStatus.Running;
            }

            if (ResetChar.AIController != null)
            {
                ResetChar.ResetNetState();
                ResetChar.AIController.Enabled = true;
                ResetChar.Enabled = true;
            }
            X = 0f;
            while (X < 2f)
            {
                X += CoroutineManager.DeltaTime;
                yield return CoroutineStatus.Running;
            }

            if (ResetChar.AIController != null)
            {
                ResetChar.ResetNetState();
                ResetChar.AIController.Enabled = true;
                ResetChar.Enabled = true;
            }

            yield return CoroutineStatus.Success;
        }

        private void UpdateCharacterInfo(NetIncomingMessage message, Client sender)
        {
            sender.SpectateOnly = message.ReadBoolean() && AllowSpectating;
            if (sender.SpectateOnly)
            {
                return;
            }

            Gender gender = Gender.Male;
            int headSpriteId = 0;
            try
            {
                gender = message.ReadBoolean() ? Gender.Male : Gender.Female;
                headSpriteId = message.ReadByte();
            }
            catch (Exception e)
            {
                gender = Gender.Male;
                headSpriteId = 0;

                DebugConsole.Log("Received invalid characterinfo from \"" + sender.Name + "\"! { " + e.Message + " }");
            }

            List<JobPrefab> jobPreferences = new List<JobPrefab>();
            try
            {
                int count = message.ReadByte();
                for (int i = 0; i < Math.Min(count, 3); i++)
                {
                    string jobName = message.ReadString();

                    JobPrefab jobPrefab = JobPrefab.List.Find(jp => jp.Name == jobName);
                    if (jobPrefab != null) jobPreferences.Add(jobPrefab);
                }
            }
            catch (Exception e)
            {
                DebugConsole.Log("Received invalid characterinfo from \"" + sender.Name + "\"! { " + e.Message + " }");
            }

            if(jobPreferences.Count == 0) DebugConsole.NewMessage("Nilmod warning - Client: " + sender.Name + " updated their character info with 0 job preferences, this should not be possible (bug/modified client?).", Color.Red);

            sender.CharacterInfo = new CharacterInfo(Character.HumanConfigFile, sender.Name, gender)
            {
                HeadSpriteId = headSpriteId
            };

            //if the client didn't provide job preferences, we'll use the preferences that are randomly assigned in the Client constructor 
            Debug.Assert(sender.JobPreferences.Count > 0);
            if (jobPreferences.Count > 0)
            {
                sender.JobPreferences = jobPreferences;
            }
        }

        public void AssignJobs(List<Client> Unassigned, bool assignHost)
        {
            List<Client> unassigned = GameMain.NilMod.RandomizeClientOrder(Unassigned.FindAll(c => !c.PrioritizeJob));
            List<Client> unassignedpreferred = GameMain.NilMod.RandomizeClientOrder(Unassigned.FindAll(c => c.PrioritizeJob));

            Dictionary<JobPrefab, int> assignedClientCount = new Dictionary<JobPrefab, int>();
            foreach (JobPrefab jp in JobPrefab.List)
            {
                assignedClientCount.Add(jp, 0);
            }

            int teamID = 1;
            if (unassignedpreferred.Count > 0) teamID = unassignedpreferred[0].TeamID;
            if (unassigned.Count > 0) teamID = unassigned[0].TeamID;

            if (assignHost)
            {
                if (characterInfo != null)
                {
                    assignedClientCount[GameMain.NetLobbyScreen.JobPreferences[0]] = 1;
                }
                else if (myCharacter?.Info?.Job != null && !myCharacter.IsDead)
                {
                    assignedClientCount[myCharacter.Info.Job.Prefab] = 1;
                }
            }
            else if (myCharacter?.Info?.Job != null && !myCharacter.IsDead && myCharacter.TeamID == teamID)
            {
                assignedClientCount[myCharacter.Info.Job.Prefab]++;
            }

            //count the clients who already have characters with an assigned job
            foreach (Client c in connectedClients)
            {
                if (c.TeamID != teamID || unassigned.Contains(c) || unassignedpreferred.Contains(c)) continue;
                if (c.Character?.Info?.Job != null && !c.Character.IsDead)
                {
                    assignedClientCount[c.Character.Info.Job.Prefab]++;
                }
            }

            //if any of the players has chosen a job that is Always Allowed, give them that job
            for (int i = unassignedpreferred.Count - 1; i >= 0; i--)
            {
                if (unassignedpreferred[i].JobPreferences.Count == 0) continue;
                if (!unassignedpreferred[i].JobPreferences[0].AllowAlways) continue;
                unassignedpreferred[i].AssignedJob = unassignedpreferred[i].JobPreferences[0];
                unassignedpreferred.RemoveAt(i);
            }
            for (int i = unassigned.Count - 1; i >= 0; i--)
            {
                if (unassigned[i].JobPreferences.Count == 0) continue;
                if (!unassigned[i].JobPreferences[0].AllowAlways) continue;
                unassigned[i].AssignedJob = unassigned[i].JobPreferences[0];
                unassigned.RemoveAt(i);
            }

            //go through the jobs whose MinNumber>0 (i.e. at least one crew member has to have the job)
            bool unassignedJobsFound = true;
            while (unassignedJobsFound && unassigned.Count > 0)
            {
                unassignedJobsFound = false;

                foreach (JobPrefab jobPrefab in JobPrefab.List)
                {
                    if (unassigned.Count == 0) break;
                    if (jobPrefab.MinNumber < 1 || assignedClientCount[jobPrefab] >= jobPrefab.MinNumber) continue;

                    //find the client that wants the job the most, or force it to random client if none of them want it
                    Client assignedClient;
                    assignedClient = FindClientWithJobPreferencePriority(unassignedpreferred, jobPrefab);

                    if(assignedClient == null)
                    {
                        assignedClient = FindClientWithJobPreference(unassigned, jobPrefab, true);
                        assignedClient.AssignedJob = jobPrefab;
                        assignedClientCount[jobPrefab]++;
                        unassigned.Remove(assignedClient);
                    }
                    else
                    {
                        assignedClient.AssignedJob = jobPrefab;
                        assignedClientCount[jobPrefab]++;
                        unassignedpreferred.Remove(assignedClient);
                    }

                    //the job still needs more crew members, set unassignedJobsFound to true to keep the while loop running
                    if (assignedClientCount[jobPrefab] < jobPrefab.MinNumber) unassignedJobsFound = true;
                }
            }

            //NilMod reqnumber if There is a required player count get all clients who do not need round sync and are not spectators
            int playercount = connectedClients.FindAll(c => !c.NeedsMidRoundSync && !c.SpectateOnly).Count;

            //Add to the player count if the host is also playing as his own character
            if ((myCharacter?.Info?.Job != null && !myCharacter.IsDead && myCharacter.TeamID == teamID)
                || (myCharacter?.Info != null && !myCharacter.IsDead && myCharacter.TeamID == teamID))
            {
                playercount += 1;
            }

            //attempt to give the preferred clients a job they have in their job preferences 
            for (int i = unassignedpreferred.Count - 1; i >= 0; i--)
            {
                foreach (JobPrefab preferredJob in unassignedpreferred[i].JobPreferences)
                {
                    //the maximum number of players that can have this job hasn't been reached yet
                    //And add in the required players check too
                    //For each player who gets the job, deduct from the requirement check
                    //So there can only be an instance of this job for each 1 at and above requirement.
                    // -> assign it to the client
                    if (assignedClientCount[preferredJob] < preferredJob.MaxNumber && playercount - assignedClientCount[preferredJob] >= preferredJob.ReqNumber && unassignedpreferred[i].Karma >= preferredJob.MinKarma)
                    {
                        unassignedpreferred[i].AssignedJob = preferredJob;
                        assignedClientCount[preferredJob]++;
                        unassignedpreferred.RemoveAt(i);
                        break;
                    }
                }
            }
            //attempt to give the clients a job they have in their job preferences 
            for (int i = unassigned.Count - 1; i >= 0; i--)
            {
                foreach (JobPrefab preferredJob in unassigned[i].JobPreferences)
                {
                    //the maximum number of players that can have this job hasn't been reached yet
                    //And add in the required players check too
                    //For each player who gets the job, deduct from the requirement check
                    //So there can only be an instance of this job for each 1 at and above requirement.
                    // -> assign it to the client
                    if (assignedClientCount[preferredJob] < preferredJob.MaxNumber && playercount - assignedClientCount[preferredJob] >= preferredJob.ReqNumber && unassigned[i].Karma >= preferredJob.MinKarma)
                    {
                        unassigned[i].AssignedJob = preferredJob;
                        assignedClientCount[preferredJob]++;
                        unassigned.RemoveAt(i);
                        break;
                    }
                }
            }

            unassignedpreferred.AddRange(unassigned);
            unassigned = unassignedpreferred;

            //give random jobs to rest of the clients 
            foreach (Client c in unassigned)
            {
                //find all jobs that are still available 
                var remainingJobs = JobPrefab.List.FindAll(jp => assignedClientCount[jp] < jp.MaxNumber && playercount - assignedClientCount[jp] >= jp.ReqNumber && c.Karma >= jp.MinKarma);

                //all jobs taken, give a random job 
                if (remainingJobs.Count == 0)
                {
                    DebugConsole.ThrowError("Failed to assign a suitable job for \"" + c.Name + "\" (all jobs already have the maximum numbers of players). Assigning a random job...");
                    int jobIndex = Rand.Range(0, JobPrefab.List.Count);
                    int skips = 0;
                    while (c.Karma < JobPrefab.List[jobIndex].MinKarma)
                    {
                        jobIndex++;
                        skips++;
                        if (jobIndex >= JobPrefab.List.Count) jobIndex -= JobPrefab.List.Count;
                        if (skips >= JobPrefab.List.Count) break;
                    }
                    c.AssignedJob = JobPrefab.List[jobIndex];
                    assignedClientCount[c.AssignedJob]++;
                }
                else //some jobs still left, choose one of them by random 
                {
                    List<JobPrefab> Skillpreference = new List<JobPrefab>();
                    foreach(JobPrefab jp in remainingJobs)
                    {
                        int x = 0;
                        while (x < (jp.Totalskill / 10))
                        {
                            Skillpreference.Add(jp);
                            x++;
                        }
                    }
                    c.AssignedJob = Skillpreference[Rand.Range(0, Skillpreference.Count)];
                    assignedClientCount[c.AssignedJob]++;
                }
            }
        }

        private Client FindClientWithJobPreference(List<Client> clients, JobPrefab job, bool forceAssign = false)
        {
            int bestPreference = 0;
            Client preferredClient = null;
            foreach (Client c in clients)
            {
                if(c.JobPreferences == null || c.JobPreferences.Count == 0)
                {
                    DebugConsole.NewMessage("Nilmod Error - Client: " + c.Name + " has null or 0 job preferences (This should not be possible).", Color.Red);
                    continue;
                }
                if (c.Karma < job.MinKarma) continue;
                int index = c.JobPreferences.IndexOf(job);
                if (c.IgnoreJobMinimum && index != 0) continue;
                if (index == -1) index = 1000;

                if (preferredClient == null || index < bestPreference)
                {
                    bestPreference = index;
                    preferredClient = c;
                }
            }

            //none of the clients wants the job, assign it to random client
            if (forceAssign && preferredClient == null)
            {
                preferredClient = clients[Rand.Int(clients.Count)];
            }

            return preferredClient;
        }

        private Client FindClientWithJobPreferencePriority(List<Client> clients, JobPrefab job)
        {
            Client preferredClient = null;
            foreach (Client c in clients)
            {
                if (c.JobPreferences == null || c.JobPreferences.Count == 0)
                {
                    DebugConsole.NewMessage("Nilmod Error - Client: " + c.Name + " has null or 0 job preferences (This should not be possible).", Color.Red);
                    continue;
                }
                if (c.Karma < job.MinKarma) continue;
                int index = c.JobPreferences.IndexOf(job);

                if (index == 0)
                {
                    preferredClient = c;
                }
            }

            return preferredClient;
        }

        public static void Log(string line, ServerLog.MessageType messageType)
        {
            if (GameMain.Server == null || !GameMain.Server.SaveServerLogs) return;

            GameMain.Server.log.WriteLine(line, messageType);
        }

        public static void LogToClientconsole(string line)
        {
            if (GameMain.Server == null) return;

            foreach (Client c in GameMain.Server.ConnectedClients)
            {
                if(c.SendServerConsoleInfo) GameMain.Server.SendConsoleMessage(line, c);
            }
        }

        public override void Disconnect()
        {
            banList.Save();
            SaveSettings();

            if (registeredToMaster && restClient != null)
            {
                var request = new RestRequest("masterserver2.php", Method.GET);
                request.AddParameter("action", "removeserver");
                request.AddParameter("serverport", Port);

                restClient.Execute(request);
                restClient = null;
            }

            if (SaveServerLogs)
            {
                Log("Shutting down the server...", ServerLog.MessageType.ServerMessage);
                log.Save();
            }

            GameAnalyticsManager.AddDesignEvent("GameServer:ShutDown");
            server.Shutdown("The server has been shut down");
        }

        //Nilmod autorestart
        public void DisconnectRestart()
        {
            banList.Save();
            SaveSettings();

            if (registeredToMaster && restClient != null)
            {
                var request = new RestRequest("masterserver2.php", Method.GET);
                request.AddParameter("action", "removeserver");
                request.AddParameter("serverport", Port);

                restClient.Execute(request);
                restClient = null;
            }

            if (SaveServerLogs)
            {
                Log("Performing scheduled server restart...", ServerLog.MessageType.ServerMessage);
                log.Save();
            }

            GameAnalyticsManager.AddDesignEvent("GameServer:ShutDown");
            //server.Shutdown("The server has been shut down");
        }

        //NilMod
        public void GrantPower(int submarine)
        {
            foreach (Item item in Item.ItemList)
            {
                if (item.Submarine == Submarine.MainSubs[submarine])
                {
                    var powerContainer = item.GetComponent<PowerContainer>();
                    if (powerContainer != null)
                    {
                        powerContainer.Charge = Math.Min(powerContainer.Capacity * 0.9f, powerContainer.Charge);
                        item.CreateServerEvent(powerContainer);
                    }
                }
            }
        }

        //NilMod
        public void MoveSub(int sub, Vector2 Position)
        {
            //Submarine.MainSubs[sub].PhysicsBody.FarseerBody.IgnoreCollisionWith(Level.Loaded.ShaftBody);

            Steering movedsubSteering = null;

            //Deactivate all autopilot related tasks
            foreach (Item item in Item.ItemList)
            {
                //Ensure any item checked to be steering is only the submarine were teleporting
                if (item.Submarine != Submarine.MainSubs[sub]) continue;

                //Find, temp field and then set the steering if not null - This may not work well on subs with 2 bridges
                var steering = item.GetComponent<Steering>();
                if (steering != null)
                {
                    movedsubSteering = steering;
                    movedsubSteering.AutoPilot = false;
                    movedsubSteering.MaintainPos = false;
                }
            }

            //Teleport the submarine and prevent any collission or other issues, remove speed, etc
            Submarine.MainSubs[sub].SetPosition(Position);
            Submarine.MainSubs[sub].Velocity = Vector2.Zero;
            //Submarine.MainSubs[sub].PhysicsBody.FarseerBody.RestoreCollisionWith(Level.Loaded.ShaftBody);

            //activate Maintain position on all controllers.
            foreach (Item item in Item.ItemList)
            {
                //Ensure any item checked to be steering is only the submarine were teleporting
                if (item.Submarine != Submarine.MainSubs[sub]) continue;

                //Find, temp field and then set the steering if not null - This may not work well on subs with 2 bridges
                var steering = item.GetComponent<Steering>();
                if (steering != null)
                {
                    //Apparently autopilot should be turned on after maintain to enable it correctly.
                    movedsubSteering = steering;
                    movedsubSteering.MaintainPos = true;
                    movedsubSteering.AutoPilot = true;
                }
            }
        }

        public void RemoveCorpses(Boolean RemoveNetPlayers)
        {
            for (int i = Character.CharacterList.Count() - 1; i >= 0; i--)
            {
                if (Character.CharacterList[i].IsDead)
                {
                    if (RemoveNetPlayers)
                    {
                        Entity.Spawner.AddToRemoveQueue(Character.CharacterList[i]);
                    }
                    else if (!Character.CharacterList[i].IsRemotePlayer)
                    {
                        Entity.Spawner.AddToRemoveQueue(Character.CharacterList[i]);
                    }
                }
            }
        }

        //NilMod Networking
        private void ClientWriteIngamenew(Client c)
        {
            GameMain.NilMod.characterstoupdate = new List<Character>();
            GameMain.NilMod.subtoupdate = new List<Submarine>();
            GameMain.NilMod.itemtoupdate = new List<Item>();
            GameMain.NilMod.PacketNumber = 0;

            if (!c.NeedsMidRoundSync)
            {
                foreach (Character character in Character.CharacterList)
                {
                    if (!character.Enabled) continue;

                    if (c.Character != null &&
                        (Vector2.DistanceSquared(character.WorldPosition, c.Character.WorldPosition) >=
                        NetConfig.CharacterIgnoreDistanceSqr) && (!character.IsRemotePlayer && !c.Character.IsDead))
                    {
                        continue;
                    }

                    GameMain.NilMod.characterstoupdate.Add(character);
                }

                foreach (Submarine sub in Submarine.Loaded)
                {
                    //if docked to a sub with a smaller ID, don't send an update
                    //  (= update is only sent for the docked sub that has the smallest ID, doesn't matter if it's the main sub or a shuttle)
                    if (sub.DockedTo.Any(s => s.ID < sub.ID)) continue;

                    GameMain.NilMod.subtoupdate.Add(sub);
                }

                foreach (Item item in Item.ItemList)
                {
                    if (!item.NeedsPositionUpdate) continue;

                    GameMain.NilMod.itemtoupdate.Add(item);
                }
            }

            //Always send one packet
            SendClientPacket(c);

            //As long as we have items left for those clients SPAM MOAR PACKETS >: O ...or if no items actually send the usual first packet.
            while ((GameMain.NilMod.characterstoupdate.Count > 0 | GameMain.NilMod.subtoupdate.Count > 0 | GameMain.NilMod.itemtoupdate.Count > 0))
            {
                SendClientPacket(c);
            }

            //DebugConsole.NewMessage("Sent packets: " + GameMain.NilMod.PacketNumber, Color.White);
        }

        //NilMod
        private void SendClientPacket(Client c)
        {
            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)ServerPacketHeader.UPDATE_INGAME);

            //outmsg.Write((float)NetTime.Now + (GameMain.NilMod.PacketNumber * 0.0001));
            outmsg.Write((float)NetTime.Now);

            if (GameMain.NilMod.PacketNumber == 0)
            {
                outmsg.Write((byte)ServerNetObject.SYNC_IDS);
                outmsg.Write(c.LastSentChatMsgID); //send this to client so they know which chat messages weren't received by the server
                outmsg.Write(c.LastSentEntityEventID);

                entityEventManager.Write(c, outmsg);

                WriteChatMessages(outmsg, c);
            }
            GameMain.NilMod.PacketNumber++;

            //don't send position updates to characters who are still midround syncing
            //characters or items spawned mid-round don't necessarily exist at the client's end yet
            if (!c.NeedsMidRoundSync)
            {
                for (int i = GameMain.NilMod.characterstoupdate.Count - 1; i >= 0; i--)
                {
                    if (outmsg.LengthBytes >= NetPeerConfiguration.kDefaultMTU - 10) continue;
                    outmsg.Write((byte)ServerNetObject.ENTITY_POSITION);
                    GameMain.NilMod.characterstoupdate[i].ServerWrite(outmsg, c);
                    outmsg.WritePadBits();

                    GameMain.NilMod.characterstoupdate.RemoveAt(i);
                }

                for (int i = GameMain.NilMod.subtoupdate.Count - 1; i >= 0; i--)
                {
                    if (outmsg.LengthBytes >= NetPeerConfiguration.kDefaultMTU - 10) continue;
                    outmsg.Write((byte)ServerNetObject.ENTITY_POSITION);
                    GameMain.NilMod.subtoupdate[i].ServerWrite(outmsg, c);
                    outmsg.WritePadBits();

                    GameMain.NilMod.subtoupdate.RemoveAt(i);
                }

                for (int i = GameMain.NilMod.itemtoupdate.Count - 1; i >= 0; i--)
                {
                    if (outmsg.LengthBytes >= NetPeerConfiguration.kDefaultMTU - 10) continue;
                    outmsg.Write((byte)ServerNetObject.ENTITY_POSITION);
                    GameMain.NilMod.itemtoupdate[i].ServerWritePosition(outmsg, c);
                    outmsg.WritePadBits();

                    GameMain.NilMod.itemtoupdate.RemoveAt(i);
                }
            }

            outmsg.Write((byte)ServerNetObject.END_OF_MESSAGE);

            //DebugConsole.NewMessage("Sending packet: " + GameMain.NilMod.PacketNumber + " with MTU Size: " + outmsg.LengthBytes, Color.White);

            server.SendMessage(outmsg, c.Connection, NetDeliveryMethod.Unreliable);
        }


#if CLIENT
        //NilMod GUI Menu Click Commands
        private void ClickCommandUpdate(float DeltaTime)
        {
            GameMain.NilMod.ClickCooldown -= DeltaTime;
            if (GameMain.NilMod.ClickCooldown <= 0f)
            {
                switch (GameMain.NilMod.ClickCommandType)
                {
                    case "spawncreature":
                        ClickCommandFrame.Visible = true;
                        ClickCommandDescription.Text = "SPAWNCREATURE - Spawning: " + GameMain.NilMod.ClickArgs[0].ToLowerInvariant() + " countleft: " + GameMain.NilMod.ClickArgs[2] + " - Left click to spawn creatures, Right click to cancel.";
                        if (PlayerInput.LeftButtonClicked())
                        {
                            if (Convert.ToInt16(GameMain.NilMod.ClickArgs[2]) > 0)
                            {
                                GameMain.NilMod.ClickArgs[2] = Convert.ToString(Convert.ToInt16(GameMain.NilMod.ClickArgs[2]) - 1);
                                GameMain.NilMod.ClickCooldown = NilMod.ClickCooldownPeriod;

                                GameMain.GameScreen.RunIngameCommand(GameMain.NilMod.ClickCommandType, new object[] { GameMain.NilMod.ClickArgs[0],
                                    GameMain.GameScreen.Cam.ScreenToWorld(PlayerInput.MousePosition).X.ToString(),
                                    GameMain.GameScreen.Cam.ScreenToWorld(PlayerInput.MousePosition).Y.ToString(),
                                    GameMain.NilMod.ClickArgs[2]});
                            }
                            if (Convert.ToInt16(GameMain.NilMod.ClickArgs[2]) == 0)
                            {
                                ClearClickCommand();
                            }
                        }
                        break;
                    case "setclientcharacter":
                        ClickCommandFrame.Visible = true;
                        ClickCommandDescription.Text = "SETCLIENTCHARACTER - " + GameMain.NilMod.ClickTargetClient.Name + " - Left Click close to a creatures center to have the client control it, right click to cancel.";
                        if (PlayerInput.LeftButtonClicked())
                        {
                            //Character to set control to if applicable
                            Character closestDistChar = null;
                            //Client to wipe assigned control of if applicable
                            Client client = null;
                            float closestDist = GameMain.NilMod.ClickFindSelectionDistance;
                            foreach (Character c in Character.CharacterList)
                            {
                                //Prioritize none-clients if not doing commands on them
                                if (c.IsRemotePlayer) continue;

                                float dist = Vector2.Distance(c.WorldPosition, GameMain.GameScreen.Cam.ScreenToWorld(PlayerInput.MousePosition));

                                if (dist < closestDist)
                                {
                                    closestDist = dist;
                                    closestDistChar = c;
                                }
                            }
                            //We have a valid target
                            if (closestDistChar != null)
                            {
                                GameMain.GameScreen.RunIngameCommand(GameMain.NilMod.ClickCommandType, new object[] { GameMain.NilMod.ClickTargetClient, closestDistChar });
                                ClearClickCommand();
                            }
                        }
                        break;
                    case "control":
                        ClickCommandFrame.Visible = true;
                        ClickCommandDescription.Text = "CONTROL - Left Click close to a creatures center to control it, Hold shift while clicking to control a players body, Hold ctrl when clicking to release a players control of a character, right click to cancel.";
                        if (PlayerInput.LeftButtonClicked())
                        {
                            //Character to set control to if applicable
                            Character closestDistChar = null;
                            //Client to wipe assigned control of if applicable
                            Client client = null;
                            float closestDist = GameMain.NilMod.ClickFindSelectionDistance;
                            foreach (Character c in Character.CharacterList)
                            {
                                //Prioritize none-clients if not doing commands on them
                                if (c.IsRemotePlayer
                                    && !PlayerInput.KeyDown(Microsoft.Xna.Framework.Input.Keys.LeftControl)
                                    && !PlayerInput.KeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift)) continue;

                                float dist = Vector2.Distance(c.WorldPosition, GameMain.GameScreen.Cam.ScreenToWorld(PlayerInput.MousePosition));

                                if (dist < closestDist)
                                {
                                    if (!c.IsRemotePlayer
                                        && (PlayerInput.KeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift)
                                        || PlayerInput.KeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift))) continue;

                                    closestDist = dist;
                                    closestDistChar = c;
                                }
                            }
                            //We have a valid target
                            if (closestDistChar != null)
                            {
                                if (PlayerInput.KeyDown(Microsoft.Xna.Framework.Input.Keys.LeftControl))
                                {
                                    client = ConnectedClients.Find(c => c.Character == closestDistChar);

                                    if(client != null)
                                    {
                                        GameMain.GameScreen.RunIngameCommand(GameMain.NilMod.ClickCommandType, new object[] { client, null });
                                        ClearClickCommand();
                                    }
                                }
                                else if (PlayerInput.KeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift))
                                {
                                    client = ConnectedClients.Find(c => c.Character == closestDistChar);

                                    GameMain.GameScreen.RunIngameCommand(GameMain.NilMod.ClickCommandType, new object[] { client, closestDistChar });
                                    ClearClickCommand();
                                }
                                else if(!closestDistChar.IsRemotePlayer)
                                {
                                    GameMain.GameScreen.RunIngameCommand(GameMain.NilMod.ClickCommandType, new object[] { null, closestDistChar });
                                    ClearClickCommand();
                                }
                            }
                        }
                        break;
                    case "shield":
                        ClickCommandFrame.Visible = true;
                        ClickCommandDescription.Text = "SHIELD - Left Click close to a creatures center to grant immunity, Hold control to revoke immunity, Hold shift while clicking to repeat, right click to cancel.";
                        if (PlayerInput.LeftButtonClicked())
                        {
                            //Character to shield
                            Character closestDistChar = null;
                            float closestDist = GameMain.NilMod.ClickFindSelectionDistance;
                            foreach (Character c in Character.CharacterList)
                            {
                                //Prioritize none-clients if not doing commands on them
                                if (c.IsDead) continue;

                                float dist = Vector2.Distance(c.WorldPosition, GameMain.GameScreen.Cam.ScreenToWorld(PlayerInput.MousePosition));

                                if (dist < closestDist)
                                {
                                    //Only target characters to be effected.
                                    if ((!c.Shielded && PlayerInput.KeyDown(Microsoft.Xna.Framework.Input.Keys.LeftControl))
                                        || (c.Shielded && !PlayerInput.KeyDown(Microsoft.Xna.Framework.Input.Keys.LeftControl))) continue;
                                    closestDist = dist;
                                    closestDistChar = c;
                                }
                            }
                            //We have a valid target
                            if (closestDistChar != null)
                            {
                                if (PlayerInput.KeyDown(Microsoft.Xna.Framework.Input.Keys.LeftControl))
                                {

                                    GameMain.GameScreen.RunIngameCommand(GameMain.NilMod.ClickCommandType, new object[] { closestDistChar, false });
                                }
                                else
                                {
                                    GameMain.GameScreen.RunIngameCommand(GameMain.NilMod.ClickCommandType, new object[] { closestDistChar, true });
                                }

                                if (!PlayerInput.KeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift))
                                {
                                    ClearClickCommand();
                                }
                                else
                                {
                                    GameMain.NilMod.ClickCooldown = NilMod.ClickCooldownPeriod;
                                }
                            }
                        }
                        break;
                    case "heal":
                        ClickCommandFrame.Visible = true;
                        ClickCommandDescription.Text = "HEAL - Left Click close to a creatures center to heal it, Hold shift while clicking to repeat, Hold ctrl when clicking to heal self, right click to cancel.";
                        if (PlayerInput.LeftButtonClicked())
                        {
                            Character closestDistChar = null;
                            float closestDist = GameMain.NilMod.ClickFindSelectionDistance;
                            foreach (Character c in Character.CharacterList)
                            {
                                if (!c.IsDead)
                                {
                                    float dist = Vector2.Distance(c.WorldPosition, GameMain.GameScreen.Cam.ScreenToWorld(PlayerInput.MousePosition));

                                    if (dist < closestDist)
                                    {
                                        closestDist = dist;
                                        closestDistChar = c;
                                    }
                                }
                            }
                            if (PlayerInput.KeyDown(Microsoft.Xna.Framework.Input.Keys.LeftControl) && Character.Controlled != null && !PlayerInput.KeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift))
                            {
                                GameMain.GameScreen.RunIngameCommand(GameMain.NilMod.ClickCommandType, new object[] { Character.Controlled });

                                //Character.Controlled.Heal();
                                ClearClickCommand();
                            }
                            else if (PlayerInput.KeyDown(Microsoft.Xna.Framework.Input.Keys.LeftControl) && Character.Controlled != null && PlayerInput.KeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift))
                            {
                                GameMain.GameScreen.RunIngameCommand(GameMain.NilMod.ClickCommandType, new object[] { Character.Controlled });

                                //Character.Controlled.Heal();
                            }
                            else if (closestDistChar != null)
                            {
                                GameMain.GameScreen.RunIngameCommand(GameMain.NilMod.ClickCommandType, new object[] { closestDistChar });

                                //closestDistChar.Heal();
                                if (!PlayerInput.KeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift))
                                {
                                    ClearClickCommand();
                                }
                                else
                                {
                                    GameMain.NilMod.ClickCooldown = NilMod.ClickCooldownPeriod;
                                }
                            }
                        }
                        break;
                    case "revive":
                        ClickCommandFrame.Visible = true;
                        ClickCommandDescription.Text = "REVIVE - Left Click close to a creatures center to revive it, Hold shift while clicking to repeat, Hold ctrl when clicking the button to revive self, if detached from body ctrl click corpse to revive+control, right click to cancel. - As a note for now IF REVIVING A PLAYER you will wish to open the console (F3) and type setclientcharacter CapitalizedClientName ; clientcharacter to give them the body back.";
                        if (PlayerInput.LeftButtonClicked())
                        {
                            Character closestDistChar = null;
                            float closestDist = GameMain.NilMod.ClickFindSelectionDistance;
                            foreach (Character c in Character.CharacterList)
                            {
                                if (c.IsDead)
                                {
                                    float dist = Vector2.Distance(c.WorldPosition, GameMain.GameScreen.Cam.ScreenToWorld(PlayerInput.MousePosition));

                                    if (dist < closestDist)
                                    {
                                        closestDist = dist;
                                        closestDistChar = c;
                                    }
                                }
                            }
                            if (closestDistChar != null)
                            {
                                if (!PlayerInput.KeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift))
                                {
                                    if (GameMain.Server.ConnectedClients.Find(c => c.Name == closestDistChar.Name) != null)
                                    {
                                        if (GameMain.Server != null)
                                        {
                                            Client MatchedClient = null;
                                            foreach (Client c in GameMain.Server.ConnectedClients)
                                            {
                                                //Don't even consider reviving a client if it is not ingame yet.
                                                if (!c.InGame || c.NeedsMidRoundSync) continue;

                                                //Check if this client just happens to be the same character.
                                                if (c.Character == closestDistChar)
                                                {
                                                    //It matched.
                                                    MatchedClient = c;
                                                }
                                                //Check if the client has a character
                                                else if (c.Character != null)
                                                {
                                                    //Check if this is the same named client, and if so, skip if they have a living character.
                                                    if (c.Name != closestDistChar.Name || c.Name == closestDistChar.Name && !c.Character.IsDead) continue;
                                                    //This name matches that of their client name.
                                                    MatchedClient = c;
                                                }
                                                //This client has no character, simply check the name
                                                else
                                                {
                                                    if (c.Name != closestDistChar.Name) continue;

                                                    MatchedClient = c;
                                                }

                                                if (MatchedClient != null)
                                                {
                                                    //They do not have a living character but they are the correct client - allow it.
                                                    GameMain.GameScreen.RunIngameCommand(GameMain.NilMod.ClickCommandType, new object[] { closestDistChar });
                                                    //closestDistChar.Revive(true);

                                                    //clients stop controlling the character when it dies, force control back
                                                    //GameMain.Server.SetClientCharacter(c, closestDistChar);
                                                    GameMain.GameScreen.RunIngameCommand("setclientcharacter", new object[] { c, closestDistChar });
                                                }
                                                break;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        closestDistChar.Revive(true);
                                    }

                                    ClearClickCommand();
                                }
                                else if (PlayerInput.KeyDown(Microsoft.Xna.Framework.Input.Keys.LeftControl) && !closestDistChar.IsRemotePlayer)
                                {
                                    Character.Controlled = closestDistChar;
                                    GameMain.GameScreen.RunIngameCommand(GameMain.NilMod.ClickCommandType, new object[] { Character.Controlled });
                                    GameMain.GameScreen.RunIngameCommand("heal", new object[] { Character.Controlled });
                                    //Character.Controlled.Revive(true);
                                    //Character.Controlled.Heal();
                                    ClearClickCommand();
                                }
                                else
                                {
                                    if (GameMain.Server.ConnectedClients.Find(c => c.Name == closestDistChar.Name) != null)
                                    {
                                        if (GameMain.Server != null)
                                        {
                                            Client MatchedClient = null;
                                            foreach (Client c in GameMain.Server.ConnectedClients)
                                            {
                                                //Don't even consider reviving a client if it is not ingame yet.
                                                if (!c.InGame || c.NeedsMidRoundSync) continue;

                                                //Check if this client just happens to be the same character.
                                                if (c.Character == closestDistChar)
                                                {
                                                    //It matched.
                                                    MatchedClient = c;
                                                }
                                                //Check if the client has a character
                                                else if (c.Character != null)
                                                {
                                                    //Check if this is the same named client, and if so, skip if they have a living character.
                                                    if (c.Name != closestDistChar.Name || c.Name == closestDistChar.Name && !c.Character.IsDead) continue;
                                                    //This name matches that of their client name.
                                                    MatchedClient = c;
                                                }
                                                //This client has no character, simply check the name
                                                else
                                                {
                                                    if (c.Name != closestDistChar.Name) continue;

                                                    MatchedClient = c;
                                                }

                                                if (MatchedClient != null)
                                                {
                                                    //They do not have a living character but they are the correct client - allow it.
                                                    GameMain.GameScreen.RunIngameCommand(GameMain.NilMod.ClickCommandType, new object[] { closestDistChar });
                                                    //closestDistChar.Revive(true);

                                                    //clients stop controlling the character when it dies, force control back
                                                    //GameMain.Server.SetClientCharacter(c, closestDistChar);
                                                    GameMain.GameScreen.RunIngameCommand("setclientcharacter", new object[] { c, closestDistChar });
                                                }
                                                break;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        GameMain.GameScreen.RunIngameCommand(GameMain.NilMod.ClickCommandType, new object[] { closestDistChar });
                                        //closestDistChar.Revive(true);
                                    }
                                    GameMain.NilMod.ClickCooldown = NilMod.ClickCooldownPeriod;
                                }
                            }
                        }
                        break;
                    case "kill":
                        ClickCommandFrame.Visible = true;
                        ClickCommandDescription.Text = "KILL CREATURE - Left Click close to a creatures center to instantaniously kill it, Hold shift while clicking to repeat, right click to cancel.";
                        if (PlayerInput.LeftButtonClicked())
                        {
                            Character closestDistChar = null;
                            float closestDist = GameMain.NilMod.ClickFindSelectionDistance;
                            foreach (Character c in Character.CharacterList)
                            {
                                if (!c.IsDead)
                                {
                                    float dist = Vector2.Distance(c.WorldPosition, GameMain.GameScreen.Cam.ScreenToWorld(PlayerInput.MousePosition));

                                    if (dist < closestDist)
                                    {
                                        closestDist = dist;
                                        closestDistChar = c;
                                    }
                                }
                            }
                            if (closestDistChar != null)
                            {
                                closestDistChar.Kill(CauseOfDeath.Disconnected, true);
                                if (!PlayerInput.KeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift))
                                {
                                    ClearClickCommand();
                                }
                                else
                                {
                                    GameMain.NilMod.ClickCooldown = NilMod.ClickCooldownPeriod;
                                }
                            }
                        }
                        break;
                    case "removecorpse":
                        ClickCommandFrame.Visible = true;
                        ClickCommandDescription.Text = "REMOVECORPSE - Left Click close to a creatures corpse to delete it, Hold shift while clicking to repeat, right click to cancel.";
                        if (PlayerInput.LeftButtonClicked())
                        {
                            Character closestDistChar = null;
                            float closestDist = GameMain.NilMod.ClickFindSelectionDistance;
                            foreach (Character c in Character.CharacterList)
                            {
                                if (c.IsDead)
                                {
                                    float dist = Vector2.Distance(c.WorldPosition, GameMain.GameScreen.Cam.ScreenToWorld(PlayerInput.MousePosition));

                                    if (dist < closestDist)
                                    {
                                        closestDist = dist;
                                        closestDistChar = c;
                                    }
                                }
                            }
                            if (closestDistChar != null)
                            {
                                //GameMain.NilMod.HideCharacter(closestDistChar);

                                GameMain.GameScreen.RunIngameCommand(GameMain.NilMod.ClickCommandType, new object[] { closestDistChar });
                                if (!PlayerInput.KeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift))
                                {
                                    ClearClickCommand();
                                }
                                else
                                {
                                    GameMain.NilMod.ClickCooldown = NilMod.ClickCooldownPeriod;
                                }
                            }
                        }
                        break;
                    case "teleportsub":
                        ClickCommandFrame.Visible = true;
                        ClickCommandDescription.Text = "TELEPORTSUB - Team " + GameMain.NilMod.ClickArgs[0] + "'s submarine - Teleports the chosen teams submarine, left click to teleport. Right click to cancel.";
                        if (PlayerInput.LeftButtonClicked())
                        {
                            int subtotp = -1;
                            if (Convert.ToInt16(GameMain.NilMod.ClickArgs[0]) <= Submarine.MainSubs.Count() - 1)
                            {
                                subtotp = Convert.ToInt16(GameMain.NilMod.ClickArgs[0]);
                            }
                            else
                            {
                                DebugConsole.NewMessage("MainSub ID Range is from 0 to " + (Submarine.MainSubs.Count() - 1), Color.Red);
                            }

                            //Not Null? Lets try it! XD
                            if (GameMain.Server != null)
                            {
                                if (subtotp >= 0)
                                {
                                    if (Submarine.MainSubs[subtotp] != null)
                                    {
                                        var cam = GameMain.GameScreen.Cam;
                                        //GameMain.Server.MoveSub(subtotp, cam.ScreenToWorld(PlayerInput.MousePosition));
                                        GameMain.GameScreen.RunIngameCommand(GameMain.NilMod.ClickCommandType,
                                            new object[] { subtotp.ToString(),
                                                cam.ScreenToWorld(PlayerInput.MousePosition).X.ToString(),
                                                cam.ScreenToWorld(PlayerInput.MousePosition).Y.ToString() });
                                    }
                                    else
                                    {
                                        DebugConsole.NewMessage("Cannot teleport submarine - Submarine ID: " + subtotp + " Is not loaded in the game (If not multiple submarines use 0 or leave blank)", Color.Red);
                                    }
                                }
                            }
                            else
                            {
                                DebugConsole.NewMessage("Cannot teleport submarine - The Server is not running.", Color.Red);
                            }
                            if (!PlayerInput.KeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift))
                            {
                                ClearClickCommand();
                            }
                            else
                            {
                                GameMain.NilMod.ClickCooldown = NilMod.ClickCooldownPeriod;
                            }
                        }
                        break;
                    case "relocate":
                        ClickCommandFrame.Visible = true;
                        if (GameMain.NilMod.ClickTargetCharacter != null)
                        {
                            ClickCommandDescription.Text = "RELOCATE - " + GameMain.NilMod.ClickTargetCharacter + " - Left Click to select target to teleport, Left click again to teleport target to new destination, hold shift to repeat (Does not keep last target), Ctrl+Left Click to relocate self, Ctrl+Shift works, Right click to cancel.";
                        }
                        else
                        {
                            ClickCommandDescription.Text = "RELOCATE - None Selected - Left Click to select target to teleport, Left click again to teleport target to new destination, hold shift to repeat (Does not keep last target), Ctrl+Left Click to relocate self, Ctrl+Shift works, Right click to cancel.";
                        }
                        if (PlayerInput.LeftButtonClicked())
                        {
                            if (PlayerInput.KeyDown(Microsoft.Xna.Framework.Input.Keys.LeftControl))
                            {
                                if (Character.Controlled != null)
                                {
                                    GameMain.NilMod.ClickTargetCharacter = null;

                                    //Character.Controlled.AnimController.CurrentHull = null;
                                    //Character.Controlled.Submarine = null;
                                    //Character.Controlled.AnimController.SetPosition(FarseerPhysics.ConvertUnits.ToSimUnits(GameMain.GameScreen.Cam.ScreenToWorld(PlayerInput.MousePosition)));
                                    //Character.Controlled.AnimController.FindHull(GameMain.GameScreen.Cam.ScreenToWorld(PlayerInput.MousePosition), true);

                                    GameMain.GameScreen.RunIngameCommand(GameMain.NilMod.ClickCommandType, new object[] { Character.Controlled, GameMain.GameScreen.Cam.ScreenToWorld(PlayerInput.MousePosition).X, GameMain.GameScreen.Cam.ScreenToWorld(PlayerInput.MousePosition).Y });

                                    if (!PlayerInput.KeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift))
                                    {
                                        ClearClickCommand();
                                    }
                                    else
                                    {
                                        GameMain.NilMod.ClickCooldown = NilMod.ClickCooldownPeriod;
                                    }
                                }
                            }
                            else if (GameMain.NilMod.ClickTargetCharacter == null)
                            {
                                Character closestDistChar = null;
                                float closestDist = GameMain.NilMod.ClickFindSelectionDistance;
                                foreach (Character c in Character.CharacterList)
                                {
                                    float dist = Vector2.Distance(c.WorldPosition, GameMain.GameScreen.Cam.ScreenToWorld(PlayerInput.MousePosition));

                                    if (dist < closestDist)
                                    {
                                        closestDist = dist;
                                        closestDistChar = c;
                                    }
                                }
                                GameMain.NilMod.ClickTargetCharacter = closestDistChar;
                            }
                            else
                            {
                                //GameMain.NilMod.RelocateTarget.AnimController.CurrentHull = null;
                                //GameMain.NilMod.RelocateTarget.Submarine = null;
                                //GameMain.NilMod.RelocateTarget.AnimController.SetPosition(FarseerPhysics.ConvertUnits.ToSimUnits(GameMain.GameScreen.Cam.ScreenToWorld(PlayerInput.MousePosition)));
                                //GameMain.NilMod.RelocateTarget.AnimController.FindHull(GameMain.GameScreen.Cam.ScreenToWorld(PlayerInput.MousePosition), true);

                                GameMain.GameScreen.RunIngameCommand(GameMain.NilMod.ClickCommandType,
                                    new object[] { GameMain.NilMod.ClickTargetCharacter,
                                    GameMain.GameScreen.Cam.ScreenToWorld(PlayerInput.MousePosition).X,
                                    GameMain.GameScreen.Cam.ScreenToWorld(PlayerInput.MousePosition).Y });

                                GameMain.NilMod.ClickTargetCharacter = null;

                                if (!PlayerInput.KeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift))
                                {
                                    ClearClickCommand();
                                }
                                else
                                {
                                    GameMain.NilMod.ClickCooldown = NilMod.ClickCooldownPeriod;
                                }

                            }

                        }
                        break;
                    /*
                case "handcuff":
                    ClickCommandFrame.Visible = true;
                    ClickCommandDescription.Text = "HANDCUFF - Left click to spawn and add handcuffs to the players hands dropping their tools - Right click to drop handcuffs if present in hands - shift to repeat - ctrl click to delete handcuffs from hands - right click to cancel.";
                    break;
                    */
                    case "freeze":
                        ClickCommandFrame.Visible = true;
                        ClickCommandDescription.Text = "FREEZE - Left click a player to freeze their movements - Left click again to unfreeze - hold only shift to repeat - hold ctrl shift and left click to freeze all - hold ctrl and left click to unfreeze everyone - Right click to cancel - Players may still talk if concious.";
                        if (PlayerInput.LeftButtonClicked())
                        {
                            Character closestDistChar = null;
                            float closestDist = GameMain.NilMod.ClickFindSelectionDistance;
                            foreach (Character c in Character.CharacterList)
                            {
                                if (!c.IsDead)
                                {
                                    float dist = Vector2.Distance(c.WorldPosition, GameMain.GameScreen.Cam.ScreenToWorld(PlayerInput.MousePosition));

                                    if (dist < closestDist)
                                    {
                                        closestDist = dist;
                                        closestDistChar = c;
                                    }
                                }
                            }
                            //Standard Left click
                            if (closestDistChar != null && !(PlayerInput.KeyDown(Microsoft.Xna.Framework.Input.Keys.LeftControl) && PlayerInput.KeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift)))
                            {
                                GameMain.GameScreen.RunIngameCommand(GameMain.NilMod.ClickCommandType, new object[] { closestDistChar });

                                if (!PlayerInput.KeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift))
                                {
                                    ClearClickCommand();
                                }
                                else
                                {
                                    GameMain.NilMod.ClickCooldown = NilMod.ClickCooldownPeriod;
                                }
                            }
                            //hold ctrl and left click to unfreeze everyone
                            else if ((PlayerInput.KeyDown(Microsoft.Xna.Framework.Input.Keys.LeftControl) && !PlayerInput.KeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift)))
                            {
                                for (int i = GameMain.NilMod.FrozenCharacters.Count() - 1; i >= 0; i--)
                                {
                                    GameMain.NilMod.FrozenCharacters.RemoveAt(i);
                                }
                            }
                            //hold ctrl shift and left click to freeze all
                            else if ((PlayerInput.KeyDown(Microsoft.Xna.Framework.Input.Keys.LeftControl) && PlayerInput.KeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift)))
                            {
                                foreach (Character character in Character.CharacterList)
                                {
                                    if (GameMain.NilMod.FrozenCharacters.Find(c => c == character) == null)
                                    {
                                        if (character.IsRemotePlayer)
                                        {
                                            if (ConnectedClients.Find(c => c.Character == closestDistChar) != null)
                                            {
                                                var chatMsg = ChatMessage.Create(
                                                "Server Message",
                                                ("You have been frozen by the server\n\nYou may still talk if able, but no longer perform any actions or movements."),
                                                (ChatMessageType)ChatMessageType.MessageBox,
                                                null);

                                                GameMain.Server.SendChatMessage(chatMsg, ConnectedClients.Find(c => c.Character == closestDistChar));
                                            }
                                        }
                                        GameMain.NilMod.FrozenCharacters.Add(character);
                                    }
                                }
                            }
                        }
                        break;
                    case "":
                    default:
                        break;
                }
            }
            //Nullify the active command if rightclicking
            if (PlayerInput.RightButtonClicked())
            {
                ClearClickCommand();
            }
        }

        public void ClearClickCommand()
        {
            GameMain.NilMod.ClickCommandType = "";
            GameMain.NilMod.ClickArgs = null;
            GameMain.NilMod.ActiveClickCommand = false;
            GameMain.NilMod.ClickCooldown = 0.5f;
            GameMain.NilMod.ClickTargetCharacter = null;
            ClickCommandFrame.Visible = false;
            ClickCommandDescription.Text = "";
        }
#endif
        private IEnumerable<object> PerformRestart()
        {
            float RestartTimer = 20f;
            float WarnFrequency = 2f;
            float WarnTimer = 0f;

            if (GameMain.Server == null) yield return CoroutineStatus.Success;
            if(GameMain.Server.AutoRestart) GameMain.Server.AutoRestartTimer = RestartTimer + 1f;

            for (int i = 0; i < GameMain.Server.ConnectedClients.Count; i++)
            {
                var chatMsg = ChatMessage.Create(
                null,
                "Server is now performing its scheduled autorestart, please wait - Clients will attempt to autoreconnect.",
                (ChatMessageType)ChatMessageType.Server,
                null);

                GameMain.Server.SendChatMessage(chatMsg, GameMain.Server.ConnectedClients[i]);
            }
            GameMain.Server.AddChatMessage("Server is now performing its scheduled autorestart, please wait - Clients should remain connected.", ChatMessageType.Server);

            while (RestartTimer >= 0f && (GameMain.Server.ConnectedClients.Count > 0 || GameMain.NetworkMember.CharacterInfo != null))
            {
                if (GameMain.Server == null) yield return CoroutineStatus.Success;

                WarnTimer += CoroutineManager.UnscaledDeltaTime;
                RestartTimer -= CoroutineManager.UnscaledDeltaTime;
                if (GameMain.Server.AutoRestart) GameMain.Server.AutoRestartTimer = RestartTimer + 1f;

                if (WarnTimer >= 0.5f && WarnTimer >= WarnFrequency)
                {
                    for (int i = 0; i < GameMain.Server.ConnectedClients.Count; i++)
                    {
                        var chatMsg = ChatMessage.Create(
                        null,
                        "Server is restarting in " + Math.Round(RestartTimer) + " seconds.",
                        (ChatMessageType)ChatMessageType.Server,
                        null);

                        GameMain.Server.SendChatMessage(chatMsg, GameMain.Server.ConnectedClients[i]);
                    }
                    GameMain.Server.AddChatMessage("Server is restarting in " + Math.Round(RestartTimer) + " seconds.", ChatMessageType.Server);
                    WarnTimer -= WarnFrequency;

                    if(GameStarted || initiatedStartGame)
                    {
                        for (int i = 0; i < GameMain.Server.ConnectedClients.Count; i++)
                        {
                            var chatMsg = ChatMessage.Create(
                            null,
                            "Server restart postponed due to manual round start.",
                            (ChatMessageType)ChatMessageType.Server,
                            null);

                            GameMain.Server.SendChatMessage(chatMsg, GameMain.Server.ConnectedClients[i]);
                        }
                        GameMain.Server.AddChatMessage("Server restart postponed due to manual round start.", ChatMessageType.Server);
                        yield return CoroutineStatus.Success;
                    }
                }

                yield return CoroutineStatus.Running;
            }
            
            GameMain.Instance.AutoRestartServer(name, Port, isPublic, password, config.EnableUPnP,maxPlayers, server, config);
            yield return CoroutineStatus.Success;
        }

        public void AddRestartClients(List<Client> previousclients, ushort PrevLobbyUpdate = 0)
        {
            ushort highhestchatmessageID = 0;

            for (int i = previousclients.Count() - 1; i >= 0; i--)
            {
                Client newClient = new Client(previousclients[i].Name, previousclients[i].ID);
                newClient.IsNilModClient = previousclients[i].IsNilModClient;
                newClient.RequiresNilModSync = previousclients[i].IsNilModClient;
                newClient.NilModSyncResendTimer = 4f;

                newClient.LastSentChatMsgID = previousclients[i].LastSentChatMsgID;
                newClient.LastRecvChatMsgID = previousclients[i].LastRecvChatMsgID;
                newClient.LastRecvGeneralUpdate = previousclients[i].LastRecvGeneralUpdate;
                newClient.LastRecvEntityEventID = previousclients[i].LastRecvEntityEventID;
                newClient.UnreceivedEntityEventCount = previousclients[i].UnreceivedEntityEventCount;
                newClient.NeedsMidRoundSync = false;

                newClient.CharacterInfo = previousclients[i].CharacterInfo;
                newClient.JobPreferences = previousclients[i].JobPreferences;
                newClient.SpectateOnly = previousclients[i].SpectateOnly;
                newClient.Karma = previousclients[i].Karma;

                newClient.Connection = previousclients[i].Connection;
                if (previousclients[i].LastRecvChatMsgID >= highhestchatmessageID) highhestchatmessageID = previousclients[i].LastRecvChatMsgID;

                SavedClientPermission savedPermissions = clientPermissions.Find(cp => cp.IP == newClient.Connection.RemoteEndPoint.Address.ToString());
                if (savedPermissions == null) savedPermissions = defaultpermission;

                newClient.OwnerSlot = savedPermissions.OwnerSlot;
                newClient.AdministratorSlot = savedPermissions.AdministratorSlot;
                newClient.TrustedSlot = savedPermissions.TrustedSlot;

                newClient.AllowInGamePM = savedPermissions.AllowInGamePM;
                newClient.GlobalChatSend = savedPermissions.GlobalChatSend;
                newClient.GlobalChatReceive = savedPermissions.GlobalChatReceive;
                newClient.KarmaImmunity = savedPermissions.KarmaImmunity;
                newClient.BypassSkillRequirements = savedPermissions.BypassSkillRequirements;
                newClient.PrioritizeJob = savedPermissions.PrioritizeJob;
                newClient.IgnoreJobMinimum = savedPermissions.IgnoreJobMinimum;
                newClient.KickImmunity = savedPermissions.KickImmunity;
                newClient.BanImmunity = savedPermissions.BanImmunity;

                newClient.HideJoin = savedPermissions.HideJoin;
                newClient.AccessDeathChatAlive = savedPermissions.AccessDeathChatAlive;
                newClient.AdminPrivateMessage = savedPermissions.AdminPrivateMessage;
                newClient.AdminChannelSend = savedPermissions.AdminChannelSend;
                newClient.AdminChannelReceive = savedPermissions.AdminChannelReceive;
                newClient.SendServerConsoleInfo = savedPermissions.SendServerConsoleInfo;
                newClient.CanBroadcast = savedPermissions.CanBroadcast;

                newClient.SetPermissions(savedPermissions.Permissions, savedPermissions.PermittedCommands);

                ConnectedClients.Add(newClient);

#if CLIENT
                GameSession.inGameInfo.AddClient(newClient);
                GameMain.NetLobbyScreen.AddPlayer(newClient.Name);
#endif
            }

            ChatMessage.LastID = highhestchatmessageID;

            CoroutineManager.StartCoroutine(SyncPlayerLobbyDelayed(PrevLobbyUpdate), "SyncPlayerLobbyDelayed");
        }

        private IEnumerable<object> SyncPlayerLobbyDelayed(ushort PrevLobbyUpdate)
        {
            float timer = 0f;

            while (timer < 1f)
            {
                timer += CoroutineManager.DeltaTime;
                yield return CoroutineStatus.Running;
            }
            GameMain.NetLobbyScreen.LastUpdateID = PrevLobbyUpdate;
            foreach (Client c in ConnectedClients)
            {
                //Resync the lobby
                c.LastRecvGeneralUpdate = 0;
                ClientWriteLobby(c);
            }
            yield return CoroutineStatus.Success;
        }
    }
}
