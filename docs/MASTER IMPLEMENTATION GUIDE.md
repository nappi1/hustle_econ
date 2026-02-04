MASTER IMPLEMENTATION GUIDE
Project: Hustle Economy
Purpose: This guide provides the foundational setup, coding patterns, build order, and integration approach for implementing all game systems. Read this before implementing any individual system.

Target Engine: Unity (C#) - adaptable to Godot if needed

I. PROJECT STRUCTURE
File Organization
HustleEconomy/
├── Assets/
│   ├── Scripts/
│   │   ├── Core/                    # Core game systems
│   │   │   ├── EntitySystem.cs
│   │   │   ├── TimeSystem.cs
│   │   │   ├── EconomySystem.cs
│   │   │   ├── ReputationSystem.cs
│   │   │   ├── RelationshipSystem.cs
│   │   │   ├── DetectionSystem.cs
│   │   │   ├── JobSystem.cs
│   │   │   ├── SkillSystem.cs
│   │   │   ├── BodySystem.cs
│   │   │   ├── IntoxicationSystem.cs
│   │   │   ├── AdultContentSystem.cs
│   │   │   ├── HeatSystem.cs
│   │   │   ├── LocationSystem.cs
│   │   │   ├── EventSystem.cs
│   │   │   ├── InventorySystem.cs
│   │   │   └── ActivitySystem.cs
│   │   ├── Data/                    # Data classes
│   │   │   ├── Entity.cs
│   │   │   ├── NPC.cs
│   │   │   ├── Job.cs
│   │   │   ├── Activity.cs
│   │   │   ├── ClothingItem.cs
│   │   │   └── ... (all data structures)
│   │   ├── Minigames/               # Minigame implementations
│   │   │   ├── MinigameBase.cs
│   │   │   ├── ClickTargetsMinigame.cs
│   │   │   ├── StreamingMinigame.cs
│   │   │   └── ...
│   │   ├── UI/                      # All UI scripts
│   │   │   ├── PhoneUI.cs
│   │   │   ├── ComputerUI.cs
│   │   │   ├── HUDController.cs
│   │   │   └── ...
│   │   ├── Camera/                  # Camera systems
│   │   │   ├── CameraController.cs
│   │   │   └── CameraRig.cs
│   │   └── Utilities/               # Helper classes
│   │       ├── ScreenFade.cs
│   │       ├── AudioManager.cs
│   │       └── ...
│   ├── Scenes/
│   │   ├── MainMenu.unity
│   │   ├── Apartment.unity
│   │   ├── Office.unity
│   │   ├── Street.unity
│   │   └── ...
│   ├── Prefabs/
│   ├── Materials/
│   ├── Audio/
│   └── Resources/
│       └── Data/                    # JSON/ScriptableObject data files
│           ├── Jobs/
│           ├── NPCs/
│           └── Items/
└── docs/
    ├── VISION.md
    ├── GDD.md
    └── specs/                       # All system specifications
II. CODING STANDARDS
Namespace Convention
csharp
namespace HustleEconomy.Core {
    // Core systems
}

namespace HustleEconomy.Data {
    // Data classes
}

namespace HustleEconomy.UI {
    // UI scripts
}

namespace HustleEconomy.Minigames {
    // Minigame implementations
}
Singleton Pattern (For All Systems)
All core systems use this exact pattern:

csharp
using UnityEngine;

namespace HustleEconomy.Core {
    public class SystemName : MonoBehaviour {
        // Singleton instance
        private static SystemName instance;
        public static SystemName Instance {
            get {
                if (instance == null) {
                    instance = FindObjectOfType<SystemName>();
                    if (instance == null) {
                        GameObject go = new GameObject("SystemName");
                        instance = go.AddComponent<SystemName>();
                    }
                }
                return instance;
            }
        }
        
        private void Awake() {
            // Enforce singleton
            if (instance != null && instance != this) {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Initialize system
            Initialize();
        }
        
        private void Initialize() {
            // System-specific initialization
        }
        
        // System methods go here...
    }
}
Why this pattern:

Systems persist across scene changes (DontDestroyOnLoad)
Only one instance ever exists
Auto-creates if referenced before scene placement
Clean access via SystemName.Instance.Method()
Event Pattern
All systems broadcast events using C# events, not UnityEvents:

csharp
// Declare events
public event Action<string> OnEventName;                    // Simple event
public event Action<string, float> OnEventWithData;         // Event with parameters

// Fire events (always null-check)
OnEventName?.Invoke(data);
OnEventWithData?.Invoke(id, value);

// Subscribe to events (in other systems)
void Start() {
    SystemName.Instance.OnEventName += HandleEvent;
}

void OnDestroy() {
    if (SystemName.Instance != null) {
        SystemName.Instance.OnEventName -= HandleEvent;
    }
}

void HandleEvent(string data) {
    // React to event
}
Data Serialization
All data classes must be serializable:

csharp
[System.Serializable]
public class DataClassName {
    public string id;
    public float value;
    // ... fields
}
For saving/loading:

Use JsonUtility.ToJson() and JsonUtility.FromJson()
Or use custom serialization if needed
Method Naming
csharp
// Public API methods: PascalCase
public void CreateEntity() { }
public float GetValue() { }

// Private methods: PascalCase
private void Initialize() { }
private void ProcessData() { }

// Event handlers: Handle[EventName]
private void HandleEntityCreated(Entity entity) { }

// Coroutines: [Action]Coroutine
private IEnumerator FadeCoroutine() { }
III. BUILD ORDER
Implement systems in this exact order to avoid dependency issues:

Phase 1: Foundational Systems (Week 1)
EntitySystem - Everything depends on this
TimeSystem - Needed by almost all systems
EconomySystem - Money flows needed early
ReputationSystem - Tracks player standing
Milestone: Can create entities, time passes, money transactions work, reputation changes

Phase 2: NPC & Detection (Week 2)
SkillSystem - Needed before jobs
RelationshipSystem - NPCs and memory
DetectionSystem - Catching player slacking
Milestone: NPCs exist, remember actions, can detect player

Phase 3: Activities & Jobs (Week 3)
LocationSystem - Where things happen
InventorySystem - What player owns
ActivitySystem - What player does
JobSystem - Work activities
Milestone: Player can work job, multitask, earn money

Phase 4: Additional Systems (Week 4)
BodySystem - Appearance, fitness
HeatSystem - Police attention
IntoxicationSystem - Substance effects
EventSystem - Scheduled events
Milestone: Full character customization, consequences functional

Phase 5: Content Systems (Week 5-6)
AdultContentSystem - Streaming, clothing, sex work
Milestone: All income paths functional

IV. INTEGRATION APPROACH
System Communication
Systems communicate ONLY through:

Direct method calls:
csharp
   float balance = EconomySystem.Instance.GetBalance(playerId);
Events (preferred for loose coupling):
csharp
   // System A broadcasts
   OnPlayerArrested?.Invoke(playerId);
   
   // System B listens
   DetectionSystem.Instance.OnPlayerArrested += HandleArrest;
Shared data (Entity System):
csharp
   Entity item = EntitySystem.Instance.GetEntity(itemId);
Never:

Don't create circular dependencies
Don't use static classes for systems (use singletons)
Don't use UnityEvents for system communication (C# events only)
Dependency Injection Pattern
When System A needs System B:

csharp
public class SystemA : MonoBehaviour {
    private SystemB systemB;
    
    private void Initialize() {
        // Get reference to dependency
        systemB = SystemB.Instance;
        
        // Subscribe to events
        systemB.OnSomeEvent += HandleEvent;
    }
}
Testing Integration
After implementing each system:

Create test scene with system GameObject
Write simple test script:
csharp
   public class SystemNameTest : MonoBehaviour {
       void Start() {
           // Test basic operations
           var result = SystemName.Instance.Method();
           Debug.Log($"Test result: {result}");
       }
   }
```
3. **Run test scene, verify console output**
4. **Check spec's testing checklist**

---

## **V. UNITY-SPECIFIC SETUP**

### **Scene Setup**

**Create "Core" scene:**
```
CoreSystems (empty GameObject)
├── EntitySystem (add component)
├── TimeSystem (add component)
├── EconomySystem (add component)
├── ... (all systems)
This scene persists across all scenes (DontDestroyOnLoad).

Load order:

Core scene loads first (contains all systems)
Game scenes (Apartment, Office) load additively
Systems persist, scenes swap
ScriptableObjects for Data
For static data (jobs, items, NPC templates):

csharp
[CreateAssetMenu(fileName = "NewJob", menuName = "Hustle Economy/Job")]
public class JobData : ScriptableObject {
    public string jobTitle;
    public float hourlyWage;
    public List<ShiftSchedule> shifts;
    // ... fields from spec
}
Store in: Assets/Resources/Data/Jobs/

Load at runtime:

csharp
JobData job = Resources.Load<JobData>("Data/Jobs/JanitorialJob");
```

---

### **Camera Setup**

**Create camera rig:**
```
MainCamera (GameObject)
├── Camera (component)
├── CameraController (component)
└── CameraRig (handles positioning)
CameraController switches between:

Third-person mode (default)
First-person mode (jobs, phone, intimacy)
VI. COMMON PATTERNS
Manager Pattern (Alternative to Singleton)
If you prefer managers over singletons:

csharp
public class GameManager : MonoBehaviour {
    public static GameManager Instance { get; private set; }
    
    public EntitySystem EntitySystem { get; private set; }
    public TimeSystem TimeSystem { get; private set; }
    // ... all systems
    
    private void Awake() {
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        // Initialize all systems
        EntitySystem = new EntitySystem();
        TimeSystem = new TimeSystem();
        // ...
    }
}
Use systems via:

csharp
GameManager.Instance.EntitySystem.CreateEntity(...);
Data-Driven Design
Store configuration in JSON/ScriptableObjects, not code:

Bad:

csharp
float janitorialWage = 15f;  // Hardcoded
Good:

csharp
JobData job = Resources.Load<JobData>("Data/Jobs/JanitorialJob");
float wage = job.hourlyWage;  // Data-driven
Why: Easier to balance, iterate, and expand without touching code.

Error Handling
Always validate inputs:

csharp
public Entity GetEntity(string entityId) {
    if (string.IsNullOrEmpty(entityId)) {
        Debug.LogWarning("GetEntity: entityId is null or empty");
        return null;
    }
    
    if (!entities.ContainsKey(entityId)) {
        Debug.LogWarning($"GetEntity: Entity {entityId} not found");
        return null;
    }
    
    return entities[entityId];
}
VII. SAVE SYSTEM STRATEGY
Each system provides save/load methods:

csharp
public class SystemName : MonoBehaviour {
    [System.Serializable]
    public class SaveData {
        // All data needed to restore system state
        public List<Entity> entities;
        public float currentTime;
        // ...
    }
    
    public SaveData GetSaveData() {
        return new SaveData {
            entities = entities.Values.ToList(),
            currentTime = Time.time,
            // ...
        };
    }
    
    public void LoadSaveData(SaveData data) {
        entities.Clear();
        foreach (var entity in data.entities) {
            entities[entity.id] = entity;
        }
        // ...
    }
}
Global save/load:

csharp
public class SaveManager : MonoBehaviour {
    public void SaveGame(string saveName) {
        GameSave save = new GameSave {
            entityData = EntitySystem.Instance.GetSaveData(),
            timeData = TimeSystem.Instance.GetSaveData(),
            // ... all systems
        };
        
        string json = JsonUtility.ToJson(save, true);
        File.WriteAllText(GetSavePath(saveName), json);
    }
    
    public void LoadGame(string saveName) {
        string json = File.ReadAllText(GetSavePath(saveName));
        GameSave save = JsonUtility.FromJson<GameSave>(json);
        
        EntitySystem.Instance.LoadSaveData(save.entityData);
        TimeSystem.Instance.LoadSaveData(save.timeData);
        // ... all systems
    }
}
VIII. PERFORMANCE CONSIDERATIONS
Update Loop Optimization
Don't update every frame if not needed:

csharp
private float updateInterval = 1f;  // Update once per second
private float timeSinceUpdate = 0f;

void Update() {
    timeSinceUpdate += Time.deltaTime;
    
    if (timeSinceUpdate >= updateInterval) {
        timeSinceUpdate = 0f;
        PerformUpdate();
    }
}
Object Pooling (for frequent spawns)
csharp
public class ObjectPool {
    private Queue<GameObject> pool = new Queue<GameObject>();
    
    public GameObject Get(GameObject prefab) {
        if (pool.Count > 0) {
            var obj = pool.Dequeue();
            obj.SetActive(true);
            return obj;
        }
        return Instantiate(prefab);
    }
    
    public void Return(GameObject obj) {
        obj.SetActive(false);
        pool.Enqueue(obj);
    }
}
Dictionary Lookups (preferred over Lists)
For frequent ID lookups:

csharp
// Good
Dictionary<string, Entity> entities;
Entity entity = entities[entityId];  // O(1)

// Bad
List<Entity> entities;
Entity entity = entities.Find(e => e.id == entityId);  // O(n)
IX. DEBUGGING TOOLS
Debug Commands
Create debug console:

csharp
public class DebugConsole : MonoBehaviour {
    void Update() {
        if (Input.GetKeyDown(KeyCode.F1)) {
            // Add money
            EconomySystem.Instance.AddIncome(playerId, 1000f, IncomeSource.Other, "Debug");
        }
        
        if (Input.GetKeyDown(KeyCode.F2)) {
            // Advance time 1 hour
            TimeSystem.Instance.AdvanceTime(60f);
        }
        
        // ... more debug keys
    }
}
Logging Standards
csharp
// Info (normal operation)
Debug.Log($"Entity created: {entity.id}");

// Warning (unexpected but handled)
Debug.LogWarning($"Entity {entityId} not found, returning null");

// Error (serious issue)
Debug.LogError($"Cannot create entity: invalid data");
```

---

## **X. IMPLEMENTATION WORKFLOW**

### **For Each System:**

1. **Read the spec** (`/docs/specs/SystemName.md`)
2. **Create the file** in correct folder with correct namespace
3. **Implement singleton pattern**
4. **Add data structures** from spec (classes, enums, structs)
5. **Implement all public methods** from spec
6. **Implement events** from spec
7. **Add Initialize() logic**
8. **Create test scene**
9. **Run through testing checklist** from spec
10. **Integrate with dependent systems**

---

### **Prompt Template for AI**

When asking AI to implement a system:
```
Implement [SystemName] following these guidelines:

1. Read the full specification: /docs/specs/[SystemName].md
2. Follow the Master Implementation Guide: /docs/MASTER_IMPLEMENTATION_GUIDE.md
3. Create file at: /Assets/Scripts/Core/[SystemName].cs
4. Use namespace: HustleEconomy.Core
5. Implement singleton pattern as shown in guide
6. Implement ALL methods from spec with exact signatures
7. Implement ALL events from spec
8. Add ALL data structures (classes, enums) from spec
9. Follow coding standards from guide

After implementation:
- Create test scene
- Verify against spec's testing checklist
- Confirm integration with dependent systems
```

---

## **XI. TROUBLESHOOTING**

### **Common Issues:**

**"Null reference exception on System.Instance"**
- System not in scene or destroyed
- Add system component to Core scene GameObject

**"DontDestroyOnLoad not working"**
- Singleton pattern not implemented correctly
- Check Awake() method

**"Events not firing"**
- Forgot null-check: `OnEvent?.Invoke()`
- Not subscribed: check `Start()` subscription

**"Circular dependency"**
- System A needs System B, System B needs System A
- Use events to break cycle
- Rethink architecture

---

## **XII. NEXT STEPS AFTER SETUP**

**After all systems are implemented:**

1. **Create first playable scene** (apartment + one job)
2. **Implement core loop** (work shift while checking phone)
3. **Add first NPC** (romantic partner)
4. **Test multitasking** (work + drug deal)
5. **Verify consequences** (get caught → fired)

**Then expand:**
- More jobs
- More NPCs
- More locations
- More content

---

## **XIII. RESOURCES**

**Unity Documentation:**
- Singleton pattern: https://unity.com/how-to/create-modular-and-maintainable-code-unity
- ScriptableObjects: https://docs.unity3d.com/Manual/class-ScriptableObject.html
- Events: https://docs.unity3d.com/Manual/UnityEvents.html

**Project Files:**
- Vision: `/docs/VISION.md`
- GDD: `/docs/GDD.md`
- Specs: `/docs/specs/`

---

## **XIV. FINAL CHECKLIST**

**Before starting implementation:**

- [ ] Unity project created
- [ ] Folder structure set up
- [ ] Core scene created with empty GameObjects for systems
- [ ] Master Implementation Guide reviewed
- [ ] Build order understood
- [ ] First system spec read (EntitySystem)

**After each system:**

- [ ] File created in correct location
- [ ] Singleton pattern implemented
- [ ] All methods from spec implemented
- [ ] All events implemented
- [ ] Test scene created
- [ ] Testing checklist completed
- [ ] Integration with other systems verified

---

**This guide is your foundation. Reference it constantly. When in doubt, check the guide, then the spec, then ask for help.**

**Good luck building Hustle Economy. You have everything you need.**

---

**END OF MASTER IMPLEMENTATION GUIDE**

---