# V2 Update (Aligned to Current Codebase)

This V2 spec reflects the current implementation. See docs/IMPLEMENTATION_DISCREPANCIES.md for cross-system gaps.

## Implementation Notes

- Equip effects for Clothing/Vehicle/Phone are TODOs; no spending logic implemented.
- Uses LocationSystem.GetPlayerLocation for item access; no ownership/vehicle checks beyond that.

---
15. INVENTORY SYSTEM
Purpose
Manages player-owned items (clothing, drugs, weapons, vehicles, properties). Tracks what player owns, where items are stored, item conditions, and provides access for other systems. Integrates with Entity System (items are entities), Economy System (buying/selling), and Clothing/Equipment systems.

Interface
GetInventory
csharp
List<Entity> GetInventory(string playerId)
Purpose: Get all items player owns.

Parameters:

playerId: Player ID
Returns: List of owned entities

GetItemsByCategory
csharp
List<Entity> GetItemsByCategory(string playerId, EntityType category)
Purpose: Get items of specific type.

Parameters:

playerId: Player ID
category: EntityType (Items, Vehicles, Properties, etc.)
Returns: Filtered list of entities

AddItem
csharp
void AddItem(string playerId, string entityId)
Purpose: Add item to player inventory.

Parameters:

playerId: Player ID
entityId: Entity to add
Side effects:

Updates entity ownership
Adds to inventory list
Broadcasts OnItemAdded event
RemoveItem
csharp
void RemoveItem(string playerId, string entityId)
Purpose: Remove item from inventory.

Parameters:

playerId: Player ID
entityId: Entity to remove
Side effects:

Clears entity ownership
Removes from inventory
Broadcasts OnItemRemoved event
HasItem
csharp
bool HasItem(string playerId, string entityId)
Purpose: Check if player owns specific item.

Parameters:

playerId: Player ID
entityId: Entity to check
Returns: true if owned

GetItemCount
csharp
int GetItemCount(string playerId, EntityType type)
Purpose: Count items of specific type.

Parameters:

playerId: Player ID
type: Item type
Returns: Count

Example: How many vehicles does player own?

GetEquippedItem
csharp
Entity GetEquippedItem(string playerId, EquipSlot slot)
Purpose: What item is currently equipped/worn.

Parameters:

playerId: Player ID
slot: EquipSlot (Clothing, Vehicle, etc.)
Returns: Equipped entity or null

EquipItem
csharp
void EquipItem(string playerId, string entityId, EquipSlot slot)
Purpose: Equip/wear/use an item.

Parameters:

playerId: Player ID
entityId: Item to equip
slot: Where to equip it
Side effects:

Unequips current item in slot
Equips new item
Applies item effects (clothing modifiers, etc.)
Broadcasts OnItemEquipped event
Events
csharp
event Action<string, string> OnItemAdded      // (playerId, entityId)
event Action<string, string> OnItemRemoved    // (playerId, entityId)
event Action<string, string, EquipSlot> OnItemEquipped
event Action<string, EquipSlot> OnItemUnequipped
Data Structures
EquipSlot (enum)
csharp
enum EquipSlot {
    Clothing,
    Vehicle,      // Current vehicle being driven
    Phone         // Current phone device
}
InventoryState
csharp
class InventoryState {
    string playerId;
    List<string> ownedItems;                       // Entity IDs
    Dictionary<EquipSlot, string> equippedItems;   // What's currently equipped
    Dictionary<string, string> itemLocations;       // Where items are stored
}
Dependencies
Reads from:

EntitySystem (all items are entities)
Writes to:

EntitySystem (updates ownership via TransferOwnership)
Subscribed to by:

UI (inventory display)
ClothingSystem (equipped clothing)
VehicleSystem (equipped vehicle)
AdultContentSystem (clothing affects income)
Implementation Notes
Inventory Management:

csharp
List<Entity> GetInventory(string playerId) {
    // Query EntitySystem for all entities owned by player
    return EntitySystem.Instance.GetEntitiesByOwner(playerId);
}

void AddItem(string playerId, string entityId) {
    // Transfer ownership to player
    EntitySystem.Instance.TransferOwnership(entityId, playerId);
    
    // Track in inventory state
    InventoryState state = GetInventoryState(playerId);
    if (!state.ownedItems.Contains(entityId)) {
        state.ownedItems.Add(entityId);
    }
    
    OnItemAdded?.Invoke(playerId, entityId);
}

