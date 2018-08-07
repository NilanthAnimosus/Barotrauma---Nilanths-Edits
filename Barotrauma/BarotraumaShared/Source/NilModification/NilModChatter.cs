using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Barotrauma.Networking;
using System.Xml.Linq;
using System.IO;

namespace Barotrauma
{
    //Class for storing, sending and receiving of Event-Specific Chat information to players and management of the Chat XML file
    class NilModEventChatter
    {
        const string ChatSavePath = "Data/NilMod/EventChatterSettings.xml";

        //Chat Configuration
        public Boolean ChatModServerJoin;
        public Boolean ChatTraitorReminder;
        public Boolean ChatNoneTraitorReminder;
        public Boolean ChatShuttleRespawn;
        public Boolean ChatShuttleLeaving500;
        public Boolean ChatShuttleLeaving400;
        public Boolean ChatShuttleLeaving300;
        public Boolean ChatShuttleLeaving200;
        public Boolean ChatShuttleLeaving130;
        public Boolean ChatShuttleLeaving100;
        public Boolean ChatShuttleLeaving030;
        public Boolean ChatShuttleLeaving015;
        public Boolean ChatShuttleLeavingKill;
        public Boolean ChatSubvsSub;
        public Boolean ChatSalvage;
        public Boolean ChatMonster;
        public Boolean ChatCargo;
        public Boolean ChatSandbox;
        public Boolean ChatVoteEnd;

        public List<String> NilModRules;
        public List<String> NilTraitorReminder;
        public List<String> NilNoneTraitorReminder;
        public List<String> NilShuttleRespawn;
        public List<String> NilShuttleLeaving500;
        public List<String> NilShuttleLeaving400;
        public List<String> NilShuttleLeaving300;
        public List<String> NilShuttleLeaving200;
        public List<String> NilShuttleLeaving130;
        public List<String> NilShuttleLeaving100;
        public List<String> NilShuttleLeaving030;
        public List<String> NilShuttleLeaving015;
        public List<String> NilShuttleLeavingKill;
        public List<String> NilSubvsSubCoalition;
        public List<String> NilSubvsSubRenegade;
        public List<String> NilSalvage;
        public List<String> NilMonster;
        public List<String> NilCargo;
        public List<String> NilSandbox;
        public List<String> NilVoteEnd;

        public void ReportSettings()
        {
            //Informational Chat Message Related Settings
            GameMain.Server.ServerLog.WriteLine("ChatModRules = " + (ChatModServerJoin ? "Enabled" : "Disabled") + "With " + NilModRules.Count() + " Lines", ServerLog.MessageType.NilMod);
            GameMain.Server.ServerLog.WriteLine("ChatTraitorReminder = " + (ChatTraitorReminder ? "Enabled" : "Disabled") + "With " + NilTraitorReminder.Count() + " Lines", ServerLog.MessageType.NilMod);
            GameMain.Server.ServerLog.WriteLine("ChatNoneTraitorReminder = " + (ChatNoneTraitorReminder ? "Enabled" : "Disabled") + "With " + NilNoneTraitorReminder.Count() + " Lines", ServerLog.MessageType.NilMod);
            GameMain.Server.ServerLog.WriteLine("ChatShuttleRespawn = " + (ChatShuttleRespawn ? "Enabled" : "Disabled") + "With " + NilShuttleRespawn.Count() + " Lines", ServerLog.MessageType.NilMod);

            GameMain.Server.ServerLog.WriteLine("ChatShuttleLeaving500 = " + (ChatShuttleLeaving500 ? "Enabled" : "Disabled") + "With " + NilShuttleLeaving500.Count() + " Lines", ServerLog.MessageType.NilMod);
            GameMain.Server.ServerLog.WriteLine("ChatShuttleLeaving400 = " + (ChatShuttleLeaving400 ? "Enabled" : "Disabled") + "With " + NilShuttleLeaving400.Count() + " Lines", ServerLog.MessageType.NilMod);
            GameMain.Server.ServerLog.WriteLine("ChatShuttleLeaving300 = " + (ChatShuttleLeaving300 ? "Enabled" : "Disabled") + "With " + NilShuttleLeaving300.Count() + " Lines", ServerLog.MessageType.NilMod);
            GameMain.Server.ServerLog.WriteLine("ChatShuttleLeaving200 = " + (ChatShuttleLeaving200 ? "Enabled" : "Disabled") + "With " + NilShuttleLeaving200.Count() + " Lines", ServerLog.MessageType.NilMod);
            GameMain.Server.ServerLog.WriteLine("ChatShuttleLeaving130 = " + (ChatShuttleLeaving130 ? "Enabled" : "Disabled") + "With " + NilShuttleLeaving130.Count() + " Lines", ServerLog.MessageType.NilMod);
            GameMain.Server.ServerLog.WriteLine("ChatShuttleLeaving100 = " + (ChatShuttleLeaving100 ? "Enabled" : "Disabled") + "With " + NilShuttleLeaving100.Count() + " Lines", ServerLog.MessageType.NilMod);
            GameMain.Server.ServerLog.WriteLine("ChatShuttleLeaving030 = " + (ChatShuttleLeaving030 ? "Enabled" : "Disabled") + "With " + NilShuttleLeaving030.Count() + " Lines", ServerLog.MessageType.NilMod);
            GameMain.Server.ServerLog.WriteLine("ChatShuttleLeaving015 = " + (ChatShuttleLeaving015 ? "Enabled" : "Disabled") + "With " + NilShuttleLeaving015.Count() + " Lines", ServerLog.MessageType.NilMod);

            GameMain.Server.ServerLog.WriteLine("ChatShuttleLeavingKill = " + (ChatShuttleLeavingKill ? "Enabled" : "Disabled") + "With " + NilShuttleLeavingKill.Count() + " Lines", ServerLog.MessageType.NilMod);
            GameMain.Server.ServerLog.WriteLine("ChatSubvsSub = " + (ChatSubvsSub ? "Enabled" : "Disabled") + "With " + NilSubvsSubCoalition.Count() + " Coalition Lines + " + NilSubvsSubRenegade.Count() + " Renegade Lines", ServerLog.MessageType.NilMod);
            GameMain.Server.ServerLog.WriteLine("ChatSalvage = " + (ChatSalvage ? "Enabled" : "Disabled") + "With " + NilSalvage.Count() + " Lines", ServerLog.MessageType.NilMod);
            GameMain.Server.ServerLog.WriteLine("ChatMonster = " + (ChatMonster ? "Enabled" : "Disabled") + "With " + NilMonster.Count() + " Lines", ServerLog.MessageType.NilMod);
            GameMain.Server.ServerLog.WriteLine("ChatCargo = " + (ChatCargo ? "Enabled" : "Disabled") + "With " + NilCargo.Count() + " Lines", ServerLog.MessageType.NilMod);
            GameMain.Server.ServerLog.WriteLine("ChatSandbox = " + (ChatSandbox ? "Enabled" : "Disabled") + "With " + NilSandbox.Count() + " Lines", ServerLog.MessageType.NilMod);
            GameMain.Server.ServerLog.WriteLine("ChatVoteEnd = " + (ChatVoteEnd ? "Enabled" : "Disabled") + "With " + NilVoteEnd.Count() + " Lines", ServerLog.MessageType.NilMod);
        }

