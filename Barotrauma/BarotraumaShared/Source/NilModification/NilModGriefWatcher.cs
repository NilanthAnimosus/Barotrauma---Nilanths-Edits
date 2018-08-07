using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Barotrauma.Networking;
using Microsoft.Xna.Framework;
using System.Xml.Linq;
using System.IO;

namespace Barotrauma
{
    class NilModGriefWatcher
    {
        public const string GriefWatchSavePath = "Data/NilMod/GriefWatcher.xml";

        //These two are for when placing an explosive in a detonator, wiring any, etc)
        public List<String> GWListDetonators;
        public List<String> GWListExplosives;

        //For thrown objects that are bad to throw
        public List<String> GWListThrown;

        //Diving masks and diving suits, if they contain any of the hazardous or a hazardous is added
        //and is not their own inventory, their probably using it to kill/grief.
        public List<String> GWListMaskItems;
        public List<String> GWListMaskHazardous;

        //critical devices that should not be unwired (Pumps, oxygen generators, reactors, etc).
        public List<String> GWListWireKeyDevices;

        //Junctions, relays, anything "Worth watching"
        public List<String> GWListWireJunctions;

        //Items that count as syringes, and what shouldn't be placed into them normally (Sufforin/morb/etc)
        public List<String> GWListSyringes;
        public List<String> GWListSyringechems;

        //Guns that have been loaded with bad ammo types
        public List<String> GWListRanged;
        public List<String> GWListRangedAmmo;

        //Railgun objects for loading / what ammo is considered significant to load/shoot
        public List<String> GWListRailgunLaunch;
        public List<String> GWListRailgunRacks;
        public List<String> GWListRailgunAmmo;

        //Things that are self used, yet bad. incase you can self-use husk eggs in a mod or such
        public List<String> GWListUse;

        //This is more for items that are used directly, like a bandage. incase needed
        public List<String> GWListMeleeOther;

        //Items that when being created should be considered bad
        public List<String> GWListFabricated;

        //Placing items that are considered cuffs/restrictive on other players could be stated.
        public List<String> GWListHandcuffs;

        public string GriefWatchName = "GW-AI";
        public Boolean ExcludeTraitors;
        public Boolean AdminsOnly;
        public Boolean KeepTeamSpecific;
        public Boolean ReactorAutoTempOff;
        public Boolean ReactorShutDownTemp;
        public Boolean ReactorStateLastBlamed;
        public Boolean ReactorFissionBeyondAuto;
        public Boolean ReactorLastFuelRemoved;
        public float ReactorLastFuelRemovedTimer;
        public Boolean WallBreachOutside90;
        public Boolean WallBreachOutside75;
        public Boolean WallBreachOutside50;
        public Boolean WallBreachOutside25;
        public Boolean WallBreachOutside0;
        public Boolean WallBreachInside90;
        public Boolean WallBreachInside75;
        public Boolean WallBreachInside50;
        public Boolean WallBreachInside25;
        public Boolean WallBreachInside0;
        public Boolean AirlockLeftOpen;
        public float AirlockLeftOpenTimer;
        public float AirlockLeftOpenRespawnMult;
        public Boolean DoorBroken;
        public Boolean DoorStuck;
        public Boolean PlayerIncapaciteDamage;
        public Boolean PlayerIncapaciteBleed;
        public Boolean PlayerIncapaciteOxygen;
        public Boolean PlayerTakeIDOffLiving;
        public Boolean PumpPositive;
        public float PumpWarnTimer;
        public Boolean PumpOff;

        public void ReportSettings()
        {

        }

