﻿using Barotrauma.Networking;
using FarseerPhysics.Dynamics;
using GameAnalyticsSDK.Net;
using Microsoft.Xna.Framework;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Xml.Linq;

namespace Barotrauma
{
    class GameMain
    {
        public static readonly Version Version = Assembly.GetEntryAssembly().GetName().Version;

        Stopwatch stopwatch;
        long prevTicks;

        public static World World;
        public static GameSettings Config;

        public static GameServer Server;
        public const GameClient Client = null;
        public static NetworkMember NetworkMember
        {
            get { return Server as NetworkMember; }
        }

        public static GameSession GameSession;

        public static GameMain Instance
        {
            get;
            private set;
        }

        //NilMod Class
        public static NilMod NilMod;

        //only screens the server implements
        public static GameScreen GameScreen;
        public static NetLobbyScreen NetLobbyScreen;

        //null screens because they are not implemented by the server,
        //but they're checked for all over the place
        //TODO: maybe clean up instead of having these constants
        public static readonly Screen MainMenuScreen = UnimplementedScreen.Instance;
        public static readonly Screen LobbyScreen = UnimplementedScreen.Instance;

        public static readonly Screen ServerListScreen = UnimplementedScreen.Instance;

        public static readonly Screen SubEditorScreen = UnimplementedScreen.Instance;
        public static readonly Screen CharacterEditorScreen = UnimplementedScreen.Instance;
        
        public static bool ShouldRun = true;

        public static ContentPackage SelectedPackage
        {
            get { return Config.SelectedContentPackage; }
        }

        public GameMain()
        {
            Instance = this;

            World = new World(new Vector2(0, -9.82f));
            FarseerPhysics.Settings.AllowSleep = true;
            FarseerPhysics.Settings.ContinuousPhysics = false;
            FarseerPhysics.Settings.VelocityIterations = 1;
            FarseerPhysics.Settings.PositionIterations = 1;

            Config = new GameSettings("config.xml");
            if (Config.WasGameUpdated)
            {
                UpdaterUtil.CleanOldFiles();
                Config.WasGameUpdated = false;
                Config.Save("config.xml");
            }

            if (GameSettings.SendUserStatistics)
            {
                GameAnalyticsManager.Init();
            }

            NilMod = new NilMod();
            NilMod.Load(false);

            NilMod.NilModVPNBanlist = new VPNBanlist();
            NilMod.NilModVPNBanlist.LoadVPNBans();

            GameScreen = new GameScreen();
        }

        public void Init()
        {
            MissionPrefab.Init();
            MapEntityPrefab.Init();
            LevelGenerationParams.LoadPresets();

            JobPrefab.LoadAll(SelectedPackage.GetFilesOfType(ContentType.Jobs));
            StructurePrefab.LoadAll(SelectedPackage.GetFilesOfType(ContentType.Structure));

            ItemPrefab.LoadAll(SelectedPackage.GetFilesOfType(ContentType.Item));

            GameModePreset.Init();

            LocationType.Init();

            Submarine.RefreshSavedSubs();

            Screen.SelectNull();

            NetLobbyScreen = new NetLobbyScreen();
        }

        public void StartServer()
        {
            if (GameMain.NilMod.OverrideServerSettings)
            {
                NetLobbyScreen.ServerName = GameMain.NilMod.ServerName;
                Server = new GameServer(GameMain.NilMod.ServerName,
                GameMain.NilMod.ServerPort,
                GameMain.NilMod.PublicServer,
                GameMain.NilMod.UseServerPassword ? "" : GameMain.NilMod.ServerPassword,
                GameMain.NilMod.UPNPForwarding,
                GameMain.NilMod.MaxPlayers);
            }
            else
            {
                XDocument doc = XMLExtensions.TryLoadXml(GameServer.SettingsFile);
                if (doc == null)
                {
                    DebugConsole.ThrowError("File \"" + GameServer.SettingsFile + "\" not found. Starting the server with default settings.");
                    Server = new GameServer("Server", 14242, false, "", false, 10);
                    return;
                }

            Server = new GameServer(
                doc.Root.GetAttributeString("name", "Server"),
                doc.Root.GetAttributeInt("port", 14242),
                doc.Root.GetAttributeBool("public", false),
                doc.Root.GetAttributeString("password", ""),
                doc.Root.GetAttributeBool("enableupnp", false),
                doc.Root.GetAttributeInt("maxplayers", 10));
            }
            NilMod.FetchExternalIP();
        }

