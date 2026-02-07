# V2 Update (Aligned to Current Codebase)

This V2 spec reflects the current implementation. See docs/IMPLEMENTATION_DISCREPANCIES.md for cross-system gaps.

## Implementation Notes

- Still uses placeholder targets; no data-driven target rendering.
- No RecordAction API; input routes directly to minigame behavior.
- Context mapping remains simplified.

---
# MINIGAME UI SPECIFICATION

**System:** MinigameUI  
**Namespace:** HustleEconomy.UI  
**Dependencies:** MinigameSystem, InputManager, CameraController, ActivitySystem  
**Purpose:** Visual representation and input handling for all minigames

---

## **I. OVERVIEW**

### **What This System Does**

MinigameUI provides:
- Visual representation of minigame state (ClickTargets, SequenceMatch, etc.)
- Input routing to MinigameSystem
- Performance feedback (score, timer, progress)
- Context-aware rendering (fullscreen vs phone screen vs computer screen)
- Pause/resume UI states

### **Design Philosophy**

- **System renderer:** UI layer only, logic in MinigameSystem
- **Context-adaptive:** Renders differently based on where minigame runs (phone, computer, fullscreen)
- **Minimal chrome:** Simple, functional UI - avoid over-gamification
- **Peripheral awareness:** Never block environmental cues (boss approaching)

---

## **II. INTEGRATION POINTS (CRITICAL)**

### **Resolves These TODOs:**

From IMPLEMENTATION_DISCREPANCIES.md:
- Line 91: MinigameSystem "UI creation and destruction are TODOs" ??? MinigameUI handles this
- Line 79: ActivitySystem "Minigame system integration is TODO" ??? MinigameUI bridges ActivitySystem ??? MinigameSystem
- Line 92: MinigameSystem "Input handling not used in tests" ??? MinigameUI routes input

### **Requires These System APIs:**

**MinigameSystem (existing):**
- `MinigameSystem.StartMinigame(minigameId, config)` ??? Returns instance ID
- `MinigameSystem.GetMinigameState(instanceId)` ??? Current state
- `MinigameSystem.GetPerformance(instanceId)` ??? 0-1 performance score
- `MinigameSystem.RecordAction(instanceId, actionType, success)` ??? Log player action
- `MinigameSystem.EndMinigame(instanceId)` ??? Cleanup
- `MinigameSystem.PauseMinigame(instanceId)` ??? Pause state
- `MinigameSystem.ResumeMinigame(instanceId)` ??? Resume

