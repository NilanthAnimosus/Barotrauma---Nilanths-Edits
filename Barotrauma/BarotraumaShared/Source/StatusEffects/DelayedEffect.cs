using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Barotrauma
{
    class DelayedListElement
    {
        public DelayedEffect Parent;
        public Entity Entity;
        public List<ISerializableEntity> Targets;
        public float StartTimer;
        public List<int> CancelledEffects = new List<int>();
        public Character causecharacter;
        public string identifier;
    }
    class DelayedEffect : StatusEffect
    {
        public static List<DelayedListElement> DelayList = new List<DelayedListElement>();

        private float delay;

        public DelayedEffect(XElement element)
            : base(element)
        {
            delay = element.GetAttributeFloat("delay", 1.0f);
        }

        public override void Apply(ActionType type, float deltaTime, Entity entity, ISerializableEntity target, Character causecharacter = null, string identifier = "")
        {
            if (this.type != type || !HasRequiredItems(entity)) return;
            if (!Stackable && DelayList.Any(d => d.Parent == this && d.Targets.Count == 1 && d.Targets[0] == target)) return;

            if (targetNames != null && !targetNames.Contains(target.Name)) return;

            if (identifier == "") identifier = "statuseffect";

            DelayedListElement element = new DelayedListElement
            {
                Parent = this,
                StartTimer = delay,
                Entity = entity,
                Targets = new List<ISerializableEntity>() { target },
                causecharacter = causecharacter,
                identifier = identifier
            };

            DelayList.Add(element);
        }

        public override void Apply(ActionType type, float deltaTime, Entity entity, List<ISerializableEntity> targets, Character causecharacter = null, string identifier = "")
        {
            if (this.type != type || !HasRequiredItems(entity)) return;
            if (!Stackable && DelayList.Any(d => d.Parent == this && d.Targets.SequenceEqual(targets))) return;

            //remove invalid targets
            if (targetNames != null)
            {
                targets.RemoveAll(t => !targetNames.Contains(t.Name));
                if (targets.Count == 0) return;
            }

            if (identifier == "") identifier = "statuseffect";

            DelayedListElement element = new DelayedListElement
            {
                Parent = this,
                StartTimer = delay,
                Entity = entity,
                Targets = targets,
                causecharacter = causecharacter,
                identifier = identifier
            };

            DelayList.Add(element);
        }

        public static void Update(float deltaTime)
        {
            for (int i = DelayList.Count - 1; i >= 0; i--)
            {
                DelayedListElement element = DelayList[i];
                if (element.Parent.CheckConditionalAlways && !element.Parent.HasRequiredConditions(element.Targets))
                {
                    DelayList.Remove(element);
                    continue;
                }

                element.Targets.RemoveAll(t => t is Entity entity && entity.Removed);
                if (element.Targets.Count == 0)
                {
                    DelayList.RemoveAt(i);
                    continue;
                }

                element.StartTimer -= deltaTime;

                if (element.StartTimer > 0.0f) continue;

                element.Parent.Apply(1.0f, element.Entity, element.Targets, element.CancelledEffects, element.causecharacter, element.identifier);
                DelayList.RemoveAt(i);
            }
        }
    }
}