﻿#region Using Statements

using System;
using System.IO;
using System.Text;
using GameAnalyticsSDK.Net;

#if WINDOWS
using System.Windows.Forms;
using Microsoft.Xna.Framework.Graphics;
#endif

#endregion

namespace Barotrauma
{
#if WINDOWS || LINUX
    /// <summary>
    /// The main class.
    /// </summary>
    public static class Program
    {
        private static int restartAttempts;
        private static Boolean CrashRestarted;

        public static string[] CommandLineArgs = Environment.GetCommandLineArgs();

#if WINDOWS
        [Flags]
        internal enum ErrorModes : uint
        {
            SYSTEM_DEFAULT = 0x0,
            SEM_FAILCRITICALERRORS = 0x0001,
            SEM_NOALIGNMENTFAULTEXCEPT = 0x0004,
            SEM_NOGPFAULTERRORBOX = 0x0002,
            SEM_NOOPENFILEERRORBOX = 0x8000
        }
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        internal static extern ErrorModes SetErrorMode(ErrorModes mode);
#endif

        static GameMain game;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
#if WINDOWS
            SetErrorMode(ErrorModes.SEM_NOGPFAULTERRORBOX);
#endif
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(UnrecoverableCrashHandler);

            using (game = new GameMain())
            {     
#if DEBUG
                game.Run();
#else
                bool attemptRestart = false;
                CrashRestarted = false;

                do
                {
                    try
                    {
                        game.Run();
                        attemptRestart = false;
                    }
                    catch (Exception e)
                    {                   
                        if (restartAttempts < 5 && CheckException(game, e))
                        {
                            attemptRestart = true;
                            restartAttempts++;
                        }
                        else
                        {
                            string CrashName = "crashreport" + "_" + DateTime.Now.ToShortDateString() + "_" + DateTime.Now.ToShortTimeString() + ".txt";

                            CrashName = CrashName.Replace(":", "");
                            CrashName = CrashName.Replace("../", "");
                            CrashName = CrashName.Replace("/", "");
                            CrashDump(game, CrashName, e);
                        }

                    }
                } while (attemptRestart);
#endif
            }
        }

        static void UnrecoverableCrashHandler(object sender, UnhandledExceptionEventArgs args)
        {
            CrashRestart(sender, args.ExceptionObject);
        }

        public static void CrashRestart(object sender, Object exception)
        {
            if (CrashRestarted) return;
            if (GameMain.NilMod != null)
            {
                if (GameMain.NilMod.CrashRestart && GameMain.NilMod.SuccesfulStart)
                {
                    Exception e = null;
                    if(exception != null) e = (Exception)exception;
                    //DebugConsole.NewMessage("Unhandled Exception Caught (Program has crashed!) : " + e.Message, Microsoft.Xna.Framework.Color.Red);
                    if (GameMain.Server != null && GameMain.Server.ServerLog != null)
                    {
                        GameMain.Server.ServerLog.WriteLine("Server Has Suffered a fatal Crash (Autorestarting).", Networking.ServerLog.MessageType.Error);
                        GameMain.Server.ServerLog.Save();
                    }

                    CrashRestarted = true;

#if LINUX
                    //System.Diagnostics.Process.Start(System.Reflection.Assembly.GetEntryAssembly().CodeBase + "\\Barotrauma NilEdit.exe");
                    
                    System.Diagnostics.Process process = new System.Diagnostics.Process();
                    process.StartInfo.FileName = "Barotrauma NilEdit.exe";
                    process.StartInfo.WorkingDirectory = System.Reflection.Assembly.GetEntryAssembly().CodeBase;
                    process.Start();
                    
                    //Kind of flipping the table here after the last one or two didnt work.
                    //inputThread.Abort(); inputThread.Join();
                    Environment.Exit(1);
#else
                    //System.Diagnostics.Process.Start(Application.StartupPath + "\\Barotrauma NilEdit.exe");

                    System.Diagnostics.Process process = new System.Diagnostics.Process();
                    process.StartInfo.FileName = "Barotrauma NilEdit.exe";
                    process.StartInfo.WorkingDirectory = Application.StartupPath;
                    process.Start();

                    Application.ExitThread();
                    Application.Exit();
                    Environment.Exit(1);
#endif
                }
            }
        }