        public void CloseServer()
        {
            Server.Disconnect();
            Server = null;
        }

        public void Run()
        {
            Hyper.ComponentModel.HyperTypeDescriptionProvider.Add(typeof(Character));
            Hyper.ComponentModel.HyperTypeDescriptionProvider.Add(typeof(Item));
            Hyper.ComponentModel.HyperTypeDescriptionProvider.Add(typeof(Items.Components.ItemComponent));
            Hyper.ComponentModel.HyperTypeDescriptionProvider.Add(typeof(Hull));

            Init();
            StartServer();

            DefaultServerStartup();

            Timing.Accumulator = 0.0;

            double frequency = (double)Stopwatch.Frequency;
            if (frequency <= 1500)
            {
                DebugConsole.NewMessage("WARNING: Stopwatch frequency under 1500 ticks per second. Expect significant syncing accuracy issues.", Color.Yellow);
            }

            int MaximumSamples = 5;
            Queue<double> sampleBuffer = new Queue<double>();
            double CurrentUpdatesPerSecond = 0;
            double AverageUpdatesPerSecond = 0;

            stopwatch = Stopwatch.StartNew();
            prevTicks = stopwatch.ElapsedTicks;
            while (ShouldRun)
            {
                long currTicks = stopwatch.ElapsedTicks;
                //Necessary for some timing
                //Timing.TotalTime = stopwatch.ElapsedMilliseconds / 1000;
                //Timing.Accumulator += (double)(currTicks - prevTicks) / frequency;
                double elapsedTime = (currTicks - prevTicks) / frequency;
                Timing.Accumulator += elapsedTime;
                Timing.TotalTime += elapsedTime;
                prevTicks = currTicks;

                if (GameMain.NilMod.UseExperimentalFPSLagPrevention)
                {
                    if ((int)AverageUpdatesPerSecond <= 2)
                    {
                        Timing.Step = 1.0 / 8.0;
                        FarseerPhysics.Settings.VelocityIterations = 10;
                        FarseerPhysics.Settings.PositionIterations = 4;
                        FarseerPhysics.Settings.TOIPositionIterations = 25;
                        FarseerPhysics.Settings.TOIVelocityIterations = 10;
                    }
                    else if ((int)AverageUpdatesPerSecond <= 4)
                    {
                        Timing.Step = 1.0 / 10.0;
                        FarseerPhysics.Settings.VelocityIterations = 10;
                        FarseerPhysics.Settings.PositionIterations = 4;
                        FarseerPhysics.Settings.TOIPositionIterations = 25;
                        FarseerPhysics.Settings.TOIVelocityIterations = 10;
                    }
                    else if ((int)AverageUpdatesPerSecond <= 6)
                    {
                        Timing.Step = 1.0 / 12.0;
                        FarseerPhysics.Settings.VelocityIterations = 10;
                        FarseerPhysics.Settings.PositionIterations = 4;
                        FarseerPhysics.Settings.TOIPositionIterations = 25;
                        FarseerPhysics.Settings.TOIVelocityIterations = 10;
                    }
                    else if ((int)AverageUpdatesPerSecond <= 8)
                    {
                        Timing.Step = 1.0 / 15.0;
                        FarseerPhysics.Settings.VelocityIterations = 9;
                        FarseerPhysics.Settings.PositionIterations = 4;
                        FarseerPhysics.Settings.TOIPositionIterations = 22;
                        FarseerPhysics.Settings.TOIVelocityIterations = 9;
                    }
                    else if ((int)AverageUpdatesPerSecond <= 10)
                    {
                        Timing.Step = 1.0 / 20.0;
                        FarseerPhysics.Settings.VelocityIterations = 9;
                        FarseerPhysics.Settings.PositionIterations = 4;
                        FarseerPhysics.Settings.TOIPositionIterations = 22;
                        FarseerPhysics.Settings.TOIVelocityIterations = 9;
                    }
                    else if ((int)AverageUpdatesPerSecond <= 12)
                    {
                        Timing.Step = 1.0 / 25.0;
                        FarseerPhysics.Settings.VelocityIterations = 9;
                        FarseerPhysics.Settings.PositionIterations = 4;
                        FarseerPhysics.Settings.TOIPositionIterations = 22;
                        FarseerPhysics.Settings.TOIVelocityIterations = 9;
                    }
                    else if ((int)AverageUpdatesPerSecond <= 14)
                    {
                        Timing.Step = 1.0 / 30.0;
                        FarseerPhysics.Settings.VelocityIterations = 8;
                        FarseerPhysics.Settings.PositionIterations = 3;
                        FarseerPhysics.Settings.TOIPositionIterations = 20;
                        FarseerPhysics.Settings.TOIVelocityIterations = 8;
                    }
                    else if ((int)AverageUpdatesPerSecond <= 16)
                    {
                        Timing.Step = 1.0 / 35.0;
                        FarseerPhysics.Settings.VelocityIterations = 8;
                        FarseerPhysics.Settings.PositionIterations = 3;
                        FarseerPhysics.Settings.TOIPositionIterations = 20;
                        FarseerPhysics.Settings.TOIVelocityIterations = 8;
                    }
                    else if ((int)AverageUpdatesPerSecond <= 18)
                    {
                        Timing.Step = 1.0 / 40.0;
                        FarseerPhysics.Settings.VelocityIterations = 8;
                        FarseerPhysics.Settings.PositionIterations = 3;
                        FarseerPhysics.Settings.TOIPositionIterations = 20;
                        FarseerPhysics.Settings.TOIVelocityIterations = 8;
                    }
                    else if ((int)AverageUpdatesPerSecond <= 20)
                    {
                        Timing.Step = 1.0 / 45.0;
                        FarseerPhysics.Settings.VelocityIterations = 8;
                        FarseerPhysics.Settings.PositionIterations = 3;
                        FarseerPhysics.Settings.TOIPositionIterations = 20;
                        FarseerPhysics.Settings.TOIVelocityIterations = 8;
                    }
                    else if ((int)AverageUpdatesPerSecond <= 22)
                    {
                        Timing.Step = 1.0 / 50.0;
                        FarseerPhysics.Settings.VelocityIterations = 8;
                        FarseerPhysics.Settings.PositionIterations = 3;
                        FarseerPhysics.Settings.TOIPositionIterations = 20;
                        FarseerPhysics.Settings.TOIVelocityIterations = 8;
                    }
                    else if ((int)AverageUpdatesPerSecond <= 25)
                    {
                        Timing.Step = 1.0 / 55.0;
                        FarseerPhysics.Settings.VelocityIterations = 8;
                        FarseerPhysics.Settings.PositionIterations = 3;
                        FarseerPhysics.Settings.TOIPositionIterations = 20;
                        FarseerPhysics.Settings.TOIVelocityIterations = 8;
                    }
                    else
                    {
                        Timing.Step = 1.0 / 60.0;
                        FarseerPhysics.Settings.VelocityIterations = 8;
                        FarseerPhysics.Settings.PositionIterations = 3;
                        FarseerPhysics.Settings.TOIPositionIterations = 20;
                        FarseerPhysics.Settings.TOIVelocityIterations = 8;
                    }
                }
                else
                {
                    Timing.Step = 1.0 / 60.0;
                    FarseerPhysics.Settings.VelocityIterations = 8;
                    FarseerPhysics.Settings.PositionIterations = 3;
                    FarseerPhysics.Settings.TOIPositionIterations = 20;
                    FarseerPhysics.Settings.TOIVelocityIterations = 8;
                }

                while (Timing.Accumulator >= Timing.Step)
                {
                    DebugConsole.Update();

                    NilMod.Update((float)Timing.Step);

                    if (Screen.Selected != null) Screen.Selected.Update((float)Timing.Step);
                    Server.Update((float)Timing.Step);
                    CoroutineManager.Update((float)Timing.Step, (float)Timing.Step);

                    CurrentUpdatesPerSecond = (1.0 / Timing.Step);

                    sampleBuffer.Enqueue(CurrentUpdatesPerSecond);

                    if (sampleBuffer.Count > MaximumSamples)
                    {
                        sampleBuffer.Dequeue();
                        AverageUpdatesPerSecond = sampleBuffer.Average(i => i);
                    }
                    else
                    {
                        AverageUpdatesPerSecond = CurrentUpdatesPerSecond;
                    }

                    Timing.Accumulator -= Timing.Step;
                }
                int frameTime = (int)(((double)(stopwatch.ElapsedTicks - prevTicks) / frequency) * 1000.0);
                Thread.Sleep(Math.Max(((int)((double)(1d / 60d) * 1000.0) - frameTime) / 2, 0));
            }
            stopwatch.Stop();

            CloseServer();

        }
        