        public void Load()
        {
            XDocument doc = null;

            if (File.Exists(GriefWatchSavePath))
            {
                doc = XMLExtensions.TryLoadXml(GriefWatchSavePath);
            }
            else
            {
                DebugConsole.ThrowError("NilModGriefWatcher config file \"" + GriefWatchSavePath + "\" Does not exist, generating default XML");
                SetDefault();
                Save();
                doc = XMLExtensions.TryLoadXml(GriefWatchSavePath);
            }

            if (doc == null)
            {
                DebugConsole.ThrowError("NilModGriefWatcher config file \"" + GriefWatchSavePath + "\" failed to load. Disabling Grief Watcher.");
                GameMain.NilMod.EnableGriefWatcher = false;
            }
            else
            {
                SetDefault();

                XElement NilModGriefWatchSettings = doc.Root.Element("NilModGriefWatchSettings");

                if (NilModGriefWatchSettings != null)
                {
                    GriefWatchName = NilModGriefWatchSettings.GetAttributeString("GriefWatchName", "GW-AI");
                    ExcludeTraitors = NilModGriefWatchSettings.GetAttributeBool("ExcludeTraitors", true);
                    AdminsOnly = NilModGriefWatchSettings.GetAttributeBool("AdminsOnly", false);
                    KeepTeamSpecific = NilModGriefWatchSettings.GetAttributeBool("KeepTeamSpecific", true);
                    ReactorAutoTempOff = NilModGriefWatchSettings.GetAttributeBool("ReactorAutoTempOff", true);
                    ReactorShutDownTemp = NilModGriefWatchSettings.GetAttributeBool("ReactorShutDownTemp", true);
                    ReactorStateLastBlamed = NilModGriefWatchSettings.GetAttributeBool("ReactorStateLastBlamed", true);
                    ReactorFissionBeyondAuto = NilModGriefWatchSettings.GetAttributeBool("ReactorFissionBeyondAuto", true);
                    ReactorLastFuelRemoved = NilModGriefWatchSettings.GetAttributeBool("ReactorLastFuelRemoved", true);
                    ReactorLastFuelRemovedTimer = MathHelper.Clamp(NilModGriefWatchSettings.GetAttributeFloat("ReactorLastFuelRemovedTimer", 6f), 0f, 15f);
                    WallBreachOutside90 = NilModGriefWatchSettings.GetAttributeBool("WallBreachOutside90", true);
                    WallBreachOutside75 = NilModGriefWatchSettings.GetAttributeBool("WallBreachOutside75", true);
                    WallBreachOutside50 = NilModGriefWatchSettings.GetAttributeBool("WallBreachOutside50", true);
                    WallBreachOutside25 = NilModGriefWatchSettings.GetAttributeBool("WallBreachOutside25", true);
                    WallBreachOutside0 = NilModGriefWatchSettings.GetAttributeBool("WallBreachOutside0", true);
                    WallBreachInside90 = NilModGriefWatchSettings.GetAttributeBool("WallBreachInside90", true);
                    WallBreachInside75 = NilModGriefWatchSettings.GetAttributeBool("WallBreachInside75", true);
                    WallBreachInside50 = NilModGriefWatchSettings.GetAttributeBool("WallBreachInside50", true);
                    WallBreachInside25 = NilModGriefWatchSettings.GetAttributeBool("WallBreachInside25", true);
                    WallBreachInside0 = NilModGriefWatchSettings.GetAttributeBool("WallBreachInside0", true);
                    AirlockLeftOpen = NilModGriefWatchSettings.GetAttributeBool("AirlockLeftOpen", true);
                    AirlockLeftOpenTimer = MathHelper.Clamp(NilModGriefWatchSettings.GetAttributeFloat("AirlockLeftOpenTimer", 10f), 3f, 30f);
                    AirlockLeftOpenRespawnMult = MathHelper.Clamp(NilModGriefWatchSettings.GetAttributeFloat("AirlockLeftOpenRespawnMult", 0.5f), 0.05f, 1f);
                    DoorBroken = NilModGriefWatchSettings.GetAttributeBool("DoorBroken", true);
                    DoorStuck = NilModGriefWatchSettings.GetAttributeBool("DoorStuck", true);
                    PlayerIncapaciteDamage = NilModGriefWatchSettings.GetAttributeBool("PlayerIncapaciteDamage", true);
                    PlayerIncapaciteBleed = NilModGriefWatchSettings.GetAttributeBool("PlayerIncapaciteBleed", true);
                    PlayerIncapaciteOxygen = NilModGriefWatchSettings.GetAttributeBool("PlayerIncapaciteOxygen", true);
                    PlayerTakeIDOffLiving = NilModGriefWatchSettings.GetAttributeBool("PlayerTakeIDOffLiving", true);
                    PumpWarnTimer = MathHelper.Clamp(NilModGriefWatchSettings.GetAttributeFloat("PumpWarnTimer", 6f), 0.2f, 30f);
                    PumpPositive = NilModGriefWatchSettings.GetAttributeBool("PumpPositive", true);
                    PumpOff = NilModGriefWatchSettings.GetAttributeBool("PumpOff", true);
                }

                XElement GWListDetonatorsdoc = doc.Root.Element("GWListDetonators");
                if (GWListDetonatorsdoc != null)
                {
                    GWListDetonators = new List<string>();

                    if (GWListDetonatorsdoc.Elements().Count() > 0)
                    {
                        foreach (XElement subElement in GWListDetonatorsdoc.Elements())
                        {
                            GWListDetonators.Add(subElement.GetAttributeString("name", ""));
                        }
                    }
                }

                XElement GWListExplosivesdoc = doc.Root.Element("GWListExplosives");
                if (GWListExplosivesdoc != null)
                {
                    GWListExplosives = new List<string>();

                    if (GWListExplosivesdoc.Elements().Count() > 0)
                    {
                        foreach (XElement subElement in GWListExplosivesdoc.Elements())
                        {
                            GWListExplosives.Add(subElement.GetAttributeString("name", ""));
                        }
                    }
                }

                XElement GWListThrowndoc = doc.Root.Element("GWListThrown");
                if (GWListThrowndoc != null)
                {
                    GWListThrown = new List<string>();

                    if (GWListThrowndoc.Elements().Count() > 0)
                    {
                        foreach (XElement subElement in GWListThrowndoc.Elements())
                        {
                            GWListThrown.Add(subElement.GetAttributeString("name", ""));
                        }
                    }
                }

                XElement GWListMaskItemsdoc = doc.Root.Element("GWListMaskItems");
                if (GWListMaskItemsdoc != null)
                {
                    GWListMaskItems = new List<string>();

                    if (GWListMaskItemsdoc.Elements().Count() > 0)
                    {
                        foreach (XElement subElement in GWListMaskItemsdoc.Elements())
                        {
                            GWListMaskItems.Add(subElement.GetAttributeString("name", ""));
                        }
                    }
                }

                XElement GWListMaskHazardousdoc = doc.Root.Element("GWListMaskHazardous");
                if (GWListMaskHazardousdoc != null)
                {
                    GWListMaskHazardous = new List<string>();

                    if (GWListMaskHazardousdoc.Elements().Count() > 0)
                    {
                        foreach (XElement subElement in GWListMaskHazardousdoc.Elements())
                        {
                            GWListMaskHazardous.Add(subElement.GetAttributeString("name", ""));
                        }
                    }
                }

                XElement GWListWireKeyDevicesdoc = doc.Root.Element("GWListWireKeyDevices");
                if (GWListWireKeyDevicesdoc != null)
                {
                    GWListWireKeyDevices = new List<string>();

                    if (GWListWireKeyDevicesdoc.Elements().Count() > 0)
                    {
                        foreach (XElement subElement in GWListWireKeyDevicesdoc.Elements())
                        {
                            GWListWireKeyDevices.Add(subElement.GetAttributeString("name", ""));
                        }
                    }
                }

                XElement GWListWireJunctionsdoc = doc.Root.Element("GWListWireJunctions");
                if (GWListWireJunctionsdoc != null)
                {
                    GWListWireJunctions = new List<string>();

                    if (GWListWireJunctionsdoc.Elements().Count() > 0)
                    {
                        foreach (XElement subElement in GWListWireJunctionsdoc.Elements())
                        {
                            GWListWireJunctions.Add(subElement.GetAttributeString("name", ""));
                        }
                    }
                }

                XElement GWListSyringesdoc = doc.Root.Element("GWListSyringes");
                if (GWListSyringesdoc != null)
                {
                    GWListSyringes = new List<string>();

                    if (GWListSyringesdoc.Elements().Count() > 0)
                    {
                        foreach (XElement subElement in GWListSyringesdoc.Elements())
                        {
                            GWListSyringes.Add(subElement.GetAttributeString("name", ""));
                        }
                    }
                }

                //Text for players when the shuttle has 30 seconds remaining

                XElement GWListSyringechemsdoc = doc.Root.Element("GWListSyringechems");
                if (GWListSyringechemsdoc != null)
                {
                    GWListSyringechems = new List<string>();

                    if (GWListSyringechemsdoc.Elements().Count() > 0)
                    {
                        foreach (XElement subElement in GWListSyringechemsdoc.Elements())
                        {
                            GWListSyringechems.Add(subElement.GetAttributeString("name", ""));
                        }
                    }
                }

                //Text for players when the shuttle has 15 seconds remaining

                XElement GWListRangeddoc = doc.Root.Element("GWListRanged");
                if (GWListRangeddoc != null)
                {
                    GWListRanged = new List<string>();

                    if (GWListRangeddoc.Elements().Count() > 0)
                    {
                        foreach (XElement subElement in GWListRangeddoc.Elements())
                        {
                            GWListRanged.Add(subElement.GetAttributeString("name", ""));
                        }
                    }
                }

                //Text for players when the shuttle is going to leave and kill its occupants

                XElement GWListRangedAmmodoc = doc.Root.Element("GWListRangedAmmo");
                if (GWListRangedAmmodoc != null)
                {
                    GWListRangedAmmo = new List<string>();

                    if (GWListRangedAmmodoc.Elements().Count() > 0)
                    {
                        foreach (XElement subElement in GWListRangedAmmodoc.Elements())
                        {
                            GWListRangedAmmo.Add(subElement.GetAttributeString("name", ""));
                        }
                    }
                }

                //Text for sub vs sub - Coalition team spawns

                XElement GWListRailgunLaunchdoc = doc.Root.Element("GWListRailgunLaunch");
                if (GWListRailgunLaunchdoc != null)
                {
                    GWListRailgunLaunch = new List<string>();

                    if (GWListRailgunLaunchdoc.Elements().Count() > 0)
                    {
                        foreach (XElement subElement in GWListRailgunLaunchdoc.Elements())
                        {
                            GWListRailgunLaunch.Add(subElement.GetAttributeString("name", ""));
                        }
                    }
                }

                //Text for sub vs sub - Renegade team spawns

                XElement GWListRailgunRacksdoc = doc.Root.Element("GWListRailgunRacks");
                if (GWListRailgunRacksdoc != null)
                {
                    GWListRailgunRacks = new List<string>();

                    if (GWListRailgunRacksdoc.Elements().Count() > 0)
                    {
                        foreach (XElement subElement in GWListRailgunRacksdoc.Elements())
                        {
                            GWListRailgunRacks.Add(subElement.GetAttributeString("name", ""));
                        }
                    }
                }

                XElement GWListRailgunAmmodoc = doc.Root.Element("GWListRailgunAmmo");
                if (GWListRailgunAmmodoc != null)
                {
                    GWListRailgunAmmo = new List<string>();

                    if (GWListRailgunAmmodoc.Elements().Count() > 0)
                    {
                        foreach (XElement subElement in GWListRailgunAmmodoc.Elements())
                        {
                            GWListRailgunAmmo.Add(subElement.GetAttributeString("name", ""));
                        }
                    }
                }

                XElement GWListSelfUsedoc = doc.Root.Element("GWListUse");
                if (GWListSelfUsedoc != null)
                {
                    GWListUse = new List<string>();

                    if (GWListSelfUsedoc.Elements().Count() > 0)
                    {
                        foreach (XElement subElement in GWListSelfUsedoc.Elements())
                        {
                            GWListUse.Add(subElement.GetAttributeString("name", ""));
                        }
                    }
                }

                XElement GWListMeleeOtherdoc = doc.Root.Element("GWListMeleeOther");
                if (GWListMeleeOtherdoc != null)
                {
                    GWListMeleeOther = new List<string>();

                    if (GWListMeleeOtherdoc.Elements().Count() > 0)
                    {
                        foreach (XElement subElement in GWListMeleeOtherdoc.Elements())
                        {
                            GWListMeleeOther.Add(subElement.GetAttributeString("name", ""));
                        }
                    }
                }

                XElement GWListFabricateddoc = doc.Root.Element("GWListFabricated");
                if (GWListFabricateddoc != null)
                {
                    GWListFabricated = new List<string>();

                    if (GWListFabricateddoc.Elements().Count() > 0)
                    {
                        foreach (XElement subElement in GWListFabricateddoc.Elements())
                        {
                            GWListFabricated.Add(subElement.GetAttributeString("name", ""));
                        }
                    }
                }

                XElement GWListHandcuffsdoc = doc.Root.Element("GWListHandcuffs");
                if (GWListHandcuffsdoc != null)
                {
                    GWListHandcuffs = new List<string>();

                    if (GWListHandcuffsdoc.Elements().Count() > 0)
                    {
                        foreach (XElement subElement in GWListHandcuffsdoc.Elements())
                        {
                            GWListHandcuffs.Add(subElement.GetAttributeString("name", ""));
                        }
                    }
                }

                Save();

                if (GameMain.Server != null)
                {
                    //Recheck the prefabs
                    GameInitialize();
                }
            }
        }


