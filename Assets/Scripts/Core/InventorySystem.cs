using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using HustleEconomy.Data;

namespace Core
{
    public class InventorySystem : MonoBehaviour
    {
        public enum EquipSlot
        {
            Clothing,
            Vehicle,
            Phone
        }

        [System.Serializable]
        public class InventoryState
        {
            public string playerId;
            public List<string> ownedItems;
            public Dictionary<EquipSlot, string> equippedItems;
            public Dictionary<string, string> itemLocations;
        }

        private static InventorySystem instance;
        public static InventorySystem Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<InventorySystem>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("InventorySystem");
                        instance = go.AddComponent<InventorySystem>();
                    }
                }
                return instance;
            }
        }

        public event Action<string, string> OnItemAdded;
        public event Action<string, string> OnItemRemoved;
        public event Action<string, string, EquipSlot> OnItemEquipped;
        public event Action<string, EquipSlot> OnItemUnequipped;

        private Dictionary<string, InventoryState> inventories;

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
            inventories = new Dictionary<string, InventoryState>();
        }

        public List<Entity> GetInventory(string playerId)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                Debug.LogWarning("GetInventory: playerId is null or empty");
                return new List<Entity>();
            }

            return EntitySystem.Instance.GetEntitiesByOwner(playerId);
        }

        public List<Entity> GetItemsByCategory(string playerId, EntityType category)
        {
            List<Entity> owned = GetInventory(playerId);
            return owned.Where(entity => entity.type == category).ToList();
        }

        public void AddItem(string playerId, string entityId)
        {
            if (string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(entityId))
            {
                Debug.LogWarning("AddItem: playerId or entityId is null or empty");
                return;
            }

            EntitySystem.Instance.TransferOwnership(entityId, playerId);

            InventoryState state = GetInventoryState(playerId);
            if (!state.ownedItems.Contains(entityId))
            {
                state.ownedItems.Add(entityId);
            }

            OnItemAdded?.Invoke(playerId, entityId);
        }

        public void RemoveItem(string playerId, string entityId)
        {
            if (string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(entityId))
            {
                Debug.LogWarning("RemoveItem: playerId or entityId is null or empty");
                return;
            }

            EntitySystem.Instance.TransferOwnership(entityId, null);

            InventoryState state = GetInventoryState(playerId);
            state.ownedItems.Remove(entityId);

            foreach (EquipSlot slot in state.equippedItems.Keys.ToList())
            {
                if (state.equippedItems[slot] == entityId)
                {
                    UnequipItem(playerId, slot);
                }
            }

            OnItemRemoved?.Invoke(playerId, entityId);
        }

        public bool HasItem(string playerId, string entityId)
        {
            if (string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(entityId))
            {
                return false;
            }

            InventoryState state = GetInventoryState(playerId);
            if (state.ownedItems.Contains(entityId))
            {
                return true;
            }

            Entity entity = EntitySystem.Instance.GetEntity(entityId);
            return entity != null && entity.owner == playerId;
        }

        public int GetItemCount(string playerId, EntityType type)
        {
            return GetItemsByCategory(playerId, type).Count;
        }

        public Entity GetEquippedItem(string playerId, EquipSlot slot)
        {
            InventoryState state = GetInventoryState(playerId);
            if (!state.equippedItems.TryGetValue(slot, out string entityId))
            {
                return null;
            }

            return EntitySystem.Instance.GetEntity(entityId);
        }

        public void EquipItem(string playerId, string entityId, EquipSlot slot)
        {
            if (!HasItem(playerId, entityId))
            {
                return;
            }

            if (!HasAccessToItem(playerId, entityId))
            {
                return;
            }

            InventoryState state = GetInventoryState(playerId);

            if (state.equippedItems.ContainsKey(slot))
            {
                UnequipItem(playerId, slot);
            }

            state.equippedItems[slot] = entityId;
            ApplyEquipEffects(playerId, entityId, slot);
            OnItemEquipped?.Invoke(playerId, entityId, slot);
        }

        public void UnequipItem(string playerId, EquipSlot slot)
        {
            InventoryState state = GetInventoryState(playerId);
            if (!state.equippedItems.ContainsKey(slot))
            {
                return;
            }

            string entityId = state.equippedItems[slot];
            RemoveEquipEffects(playerId, entityId, slot);
            state.equippedItems.Remove(slot);
            OnItemUnequipped?.Invoke(playerId, slot);
        }

        public void SetItemLocation(string playerId, string entityId, string locationId)
        {
            InventoryState state = GetInventoryState(playerId);
            state.itemLocations[entityId] = locationId;
        }

        public string GetItemLocation(string playerId, string entityId)
        {
            InventoryState state = GetInventoryState(playerId);
            return state.itemLocations.ContainsKey(entityId) ? state.itemLocations[entityId] : null;
        }

        public bool HasAccessToItem(string playerId, string entityId)
        {
            string itemLocation = GetItemLocation(playerId, entityId);
            if (string.IsNullOrEmpty(itemLocation))
            {
                return true;
            }

            string playerLocation = LocationSystem.Instance.GetPlayerLocation(playerId);
            return itemLocation == playerLocation;
        }

        public InventoryState GetInventoryStateForTesting(string playerId)
        {
            return GetInventoryState(playerId);
        }

        private InventoryState GetInventoryState(string playerId)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                playerId = "player";
            }

            if (!inventories.TryGetValue(playerId, out InventoryState state))
            {
                state = new InventoryState
                {
                    playerId = playerId,
                    ownedItems = new List<string>(),
                    equippedItems = new Dictionary<EquipSlot, string>(),
                    itemLocations = new Dictionary<string, string>()
                };
                inventories[playerId] = state;
            }

            return state;
        }

        private void ApplyEquipEffects(string playerId, string entityId, EquipSlot slot)
        {
            switch (slot)
            {
                case EquipSlot.Clothing:
                    Debug.LogWarning("TODO: ClothingSystem integration for equipped clothing");
                    Debug.LogWarning("TODO: BodySystem UpdateAppearance for equipped clothing");
                    break;
                case EquipSlot.Vehicle:
                    Debug.LogWarning("TODO: VehicleSystem SetActiveVehicle");
                    break;
                case EquipSlot.Phone:
                    Debug.LogWarning("TODO: PhoneSystem SetActivePhone");
                    break;
            }
        }

        private void RemoveEquipEffects(string playerId, string entityId, EquipSlot slot)
        {
            switch (slot)
            {
                case EquipSlot.Clothing:
                    Debug.LogWarning("TODO: ClothingSystem remove clothing effects");
                    break;
                case EquipSlot.Vehicle:
                    Debug.LogWarning("TODO: VehicleSystem clear active vehicle");
                    break;
                case EquipSlot.Phone:
                    Debug.LogWarning("TODO: PhoneSystem clear active phone");
                    break;
            }
        }
    }
}