        public void ProcessInput()
        {
            while (true)
            {
                string input = Console.ReadLine();
                lock (DebugConsole.QueuedCommands)
                {
                    DebugConsole.QueuedCommands.Add(input);
                }
            }
        }

        public CoroutineHandle ShowLoading(IEnumerable<object> loader, bool waitKeyHit = true)
        {
            return CoroutineManager.StartCoroutine(loader);
        }

        public void DefaultServerStartup()
        {
            Boolean startcampaign = false;

            //Default Mission Parameters
            if (GameMain.NilMod.DefaultGamemode.ToLowerInvariant() == "mission")
            {
                GameMain.NetLobbyScreen.SelectedModeIndex = 1;
                //GameMain.NilMod.DefaultMissionType = "Cargo";
                //Only select this default if we actually default to mission mode
                switch (GameMain.NilMod.DefaultMissionType.ToLowerInvariant())
                {
                    case "random":
                        GameMain.NetLobbyScreen.MissionTypeIndex = 0;
                        break;
                    case "salvage":
                        GameMain.NetLobbyScreen.MissionTypeIndex = 1;
                        break;
                    case "monster":
                        GameMain.NetLobbyScreen.MissionTypeIndex = 2;
                        break;
                    case "cargo":
                        GameMain.NetLobbyScreen.MissionTypeIndex = 3;
                        break;
                    case "combat":
                        GameMain.NetLobbyScreen.MissionTypeIndex = 4;
                        break;
                    //Random if no valid mission type
                    default:
                        GameMain.NetLobbyScreen.MissionTypeIndex = 0;
                        break;
                }
            }
            else if (GameMain.NilMod.DefaultGamemode.ToLowerInvariant() == "campaign")
            {
                startcampaign = true;
                GameMain.NetLobbyScreen.SelectedModeIndex = 1;
            }
            else
            {
                GameMain.NetLobbyScreen.SelectedModeIndex = 0;
            }

            if (GameMain.NilMod.DefaultLevelSeed != "") GameMain.NetLobbyScreen.LevelSeed = GameMain.NilMod.DefaultLevelSeed;

            if (GameMain.NilMod.DefaultSubmarine != "")
            {
                Submarine sub = GameMain.NetLobbyScreen.GetSubList().Find(s => s.Name.ToLower() == GameMain.NilMod.DefaultSubmarine.ToLower());

                if (sub != null)
                {
                    GameMain.NetLobbyScreen.SelectedSub = sub;
                }
                else
                {
                    sub = GameMain.NetLobbyScreen.SelectedSub;
                    DebugConsole.NewMessage("Default submarine: " + GameMain.NilMod.DefaultSubmarine + " not found, using " + sub.Name + " instead", Color.Red);
                }
            }

            if (GameMain.NilMod.DefaultRespawnShuttle != "")
            {
                Submarine shuttle = GameMain.NetLobbyScreen.GetSubList().Find(s => s.Name.ToLower() == GameMain.NilMod.DefaultRespawnShuttle.ToLower());

                if (shuttle != null)
                {
                    GameMain.NetLobbyScreen.SelectedShuttle = shuttle;
                }
                else
                {
                    shuttle = GameMain.NetLobbyScreen.SelectedShuttle;
                    DebugConsole.NewMessage("Default shuttle: " + GameMain.NilMod.DefaultRespawnShuttle + " not found, using " + shuttle.Name + " instead", Color.Red);
                }
            }

            DebugConsole.NewMessage(
                "Save Server Logs: " + (GameMain.Server.SaveServerLogs ? "YES" : "NO") +
                ", Allow File Transfers: " + (GameMain.Server.AllowFileTransfers ? "YES" : "NO"), Color.Cyan);

            DebugConsole.NewMessage(
                "Allow Spectating: " + (GameMain.Server.AllowSpectating ? "YES" : "NO"), Color.Cyan);

            //LevelSeed = ToolBox.RandomSeed(8);



            DebugConsole.NewMessage(" ", Color.Cyan);

            DebugConsole.NewMessage(
                "Auto Restart: " + (GameMain.Server.AutoRestart ? "YES" : "NO") +
                ", Auto Restart Interval: " + ToolBox.SecondsToReadableTime(GameMain.Server.AutoRestartInterval), Color.Cyan);

            DebugConsole.NewMessage(
                "End Round At Level End: " + (GameMain.Server.EndRoundAtLevelEnd ? "YES" : "NO") +
                ", End Vote Required Ratio: " + (GameMain.Server.EndVoteRequiredRatio * 100) + "%", Color.Cyan);
            
            DebugConsole.NewMessage(
                "Allow Vote Kick: " + (GameMain.Server.AllowVoteKick ? "YES" : "NO") +
                ", Kick Vote Required Ratio: " + (GameMain.Server.KickVoteRequiredRatio * 100) + "%", Color.Cyan);

            DebugConsole.NewMessage(" ", Color.Cyan);

            DebugConsole.NewMessage(
                "Allow Respawns: " + (GameMain.Server.AllowRespawn ? "YES" : "NO") +
                ", Min Respawn Ratio:" + GameMain.Server.MinRespawnRatio, Color.Cyan);

            DebugConsole.NewMessage(
                "Respawn Interval: " + ToolBox.SecondsToReadableTime(GameMain.Server.RespawnInterval) +
                ", Max Transport Time:" + ToolBox.SecondsToReadableTime(GameMain.Server.MaxTransportTime), Color.Cyan);

            DebugConsole.NewMessage(" ", Color.Cyan);

            DebugConsole.NewMessage(
                "Gamemode Selection: " + GameMain.Server.ModeSelectionMode.ToString() +
                ", Submarine Selection: " + GameMain.Server.SubSelectionMode.ToString(), Color.Cyan);

            DebugConsole.NewMessage(
                "Default Gamemode: " + GameMain.NetLobbyScreen.SelectedModeName +
                ", Default Mission Type: " + GameMain.NetLobbyScreen.MissionTypeName, Color.Cyan);

            DebugConsole.NewMessage("TraitorsEnabled: " + GameMain.Server.TraitorsEnabled.ToString(), Color.Cyan);

            DebugConsole.NewMessage(" ", Color.Cyan);

            if (!startcampaign)
            {
                DebugConsole.NewMessage("Starting with Level Seed: " + GameMain.NetLobbyScreen.LevelSeed, Color.Cyan);
                DebugConsole.NewMessage("On submarine: " + GameMain.NetLobbyScreen.SelectedSub.Name, Color.Cyan);
                DebugConsole.NewMessage("Using respawn shuttle: " + GameMain.NetLobbyScreen.SelectedShuttle.Name, Color.Cyan);
            }
            else
            {
                if (GameMain.NilMod.CampaignDefaultSaveName != "")
                {
                    MultiPlayerCampaign.StartCampaignSetup(true);
                }
                else
                {
                    DebugConsole.NewMessage("Nilmod default campaign savefile not specified. Please setup the campaign or specify a filename in nilmodsettings.xml", Color.Cyan);
                    MultiPlayerCampaign.StartCampaignSetup(false);
                }
                DebugConsole.NewMessage(" ", Color.Cyan);
            }
        }

