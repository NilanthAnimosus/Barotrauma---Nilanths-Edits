﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Barotrauma.Networking;

namespace Barotrauma
{
    class InGameInfoCharacter
    {
        //This is to make it so the hosts original characters are, essentially, also treated as clients for filtering purposes.
        public Boolean IsHostCharacter;
        public Client client;
        public Client previousclient;
        public Character character;
        public Boolean Removed = false;
        public float RemovalTimer = 0f;
        //public CharacterInfo characterinfo;
    }

    class StatusWidget : GUIComponent
    {
        Character character;

        GUITextBlock healthlabel;
        GUITextBlock bleedlabel;
        GUITextBlock oxygenlabel;
        GUITextBlock pressurelabel;
        GUITextBlock stunlabel;
        GUITextBlock husklabel;

        public StatusWidget(Rectangle rect, Alignment alignment, Character character, GUIComponent parent = null)
            : base(null)
        {
            this.rect = rect;

            this.alignment = alignment;

            this.character = character;

            color = new Color(15,15,15,125);

            int barheight = rect.Y;

            healthlabel = new GUITextBlock(new Rectangle(rect.X, barheight, 55, 15), "", null, Alignment.Center, Alignment.Center, this, false);
            healthlabel.TextColor = Color.Black;
            healthlabel.TextScale = 0.75f;
            healthlabel.ToolTip = "Health";
            healthlabel.Visible = true;

            bleedlabel = new GUITextBlock(new Rectangle(rect.X + 55, barheight, 45, 15), "", null, Alignment.Center, Alignment.Center, this, false);
            bleedlabel.TextColor = Color.Black;
            bleedlabel.TextScale = 0.75f;
            healthlabel.ToolTip = "Bleed";
            bleedlabel.Visible = false;

            barheight += 15;

            oxygenlabel = new GUITextBlock(new Rectangle(rect.X, barheight, 55, 15), "", null, Alignment.Center, Alignment.Center, this, false);
            oxygenlabel.TextColor = Color.Black;
            oxygenlabel.TextScale = 0.75f;
            healthlabel.ToolTip = "Oxygen";
            oxygenlabel.Visible = false;

            pressurelabel = new GUITextBlock(new Rectangle(rect.X + 55, barheight, 45, 15), "Pressure", null, Alignment.Center, Alignment.Center, this, false);
            pressurelabel.TextColor = Color.Black;
            pressurelabel.TextScale = 0.75f;
            healthlabel.ToolTip = "Pressure";
            pressurelabel.Visible = false;

            barheight += 15;

            stunlabel = new GUITextBlock(new Rectangle(rect.X, barheight, 55, 15), "Stun", null, Alignment.Center, Alignment.Center, this, false);
            stunlabel.TextColor = Color.Black;
            stunlabel.TextScale = 0.75f;
            healthlabel.ToolTip = "Stun";
            stunlabel.Visible = true;

            husklabel = new GUITextBlock(new Rectangle(rect.X + 55, barheight, 45, 15), "Husk", null, Alignment.Center, Alignment.Center, this, false);
            husklabel.TextColor = Color.Black;
            husklabel.TextScale = 0.75f;
            healthlabel.ToolTip = "Husk Infection";
            husklabel.Visible = false;


            if (parent != null) parent.AddChild(this);
            this.parent = parent;
        }

        public override void Update(float deltaTime)
        {
            if (!Visible) return;
            base.Update(deltaTime);
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (!Visible) return;

            base.Draw(spriteBatch);

            Color currColor = color;
            //if (state == ComponentState.Hover) currColor = hoverColor;
            //if (state == ComponentState.Selected) currColor = selectedColor;
            if (state == ComponentState.Hover) Parent.State = ComponentState.Hover;
            if (state == ComponentState.Selected) Parent.State = ComponentState.Selected;


            Color outLineColour = Color.Gray;

            //Negative Colours
            Color NegativeLow = new Color(145, 145, 145, 160);
            Color NegativeHigh = new Color(25, 25, 25, 220);

            //Health Colours
            Color HealthPositiveHigh = new Color(0, 255, 0, 15);
            Color HealthPositiveLow = new Color(255, 0, 0, 60);
            //Oxygen Colours
            Color OxygenPositiveHigh = new Color(0, 255, 255, 15);
            Color OxygenPositiveLow = new Color(0, 0, 200, 60);
            //Stun Colours
            Color StunPositiveHigh = new Color(235, 135, 45, 100);
            Color StunPositiveLow = new Color(204, 119, 34, 30);
            //Bleeding Colours
            Color BleedPositiveHigh = new Color(255, 50, 50, 100);
            Color BleedPositiveLow = new Color(150, 50, 50, 15);
            //Pressure Colours
            Color PressurePositiveHigh = new Color(255, 255, 0, 100);
            Color PressurePositiveLow = new Color(125, 125, 0, 15);

            //Husk Colours
            Color HuskPositiveHigh = new Color(255, 100, 255, 150);
            Color HuskPositiveLow = new Color(125, 30, 125, 15);

            float pressureFactor = (character.AnimController.CurrentHull == null) ?
            100.0f : Math.Min(character.AnimController.CurrentHull.LethalPressure, 100.0f);
            if (character.PressureProtection > 0.0f && (character.WorldPosition.Y > GameMain.NilMod.PlayerCrushDepthOutsideHull || (character.WorldPosition.Y > GameMain.NilMod.PlayerCrushDepthInHull && character.CurrentHull != null))) pressureFactor = 0.0f;

            //GUI.DrawRectangle(spriteBatch, rect, currColor * (currColor.A / 255.0f), true);
            //GUI.DrawRectangle(spriteBatch, new Vector2(rect.X, rect.Y), new Vector2(80.0f, 10.0f), Color.Green, false, 0f);

            int barheight = rect.Y;

            if (!character.NeedsAir) barheight = barheight + 15;

            if (!character.IsDead)
            {
                Parent.Color = Color.Transparent;
                if(character.Health >= (character.MaxHealth * 0.24f))
                {
                    healthlabel.Rect = new Rectangle(rect.X, barheight,
                    Convert.ToInt16((character.Bleeding >= 0.1f ? 55 : 100) * (character.Health / character.MaxHealth))
                    , 15);
                    healthlabel.TextScale = 0.75f;
                    healthlabel.Text = Math.Round((character.Health / character.MaxHealth) * 100, 0) + "%";
                }
                else if(character.Health <= -(character.MaxHealth * 0.24f))
                {
                    healthlabel.Rect = new Rectangle(rect.X, barheight,
                    Convert.ToInt16((character.Bleeding >= 0.1f ? 55 : 100) * (character.Health / character.MaxHealth))
                    , 15);
                    healthlabel.TextScale = 0.75f;
                    healthlabel.Text = "-" + Math.Round(-character.Health, 0) + "/" + Math.Round(character.MaxHealth, 0);
                }
                else
                {
                    healthlabel.Rect = new Rectangle(rect.X, barheight,
                    Convert.ToInt16((character.Bleeding >= 0.1f ? 55 : 100) * (character.Health / character.MaxHealth))
                    , 15);
                    healthlabel.TextScale = 0.75f;
                    healthlabel.Text = "";
                }

                if (character.Health >= 0f)
                {
                    GUI.DrawProgressBar(spriteBatch, new Vector2(rect.X, -barheight), new Vector2((character.Bleeding >= 0.1f ? 55.0f : 100.0f), 15.0f), character.Health / character.MaxHealth, Color.Lerp(HealthPositiveLow, HealthPositiveHigh, character.Health / character.MaxHealth), outLineColour, 0.5f, 0f, "Left");
                }
                //Health has gone below 0
                else
                {
                    GUI.DrawProgressBar(spriteBatch, new Vector2(rect.X, -barheight), new Vector2((character.Bleeding >= 0.1f ? 55.0f : 100.0f), 15.0f), -(character.Health / character.MaxHealth), Color.Lerp(NegativeLow, NegativeHigh, -(character.Health / character.MaxHealth)), outLineColour, 0.5f, 0f, "Right");
                }

                if (character.Bleeding >= 0.1f)
                {
                    GUI.DrawProgressBar(spriteBatch, new Vector2(rect.X + 55, -barheight), new Vector2(45.0f, 15.0f), character.Bleeding / 5f, Color.Lerp(BleedPositiveLow, BleedPositiveHigh, character.Bleeding / 5f), outLineColour, 0.5f, 0f, "Right");
                    bleedlabel.Rect = new Rectangle(rect.X + 55, barheight, 45, 15);
                    bleedlabel.Visible = true;
                }
                else
                {
                    bleedlabel.Visible = false;
                }

                barheight += 15;

                if (character.NeedsAir)
                {
                    Boolean showpressure = false;
                    if (pressureFactor / 100f >= 0.3f) showpressure = true;
                    //Oxygen Bar
                    if (character.Oxygen >= 0f)
                    {
                        GUI.DrawProgressBar(spriteBatch, new Vector2(rect.X, -barheight), new Vector2((showpressure ? 55 : 100), 15.0f), character.Oxygen / 100f, Color.Lerp(OxygenPositiveLow, OxygenPositiveHigh, character.Oxygen / 100f), outLineColour, 0.5f, 0f, "Left");
                    }
                    //Oxygen has gone below 0
                    else if (character.Oxygen < 0f)
                    {
                        GUI.DrawProgressBar(spriteBatch, new Vector2(rect.X, -barheight), new Vector2((showpressure ? 55f : 100f), 15.0f), -(character.Oxygen / 100f), Color.Lerp(NegativeLow, NegativeHigh, -(character.Oxygen / 100f)), outLineColour, 0.5f, 0f, "Right");
                    }
                    oxygenlabel.Rect = new Rectangle(rect.X, barheight, (showpressure ? 55 : 100), 15);
                    oxygenlabel.Visible = true;

                    //Pressure Bar
                    if (showpressure)
                    {
                        GUI.DrawProgressBar(spriteBatch, new Vector2(rect.X + 55, -barheight), new Vector2(45.0f, 15.0f), pressureFactor / 100f, Color.Lerp(PressurePositiveLow, PressurePositiveHigh, pressureFactor / 100f), outLineColour, 0.5f, 0f, "Right");
                        pressurelabel.Rect = new Rectangle(rect.X + 55, barheight, 45, 15);
                        pressurelabel.Visible = true;
                    }
                    else
                    {
                        pressurelabel.Visible = false;
                    }
                    barheight += 15;
                }
                else
                {
                    oxygenlabel.Visible = false;
                    pressurelabel.Visible = false;
                }

                stunlabel.Visible = true;

                if (character.huskInfection == null)
                {
                    //Stun bar
                    GUI.DrawProgressBar(spriteBatch, new Vector2(rect.X, -barheight), new Vector2(100.0f, 15.0f), character.Stun / 60f, Color.Lerp(StunPositiveLow, StunPositiveHigh, character.Stun / 60f), outLineColour, 0.5f, 0f, "Left");
                    stunlabel.Rect = new Rectangle(rect.X, barheight, 100, 15);
                    husklabel.Visible = false;
                }
                else
                {
                    //Stun bar
                    GUI.DrawProgressBar(spriteBatch, new Vector2(rect.X, -barheight), new Vector2(55.0f, 15.0f), character.Stun / 60f, Color.Lerp(StunPositiveLow, StunPositiveHigh, character.Stun / 60f), outLineColour, 0.5f, 0f, "Left");
                    stunlabel.Rect = new Rectangle(rect.X, barheight, 55, 15);

                    //Husk bar
                    GUI.DrawProgressBar(spriteBatch, new Vector2(rect.X + 55, -barheight), new Vector2(45.0f, 15.0f), character.HuskInfectionState, Color.Lerp(HuskPositiveLow, HuskPositiveHigh, character.HuskInfectionState), outLineColour, 0.5f, 0f, "Right");
                    husklabel.Rect = new Rectangle(rect.X + 55, barheight, 45, 15);
                    husklabel.Visible = true;
                }
            }
            else
            {
                Parent.Color = new Color(0, 0, 0, 150);

                if (!character.NeedsAir)
                {
                    GUI.DrawRectangle(spriteBatch, new Vector2(rect.X, rect.Y + 15), new Vector2(rect.Width, rect.Height - 15), new Color(150, 5, 5, 15), true, 0f, 1);
                    healthlabel.Rect = new Rectangle(rect.X, rect.Y, rect.Width, rect.Height);
                }
                else
                {
                    GUI.DrawRectangle(spriteBatch, new Vector2(rect.X, rect.Y), new Vector2(rect.Width, rect.Height), new Color(150, 5, 5, 15), true, 0f, 1);
                    healthlabel.Rect = new Rectangle(rect.X, rect.Y, rect.Width, rect.Height);
                }
                
                healthlabel.TextScale = 1.4f;
                healthlabel.Text = "DECEASED.";

                bleedlabel.Visible = false;
                oxygenlabel.Visible = false;
                pressurelabel.Visible = false;
                stunlabel.Visible = false;
                husklabel.Visible = false;
            }

            DrawChildren(spriteBatch);
        }
    }