**InputManager (already spec'd):**
- `InputManager.GetAction(InputAction.MinigameAction1)` ??? Click/interact
- `InputManager.GetMousePosition()` ??? For click detection
- `InputManager.SetContext(InputContext.Minigame)` ??? During fullscreen minigames

**ActivitySystem (existing):**
- `ActivitySystem.OnActivityStarted` ??? Event when activity begins (triggers minigame UI)
- `ActivitySystem.OnActivityEnded` ??? Event when activity ends (cleanup minigame UI)

**CameraController (already spec'd):**
- `CameraController.GetCurrentMode()` ??? Determines rendering context

### **New Integration Flow:**

```
ActivitySystem.StartActivity("janitorial_work")
  ???
OnActivityStarted event fires
  ???
MinigameUI listens, determines minigame type
  ???
MinigameUI.CreateMinigameUI(minigameId, context)
  ???
MinigameSystem.StartMinigame(minigameId, config)
  ???
MinigameUI renders ClickTargets visually
  ???
Player clicks on target
  ???
MinigameUI detects click, calculates hit
  ???
MinigameSystem.RecordAction(instanceId, "click", success)
  ???
MinigameSystem updates performance
  ???
ActivitySystem reads performance for job success
```

---

## **III. DATA STRUCTURES**

### **Enums**

```csharp
public enum MinigameUIContext {
    Fullscreen,     // Takes entire viewport (e.g., driving)
    PhoneScreen,    // Renders inside phone UI bounds
    ComputerScreen, // Renders inside computer monitor bounds
    WorldSpace      // 3D objects in world (e.g., mop spots on floor)
}

public enum MinigameUIState {
    Hidden,         // Not visible
    Initializing,   // Creating UI elements
    Active,         // Player can interact
    Paused,         // Frozen, showing pause overlay
    Ending          // Cleanup in progress
}
```

### **Classes**

```csharp
[System.Serializable]
public class MinigameUIConfig {
    public MinigameType minigameType;
    public MinigameUIContext context;
    public RectTransform containerBounds;  // For phone/computer contexts
    public Canvas targetCanvas;            // Which canvas to render on
    public Color primaryColor;             // Theme color
    public bool showTimer;
    public bool showScore;
    public bool showPerformance;
}

[System.Serializable]
public class ClickTargetVisual {
    public GameObject visualObject;
    public Vector3 worldPosition;          // For world-space targets
    public Vector2 screenPosition;         // For screen-space targets
    public float radius;
    public bool isActive;
    public int targetIndex;
}

[System.Serializable]
public class MinigameUIInstance {
    public string instanceId;              // Matches MinigameSystem instance
    public MinigameType type;
    public MinigameUIContext context;
    public MinigameUIState state;
    public GameObject rootObject;          // Parent of all UI elements
    public List<ClickTargetVisual> targets;
    public float startTime;
    public float currentPerformance;
}
```

---

## **IV. PUBLIC API**

### **Core Methods**

```csharp
// Initialization
public void Initialize();

// Minigame Lifecycle
public string CreateMinigameUI(string minigameId, MinigameUIContext context, MinigameUIConfig config);
public void DestroyMinigameUI(string instanceId);
public void PauseMinigameUI(string instanceId);
public void ResumeMinigameUI(string instanceId);

// Update (called per frame)
public void UpdateMinigameUI(string instanceId);

// Input Handling
public void HandleClick(Vector3 clickPosition);
public void HandleKeyPress(KeyCode key);

// State Queries
public MinigameUIState GetState(string instanceId);
public float GetPerformance(string instanceId);
public bool IsMinigameActive(string instanceId);

// Visual Feedback
public void ShowSuccess(string instanceId, Vector3 position);
public void ShowFailure(string instanceId, Vector3 position);
public void UpdatePerformanceDisplay(string instanceId, float performance);

// Testing Helpers
public void SetStateForTesting(string instanceId, MinigameUIState state);
public MinigameUIInstance GetInstanceForTesting(string instanceId);
```

### **Events**

```csharp
// Lifecycle Events
public event Action<string> OnMinigameUICreated;    // instanceId
public event Action<string> OnMinigameUIDestroyed;  // instanceId

// Interaction Events
public event Action<string, bool> OnTargetClicked;  // instanceId, success
public event Action<string> OnMinigamePaused;
public event Action<string> OnMinigameResumed;
```

---

## **V. IMPLEMENTATION DETAILS**

### **Singleton Pattern**

```csharp
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using HustleEconomy.Core;

namespace HustleEconomy.UI {
    public class MinigameUI : MonoBehaviour {
        // Singleton
        private static MinigameUI instance;
        public static MinigameUI Instance {
            get {
                if (instance == null) {
                    instance = FindObjectOfType<MinigameUI>();
                    if (instance == null) {
                        Debug.LogError("MinigameUI instance not found in scene");
                    }
                }
                return instance;
            }
        }

        // Prefabs (assigned in Inspector)
        [Header("Prefabs")]
        [SerializeField] private GameObject clickTargetPrefab;
        [SerializeField] private GameObject successFeedbackPrefab;
        [SerializeField] private GameObject failureFeedbackPrefab;
        [SerializeField] private GameObject performanceHUDPrefab;

        // Canvases
        [Header("Canvases")]
        [SerializeField] private Canvas worldCanvas;       // For world-space minigames
        [SerializeField] private Canvas screenCanvas;      // For fullscreen minigames
        [SerializeField] private Canvas phoneCanvas;       // From PhoneUI
        [SerializeField] private Canvas computerCanvas;    // For computer minigames

        // State
        private Dictionary<string, MinigameUIInstance> instances;

        private void Awake() {
            if (instance != null && instance != this) {
                Destroy(gameObject);
                return;
            }
            instance = this;

            Initialize();
        }

        public void Initialize() {
            instances = new Dictionary<string, MinigameUIInstance>();

            // Subscribe to ActivitySystem events
            ActivitySystem.Instance.OnActivityStarted += HandleActivityStarted;
            ActivitySystem.Instance.OnActivityEnded += HandleActivityEnded;
        }

        private void OnDestroy() {
            if (ActivitySystem.Instance != null) {
                ActivitySystem.Instance.OnActivityStarted -= HandleActivityStarted;
                ActivitySystem.Instance.OnActivityEnded -= HandleActivityEnded;
            }
        }

        // ... (implementation continues)
    }
}
```

### **Activity Integration**

```csharp
private void HandleActivityStarted(string activityId) {
    var activity = ActivitySystem.Instance.GetActivity(activityId);
    if (activity == null) return;

    // Determine if activity has minigame
    string minigameId = activity.minigameId;
    if (string.IsNullOrEmpty(minigameId)) return;

    // Determine context based on activity type and camera mode
    MinigameUIContext context = DetermineContext(activity);

    // Create config
    MinigameUIConfig config = new MinigameUIConfig {
        minigameType = GetMinigameType(minigameId),
        context = context,
        showTimer = true,
        showScore = false,
        showPerformance = true
    };

    // Create UI
    string instanceId = CreateMinigameUI(minigameId, context, config);

    // Link activity to UI instance
    // (Store mapping for later cleanup)
}

private void HandleActivityEnded(string activityId) {
    // Find UI instance for this activity
    // Destroy UI
    // Cleanup mapping
}

private MinigameUIContext DetermineContext(Activity activity) {
    // Phone-based activities
    if (activity.type == ActivityType.DrugDealing || 
        activity.type == ActivityType.Trading) {
        return MinigameUIContext.PhoneScreen;
    }

    // Computer-based activities
    if (activity.type == ActivityType.Streaming || 
        activity.type == ActivityType.Work && activity.context == "office") {
        return MinigameUIContext.ComputerScreen;
    }

    // Physical world activities
    if (activity.type == ActivityType.Work && activity.context == "janitorial") {
        return MinigameUIContext.WorldSpace;
    }

    // Default
    return MinigameUIContext.Fullscreen;
}
```

### **ClickTargets Minigame UI**

```csharp
public string CreateMinigameUI(string minigameId, MinigameUIContext context, MinigameUIConfig config) {
    // Start minigame in MinigameSystem
    var minigameConfig = new MinigameConfig {
        targetCount = 5,
        timeLimit = 60f,
        difficulty = 1.0f
    };
    
    string instanceId = MinigameSystem.Instance.StartMinigame(minigameId, minigameConfig);

    // Create UI instance
    MinigameUIInstance uiInstance = new MinigameUIInstance {
        instanceId = instanceId,
        type = config.minigameType,
        context = context,
        state = MinigameUIState.Initializing,
        targets = new List<ClickTargetVisual>(),
        startTime = Time.time
    };

    // Create root GameObject
    uiInstance.rootObject = new GameObject($"MinigameUI_{instanceId}");

    // Setup based on minigame type
    switch (config.minigameType) {
        case MinigameType.ClickTargets:
            SetupClickTargetsUI(uiInstance, config);
            break;

        case MinigameType.SequenceMatch:
            SetupSequenceMatchUI(uiInstance, config);
            break;

        // Other types stubbed for now
        default:
            Debug.LogWarning($"Minigame type {config.minigameType} UI not implemented");
            break;
    }

    uiInstance.state = MinigameUIState.Active;
    instances[instanceId] = uiInstance;

    OnMinigameUICreated?.Invoke(instanceId);
    return instanceId;
}

private void SetupClickTargetsUI(MinigameUIInstance instance, MinigameUIConfig config) {
    // Get target data from MinigameSystem
    var minigameState = MinigameSystem.Instance.GetMinigameState(instance.instanceId);
    var clickTargetsState = minigameState as ClickTargetsMinigame; // Cast to specific type

    if (clickTargetsState == null) {
        Debug.LogError("Failed to get ClickTargets state");
        return;
    }

    // Create visual for each target
    foreach (var target in clickTargetsState.targets) {
        GameObject targetVisual = Instantiate(clickTargetPrefab);

        if (config.context == MinigameUIContext.WorldSpace) {
            // Position in 3D world (janitorial - mop spots on floor)
            targetVisual.transform.position = target.position;
            targetVisual.transform.SetParent(instance.rootObject.transform);
        } else {
            // Position in screen space (phone/computer)
            targetVisual.transform.SetParent(GetCanvas(config.context).transform);
            RectTransform rect = targetVisual.GetComponent<RectTransform>();
            rect.anchoredPosition = ConvertToScreenPosition(target.position, config.containerBounds);
        }

        // Style based on active state
        UpdateTargetVisual(targetVisual, target.isActive);

        instance.targets.Add(new ClickTargetVisual {
            visualObject = targetVisual,
            worldPosition = target.position,
            targetIndex = instance.targets.Count,
            isActive = target.isActive
        });
    }
}

private void UpdateTargetVisual(GameObject targetVisual, bool isActive) {
    var image = targetVisual.GetComponent<Image>();
    if (image != null) {
        image.color = isActive ? Color.yellow : Color.gray;
    }

    // Scale animation for active targets
    if (isActive) {
        // Simple pulse animation
        StartCoroutine(PulseTarget(targetVisual));
    }
}

private IEnumerator PulseTarget(GameObject target) {
    float time = 0f;
    Vector3 baseScale = target.transform.localScale;

    while (target != null && target.activeSelf) {
        time += Time.deltaTime;
        float scale = 1f + Mathf.Sin(time * 3f) * 0.1f;
        target.transform.localScale = baseScale * scale;
        yield return null;
    }
}
```

### **Input Handling**

```csharp
private void Update() {
    // Handle input for all active minigames
    if (InputManager.Instance.GetActionDown(InputAction.MinigameAction1)) {
        Vector3 clickPosition = InputManager.Instance.GetMousePosition();
        HandleClick(clickPosition);
    }

    // Update all active instances
    foreach (var instance in instances.Values) {
        if (instance.state == MinigameUIState.Active) {
            UpdateMinigameUI(instance.instanceId);
        }
    }
}

public void HandleClick(Vector3 clickPosition) {
    foreach (var instance in instances.Values) {
        if (instance.state != MinigameUIState.Active) continue;

        // Check if click hit any target
        foreach (var target in instance.targets) {
            if (!target.isActive) continue;

            bool hit = CheckClickHit(clickPosition, target, instance.context);

            if (hit) {
                // Record action in MinigameSystem
                MinigameSystem.Instance.RecordAction(
                    instance.instanceId,
                    "click",
                    true  // success
                );

                // Visual feedback
                ShowSuccess(instance.instanceId, target.worldPosition);

                // Update target state
                target.isActive = false;
                UpdateTargetVisual(target.visualObject, false);

                // Fire event
                OnTargetClicked?.Invoke(instance.instanceId, true);

                break;
            }
        }
    }
}

private bool CheckClickHit(Vector3 clickPosition, ClickTargetVisual target, MinigameUIContext context) {
    if (context == MinigameUIContext.WorldSpace) {
        // Raycast from camera to world
        Ray ray = Camera.main.ScreenPointToRay(clickPosition);
        return Physics.Raycast(ray, out RaycastHit hit) && 
               Vector3.Distance(hit.point, target.worldPosition) < target.radius;
    } else {
        // Screen space distance check
        Vector2 clickPos2D = new Vector2(clickPosition.x, clickPosition.y);
        float distance = Vector2.Distance(clickPos2D, target.screenPosition);
        return distance < target.radius;
    }
}
```

### **Performance Updates**

```csharp
public void UpdateMinigameUI(string instanceId) {
    if (!instances.ContainsKey(instanceId)) return;

    var instance = instances[instanceId];

    // Get current performance from MinigameSystem
    float performance = MinigameSystem.Instance.GetPerformance(instanceId);
    instance.currentPerformance = performance;

    // Update performance display
    UpdatePerformanceDisplay(instanceId, performance);

    // Sync visual state with MinigameSystem state
    var minigameState = MinigameSystem.Instance.GetMinigameState(instanceId);
    if (minigameState.isCompleted) {
        // End minigame
        DestroyMinigameUI(instanceId);
    }
}

public void UpdatePerformanceDisplay(string instanceId, float performance) {
    // Update performance HUD (if visible)
    // This could be a progress bar, percentage, etc.
    // For V1.0, keep minimal - maybe just a text display
}
```

### **Cleanup**

```csharp
public void DestroyMinigameUI(string instanceId) {
    if (!instances.ContainsKey(instanceId)) return;

    var instance = instances[instanceId];
    instance.state = MinigameUIState.Ending;

    // Destroy all visuals
    foreach (var target in instance.targets) {
        if (target.visualObject != null) {
            Destroy(target.visualObject);
        }
    }

    // Destroy root object
    if (instance.rootObject != null) {
        Destroy(instance.rootObject);
    }

    // Remove from tracking
    instances.Remove(instanceId);

    // End minigame in system
    MinigameSystem.Instance.EndMinigame(instanceId);

    OnMinigameUIDestroyed?.Invoke(instanceId);
}
```

---

## **VI. CONTEXT-SPECIFIC RENDERING**

### **World Space (Janitorial)**

```
Player in first-person looking down at floor
???
Dirty spots rendered as 3D objects on floor
???
Click on spot ??? Raycast hits floor object
???
Spot disappears, "clean" effect plays
```

### **Phone Screen (Drug Dealing)**

```
Phone UI open (25% center)
???
MinigameUI renders inside phone bounds
???
Targets appear as text choices/buttons
???
Click choice ??? MinigameSystem records
```

### **Computer Screen (Streaming)**

```
Sitting at desk, monitor visible
???
MinigameUI renders inside monitor bounds (60% viewport)
???
Chat messages/buttons appear
???
Click to respond ??? MinigameSystem records
```

---

## **VII. INTEGRATION TESTS (POST-IMPLEMENTATION)**

### **Test Scenarios**

**Test 1: Janitorial Minigame Full Flow**
```
1. Player starts janitorial job
2. ActivitySystem.OnActivityStarted fires
3. MinigameUI creates ClickTargets UI (world-space)
4. 5 dirty spots appear on floor (3D objects)
5. Player clicks spot
6. Raycast hits ??? MinigameSystem.RecordAction
7. Spot disappears
8. Performance increases
9. ActivitySystem reads performance
10. After all spots cleaned, ActivitySystem ends activity
11. MinigameUI cleanup
```

**Test 2: Phone Drug Dealing**
```
1. Player opens phone
2. Opens Drug Dealing app
3. ActivitySystem starts activity
4. MinigameUI creates UI (phone context)
5. Choices render inside phone bounds
6. World still visible around phone
7. Player makes choice
8. MinigameSystem records
9. Boss visible approaching in peripheral
10. Player closes phone
11. ActivitySystem ends activity
12. MinigameUI cleanup
```

**Test 3: Performance Sync**
```
1. Minigame active
2. MinigameSystem performance = 0.5
3. MinigameUI UpdateMinigameUI() reads performance
4. Display shows 50%
5. Player completes target
6. MinigameSystem performance = 0.7
7. MinigameUI display updates to 70%
8. ActivitySystem queries performance ??? gets 0.7 ???
```

---

## **VIII. MISSING APIS REQUIRED**

### **MinigameSystem (Need to expose):**

Currently MinigameSystem has these but they may not be public:
```csharp
// Ensure these are public
public MinigameState GetMinigameState(string instanceId);
public ClickTargetsMinigame GetClickTargetsState(string instanceId); // Type-specific accessor
```

### **ActivitySystem (Already exists):**
```csharp
public Activity GetActivity(string activityId); // Access minigameId
```

---

## **IX. TESTING CHECKLIST**

### **Unit Tests (30 minimum)**

**Lifecycle Tests (6):**
- [ ] CreateMinigameUI creates instance
- [ ] DestroyMinigameUI removes instance
- [ ] Created instance has correct context
- [ ] Created instance starts MinigameSystem minigame
- [ ] Destroy cleans up all GameObjects
- [ ] Destroy ends MinigameSystem minigame

**ClickTargets Tests (8):**
- [ ] World-space targets positioned correctly
- [ ] Screen-space targets positioned correctly
- [ ] Click hit detection works (world-space)
- [ ] Click hit detection works (screen-space)
- [ ] Clicking active target records action
- [ ] Clicking inactive target does nothing
- [ ] Target visual updates on click
- [ ] Success feedback spawns on hit

**Performance Sync Tests (4):**
- [ ] UpdateMinigameUI reads MinigameSystem performance
- [ ] Performance display updates correctly
- [ ] Performance change triggers visual update
- [ ] Completion triggers cleanup

**Context Tests (6):**
- [ ] World-space targets render in 3D world
- [ ] Phone context targets render in phone bounds
- [ ] Computer context targets render in monitor bounds
- [ ] Fullscreen context takes entire viewport
- [ ] Context switch updates rendering correctly
- [ ] Peripheral vision maintained (world visible around UI)

**Integration Tests (6):**
- [ ] ActivitySystem.OnActivityStarted creates UI
- [ ] ActivitySystem.OnActivityEnded destroys UI
- [ ] Input routed to MinigameSystem correctly
- [ ] Performance synced with ActivitySystem
- [ ] Multiple minigames can run simultaneously
- [ ] Camera context affects rendering

---

## **X. KNOWN LIMITATIONS**

- Only ClickTargets implemented in V1.0
- No minigame tutorials/instructions
- Simple visual feedback (no particles/effects)
- No accessibility options (colorblind mode, etc.)
- No dynamic difficulty adjustment UI

---

## **XI. FUTURE ENHANCEMENTS (Post-V1.0)**

- Particle effects for success/failure
- Tutorial overlays for new minigames
- Difficulty indicators
- Combo/streak visual feedback
- Minigame-specific themes
- Accessibility features

---

**END OF SPECIFICATION**

