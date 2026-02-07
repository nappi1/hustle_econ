# V2 Update (Aligned to Current Codebase)

This V2 spec reflects the current implementation. See docs/IMPLEMENTATION_DISCREPANCIES.md for cross-system gaps.

## Implementation Notes

- ValidateStartJob checks HasJob + job active; no shift schedule validation.
- ValidateTravelTo uses LocationSystem.CanTravelTo.
- HUD prompt integration still missing.

---
# INTERACTION SYSTEM SPECIFICATION

**System:** InteractionSystem  
**Namespace:** HustleEconomy.Core  
**Dependencies:** PlayerController, InputManager, EntitySystem, ActivitySystem, InventorySystem  
**Purpose:** Raycasting, object interaction, context-sensitive actions in 3D world

---

## **I. OVERVIEW**

### **What This System Does**

InteractionSystem handles player interaction with the 3D world:
- Raycasting from player to detect interactable objects
- Context-sensitive prompts ("Press E to pick up", "Press E to start shift")
- Triggering interactions (start job, pick up item, open door, talk to NPC)
- Validation (can player afford? has required items? meets conditions?)
- Integration with systems (ActivitySystem, InventorySystem, EntitySystem)

### **Design Philosophy**

- **Physical interaction:** Player must be looking at object and within range
- **Context-aware:** Different prompts based on object type and player state
- **Validation first:** Check conditions before allowing interaction
- **System integration:** Route interactions to appropriate systems

---

## **II. INTEGRATION POINTS (CRITICAL)**

### **Resolves These TODOs:**

General: Provides the "bridge" between player input and systems. No specific discrepancy line, but fills gap between PlayerController and game systems.

### **Requires These System APIs:**