        public void Load()
        {
            SetDefault();
            XDocument doc = null;

            if (File.Exists(ChatSavePath))
            {
                doc = XMLExtensions.TryLoadXml(ChatSavePath);
            }
            else
            {
                DebugConsole.ThrowError("NilModChatter config file \"" + ChatSavePath + "\" Does not exist, generating new XML");
                Save();
                doc = XMLExtensions.TryLoadXml(ChatSavePath);
            }
            if (doc == null)
            {
                DebugConsole.ThrowError("NilModChatter config file \"" + ChatSavePath + "\" failed to load.");
            }
            else
            {
                //Chatter Settings
                XElement NilModEventChatterSettings = doc.Root.Element("NilModEventChatterSettings");

                ChatModServerJoin = NilModEventChatterSettings.GetAttributeBool("ChatModServerJoin", false);
                ChatTraitorReminder = NilModEventChatterSettings.GetAttributeBool("ChatTraitorReminder", false);
                ChatNoneTraitorReminder = NilModEventChatterSettings.GetAttributeBool("ChatNoneTraitorReminder", false);
                ChatShuttleRespawn = NilModEventChatterSettings.GetAttributeBool("ChatShuttleRespawn", false);
                ChatShuttleLeaving500 = NilModEventChatterSettings.GetAttributeBool("ChatShuttleLeaving500", false);
                ChatShuttleLeaving400 = NilModEventChatterSettings.GetAttributeBool("ChatShuttleLeaving400", false);
                ChatShuttleLeaving300 = NilModEventChatterSettings.GetAttributeBool("ChatShuttleLeaving300", false);
                ChatShuttleLeaving200 = NilModEventChatterSettings.GetAttributeBool("ChatShuttleLeaving200", false);
                ChatShuttleLeaving130 = NilModEventChatterSettings.GetAttributeBool("ChatShuttleLeaving130", false);
                ChatShuttleLeaving100 = NilModEventChatterSettings.GetAttributeBool("ChatShuttleLeaving100", false);
                ChatShuttleLeaving030 = NilModEventChatterSettings.GetAttributeBool("ChatShuttleLeaving030", false);
                ChatShuttleLeaving015 = NilModEventChatterSettings.GetAttributeBool("ChatShuttleLeaving015", false);
                ChatShuttleLeavingKill = NilModEventChatterSettings.GetAttributeBool("ChatShuttleLeavingKill", false);
                ChatSubvsSub = NilModEventChatterSettings.GetAttributeBool("ChatSubvsSub", false);
                ChatSalvage = NilModEventChatterSettings.GetAttributeBool("ChatSalvage", false);
                ChatMonster = NilModEventChatterSettings.GetAttributeBool("ChatMonster", false);
                ChatCargo = NilModEventChatterSettings.GetAttributeBool("ChatCargo", false);
                ChatSandbox = NilModEventChatterSettings.GetAttributeBool("ChatSandbox", false);
                ChatVoteEnd = NilModEventChatterSettings.GetAttributeBool("ChatVoteEnd", false);

                //Rules + Greeting Text On Lobby Join

                XElement NilModRulesdoc = doc.Root.Element("NilModServerJoin");

                if (NilModRulesdoc != null)
                {
                    NilModRules = new List<string>();
                    foreach (XElement subElement in NilModRulesdoc.Elements())
                    {
                        NilModRules.Add(subElement.GetAttributeString("Text", ""));
                    }
                }

                //Traitor reminder on spawn

                XElement NilTraitorReminderdoc = doc.Root.Element("NilTraitorReminder");

                if (NilTraitorReminderdoc != null)
                {
                    NilTraitorReminder = new List<string>();
                    foreach (XElement subElement in NilTraitorReminderdoc.Elements())
                    {
                        NilTraitorReminder.Add(subElement.GetAttributeString("Text", ""));
                    }
                }

                //Non-Traitor reminder on spawn

                XElement NilNoneTraitorReminderdoc = doc.Root.Element("NilNoneTraitorReminder");

                if (NilNoneTraitorReminderdoc != null)
                {
                    NilNoneTraitorReminder = new List<string>();
                    foreach (XElement subElement in NilNoneTraitorReminderdoc.Elements())
                    {
                        NilNoneTraitorReminder.Add(subElement.GetAttributeString("Text", ""));
                    }
                }

                //Text for respawning players

                XElement NilShuttleRespawndoc = doc.Root.Element("NilShuttleRespawn");

                if (NilShuttleRespawndoc != null)
                {
                    NilShuttleRespawn = new List<string>();
                    foreach (XElement subElement in NilShuttleRespawndoc.Elements())
                    {
                        NilShuttleRespawn.Add(subElement.GetAttributeString("Text", ""));
                    }
                }

                //Text for players when the shuttle has 5 minutes remaining

                XElement NilShuttleLeaving500doc = doc.Root.Element("NilShuttleLeaving500");

                if (NilShuttleLeaving500doc != null)
                {
                    NilShuttleLeaving500 = new List<string>();
                    foreach (XElement subElement in NilShuttleLeaving500doc.Elements())
                    {
                        NilShuttleLeaving500.Add(subElement.GetAttributeString("Text", ""));
                    }
                }

                //Text for players when the shuttle has 4 minutes remaining

                XElement NilShuttleLeaving400doc = doc.Root.Element("NilShuttleLeaving400");

                if (NilShuttleLeaving400doc != null)
                {
                    NilShuttleLeaving400 = new List<string>();
                    foreach (XElement subElement in NilShuttleLeaving400doc.Elements())
                    {
                        NilShuttleLeaving400.Add(subElement.GetAttributeString("Text", ""));
                    }
                }

                //Text for players when the shuttle has 3 minutes remaining

                XElement NilShuttleLeaving300doc = doc.Root.Element("NilShuttleLeaving300");

                if (NilShuttleLeaving300doc != null)
                {
                    NilShuttleLeaving300 = new List<string>();
                    foreach (XElement subElement in NilShuttleLeaving300doc.Elements())
                    {
                        NilShuttleLeaving300.Add(subElement.GetAttributeString("Text", ""));
                    }
                }

                //Text for players when the shuttle has 2 minutes remaining

                XElement NilShuttleLeaving200doc = doc.Root.Element("NilShuttleLeaving200");

                if (NilShuttleLeaving200doc != null)
                {
                    NilShuttleLeaving200 = new List<string>();
                    foreach (XElement subElement in NilShuttleLeaving200doc.Elements())
                    {
                        NilShuttleLeaving200.Add(subElement.GetAttributeString("Text", ""));
                    }
                }

                //Text for players when the shuttle has 1:30 minutes remaining

                XElement NilShuttleLeaving130doc = doc.Root.Element("NilShuttleLeaving130");

                if (NilShuttleLeaving130doc != null)
                {
                    NilShuttleLeaving130 = new List<string>();
                    foreach (XElement subElement in NilShuttleLeaving130doc.Elements())
                    {
                        NilShuttleLeaving130.Add(subElement.GetAttributeString("Text", ""));
                    }
                }

                //Text for players when the shuttle has 1 minutes remaining

                XElement NilShuttleLeaving100doc = doc.Root.Element("NilShuttleLeaving100");

                if (NilShuttleLeaving100doc != null)
                {
                    NilShuttleLeaving100 = new List<string>();
                    foreach (XElement subElement in NilShuttleLeaving100doc.Elements())
                    {
                        NilShuttleLeaving100.Add(subElement.GetAttributeString("Text", ""));
                    }
                }

                //Text for players when the shuttle has 30 seconds remaining

                XElement NilShuttleLeaving030doc = doc.Root.Element("NilShuttleLeaving030");

                if (NilShuttleLeaving030doc != null)
                {
                    NilShuttleLeaving030 = new List<string>();
                    foreach (XElement subElement in NilShuttleLeaving030doc.Elements())
                    {
                        NilShuttleLeaving030.Add(subElement.GetAttributeString("Text", ""));
                    }
                }

                //Text for players when the shuttle has 15 seconds remaining

                XElement NilShuttleLeaving015doc = doc.Root.Element("NilShuttleLeaving015");

                if (NilShuttleLeaving015doc != null)
                {
                    NilShuttleLeaving015 = new List<string>();
                    foreach (XElement subElement in NilShuttleLeaving015doc.Elements())
                    {
                        NilShuttleLeaving015.Add(subElement.GetAttributeString("Text", ""));
                    }
                }

                //Text for players when the shuttle is going to leave and kill its occupants

                XElement NilShuttleLeavingKilldoc = doc.Root.Element("NilShuttleLeavingKill");

                if (NilShuttleLeavingKilldoc != null)
                {
                    NilShuttleLeavingKill = new List<string>();
                    foreach (XElement subElement in NilShuttleLeavingKilldoc.Elements())
                    {
                        NilShuttleLeavingKill.Add(subElement.GetAttributeString("Text", ""));
                    }
                }

                //Text for sub vs sub - Coalition team spawns

                XElement NilSubvsSubCoalitiondoc = doc.Root.Element("NilSubvsSubCoalition");

                if (NilSubvsSubCoalitiondoc != null)
                {
                    NilSubvsSubCoalition = new List<string>();
                    foreach (XElement subElement in NilSubvsSubCoalitiondoc.Elements())
                    {
                        NilSubvsSubCoalition.Add(subElement.GetAttributeString("Text", ""));
                    }
                }

                //Text for sub vs sub - Renegade team spawns

                XElement NilSubvsSubRenegadedoc = doc.Root.Element("NilSubvsSubRenegade");

                if (NilSubvsSubRenegadedoc != null)
                {
                    NilSubvsSubRenegade = new List<string>();
                    foreach (XElement subElement in NilSubvsSubRenegadedoc.Elements())
                    {
                        NilSubvsSubRenegade.Add(subElement.GetAttributeString("Text", ""));
                    }
                }

                XElement NilSalvagedoc = doc.Root.Element("NilSalvage");

                if (NilSalvagedoc != null)
                {
                    NilSalvage = new List<string>();
                    foreach (XElement subElement in NilSalvagedoc.Elements())
                    {
                        NilSalvage.Add(subElement.GetAttributeString("Text", ""));
                    }
                }

                XElement NilMonsterdoc = doc.Root.Element("NilMonster");

                if (NilMonsterdoc != null)
                {
                    NilMonster = new List<string>();
                    foreach (XElement subElement in NilMonsterdoc.Elements())
                    {
                        NilMonster.Add(subElement.GetAttributeString("Text", ""));
                    }
                }

                XElement NilCargodoc = doc.Root.Element("NilCargo");

                if (NilCargodoc != null)
                {
                    NilCargo = new List<string>();
                    foreach (XElement subElement in NilCargodoc.Elements())
                    {
                        NilCargo.Add(subElement.GetAttributeString("Text", ""));
                    }
                }

                XElement NilSandboxdoc = doc.Root.Element("NilSandbox");

                if (NilSandboxdoc != null)
                {
                    NilSandbox = new List<string>();
                    foreach (XElement subElement in NilSandboxdoc.Elements())
                    {
                        NilSandbox.Add(subElement.GetAttributeString("Text", ""));
                    }
                }

                XElement NilVoteEnddoc = doc.Root.Element("NilVoteEnd");

                if (NilVoteEnddoc != null)
                {
                    NilVoteEnd = new List<string>();
                    foreach (XElement subElement in NilVoteEnddoc.Elements())
                    {
                        NilVoteEnd.Add(subElement.GetAttributeString("Text", ""));
                    }
                }

                Save();
            }
        }

