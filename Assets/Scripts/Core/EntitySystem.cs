using System;
using System.Collections.Generic;
using UnityEngine;
using Data;

namespace Core
{
    public class EntitySystem : MonoBehaviour
    {
        private static EntitySystem instance;
        public static EntitySystem Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<EntitySystem>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("EntitySystem");
                        instance = go.AddComponent<EntitySystem>();
                    }
                }
                return instance;
            }
        }

        public event Action<Entity> OnEntityCreated;
        public event Action<string, Dictionary<string, object>> OnEntityUpdated;
        public event Action<string, string, string> OnOwnershipTransferred;
        public event Action<string> OnEntityDestroyed;

        private Dictionary<string, Entity> entities;
        private Dictionary<EntityType, HashSet<string>> entitiesByType;
        private Dictionary<string, HashSet<string>> entitiesByOwner;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }

        private void Initialize()
        {
            entities = new Dictionary<string, Entity>();
            entitiesByType = new Dictionary<EntityType, HashSet<string>>();
            entitiesByOwner = new Dictionary<string, HashSet<string>>();
        }

        public Entity CreateEntity(EntityType type, EntityData data)
        {
            string id = GenerateUniqueEntityId(type);
            string ownerId = NormalizeOwner(data.owner);
            float condition = data.condition == 0f ? 100f : data.condition;

            Entity entity = new Entity
            {
                id = id,
                type = type,
                owner = ownerId,
                value = data.value,
                condition = Mathf.Clamp(condition, 0f, 100f),
                location = data.location,
                status = data.status,
                components = data.customProperties != null
                    ? new Dictionary<string, object>(data.customProperties)
                    : new Dictionary<string, object>(),
                createdAt = DateTime.UtcNow,
                lastModified = DateTime.UtcNow
            };

            entities[id] = entity;
            AddToTypeIndex(type, id);
            if (!string.IsNullOrEmpty(ownerId))
            {
                AddToOwnerIndex(ownerId, id);
            }

            OnEntityCreated?.Invoke(entity);
            return entity;
        }

        public Entity GetEntity(string entityId)
        {
            if (string.IsNullOrEmpty(entityId))
            {
                Debug.LogWarning("GetEntity: entityId is null or empty");
                return null;
            }

            if (!entities.TryGetValue(entityId, out Entity entity))
            {
                return null;
            }

            return entity;
        }

        public List<Entity> GetEntitiesByType(EntityType type)
        {
            List<Entity> results = new List<Entity>();
            if (!entitiesByType.TryGetValue(type, out HashSet<string> ids))
            {
                return results;
            }

            foreach (string id in ids)
            {
                if (entities.TryGetValue(id, out Entity entity))
                {
                    results.Add(entity);
                }
            }

            return results;
        }

        public List<Entity> GetEntitiesByOwner(string ownerId)
        {
            List<Entity> results = new List<Entity>();
            ownerId = NormalizeOwner(ownerId);
            if (string.IsNullOrEmpty(ownerId))
            {
                return results;
            }

            if (!entitiesByOwner.TryGetValue(ownerId, out HashSet<string> ids))
            {
                return results;
            }

            foreach (string id in ids)
            {
                if (entities.TryGetValue(id, out Entity entity))
                {
                    results.Add(entity);
                }
            }

            return results;
        }

        public bool UpdateEntity(string entityId, Dictionary<string, object> updates)
        {
            if (string.IsNullOrEmpty(entityId))
            {
                Debug.LogWarning("UpdateEntity: entityId is null or empty");
                return false;
            }

            if (updates == null || updates.Count == 0)
            {
                return true;
            }

            if (!entities.TryGetValue(entityId, out Entity entity))
            {
                Debug.LogWarning($"UpdateEntity: Entity {entityId} not found");
                return false;
            }

            foreach (KeyValuePair<string, object> update in updates)
            {
                if (string.IsNullOrEmpty(update.Key))
                {
                    continue;
                }

                string key = update.Key.Trim().ToLowerInvariant();
                switch (key)
                {
                    case "owner":
                        UpdateOwnerIndex(entity, NormalizeOwner(update.Value as string));
                        break;
                    case "value":
                        if (TryConvertFloat(update.Value, out float value))
                        {
                            entity.value = value;
                        }
                        break;
                    case "condition":
                        if (TryConvertFloat(update.Value, out float condition))
                        {
                            entity.condition = Mathf.Clamp(condition, 0f, 100f);
                        }
                        break;
                    case "location":
                        entity.location = update.Value as string;
                        break;
                    case "status":
                        if (TryConvertStatus(update.Value, out EntityStatus status))
                        {
                            entity.status = status;
                        }
                        break;
                    case "components":
                    case "customproperties":
                        if (update.Value is Dictionary<string, object> dict)
                        {
                            entity.components = new Dictionary<string, object>(dict);
                        }
                        break;
                    default:
                        if (entity.components == null)
                        {
                            entity.components = new Dictionary<string, object>();
                        }
                        entity.components[update.Key] = update.Value;
                        break;
                }
            }

            entity.lastModified = DateTime.UtcNow;
            OnEntityUpdated?.Invoke(entityId, updates);
            return true;
        }

        public bool TransferOwnership(string entityId, string newOwnerId)
        {
            if (string.IsNullOrEmpty(entityId))
            {
                Debug.LogWarning("TransferOwnership: entityId is null or empty");
                return false;
            }

            if (!entities.TryGetValue(entityId, out Entity entity))
            {
                Debug.LogWarning($"TransferOwnership: Entity {entityId} not found");
                return false;
            }

            string oldOwnerId = NormalizeOwner(entity.owner);
            newOwnerId = NormalizeOwner(newOwnerId);

            if (oldOwnerId == newOwnerId)
            {
                return true;
            }

            if (!string.IsNullOrEmpty(oldOwnerId))
            {
                RemoveFromOwnerIndex(oldOwnerId, entityId);
            }

            if (!string.IsNullOrEmpty(newOwnerId))
            {
                AddToOwnerIndex(newOwnerId, entityId);
            }

            entity.owner = newOwnerId;
            entity.lastModified = DateTime.UtcNow;
            OnOwnershipTransferred?.Invoke(entityId, oldOwnerId, newOwnerId);
            return true;
        }

        public bool DestroyEntity(string entityId)
        {
            if (string.IsNullOrEmpty(entityId))
            {
                Debug.LogWarning("DestroyEntity: entityId is null or empty");
                return false;
            }

            if (!entities.TryGetValue(entityId, out Entity entity))
            {
                Debug.LogWarning($"DestroyEntity: Entity {entityId} not found");
                return false;
            }

            entities.Remove(entityId);
            RemoveFromTypeIndex(entity.type, entityId);
            if (!string.IsNullOrEmpty(entity.owner))
            {
                RemoveFromOwnerIndex(entity.owner, entityId);
            }

            OnEntityDestroyed?.Invoke(entityId);
            return true;
        }

        public float GetEntityValue(string entityId)
        {
            Entity entity = GetEntity(entityId);
            if (entity == null)
            {
                Debug.LogWarning($"GetEntityValue: Entity {entityId} not found");
                return 0f;
            }

            return entity.value * (entity.condition / 100f);
        }

        private string GenerateUniqueEntityId(EntityType type)
        {
            string id;
            do
            {
                id = GenerateEntityId(type);
            }
            while (entities.ContainsKey(id));

            return id;
        }

        private string GenerateEntityId(EntityType type)
        {
            return $"{type.ToString().ToLowerInvariant()}_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        }

        private void AddToTypeIndex(EntityType type, string entityId)
        {
            if (!entitiesByType.TryGetValue(type, out HashSet<string> ids))
            {
                ids = new HashSet<string>();
                entitiesByType[type] = ids;
            }
            ids.Add(entityId);
        }

        private void RemoveFromTypeIndex(EntityType type, string entityId)
        {
            if (!entitiesByType.TryGetValue(type, out HashSet<string> ids))
            {
                return;
            }
            ids.Remove(entityId);
            if (ids.Count == 0)
            {
                entitiesByType.Remove(type);
            }
        }

        private void AddToOwnerIndex(string ownerId, string entityId)
        {
            if (!entitiesByOwner.TryGetValue(ownerId, out HashSet<string> ids))
            {
                ids = new HashSet<string>();
                entitiesByOwner[ownerId] = ids;
            }
            ids.Add(entityId);
        }

        private void RemoveFromOwnerIndex(string ownerId, string entityId)
        {
            if (!entitiesByOwner.TryGetValue(ownerId, out HashSet<string> ids))
            {
                return;
            }
            ids.Remove(entityId);
            if (ids.Count == 0)
            {
                entitiesByOwner.Remove(ownerId);
            }
        }

        private void UpdateOwnerIndex(Entity entity, string newOwnerId)
        {
            string oldOwnerId = NormalizeOwner(entity.owner);
            newOwnerId = NormalizeOwner(newOwnerId);

            if (oldOwnerId == newOwnerId)
            {
                return;
            }

            if (!string.IsNullOrEmpty(oldOwnerId))
            {
                RemoveFromOwnerIndex(oldOwnerId, entity.id);
            }

            if (!string.IsNullOrEmpty(newOwnerId))
            {
                AddToOwnerIndex(newOwnerId, entity.id);
            }

            entity.owner = newOwnerId;
        }

        private static string NormalizeOwner(string ownerId)
        {
            return string.IsNullOrWhiteSpace(ownerId) ? null : ownerId;
        }

        private static bool TryConvertFloat(object value, out float result)
        {
            try
            {
                if (value == null)
                {
                    result = 0f;
                    return false;
                }

                if (value is float floatValue)
                {
                    result = floatValue;
                    return true;
                }

                if (value is double doubleValue)
                {
                    result = (float)doubleValue;
                    return true;
                }

                if (value is int intValue)
                {
                    result = intValue;
                    return true;
                }

                result = Convert.ToSingle(value);
                return true;
            }
            catch
            {
                result = 0f;
                return false;
            }
        }

        private static bool TryConvertStatus(object value, out EntityStatus status)
        {
            if (value is EntityStatus statusValue)
            {
                status = statusValue;
                return true;
            }

            if (value is string statusString &&
                Enum.TryParse(statusString, true, out EntityStatus parsedStatus))
            {
                status = parsedStatus;
                return true;
            }

            status = EntityStatus.Active;
            return false;
        }
    }
}