    class ControlWidget : GUIComponent
    {
        public ControlWidget(Rectangle rect, Alignment alignment, Character character, Client client = null, GUIComponent parent = null)
            : base(null)
        {
            this.rect = rect;
            this.parent = parent;
            this.alignment = alignment;
            color = new Color(15, 15, 15, 125);
            this.Padding = Vector4.Zero;
            Vector2 ButtonPosition = new Vector2(parent.Rect.X, parent.Rect.Y);

            int Buttoncount = 0;

            //Widget code goes here

            GUIImageButton GUITempImageButton = null;

            if (character != null)
            {
                //Heal Character Button
                ButtonPosition = CalculatePageButtonPosition(Buttoncount, parent);
                GUITempImageButton = new GUIImageButton(new Rectangle((Int16)ButtonPosition.X, (Int16)ButtonPosition.Y, 20, 20)
                    , InGameInfo.HealButton, Alignment.Left, this);
                //Colour Definition
                GUITempImageButton.Color = new Color(255, 255, 255, 255);
                GUITempImageButton.HoverColor = new Color(200, 200, 25, 255);
                GUITempImageButton.SelectedColor = new Color(100, 100, 100, 255);

                //Button image Definition
                //GUITempImageButton.Offset = new Vector2(6, 6);
                GUITempImageButton.Scale = 0.16f;
                GUITempImageButton.Padding = Vector4.Zero;
                GUITempImageButton.CanDoubleClick = false;

                //Button code / specifics
                GUITempImageButton.ToolTip = "Revive/Heal";
                GUITempImageButton.UserData = parent.UserData;
                GUITempImageButton.OnClicked += (btn, userData) =>
                {
                    InGameInfoCharacter thischar = (InGameInfoCharacter)parent.UserData;
                    if (thischar.character == null) return true;

                    if (!thischar.character.IsDead)
                    {
                        thischar.character.Heal();
                    }
                    else
                    {
                        thischar.character.Revive();
                        Client matchedclient = GameMain.Server.ConnectedClients.Find(c => c.Name == thischar.character.Name);

                        if (thischar.character.IsRemotePlayer && thischar.client == null
                        && matchedclient != null)
                        {
                            //see if the original client is in the server
                            GameSession.inGameInfo.UpdateClientCharacter(GameMain.Server.ConnectedClients.Find(c => c.Name == thischar.character?.Info?.Name),thischar.character,false);
                            GameSession.inGameInfo.RemoveEntry(thischar);
                        }
                        if (thischar.client != null) GameMain.GameScreen.RunIngameCommand("setclientcharacter", new object[] { thischar.client, thischar.character });
                        else if (matchedclient != null && (matchedclient.Character == null || matchedclient.Character.IsDead)) GameMain.GameScreen.RunIngameCommand("setclientcharacter", new object[] { matchedclient, thischar.character });
                        else if (thischar.IsHostCharacter && Character.Controlled == null)
                        {
                            GameMain.Server.Character = thischar.character;
                            Character.Controlled = thischar.character;
                            Character.SpawnCharacter = thischar.character;
                            Character.LastControlled = thischar.character;
                        }
                    }

                    GameSession.inGameInfo.GuiUpdateRequired = true;
                    GameSession.inGameInfo.Guiupdatetimer = 0f;

                    return true;
                };
                Buttoncount += 1;

                //Teleport Character Button
                ButtonPosition = CalculatePageButtonPosition(Buttoncount, parent);
                GUITempImageButton = new GUIImageButton(new Rectangle((Int16)ButtonPosition.X, (Int16)ButtonPosition.Y, 20, 20)
                    , InGameInfo.TeleportButton, Alignment.Left, this);
                //Colour Definition
                GUITempImageButton.Color = new Color(255, 255, 255, 255);
                GUITempImageButton.HoverColor = new Color(200, 200, 25, 255);
                GUITempImageButton.SelectedColor = new Color(100, 100, 100, 255);

                //Button image Definition
                //GUITempImageButton.Offset = new Vector2(6, 6);
                GUITempImageButton.Scale = 0.16f;
                GUITempImageButton.Padding = Vector4.Zero;
                GUITempImageButton.CanDoubleClick = false;

                //Button code / specifics
                GUITempImageButton.ToolTip = "Relocate";
                GUITempImageButton.UserData = parent.UserData;
                GUITempImageButton.OnClicked += (btn, userData) =>
                {
                    InGameInfoCharacter thischar = (InGameInfoCharacter)parent.UserData;
                    if (thischar.character == null) return true;

                    GameMain.NilMod.ClickCommandType = "relocate";
                    GameMain.NilMod.ActiveClickCommand = true;
                    GameMain.NilMod.ClickCooldown = 0.5f;
                    GameMain.Server.ClickCommandFrame.Visible = true;
                    GameMain.NilMod.ClickTargetCharacter = thischar.character;
                    GameMain.Server.ClickCommandDescription.Text = "RELOCATE - " + GameMain.NilMod.ClickTargetCharacter + " - Left Click to select target to teleport, Left click again to teleport target to new destination, hold shift to repeat (Does not keep last target), Ctrl+Left Click to relocate self, Ctrl+Shift works, Right click to cancel.";

                    return true;
                };
                Buttoncount += 1;

                //Kill Character/remove corpse Button
                ButtonPosition = CalculatePageButtonPosition(Buttoncount, parent);
                GUITempImageButton = new GUIImageButton(new Rectangle((Int16)ButtonPosition.X, (Int16)ButtonPosition.Y, 20, 20)
                    , InGameInfo.KillButton, Alignment.Left, this);
                //Colour Definition
                GUITempImageButton.Color = new Color(255, 255, 255, 255);
                GUITempImageButton.HoverColor = new Color(200, 200, 25, 255);
                GUITempImageButton.SelectedColor = new Color(100, 100, 100, 255);

                //Button image Definition
                //GUITempImageButton.Offset = new Vector2(6, 6);
                GUITempImageButton.Scale = 0.16f;
                GUITempImageButton.Padding = Vector4.Zero;
                GUITempImageButton.CanDoubleClick = false;

                //Button code / specifics
                GUITempImageButton.ToolTip = "Kill/remove corpse";
                GUITempImageButton.UserData = parent.UserData;
                GUITempImageButton.OnClicked += (btn, userData) =>
                {
                    InGameInfoCharacter thischar = (InGameInfoCharacter)parent.UserData;
                    if (thischar.character == null) return true;

                    if(!thischar.character.IsDead)
                    {
                        thischar.character.Kill(CauseOfDeath.Disconnected, false);
                        thischar.character.Health = -10000f;
                        thischar.character.Oxygen = -100f;
                    }
                    else
                    {
                        Entity.Spawner.AddToRemoveQueue(thischar.character);
                        GameSession.inGameInfo.RemoveCharacter(thischar.character);
                    }

                    GameSession.inGameInfo.GuiUpdateRequired = true;
                    GameSession.inGameInfo.Guiupdatetimer = 0f;

                    return true;
                };
                Buttoncount += 1;
            }
            //Client OR host character (Specifically, forced respawning really)
            if((client != null)
                || (client == null && ((InGameInfoCharacter)parent.UserData).IsHostCharacter))
            {
                //Respawn client/host button.
                ButtonPosition = CalculatePageButtonPosition(Buttoncount, parent);
                GUITempImageButton = new GUIImageButton(new Rectangle((Int16)ButtonPosition.X, (Int16)ButtonPosition.Y, 20, 20)
                    , InGameInfo.RespawnButton, Alignment.Left, this);
                //Colour Definition
                GUITempImageButton.Color = new Color(255, 255, 255, 255);
                GUITempImageButton.HoverColor = new Color(200, 200, 25, 255);
                GUITempImageButton.SelectedColor = new Color(100, 100, 100, 255);

                //Button image Definition
                //GUITempImageButton.Offset = new Vector2(6, 6);
                GUITempImageButton.Scale = 0.16f;
                GUITempImageButton.Padding = Vector4.Zero;
                GUITempImageButton.CanDoubleClick = false;

                //Button code / specifics
                GUITempImageButton.ToolTip = "Respawn character to submarine";
                GUITempImageButton.UserData = parent.UserData;
                GUITempImageButton.OnClicked += (btn, userData) =>
                {
                    InGameInfoCharacter thischar = (InGameInfoCharacter)parent.UserData;
                    if (!thischar.IsHostCharacter && thischar.client == null) return true;

                    //Default team for standard rounds
                    int teamID = 1;

                    if (thischar.client != null)
                    {
                        if (thischar.client.Character != null && !thischar.client.Character.IsDead) return true;

                        //If client has no assigned team, give him one.
                        if (thischar.client.TeamID == 0)
                        {
                            if (GameMain.GameSession?.GameMode.Name == "Mission" && GameMain.GameSession?.GameMode.Mission.Prefab.Name == "Combat")
                            {
                                int Team1count = GameMain.Server.ConnectedClients.FindAll(c => c.TeamID == 1).Count();
                                int Team2count = GameMain.Server.ConnectedClients.FindAll(c => c.TeamID == 2).Count();
                                //team 1 is coalition, 2 is renegades, 0 is AI

                                if (Team1count <= Team2count)
                                {
                                    //Coalition
                                    teamID = 1;
                                }
                                else
                                {
                                    //Renegade
                                    teamID = 2;
                                }
                            }
                        }
                        else
                        {
                            teamID = 1;
                        }
                    }
                    else if(thischar.IsHostCharacter)
                    {
                        if ((Character.SpawnCharacter != null && !Character.SpawnCharacter.IsDead)
                        || (Character.Controlled != null
                        && Character.Controlled.Info.Name == GameMain.NilMod.PlayYourselfName
                        && !Character.Controlled.IsDead)) return true;

                        if (Character.SpawnCharacter != null)
                        {
                            teamID = Character.SpawnCharacter.TeamID;
                        }
                        else if (GameMain.NetworkMember.CharacterInfo != null)
                        {
                            teamID = GameMain.NetworkMember.CharacterInfo.TeamID;
                        }
                    }



                    if (thischar.IsHostCharacter)
                    {
                        if (GameMain.Server.CharacterInfo != null)
                        {
                            GameMain.Server.CharacterInfo.Job = new Job(GameMain.NetLobbyScreen.JobPreferences[0]);
                            //GameMain.Server.AssignJobs(new List<Client>(), true);
                            WayPoint Waypoint = WayPoint.SelectCrewSpawnPoints(new List<CharacterInfo>() { GameMain.Server.CharacterInfo }, Submarine.MainSubs[teamID - 1])[0];
                            Character spawnedCharacter = Character.Create(GameMain.Server.CharacterInfo, Waypoint.WorldPosition, true, false);
                            spawnedCharacter.TeamID = (byte)teamID;
                            spawnedCharacter.GiveJobItems(Waypoint);

                            //Spawn protection
                            if (GameMain.NilMod.PlayerSpawnProtectMidgame)
                            {
                                if (GameMain.NilMod.PlayerSpawnProtectHealth != 0f) spawnedCharacter.SpawnProtectionHealth = GameMain.NilMod.PlayerSpawnProtectHealth / 5f;
                                if (GameMain.NilMod.PlayerSpawnProtectOxygen != 0f) spawnedCharacter.SpawnProtectionOxygen = GameMain.NilMod.PlayerSpawnProtectOxygen * 1.2f;
                                if (GameMain.NilMod.PlayerSpawnProtectPressure != 0f) spawnedCharacter.SpawnProtectionPressure = GameMain.NilMod.PlayerSpawnProtectPressure * 1.2f;
                                if (GameMain.NilMod.PlayerSpawnProtectStun != 0f) spawnedCharacter.SpawnProtectionStun = GameMain.NilMod.PlayerSpawnProtectStun / 5f;
                                if (GameMain.NilMod.PlayerSpawnRewireWaitTimer != 0f) spawnedCharacter.SpawnRewireWaitTimer = GameMain.NilMod.PlayerSpawnRewireWaitTimer / 5f;
                            }

                            GameMain.Server.Character = thischar.character;
                            Character.SpawnCharacter = spawnedCharacter;
                            Character.Controlled = spawnedCharacter;
                            Character.LastControlled = spawnedCharacter;

#if CLIENT
                            GameSession.inGameInfo.AddNoneClientCharacter(spawnedCharacter, true);
#endif

#if CLIENT
                            GameMain.GameSession.CrewManager.AddCharacter(spawnedCharacter);
#endif
                        }
                    }
                    else if(thischar.client != null && (thischar.client.Character == null || (thischar.client.Character != null && thischar.client.Character.IsDead)))
                    {
                        if (thischar.client.CharacterInfo == null)
                        {
                            thischar.client.CharacterInfo = new CharacterInfo(Character.HumanConfigFile, thischar.client.Name);
                        }
                        GameMain.Server.AssignJobs(new List<Client>() { thischar.client }, false);
                        thischar.client.CharacterInfo.Job = new Job(thischar.client.AssignedJob);

                        if(thischar.client.BypassSkillRequirements)
                        {
                            foreach (Skill skill in thischar.client.CharacterInfo.Job.Skills)
                            {
                                skill.Level = 100;
                            }
                        }

                        WayPoint Waypoint = WayPoint.SelectCrewSpawnPoints(new List<CharacterInfo>() { thischar.client.CharacterInfo }, Submarine.MainSubs[teamID - 1])[0];
                        Character spawnedCharacter = Character.Create(thischar.client.CharacterInfo, Waypoint.WorldPosition, true, false);
                        spawnedCharacter.TeamID = (byte)teamID;
                        spawnedCharacter.GiveJobItems(Waypoint);
                        

                        //Spawn protection
                        if (GameMain.NilMod.PlayerSpawnProtectMidgame)
                        {
                            if(GameMain.NilMod.PlayerSpawnProtectHealth != 0f) spawnedCharacter.SpawnProtectionHealth = GameMain.NilMod.PlayerSpawnProtectHealth;
                            if (GameMain.NilMod.PlayerSpawnProtectHealth != 0f) spawnedCharacter.SpawnProtectionOxygen = GameMain.NilMod.PlayerSpawnProtectOxygen;
                            if (GameMain.NilMod.PlayerSpawnProtectHealth != 0f) spawnedCharacter.SpawnProtectionPressure = GameMain.NilMod.PlayerSpawnProtectPressure;
                            if (GameMain.NilMod.PlayerSpawnProtectHealth != 0f) spawnedCharacter.SpawnProtectionStun = GameMain.NilMod.PlayerSpawnProtectStun;
                            if (GameMain.NilMod.PlayerSpawnProtectHealth != 0f) spawnedCharacter.SpawnRewireWaitTimer = GameMain.NilMod.PlayerSpawnRewireWaitTimer;
                        }

                        GameMain.GameScreen.RunIngameCommand("setclientcharacter", new object[] { thischar.client, spawnedCharacter });

#if CLIENT
                        GameSession.inGameInfo.UpdateClientCharacter(thischar.client, spawnedCharacter, false);
#endif

#if CLIENT
                        GameMain.GameSession.CrewManager.AddCharacter(spawnedCharacter);
#endif

                    }

                    GameSession.inGameInfo.GuiUpdateRequired = true;
                    GameSession.inGameInfo.Guiupdatetimer = 0f;

                    return true;
                };
                Buttoncount += 1;
            }
            //Client only buttons
            if (client != null)
            {
                //Set client character Button
                ButtonPosition = CalculatePageButtonPosition(Buttoncount, parent);
                GUITempImageButton = new GUIImageButton(new Rectangle((Int16)ButtonPosition.X, (Int16)ButtonPosition.Y, 20, 20)
                    , InGameInfo.ControlButton, Alignment.Left, this);
                //Colour Definition
                GUITempImageButton.Color = new Color(255, 255, 255, 255);
                GUITempImageButton.HoverColor = new Color(200, 200, 25, 255);
                GUITempImageButton.SelectedColor = new Color(100, 100, 100, 255);

                //Button image Definition
                //GUITempImageButton.Offset = new Vector2(6, 6);
                GUITempImageButton.Scale = 0.16f;
                GUITempImageButton.Padding = Vector4.Zero;
                GUITempImageButton.CanDoubleClick = false;

                //Button code / specifics
                GUITempImageButton.ToolTip = "Set client character";
                GUITempImageButton.UserData = parent.UserData;
                GUITempImageButton.OnClicked += (btn, userData) =>
                {
                    InGameInfoCharacter thischar = (InGameInfoCharacter)parent.UserData;

                    GameMain.NilMod.ClickCommandType = "setclientcharacter";
                    GameMain.NilMod.ActiveClickCommand = true;
                    GameMain.NilMod.ClickCooldown = 0.5f;
                    GameMain.Server.ClickCommandFrame.Visible = true;
                    GameMain.NilMod.ClickTargetClient = thischar.client;
                    GameMain.Server.ClickCommandDescription.Text = "SETCLIENTCHARACTER - " + GameMain.NilMod.ClickTargetClient.Name + " - Left Click close to a creatures center to have the client control it, right click to cancel.";

                    return true;
                };
                Buttoncount += 1;
            }

            if (parent != null) parent.AddChild(this);
        }