        public void Save()
        {
            List<string> lines = new List<string>
            {
                @"<?xml version=""1.0"" encoding=""utf-8"" ?>",

                "",

                "  <!--The chat information below can currently use the following TAGS:-->",
                "  <!--#SERVERNAME #CLIENTNAME #TRAITORTARGET #TRAITORNAME #MISSIONNAME #MISSIONDESC #REWARD #RADARLABEL #STARTLOCATION #ENDLOCATION-->",
                "  <!--Remember that these messages have a maximum size and you should write considering the per-line looks, as well as test for issues-->",
                "  <!--They are sent to specific clients and others do not get sent these messages but if there is enough messages to a single client their spam filter may or may not block them-->",

                "",

                "  <!--ChatModRules = Setting to enable per-client-sending of messages on server-join (configured at bottom of the xml), Default=false-->",
                "  <!--ChatTraitorReminder = Setting to per-client-sending of messages to specifically the traitor on initial spawn (configured at bottom of the xml), Default=false-->",
                "  <!--ChatNoneTraitorReminder = Setting to enable per-client-sending of messages to none-traitors on initial spawn (configured at bottom of the xml), Default=false-->",
                "  <!--ChatShuttleRespawn = Setting to enable per-client-sending of messages on shuttle respawn (configured at bottom of the xml), Default=false-->",
                "  <!--ChatShuttleLeavingKill = Setting to enable per-client-sending of messages if shuttle kills the player by leaving (configured at bottom of the xml), Default=false-->",
                "  <!--ChatShuttleLeaving### = Settings to use when the shuttle transport duration has this long left, in minute # then seconds ##, Default=false-->",
                "  <!--ChatSubvsSub = Setting to enable per-client-sending of the Coalition/Renegade text below (configured at bottom of the xml), Default=false-->",
                "  <!--ChatSalvage = Setting to enable per-client-sending of the text below (configured at bottom of the xml), Default=false-->",
                "  <!--ChatMonster = Setting to enable per-client-sending of the text below (configured at bottom of the xml), Default=false-->",
                "  <!--ChatCargo = Setting to enable per-client-sending of the text below (configured at bottom of the xml), Default=false-->",
                "  <!--ChatSandbox = Setting to enable per-client-sending per-player dialogue (configured at bottom of the xml), Default=false-->",
                "  <!--ChatVoteEnd = Setting to enable per-client-sending per-player dialogue (configured at bottom of the xml), Default=false-->",

                "",

                "<NilModEvents>",
                "  <NilModEventChatterSettings",
                @"    ChatModServerJoin=""" + ChatModServerJoin + @"""",
                @"    ChatTraitorReminder=""" + ChatTraitorReminder + @"""",
                @"    ChatNoneTraitorReminder=""" + ChatNoneTraitorReminder + @"""",
                @"    ChatShuttleRespawn=""" + ChatShuttleRespawn + @"""",
                @"    ChatShuttleLeaving500=""" + ChatShuttleLeaving500 + @"""",
                @"    ChatShuttleLeaving400=""" + ChatShuttleLeaving400 + @"""",
                @"    ChatShuttleLeaving300=""" + ChatShuttleLeaving300 + @"""",
                @"    ChatShuttleLeaving200=""" + ChatShuttleLeaving200 + @"""",
                @"    ChatShuttleLeaving130=""" + ChatShuttleLeaving130 + @"""",
                @"    ChatShuttleLeaving100=""" + ChatShuttleLeaving100 + @"""",
                @"    ChatShuttleLeaving030=""" + ChatShuttleLeaving030 + @"""",
                @"    ChatShuttleLeaving015=""" + ChatShuttleLeaving015 + @"""",
                @"    ChatShuttleLeavingKill=""" + ChatShuttleLeavingKill + @"""",
                @"    ChatSubvsSub=""" + ChatSubvsSub + @"""",
                @"    ChatSalvage=""" + ChatSalvage + @"""",
                @"    ChatMonster=""" + ChatMonster + @"""",
                @"    ChatCargo=""" + ChatCargo + @"""",
                @"    ChatSandbox=""" + ChatSandbox + @"""",
                @"    ChatVoteEnd=""" + ChatVoteEnd + @"""",
                "  />" };

            lines.Add("");

            lines.Add("  <!--This is for the initial On server join messages to inform players of rules, welcome text or otherwise for your server!-->");
            lines.Add("  <NilModServerJoin>");
            foreach (string line in NilModRules)
            {
                lines.Add(@"    <Line Text=""" + line + @"""/>");
            }
            lines.Add("  </NilModServerJoin>");

            lines.Add("");

            lines.Add("  <!--This is the custom text a TRAITOR will see on spawn, it replaces the none-traitor round text.-->");
            lines.Add("  <NilTraitorReminder>");
            foreach (string line in NilTraitorReminder)
            {
                lines.Add(@"    <Line Text=""" + line + @"""/>");
            }
            lines.Add("  </NilTraitorReminder>");

            lines.Add("");

            lines.Add("  <!--This is the custom text a NONE TRAITOR will see on spawn, if it is set to MAYBE or YES (Regardless of traitors)-->");
            lines.Add("  <NilNoneTraitorReminder>");
            foreach (string line in NilNoneTraitorReminder)
            {
                lines.Add(@"    <Line Text=""" + line + @"""/>");
            }
            lines.Add("  </NilNoneTraitorReminder>");

            lines.Add("");

            lines.Add("  <!--This is the text a player will see when respawning via Shuttle-->");
            lines.Add("  <NilShuttleRespawn>");
            foreach (string line in NilShuttleRespawn)
            {
                lines.Add(@"    <Line Text=""" + line + @"""/>");
            }
            lines.Add("  </NilShuttleRespawn>");

            lines.Add("");

            lines.Add("  <!--This is the text a player will see if they have 5 minutes remaining inside the shuttle-->");
            lines.Add("  <NilShuttleLeavingWarn500>");
            foreach (string line in NilShuttleLeaving500)
            {
                lines.Add(@"    <Line Text=""" + line + @"""/>");
            }
            lines.Add("  </NilShuttleLeavingWarn500>");

            lines.Add("");

            lines.Add("  <!--This is the text a player will see if they have 4 minutes remaining inside the shuttle-->");
            lines.Add("  <NilShuttleLeavingWarn400>");
            foreach (string line in NilShuttleLeaving400)
            {
                lines.Add(@"    <Line Text=""" + line + @"""/>");
            }
            lines.Add("  </NilShuttleLeavingWarn400>");

            lines.Add("");

            lines.Add("  <!--This is the text a player will see if they have 3 minutes remaining inside the shuttle-->");
            lines.Add("  <NilShuttleLeavingWarn300>");
            foreach (string line in NilShuttleLeaving300)
            {
                lines.Add(@"    <Line Text=""" + line + @"""/>");
            }
            lines.Add("  </NilShuttleLeavingWarn300>");

            lines.Add("");

            lines.Add("  <!--This is the text a player will see if they have 2 minutes remaining inside the shuttle-->");
            lines.Add("  <NilShuttleLeavingWarn200>");
            foreach (string line in NilShuttleLeaving200)
            {
                lines.Add(@"    <Line Text=""" + line + @"""/>");
            }
            lines.Add("  </NilShuttleLeavingWarn200>");

            lines.Add("");

            lines.Add("  <!--This is the text a player will see if they have 1:30 minutes remaining inside the shuttle-->");
            lines.Add("  <NilShuttleLeavingWarn130>");
            foreach (string line in NilShuttleLeaving130)
            {
                lines.Add(@"    <Line Text=""" + line + @"""/>");
            }
            lines.Add("  </NilShuttleLeavingWarn130>");

            lines.Add("");

            lines.Add("  <!--This is the text a player will see if they have 1 minute remaining inside the shuttle-->");
            lines.Add("  <NilShuttleLeavingWarn100>");
            foreach (string line in NilShuttleLeaving100)
            {
                lines.Add(@"    <Line Text=""" + line + @"""/>");
            }
            lines.Add("  </NilShuttleLeavingWarn100>");

            lines.Add("");

            lines.Add("  <!--This is the text a player will see if they have 30 seconds remaining inside the shuttle-->");
            lines.Add("  <NilShuttleLeavingWarn030>");
            foreach (string line in NilShuttleLeaving030)
            {
                lines.Add(@"    <Line Text=""" + line + @"""/>");
            }
            lines.Add("  </NilShuttleLeavingWarn030>");

            lines.Add("");

            lines.Add("  <!--This is the text a player will see if they have 15 seconds remaining inside the shuttle-->");
            lines.Add("  <NilShuttleLeavingWarn015>");
            foreach (string line in NilShuttleLeaving015)
            {
                lines.Add(@"    <Line Text=""" + line + @"""/>");
            }
            lines.Add("  </NilShuttleLeavingWarn015>");

            lines.Add("");

            lines.Add("  <!--This is the text a player will see if they are killed by staying on a shuttle as it leaves-->");
            lines.Add("  <NilShuttleLeavingKill>");
            foreach (string line in NilShuttleLeavingKill)
            {
                lines.Add(@"    <Line Text=""" + line + @"""/>");
            }
            lines.Add("  </NilShuttleLeavingKill>");

            lines.Add("");

            lines.Add("  <!--This is the text a player will see if its sub vs sub and they are on the Coalition team-->");
            lines.Add("  <NilSubvsSubCoalition>");
            foreach (string line in NilSubvsSubCoalition)
            {
                lines.Add(@"    <Line Text=""" + line + @"""/>");
            }
            lines.Add("  </NilSubvsSubCoalition>");

            lines.Add("");

            lines.Add("  <!--This is the text a player will see if its sub vs sub and they are on the Renegade team-->");
            lines.Add("  <NilSubvsSubRenegade>");
            foreach (string line in NilSubvsSubRenegade)
            {
                lines.Add(@"    <Line Text=""" + line + @"""/>");
            }
            lines.Add("  </NilSubvsSubRenegade>");

            lines.Add("");

            lines.Add("  <!--This is the text a player will see on spawn if the mission is Salvage-->");
            lines.Add("  <NilSalvage>");
            foreach (string line in NilSalvage)
            {
                lines.Add(@"    <Line Text=""" + line + @"""/>");
            }
            lines.Add("  </NilSalvage>");

            lines.Add("");

            lines.Add("  <!--This is the text a player will see on spawn if the mission is Monster-->");
            lines.Add("  <NilMonster>");
            foreach (string line in NilMonster)
            {
                lines.Add(@"    <Line Text=""" + line + @"""/>");
            }
            lines.Add("  </NilMonster>");

            lines.Add("");

            lines.Add("  <!--This is the text a player will see on spawn if the mission is Cargo-->");
            lines.Add("  <NilCargo>");
            foreach (string line in NilModRules)
            {
                lines.Add(@"    <Line Text=""" + line + @"""/>");
            }
            lines.Add("  </NilCargo>");

            lines.Add("");

            lines.Add("  <!--This is the text a player will see on spawn if the Gamemode is Sandbox-->");
            lines.Add("  <NilSandbox>");
            foreach (string line in NilModRules)
            {
                lines.Add(@"    <Line Text=""" + line + @"""/>");
            }
            lines.Add("  </NilSandbox>");

            lines.Add("");

            lines.Add("  <!--Text for players voting end round-->");
            lines.Add("  <NilVoteEnd>");
            foreach (string line in NilModRules)
            {
                lines.Add(@"    <Line Text=""" + line + @"""/>");
            }
            lines.Add("  </NilVoteEnd>");
            lines.Add("</NilModEvents>");

            using (System.IO.StreamWriter file =
            new System.IO.StreamWriter(ChatSavePath, false, Encoding.UTF8))
            {
                foreach (string line in lines)
                {
                    file.WriteLine(line);
                }
            }
        }

        public void SetDefault()
        {
            ChatModServerJoin = false;
            ChatTraitorReminder = false;
            ChatNoneTraitorReminder = false;
            ChatShuttleRespawn = false;
            ChatShuttleLeaving500 = false;
            ChatShuttleLeaving400 = false;
            ChatShuttleLeaving300 = false;
            ChatShuttleLeaving200 = false;
            ChatShuttleLeaving130 = false;
            ChatShuttleLeaving100 = false;
            ChatShuttleLeaving030 = false;
            ChatShuttleLeaving015 = false;
            ChatShuttleLeavingKill = false;
            ChatSubvsSub = false;
            ChatSalvage = false;
            ChatMonster = false;
            ChatCargo = false;
            ChatSandbox = false;
            ChatVoteEnd = false;

            NilModRules = new List<string>();
            NilModRules.Add("Welcome to #SERVERNAME!");
            NilModRules.Add("This is a good place to greet and add your rules!");
            NilModRules.Add("Clients will see this text on join.");

            NilTraitorReminder = new List<string>();
            NilTraitorReminder.Add("You have been handed a secret mission by your fellow Renegade forces!");
            NilTraitorReminder.Add("Your task is to Assassinate #TRAITORTARGET! Though take care in this important endeavour");
            NilTraitorReminder.Add("Take as few Coalition out as possible and make it back in one piece #TRAITORNAME, They must not find out your involvement.");

            NilNoneTraitorReminder = new List<string>();
            NilNoneTraitorReminder.Add("The coalition have potential reports of renegade spies targeting key personnel!");
            NilNoneTraitorReminder.Add("Although it is unknown if they have made it onboard or what their target may be...");
            NilNoneTraitorReminder.Add("The coalition finds it unacceptable to let these scum have their way!");
            NilNoneTraitorReminder.Add("Ensure the submarine reaches its objective and the traitor either hangs or fails.");

            NilShuttleRespawn = new List<string>();
            NilShuttleRespawn.Add("The coalition have sent you useless meatbags as additional backup.");
            NilShuttleRespawn.Add("Locate the submarine and use your provided supplies to aid its mission.");
            NilShuttleRespawn.Add("You only have limited time to disembark the shuttle, we will be disappointed if you should fail us.");

            NilShuttleLeaving500 = new List<string>();
            NilShuttleLeaving500.Add("You have #SHUTTLELEAVETIME to reach the main submarine and disembark.");

            NilShuttleLeaving400 = new List<string>();
            NilShuttleLeaving400.Add("You have #SHUTTLELEAVETIME to reach the main submarine and disembark.");

            NilShuttleLeaving300 = new List<string>();
            NilShuttleLeaving300.Add("You have #SHUTTLELEAVETIME to reach the main submarine and disembark.");

            NilShuttleLeaving200 = new List<string>();
            NilShuttleLeaving200.Add("You have #SHUTTLELEAVETIME to reach the main submarine and disembark.");

            NilShuttleLeaving130 = new List<string>();
            NilShuttleLeaving130.Add("You have #SHUTTLELEAVETIME to reach the main submarine and disembark.");

            NilShuttleLeaving100 = new List<string>();
            NilShuttleLeaving100.Add("You have #SHUTTLELEAVETIME to reach the main submarine and disembark.");

            NilShuttleLeaving030 = new List<string>();
            NilShuttleLeaving030.Add("You have #SHUTTLELEAVETIME to reach the main submarine and disembark.");

            NilShuttleLeaving015 = new List<string>();
            NilShuttleLeaving015.Add("You only have #SHUTTLELEAVETIME to reach the main submarine and disembark!");
            NilShuttleLeaving015.Add("You must leave before the shuttle returns or we will throw you in the drink for insubordination!");

            NilShuttleLeavingKill = new List<string>();
            NilShuttleLeavingKill.Add("Cowardess is not tolerated by the coalition #CLIENTNAME.");
            NilShuttleLeavingKill.Add("You will be sent back into the drink, Fish food or otherwise...");
            NilShuttleLeavingKill.Add("(Next time examine a shuttle for invisible suits, supplies and disembark before the timer ends!)");

            NilSubvsSubCoalition = new List<string>();
            NilSubvsSubCoalition.Add("A renegade vessel has been located in the nearby area, Remove the subversive elements.");
            NilSubvsSubCoalition.Add("Gear up and use sonar to find the Renegade sub, then shoot, board and do anything it takes.");
            NilSubvsSubCoalition.Add("Failiure is not an option.");

            NilSubvsSubRenegade = new List<string>();
            NilSubvsSubRenegade.Add("A Nearby coalition sub has likely identified we are not with the coalition, dispose of them!");
            NilSubvsSubRenegade.Add("Gear up and use sonar to find the Coalition sub, then shoot, board and do anything it takes.");
            NilSubvsSubRenegade.Add("Failiure is not an option.");

            NilSalvage = new List<string>();
            NilSalvage.Add("#CLIENTNAME! You have been employed by the coalition to embark from #STARTLOCATION and collect an artifact!");
            NilSalvage.Add("Gear up into your diving suits and use a portable Sonar to locate the #RADARLABEL");
            NilSalvage.Add("You will be compensated with #REWARD Credits to divy up amongst your fellow crewmates.");
            NilSalvage.Add("Provided you successfully get our artifact to #ENDLOCATION without losing it.");
            NilSalvage.Add("Some artifacts are very dangerous, Great care is to be taken depending on its type.");

            NilMonster = new List<string>();
            NilMonster.Add("#CLIENTNAME! You have been employed by the coalition to embark from #STARTLOCATION for monster patrol!");
            NilMonster.Add("Prepare your submarine for combat and reach the designated target: #RADARLABEL");
            NilMonster.Add("You will be compensated with #REWARD Credits to divy up amongst your fellow crewmates.");
            NilMonster.Add("Provided you successfully survive the ordeal and actually reach #ENDLOCATION with the submarine intact");
            NilMonster.Add("The coalition is not in the business of losing submarines, It is unacceptable to return without it.");

            NilCargo = new List<string>();
            NilCargo.Add("#CLIENTNAME! You have been employed by the coalition to embark from #STARTLOCATION for a Cargo run");
            NilCargo.Add("Simply reach #ENDLOCATION without losing the cargo.");
            NilCargo.Add("You will be compensated with #REWARD Credits to divy up amongst your fellow crewmates.");
            NilCargo.Add("Consider it an almost free meal and paycheck for this simple work.");

            NilSandbox = new List<string>();
            NilSandbox.Add("#CLIENTNAME! Welcome to sandbox mode.");
            NilSandbox.Add("No Goals, No paychecks, no respawning fishies im afraid(They spawn once per level generation)");
            NilSandbox.Add("When your bored of this feel free to hit the vote end at the top right");
            NilSandbox.Add("Simply reach #ENDLOCATION alive.");

            NilVoteEnd = new List<string>();
            NilVoteEnd.Add("#CLIENTNAME you and your crew are dishonerable cowards! x:");
        }

        //Code related stuff for sending messages
        public void SendServerMessage(string MessageToSend, Client clientreceiver)
        {
            string RefinedMessage = MessageToSend.Trim();

            if (RefinedMessage.Contains("#"))
            {
                if (RefinedMessage.Contains("#SERVERNAME"))
                {
                    RefinedMessage = RefinedMessage.Replace("#SERVERNAME", GameMain.Server.Name);
                }
                if (RefinedMessage.Contains("#CLIENTNAME"))
                {
                    if (clientreceiver != null)
                    {
                        RefinedMessage = RefinedMessage.Replace("#CLIENTNAME", clientreceiver.Name);
                    }
                    else
                    {
                        if (GameMain.Server.CharacterInfo != null)
                        {
                            RefinedMessage = RefinedMessage.Replace("#CLIENTNAME", GameMain.Server.CharacterInfo.Name);
                        }
                        else
                        {
                            RefinedMessage = RefinedMessage.Replace("#CLIENTNAME", "NA");
                        }
                    }
                }
                if (RefinedMessage.Contains("#TRAITORTARGET"))
                {
                    if (GameMain.Server.TraitorManager.TraitorList.Find(t => clientreceiver.Character == t.Character) != null)
                    {
                        Traitor traitor = GameMain.Server.TraitorManager.TraitorList.Find(t => clientreceiver.Character == t.Character);
                        RefinedMessage = RefinedMessage.Replace("#TRAITORTARGET", traitor.TargetCharacter.Name);
                    }
                    else
                    {
                        RefinedMessage = RefinedMessage.Replace("#TRAITORTARGET", "REDACTED");
                    }

                }
                if (RefinedMessage.Contains("#TRAITORNAME"))
                {
                    if (GameMain.Server.TraitorManager.TraitorList.Find(t => clientreceiver.Character == t.Character) != null)
                    {
                        Traitor traitor = GameMain.Server.TraitorManager.TraitorList.Find(t => clientreceiver.Character == t.Character);
                        RefinedMessage = RefinedMessage.Replace("#TRAITORNAME", traitor.Character.Name);
                    }
                }
                if (RefinedMessage.Contains("#SHUTTLELEAVETIME"))
                {
                    RefinedMessage = RefinedMessage.Replace("#SHUTTLELEAVETIME", ToolBox.SecondsToReadableTime(GameMain.Server.respawnManager.TransportTimer));
                }
                if (RefinedMessage.Contains("#MISSIONNAME"))
                {
                    RefinedMessage = RefinedMessage.Replace("#MISSIONNAME", GameMain.GameSession.Mission.Name);
                }
                if (RefinedMessage.Contains("#MISSIONDESC"))
                {
                    RefinedMessage = RefinedMessage.Replace("#MISSIONDESC", GameMain.GameSession.Mission.Description);
                }
                if (RefinedMessage.Contains("#REWARD"))
                {
                    int reward = Convert.ToInt32((Math.Round(GameMain.NilMod.CampaignSurvivalReward + GameMain.NilMod.CampaignBonusMissionReward + (GameMain.GameSession.Mission.Reward * GameMain.NilMod.CampaignBaseRewardMultiplier), 0)));
                    RefinedMessage = RefinedMessage.Replace("#REWARD", reward.ToString());
                }
                if (RefinedMessage.Contains("#RADARLABEL"))
                {
                    RefinedMessage = RefinedMessage.Replace("#RADARLABEL", GameMain.GameSession.Mission.RadarLabel);
                }
                if (RefinedMessage.Contains("#STARTLOCATION"))
                {
                    RefinedMessage = RefinedMessage.Replace("#STARTLOCATION", GameMain.GameSession.StartLocation.Name);
                }
                if (RefinedMessage.Contains("#ENDLOCATION"))
                {
                    RefinedMessage = RefinedMessage.Replace("#ENDLOCATION", GameMain.GameSession.EndLocation.Name);
                }
            }



            if (clientreceiver != null)
            {
                var chatMsg = ChatMessage.Create(
                null,
                RefinedMessage,
                (ChatMessageType)ChatMessageType.Server,
                null);

                GameMain.Server.SendChatMessage(chatMsg, clientreceiver);
            }
            else
            {
                //Local Host Chat code here
                if (Character.Controlled != null)
                {
                    GameMain.NetworkMember.AddChatMessage(RefinedMessage, ChatMessageType.Server);
                }
            }
        }

        public void RoundStartClientMessages(Client receivingclient)
        {
            //Barotrauma.MonsterMission
            //Barotrauma.CombatMission
            //Barotrauma.CargoMission
            //Barotrauma.SalvageMission

            //An Actual Mission
            if (GameMain.GameSession.Mission != null)
            {
                //GameMain.NilMod.SendServerMessage("This Missions name is: " + GameMain.GameSession.Mission.ToString(), receivingclient);

                //Combat Mission code
                if (GameMain.GameSession.Mission.ToString() == "Barotrauma.CombatMission")
                {
                    if (receivingclient.TeamID == 1)
                    {
                        if (NilMod.NilModEventChatter.NilSubvsSubCoalition.Count() > 0 && NilMod.NilModEventChatter.ChatSubvsSub == true)
                        {
                            foreach (string message in NilMod.NilModEventChatter.NilSubvsSubCoalition)
                            {
                                NilMod.NilModEventChatter.SendServerMessage(message, receivingclient);
                            }
                        }
                    }
                    if (receivingclient.TeamID == 2)
                    {
                        if (NilMod.NilModEventChatter.NilSubvsSubRenegade.Count() > 0 && NilMod.NilModEventChatter.ChatSubvsSub == true)
                        {
                            foreach (string message in NilMod.NilModEventChatter.NilSubvsSubRenegade)
                            {
                                NilMod.NilModEventChatter.SendServerMessage(message, receivingclient);
                            }
                        }
                    }
                }
                //Monster Mission code
                if (GameMain.GameSession.Mission.ToString() == "Barotrauma.MonsterMission")
                {
                    if (NilMod.NilModEventChatter.NilMonster.Count() > 0 && NilMod.NilModEventChatter.ChatMonster == true)
                    {
                        foreach (string message in NilMod.NilModEventChatter.NilMonster)
                        {
                            NilMod.NilModEventChatter.SendServerMessage(message, receivingclient);
                        }
                    }
                }
                //Salvage Mission code
                if (GameMain.GameSession.Mission.ToString() == "Barotrauma.SalvageMission")
                {
                    if (NilMod.NilModEventChatter.NilSalvage.Count() > 0 && NilMod.NilModEventChatter.ChatSalvage == true)
                    {
                        foreach (string message in NilMod.NilModEventChatter.NilSalvage)
                        {
                            NilMod.NilModEventChatter.SendServerMessage(message, receivingclient);
                        }
                    }
                }
                //Cargo Mission code
                if (GameMain.GameSession.Mission.ToString() == "Barotrauma.CargoMission")
                {
                    if (NilMod.NilModEventChatter.NilCargo.Count() > 0 && NilMod.NilModEventChatter.ChatCargo == true)
                    {
                        foreach (string message in NilMod.NilModEventChatter.NilCargo)
                        {
                            NilMod.NilModEventChatter.SendServerMessage(message, receivingclient);
                        }
                    }
                }

            }
            //Sandbox Mode
            else
            {
                if (NilMod.NilModEventChatter.NilSandbox.Count() > 0 && NilMod.NilModEventChatter.ChatSandbox == true)
                {
                    foreach (string message in NilMod.NilModEventChatter.NilSandbox)
                    {
                        NilMod.NilModEventChatter.SendServerMessage(message, receivingclient);
                    }
                }
            }


            //Traitor Reminder Code

            if (GameMain.Server.TraitorsEnabled == YesNoMaybe.Yes | GameMain.Server.TraitorsEnabled == YesNoMaybe.Maybe)
            {
                if (receivingclient.Name == GameMain.NilMod.Traitor)
                {
                    if (NilMod.NilModEventChatter.NilTraitorReminder.Count() > 0 && NilMod.NilModEventChatter.ChatTraitorReminder == true)
                    {
                        foreach (string message in NilMod.NilModEventChatter.NilTraitorReminder)
                        {
                            NilMod.NilModEventChatter.SendServerMessage(message, receivingclient);
                        }
                    }
                }
                else
                {
                    if (NilMod.NilModEventChatter.NilNoneTraitorReminder.Count() > 0 && NilMod.NilModEventChatter.ChatNoneTraitorReminder == true)
                    {
                        foreach (string message in NilMod.NilModEventChatter.NilNoneTraitorReminder)
                        {
                            NilMod.NilModEventChatter.SendServerMessage(message, receivingclient);
                        }
                    }
                }
            }
        }

        public void SendRespawnLeavingWarning(float timeremaining)
        {
            foreach (Client client in GameMain.Server.ConnectedClients)
            {
                if (client.Character != null)
                {
                    if (client.Character.Submarine == GameMain.Server.respawnManager.respawnShuttle && client.Character.Enabled)
                    {
                        switch (timeremaining)
                        {
                            case 15f:
                                if (NilMod.NilModEventChatter.NilShuttleLeaving015.Count() > 0)
                                {
                                    foreach (string message in NilMod.NilModEventChatter.NilShuttleLeaving015)
                                    {
                                        SendServerMessage(message, client);
                                    }
                                }
                                break;
                            case 30f:
                                if (NilMod.NilModEventChatter.NilShuttleLeaving030.Count() > 0)
                                {
                                    foreach (string message in NilMod.NilModEventChatter.NilShuttleLeaving030)
                                    {
                                        SendServerMessage(message, client);
                                    }
                                }
                                break;
                            case 60f:
                                if (NilMod.NilModEventChatter.NilShuttleLeaving100.Count() > 0)
                                {
                                    foreach (string message in NilMod.NilModEventChatter.NilShuttleLeaving100)
                                    {
                                        SendServerMessage(message, client);
                                    }
                                }
                                break;
                            case 90f:
                                if (NilMod.NilModEventChatter.NilShuttleLeaving130.Count() > 0)
                                {
                                    foreach (string message in NilMod.NilModEventChatter.NilShuttleLeaving130)
                                    {
                                        SendServerMessage(message, client);
                                    }
                                }
                                break;
                            case 120f:
                                if (NilMod.NilModEventChatter.NilShuttleLeaving200.Count() > 0)
                                {
                                    foreach (string message in NilMod.NilModEventChatter.NilShuttleLeaving200)
                                    {
                                        SendServerMessage(message, client);
                                    }
                                }
                                break;
                            case 180f:
                                if (NilMod.NilModEventChatter.NilShuttleLeaving300.Count() > 0)
                                {
                                    foreach (string message in NilMod.NilModEventChatter.NilShuttleLeaving300)
                                    {
                                        SendServerMessage(message, client);
                                    }
                                }
                                break;
                            case 240f:
                                if (NilMod.NilModEventChatter.NilShuttleLeaving400.Count() > 0)
                                {
                                    foreach (string message in NilMod.NilModEventChatter.NilShuttleLeaving400)
                                    {
                                        SendServerMessage(message, client);
                                    }
                                }
                                break;
                            case 300f:
                                if (NilMod.NilModEventChatter.NilShuttleLeaving500.Count() > 0)
                                {
                                    foreach (string message in NilMod.NilModEventChatter.NilShuttleLeaving500)
                                    {
                                        SendServerMessage(message, client);
                                    }
                                }
                                break;
                        }
                    }
                }
            }
            //Hosts character code
            if (Character.Controlled != null)
            {
                if (Character.Controlled.Submarine == GameMain.Server.respawnManager.respawnShuttle && Character.Controlled.Enabled)
                {
                    switch (timeremaining)
                    {
                        case 15f:
                            if (NilMod.NilModEventChatter.NilShuttleLeaving015.Count() > 0)
                            {
                                foreach (string message in NilMod.NilModEventChatter.NilShuttleLeaving015)
                                {
                                    SendServerMessage(message, null);
                                }
                            }
                            break;
                        case 30f:
                            if (NilMod.NilModEventChatter.NilShuttleLeaving030.Count() > 0)
                            {
                                foreach (string message in NilMod.NilModEventChatter.NilShuttleLeaving030)
                                {
                                    SendServerMessage(message, null);
                                }
                            }
                            break;
                        case 60f:
                            if (NilMod.NilModEventChatter.NilShuttleLeaving100.Count() > 0)
                            {
                                foreach (string message in NilMod.NilModEventChatter.NilShuttleLeaving100)
                                {
                                    SendServerMessage(message, null);
                                }
                            }
                            break;
                        case 90f:
                            if (NilMod.NilModEventChatter.NilShuttleLeaving130.Count() > 0)
                            {
                                foreach (string message in NilMod.NilModEventChatter.NilShuttleLeaving130)
                                {
                                    SendServerMessage(message, null);
                                }
                            }
                            break;
                        case 120f:
                            if (NilMod.NilModEventChatter.NilShuttleLeaving200.Count() > 0)
                            {
                                foreach (string message in NilMod.NilModEventChatter.NilShuttleLeaving200)
                                {
                                    SendServerMessage(message, null);
                                }
                            }
                            break;
                        case 180f:
                            if (NilMod.NilModEventChatter.NilShuttleLeaving300.Count() > 0)
                            {
                                foreach (string message in NilMod.NilModEventChatter.NilShuttleLeaving300)
                                {
                                    SendServerMessage(message, null);
                                }
                            }
                            break;
                        case 240f:
                            if (NilMod.NilModEventChatter.NilShuttleLeaving400.Count() > 0)
                            {
                                foreach (string message in NilMod.NilModEventChatter.NilShuttleLeaving400)
                                {
                                    SendServerMessage(message, null);
                                }
                            }
                            break;
                        case 300f:
                            if (NilMod.NilModEventChatter.NilShuttleLeaving500.Count() > 0)
                            {
                                foreach (string message in NilMod.NilModEventChatter.NilShuttleLeaving500)
                                {
                                    SendServerMessage(message, null);
                                }
                            }
                            break;
                    }
                }
            }
        }

        public void SendHostMessages()
        {

            //Barotrauma.MonsterMission
            //Barotrauma.CombatMission
            //Barotrauma.CargoMission
            //Barotrauma.SalvageMission

            //An Actual Mission
            if (GameMain.GameSession.Mission != null)
            {
                //GameMain.NilMod.SendServerMessage("This Missions name is: " + GameMain.GameSession.Mission.ToString(), receivingclient);

                //Combat Mission code
                if (GameMain.GameSession.Mission.ToString() == "Barotrauma.CombatMission")
                {
                    if (GameMain.Server.CharacterInfo != null)
                    {
                        if (GameMain.Server.CharacterInfo.Character.TeamID == 1)
                        {
                            if (NilMod.NilModEventChatter.NilSubvsSubCoalition.Count() > 0 && NilMod.NilModEventChatter.ChatSubvsSub == true)
                            {
                                foreach (string message in NilMod.NilModEventChatter.NilSubvsSubCoalition)
                                {
                                    NilMod.NilModEventChatter.SendServerMessage(message, null);
                                }
                            }
                        }
                    }
                }
                //Monster Mission code
                if (GameMain.GameSession.Mission.ToString() == "Barotrauma.MonsterMission")
                {
                    if (NilMod.NilModEventChatter.NilMonster.Count() > 0 && NilMod.NilModEventChatter.ChatMonster == true)
                    {
                        foreach (string message in NilMod.NilModEventChatter.NilMonster)
                        {
                            NilMod.NilModEventChatter.SendServerMessage(message, null);
                        }
                    }
                }
                //Salvage Mission code
                if (GameMain.GameSession.Mission.ToString() == "Barotrauma.SalvageMission")
                {
                    if (NilMod.NilModEventChatter.NilSalvage.Count() > 0 && NilMod.NilModEventChatter.ChatSalvage == true)
                    {
                        foreach (string message in NilMod.NilModEventChatter.NilSalvage)
                        {
                            NilMod.NilModEventChatter.SendServerMessage(message, null);
                        }
                    }
                }
                //Cargo Mission code
                if (GameMain.GameSession.Mission.ToString() == "Barotrauma.CargoMission")
                {
                    if (NilMod.NilModEventChatter.NilCargo.Count() > 0 && NilMod.NilModEventChatter.ChatCargo == true)
                    {
                        foreach (string message in NilMod.NilModEventChatter.NilCargo)
                        {
                            NilMod.NilModEventChatter.SendServerMessage(message, null);
                        }
                    }
                }

            }
            //Sandbox Mode
            else
            {
                if (NilMod.NilModEventChatter.NilSandbox.Count() > 0 && NilMod.NilModEventChatter.ChatSandbox == true)
                {
                    foreach (string message in NilMod.NilModEventChatter.NilSandbox)
                    {
                        NilMod.NilModEventChatter.SendServerMessage(message, null);
                    }
                }
            }


            //Traitor Reminder Code

            if (Character.Controlled != null)
            {
                if (GameMain.Server.TraitorsEnabled == YesNoMaybe.Yes | GameMain.Server.TraitorsEnabled == YesNoMaybe.Maybe)
                {
                    if (Character.Controlled.Name == GameMain.NilMod.Traitor)
                    {
                        if (NilMod.NilModEventChatter.NilTraitorReminder.Count() > 0 && NilMod.NilModEventChatter.ChatTraitorReminder == true)
                        {
                            foreach (string message in NilMod.NilModEventChatter.NilTraitorReminder)
                            {
                                SendServerMessage(message, null);
                            }
                        }
                    }
                    else
                    {
                        if (NilMod.NilModEventChatter.NilNoneTraitorReminder.Count() > 0 && NilMod.NilModEventChatter.ChatNoneTraitorReminder == true)
                        {
                            foreach (string message in NilMod.NilModEventChatter.NilNoneTraitorReminder)
                            {
                                NilMod.NilModEventChatter.SendServerMessage(message, null);
                            }
                        }
                    }
                }
            }

        }
    }
}
