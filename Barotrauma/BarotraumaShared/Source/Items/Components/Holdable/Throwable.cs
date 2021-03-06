﻿using Barotrauma.Networking;
using Microsoft.Xna.Framework;
using System.Xml.Linq;
using System.Linq;

namespace Barotrauma.Items.Components
{
    class Throwable : Holdable
    {
        float throwForce;

        float throwPos;

        bool throwing;
        bool throwDone;

        [Serialize(1.0f, false)]
        public float ThrowForce
        {
            get { return throwForce; }
            set { throwForce = value; }
        }

        public Throwable(Item item, XElement element)
            : base(item, element)
        {
            //throwForce = ToolBox.GetAttributeFloat(element, "throwforce", 1.0f);
        }

        public override bool Use(float deltaTime, Character character = null)
        {
            return characterUsable || character == null; //We do the actual throwing in Aim because Use might be used by chems 
        }

        public override bool SecondaryUse(float deltaTime, Character character = null)
        {
            if (!throwDone) return false; //This should only be triggered in update
            throwDone = false;
            return true;
        }

        public override void Drop(Character dropper)
        {
            base.Drop(dropper);

            throwing = false;
            throwPos = 0.0f;
        }

        public override void UpdateBroken(float deltaTime, Camera cam)
        {
            Update(deltaTime, cam);
        }
        
        public override void Update(float deltaTime, Camera cam)
        {
            if (!item.body.Enabled) return;
            if (picker == null || picker.Removed || !picker.HasSelectedItem(item))
            {
                IsActive = false;
                return;
            }

            if (picker.IsKeyDown(InputType.Aim) && picker.IsKeyHit(InputType.Use))
                throwing = true;

            if (!picker.IsKeyDown(InputType.Aim) && !throwing) throwPos = 0.0f;

            ApplyStatusEffects(ActionType.OnActive, deltaTime, picker);

            if (item.body.Dir != picker.AnimController.Dir) Flip(item);

            AnimController ac = picker.AnimController;

            item.Submarine = picker.Submarine;

            if (!throwing)
            {
                if (picker.IsKeyDown(InputType.Aim))
                {
                    throwPos = (float)System.Math.Min(throwPos + deltaTime * 5.0f, MathHelper.Pi * 0.7f);

                    ac.HoldItem(deltaTime, item, handlePos, new Vector2(0.6f, -0.0f), new Vector2(-0.3f, 0.2f), false, throwPos);
                }
                else
                {
                    ac.HoldItem(deltaTime, item, handlePos, holdPos, aimPos, false, holdAngle);
                }
            }
            else
            {
                throwPos -= deltaTime * 15.0f;

                ac.HoldItem(deltaTime, item, handlePos, new Vector2(0.6f, 0.0f), new Vector2(-0.3f, 0.2f), false, throwPos);

                if (throwPos < -0.0)
                {
                    Vector2 throwVector = Vector2.Normalize(picker.CursorWorldPosition - picker.WorldPosition);
                    //throw upwards if cursor is at the position of the character 
                    if (!MathUtils.IsValid(throwVector)) throwVector = Vector2.UnitY;

                    GameServer.Log(picker.LogName + " threw " + item.Name, ServerLog.MessageType.ItemInteraction);

                    if (GameMain.NilMod.EnableGriefWatcher)
                    {
                        //Grief watch throw checks
                        for (int y = 0; y < NilMod.NilModGriefWatcher.GWListThrown.Count; y++)
                        {
                            if (NilMod.NilModGriefWatcher.GWListThrown[y] == Item.Name)
                            {
                                Barotrauma.Networking.Client warnedclient = GameMain.Server.ConnectedClients.Find(c => c.Character == picker);

                                if (item.ContainedItems == null || item.ContainedItems.All(it => it == null))
                                {
                                    NilMod.NilModGriefWatcher.SendWarning(picker.LogName
                                        + " threw dangerous item " + Item.Name, warnedclient);
                                }
                                else
                                {
                                    NilMod.NilModGriefWatcher.SendWarning(picker.LogName
                                        + " threw dangerous item "
                                        + Item.Name
                                        + " (" + string.Join(", ", System.Array.FindAll(item.ContainedItems, it => it != null).Select(it => it.Name))
                                        + ")", warnedclient);
                                }
                            }
                        }
                    }

                    item.Drop();
                    item.body.ApplyLinearImpulse(throwVector * throwForce * item.body.Mass * 3.0f);

                    ac.GetLimb(LimbType.Head).body.ApplyLinearImpulse(throwVector*10.0f);
                    ac.GetLimb(LimbType.Torso).body.ApplyLinearImpulse(throwVector * 10.0f);

                    Limb rightHand = ac.GetLimb(LimbType.RightHand);
                    item.body.AngularVelocity = rightHand.body.AngularVelocity;
                    throwDone = true;
                    ApplyStatusEffects(ActionType.OnSecondaryUse, deltaTime, picker); //Stun grenades, flares, etc. all have their throw-related things handled in "onSecondaryUse"
                    throwing = false;
                }
            }
        }    
    }
}