        private static bool CheckException(GameMain game, Exception e)
        {
#if WINDOWS

            if (e is SharpDX.SharpDXException)
            {
                DebugConsole.NewMessage("SharpDX exception caught. (" + e.Message + "). Attempting to fix...", Microsoft.Xna.Framework.Color.Red);

                switch ((uint)((SharpDX.SharpDXException)e).ResultCode.Code)
                {
                    case 0x887A0022: //DXGI_ERROR_NOT_CURRENTLY_AVAILABLE
                        switch (restartAttempts)
                        {
                            case 0:
                                //just wait and try again
                                DebugConsole.NewMessage("Retrying after 100 ms...", Microsoft.Xna.Framework.Color.Red);
                                System.Threading.Thread.Sleep(100);
                                return true;
                            case 1:
                                //force focus to this window
                                DebugConsole.NewMessage("Forcing focus to the window and retrying...", Microsoft.Xna.Framework.Color.Red);
                                var myForm = (System.Windows.Forms.Form)System.Windows.Forms.Form.FromHandle(game.Window.Handle);
                                myForm.Focus();
                                return true;
                            case 2:
                                //try disabling hardware mode switch
                                if (GameMain.Config.WindowMode == WindowMode.Fullscreen)
                                {
                                    DebugConsole.NewMessage("Failed to set fullscreen mode, switching configuration to borderless windowed", Microsoft.Xna.Framework.Color.Red);
                                    GameMain.Config.WindowMode = WindowMode.BorderlessWindowed;
                                    GameMain.Config.Save("config.xml");
                                }
                                return false;
                            default:
                                return false;
                            
                        }
                    case 0x80070057: //E_INVALIDARG/Invalid Arguments
                        DebugConsole.NewMessage("Invalid graphics settings, attempting to fix...", Microsoft.Xna.Framework.Color.Red);

                        GameMain.Config.GraphicsWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
                        GameMain.Config.GraphicsHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;

                        DebugConsole.NewMessage("Display size set to " + GameMain.Config.GraphicsWidth + "x" + GameMain.Config.GraphicsHeight, Microsoft.Xna.Framework.Color.Red);

                        game.ApplyGraphicsSettings();

                        return true;
                    default:
                        return false;
                }
            }

#endif
                
            return false;            
        }

        public static void CrashMessageBox(string message)
        {
#if WINDOWS
            MessageBox.Show(message, "Oops! Barotrauma just crashed.", MessageBoxButtons.OK, MessageBoxIcon.Error);
#endif
        }