        Vector2 CalculatePageButtonPosition(int Button, GUIComponent parent)
        {
            int ButtonCoordX = parent.Rect.X - 20 + 6;
            int ButtonCoordY = parent.Rect.Y + 6;
            int processedbuttons = 0;

            while (processedbuttons <= Button)
            {
                
                if (processedbuttons % 5 == 0 && processedbuttons != 0)
                {
                    ButtonCoordY += 20;
                    ButtonCoordX -= 80;
                }
                else
                {
                    ButtonCoordX += 20;
                }

                processedbuttons += 1;
            }

            return new Vector2(ButtonCoordX, ButtonCoordY);
        }

        public override void Update(float deltaTime)
        {
            if (!Visible) return;
            base.Update(deltaTime);
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (!Visible) return;

            base.Draw(spriteBatch);

            Color currColor = color;
            //if (state == ComponentState.Hover) currColor = hoverColor;
            //if (state == ComponentState.Selected) currColor = selectedColor;
            if (state == ComponentState.Hover) Parent.State = ComponentState.Hover;
            if (state == ComponentState.Selected) Parent.State = ComponentState.Selected;

            DrawChildren(spriteBatch);
        }

    }


    class InGameInfo
    {
        private static Texture2D CommandIcons;
        private static Texture2D NoCommandIcon;

