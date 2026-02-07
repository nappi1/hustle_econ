# V2 Update (Aligned to Current Codebase)

This V2 spec reflects the current implementation. See docs/IMPLEMENTATION_DISCREPANCIES.md for cross-system gaps.

## Implementation Notes

- Save/Load is minimal (version, date, playtime, scene).
- Scene loading guarded by build settings; no LocationSystem OnSceneLoaded callback.

---
# GAME MANAGER SPECIFICATION

**System:** GameManager  
**Namespace:** HustleEconomy.Core  
**Dependencies:** All systems, all UI components, Unity SceneManager  
**Purpose:** Scene management, system initialization, game loop coordination, save/load

---

## **I. OVERVIEW**

### **What This System Does**

GameManager is the master coordinator:
- Initializes all systems on game start
- Manages scene loading/unloading
- Coordinates save/load operations
- Handles pause/resume
- Manages game state transitions (menu ??? gameplay ??? pause ??? etc.)
- Provides centralized access point for game-wide operations

### **Design Philosophy**

- **Single source of truth:** One place that knows about all systems
- **Initialization order:** Systems start in correct dependency order
- **Scene persistence:** DontDestroyOnLoad for systems, scenes swap underneath
- **Fail-safe:** Handles missing systems gracefully (degraded mode)

---

## **II. INTEGRATION POINTS (CRITICAL)**

### **Resolves These TODOs:**

From IMPLEMENTATION_DISCREPANCIES.md:
- Line 35: LocationSystem "Scene load is a Debug.Log" ??? GameManager handles actual scene loading
- General: No coordinated system initialization ??? GameManager provides initialization flow

### **Requires These System APIs:**

**All 17 Systems need:**
```csharp
SystemName.Instance.Initialize();  // Called on game start
SystemName.Instance.GetSaveData(); // For saving
SystemName.Instance.LoadSaveData(data); // For loading
```

**Unity SceneManager:**
```csharp
SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
SceneManager.UnloadSceneAsync(sceneName);
```

**All UI Components:**
```csharp
PhoneUI.Instance.Initialize();
MinigameUI.Instance.Initialize();
HUDController.Instance.Initialize();
```

### **Initialization Order (Critical):**

```
1. GameManager.Awake()
2. Foundational Systems:
   - EntitySystem
   - TimeEnergySystem
   - EconomySystem
3. NPC Systems:
   - ReputationSystem
   - RelationshipSystem
   - DetectionSystem
4. Activity Systems:
   - SkillSystem
   - JobSystem
   - ActivitySystem
   - MinigameSystem
5. Content Systems:
   - LocationSystem
   - InventorySystem
   - EventSystem
   - HeatSystem
   - IntoxicationSystem
   - BodySystem
   - AdultContentSystem
6. Presentation Layer:
   - InputManager
   - PlayerController
   - CameraController
7. UI Layer:
   - HUDController
   - PhoneUI
   - MinigameUI
8. Load initial scene (Main Menu or Apartment)
```

---

## **III. DATA STRUCTURES**

### **Enums**

```csharp
public enum GameState {
    Initializing,   // Systems loading
    MainMenu,       // Title screen
    Playing,        // Normal gameplay
    Paused,         // Game paused
    Loading,        // Scene transition
    Saving,         // Save in progress
    GameOver        // Player died/failed (future)
}

public enum SceneType {
    Menu,           // Main menu
    Apartment,      // Player's home
    Office,         // Office job
    Street,         // Outdoor areas
    Store,          // Shopping
    Restaurant,     // Food/social
    Other           // Generic
}
```

### **Classes**

```csharp
[System.Serializable]
public class GameSaveData {
    public string version;
    public System.DateTime saveDate;
    public float playTime;
    
    // System save data
    public EntitySystem.SaveData entityData;
    public TimeEnergySystem.SaveData timeData;
    public EconomySystem.SaveData economyData;
    public ReputationSystem.SaveData reputationData;
    public RelationshipSystem.SaveData relationshipData;
    public DetectionSystem.SaveData detectionData;
    public JobSystem.SaveData jobData;
    public SkillSystem.SaveData skillData;
    public HeatSystem.SaveData heatData;
    public LocationSystem.SaveData locationData;
    public IntoxicationSystem.SaveData intoxicationData;
    public AdultContentSystem.SaveData adultContentData;
    public BodySystem.SaveData bodyData;
    public EventSystem.SaveData eventData;
    public InventorySystem.SaveData inventoryData;
    public ActivitySystem.SaveData activityData;
    public MinigameSystem.SaveData minigameData;
    
    // UI state
    public PlayerController.SaveData playerData;
    public CameraController.SaveData cameraData;
    
    // Scene state
    public string currentScene;
}

[System.Serializable]
public class GameManagerState {
    public GameState currentState;
    public string currentScene;
    public string previousScene;
    public float gameStartTime;
    public float totalPlayTime;
    public bool isInitialized;
    public bool isPaused;
    public List<string> loadedScenes;
}
```

