using System;
using System.Collections.Generic;

namespace Data
{
    [Serializable]
    public class Entity
    {
        public string id;
        public EntityType type;
        public string owner;
        public float value;
        public float condition;
        public string location;
        public EntityStatus status;
        public Dictionary<string, object> components;
        public DateTime createdAt;
        public DateTime lastModified;
    }

    public enum EntityType
    {
        Job,
        NPC,
        Item,
        Property,
        Vehicle,
        Business
    }

    public enum EntityStatus
    {
        Active,
        Inactive,
        Locked,
        Broken,
        Stolen
    }

    [Serializable]
    public struct EntityData
    {
        public string owner;
        public float value;
        public float condition;
        public string location;
        public EntityStatus status;
        public Dictionary<string, object> customProperties;
    }
}

