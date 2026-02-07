# COMPLETE IMPLEMENTATION GUIDE
**Hustle Economy - From Systems to Playable Prototype**

---

## ✅ PHASE 1 COMPLETE: ALL SPECIFICATIONS READY

You now have **complete specifications** for all components needed to build a playable prototype:

### **Core Systems (17 - Already Implemented)**
1. EntitySystem
2. TimeEnergySystem
3. EconomySystem
4. ReputationSystem
5. RelationshipSystem
6. DetectionSystem
7. JobSystem
8. SkillSystem
9. HeatSystem
10. LocationSystem
11. IntoxicationSystem
12. AdultContentSystem
13. BodySystem
14. EventSystem
15. InventorySystem
16. ActivitySystem
17. MinigameSystem

### **New Components (8 - Implemented)**
18. **PlayerController** - 3D character movement, posture, physics
19. **CameraController** - Third/first-person switching, auto-modes
20. **InputManager** - Unified input handling (keyboard/mouse)
21. **PhoneUI** - Phone overlay interface for multitasking
22. **MinigameUI** - Visual representation of minigames
23. **HUDController** - Time, energy, money, notifications
24. **GameManager** - Scene loading, system initialization, save/load
25. **InteractionSystem** - Raycasting, world interactions, context actions

---

## 📋 IMPLEMENTATION ORDER (Dependency-Respecting)

### **Step 1: Foundation (No Dependencies)**
```
1. InputManager (20-input-manager.md)
   - No dependencies
   - Provides input to everything else
   - ~6-8 hours implementation
   - 30 unit tests
```

### **Step 2: Player Control**
```
2. PlayerController (18-player-controller.md)
   - Depends on: InputManager
   - ~8-10 hours implementation
   - 30 unit tests

3. CameraController (19-camera-controller.md)
   - Depends on: PlayerController, InputManager
   - ~6-8 hours implementation
   - 30 unit tests
```

### **Step 3: UI Layer**
```
4. HUDController (23-hud-controller.md)
   - Depends on: TimeEnergySystem, EconomySystem, ActivitySystem
   - ~6-8 hours implementation
   - 30 unit tests

5. PhoneUI (21-phone-ui.md)
   - Depends on: CameraController, InputManager, ActivitySystem
   - ~10-12 hours implementation
   - 30 unit tests

6. MinigameUI (22-minigame-ui.md)
   - Depends on: MinigameSystem, InputManager, ActivitySystem
   - ~12-15 hours implementation
   - 30 unit tests
```

### **Step 4: World Interaction**
```
7. InteractionSystem (25-interaction-system.md)
   - Depends on: PlayerController, InputManager, all systems
   - ~8-10 hours implementation
   - 30 unit tests
```

### **Step 5: Orchestration**
```
8. GameManager (24-game-manager.md)
   - Depends on: EVERYTHING (coordinates all systems)
   - ~10-12 hours implementation
   - 30 unit tests
```

**Total Implementation Time:** ~66-83 hours (with AI assistance: ~40-60 hours)

---

## 🔧 INTEGRATION PHASE (After Implementation)

### **Required System API Additions**

Before integration tests, these APIs must be added to existing systems:

**EconomySystem:**
```csharp
// Add missing enum values
public enum IncomeSource {
    // ... existing values
    SexWork,            // Line 8 discrepancy
    SugarRelationship   // Line 8 discrepancy
}

public enum ExpenseType {
    // ... existing values
    Blackmail,          // Line 8 discrepancy
    Personal            // Line 8 discrepancy
}
```

**JobSystem:**
```csharp
// Add missing APIs (Line 9 discrepancy)
public void FireAllJobs(string playerId, string reason);
public void TriggerWarning(string playerId, string reason);
public void CheckTerminationForSexWork(string playerId);
public Job GetCurrentJob(string playerId); // Line 83 discrepancy
```

**RelationshipSystem:**
```csharp
// Add missing APIs (Line 10 discrepancy)
public List<NPC> GetNPCs(string playerId);
public NPC GetNPC(string npcId);

// Add to NPC class
public class NPC {
    // ... existing fields
    public BodyPreferences bodyPreferences;
}

// Add to PlayerAction
public class PlayerAction {
    // ... existing fields
    public bool isPositive;
}
```

**TimeEnergySystem:**
```csharp
// Add missing API (Line 6, 29, 64 discrepancies)
public float GetDeltaGameHours(float timestamp);
public void ScheduleRecurringEvent(string eventId, float intervalHours);
public void CancelRecurringEvent(string eventId);
```