---

## **IV. PUBLIC API**

### **Core Methods**

```csharp
// Initialization
public void Initialize();
private void InitializeAllSystems();
private void InitializeUI();

// Game State
public void SetGameState(GameState state);
public GameState GetGameState();
public void PauseGame();
public void ResumeGame();
public void QuitGame();

// Scene Management
public void LoadScene(string sceneName, bool unloadCurrent = true);
public void UnloadScene(string sceneName);
public string GetCurrentScene();
public bool IsSceneLoaded(string sceneName);

// Save/Load
public void SaveGame(string saveName);
public void LoadGame(string saveName);
public bool SaveExists(string saveName);
public List<string> GetSaveFiles();
public void DeleteSave(string saveName);

// Time Management
public float GetPlayTime();
public void ResetPlayTime();

// Queries
public bool IsInitialized();
public bool IsPaused();

// Testing Helpers
public void SetStateForTesting(GameManagerState state);
public GameManagerState GetStateForTesting();
```

### **Events**

```csharp
// Game State Events
public event Action<GameState> OnGameStateChanged;
public event Action OnGamePaused;
public event Action OnGameResumed;

// Scene Events
public event Action<string> OnSceneLoadStarted;
public event Action<string> OnSceneLoadCompleted;
public event Action<string> OnSceneUnloaded;

// Save/Load Events
public event Action<string> OnSaveStarted;
public event Action<string> OnSaveCompleted;
public event Action<string> OnLoadStarted;
public event Action<string> OnLoadCompleted;

// Initialization
public event Action OnAllSystemsInitialized;
```

---

## **V. IMPLEMENTATION DETAILS**

### **Singleton Pattern (Persistent)**

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using HustleEconomy.UI;

namespace HustleEconomy.Core {
    public class GameManager : MonoBehaviour {
        // Singleton
        private static GameManager instance;
        public static GameManager Instance {
            get {
                if (instance == null) {
                    instance = FindObjectOfType<GameManager>();
                    if (instance == null) {
                        GameObject go = new GameObject("GameManager");
                        instance = go.AddComponent<GameManager>();
                    }
                }
                return instance;
            }
        }

        // State
        private GameManagerState state;
        
        private void Awake() {
            // Enforce singleton
            if (instance != null && instance != this) {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);

            Initialize();
        }

        public void Initialize() {
            if (state != null && state.isInitialized) {
                Debug.LogWarning("GameManager already initialized");
                return;
            }

            state = new GameManagerState {
                currentState = GameState.Initializing,
                gameStartTime = Time.time,
                totalPlayTime = 0f,
                isInitialized = false,
                isPaused = false,
                loadedScenes = new List<string>()
            };

            // Initialize all systems in order
            InitializeAllSystems();

            // Initialize UI
            InitializeUI();

            state.isInitialized = true;
            SetGameState(GameState.MainMenu);

            OnAllSystemsInitialized?.Invoke();
        }