        public void Save()
        {
            List<string> lines = new List<string>
            {
                @"<?xml version=""1.0"" encoding=""utf-8"" ?>",
                "<GriefWatcher>",
                "  <NilModGriefWatchSettings",
                @"    GriefWatchName=""" + GriefWatchName + @"""",
                @"    ExcludeTraitors=""" + ExcludeTraitors + @"""",
                @"    AdminsOnly=""" + AdminsOnly + @"""",
                @"    KeepTeamSpecific=""" + KeepTeamSpecific + @"""",
                @"    ReactorAutoTempOff=""" + ReactorAutoTempOff + @"""",
                @"    ReactorShutDownTemp=""" + ReactorShutDownTemp + @"""",
                @"    ReactorStateLastBlamed=""" + ReactorStateLastBlamed + @"""",
                @"    ReactorFissionBeyondAuto=""" + ReactorFissionBeyondAuto + @"""",
                @"    ReactorLastFuelRemoved=""" + ReactorLastFuelRemoved + @"""",
                @"    ReactorLastFuelRemovedTimer=""" + ReactorLastFuelRemovedTimer + @"""",
                @"    WallBreachOutside90=""" + WallBreachOutside90 + @"""",
                @"    WallBreachOutside75=""" + WallBreachOutside75 + @"""",
                @"    WallBreachOutside50=""" + WallBreachOutside50 + @"""",
                @"    WallBreachOutside25=""" + WallBreachOutside25 + @"""",
                @"    WallBreachOutside0=""" + WallBreachOutside0 + @"""",
                @"    WallBreachInside90=""" + WallBreachInside90 + @"""",
                @"    WallBreachInside75=""" + WallBreachInside75 + @"""",
                @"    WallBreachInside50=""" + WallBreachInside50 + @"""",
                @"    WallBreachInside25=""" + WallBreachInside25 + @"""",
                @"    WallBreachInside0=""" + WallBreachInside0 + @"""",
                @"    AirlockLeftOpen=""" + AirlockLeftOpen + @"""",
                @"    AirlockLeftOpenTimer=""" + AirlockLeftOpenTimer + @"""",
                @"    AirlockLeftOpenRespawnMult=""" + AirlockLeftOpenRespawnMult + @"""",
                //@"    DoorBroken=""" + DoorBroken + @"""",
                @"    DoorStuck=""" + DoorStuck + @"""",
                @"    PlayerIncapaciteDamage=""" + PlayerIncapaciteDamage + @"""",
                //@"    PlayerIncapaciteBleed=""" + PlayerIncapaciteBleed + @"""",
                @"    PlayerIncapaciteOxygen=""" + PlayerIncapaciteOxygen + @"""",
                //@"    PlayerTakeIDOffLiving=""" + PlayerTakeIDOffLiving + @"""",
                @"    PumpWarnTimer=""" + PumpWarnTimer + @"""",
                @"    PumpPositive=""" + PumpPositive + @"""",
                @"    PumpOff=""" + PumpOff + @"""",
                "  />",

                "  ",
            };

            lines.Add("  <!--List of detonator items that if attached onto a wall or have an explosive placed inside triggers a report-->");
            lines.Add("  <GWListDetonators>");
            foreach (string item in GWListDetonators)
            {
                lines.Add(@"    <Item name=""" + item + @"""/>");
            }
            lines.Add("  </GWListDetonators>");

            lines.Add("  ");
            lines.Add("  <!--List of explosive items that if placed into a detonator trigger a report-->");
            lines.Add("  <GWListExplosives>");
            foreach (string item in GWListExplosives)
            {
                lines.Add(@"    <Item name=""" + item + @"""/>");
            }
            lines.Add("  </GWListExplosives>");

            lines.Add("  ");
            lines.Add("  <!--List containing items that if thrown triggers a report-->");
            lines.Add("  <GWListThrown>");
            foreach (string item in GWListThrown)
            {
                lines.Add(@"    <Item name=""" + item + @"""/>");
            }
            lines.Add("  </GWListThrown>");

            lines.Add("  ");
            lines.Add("  <!--List defining mask/suit items-->");
            lines.Add("  <GWListMaskItems>");
            foreach (string item in GWListMaskItems)
            {
                lines.Add(@"    <Item name=""" + item + @"""/>");
            }
            lines.Add("  </GWListMaskItems>");

            lines.Add("  ");
            lines.Add("  <!--This is used in conjunction with mask items, if placed onto another player or inserted into their worn mask reports it-->");
            lines.Add("  <GWListMaskHazardous>");
            foreach (string item in GWListMaskHazardous)
            {
                lines.Add(@"    <Item name=""" + item + @"""/>");
            }
            lines.Add("  </GWListMaskHazardous>");

            lines.Add("  ");
            lines.Add("  <!--If any device on this list is rewired trigger a report-->");
            lines.Add("  <GWListWireKeyDevices>");
            foreach (string item in GWListWireKeyDevices)
            {
                lines.Add(@"    <Item name=""" + item + @"""/>");
            }
            lines.Add("  </GWListWireKeyDevices>");

            lines.Add("  ");
            lines.Add("  <!--list of power and signal connection devices that if rewired trigger a report (Unless its a connection to a device not on the above list)-->");
            lines.Add("  <GWListWireJunctions>");
            foreach (string item in GWListWireJunctions)
            {
                lines.Add(@"    <Item name=""" + item + @"""/>");
            }
            lines.Add("  </GWListWireJunctions>");

            lines.Add("  ");
            lines.Add("  <!--List of items that count as medical syringes for the chems list-->");
            lines.Add("  <GWListSyringes>");
            foreach (string item in GWListSyringes)
            {
                lines.Add(@"    <Item name=""" + item + @"""/>");
            }
            lines.Add("  </GWListSyringes>");

            lines.Add("  ");
            lines.Add("  <!--List of items that if placed into a medical syringe will trigger a report-->");
            lines.Add("  <GWListSyringechems>");
            foreach (string item in GWListSyringechems)
            {
                lines.Add(@"    <Item name=""" + item + @"""/>");
            }
            lines.Add("  </GWListSyringechems>");

            lines.Add("  ");
            lines.Add("  <!--This is a list of ranged weapons that is used in conjunction with the ammo list, any bad ammo added into a ranged gun will be reported-->");
            lines.Add("  <GWListRanged>");
            foreach (string item in GWListRanged)
            {
                lines.Add(@"    <Item name=""" + item + @"""/>");
            }
            lines.Add("  </GWListRanged>");

            lines.Add("  ");
            lines.Add("  <!--This is a list of items that should be reported if placed into any Ranged weapon on the list, This will also list contained items if applicable-->");
            lines.Add("  <GWListRangedAmmo>");
            foreach (string item in GWListRangedAmmo)
            {
                lines.Add(@"    <Item name=""" + item + @"""/>");
            }
            lines.Add("  </GWListRangedAmmo>");

            lines.Add("  ");
            lines.Add("  <!--This is a list of items that should be reported if fired from a turret-->");
            lines.Add("  <GWListRailgunLaunch>");
            foreach (string item in GWListRailgunLaunch)
            {
                lines.Add(@"    <Item name=""" + item + @"""/>");
            }
            lines.Add("  </GWListRailgunLaunch>");

            lines.Add("  ");
            lines.Add("  <!--List of ammo-loader type objects for railguns, this is for tracking when ammo is placed into a loaders inventory for reporting purposes-->");
            lines.Add("  <GWListRailgunRacks>");
            foreach (string item in GWListRailgunRacks)
            {
                lines.Add(@"    <Item name=""" + item + @"""/>");
            }
            lines.Add("  </GWListRailgunRacks>");

            lines.Add("  ");
            lines.Add("  <!--List of Ammunition types placed into an railgun loader to be reported-->");
            lines.Add("  <GWListRailgunAmmo>");
            foreach (string item in GWListRailgunAmmo)
            {
                lines.Add(@"    <Item name=""" + item + @"""/>");
            }
            lines.Add("  </GWListRailgunAmmo>");

            lines.Add("  ");
            lines.Add("  <!--List of items that if used on self or another triggers a report-->");
            lines.Add("  <GWListUse>");
            foreach (string item in GWListUse)
            {
                lines.Add(@"    <Item name=""" + item + @"""/>");
            }
            lines.Add("  </GWListUse>");

            lines.Add("  ");
            lines.Add("  <!--List of items that if used to melee another player to be reported (Mainly for mods with weird potentially griefy weapons)-->");
            lines.Add("  <GWListMeleeOther>");
            foreach (string item in GWListMeleeOther)
            {
                lines.Add(@"    <Item name=""" + item + @"""/>");
            }
            lines.Add("  </GWListMeleeOther>");

            lines.Add("  ");
            lines.Add("  <!--Items that if created by a fabricator of any kind should be reported-->");
            lines.Add("  <GWListFabricated>");
            foreach (string item in GWListFabricated)
            {
                lines.Add(@"    <Item name=""" + item + @"""/>");
            }
            lines.Add("  </GWListFabricated>");

            lines.Add("  ");
            lines.Add("  <!--List of items that if placed into anothers hands count as handcuffs to be reported-->");
            lines.Add("  <GWListHandcuffs>");
            foreach (string item in GWListHandcuffs)
            {
                lines.Add(@"    <Item name=""" + item + @"""/>");
            }
            lines.Add("  </GWListHandcuffs>");
            lines.Add("</GriefWatcher>");

            using (System.IO.StreamWriter file =
            new System.IO.StreamWriter(GriefWatchSavePath, false, Encoding.UTF8))
            {
                foreach (string line in lines)
                {
                    file.WriteLine(line);
                }
            }
        }

        public void SendWarning(string WarningMessage, Client Offender)
        {
            //If server is not running this should never trigger
            if (GameMain.Server == null || Offender == null) return;

            //Do not send messages if their traitors and set to be ignored as such.
            if(GameMain.Server.TraitorManager != null && GameMain.Server.TraitorManager.IsTraitor(Offender.Character) && NilMod.NilModGriefWatcher.ExcludeTraitors) return;

            //Loop through the clients whom qualify for receiving the warning.
            for (int i = 0; i < GameMain.Server.ConnectedClients.Count; i++)
            {
                if((GameMain.Server.ConnectedClients[i].TeamID == Offender.TeamID
                    || !NilMod.NilModGriefWatcher.KeepTeamSpecific
                    || GameMain.Server.ConnectedClients[i].SpectateOnly
                    || GameMain.Server.ConnectedClients[i].Character == null
                    || (GameMain.Server.ConnectedClients[i].Character != null && GameMain.Server.ConnectedClients[i].Character.IsDead))
                    && !AdminsOnly)
                {
                    SendMessage("[Team " + Offender.TeamID + "] " + WarningMessage, GameMain.Server.ConnectedClients[i]);
                }
                else if(AdminsOnly &&
                    (GameMain.Server.ConnectedClients[i].AdministratorSlot
                    || GameMain.Server.ConnectedClients[i].OwnerSlot
                    || GameMain.Server.ConnectedClients[i].HasPermission(ClientPermissions.Ban)
                    || GameMain.Server.ConnectedClients[i].HasPermission(ClientPermissions.Kick)))
                {
                    SendMessage("[Team " + Offender.TeamID + "] " + WarningMessage, GameMain.Server.ConnectedClients[i]);
                }
            }
            //Send to the Server/Host itself.
            SendMessage(WarningMessage, null);
        }

        public void SendMessage(string messagetext,Client clientreceiver)
        {
            if (clientreceiver != null)
            {
                var chatMsg = ChatMessage.Create(
                GriefWatchName,
                messagetext,
                (ChatMessageType)ChatMessageType.Server,
                null);

                GameMain.Server.SendChatMessage(chatMsg, clientreceiver);
            }
            else
            {
                //Local Host Chat code here
                //if (Character.Controlled != null)
                //{
                    GameMain.NetworkMember.AddChatMessage(GriefWatchName + ":" + messagetext, ChatMessageType.Server);
                //}
            }
        }

        public void SetDefault()
        {
            GriefWatchName = "GW-AI";
            ExcludeTraitors = true;
            AdminsOnly = false;
            KeepTeamSpecific = true;
            ReactorAutoTempOff = true;
            ReactorShutDownTemp = true;
            ReactorStateLastBlamed = true;
            ReactorFissionBeyondAuto = true;
            ReactorLastFuelRemoved = true;
            ReactorLastFuelRemovedTimer = 6f;
            WallBreachOutside90 = true;
            WallBreachOutside75 = true;
            WallBreachOutside50 = true;
            WallBreachOutside25 = true;
            WallBreachOutside0 = true;
            WallBreachInside90 = true;
            WallBreachInside75 = true;
            WallBreachInside50 = true;
            WallBreachInside25 = true;
            WallBreachInside0 = true;
            AirlockLeftOpen = true;
            AirlockLeftOpenTimer = 10f;
            AirlockLeftOpenRespawnMult = 0.5f;
            DoorBroken = true;
            DoorStuck = true;
            PlayerIncapaciteDamage = true;
            PlayerIncapaciteBleed = true;
            PlayerTakeIDOffLiving = true;
            PumpWarnTimer = 6f;
            PumpPositive = true;
            PumpOff = true;

            GWListDetonators = new List<string>();
            GWListDetonators.Add("Detonator");

            GWListExplosives = new List<string>();
            GWListExplosives.Add("Nitroglycerin");
            GWListExplosives.Add("Flash Powder");
            GWListExplosives.Add("C-4 Block");
            GWListExplosives.Add("Compound N");
            GWListExplosives.Add("Volatile Compound N");
            GWListExplosives.Add("IC-4 Block");

            GWListThrown = new List<string>();
            GWListThrown.Add("Stun Grenade");
            GWListThrown.Add("Incendium Grenade");
            GWListThrown.Add("Nitroglycerin");
            GWListThrown.Add("Oxygenite Shard");
            GWListThrown.Add("Liquid Oxygenite");

            GWListMaskItems = new List<string>();
            GWListMaskItems.Add("Diving Mask");
            GWListMaskItems.Add("Diving Suit");

            GWListMaskHazardous = new List<string>();
            GWListMaskHazardous.Add("Welding Fuel Tank");

            GWListWireKeyDevices = new List<string>();
            GWListWireKeyDevices.Add("Oxygen Generator");
            GWListWireKeyDevices.Add("Battery");
            GWListWireKeyDevices.Add("Navigation Terminal");
            GWListWireKeyDevices.Add("Supercapacitor");
            GWListWireKeyDevices.Add("Small Pump");
            GWListWireKeyDevices.Add("Pump");
            GWListWireKeyDevices.Add("Nuclear Reactor");
            GWListWireKeyDevices.Add("Engine");
            GWListWireKeyDevices.Add("Railgun Controller");

            GWListWireJunctions = new List<string>();
            GWListWireJunctions.Add("Junction Box");
            GWListWireJunctions.Add("Relay Component");

            GWListSyringes = new List<string>();
            GWListSyringes.Add("Medical Syringe");

            GWListSyringechems = new List<string>();
            GWListSyringechems.Add("Morbusine");
            GWListSyringechems.Add("Sufforin");
            GWListSyringechems.Add("Nitroglycerin");
            GWListSyringechems.Add("Velonaceps Calyx Eggs");

            GWListRanged = new List<string>();
            GWListRanged.Add("Syringe Gun");

            GWListRangedAmmo = new List<string>();
            GWListRangedAmmo.Add("Medical Syringe");

            GWListRailgunLaunch = new List<string>();
            GWListRailgunLaunch.Add("Nuclear Shell");
            GWListRailgunLaunch.Add("Nuclear Depth Charge");

            GWListRailgunRacks = new List<string>();
            GWListRailgunRacks.Add("Railgun Loader");
            GWListRailgunRacks.Add("Depth Charge Loader");

            GWListRailgunAmmo = new List<string>();
            GWListRailgunAmmo.Add("Nuclear Shell");
            GWListRailgunAmmo.Add("Nuclear Depth Charge");

            GWListUse = new List<string>();
            GWListUse.Add("Morbusine");
            GWListUse.Add("Sufforin");
            GWListUse.Add("Nitroglycerin");
            GWListUse.Add("Velonaceps Calyx Eggs");

            GWListMeleeOther = new List<string>();
            GWListMeleeOther.Add("Stun Baton");
            GWListMeleeOther.Add("Crowbar");
            GWListMeleeOther.Add("Wrench");

            GWListFabricated = new List<string>();
            GWListFabricated.Add("Volatile Compound N");
            GWListFabricated.Add("Sufforin");
            GWListFabricated.Add("Morbusine");
            GWListFabricated.Add("Incendium Grenade");

            GWListHandcuffs = new List<string>();
            GWListHandcuffs.Add("Handcuffs");
        }


        public void GameInitialize()
        {
            if (!GameMain.NilMod.EnableGriefWatcher) return;

            for (int i = GWListDetonators.Count - 1; i >= 0; i--)
            {
                MapEntityPrefab PrefabCheck = ItemPrefab.Find(GWListDetonators[i]);
                if (PrefabCheck == null)
                {
                    DebugConsole.NewMessage("NilModGriefWatcher Error - Prefab does not exist! ("
                        + GWListDetonators[i] + ").", Color.Red);
                    GWListDetonators.RemoveAt(i);
                }
                else
                {
                    GWListDetonators[i] = PrefabCheck.Name;
                }
            }

            for (int i = GWListExplosives.Count - 1; i >= 0; i--)
            {
                MapEntityPrefab PrefabCheck = ItemPrefab.Find(GWListExplosives[i]);
                if (PrefabCheck == null)
                {
                    DebugConsole.NewMessage("NilModGriefWatcher Error - Prefab does not exist! ("
                        + GWListExplosives[i] + ").", Color.Red);
                    GWListExplosives.RemoveAt(i);
                }
                else
                {
                    GWListExplosives[i] = PrefabCheck.Name;
                }
            }

            for (int i = GWListThrown.Count - 1; i >= 0; i--)
            {
                MapEntityPrefab PrefabCheck = ItemPrefab.Find(GWListThrown[i]);
                if (PrefabCheck == null)
                {
                    DebugConsole.NewMessage("NilModGriefWatcher Error - Prefab does not exist! ("
                        + GWListThrown[i] + ").", Color.Red);
                    GWListThrown.RemoveAt(i);
                }
                else
                {
                    GWListThrown[i] = PrefabCheck.Name;
                }
            }

            for (int i = GWListMaskItems.Count - 1; i >= 0; i--)
            {
                MapEntityPrefab PrefabCheck = ItemPrefab.Find(GWListMaskItems[i]);
                if (PrefabCheck == null)
                {
                    DebugConsole.NewMessage("NilModGriefWatcher Error - Prefab does not exist! ("
                        + GWListMaskItems[i] + ").", Color.Red);
                    GWListMaskItems.RemoveAt(i);
                }
                else
                {
                    GWListMaskItems[i] = PrefabCheck.Name;
                }
            }

            for (int i = GWListMaskHazardous.Count - 1; i >= 0; i--)
            {
                MapEntityPrefab PrefabCheck = ItemPrefab.Find(GWListMaskHazardous[i]);
                if (PrefabCheck == null)
                {
                    DebugConsole.NewMessage("NilModGriefWatcher Error - Prefab does not exist! ("
                        + GWListMaskHazardous[i] + ").", Color.Red);
                    GWListMaskHazardous.RemoveAt(i);
                }
                else
                {
                    GWListMaskHazardous[i] = PrefabCheck.Name;
                }
            }

            for (int i = GWListWireKeyDevices.Count - 1; i >= 0; i--)
            {
                MapEntityPrefab PrefabCheck = ItemPrefab.Find(GWListWireKeyDevices[i]);
                if (PrefabCheck == null)
                {
                    DebugConsole.NewMessage("NilModGriefWatcher Error - Prefab does not exist! ("
                        + GWListWireKeyDevices[i] + ").", Color.Red);
                    GWListWireKeyDevices.RemoveAt(i);
                }
                else
                {
                    GWListWireKeyDevices[i] = PrefabCheck.Name;
                }
            }

            for (int i = GWListWireJunctions.Count - 1; i >= 0; i--)
            {
                MapEntityPrefab PrefabCheck = ItemPrefab.Find(GWListWireJunctions[i]);
                if (PrefabCheck == null)
                {
                    DebugConsole.NewMessage("NilModGriefWatcher Error - Prefab does not exist! ("
                        + GWListWireJunctions[i] + ").", Color.Red);
                    GWListWireJunctions.RemoveAt(i);
                }
                else
                {
                    GWListWireJunctions[i] = PrefabCheck.Name;
                }
            }

            for (int i = GWListSyringes.Count - 1; i >= 0; i--)
            {
                MapEntityPrefab PrefabCheck = ItemPrefab.Find(GWListSyringes[i]);
                if (PrefabCheck == null)
                {
                    DebugConsole.NewMessage("NilModGriefWatcher Error - Prefab does not exist! ("
                        + GWListSyringes[i] + ").", Color.Red);
                    GWListSyringes.RemoveAt(i);
                }
                else
                {
                    GWListSyringes[i] = PrefabCheck.Name;
                }
            }

            for (int i = GWListSyringechems.Count - 1; i >= 0; i--)
            {
                MapEntityPrefab PrefabCheck = ItemPrefab.Find(GWListSyringechems[i]);
                if (PrefabCheck == null)
                {
                    DebugConsole.NewMessage("NilModGriefWatcher Error - Prefab does not exist! ("
                        + GWListSyringechems[i] + ").", Color.Red);
                    GWListSyringechems.RemoveAt(i);
                }
                else
                {
                    GWListSyringechems[i] = PrefabCheck.Name;
                }
            }

            for (int i = GWListRanged.Count - 1; i >= 0; i--)
            {
                MapEntityPrefab PrefabCheck = ItemPrefab.Find(GWListRanged[i]);
                if (PrefabCheck == null)
                {
                    DebugConsole.NewMessage("NilModGriefWatcher Error - Prefab does not exist! ("
                        + GWListRanged[i] + ").", Color.Red);
                    GWListRanged.RemoveAt(i);
                }
                else
                {
                    GWListRanged[i] = PrefabCheck.Name;
                }
            }

            for (int i = GWListRangedAmmo.Count - 1; i >= 0; i--)
            {
                MapEntityPrefab PrefabCheck = ItemPrefab.Find(GWListRangedAmmo[i]);
                if (PrefabCheck == null)
                {
                    DebugConsole.NewMessage("NilModGriefWatcher Error - Prefab does not exist! ("
                        + GWListRangedAmmo[i] + ").", Color.Red);
                    GWListRangedAmmo.RemoveAt(i);
                }
                else
                {
                    GWListRangedAmmo[i] = PrefabCheck.Name;
                }
            }

            for (int i = GWListRailgunLaunch.Count - 1; i >= 0; i--)
            {
                MapEntityPrefab PrefabCheck = ItemPrefab.Find(GWListRailgunLaunch[i]);
                if (PrefabCheck == null)
                {
                    DebugConsole.NewMessage("NilModGriefWatcher Error - Prefab does not exist! ("
                        + GWListRailgunLaunch[i] + ").", Color.Red);
                    GWListRailgunLaunch.RemoveAt(i);
                }
                else
                {
                    GWListRailgunLaunch[i] = PrefabCheck.Name;
                }
            }

            for (int i = GWListRailgunRacks.Count - 1; i >= 0; i--)
            {
                MapEntityPrefab PrefabCheck = ItemPrefab.Find(GWListRailgunRacks[i]);
                if (PrefabCheck == null)
                {
                    DebugConsole.NewMessage("NilModGriefWatcher Error - Prefab does not exist! ("
                        + GWListRailgunRacks[i] + ").", Color.Red);
                    GWListRailgunRacks.RemoveAt(i);
                }
                else
                {
                    GWListRailgunRacks[i] = PrefabCheck.Name;
                }
            }

            for (int i = GWListRailgunAmmo.Count - 1; i >= 0; i--)
            {
                MapEntityPrefab PrefabCheck = ItemPrefab.Find(GWListRailgunAmmo[i]);
                if (PrefabCheck == null)
                {
                    DebugConsole.NewMessage("NilModGriefWatcher Error - Prefab does not exist! ("
                        + GWListRailgunAmmo[i] + ").", Color.Red);
                    GWListRailgunAmmo.RemoveAt(i);
                }
                else
                {
                    GWListRailgunAmmo[i] = PrefabCheck.Name;
                }
            }

            for (int i = GWListUse.Count - 1; i >= 0; i--)
            {
                MapEntityPrefab PrefabCheck = ItemPrefab.Find(GWListUse[i]);
                if (PrefabCheck == null)
                {
                    DebugConsole.NewMessage("NilModGriefWatcher Error - Prefab does not exist! ("
                        + GWListUse[i] + ").", Color.Red);
                    GWListUse.RemoveAt(i);
                }
                else
                {
                    GWListUse[i] = PrefabCheck.Name;
                }
            }

            for (int i = GWListFabricated.Count - 1; i >= 0; i--)
            {
                MapEntityPrefab PrefabCheck = ItemPrefab.Find(GWListFabricated[i]);
                if (PrefabCheck == null)
                {
                    DebugConsole.NewMessage("NilModGriefWatcher Error - Prefab does not exist! ("
                        + GWListFabricated[i] + ").", Color.Red);
                    GWListFabricated.RemoveAt(i);
                }
                else
                {
                    GWListFabricated[i] = PrefabCheck.Name;
                }
            }

            for (int i = GWListHandcuffs.Count - 1; i >= 0; i--)
            {
                MapEntityPrefab PrefabCheck = ItemPrefab.Find(GWListHandcuffs[i]);
                if (PrefabCheck == null)
                {
                    DebugConsole.NewMessage("NilModGriefWatcher Error - Prefab does not exist! ("
                        + GWListHandcuffs[i] + ").", Color.Red);
                    GWListHandcuffs.RemoveAt(i);
                }
                else
                {
                    GWListHandcuffs[i] = PrefabCheck.Name;
                }
            }
        }
    }
}