        public static Sprite HealButton;
        public static Sprite TeleportButton;
        public static Sprite KillButton;
        public static Sprite ControlButton;
        public static Sprite RespawnButton;

        public float LowestRemoveTimer;
        public int TotalRemovesleft;

        private GUIFrame ingameInfoFrame;
        private GUITextBlock ingameInfoFilterText;
        private GUITextBlock timerwarning;
        private GUIListBox clientguilist;

        private Sprite Controlsprites;

        private List<InGameInfoCharacter> characterlist;

        private List<InGameInfoCharacter> filteredcharacterlist;

        public float Guiupdatetimer;
        public Boolean GuiUpdateRequired;
        //Used to re-draw controls and other such for characters.
        private const float GuiUpdateTime = 2.0f;
        int currentfilter = 0;
        float IngameInfoScroll;
        int LastCharacterCount;

        public InGameInfo()
        {
            Initialize();
        }

        public void Initialize()
        {
            //if (CommandIcons == null) CommandIcons = TextureLoader.FromFile("Content/UI/NilMod/inventoryIcons.png");
            //if (NoCommandIcon == null) NoCommandIcon = TextureLoader.FromFile("Content/UI/NilMod/NoCommandIcon.png");
            if (NoCommandIcon == null) NoCommandIcon = TextureLoader.FromFile("Content/UI/uiButton.png");

            //HealButton = new Sprite("Content/UI/Nilmod/BtnBlank.png", new Rectangle(0, 0, 128, 128));
            if(HealButton == null) HealButton = new Sprite("Content/UI/Nilmod/BtnHeal.png", new Rectangle(0, 0, 128, 128));
            if (TeleportButton == null) TeleportButton = new Sprite("Content/UI/Nilmod/BtnTeleport.png", new Rectangle(0, 0, 128, 128));
            if (KillButton == null) KillButton = new Sprite("Content/UI/Nilmod/BtnKill.png", new Rectangle(0, 0, 128, 128));
            if (ControlButton == null) ControlButton = new Sprite("Content/UI/Nilmod/BtnControl.png", new Rectangle(0, 0, 128, 128));
            if (RespawnButton == null) RespawnButton = new Sprite("Content/UI/Nilmod/BtnRespawn.png", new Rectangle(0, 0, 128, 128));

            characterlist = new List<InGameInfoCharacter>();
            filteredcharacterlist = new List<InGameInfoCharacter>();

            ingameInfoFilterText = null;
            timerwarning = null;
            clientguilist = null;
            ingameInfoFrame = null;

            //InGameInfoClient Host = new InGameInfoClient();
            //clientlist.Add(Host);


            currentfilter = 0;
        }

        public void AddClient(Client newclient)
        {
            InGameInfoCharacter newingameinfoclient = new InGameInfoCharacter();
            newingameinfoclient.client = newclient;
            characterlist.Add(newingameinfoclient);
            //UpdateGameInfoGUIList();
        }

        public void RemoveClient(Client removedclient)
        {
            InGameInfoCharacter inGameInfoClienttoremove = characterlist.Find(c => c.client == removedclient);
            if (inGameInfoClienttoremove != null)
            {
                if (inGameInfoClienttoremove.character != null)
                {
                    //We need to keep the character itself.
                    inGameInfoClienttoremove.client = null;
                }
                else
                {
                    //This is not a client, safe to completely remove.
                    RemoveEntry(inGameInfoClienttoremove);
                }

                TriggerGUIUpdate();
            }
            List<InGameInfoCharacter> inGameInfoPreviousClients = characterlist.FindAll(c => c.previousclient == removedclient);

            //Clear references of "Previous client"
            foreach(InGameInfoCharacter previousclient in inGameInfoPreviousClients)
            {
                previousclient.previousclient = null;
            }
        }

        public void AddNoneClientCharacter(Character newcharacter, Boolean IsHost = false)
        {
            InGameInfoCharacter newingameinfocharacter = new InGameInfoCharacter();
            newingameinfocharacter.character = newcharacter;

            //Only one host
            if(IsHost)
            {
                newingameinfocharacter.IsHostCharacter = IsHost;
                for (int i = characterlist.Count() - 1; i >= 0; i--)
                {
                    if(characterlist[i].IsHostCharacter)
                    {
                        characterlist[i].IsHostCharacter = false;
                        //This reference is no longer a hostcharacter or has a character, so remove it
                        if (characterlist[i].character == null) RemoveEntry(characterlist[i]);
                    }
                }
                //Set hosts character to be the first in the list by re-sorting it
                List<InGameInfoCharacter> newlist = new List<InGameInfoCharacter>();
                newlist.Add(newingameinfocharacter);
                newlist.AddRange(characterlist);
                characterlist = newlist;
            }
            else
            {
                characterlist.Add(newingameinfocharacter);
            }
            
            TriggerGUIUpdate();
        }