        // ... (implementation continues)
    }
}
```

### **System Initialization**

```csharp
private void InitializeAllSystems() {
    Debug.Log("=== INITIALIZING ALL SYSTEMS ===");

    // Phase 1: Foundational
    Debug.Log("Phase 1: Foundational Systems");
    EntitySystem.Instance.Initialize();
    TimeEnergySystem.Instance.Initialize();
    EconomySystem.Instance.Initialize();

    // Phase 2: NPC Systems
    Debug.Log("Phase 2: NPC Systems");
    ReputationSystem.Instance.Initialize();
    RelationshipSystem.Instance.Initialize();
    DetectionSystem.Instance.Initialize();

    // Phase 3: Activity Systems
    Debug.Log("Phase 3: Activity Systems");
    SkillSystem.Instance.Initialize();
    JobSystem.Instance.Initialize();
    ActivitySystem.Instance.Initialize();
    MinigameSystem.Instance.Initialize();

    // Phase 4: Content Systems
    Debug.Log("Phase 4: Content Systems");
    LocationSystem.Instance.Initialize();
    InventorySystem.Instance.Initialize();
    EventSystem.Instance.Initialize();
    HeatSystem.Instance.Initialize();
    IntoxicationSystem.Instance.Initialize();
    BodySystem.Instance.Initialize();
    AdultContentSystem.Instance.Initialize();

    // Phase 5: Presentation Layer
    Debug.Log("Phase 5: Presentation Layer");
    InputManager.Instance.Initialize();
    
    // PlayerController and CameraController might not exist yet (scene-dependent)
    if (PlayerController.Instance != null) {
        PlayerController.Instance.Initialize("player");
    }
    
    if (CameraController.Instance != null) {
        CameraController.Instance.Initialize();
    }

    Debug.Log("=== ALL SYSTEMS INITIALIZED ===");
}

private void InitializeUI() {
    Debug.Log("=== INITIALIZING UI ===");

    // HUD is always present
    if (HUDController.Instance != null) {
        HUDController.Instance.Initialize();
    }

    // PhoneUI and MinigameUI might be scene-dependent
    if (PhoneUI.Instance != null) {
        PhoneUI.Instance.Initialize();
    }

    if (MinigameUI.Instance != null) {
        MinigameUI.Instance.Initialize();
    }

    Debug.Log("=== UI INITIALIZED ===");
}
```

### **Scene Management**

```csharp
public void LoadScene(string sceneName, bool unloadCurrent = true) {
    if (state.currentState == GameState.Loading) {
        Debug.LogWarning("Scene load already in progress");
        return;
    }

    StartCoroutine(LoadSceneCoroutine(sceneName, unloadCurrent));
}

private IEnumerator LoadSceneCoroutine(string sceneName, bool unloadCurrent) {
    SetGameState(GameState.Loading);
    OnSceneLoadStarted?.Invoke(sceneName);

    // Unload previous scene if requested
    if (unloadCurrent && !string.IsNullOrEmpty(state.currentScene)) {
        yield return UnloadSceneCoroutine(state.currentScene);
    }

    // Load new scene additively
    AsyncOperation loadOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
    
    while (!loadOp.isDone) {
        yield return null;
    }

    // Set as active scene
    Scene scene = SceneManager.GetSceneByName(sceneName);
    if (scene.isLoaded) {
        SceneManager.SetActiveScene(scene);
        
        state.previousScene = state.currentScene;
        state.currentScene = sceneName;
        state.loadedScenes.Add(sceneName);

        // Initialize scene-specific components
        InitializeSceneComponents();

        // Update LocationSystem
        LocationSystem.Instance.OnSceneLoaded(sceneName);

        OnSceneLoadCompleted?.Invoke(sceneName);
        SetGameState(GameState.Playing);
    } else {
        Debug.LogError($"Failed to load scene: {sceneName}");
        SetGameState(GameState.MainMenu);
    }
}

private IEnumerator UnloadSceneCoroutine(string sceneName) {
    if (!state.loadedScenes.Contains(sceneName)) {
        Debug.LogWarning($"Scene {sceneName} not loaded, cannot unload");
        yield break;
    }

    AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(sceneName);
    
    while (!unloadOp.isDone) {
        yield return null;
    }

    state.loadedScenes.Remove(sceneName);
    OnSceneUnloaded?.Invoke(sceneName);
}

private void InitializeSceneComponents() {
    // Find and initialize PlayerController if present in scene
    var playerController = FindObjectOfType<PlayerController>();
    if (playerController != null && PlayerController.Instance == null) {
        playerController.Initialize("player");
    }

    // Set camera target if player exists
    if (PlayerController.Instance != null && CameraController.Instance != null) {
        CameraController.Instance.SetTarget(PlayerController.Instance.GetTransform());
    }
}
```

### **Pause/Resume**

```csharp
public void PauseGame() {
    if (state.isPaused) return;

    state.isPaused = true;
    Time.timeScale = 0f;

    // Change input context
    InputManager.Instance.PushContext(InputContext.UI);

    // Show pause menu (if exists)
    // TODO: PauseMenu.Instance.Show();

    OnGamePaused?.Invoke();
}