**DetectionSystem:**
```csharp
// Add missing API (Line 26 discrepancy)
public void SetPatrolFrequency(float multiplier);
public void SetDetectionSensitivity(float multiplier);
```

---

## 🔌 INTEGRATION WIRING TASKS

### **1. ActivitySystem ↔ MinigameSystem**
**Current:** Line 79 - "start/pause/resume/end logs only"

**Fix:**
```csharp
// In ActivitySystem.StartActivity():
if (!string.IsNullOrEmpty(activity.minigameId)) {
    string instanceId = MinigameSystem.Instance.StartMinigame(
        activity.minigameId,
        GetMinigameConfig(activity)
    );
    activity.minigameInstanceId = instanceId;
}

// In ActivitySystem.EndActivity():
if (!string.IsNullOrEmpty(activity.minigameInstanceId)) {
    MinigameSystem.Instance.EndMinigame(activity.minigameInstanceId);
}

// In ActivitySystem.Update():
if (activity.isActive && !string.IsNullOrEmpty(activity.minigameInstanceId)) {
    activity.performance = MinigameSystem.Instance.GetPerformance(activity.minigameInstanceId);
}
```

### **2. ActivitySystem ↔ CameraController**
**Current:** Line 80 - "Camera mode switching is TODO"

**Fix:**
```csharp
// In ActivitySystem, subscribe to events:
void Start() {
    OnActivityStarted += (activityId) => {
        var activity = GetActivity(activityId);
        if (RequiresFirstPerson(activity.type)) {
            CameraController.Instance.OnJobStarted();
        }
    };
    
    OnActivityEnded += (activityId) => {
        var activity = GetActivity(activityId);
        if (RequiresFirstPerson(activity.type)) {
            CameraController.Instance.OnJobEnded();
        }
    };
}
```

### **3. ActivitySystem ↔ DetectionSystem**
**Current:** Line 81 - "Detection during work uses test flags only"

**Fix:**
```csharp
// In ActivitySystem.Update(), for active work activities:
if (activity.type == ActivityType.Work) {
    bool detected = DetectionSystem.Instance.CheckDetection(
        activity.playerId,
        activity.locationId
    );
    
    if (detected) {
        HandleDetection(activity);
    }
}

private void HandleDetection(Activity activity) {
    // Trigger warning
    JobSystem.Instance.TriggerWarning(activity.playerId, "Caught slacking");
    
    // Reduce performance
    activity.performance = Mathf.Max(0, activity.performance - 0.2f);
}
```

### **4. MinigameSystem ↔ MinigameUI**
**Current:** Line 91 - "UI creation and destruction are TODOs"

**Fix:**
This is handled by MinigameUI listening to ActivitySystem events (already designed in spec).

### **5. LocationSystem ↔ GameManager**
**Current:** Line 35 - "Scene load is a Debug.Log"

**Fix:**
```csharp
// In LocationSystem.TravelTo():
public void TravelTo(string playerId, string locationId) {
    // ... existing validation
    
    // Trigger actual scene load
    string sceneName = GetSceneNameForLocation(locationId);
    GameManager.Instance.LoadScene(sceneName);
    
    // Update player location data
    // ... rest of existing code
}
```

---

## 🧪 INTEGRATION TEST SCENARIOS

After wiring is complete, verify these end-to-end flows:

### **Test 1: Core Multitasking Loop**
```
1. Player spawns in apartment (GameManager loads scene)
2. Player walks to office (LocationSystem → GameManager scene load)
3. Player looks at janitorial closet (InteractionSystem raycast)
4. Prompt shows: "Press E to start shift" (HUDController)
5. Player presses E (InputManager → InteractionSystem)
6. Job starts (JobSystem.StartShift)
7. Activity created (ActivitySystem.CreateActivity)
8. Minigame starts (MinigameSystem.StartMinigame)
9. Minigame UI appears (MinigameUI creates visuals)
10. Camera switches to first-person (CameraController auto-switch)
11. Player sees dirty spots on floor (world-space targets)
12. Player presses P to open phone (PhoneUI)
13. Phone appears, world visible around edges (25% center)
14. Boss footsteps approach (DetectionSystem + audio)
15. Boss visible in peripheral (no UI warning)
16. Player closes phone quickly (PhoneUI)
17. Boss passes, no detection
18. Player clicks mop spots (MinigameUI input → MinigameSystem)
19. Performance increases (ActivitySystem reads MinigameSystem)
20. Shift ends (JobSystem.EndShift)
21. Camera returns to third-person (CameraController)
22. Money added (EconomySystem)
23. HUD updates balance (HUDController event listener)

✅ Complete multitasking loop verified
```