void RemoveItem(string playerId, string entityId) {
    // Clear ownership
    EntitySystem.Instance.TransferOwnership(entityId, null);
    
    // Remove from inventory
    InventoryState state = GetInventoryState(playerId);
    state.ownedItems.Remove(entityId);
    
    // Unequip if equipped
    foreach (var slot in state.equippedItems.Keys.ToList()) {
        if (state.equippedItems[slot] == entityId) {
            UnequipItem(playerId, slot);
        }
    }
    
    OnItemRemoved?.Invoke(playerId, entityId);
}
Equipment System:

csharp
void EquipItem(string playerId, string entityId, EquipSlot slot) {
    // Verify player owns item
    if (!HasItem(playerId, entityId)) return;
    
    InventoryState state = GetInventoryState(playerId);
    
    // Unequip current item in slot
    if (state.equippedItems.ContainsKey(slot)) {
        UnequipItem(playerId, slot);
    }
    
    // Equip new item
    state.equippedItems[slot] = entityId;
    
    // Apply item effects based on slot
    ApplyEquipEffects(playerId, entityId, slot);
    
    OnItemEquipped?.Invoke(playerId, entityId, slot);
}

void ApplyEquipEffects(string playerId, string entityId, EquipSlot slot) {
    Entity item = EntitySystem.Instance.GetEntity(entityId);
    
    switch (slot) {
        case EquipSlot.Clothing:
            // Apply clothing modifiers
            ClothingItem clothing = item as ClothingItem;
            AdultContentSystem.Instance.EquipClothing(entityId);
            BodySystem.Instance.UpdateAppearance(clothing);
            break;
        
        case EquipSlot.Vehicle:
            // Set active vehicle
            VehicleSystem.Instance.SetActiveVehicle(entityId);
            break;
        
        case EquipSlot.Phone:
            // Set active phone (affects UI, features)
            PhoneSystem.Instance.SetActivePhone(entityId);
            break;
    }
}

void UnequipItem(string playerId, EquipSlot slot) {
    InventoryState state = GetInventoryState(playerId);
    
    if (!state.equippedItems.ContainsKey(slot)) return;
    
    string entityId = state.equippedItems[slot];
    
    // Remove effects
    RemoveEquipEffects(playerId, entityId, slot);
    
    // Clear slot
    state.equippedItems.Remove(slot);
    
    OnItemUnequipped?.Invoke(playerId, slot);
}
Item Storage Locations:

csharp
public void SetItemLocation(string entityId, string locationId) {
    InventoryState state = GetInventoryState(playerId);
    state.itemLocations[entityId] = locationId;
}

public string GetItemLocation(string entityId) {
    InventoryState state = GetInventoryState(playerId);
    return state.itemLocations.ContainsKey(entityId) 
        ? state.itemLocations[entityId] 
        : null;
}

// Example: Drugs stored at apartment, not carried
public bool HasAccessToItem(string playerId, string entityId) {
    string itemLocation = GetItemLocation(entityId);
    string playerLocation = LocationSystem.Instance.GetPlayerLocation(playerId);
    
    return itemLocation == null || itemLocation == playerLocation;
}
```

---

## **Edge Cases**

1. **Equip item not owned:** Silently fail or warn
2. **Remove equipped item:** Auto-unequip first
3. **Item at different location:** Can't equip if not accessible
4. **Sell equipped item:** Auto-unequip, then sell
5. **Multiple clothing items:** Only one equipped at a time

---

## **Testing Checklist**
```
[ ] GetInventory returns all owned items
[ ] GetItemsByCategory filters correctly
[ ] AddItem adds to inventory and sets ownership
[ ] RemoveItem removes from inventory and clears ownership
[ ] HasItem returns true for owned items
[ ] GetItemCount counts items correctly
[ ] EquipItem equips to correct slot
[ ] EquipItem unequips previous item in slot
[ ] Equipping clothing applies visual changes
[ ] Equipping vehicle sets active vehicle
[ ] UnequipItem clears slot
[ ] Can't equip item not owned
[ ] Removing equipped item auto-unequips