public void ResumeGame() {
    if (!state.isPaused) return;

    state.isPaused = false;
    Time.timeScale = 1f;

    // Restore input context
    InputManager.Instance.PopContext();

    // Hide pause menu
    // TODO: PauseMenu.Instance.Hide();

    OnGameResumed?.Invoke();
}
```

### **Save/Load System**

```csharp
public void SaveGame(string saveName) {
    SetGameState(GameState.Saving);
    OnSaveStarted?.Invoke(saveName);

    try {
        GameSaveData saveData = new GameSaveData {
            version = "1.0.0",
            saveDate = System.DateTime.Now,
            playTime = GetPlayTime(),
            currentScene = state.currentScene,

            // Collect data from all systems
            entityData = EntitySystem.Instance.GetSaveData(),
            timeData = TimeEnergySystem.Instance.GetSaveData(),
            economyData = EconomySystem.Instance.GetSaveData(),
            reputationData = ReputationSystem.Instance.GetSaveData(),
            relationshipData = RelationshipSystem.Instance.GetSaveData(),
            detectionData = DetectionSystem.Instance.GetSaveData(),
            jobData = JobSystem.Instance.GetSaveData(),
            skillData = SkillSystem.Instance.GetSaveData(),
            heatData = HeatSystem.Instance.GetSaveData(),
            locationData = LocationSystem.Instance.GetSaveData(),
            intoxicationData = IntoxicationSystem.Instance.GetSaveData(),
            adultContentData = AdultContentSystem.Instance.GetSaveData(),
            bodyData = BodySystem.Instance.GetSaveData(),
            eventData = EventSystem.Instance.GetSaveData(),
            inventoryData = InventorySystem.Instance.GetSaveData(),
            activityData = ActivitySystem.Instance.GetSaveData(),
            minigameData = MinigameSystem.Instance.GetSaveData(),

            playerData = PlayerController.Instance.GetSaveData(),
            cameraData = CameraController.Instance.GetSaveData()
        };

        // Serialize to JSON
        string json = JsonUtility.ToJson(saveData, true);

        // Write to file
        string savePath = GetSavePath(saveName);
        File.WriteAllText(savePath, json);

        Debug.Log($"Game saved to: {savePath}");
        OnSaveCompleted?.Invoke(saveName);

    } catch (System.Exception e) {
        Debug.LogError($"Save failed: {e.Message}");
    }

    SetGameState(GameState.Playing);
}

public void LoadGame(string saveName) {
    SetGameState(GameState.Loading);
    OnLoadStarted?.Invoke(saveName);

    try {
        string savePath = GetSavePath(saveName);
        
        if (!File.Exists(savePath)) {
            Debug.LogError($"Save file not found: {savePath}");
            SetGameState(GameState.MainMenu);
            return;
        }

        // Read file
        string json = File.ReadAllText(savePath);
        GameSaveData saveData = JsonUtility.FromJson<GameSaveData>(json);

        // Load into all systems
        EntitySystem.Instance.LoadSaveData(saveData.entityData);
        TimeEnergySystem.Instance.LoadSaveData(saveData.timeData);
        EconomySystem.Instance.LoadSaveData(saveData.economyData);
        ReputationSystem.Instance.LoadSaveData(saveData.reputationData);
        RelationshipSystem.Instance.LoadSaveData(saveData.relationshipData);
        DetectionSystem.Instance.LoadSaveData(saveData.detectionData);
        JobSystem.Instance.LoadSaveData(saveData.jobData);
        SkillSystem.Instance.LoadSaveData(saveData.skillData);
        HeatSystem.Instance.LoadSaveData(saveData.heatData);
        LocationSystem.Instance.LoadSaveData(saveData.locationData);
        IntoxicationSystem.Instance.LoadSaveData(saveData.intoxicationData);
        AdultContentSystem.Instance.LoadSaveData(saveData.adultContentData);
        BodySystem.Instance.LoadSaveData(saveData.bodyData);
        EventSystem.Instance.LoadSaveData(saveData.eventData);
        InventorySystem.Instance.LoadSaveData(saveData.inventoryData);
        ActivitySystem.Instance.LoadSaveData(saveData.activityData);
        MinigameSystem.Instance.LoadSaveData(saveData.minigameData);

        PlayerController.Instance.LoadSaveData(saveData.playerData);
        CameraController.Instance.LoadSaveData(saveData.cameraData);

        // Load scene
        state.totalPlayTime = saveData.playTime;
        LoadScene(saveData.currentScene, unloadCurrent: true);

        Debug.Log($"Game loaded from: {savePath}");
        OnLoadCompleted?.Invoke(saveName);

    } catch (System.Exception e) {
        Debug.LogError($"Load failed: {e.Message}");
        SetGameState(GameState.MainMenu);
    }
}