        //Setting of a clients character or respawning characters need their entry (And thus the GUI) Updated.
        //This also includes modifying their current character and handling of their old characters.
        public void UpdateClientCharacter(Client clienttoupdate, Character newcharacter, Boolean UpdateGUIList = false)
        {
            InGameInfoCharacter inGameInfoClienttochange = characterlist.Find(c => c.client == clienttoupdate);
            if (inGameInfoClienttochange != null)
            {
                if(inGameInfoClienttochange.character != null && inGameInfoClienttochange.character != newcharacter)
                {
                    //Create a new InGameInfoCharacter for the now orphened char
                    InGameInfoCharacter newingameinfocharacter = new InGameInfoCharacter();
                    newingameinfocharacter.character = inGameInfoClienttochange.character;
                    //newingameinfocharacter.IsHostCharacter = inGameInfoClienttochange.IsHostCharacter;
                    newingameinfocharacter.previousclient = inGameInfoClienttochange.client;
                    characterlist.Add(newingameinfocharacter);
                }
                if(newcharacter != null)
                {
                    InGameInfoCharacter existingingameinfocharacter = characterlist.Find(c => c.character == newcharacter && c.client != inGameInfoClienttochange.client);
                    if(existingingameinfocharacter != null)
                    {
                        if(existingingameinfocharacter.client == null)
                        {
                            RemoveCharacter(existingingameinfocharacter.character);
                        }
                        else
                        {
                            DebugConsole.NewMessage("NILMOD ERROR - InGameInfo GUI changed clientname: " + existingingameinfocharacter.client.Name + "'s character to be controlled by client: " + inGameInfoClienttochange.client.Name + " (This shouldn't be possible and is a severe error)", Color.Red, false);
                            existingingameinfocharacter.client = null;
                        }
                    }
                }
                inGameInfoClienttochange.character = newcharacter;
                inGameInfoClienttochange.previousclient = null;

                TriggerGUIUpdate();
            }
        }

        public void RemoveCharacter(Character character)
        {
            InGameInfoCharacter inGameInfoCharactertoremove = characterlist.Find(c => c.character == character);
            if (inGameInfoCharactertoremove != null)
            {
                if(inGameInfoCharactertoremove.client != null)
                {
                    //We need to keep the client itself.
                    inGameInfoCharactertoremove.character = null;
                }
                else if(inGameInfoCharactertoremove.IsHostCharacter)
                {
                    //This is the host character, we should keep this even if theres no character or client
                    inGameInfoCharactertoremove.character = null;
                }
                else
                {
                    //This is not a client, safe to completely remove.
                    RemoveEntry(inGameInfoCharactertoremove);
                }
                TriggerGUIUpdate();
            }
        }

        public void RemoveEntry(InGameInfoCharacter removed)
        {
            if (removed.Removed) return;

            if(GameMain.NetworkMember != null && GameMain.NetworkMember.GameStarted)
            {
                removed.Removed = true;
                removed.RemovalTimer = 10f;
            }
            else
            {
                characterlist.Remove(removed);
            }
        }

        //Wipe the characters clean and Reset all character data (Keep clients if applicable)
        //close the ingameinfo and reset the filter to 0
        public void ResetGUIListData()
        {
            if (ingameInfoFrame != null) ToggleGameInfoFrame(null, null);
            currentfilter = 0;

            for (int i = characterlist.Count() - 1; i >= 0; i--)
            {
                if (characterlist[i].Removed)
                {
                    characterlist.RemoveAt(i);
                }
                else if (characterlist[i].client != null)
                {
                    characterlist[i].character = null;
                    characterlist[i].previousclient = null;
                }
                else if (characterlist[i].client == null)
                {
                    characterlist.RemoveAt(i);
                }
            }
        }

        public void AddToGUIUpdateList()
        {
            if (ingameInfoFrame != null) ingameInfoFrame.AddToGUIUpdateList();
        }

        public void TriggerGUIUpdate()
        {
            if (GuiUpdateRequired) return;

            GuiUpdateRequired = true;
            Guiupdatetimer = GuiUpdateTime;
        }

