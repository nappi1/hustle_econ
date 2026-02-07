# V2 Update (Aligned to Current Codebase)

This V2 spec reflects the current implementation. See docs/IMPLEMENTATION_DISCREPANCIES.md for cross-system gaps.

## Implementation Notes

- No entity name/description fields; entities are identified by id only.
- Entity ownership and type indices are maintained; creation uses Data.EntityData.

---
1. ENTITY SYSTEM
Purpose
The Entity System is the foundational data layer for all interactable objects in the game. Everything that exists in the game world (jobs, NPCs, items, properties, vehicles, businesses) is an Entity. This system provides creation, retrieval, modification, ownership transfer, and lifecycle management for all entities.

Design principle: If it can be owned, bought, sold, or interacted with - it's an Entity.

Interface
CreateEntity
csharp
Entity CreateEntity(EntityType type, EntityData data)
Purpose: Instantiates a new entity and registers it in the system.

Parameters:

type: EntityType enum (Job, NPC, Item, Property, Vehicle, Business)
data: EntityData object containing initial properties
Returns: Entity instance with assigned unique ID

Side effects:

Generates unique entity ID
Registers entity in internal dictionary
Broadcasts OnEntityCreated event
If entity has owner, updates owner's inventory reference
Example:

csharp
var car = CreateEntity(EntityType.Vehicle, new EntityData {
    value = 20000f,
    owner = playerId,
    condition = 100f,
    location = "street_main"
});
GetEntity
csharp
Entity GetEntity(string entityId)
Purpose: Retrieves entity by ID.

Parameters:

entityId: Unique entity identifier
Returns: Entity instance, or null if not found

Side effects: None (read-only)

GetEntitiesByType
csharp
List<Entity> GetEntitiesByType(EntityType type)
Purpose: Returns all entities of a specific type.

Parameters:

type: EntityType enum
Returns: List of entities (empty if none)

GetEntitiesByOwner
csharp
List<Entity> GetEntitiesByOwner(string ownerId)
Purpose: Returns all entities owned by a player/NPC.

Parameters:

ownerId: Owner's unique ID
Returns: List of owned entities (empty if none)

UpdateEntity
csharp
bool UpdateEntity(string entityId, Dictionary<string, object> updates)
Purpose: Modifies entity properties.

Parameters:

entityId: Entity to update
updates: Dictionary of property names ??? new values
Returns: true if successful, false if entity doesn't exist

Side effects:

Updates entity properties
Broadcasts OnEntityUpdated event
TransferOwnership
csharp
bool TransferOwnership(string entityId, string newOwnerId)
Purpose: Changes entity ownership (buying, selling, gifting).

Parameters:

entityId: Entity being transferred
newOwnerId: New owner's ID (or null for unowned)
Returns: true if successful, false if entity doesn't exist

Side effects:

Updates entity.owner field
Broadcasts OnOwnershipTransferred event
Does NOT handle money transaction (EconomySystem does that)
DestroyEntity
csharp
bool DestroyEntity(string entityId)
Purpose: Permanently removes entity from game.

Parameters:

entityId: Entity to destroy
Returns: true if successful, false if doesn't exist

Side effects:

Unregisters from system
Broadcasts OnEntityDestroyed event
Cannot be undone
GetEntityValue
csharp
float GetEntityValue(string entityId)
Purpose: Calculates current market value (considers condition, demand).

Parameters:

entityId: Entity to evaluate
Returns: Current value in dollars

Formula: baseValue * (condition / 100f)

Events
csharp
event Action<Entity> OnEntityCreated
event Action<string, Dictionary<string, object>> OnEntityUpdated  
event Action<string, string, string> OnOwnershipTransferred // (entityId, oldOwner, newOwner)
event Action<string> OnEntityDestroyed
Data Structures
Entity
csharp
class Entity {
    string id;                           // Unique identifier (auto-generated)
    EntityType type;                     // Job, NPC, Item, Property, Vehicle, Business
    string owner;                        // Player/NPC ID (null if unowned)
    float value;                         // Base monetary value
    float condition;                     // 0-100 (wear/damage)
    string location;                     // Current location ID
    EntityStatus status;                 // Active, Inactive, Locked, Broken, Stolen
    Dictionary<string, object> components; // Type-specific data
    DateTime createdAt;                  // When entity was created
    DateTime lastModified;               // Last update timestamp
}
EntityType (enum)
csharp
enum EntityType {
    Job,
    NPC,
    Item,
    Property,
    Vehicle,
    Business
}
EntityStatus (enum)
csharp
enum EntityStatus {
    Active,      // Normal state
    Inactive,    // Not in use
    Locked,      // Cannot interact (impounded, seized)
    Broken,      // Needs repair
    Stolen       // Illegal possession
}
EntityData (initialization)
csharp
struct EntityData {
    string owner;                        // Optional
    float value;
    float condition;                     // Default 100
    string location;
    EntityStatus status;                 // Default Active
    Dictionary<string, object> customProperties; // Optional type-specific data
}
Dependencies
Reads from: None (foundational system)

Writes to: None directly (broadcasts events)

Called by:

EconomySystem (buying/selling)
JobSystem (creating job entities)
RelationshipSystem (creating NPC entities)
SaveSystem (loading entities)
Implementation Notes
Internal Storage:

csharp
private Dictionary<string, Entity> entities;
private Dictionary<EntityType, HashSet<string>> entitiesByType;
private Dictionary<string, HashSet<string>> entitiesByOwner;
ID Generation:

csharp
private string GenerateEntityId(EntityType type) {
    return $"{type}_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
    // Example: "vehicle_a3f2c901"
}
Singleton Pattern:

csharp
public class EntitySystem : MonoBehaviour {
    private static EntitySystem instance;
    public static EntitySystem Instance {
        get {
            if (instance == null) {
                instance = FindObjectOfType<EntitySystem>();
                if (instance == null) {
                    GameObject go = new GameObject("EntitySystem");
                    instance = go.AddComponent<EntitySystem>();
                }
            }
            return instance;
        }
    }
    
    private void Awake() {
        if (instance != null && instance != this) {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
```

---

## **Edge Cases**

1. **Transfer ownership to self:** Succeeds but changes nothing
2. **Update non-existent entity:** Returns false, logs warning
3. **Destroy entity in active use:** Should check with ActivitySystem first
4. **Duplicate ID collision:** Regenerate until unique
5. **Null owner:** Valid (unowned items in world)

---

## **Testing Checklist**
```
[ ] Can create entity of each type
[ ] Generated IDs are unique across multiple creations
[ ] GetEntity returns correct entity for valid ID
[ ] GetEntity returns null for invalid ID
[ ] GetEntitiesByType filters correctly
[ ] GetEntitiesByOwner returns only owned entities
[ ] UpdateEntity modifies properties correctly
[ ] UpdateEntity returns false for non-existent entity
[ ] TransferOwnership updates owner field
[ ] TransferOwnership broadcasts event with correct parameters
[ ] DestroyEntity removes from all internal indexes
[ ] GetEntityValue calculates correctly (value * condition/100)
[ ] All events fire when expected
[ ] System persists across scene changes (DontDestroyOnLoad)
