﻿using Lidgren.Network;
using System;

namespace Barotrauma.Networking
{
    abstract class NetEntityEvent
    {
        public enum Type
        {
            Invalid,
            ComponentState, 
            InventoryState,
            Status,
            Repair,
            ApplyStatusEffect,
            ChangeProperty,
            Control
        }

        public readonly Entity Entity;
        public readonly UInt16 ID;

        //arbitrary extra data that will be passed to the Write method of the serializable entity
        //(the index of an itemcomponent for example)
        protected object[] Data;

        protected NetEntityEvent(INetSerializable entity, UInt16 id)
        {
            this.ID = id;
            this.Entity = entity as Entity;
        }

        public void SetData(object[] data)
        {
            this.Data = data;
        }

        public bool IsDuplicate(NetEntityEvent other)
        {
            if (other.Entity != this.Entity) return false;

            if (Data != null && other.Data != null)
            {
                if (Data.Length != other.Data.Length) return false;
                
                for (int i = 0; i < Data.Length; i++)
                {
                    if (Data[i] == null)
                    {
                        if (other.Data[i] != null) return false;
                    }
                    else
                    {
                        if (other.Data[i] == null) return false;
                        if (!Data[i].Equals(other.Data[i])) return false;
                    }
                }
                return true;
            }

            return Data == other.Data;
        }
    }

    class ServerEntityEvent : NetEntityEvent
    {
        private IServerSerializable serializable;

        public bool Sent;

#if DEBUG
        public string StackTrace;
#endif

        private double createTime;
        public double CreateTime
        {
            get { return createTime; }
        }

        public ServerEntityEvent(IServerSerializable entity, UInt16 id)
            : base(entity, id)
        { 
            serializable = entity;

            createTime = Timing.TotalTime;

#if DEBUG
            StackTrace = Environment.StackTrace.ToString();
#endif
        }

        public void Write(NetBuffer msg, Client recipient)
        {
            serializable.ServerWrite(msg, recipient, Data);
        } 
    }
}