        public void Update(float deltaTime)
        {
            if (characterlist != null && characterlist.Count > 0)
            {
                Boolean NeedsRemoval = false;
                float? HighestTimer = null;
                for (int i = characterlist.Count - 1; i >= 0; i--)
                {
                    if (characterlist[i].Removed)
                    {
                        characterlist[i].RemovalTimer -= deltaTime;
                        if (HighestTimer == null)
                        {
                            HighestTimer = characterlist[i].RemovalTimer;
                        }
                        else
                        {
                            if (HighestTimer < characterlist[i].RemovalTimer) HighestTimer = characterlist[i].RemovalTimer;
                        }
                        //if (characterlist[i].RemovalTimer <= 0f)
                        //{
                            //characterlist.RemoveAt(i);
                            //NeedsRemoval = true;
                        //}
                    }
                }

                if(HighestTimer != null && HighestTimer <= 0f) NeedsRemoval = true;

                if (NeedsRemoval)
                {
                    for (int i = characterlist.Count - 1; i >= 0; i--)
                    {
                        if (characterlist[i].Removed) characterlist.RemoveAt(i);
                    }
                    UpdateGameInfoGUIList();
                    Guiupdatetimer = 0f;
                    GuiUpdateRequired = false;
                }
                else if (GuiUpdateRequired)
                {
                    Guiupdatetimer -= deltaTime;

                    if (Guiupdatetimer <= 0f)
                    {
                        UpdateGameInfoGUIList();
                        GuiUpdateRequired = false;
                    }
                }

                if (timerwarning != null)
                {
                    if (HighestTimer != null && HighestTimer <= 10f)
                    {
                        timerwarning.Visible = true;
                        timerwarning.Text = "Removal in: " + Math.Round((float)HighestTimer, 1) + "s";
                    }
                    else
                    {
                        timerwarning.Visible = false;
                        timerwarning.Text = "";
                    }
                }
            }
            else
            {
                if (timerwarning != null) timerwarning.Visible = false;
            }

            if (ingameInfoFrame != null)
            {
                ingameInfoFrame.Update(deltaTime);
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (ingameInfoFrame != null) ingameInfoFrame.Draw(spriteBatch);
        }

        public bool ToggleGameInfoFrame(GUIButton button, object obj)
        {
            if (ingameInfoFrame == null)
            {
                CreateGameInfoFrame();
            }
            else
            {
                ingameInfoFilterText = null;
                timerwarning = null;
                clientguilist = null;
                ingameInfoFrame = null;
            }

            return true;
        }



        public void CreateGameInfoFrame()
        {
            int width = 200, height = 600;


            ingameInfoFrame = new GUIFrame(
                Rectangle.Empty, new Color(0,0,0,0), "", null);
            ingameInfoFrame.CanBeFocused = false;

            var innerFrame = new GUIFrame(
                new Rectangle(-70, 50, width, height), new Color(255, 255, 255, 100), "", ingameInfoFrame);

            innerFrame.Padding = new Vector4(10.0f, 10.0f, 10.0f, 10.0f);

            var LeftButton = new GUIButton(new Rectangle(20, -30, 85, 20), "<-", "", innerFrame);
            LeftButton.UserData = -1;
            LeftButton.OnClicked += (btn, userData) =>
            {
                ChangeFilter(Convert.ToInt32(userData));
                UpdateGameInfoGUIList();
                return true;
            };

                var RightButton = new GUIButton(new Rectangle(105, -30, 85, 20), "->", "", innerFrame);
            RightButton.UserData = +1;
            RightButton.OnClicked += (btn, userData) =>
            {
                ChangeFilter(Convert.ToInt32(userData));
                UpdateGameInfoGUIList();
                return true;
            };

            timerwarning = new GUITextBlock(new Rectangle(25, 18, 150, 10), "", new Color(0, 0, 0, 0), new Color(200, 200, 10, 255), Alignment.Left, Alignment.Center,null, innerFrame, false);
            timerwarning.TextScale = 0.78f;

            ingameInfoFilterText = new GUITextBlock(new Rectangle(25, 0, 150, 20), "Filter: None", new Color(0,0,0,0),new Color(255, 255, 255, 255), Alignment.Left, Alignment.Center, "", innerFrame,false);

            clientguilist = new GUIListBox(new Rectangle(30, 30, 150, 500), new Color(15, 15, 15, 180), "", innerFrame);
            clientguilist.OutlineColor = new Color(0, 0, 0, 0);
            clientguilist.HoverColor = new Color(255, 255, 255, 20);
            clientguilist.SelectedColor = new Color(15, 15, 15, 20);
            clientguilist.OnSelected += (btn, userData) =>
            {
                clientguilist.Deselect();
                return true;
            };
            UpdateGameInfoGUIList();
        }

        public void UpdateGameInfoGUIList()
        {
            //Only update if its actually running and open (IE. ingame, etc) - it will do the necessary update on creation anyways
            if (ingameInfoFrame != null)
            {
                ChangeFilter(0);
                if (filteredcharacterlist.Count() > 0 && LastCharacterCount > 0)
                {
                    int scrolldifference = LastCharacterCount - filteredcharacterlist.Count();
                    float scrollchangepercent = LastCharacterCount / filteredcharacterlist.Count();
                    float newscroll = ((clientguilist.BarScroll * LastCharacterCount * 150)) / (150 * filteredcharacterlist.Count());
                    IngameInfoScroll = newscroll;
                    //Removed items
                    if (scrolldifference > 0)
                    {
                        newscroll = MathHelper.Clamp((clientguilist.BarScroll + ((scrolldifference * clientguilist.BarScroll) / filteredcharacterlist.Count())), 0f, 1f);
                    }
                    //Added Items
                    else if (scrolldifference < 0)
                    {
                        //TODO - Plead for mercy with somebody who actually understands how to math scrollbars
                        //This is the best "Scrollbar smoothing" A dummy like Nilanth could manage without another 50 hours tweaking it.
                        if (clientguilist.BarScroll < 0.002f)
                        {
                            newscroll = 0f;
                        }
                        else if (clientguilist.BarScroll < 0.1f)
                        {
                            newscroll = MathHelper.Clamp((clientguilist.BarScroll - ((((-scrolldifference * clientguilist.BarScroll)) / (filteredcharacterlist.Count() - -scrolldifference)) + (-0.04f / filteredcharacterlist.Count()))), 0f, 1f);
                        }
                        else if (clientguilist.BarScroll < 0.2f)
                        {
                            newscroll = MathHelper.Clamp((clientguilist.BarScroll - ((((-scrolldifference * clientguilist.BarScroll)) / (filteredcharacterlist.Count() - -scrolldifference)) + (-0.03f / filteredcharacterlist.Count()))), 0f, 1f);
                        }
                        else if (clientguilist.BarScroll < 0.3f)
                        {
                            newscroll = MathHelper.Clamp((clientguilist.BarScroll - ((((-scrolldifference * clientguilist.BarScroll)) / (filteredcharacterlist.Count() - -scrolldifference)) + (-0.02f / filteredcharacterlist.Count()))), 0f, 1f);
                        }
                        else if (clientguilist.BarScroll < 0.4f)
                        {
                            newscroll = MathHelper.Clamp((clientguilist.BarScroll - ((((-scrolldifference * clientguilist.BarScroll)) / (filteredcharacterlist.Count() - -scrolldifference)) + (-0.01f / filteredcharacterlist.Count()))), 0f, 1f);
                        }
                        else if (clientguilist.BarScroll < 0.5f)
                        {
                            newscroll = MathHelper.Clamp((clientguilist.BarScroll - ((((-scrolldifference * clientguilist.BarScroll)) / (filteredcharacterlist.Count() - -scrolldifference)) + (0.01f / filteredcharacterlist.Count()))), 0f, 1f);
                        }
                        else if (clientguilist.BarScroll < 0.6f)
                        {
                            newscroll = MathHelper.Clamp((clientguilist.BarScroll - ((((-scrolldifference * clientguilist.BarScroll)) / (filteredcharacterlist.Count() - -scrolldifference)) + (0.015f / filteredcharacterlist.Count()))), 0f, 1f);
                        }
                        else if (clientguilist.BarScroll < 0.7f)
                        {
                            newscroll = MathHelper.Clamp((clientguilist.BarScroll - ((((-scrolldifference * clientguilist.BarScroll)) / (filteredcharacterlist.Count() - -scrolldifference)) + (0.15f / filteredcharacterlist.Count()))), 0f, 1f);
                        }
                        else if (clientguilist.BarScroll < 0.8f)
                        {
                            newscroll = MathHelper.Clamp((clientguilist.BarScroll - ((((-scrolldifference * clientguilist.BarScroll)) / (filteredcharacterlist.Count() - -scrolldifference)) + (0.2f / filteredcharacterlist.Count()))), 0f, 1f);
                        }
                        else if (clientguilist.BarScroll < 0.9f)
                        {
                            newscroll = MathHelper.Clamp((clientguilist.BarScroll - ((((-scrolldifference * clientguilist.BarScroll)) / (filteredcharacterlist.Count() - -scrolldifference)) + (0.22f / filteredcharacterlist.Count()))), 0f, 1f);
                        }
                        else
                        {
                            newscroll = MathHelper.Clamp((clientguilist.BarScroll - ((((-scrolldifference * clientguilist.BarScroll)) / (filteredcharacterlist.Count() - -scrolldifference)) + (0.25f / filteredcharacterlist.Count()))), 0f, 1f);
                        }
                    }
                    //Same item count
                    else
                    {
                        newscroll = IngameInfoScroll;
                    }

                    IngameInfoScroll = newscroll;
                }
                clientguilist.children = new List<GUIComponent>();

                for (int i = 0; i < filteredcharacterlist.Count(); i++)
                {
                    GUIFrame frame = new GUIFrame(new Rectangle(0, 0, 160, 150), Color.Transparent, "ListBoxElement", clientguilist);
                    frame.UserData = filteredcharacterlist[i];
                    frame.Padding = new Vector4(5.0f, 5.0f, 5.0f, 5.0f);
                    frame.Color = new Color(0, 0, 0, 0);
                    frame.SelectedColor = new Color(0, 0, 0, 50);
                    //frame.CanBeFocused = false;

                    if (!filteredcharacterlist[i].Removed)
                    {
                        int TextHeight = -10;

                        //Clients name (Not their characters name)
                        if (filteredcharacterlist[i].client != null || filteredcharacterlist[i].IsHostCharacter)
                        {
                            GUITextBlock textBlockclientname = new GUITextBlock(
                                new Rectangle(22, TextHeight, 100, 15),
                                "",
                                null, null,
                                Alignment.TopLeft, Alignment.TopLeft,
                                "", frame);
                            textBlockclientname.TextScale = 0.8f;
                            //textBlockclientname.Padding = new Vector4(5.0f, 0.0f, 5.0f, 0.0f);

                            TextHeight += 10;

                            if (filteredcharacterlist[i].client != null)
                            {
                                textBlockclientname.Text = ToolBox.LimitString("CL: " + filteredcharacterlist[i].client.Name, GUI.Font, frame.Rect.Width - 10);
                            }
                            else
                            {
                                textBlockclientname.Text = "Host Character";
                            }
                        }

                        GUITextBlock textBlockcharactername = new GUITextBlock(
                                new Rectangle(22, TextHeight, 100, 15),
                                "",
                                null, null,
                                Alignment.TopLeft, Alignment.TopLeft,
                                "", frame);
                        textBlockcharactername.TextScale = 0.8f;
                        //textBlockcharactername.Padding = new Vector4(5.0f, 0.0f, 5.0f, 0.0f);

                        TextHeight += 10;

                        if (filteredcharacterlist[i].character != null && !filteredcharacterlist[i].character.IsDead)
                        {
                            textBlockcharactername.Text = ToolBox.LimitString("Chr: " + filteredcharacterlist[i].character.Name, GUI.Font, frame.Rect.Width - 10);
                        }
                        else if (filteredcharacterlist[i].client != null && (filteredcharacterlist[i].client.NeedsMidRoundSync || !filteredcharacterlist[i].client.InGame))
                        {
                            textBlockcharactername.Text = "Chr: Lobby";
                        }
                        else if (filteredcharacterlist[i].client != null && filteredcharacterlist[i].client.InGame && filteredcharacterlist[i].client.SpectateOnly)
                        {
                            textBlockcharactername.Text = "Chr: Spectator";
                        }
                        else if (filteredcharacterlist[i].character != null && !filteredcharacterlist[i].character.IsDead)
                        {
                            textBlockcharactername.Text = "Chr: Corpse";
                        }
                        else if (filteredcharacterlist[i].client != null && filteredcharacterlist[i].client.InGame)
                        {
                            textBlockcharactername.Text = "Chr: Ghost";
                        }

                        if (filteredcharacterlist[i].character != null && filteredcharacterlist[i].character.SpeciesName.ToLowerInvariant() == "human")
                        {
                            GUITextBlock textBlockjob = new GUITextBlock(
                                new Rectangle(22, TextHeight, 100, 20),
                                "",
                                null, null,
                                Alignment.TopLeft, Alignment.TopLeft,
                                "", frame);
                            textBlockjob.TextScale = 0.8f;
                            //textBlockjob.Padding = new Vector4(5.0f, 0.0f, 5.0f, 0.0f);

                            TextHeight += 10;

                            if (filteredcharacterlist[i].character.Info != null)
                            {
                                textBlockjob.Text = ToolBox.LimitString("JB: " + filteredcharacterlist[i].character.Info.Job.Name, GUI.Font, frame.Rect.Width - 10);
                            }
                        }


                        GUITextBlock textBlockteam = new GUITextBlock(
                                new Rectangle(22, TextHeight, 100, 20),
                                "",
                                null, null,
                                Alignment.TopLeft, Alignment.TopLeft,
                                "", frame);
                        textBlockteam.TextScale = 0.8f;
                        //textBlockteam.Padding = new Vector4(5.0f, 0.0f, 5.0f, 0.0f);

                        TextHeight += 10;

                        if (filteredcharacterlist[i].character != null)
                        {
                            if (filteredcharacterlist[i].character.TeamID == 0)
                            {
                                if (Character.Controlled == filteredcharacterlist[i].character)
                                {
                                    textBlockteam.Text = "T: Host Controlled";
                                }
                                else if (filteredcharacterlist[i].client != null)
                                {
                                    textBlockteam.Text = "T: Neutral";
                                }
                                else if (filteredcharacterlist[i].character.AIController is HumanAIController && filteredcharacterlist[i].client == null && Character.Controlled != filteredcharacterlist[i].character)
                                {
                                    textBlockteam.Text = "T: AI Human";
                                }
                                else
                                {
                                    textBlockteam.Text = "T: Fish";
                                }
                            }
                            else if (filteredcharacterlist[i].character.TeamID == 1)
                            {
                                textBlockteam.Text = "T: Coalition";
                            }
                            else if (filteredcharacterlist[i].character.TeamID == 2)
                            {
                                textBlockteam.Text = "T: Renegades";
                            }
                        }
                        else if(filteredcharacterlist[i].client != null)
                        {
                            if (filteredcharacterlist[i].client.TeamID == 0)
                            {
                                textBlockteam.Text = "T: Neutral";
                            }
                            else if (filteredcharacterlist[i].client.TeamID == 1)
                            {
                                textBlockteam.Text = "T: Coalition";
                            }
                            else if (filteredcharacterlist[i].client.TeamID == 2)
                            {
                                textBlockteam.Text = "T: Renegades";
                            }
                        }
                        else if(filteredcharacterlist[i].IsHostCharacter)
                        {
                            if (GameMain.NetworkMember.CharacterInfo == null || GameMain.NetworkMember.CharacterInfo != null && GameMain.NetworkMember.CharacterInfo.TeamID == 0)
                            {
                                textBlockteam.Text = "T: Neutral";
                            }
                            if (GameMain.NetworkMember.CharacterInfo != null && GameMain.NetworkMember.CharacterInfo.TeamID == 1)
                            {
                                textBlockteam.Text = "T: Coalition";
                            }
                            else if (GameMain.NetworkMember.CharacterInfo != null && GameMain.NetworkMember.CharacterInfo.TeamID == 2)
                            {
                                textBlockteam.Text = "T: Renegades";
                            }
                        }








                        /*

                        //Client job and team if applicable.
                        //If they have a character with a valid info use this first
                        if (filteredcharacterlist[i].character != null && filteredcharacterlist[i].character.Info != null && filteredcharacterlist[i].character.Info.Job != null)
                        {
                            textBlockjob.Text = ToolBox.LimitString(filteredcharacterlist[i].character.Info.Job.Name, GUI.Font, frame.Rect.Width - 20);

                            switch (filteredcharacterlist[i].character.TeamID)
                            {
                                case 0:
                                    textBlockjob.Text = ToolBox.LimitString(textBlockjob.Text + " (Creature)", GUI.Font, frame.Rect.Width - 20);
                                    break;
                                case 1:
                                    textBlockjob.Text = ToolBox.LimitString(textBlockjob.Text + " (Coalition)", GUI.Font, frame.Rect.Width - 20);
                                    break;
                                case 2:
                                    textBlockjob.Text = ToolBox.LimitString(textBlockjob.Text + " (Renegade)", GUI.Font, frame.Rect.Width - 20);
                                    break;
                                default:
                                    textBlockjob.Text = ToolBox.LimitString(textBlockjob.Text + " (Team NA)", GUI.Font, frame.Rect.Width - 20);
                                    break;
                            }
                        }
                        //If they do not have a character with valid info, use the clients info if it exists
                        else if (filteredcharacterlist[i].client != null && filteredcharacterlist[i].client.CharacterInfo != null)
                        {
                            if (filteredcharacterlist[i].client.NeedsMidRoundSync || !filteredcharacterlist[i].client.InGame)
                            {
                                textBlockjob.Text = ToolBox.LimitString("Not In Game", GUI.Font, frame.Rect.Width - 20);
                            }
                            else if (filteredcharacterlist[i].character != null && filteredcharacterlist[i].character.SpeciesName.ToLowerInvariant() == "human" && filteredcharacterlist[i].character.Info == null)
                            {
                                textBlockjob.Text = ToolBox.LimitString("Unemployed", GUI.Font, frame.Rect.Width - 20);
                            }
                            else if(filteredcharacterlist[i].character != null && filteredcharacterlist[i].character.SpeciesName.ToLowerInvariant() == "human" && filteredcharacterlist[i].character.Info == null)
                            {

                            }
                            else
                            {
                                textBlockjob.Text = ToolBox.LimitString("Fish", GUI.Font, frame.Rect.Width - 20);
                            }

                            switch (filteredcharacterlist[i].client.CharacterInfo.TeamID)
                            {
                                case 0:
                                    textBlockjob.Text = ToolBox.LimitString(textBlockjob.Text + " (Creature)", GUI.Font, frame.Rect.Width - 20);
                                    break;
                                case 1:
                                    textBlockjob.Text = ToolBox.LimitString(textBlockjob.Text + " (Coalition)", GUI.Font, frame.Rect.Width - 20);
                                    break;
                                case 2:
                                    textBlockjob.Text = ToolBox.LimitString(textBlockjob.Text + " (Renegade)", GUI.Font, frame.Rect.Width - 20);
                                    break;
                                default:
                                    textBlockjob.Text = ToolBox.LimitString(textBlockjob.Text + " (Team NA)", GUI.Font, frame.Rect.Width - 20);
                                    break;
                            }
                        }
                        //If they don't even have a character info classify them as a fish ><>
                        else if(filteredcharacterlist[i].character != null)
                        {
                            textBlockjob.Text = ToolBox.LimitString("Fish (Creature)", GUI.Font, frame.Rect.Width - 20);
                        }

                        */

                        GUIImageButton GUIImageCharsprite = null;

                        if (filteredcharacterlist[i].character != null && filteredcharacterlist[i].character.AnimController != null && filteredcharacterlist[i].character.AnimController.Limbs != null)
                        {
                            Limb CharspriteLimb = filteredcharacterlist[i].character.AnimController.Limbs.ToList().Find(l => l.type == LimbType.Head);
                            if (CharspriteLimb != null)
                            {
                                //Sprite Charsprite = new Sprite(CharspriteLimb.sprite.Texture,new Rectangle(0,0,25,25),new Vector2(0,0),0f);
                                //Charsprite.size = new Vector2(25, 25);
                                //Charsprite.size = CharspriteLimb.sprite.size;
                                GUIImageCharsprite = new GUIImageButton(new Rectangle(0, 0, 25, 80), CharspriteLimb.sprite, Alignment.Left, frame);
                                float rescalesize = (CharspriteLimb.sprite.size.X * 1.5f);
                                if (rescalesize < (CharspriteLimb.sprite.size.Y / 1.5f)) rescalesize = (CharspriteLimb.sprite.size.Y / 1.3f);
                                float newscale = 1f;

                                //Colour Definition
                                GUIImageCharsprite.Color = new Color(255, 255, 255, 255);
                                GUIImageCharsprite.HoverColor = new Color(200, 200, 25, 255);
                                GUIImageCharsprite.SelectedColor = new Color(100, 100, 100, 255);

                                GUIImageCharsprite.UserData = frame.UserData;
                                GUIImageCharsprite.OnClicked += (btn, userData) =>
                                {
                                    InGameInfoCharacter thischar = (InGameInfoCharacter)frame.UserData;

                                //Only spy if not already controlling
                                if (Character.Controlled != thischar.character)
                                    {
                                        Character.Spied = thischar.character;
                                        GameMain.GameScreen.Cam.Zoom = 0.8f;
                                        GameMain.GameScreen.Cam.Position = Character.Spied.WorldPosition;
                                        GameMain.GameScreen.Cam.UpdateTransform(true);
                                    }
                                    return true;
                                };

                                GUIImageCharsprite.OnDoubleClicked += (btn, userData) =>
                                {
                                    InGameInfoCharacter thischar = (InGameInfoCharacter)frame.UserData;

                                //Do not take control of client characters or remote players
                                if (thischar.client == null && !thischar.character.IsRemotePlayer)
                                    {
                                    //Remove the spy effect if setting control
                                    Character.Spied = null;
                                        Character.Controlled = thischar.character;
                                    //GameMain.GameScreen.Cam.Zoom = 0.8f;
                                    GameMain.GameScreen.Cam.Position = Character.Controlled.WorldPosition;
                                        GameMain.GameScreen.Cam.UpdateTransform(true);
                                    }
                                    return true;
                                };

                                while (rescalesize > 125f)
                                {
                                    newscale = newscale / 2f;
                                    //rescalesize -= 50f;
                                    rescalesize = rescalesize / 2f;
                                }

                                while (rescalesize > 60f)
                                {
                                    newscale = newscale / 1.25f;
                                    //rescalesize -= 50f;
                                    rescalesize = rescalesize / 1.25f;
                                }

                                GUIImageCharsprite.Scale = newscale;
                                GUIImageCharsprite.Rotation = 0f;
                            }
                            else
                            {
                                //TODO - add code for No creature image found (HEADLESS creatures? DEFINATELY DESERVES SOMETHING THERE like decapitation or question mark)
                                GUIImageCharsprite = null;
                            }

                            StatusWidget playerstatus = new StatusWidget(new Rectangle(25, 40, 100, 46), Alignment.Left, filteredcharacterlist[i].character, frame);
                        }

                        ControlWidget playercontrols = new ControlWidget(new Rectangle(25, 86, 100, 50), Alignment.Left, filteredcharacterlist[i].character, filteredcharacterlist[i].client, frame);
                    }
                    else
                    {
                        GUITextBlock removallabel = new GUITextBlock(new Rectangle(25, 40, 100, 46), "Character no\nlonger exists.", null, Alignment.Left, Alignment.Center, frame, false);
                        removallabel.TextColor = Color.Red;
                        removallabel.Visible = true;
                        removallabel.Color = new Color(150, 90, 5, 10);
                        removallabel.HoverColor = new Color(150, 90, 5, 10);
                        removallabel.OutlineColor = new Color(150, 90, 5, 10);
                        removallabel.SelectedColor = new Color(150, 90, 5, 10);
                        //GUI.DrawRectangle(spriteBatch, new Vector2(frame.Rect.X, frame.Rect.Y), new Vector2(frame.Rect.Width, frame.Rect.Height), new Color(150, 90, 5, 10), true, 0f, 1);
                        removallabel.Rect = new Rectangle(frame.Rect.X, frame.Rect.Y, frame.Rect.Width, frame.Rect.Height);
                        removallabel.TextScale = 1.1f;
                    }
                }

                LastCharacterCount = filteredcharacterlist.Count;
                clientguilist.BarScroll = IngameInfoScroll;
            }
        }

        public void ChangeFilter(int filterincrement)
        {
            currentfilter = currentfilter + filterincrement;
            //Page Cycling
            if (currentfilter < 0) currentfilter = 7;
            if (currentfilter > 7) currentfilter = 0;

            if (characterlist.Count() == 0)
            {
                currentfilter = 0;
                return;
            }

            List<InGameInfoCharacter> Searchlist = characterlist.ToList<InGameInfoCharacter>();
            if (filterincrement != 0)
            {
                if (Searchlist.Count > 0)
                {
                    for (int i = Searchlist.Count - 1; i >= 0; i--)
                    {
                        if (Searchlist[i].character != null && Searchlist[i].character.Removed)
                        {
                            RemoveCharacter(Searchlist[i].character);
                            if (filterincrement != 0)
                            {
                                Searchlist[i].RemovalTimer = 0f;
                                Searchlist.RemoveAt(i);
                            }
                        }
                    }
                }
            }

            filteredcharacterlist = new List<InGameInfoCharacter>();
            //Server Filters
            if (GameMain.Server != null)
            {
                switch (currentfilter)
                {
                    case 0:     //0 - All Clients
                        ingameInfoFilterText.Text = "Filter: All Clients";

                        //Host character
                        filteredcharacterlist.AddRange(Searchlist.FindAll(cl =>
                        (cl.character != null && (cl.character == Character.Controlled))
                        || cl.IsHostCharacter));

                        Searchlist.RemoveAll(sl => filteredcharacterlist.Find(fcl => fcl == sl) != null);
                        filteredcharacterlist.AddRange(Searchlist.FindAll(cl =>
                        cl.client != null));
                        break;
                    case 1:     //1 - Coalition Clients
                        ingameInfoFilterText.Text = "Filter: Coalition Clients";

                        filteredcharacterlist.AddRange(Searchlist.FindAll(cl =>
                        (cl.IsHostCharacter && ((cl.character != null && cl.character.TeamID == 1) || (GameMain.Server.CharacterInfo != null && GameMain.Server.CharacterInfo.TeamID == 1)))));

                        Searchlist.RemoveAll(sl => filteredcharacterlist.Find(fcl => fcl == sl) != null);
                        filteredcharacterlist.AddRange(Searchlist.FindAll(cl =>
                        (cl.client != null && cl.client.TeamID == 1)));
                        break;
                    case 2:     //2 - Renegade Clients
                        ingameInfoFilterText.Text = "Filter: Renegade Clients";

                        filteredcharacterlist.AddRange(Searchlist.FindAll(cl =>
                        (cl.IsHostCharacter && ((cl.character != null && cl.character.TeamID == 2) || (GameMain.Server.CharacterInfo != null && GameMain.Server.CharacterInfo.TeamID == 2)))));

                        Searchlist.RemoveAll(sl => filteredcharacterlist.Find(fcl => fcl == sl) != null);
                        filteredcharacterlist.AddRange(Searchlist.FindAll(cl =>
                        (cl.client != null && cl.client.TeamID == 2)));
                        break;
                    case 3:     //3 - Creature Clients
                        ingameInfoFilterText.Text = "Filter: Creature Clients";
                        //filteredcharacterlist = filteredcharacterlist.FindAll(cl => ((cl.client != null || cl.IsHostCharacter) || (cl.character != null && cl.character == Character.Controlled)) && cl.character != null && cl.character.TeamID == 0);

                        filteredcharacterlist.AddRange(Searchlist.FindAll(cl =>
                        (cl.IsHostCharacter && cl.character != null && cl.character.TeamID == 0)));

                        Searchlist.RemoveAll(sl => filteredcharacterlist.Find(fcl => fcl == sl) != null);
                        filteredcharacterlist.AddRange(Searchlist.FindAll(cl =>
                        (cl.client != null && cl.client.TeamID == 0)));
                        break;
                    case 4:     //4 - Creature AI
                        ingameInfoFilterText.Text = "Filter: Creature AI";
                        Searchlist = Searchlist.FindAll(cl => cl.client == null || !cl.IsHostCharacter);
                        filteredcharacterlist = Searchlist.FindAll(cl => cl.character != null && cl.character != Character.Controlled
                        && cl.character.AIController is EnemyAIController && cl.character.TeamID == 0);
                        break;
                    case 5:     //5 - Human AI
                        ingameInfoFilterText.Text = "Filter: Human AI";
                        Searchlist = Searchlist.FindAll(sl => sl.client == null || !sl.IsHostCharacter);
                        filteredcharacterlist = Searchlist.FindAll(cl => cl.character != null && cl.character != Character.Controlled
                        && cl.character.AIController is HumanAIController);
                        break;
                    case 6:     //6 - Player Corpses
                        ingameInfoFilterText.Text = "Filter: Player Corpses";
                        filteredcharacterlist = Searchlist.FindAll(cl =>
                        cl.character != null
                        && (cl.character.IsRemotePlayer || cl.IsHostCharacter || cl.character?.Info?.Name == GameMain.NilMod.PlayYourselfName)
                        && cl.character.IsDead);
                        break;
                    case 7:     //7 - AI Corpses
                        ingameInfoFilterText.Text = "Filter: AI Corpses";
                        Searchlist = Searchlist.FindAll(cl => cl.character != null && (cl.character.AIController != null));
                        filteredcharacterlist = Searchlist.FindAll(cl => cl.character != null
                        && !cl.character.IsRemotePlayer
                        && cl.character.IsDead
                        && !cl.IsHostCharacter);
                        break;
                    default:    // Filter out of range.
                        ingameInfoFilterText.Text = "Filter: ERROR.";
                        filteredcharacterlist = new List<InGameInfoCharacter>();
                        break;
                }
            }
            //Client Filters
            else if (GameMain.Client != null)
            {
                switch (currentfilter)
                {
                    case 0:     //0 - All Clients
                        ingameInfoFilterText.Text = "Filter: Humans";
                        //Include the hosts original spawns and respawns, but only if their actually alive or controlled.
                        filteredcharacterlist = Searchlist.FindAll(cl => !cl.Removed && ( cl.character.SpeciesName.ToLowerInvariant() == "human"));
                        break;
                    default:
                        ChangeFilter(filterincrement);
                        break;
                }

            }
            //Single Player Filters
            else
            {
                switch (currentfilter)
                {
                    case 0:     //0 - All Clients
                        ingameInfoFilterText.Text = "Filter: Humans";
                        //Include the hosts original spawns and respawns, but only if their actually alive or controlled.
                        filteredcharacterlist = Searchlist.FindAll(cl => !cl.Removed && (cl.character.SpeciesName.ToLowerInvariant() == "human"));
                        break;

                    default:
                        ChangeFilter(filterincrement);
                        break;
                }
            }

            if (filteredcharacterlist.Count == 0 && currentfilter != 0)
            {
                if (filterincrement == 0) filterincrement = 1;
                ChangeFilter(filterincrement);
            }
        }

        public void RunCommand(string Command, string[] Arguments)
        {

        }
    }
}