        static void CrashDump(GameMain game, string filePath, Exception exception)
        {
            DebugConsole.DequeueMessages();

            StreamWriter sw = new StreamWriter(filePath);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Barotrauma Client crash report (generated on " + DateTime.Now + ")");
            sb.AppendLine("\n");
            sb.AppendLine("Barotrauma seems to have crashed. Sorry for the inconvenience! ");
            sb.AppendLine("\n");
#if DEBUG
            sb.AppendLine("Game version " + GameMain.Version + " NILMOD SERVER MODIFICATION" + " (debug build)");
#else
            sb.AppendLine("Game version " + GameMain.Version + " NILMOD SERVER MODIFICATION");
#endif
            sb.AppendLine("Nilmod version stamp: " + NilMod.NilModVersionDate);
            sb.AppendLine("Graphics mode: " + GameMain.Config.GraphicsWidth + "x" + GameMain.Config.GraphicsHeight + " (" + GameMain.Config.WindowMode.ToString() + ")");
            sb.AppendLine("Selected content package: " + GameMain.SelectedPackage.Name);
            sb.AppendLine("Level seed: " + ((Level.Loaded == null) ? "no level loaded" : Level.Loaded.Seed));
            sb.AppendLine("Loaded submarine: " + ((Submarine.MainSub == null) ? "None" : Submarine.MainSub.Name + " (" + Submarine.MainSub.MD5Hash + ")"));
            sb.AppendLine("Selected screen: " + (Screen.Selected == null ? "None" : Screen.Selected.ToString()));

            if (GameMain.GameSession != null)
            {
                if (GameMain.GameSession.GameMode != null) sb.AppendLine("Gamemode: " + GameMain.GameSession.GameMode.Name);
                if (GameMain.GameSession.Mission != null) sb.AppendLine("Mission: " + GameMain.GameSession.Mission.Name);
            }

            if (Character.CharacterList != null && Character.CharacterList.Count > 0)
            {
                sb.AppendLine("\n");
                if (Character.CharacterList.FindAll(c => c.Removed).Count > 0) sb.AppendLine("Removed character references detected, " + Character.CharacterList.Count + " Characters in list, " + Character.CharacterList.FindAll(c => c.Removed).Count + " Removed.");

                int foundnullanimcontroller = 0;
                int foundnulllimbs = 0;
                int foundzerolengthlimbs = 0;
                for (int i = Character.CharacterList.Count - 1; i >= 0; i--)
                {
                    if (Character.CharacterList[i].AnimController == null)
                    {
                        foundnullanimcontroller += 1;
                    }
                    else
                    {
                        if (Character.CharacterList[i].AnimController.Limbs == null)
                        {
                            foundnulllimbs += 1;
                        }
                        else if (Character.CharacterList[i].AnimController.Limbs.Length < 1) foundzerolengthlimbs += 1;
                    }
                }
                if (foundnullanimcontroller > 0) sb.AppendLine(foundnullanimcontroller + " Characters with null AnimControllers found.");
                if (foundnulllimbs > 0) sb.AppendLine(foundnulllimbs + " Characters with null limbs[] reference found.");
                if (foundzerolengthlimbs > 0) sb.AppendLine(foundzerolengthlimbs + " characters with 0 limbs found.");
            }

            if (GameMain.Server != null)
            {
                sb.AppendLine("Server (" + (GameMain.Server.GameStarted ? "Round had started)" : "Round hadn't been started)"));
            }
            else if (GameMain.Client != null)
            {
                sb.AppendLine("Client (" + (GameMain.Client.GameStarted ? "Round had started)" : "Round hadn't been started)"));
            }

            sb.AppendLine("\n");
            sb.AppendLine("System info:");
            sb.AppendLine("    Operating system: " + System.Environment.OSVersion + (System.Environment.Is64BitOperatingSystem ? " 64 bit" : " x86"));

            if (game.GraphicsDevice == null)
            {
                sb.AppendLine("    Graphics device not set");
            }
            else
            {
                if (game.GraphicsDevice.Adapter == null)
                {
                    sb.AppendLine("    Graphics adapter not set");
                }
                else
                {
                    sb.AppendLine("    GPU name: " + game.GraphicsDevice.Adapter.Description);
                    sb.AppendLine("    Display mode: " + game.GraphicsDevice.Adapter.CurrentDisplayMode);
                }

                sb.AppendLine("    GPU status: " + game.GraphicsDevice.GraphicsDeviceStatus);
            }

#if LINUX
            if (GameMain.NilMod != null && GameMain.NilMod.CrashRestart)
            {
                sb.AppendLine("\n");
                sb.AppendLine("Attempted restart of process using: " + System.Diagnostics.Process.Start(System.Reflection.Assembly.GetEntryAssembly().CodeBase + "\\Barotrauma NilEdit.exe"));
                sb.AppendLine("\n");
            }
#else
            if (GameMain.NilMod != null && GameMain.NilMod.CrashRestart)
            {
                sb.AppendLine("\n");
                sb.AppendLine("Attempted restart of process using: " + Application.StartupPath + "\\Barotrauma NilEdit.exe");
                sb.AppendLine("\n");
            }
#endif

            sb.AppendLine("\n");
            sb.AppendLine("This was running NilMod Code!");
            sb.AppendLine("\n");
            sb.AppendLine("Exception: " + exception.Message);
            sb.AppendLine("Target site: " + exception.TargetSite.ToString());
            sb.AppendLine("Stack trace: ");
            sb.AppendLine(exception.StackTrace);
            sb.AppendLine("\n");

            sb.AppendLine("Last debug messages:");
            if(game != null)
            {
                if(GameMain.NilMod != null)
                {
                    if(GameMain.NilMod.DebugConsoleTimeStamp)
                    {
                        for (int i = DebugConsole.Messages.Count - 1; i > 0; i--)
                        {
                            sb.AppendLine(DebugConsole.Messages[i].Text);
                        }
                    }
                    else
                    {
                        for (int i = DebugConsole.Messages.Count - 1; i > 0; i--)
                        {
                            sb.AppendLine("[" + DebugConsole.Messages[i].Time + "] " + DebugConsole.Messages[i].Text);
                        }
                    }
                }
                else
                {
                    for (int i = DebugConsole.Messages.Count - 1; i > 0; i--)
                    {
                        sb.AppendLine("[" + DebugConsole.Messages[i].Time + "] " + DebugConsole.Messages[i].Text);
                    }
                }
            }
            else
            {
                for (int i = DebugConsole.Messages.Count - 1; i > 0; i--)
                {
                    sb.AppendLine("[" + DebugConsole.Messages[i].Time + "] " + DebugConsole.Messages[i].Text);
                }
            }

            string crashReport = sb.ToString();

            sw.WriteLine(crashReport);
            sw.Close();

            if (GameMain.NilMod != null)
            {
                if (GameMain.NilMod.CrashRestart && GameMain.NilMod.SuccesfulStart)
                {
                    if (GameSettings.SaveDebugConsoleLogs) DebugConsole.SaveLogs();

                    if (GameSettings.SendUserStatistics)
                    {
                        GameAnalytics.AddErrorEvent(EGAErrorSeverity.Critical, crashReport);
                        GameAnalytics.OnStop();
                    }

                    CrashRestart(null, exception);
                }
                else
                {
                    if (GameSettings.SaveDebugConsoleLogs) DebugConsole.SaveLogs();

                    if (GameSettings.SendUserStatistics)
                    {
                        CrashMessageBox("A crash report (\"crashreport.log\") was saved in the root folder of the game and sent to the developers.");
                        GameAnalytics.AddErrorEvent(EGAErrorSeverity.Critical, crashReport);
                        GameAnalytics.OnStop();
                    }
                    else
                    {
                        CrashMessageBox("A crash report (\"crashreport.log\") was saved in the root folder of the game." + Environment.NewLine +
                            "If you'd like to help fix this bug, please post the report on Barotrauma's GitHub issue tracker: https://github.com/Regalis11/Barotrauma/issues/" + Environment.NewLine +
                            "Alternatively, If you believe this to be a mod bug, please post the report on the forum topic or the mods GitHub issue tracker: https://github.com/NilanthAnimosus/Barotrauma---Nilanths-Edits/issues");
                    }
                }
            }
            else
            {
                if (GameSettings.SaveDebugConsoleLogs) DebugConsole.SaveLogs();

                CrashMessageBox("A crash report (\"crashreport.log\") was saved in the root folder of the game." + Environment.NewLine +
                    "If you'd like to help fix this bug, please post the report on Barotrauma's GitHub issue tracker: https://github.com/Regalis11/Barotrauma/issues/" + Environment.NewLine +
                    "Alternatively, If you believe this to be a mod bug, please post the report on the forum topic or the mods GitHub issue tracker: https://github.com/NilanthAnimosus/Barotrauma---Nilanths-Edits/issues");
            }
        }
    }
#endif
}