private string GetSavePath(string saveName) {
    string saveDir = Path.Combine(Application.persistentDataPath, "Saves");
    
    if (!Directory.Exists(saveDir)) {
        Directory.CreateDirectory(saveDir);
    }

    return Path.Combine(saveDir, $"{saveName}.json");
}

public bool SaveExists(string saveName) {
    return File.Exists(GetSavePath(saveName));
}

public List<string> GetSaveFiles() {
    string saveDir = Path.Combine(Application.persistentDataPath, "Saves");
    
    if (!Directory.Exists(saveDir)) {
        return new List<string>();
    }

    string[] files = Directory.GetFiles(saveDir, "*.json");
    List<string> saveNames = new List<string>();

    foreach (string file in files) {
        string fileName = Path.GetFileNameWithoutExtension(file);
        saveNames.Add(fileName);
    }

    return saveNames;
}
```

### **Time Tracking**

```csharp
private void Update() {
    if (state.currentState == GameState.Playing && !state.isPaused) {
        state.totalPlayTime += Time.deltaTime;
    }
}

public float GetPlayTime() {
    return state.totalPlayTime;
}
```

---

## **VI. INTEGRATION TESTS (POST-IMPLEMENTATION)**

### **Test Scenarios**

**Test 1: System Initialization Order**
```
1. GameManager.Initialize() called
2. EntitySystem initialized first ???
3. TimeEnergySystem initialized second ???
4. All 17 systems initialized in dependency order ???
5. UI components initialized last ???
6. OnAllSystemsInitialized event fires ???
```

**Test 2: Scene Loading**
```
1. GameManager.LoadScene("Apartment")
2. Current scene unloaded
3. Apartment scene loaded additively
4. PlayerController initialized in new scene
5. CameraController target set to player
6. LocationSystem.OnSceneLoaded called ???
```

**Test 3: Save/Load**
```
1. Player at position (5, 0, 3) with $500
2. GameManager.SaveGame("test_save")
3. All systems provide save data
4. JSON written to disk
5. GameManager.LoadGame("test_save")
6. Player restored to (5, 0, 3) with $500 ???
```

---

## **VII. TESTING CHECKLIST**

### **Unit Tests (30 minimum)**

**Initialization Tests (8):**
- [ ] Initialize creates GameManagerState
- [ ] InitializeAllSystems called in correct order
- [ ] All 17 systems initialized
- [ ] All UI components initialized
- [ ] OnAllSystemsInitialized event fires
- [ ] Double initialization prevented
- [ ] DontDestroyOnLoad applied
- [ ] Singleton enforced

**Scene Management Tests (8):**
- [ ] LoadScene starts load coroutine
- [ ] Scene loaded additively
- [ ] Previous scene unloaded
- [ ] Current scene tracked correctly
- [ ] OnSceneLoadStarted event fires
- [ ] OnSceneLoadCompleted event fires
- [ ] InitializeSceneComponents called
- [ ] LocationSystem notified

**Save/Load Tests (8):**
- [ ] SaveGame creates JSON file
- [ ] SaveGame collects all system data
- [ ] LoadGame reads JSON file
- [ ] LoadGame restores all system data
- [ ] SaveExists returns true for existing save
- [ ] GetSaveFiles returns all saves
- [ ] DeleteSave removes file
- [ ] Save/Load events fire

**Pause/Resume Tests (3):**
- [ ] PauseGame sets Time.timeScale = 0
- [ ] ResumeGame sets Time.timeScale = 1
- [ ] Pause/Resume events fire

**State Tests (3):**
- [ ] GetGameState returns current state
- [ ] SetGameState changes state
- [ ] State change fires event

---

## **VIII. KNOWN LIMITATIONS**

- No autosave
- No multiple save slots UI (just raw save names)
- No save corruption detection
- No cloud saves
- Scene loading has no progress bar
- No "new game" vs "continue" menu integration

---

## **IX. FUTURE ENHANCEMENTS (Post-V1.0)**

- Autosave every X minutes
- Save slot management UI
- Cloud save integration (Steam, etc.)
- Scene loading progress bar
- Save file versioning/migration
- Corruption detection and recovery

---

**END OF SPECIFICATION**

