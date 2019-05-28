﻿using Barotrauma.Items.Components;
using FarseerPhysics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Barotrauma
{
    class CharacterHUD
    {
        private static Dictionary<Entity, int> orderIndicatorCount = new Dictionary<Entity, int>();
        const float ItemOverlayDelay = 1.0f;
        private static Item focusedItem;
        private static float focusedItemOverlayTimer;
        
        private static List<Item> brokenItems = new List<Item>();
        private static float brokenItemsCheckTimer;

        private static Dictionary<string, string> cachedHudTexts = new Dictionary<string, string>();

        private static GUIFrame hudFrame;
        public static GUIFrame HUDFrame
        {

            get
            {
                if (hudFrame == null)
                {
                    hudFrame = new GUIFrame(new RectTransform(Vector2.One, GUI.Canvas), style: null)
                    {
                        CanBeFocused = false
                    };
                }
                return hudFrame;
            }
        }

        private static string GetCachedHudText(string textTag, string keyBind)
        {
            if (cachedHudTexts.TryGetValue(textTag + keyBind, out string text))
            {
                return text;
            }
            text = TextManager.GetWithVariable(textTag, "[key]", keyBind);
            cachedHudTexts.Add(textTag + keyBind, text);
            return text;
        }
        
        public static void AddToGUIUpdateList(Character character)
        {
            if (GUI.DisableHUD) return;
            
            if (!character.IsUnconscious && character.Stun <= 0.0f)
            {
                if (character.Inventory != null)
                {
                    for (int i = 0; i < character.Inventory.Items.Length - 1; i++)
                    {
                        var item = character.Inventory.Items[i];
                        if (item == null || character.Inventory.SlotTypes[i] == InvSlotType.Any) continue;

                        foreach (ItemComponent ic in item.Components)
                        {
                            if (ic.DrawHudWhenEquipped) ic.AddToGUIUpdateList();
                        }
                    }
                }

                if (character.IsHumanoid && character.SelectedCharacter != null)
                {
                    character.SelectedCharacter.CharacterHealth.AddToGUIUpdateList();
                }
            }

            HUDFrame.AddToGUIUpdateList();
        }

        public static void Update(float deltaTime, Character character, Camera cam)
        {
            if (GUI.DisableHUD) { return; }

            if (!character.IsUnconscious && character.Stun <= 0.0f)
            {
                if (character.Inventory != null)
                {
                    if (!character.LockHands && character.Stun < 0.1f && 
                        (character.SelectedConstruction == null || character.SelectedConstruction?.GetComponent<Controller>()?.User != character))
                    {
                        character.Inventory.Update(deltaTime, cam);
                    }

                    for (int i = 0; i < character.Inventory.Items.Length - 1; i++)
                    {
                        var item = character.Inventory.Items[i];
                        if (item == null || character.Inventory.SlotTypes[i] == InvSlotType.Any) continue;

                        foreach (ItemComponent ic in item.Components)
                        {
                            if (ic.DrawHudWhenEquipped) ic.UpdateHUD(character, deltaTime, cam);
                        }
                    }
                }

                if (character.IsHumanoid && character.SelectedCharacter != null && character.SelectedCharacter.Inventory != null)
                {
                    if (character.SelectedCharacter.CanInventoryBeAccessed)
                    {
                        character.SelectedCharacter.Inventory.Update(deltaTime, cam);
                    }
                    character.SelectedCharacter.CharacterHealth.UpdateHUD(deltaTime);
                }

                Inventory.UpdateDragging();
            }

            if (focusedItem != null)
            {
                if (character.FocusedItem != null)
                {
                    focusedItemOverlayTimer = Math.Min(focusedItemOverlayTimer + deltaTime, ItemOverlayDelay + 1.0f);
                }
                else
                {
                    focusedItemOverlayTimer = Math.Max(focusedItemOverlayTimer - deltaTime, 0.0f);
                    if (focusedItemOverlayTimer <= 0.0f) focusedItem = null;
                }
            }

            if (brokenItemsCheckTimer > 0.0f)
            {
                brokenItemsCheckTimer -= deltaTime;
            }
            else
            {
                brokenItems.Clear();
                brokenItemsCheckTimer = 1.0f;
                foreach (Item item in Item.ItemList)
                {
                    if (!item.Repairables.Any(r => item.Condition < r.ShowRepairUIThreshold)) { continue; }
                    if (!Submarine.VisibleEntities.Contains(item)) { continue; }

                    Vector2 diff = item.WorldPosition - character.WorldPosition;
                    if (Submarine.CheckVisibility(character.SimPosition, character.SimPosition + ConvertUnits.ToSimUnits(diff)) == null)
                    {
                        brokenItems.Add(item); 
                    }                   
                }
            }
        }
        
        public static void Draw(SpriteBatch spriteBatch, Character character, Camera cam)
        {
            if (GUI.DisableHUD) return;
            
            character.CharacterHealth.Alignment = Alignment.Right;

            if (GameMain.GameSession?.CrewManager != null)
            {
                orderIndicatorCount.Clear();
                foreach (Pair<Order, float> timedOrder in GameMain.GameSession.CrewManager.ActiveOrders)
                {
                    DrawOrderIndicator(spriteBatch, cam, character, timedOrder.First, MathHelper.Clamp(timedOrder.Second / 10.0f, 0.2f, 1.0f));
                }

                if (character.CurrentOrder != null)
                {
                    DrawOrderIndicator(spriteBatch, cam, character, character.CurrentOrder, 1.0f);                    
                }                
            }

            foreach (Character.ObjectiveEntity objectiveEntity in character.ActiveObjectiveEntities)
            {
                DrawObjectiveIndicator(spriteBatch, cam, character, objectiveEntity, 1.0f);
            }

            foreach (Item brokenItem in brokenItems)
            {
                float dist = Vector2.Distance(character.WorldPosition, brokenItem.WorldPosition);
                Vector2 drawPos = brokenItem.DrawPosition;
                float alpha = Math.Min((1000.0f - dist) / 1000.0f * 2.0f, 1.0f);
                if (alpha <= 0.0f) continue;
                GUI.DrawIndicator(spriteBatch, drawPos, cam, 100.0f, GUI.BrokenIcon, 
                    Color.Lerp(Color.DarkRed, Color.Orange * 0.5f, brokenItem.Condition / brokenItem.MaxCondition) * alpha);                
            }

            if (!character.IsUnconscious && character.Stun <= 0.0f)
            {
                if (character.FocusedCharacter != null && character.FocusedCharacter.CanBeSelected)
                {
                    Vector2 startPos = character.DrawPosition + (character.FocusedCharacter.DrawPosition - character.DrawPosition) * 0.7f;
                    startPos = cam.WorldToScreen(startPos);

                    string focusName = character.FocusedCharacter.DisplayName;
                    if (character.FocusedCharacter.Info != null)
                    {
                        focusName = character.FocusedCharacter.Info.DisplayName;
                    }
                    Vector2 textPos = startPos;
                    Vector2 offset = GUI.Font.MeasureString(focusName);

                    textPos -= new Vector2(offset.X / 2, offset.Y);
                    
                    Color nameColor = Color.White;
                    if (character.TeamID != character.FocusedCharacter.TeamID)
                    {
                        nameColor = character.FocusedCharacter.TeamID == Character.TeamType.FriendlyNPC ? Color.SkyBlue : Color.Red;
                    }

                    GUI.DrawString(spriteBatch, textPos, focusName, nameColor, Color.Black * 0.7f, 2);
                    textPos.Y += offset.Y;
                    if (character.FocusedCharacter.CanInventoryBeAccessed)
                    {
                        GUI.DrawString(spriteBatch, textPos, GetCachedHudText("GrabHint", GameMain.Config.KeyBind(InputType.Grab).ToString()),
                            Color.LightGreen, Color.Black, 2, GUI.SmallFont);
                        textPos.Y += offset.Y;
                    }
                    if (character.FocusedCharacter.CharacterHealth.UseHealthWindow)
                    {
                        GUI.DrawString(spriteBatch, textPos, GetCachedHudText("HealHint", GameMain.Config.KeyBind(InputType.Health).ToString()),
                            Color.LightGreen, Color.Black, 2, GUI.SmallFont);
                        textPos.Y += offset.Y;
                    }
                    if (!string.IsNullOrEmpty(character.FocusedCharacter.customInteractHUDText))
                    {
                        GUI.DrawString(spriteBatch, textPos, character.FocusedCharacter.customInteractHUDText, Color.LightGreen, Color.Black, 2, GUI.SmallFont);
                        textPos.Y += offset.Y;
                    }
                }

                float circleSize = 1.0f;
                if (character.FocusedItem != null)
                {
                    if (focusedItem != character.FocusedItem)
                    {
                        focusedItemOverlayTimer = Math.Min(1.0f, focusedItemOverlayTimer);
                    }
                    focusedItem = character.FocusedItem;                    
                }

                if (focusedItem != null && focusedItemOverlayTimer > ItemOverlayDelay)
                {
                    Vector2 circlePos = cam.WorldToScreen(focusedItem.DrawPosition);
                    circleSize = Math.Max(focusedItem.Rect.Width, focusedItem.Rect.Height) * 1.5f;
                    circleSize = MathHelper.Clamp(circleSize, 45.0f, 100.0f) * Math.Min((focusedItemOverlayTimer - 1.0f) * 5.0f, 1.0f);
                    if (circleSize > 0.0f)
                    {
                        Vector2 scale = new Vector2(circleSize / GUI.Style.FocusIndicator.FrameSize.X);
                        GUI.Style.FocusIndicator.Draw(spriteBatch,
                            (int)((focusedItemOverlayTimer - 1.0f) * GUI.Style.FocusIndicator.FrameCount * 3.0f),
                            circlePos,
                            Color.LightBlue * 0.3f,
                            origin: GUI.Style.FocusIndicator.FrameSize.ToVector2() / 2,
                            rotate: (float)Timing.TotalTime,
                            scale: scale);
                    }

                    if (!GUI.DisableItemHighlights && !Inventory.DraggingItemToWorld)
                    {
                        var hudTexts = focusedItem.GetHUDTexts(character);

                        int dir = Math.Sign(focusedItem.WorldPosition.X - character.WorldPosition.X);

                        Vector2 offset = GUI.Font.MeasureString(focusedItem.Name);
                        Vector2 startPos = cam.WorldToScreen(focusedItem.DrawPosition);
                        startPos.Y -= (hudTexts.Count + 1) * offset.Y;
                        if (focusedItem.Sprite != null)
                        {
                            startPos.X += (int)(circleSize * 0.4f * dir);
                            startPos.Y -= (int)(circleSize * 0.4f);
                        }

                        Vector2 textPos = startPos;
                        if (dir == -1) textPos.X -= offset.X;

                        float alpha = MathHelper.Clamp((focusedItemOverlayTimer - ItemOverlayDelay) * 2.0f, 0.0f, 1.0f);

                        GUI.DrawString(spriteBatch, textPos, focusedItem.Name, Color.White * alpha, Color.Black * alpha * 0.7f, 2);
                        textPos.Y += offset.Y;
                        foreach (ColoredText coloredText in hudTexts)
                        {
                            if (dir == -1) textPos.X = (int)(startPos.X - GUI.SmallFont.MeasureString(coloredText.Text).X);
                            GUI.DrawString(spriteBatch, textPos, coloredText.Text, coloredText.Color * alpha, Color.Black * alpha * 0.7f, 2, GUI.SmallFont);
                            textPos.Y += offset.Y;
                        }
                    }                    
                }

                foreach (HUDProgressBar progressBar in character.HUDProgressBars.Values)
                {
                    progressBar.Draw(spriteBatch, cam);
                }
            }

            if (character.SelectedConstruction != null && 
                (character.CanInteractWith(Character.Controlled.SelectedConstruction) || Screen.Selected == GameMain.SubEditorScreen))
            {
                character.SelectedConstruction.DrawHUD(spriteBatch, cam, Character.Controlled);
            }

            if (character.Inventory != null)
            {
                for (int i = 0; i < character.Inventory.Items.Length - 1; i++)
                {
                    var item = character.Inventory.Items[i];
                    if (item == null || character.Inventory.SlotTypes[i] == InvSlotType.Any) continue;

                    foreach (ItemComponent ic in item.Components)
                    {
                        if (ic.DrawHudWhenEquipped) ic.DrawHUD(spriteBatch, character);
                    }
                }
            }
            bool drawPortraitToolTip = false;
            if (character.Stun <= 0.1f && !character.IsDead)
            {
                if (CharacterHealth.OpenHealthWindow == null && character.SelectedCharacter == null)
                {
                    if (character.Info != null)
                    {
                        character.Info.DrawPortrait(spriteBatch, HUDLayoutSettings.PortraitArea.Location.ToVector2(), targetWidth: HUDLayoutSettings.PortraitArea.Width);
                    }
                    drawPortraitToolTip = HUDLayoutSettings.PortraitArea.Contains(PlayerInput.MousePosition);
                }
                if (character.Inventory != null && !character.LockHands)
                {
                    character.Inventory.Locked = (character.SelectedConstruction?.GetComponent<Controller>()?.User == character);
                    character.Inventory.DrawOwn(spriteBatch);
                    character.Inventory.CurrentLayout = CharacterHealth.OpenHealthWindow == null && character.SelectedCharacter == null ?
                        CharacterInventory.Layout.Default :
                        CharacterInventory.Layout.Right;
                }
            }

            if (!character.IsUnconscious && character.Stun <= 0.0f)
            {
                if (character.IsHumanoid && character.SelectedCharacter != null && character.SelectedCharacter.Inventory != null)
                {
                    if (character.SelectedCharacter.CanInventoryBeAccessed)
                    {
                        ///character.Inventory.CurrentLayout = Alignment.Left;
                        character.SelectedCharacter.Inventory.CurrentLayout = CharacterInventory.Layout.Left;
                        character.SelectedCharacter.Inventory.DrawOwn(spriteBatch);
                    }
                    else
                    {
                        //character.Inventory.CurrentLayout = (CharacterHealth.OpenHealthWindow == null) ? Alignment.Center : Alignment.Left;
                    }
                    if (CharacterHealth.OpenHealthWindow == character.SelectedCharacter.CharacterHealth)
                    {
                        character.SelectedCharacter.CharacterHealth.Alignment = Alignment.Left;
                        character.SelectedCharacter.CharacterHealth.DrawStatusHUD(spriteBatch);
                    }
                }
                else if (character.Inventory != null)
                {
                    //character.Inventory.CurrentLayout = (CharacterHealth.OpenHealthWindow == null) ? Alignment.Center : Alignment.Left;
                }
            }

            if (drawPortraitToolTip)
            {
                GUIComponent.DrawToolTip(
                    spriteBatch,
                    character.Info?.Job == null ? character.DisplayName : character.Name + " (" + character.Info.Job.Name + ")",
                    HUDLayoutSettings.PortraitArea);
            }
        }

        private static void DrawOrderIndicator(SpriteBatch spriteBatch, Camera cam, Character character, Order order, float iconAlpha = 1.0f)
        {
            if (order.TargetAllCharacters)
            {
                if (order.OrderGiver != character && !order.HasAppropriateJob(character))
                {
                    return;
                }
            }

            Entity target = order.ConnectedController != null ? order.ConnectedController.Item : order.TargetEntity;
            if (target == null) { return; }

            //don't show the indicator if far away and not inside the same sub
            //prevents exploiting the indicators in locating the sub
            if (character.Submarine != target.Submarine && 
                Vector2.DistanceSquared(character.WorldPosition, target.WorldPosition) > 1000.0f * 1000.0f)
            {
                return;
            }

            if (!orderIndicatorCount.ContainsKey(target)) { orderIndicatorCount.Add(target, 0); }

            Vector2 drawPos = target.WorldPosition + Vector2.UnitX * order.SymbolSprite.size.X * 1.5f * orderIndicatorCount[target];
            GUI.DrawIndicator(spriteBatch, drawPos, cam, 100.0f, order.SymbolSprite, order.Color * iconAlpha);

            orderIndicatorCount[target] = orderIndicatorCount[target] + 1;
        }        

        private static void DrawObjectiveIndicator(SpriteBatch spriteBatch, Camera cam, Character character, Character.ObjectiveEntity objectiveEntity, float iconAlpha = 1.0f)
        {
            if (objectiveEntity == null) return;

            Vector2 drawPos = objectiveEntity.Entity.WorldPosition;// + Vector2.UnitX * objectiveEntity.Sprite.size.X * 1.5f;
            GUI.DrawIndicator(spriteBatch, drawPos, cam, 100.0f, objectiveEntity.Sprite, objectiveEntity.Color * iconAlpha);
        }
    }
}
