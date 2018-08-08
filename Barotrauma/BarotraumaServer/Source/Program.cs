﻿#region Using Statements

using GameAnalyticsSDK.Net;
using System;
using System.IO;
using System.Text;
using System.Threading;

#endregion

namespace Barotrauma
{
    /// <summary>
    /// The main class.
    /// </summary>
    public static class Program
    {
        public static string[] CommandLineArgs = Environment.GetCommandLineArgs();
        private static Boolean CrashRestarted;

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

        static GameMain game = null;
        public static Thread inputThread = null;

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

            try
            {
                CrashRestarted = false;
                game = new GameMain();
                inputThread = new Thread(new ThreadStart(game.ProcessInput));
                inputThread.Start();
                game.Run();
                inputThread.Abort(); inputThread.Join();
                if (GameSettings.SendUserStatistics) GameAnalytics.OnStop();
            }
            catch (Exception e)
            {
                string CrashName = "crashreport" + "_" + DateTime.Now.ToShortDateString() + "_" + DateTime.Now.ToShortTimeString() + ".txt";

                CrashName = CrashName.Replace(":", "");
                CrashName = CrashName.Replace("../", "");
                CrashName = CrashName.Replace("/", "");
                CrashDump(game, CrashName, e);
                //inputThread.Abort(); inputThread.Join();
            }
        }

        static void UnrecoverableCrashHandler(object sender, UnhandledExceptionEventArgs args)
        {
            CrashRestart(sender, args.ExceptionObject);
        }

        static void CrashRestart(object sender, Object exception)
        {
            if (CrashRestarted) return;
            if (GameMain.NilMod != null)
            {
                if (GameMain.NilMod.CrashRestart && GameMain.NilMod.SuccesfulStart)
                {
                    Exception e = null;
                    if (exception != null) e = (Exception)exception;
                    //DebugConsole.NewMessage("Unhandled Exception Caught (Program has crashed!) : " + e.Message, Microsoft.Xna.Framework.Color.Red);
                    if (GameMain.Server != null)
                    {
                        GameMain.Server.ServerLog.WriteLine("Server Has Suffered a fatal Crash (Autorestarting).", Networking.ServerLog.MessageType.Error);
                        GameMain.Server.ServerLog.Save();
                    }

                    CrashRestarted = true;

                    //System.Diagnostics.Process.Start(System.Reflection.Assembly.GetEntryAssembly().CodeBase + "\\BarotraumaServer NilEdit.exe");

                    System.Diagnostics.Process process = new System.Diagnostics.Process();
                    process.StartInfo.WorkingDirectory = System.Reflection.Assembly.GetEntryAssembly().CodeBase;
                    process.StartInfo.FileName = "BarotraumaServer NilEdit.exe";
                    process.Start();

                    //Kind of flipping the table here after the last one or two didnt work.
                    inputThread.Abort(); inputThread.Join();
                    if (GameSettings.SendUserStatistics) GameAnalytics.OnStop();
                    Environment.Exit(-1);
                }
            }
        }

        static void CrashDump(GameMain game, string filePath, Exception exception)
        {
            StreamWriter sw = new StreamWriter(filePath);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Barotrauma Dedicated Server crash report (generated on " + DateTime.Now + ")");
            sb.AppendLine("\n");
            sb.AppendLine("Barotrauma seems to have crashed. Sorry for the inconvenience! ");
            sb.AppendLine("\n");
            sb.AppendLine("Game version " + GameMain.Version + " NILMOD SERVER MODIFICATION");
            sb.AppendLine("Nilmod version stamp: " + NilMod.NilModVersionDate);
            //sb.AppendLine("Selected content package: " + GameMain.SelectedPackage.Name);
            sb.AppendLine("Level seed: " + ((Level.Loaded == null) ? "no level loaded" : Level.Loaded.Seed));
            sb.AppendLine("Loaded submarine: " + ((Submarine.MainSub == null) ? "None" : Submarine.MainSub.Name + " (" + Submarine.MainSub.MD5Hash + ")"));
            sb.AppendLine("Selected screen: " + (Screen.Selected == null ? "None" : Screen.Selected.ToString()));

            if (GameMain.GameSession != null)
            {
                if (GameMain.GameSession.Mission != null) sb.AppendLine("Mission: " + GameMain.GameSession.Mission.Name);
                if (GameMain.GameSession.GameMode != null) sb.AppendLine("Gamemode: " + GameMain.GameSession.GameMode.Name);
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
                    if(Character.CharacterList[i].AnimController == null)
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

            sb.AppendLine("\n");
            sb.AppendLine("System info:");
            sb.AppendLine("    Operating system: " + System.Environment.OSVersion + (System.Environment.Is64BitOperatingSystem ? " 64 bit" : " x86"));
            sb.AppendLine("\n");
            sb.AppendLine("This was running NilMod Code!");
            sb.AppendLine("\n");
            sb.AppendLine("Exception: "+exception.Message);
            sb.AppendLine("Target site: " +exception.TargetSite.ToString());
            sb.AppendLine("Stack trace: ");
            sb.AppendLine(exception.StackTrace);
            sb.AppendLine("\n");

            sb.AppendLine("Last debug messages:");
            for (int i = DebugConsole.Messages.Count - 1; i > 0; i--)
            {
                sb.AppendLine("   " + DebugConsole.Messages[i].Time + " - " + DebugConsole.Messages[i].Text);
            }

            string crashReport = sb.ToString();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write(sb.ToString());

            sw.WriteLine(sb.ToString());
            sw.Close();

            if (GameSettings.SendUserStatistics)
            {
                GameAnalytics.AddErrorEvent(EGAErrorSeverity.Error, crashReport);
                GameAnalytics.OnStop();
                Console.Write("A crash report (\"crashreport.log\") was saved in the root folder of the game and sent to the developers.");
            }
            else
            {
                Console.Write("A crash report(\"crashreport.log\") was saved in the root folder of the game. The error was not sent to the developers because user statistics have been disabled, but" +
                    "If you'd like to help fix this bug, please post the report on Barotrauma's GitHub issue tracker: https://github.com/Regalis11/Barotrauma/issues/" + Environment.NewLine +
                    "Alternatively, If you believe this to be a mod bug, please post the report on the forum topic or the mods GitHub issue tracker: https://github.com/NilanthAnimosus/Barotrauma---Nilanths-Edits/issues");
            }

            if (GameMain.NilMod != null)
            {
                if (GameMain.NilMod.CrashRestart && GameMain.NilMod.SuccesfulStart)
                {
                    CrashRestart(null, exception);
                }
            }
        }
    }
}