### **Test 2: Save/Load Flow**
```
1. Player at position (5, 0, 3) with $500
2. Press Escape → pause menu
3. Click "Save Game"
4. GameManager.SaveGame() collects all system data
5. JSON written to disk
6. Quit to main menu
7. Click "Load Game"
8. GameManager.LoadGame() reads JSON
9. All systems restored
10. Scene loaded
11. Player at (5, 0, 3) with $500 ✅
```

### **Test 3: Detection & Consequences**
```
1. Player working (mopping)
2. Player opens phone
3. Boss patrol active (DetectionSystem)
4. Boss line-of-sight check
5. Player visible, phone out
6. DetectionSystem.OnPlayerDetected fires
7. JobSystem.TriggerWarning called
8. HUDController shows notification: "Caught!"
9. Warning count increased
10. 3rd warning → JobSystem.FirePlayer
11. Activity ends, job lost
12. ReputationSystem updated (negative)
13. HeatSystem potentially triggered (depends on severity)

✅ Consequence cascade verified
```

---

## 📦 UNITY SCENE REQUIREMENTS

Before implementation, prepare Unity project:

### **Core Scene (Persistent)**
```
CoreSystems (empty GameObject with DontDestroyOnLoad)
├── EntitySystem
├── TimeEnergySystem
├── EconomySystem
├── ReputationSystem
├── RelationshipSystem
├── DetectionSystem
├── JobSystem
├── SkillSystem
├── HeatSystem
├── LocationSystem
├── IntoxicationSystem
├── AdultContentSystem
├── BodySystem
├── EventSystem
├── InventorySystem
├── ActivitySystem
├── MinigameSystem
├── InputManager
├── GameManager
└── InteractionSystem

UI (Canvas - DontDestroyOnLoad)
├── HUDController
├── PhoneUI
└── MinigameUI
```

### **Office Scene (Test Scene)**
```
Environment
├── Floor
├── Walls
├── Desk (with Interactable: UseComputer)
├── JanitorialCloset (with Interactable: StartJob)
├── Door (with Interactable: OpenDoor → Apartment)
└── DirtySpots (5x, for ClickTargets minigame)

NPCs
└── Boss (with patrol path, DetectionSystem observer)

Player
├── PlayerController
└── CameraController
```

---

## ⏭️ NEXT STEPS

**You are here:** ✅ Core systems/components/scenes are in place

**Option A: Runtime Hardening & Validation (Recommended)**
1. Run the three integration scenarios from this guide.
2. Expand save/load from minimal GameManager snapshot to per-system state serialization.
3. Migrate InputManager/minigame input paths to native Unity Input System bindings.
4. Resolve high-impact TODO integrations (EventSystem minigame creation, InteractionSystem shift/location checks, ActivitySystem pay/skill mapping).
5. Re-run integration tests and update discrepancy docs.

**Estimated Timeline:**
- Implementation: 40-60 hours (with AI assistance)
- Integration: 20-30 hours
- Scene building: 15-20 hours
- Testing & iteration: 10-20 hours
**Total: 85-130 hours (~3-5 weeks at 25 hrs/week)**

**Option B: Build Test Scene First**
1. Create minimal office scene with placeholders
2. Test systems manually with debug commands
3. Implement components as needed
4. Higher risk of rework, but faster to "see something"

**My Recommendation:** Option A - methodical implementation matching your existing approach.

---

## 📚 DOCUMENTATION COMPLETE

**You now have:**
- ✅ 17 system specifications (implemented with unit tests)
- ✅ 8 components implemented with tests
- ✅ Master Implementation Guide (coding standards, patterns)
- ✅ Implementation Discrepancies (known gaps documented)
- ✅ Integration requirements (missing APIs, wiring tasks)
- ✅ Testing strategy (unit tests + integration tests)
- ✅ Clear implementation order (dependency-respecting)

**Everything needed to build the prototype is documented.**

Ready to code? Let me know which component you want to implement first, or if you need any clarification on the specs!

---

**END OF IMPLEMENTATION GUIDE**