**PlayerController (already spec'd):**
- `PlayerController.GetLookTarget(maxDistance)` ??? RaycastHit
- `PlayerController.GetPosition()` ??? Player position
- `PlayerController.IsLookingAt(object, distance)` ??? Validation

**InputManager (already spec'd):**
- `InputManager.GetActionDown(InputAction.Interact)` ??? E key press

**EntitySystem (existing):**
- `EntitySystem.GetEntity(entityId)` ??? Get entity data
- `EntitySystem.SetOwner(entityId, ownerId)` ??? Pickup items

**ActivitySystem (existing):**
- `ActivitySystem.CreateActivity(playerId, type, context)` ??? Start job
- `ActivitySystem.StartActivity(activityId)` ??? Begin activity

**InventorySystem (existing):**
- `InventorySystem.PickupItem(playerId, itemId)` ??? Add to inventory
- `InventorySystem.DropItem(playerId, itemId)` ??? Remove from inventory
- `InventorySystem.HasItem(playerId, itemId)` ??? Check possession

**JobSystem (existing):**
- `JobSystem.StartShift(playerId, jobId)` ??? Begin work
- `JobSystem.EndShift(playerId, jobId)` ??? Finish work

**LocationSystem (existing):**
- `LocationSystem.TravelTo(playerId, locationId)` ??? Change location

### **New Component: Interactable**

Objects in the world need an `Interactable` MonoBehaviour component:
```csharp
public class Interactable : MonoBehaviour {
    public InteractionType interactionType;
    public string targetId;          // Entity ID, Job ID, Location ID, etc.
    public string promptText;        // "Press E to start shift"
    public float interactionRange;   // Max distance
    public bool requiresLineOfSight;
    public List<string> requiredItems; // For conditional interactions
}
```

---

## **III. DATA STRUCTURES**

### **Enums**

```csharp
public enum InteractionType {
    PickupItem,         // Add item to inventory
    DropItem,           // Remove item from inventory
    StartJob,           // Begin work shift
    EndJob,             // Finish work shift
    TalkToNPC,          // Initiate dialogue
    OpenDoor,           // Scene transition
    SitDown,            // Change posture
    StandUp,            // Change posture
    UseComputer,        // Start computer activity
    UsePhone,           // Open phone UI
    Examine,            // Display info
    Custom              // Game-specific interactions
}

public enum InteractionResult {
    Success,            // Interaction completed
    Failed,             // Validation failed
    Cancelled,          // Player cancelled
    Unavailable         // Object not interactable
}
```

### **Classes**

```csharp
[System.Serializable]
public class InteractionData {
    public GameObject interactableObject;
    public Interactable interactableComponent;
    public InteractionType type;
    public string targetId;
    public string promptText;
    public float distance;
    public bool isAvailable;        // Passes validation
    public string unavailableReason; // Why it failed (e.g., "Not enough money")
}

[System.Serializable]
public class InteractionSystemState {
    public InteractionData currentTarget;    // What player is looking at
    public InteractionData lastInteraction;  // Last successful interaction
    public bool isInteracting;               // Mid-interaction
    public float lastInteractionTime;
}
```

---

## **IV. PUBLIC API**

### **Core Methods**

```csharp
// Initialization
public void Initialize();

// Raycasting (called every frame)
public InteractionData DetectInteractable();
public void UpdateCurrentTarget();

// Interaction Execution
public InteractionResult TryInteract();
public InteractionResult Interact(InteractionData data);

// Validation
public bool CanInteract(InteractionData data, out string reason);
private bool ValidatePickupItem(string itemId, out string reason);
private bool ValidateStartJob(string jobId, out string reason);
private bool ValidateTravelTo(string locationId, out string reason);

// State Queries
public InteractionData GetCurrentTarget();
public bool IsLookingAtInteractable();
public bool IsInteracting();

// Testing Helpers
public void SetCurrentTargetForTesting(InteractionData data);
public InteractionSystemState GetStateForTesting();
```

### **Events**

```csharp
// Detection Events
public event Action<InteractionData> OnInteractableDetected;
public event Action OnInteractableLost;

// Interaction Events
public event Action<InteractionData> OnInteractionStarted;
public event Action<InteractionData, InteractionResult> OnInteractionCompleted;
public event Action<InteractionData, string> OnInteractionFailed;

// Target Events
public event Action<InteractionData> OnCurrentTargetChanged;
```

---

## **V. IMPLEMENTATION DETAILS**

### **Singleton Pattern**

```csharp
using UnityEngine;
using HustleEconomy.Core;

namespace HustleEconomy.Core {
    public class InteractionSystem : MonoBehaviour {
        // Singleton
        private static InteractionSystem instance;
        public static InteractionSystem Instance {
            get {
                if (instance == null) {
                    instance = FindObjectOfType<InteractionSystem>();
                    if (instance == null) {
                        GameObject go = new GameObject("InteractionSystem");
                        instance = go.AddComponent<InteractionSystem>();
                    }
                }
                return instance;
            }
        }

        [Header("Settings")]
        [SerializeField] private float maxInteractionDistance = 3f;
        [SerializeField] private float interactionCooldown = 0.5f;
        [SerializeField] private LayerMask interactableLayer;

        // State
        private InteractionSystemState state;

        private void Awake() {
            if (instance != null && instance != this) {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);

            Initialize();
        }

        public void Initialize() {
            state = new InteractionSystemState {
                currentTarget = null,
                isInteracting = false,
                lastInteractionTime = 0f
            };
        }

        // ... (implementation continues)
    }
}
```

### **Raycast Detection**

```csharp
private void Update() {
    // Update what player is looking at
    UpdateCurrentTarget();

    // Check for interaction input
    if (InputManager.Instance.GetActionDown(InputAction.Interact)) {
        TryInteract();
    }
}

public void UpdateCurrentTarget() {
    InteractionData previousTarget = state.currentTarget;
    
    // Raycast from player
    RaycastHit hit = PlayerController.Instance.GetLookTarget(maxInteractionDistance);

    if (hit.collider != null) {
        // Check if object has Interactable component
        Interactable interactable = hit.collider.GetComponent<Interactable>();

        if (interactable != null) {
            // Create interaction data
            InteractionData newTarget = new InteractionData {
                interactableObject = hit.collider.gameObject,
                interactableComponent = interactable,
                type = interactable.interactionType,
                targetId = interactable.targetId,
                promptText = interactable.promptText,
                distance = hit.distance
            };

            // Validate
            newTarget.isAvailable = CanInteract(newTarget, out string reason);
            newTarget.unavailableReason = reason;

            state.currentTarget = newTarget;

            // Fire event if target changed
            if (previousTarget == null || 
                previousTarget.interactableObject != newTarget.interactableObject) {
                OnInteractableDetected?.Invoke(newTarget);
                OnCurrentTargetChanged?.Invoke(newTarget);
            }

            return;
        }
    }

    // No interactable found
    if (previousTarget != null) {
        OnInteractableLost?.Invoke();
        OnCurrentTargetChanged?.Invoke(null);
    }

    state.currentTarget = null;
}
```

### **Interaction Execution**

```csharp
public InteractionResult TryInteract() {
    // Check cooldown
    if (Time.time - state.lastInteractionTime < interactionCooldown) {
        return InteractionResult.Cancelled;
    }

    // Check if target exists
    if (state.currentTarget == null) {
        return InteractionResult.Unavailable;
    }

    // Validate
    if (!state.currentTarget.isAvailable) {
        OnInteractionFailed?.Invoke(state.currentTarget, state.currentTarget.unavailableReason);
        return InteractionResult.Failed;
    }

    // Execute interaction
    InteractionResult result = Interact(state.currentTarget);

    state.lastInteractionTime = Time.time;
    state.lastInteraction = state.currentTarget;

    return result;
}

public InteractionResult Interact(InteractionData data) {
    state.isInteracting = true;
    OnInteractionStarted?.Invoke(data);

    InteractionResult result = InteractionResult.Failed;

    try {
        switch (data.type) {
            case InteractionType.PickupItem:
                result = HandlePickupItem(data.targetId);
                break;

            case InteractionType.StartJob:
                result = HandleStartJob(data.targetId);
                break;

            case InteractionType.EndJob:
                result = HandleEndJob(data.targetId);
                break;

            case InteractionType.TalkToNPC:
                result = HandleTalkToNPC(data.targetId);
                break;

            case InteractionType.OpenDoor:
                result = HandleOpenDoor(data.targetId);
                break;

            case InteractionType.SitDown:
                result = HandleSitDown(data.interactableObject);
                break;

            case InteractionType.StandUp:
                result = HandleStandUp();
                break;

            case InteractionType.UseComputer:
                result = HandleUseComputer(data.targetId);
                break;

            case InteractionType.UsePhone:
                result = HandleUsePhone();
                break;

            case InteractionType.Examine:
                result = HandleExamine(data.targetId);
                break;

            default:
                Debug.LogWarning($"Unhandled interaction type: {data.type}");
                result = InteractionResult.Failed;
                break;
        }

    } catch (System.Exception e) {
        Debug.LogError($"Interaction failed: {e.Message}");
        result = InteractionResult.Failed;
    }

    state.isInteracting = false;
    OnInteractionCompleted?.Invoke(data, result);

    return result;
}
```

### **Interaction Handlers**

```csharp
private InteractionResult HandlePickupItem(string itemId) {
    bool success = InventorySystem.Instance.PickupItem("player", itemId);
    
    if (success) {
        // Hide object in world
        var entity = EntitySystem.Instance.GetEntity(itemId);
        if (entity != null) {
            EntitySystem.Instance.SetOwner(itemId, "player");
        }
        
        return InteractionResult.Success;
    }

    return InteractionResult.Failed;
}

private InteractionResult HandleStartJob(string jobId) {
    bool success = JobSystem.Instance.StartShift("player", jobId);
    
    if (success) {
        // ActivitySystem will handle the rest
        return InteractionResult.Success;
    }

    return InteractionResult.Failed;
}

private InteractionResult HandleEndJob(string jobId) {
    JobSystem.Instance.EndShift("player", jobId);
    return InteractionResult.Success;
}

private InteractionResult HandleTalkToNPC(string npcId) {
    // TODO: Dialogue system integration
    Debug.Log($"Talking to NPC: {npcId}");
    return InteractionResult.Success;
}

private InteractionResult HandleOpenDoor(string locationId) {
    LocationSystem.Instance.TravelTo("player", locationId);
    return InteractionResult.Success;
}

private InteractionResult HandleSitDown(GameObject chair) {
    PlayerController.Instance.SetPosture(PlayerPosture.Sitting);
    
    // Position player at chair
    // TODO: Snap player to chair position/rotation
    
    return InteractionResult.Success;
}

private InteractionResult HandleStandUp() {
    PlayerController.Instance.SetPosture(PlayerPosture.Standing);
    return InteractionResult.Success;
}

private InteractionResult HandleUseComputer(string computerId) {
    // TODO: Computer UI integration
    Debug.Log($"Using computer: {computerId}");
    return InteractionResult.Success;
}

private InteractionResult HandleUsePhone() {
    PhoneUI.Instance.TogglePhone();
    return InteractionResult.Success;
}

private InteractionResult HandleExamine(string entityId) {
    var entity = EntitySystem.Instance.GetEntity(entityId);
    if (entity != null) {
        HUDController.Instance.ShowNotification(
            entity.name,
            entity.description ?? "Nothing special.",
            NotificationType.Info,
            3f
        );
        return InteractionResult.Success;
    }

    return InteractionResult.Failed;
}
```

### **Validation**

```csharp
public bool CanInteract(InteractionData data, out string reason) {
    // Distance check
    if (data.distance > maxInteractionDistance) {
        reason = "Too far away";
        return false;
    }

    // Line of sight check
    if (data.interactableComponent.requiresLineOfSight) {
        if (!PlayerController.Instance.IsLookingAt(data.interactableObject, maxInteractionDistance)) {
            reason = "Cannot see target";
            return false;
        }
    }

    // Type-specific validation
    switch (data.type) {
        case InteractionType.PickupItem:
            return ValidatePickupItem(data.targetId, out reason);

        case InteractionType.StartJob:
            return ValidateStartJob(data.targetId, out reason);

        case InteractionType.OpenDoor:
            return ValidateTravelTo(data.targetId, out reason);

        default:
            reason = "";
            return true;
    }
}

private bool ValidatePickupItem(string itemId, out string reason) {
    // Check if player has inventory space
    // TODO: InventorySystem capacity check
    
    // Check if item is accessible
    var entity = EntitySystem.Instance.GetEntity(itemId);
    if (entity == null) {
        reason = "Item not found";
        return false;
    }

    if (!string.IsNullOrEmpty(entity.owner) && entity.owner != "player") {
        reason = "Item belongs to someone else";
        return false;
    }

    reason = "";
    return true;
}

private bool ValidateStartJob(string jobId, out string reason) {
    // Check if player is at correct location
    var job = JobSystem.Instance.GetJob(jobId);
    if (job == null) {
        reason = "Job not found";
        return false;
    }

    string playerLocation = LocationSystem.Instance.GetPlayerLocation("player");
    if (playerLocation != job.locationId) {
        reason = "Not at job location";
        return false;
    }

    // Check if already working
    var currentJob = JobSystem.Instance.GetCurrentJob("player");
    if (currentJob != null) {
        reason = "Already working";
        return false;
    }

    reason = "";
    return true;
}

private bool ValidateTravelTo(string locationId, out string reason) {
    // Check if location is accessible
    bool canTravel = LocationSystem.Instance.CanTravelTo("player", locationId);
    
    if (!canTravel) {
        reason = "Location not accessible";
        return false;
    }

    reason = "";
    return true;
}
```

---

## **VI. UI INTEGRATION**

### **Prompt Display**

InteractionSystem should integrate with HUDController to show prompts:

```csharp
// In UpdateCurrentTarget(), after detecting interactable:
if (state.currentTarget != null && state.currentTarget.isAvailable) {
    // Show prompt via HUD
    // TODO: HUDController.ShowInteractionPrompt(state.currentTarget.promptText);
} else if (state.currentTarget != null && !state.currentTarget.isAvailable) {
    // Show unavailable reason
    // TODO: HUDController.ShowInteractionPrompt(state.currentTarget.unavailableReason, disabled: true);
} else {
    // Hide prompt
    // TODO: HUDController.HideInteractionPrompt();
}
```

This requires HUDController to have prompt methods (can be added during integration).

---

## **VII. INTEGRATION TESTS (POST-IMPLEMENTATION)**

### **Test Scenarios**

**Test 1: Pickup Item**
```
1. Player looks at item on floor
2. InteractionSystem raycasts, finds item
3. Validates: item is accessible, player has space
4. Shows prompt: "Press E to pick up"
5. Player presses E
6. InteractionSystem.HandlePickupItem() called
7. InventorySystem.PickupItem() called
8. EntitySystem.SetOwner() called
9. Item disappears from world ???
```

**Test 2: Start Job**
```
1. Player at janitorial closet
2. Looks at closet (Interactable component)
3. Validates: at correct location, not already working
4. Shows prompt: "Press E to start shift"
5. Player presses E
6. JobSystem.StartShift() called
7. ActivitySystem creates activity
8. MinigameUI creates UI
9. CameraController switches to first-person ???
```

**Test 3: Validation Failure**
```
1. Player looks at locked door
2. Validates: LocationSystem.CanTravelTo() = false
3. Shows prompt: "Locked" (disabled)
4. Player presses E
5. OnInteractionFailed event fires
6. HUDController shows: "Location not accessible" ???
```

---

## **VIII. TESTING CHECKLIST**

### **Unit Tests (30 minimum)**

**Raycast Tests (6):**
- [ ] DetectInteractable finds object with Interactable component
- [ ] DetectInteractable returns null beyond max distance
- [ ] DetectInteractable ignores objects without Interactable
- [ ] UpdateCurrentTarget fires OnInteractableDetected
- [ ] UpdateCurrentTarget fires OnInteractableLost
- [ ] OnCurrentTargetChanged fires on target change

**Interaction Tests (10):**
- [ ] TryInteract succeeds when target valid
- [ ] TryInteract fails when no target
- [ ] TryInteract respects cooldown
- [ ] HandlePickupItem calls InventorySystem
- [ ] HandleStartJob calls JobSystem
- [ ] HandleOpenDoor calls LocationSystem
- [ ] HandleSitDown changes PlayerController posture
- [ ] HandleUsePhone opens PhoneUI
- [ ] OnInteractionStarted fires
- [ ] OnInteractionCompleted fires

**Validation Tests (8):**
- [ ] CanInteract checks distance
- [ ] CanInteract checks line of sight
- [ ] ValidatePickupItem checks ownership
- [ ] ValidateStartJob checks location
- [ ] ValidateStartJob checks current job
- [ ] ValidateTravelTo checks accessibility
- [ ] Unavailable reason set correctly
- [ ] isAvailable flag set correctly

**Integration Tests (6):**
- [ ] Interacting with item removes from world
- [ ] Interacting with job closet starts activity
- [ ] Interacting with door triggers scene load
- [ ] Prompt displayed when looking at interactable
- [ ] Prompt hidden when looking away
- [ ] Multiple interaction types work correctly

---

## **IX. KNOWN LIMITATIONS**

- No multi-object interaction (can't interact with multiple things at once)
- Simple raycast (single ray from camera, no sphere cast)
- No interaction animations
- No interaction cancellation (once started, completes)
- Prompt system requires HUDController extension

---

## **X. FUTURE ENHANCEMENTS (Post-V1.0)**

- Interaction animations (pickup, open door, sit down)
- Multi-step interactions (combine items, craft)
- Contextual interaction wheel (multiple actions per object)
- Interaction cooldown per object type
- Highlight interactable objects (outline shader)
- Audio feedback for interactions

---

**END OF SPECIFICATION**

---

## **FINAL NOTE**

This completes all component specifications needed for the playable prototype:

1. ??? PlayerController (18)
2. ??? CameraController (19)
3. ??? InputManager (20)
4. ??? PhoneUI (21)
5. ??? MinigameUI (22)
6. ??? HUDController (23)
7. ??? GameManager (24)
8. ??? InteractionSystem (25)

**Next step:** Sequential implementation following dependency order, then integration testing.