        //Refresh the entire server
        public void AutoRestartServer(string name, int port, bool isPublic, string password, bool attemptUPnP, int maxPlayers, Lidgren.Network.NetServer prevserver = null, Lidgren.Network.NetPeerConfiguration prevconfig = null)
        {
            List<Client> PreviousClients = new List<Client>(GameMain.Server.ConnectedClients);
            ushort LastUpdateID = GameMain.NetLobbyScreen.LastUpdateID += 1;
            Server.DisconnectRestart();
            Server = null;

            Config = new GameSettings("config.xml");
            if (Config.WasGameUpdated)
            {
                UpdaterUtil.CleanOldFiles();
                Config.WasGameUpdated = false;
                Config.Save("config.xml");
            }

            NilMod = new NilMod();
            NilMod.Load(false);

            NilMod.NilModVPNBanlist = new VPNBanlist();
            NilMod.NilModVPNBanlist.LoadVPNBans();

            GameScreen = new GameScreen();

            //Init();

            Submarine.RefreshSavedSubs();

            Screen.SelectNull();

            NetLobbyScreen = new NetLobbyScreen();

            NetLobbyScreen.ServerName = GameMain.NilMod.ServerName;

            Server = new GameServer(name,
            port,
            isPublic,
            password,
            attemptUPnP,
            maxPlayers,
            prevserver,
            prevconfig);

            DefaultServerStartup();

            Timing.Accumulator = 0.0;
            stopwatch.Stop();
            stopwatch = Stopwatch.StartNew();
            prevTicks = stopwatch.ElapsedTicks;

            GameMain.Server.AddRestartClients(PreviousClients, LastUpdateID);
        }
    }
}
