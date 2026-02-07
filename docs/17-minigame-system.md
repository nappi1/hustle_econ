# MINIGAME SYSTEM SPECIFICATION

## Purpose
Provides interactive gameplay mechanics for activities (work shifts, streaming, etc.). Manages minigame instances, tracks performance, handles input, and communicates scores to ActivitySystem. Supports multiple concurrent minigames with independent state.

---

## Interface

### StartMinigame
```csharp
void StartMinigame(string minigameId, string activityId)
```
**Purpose:** Launch a minigame instance.

**Parameters:**
- `minigameId`: Minigame identifier (e.g., "clicktargets_janitor_job_1")
- `activityId`: Associated activity ID for cross-reference

**Side effects:**
- Creates minigame instance
- Initializes UI elements
- Starts input tracking
- Sets initial performance score (50.0)
- Broadcasts `OnMinigameStarted` event

---

### PauseMinigame
```csharp
void PauseMinigame(string minigameId)
```
**Purpose:** Temporarily freeze minigame state.

**Parameters:**
- `minigameId`: Which minigame to pause

**Side effects:**
- Minigame state → Paused
- Input ignored
- Timers frozen
- UI shows paused state
- Broadcasts `OnMinigamePaused` event

---

### ResumeMinigame
```csharp
void ResumeMinigame(string minigameId)
```
**Purpose:** Resume paused minigame.

**Parameters:**
- `minigameId`: Which minigame to resume

**Side effects:**
- Minigame state → Running
- Input tracking resumes
- Timers continue
- UI shows active state
- Broadcasts `OnMinigameResumed` event

---

### EndMinigame
```csharp
MinigameResult EndMinigame(string minigameId)
```
**Purpose:** Complete minigame and return final results.

**Parameters:**
- `minigameId`: Which minigame to end

**Returns:** `MinigameResult` containing final score and statistics

**Side effects:**
- Minigame state → Completed
- Cleans up UI elements
- Removes from active minigames
- Broadcasts `OnMinigameEnded` event

---

### GetPerformance
```csharp
float GetPerformance(string minigameId)
```
**Purpose:** Get current performance score (polled by ActivitySystem every frame).

**Parameters:**
- `minigameId`: Which minigame to query

**Returns:** Performance score from 0-100
- 0-30: Poor performance
- 30-50: Below average
- 50-70: Average
- 70-85: Good
- 85-100: Excellent

**Note:** This is called frequently (every Update), must be fast (O(1) lookup).

---

### IsMinigameActive
```csharp
bool IsMinigameActive(string minigameId)
```
**Purpose:** Check if minigame is currently running.

**Parameters:**
- `minigameId`: Minigame to check

**Returns:** `true` if active, `false` if paused/completed/nonexistent

---

### SetDifficulty
```csharp
void SetDifficulty(string minigameId, float difficulty)
```
**Purpose:** Adjust minigame difficulty (based on player skill level).

**Parameters:**
- `minigameId`: Which minigame
- `difficulty`: Difficulty multiplier (0.5 = easier, 1.0 = normal, 1.5 = harder)

**Side effects:**
- Adjusts spawn rates, timing windows, or other difficulty parameters
- Does not reset current progress

---

## Events

```csharp
event Action<string> OnMinigameStarted;           // (minigameId)
event Action<string> OnMinigamePaused;            // (minigameId)
event Action<string> OnMinigameResumed;           // (minigameId)
event Action<MinigameResult> OnMinigameEnded;
event Action<string, float> OnPerformanceChanged; // (minigameId, newScore) - fires when score changes significantly (>5 points)
```

---

## Data Structures

### MinigameInstance
```csharp
[System.Serializable]
public class MinigameInstance {
    public string minigameId;
    public string activityId;
    public MinigameType type;
    public MinigameState state;
    
    public float currentPerformance;    // 0-100, updated continuously
    public float difficulty;            // Multiplier (0.5-2.0)
    
    public DateTime startTime;
    public float elapsedTime;
    
    // Type-specific behavior
    public Minigame behavior;           // Polymorphic game logic
    
    // Statistics
    public int successfulActions;
    public int failedActions;
    public int totalActions;
}
```

### MinigameType (enum)
```csharp
public enum MinigameType {
    ClickTargets,       // Mopping, stocking - click moving targets
    SequenceMatch,      // Assembly line - repeat pattern
    TimingGame,         // Restaurant orders - hit timing windows
    EmailManagement,    // Office work - sort/respond to emails
    Driving,            // Gig work - steering/navigation
    Streaming,          // Creative work - maintain chat engagement
    Coding              // Programming - syntax matching
}
```

### MinigameState (enum)
```csharp
public enum MinigameState {
    Running,    // Active, accepting input
    Paused,     // Frozen, input ignored
    Completed,  // Finished, waiting for cleanup
    Failed      // Terminated early (rare)
}
```

### MinigameResult
```csharp
[System.Serializable]
public struct MinigameResult {
    public string minigameId;
    public float finalPerformance;      // 0-100
    public float accuracy;              // successfulActions / totalActions
    public int successfulActions;
    public int failedActions;
    public float timeElapsed;           // Seconds
    public bool completedSuccessfully;  // vs failed/cancelled
}
```

### Minigame (Abstract Base Class)
```csharp
public abstract class Minigame {
    protected MinigameInstance instance;
    
    public abstract void Initialize();
    public abstract void UpdateLogic(float deltaTime);
    public abstract void HandleInput();
    public abstract float CalculatePerformance();
    public abstract void Cleanup();
    
    // Helper for performance calculation
    protected float CalculateAccuracyScore() {
        int total = instance.successfulActions + instance.failedActions;
        if (total == 0) return 50f;  // Neutral at start
        return (instance.successfulActions / (float)total) * 100f;
    }
}
```

---

## Dependencies

**Reads from:**
- None (self-contained)

**Writes to:**
- None directly (broadcasts events)

**Subscribed to by:**
- ActivitySystem (polls `GetPerformance()` every frame)
- UI (displays minigame elements)
- SkillSystem (difficulty scales with skill level - future)

---

## Implementation Notes

### Singleton Pattern
```csharp
public class MinigameSystem : MonoBehaviour {
    public static MinigameSystem Instance { get; private set; }
    
    private Dictionary<string, MinigameInstance> activeMinigames 
        = new Dictionary<string, MinigameInstance>();
    
    void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
}
```

### Starting a Minigame
```csharp
public void StartMinigame(string minigameId, string activityId) {
    if (activeMinigames.ContainsKey(minigameId)) {
        Debug.LogWarning($"Minigame {minigameId} already active");
        return;
    }
    
    // Parse type from minigameId (format: "{type}_{jobId}")
    MinigameType type = ParseMinigameType(minigameId);
    
    MinigameInstance minigame = new MinigameInstance {
        minigameId = minigameId,
        activityId = activityId,
        type = type,
        state = MinigameState.Running,
        currentPerformance = 50f,  // Start neutral
        difficulty = 1.0f,
        startTime = DateTime.Now,
        elapsedTime = 0f,
        successfulActions = 0,
        failedActions = 0,
        totalActions = 0
    };
    
    // Create type-specific behavior
    minigame.behavior = CreateMinigameBehavior(type, minigame);
    minigame.behavior.Initialize();
    
    activeMinigames[minigameId] = minigame;
    
    OnMinigameStarted?.Invoke(minigameId);
}

private MinigameType ParseMinigameType(string minigameId) {
    // Format: "clicktargets_janitor_job_1" or "emailmanagement_office_job_2"
    string typeStr = minigameId.Split('_')[0];
    
    if (Enum.TryParse<MinigameType>(typeStr, true, out MinigameType type)) {
        return type;
    }
    
    Debug.LogWarning($"Unknown minigame type in ID: {minigameId}, defaulting to ClickTargets");
    return MinigameType.ClickTargets;
}

private Minigame CreateMinigameBehavior(MinigameType type, MinigameInstance instance) {
    return type switch {
        MinigameType.ClickTargets => new ClickTargetsMinigame(instance),
        MinigameType.SequenceMatch => new SequenceMatchMinigame(instance),
        MinigameType.TimingGame => new TimingGameMinigame(instance),
        MinigameType.EmailManagement => new EmailManigame(instance),
        MinigameType.Driving => new DrivingMinigame(instance),
        MinigameType.Streaming => new StreamingMinigame(instance),
        MinigameType.Coding => new CodingMinigame(instance),
        _ => new StubMinigame(instance)  // Fallback for unimplemented types
    };
}
```

### Update Loop
```csharp
void Update() {
    float deltaTime = Time.deltaTime;
    
    foreach (var minigame in activeMinigames.Values.ToList()) {
        if (minigame.state != MinigameState.Running) continue;
        
        minigame.elapsedTime += deltaTime;
        
        // Update minigame logic
        minigame.behavior.HandleInput();
        minigame.behavior.UpdateLogic(deltaTime);
        
        // Calculate new performance
        float oldPerformance = minigame.currentPerformance;
        minigame.currentPerformance = minigame.behavior.CalculatePerformance();
        
        // Fire event if significant change (>5 points)
        if (Mathf.Abs(minigame.currentPerformance - oldPerformance) > 5f) {
            OnPerformanceChanged?.Invoke(minigame.minigameId, minigame.currentPerformance);
        }
    }
}
```

### Getting Performance (Called Every Frame by ActivitySystem)
```csharp
public float GetPerformance(string minigameId) {
    if (!activeMinigames.ContainsKey(minigameId)) {
        Debug.LogWarning($"GetPerformance: Minigame {minigameId} not found");
        return 50f;  // Neutral score if not found
    }
    
    return activeMinigames[minigameId].currentPerformance;
}
```

### Ending a Minigame
```csharp
public MinigameResult EndMinigame(string minigameId) {
    if (!activeMinigames.ContainsKey(minigameId)) {
        Debug.LogWarning($"EndMinigame: Minigame {minigameId} not found");
        return new MinigameResult { completedSuccessfully = false };
    }
    
    MinigameInstance minigame = activeMinigames[minigameId];
    minigame.state = MinigameState.Completed;
    
    // Cleanup behavior
    minigame.behavior.Cleanup();
    
    // Create result
    MinigameResult result = new MinigameResult {
        minigameId = minigameId,
        finalPerformance = minigame.currentPerformance,
        accuracy = (float)minigame.successfulActions / 
                   Mathf.Max(1, minigame.successfulActions + minigame.failedActions),
        successfulActions = minigame.successfulActions,
        failedActions = minigame.failedActions,
        timeElapsed = minigame.elapsedTime,
        completedSuccessfully = true
    };
    
    activeMinigames.Remove(minigameId);
    
    OnMinigameEnded?.Invoke(result);
    
    return result;
}
```

---

## Minigame Implementations

### 1. ClickTargets Minigame (PRIORITY - Implement First)

**Use Case:** Janitorial work (mopping), retail (stocking shelves)

**Gameplay:**
- Targets spawn at random positions on screen
- Player must click targets before they expire
- Successful click = +1 successful action, score increases
- Miss/timeout = +1 failed action, score decreases

**Implementation:**
```csharp
public class ClickTargetsMinigame : Minigame {
    private List<Target> activeTargets = new List<Target>();
    private float timeSinceLastSpawn = 0f;
    private float spawnInterval = 2f;  // Spawn new target every 2 seconds
    
    public override void Initialize() {
        // Spawn first target immediately
        SpawnTarget();
    }
    
    public override void UpdateLogic(float deltaTime) {
        timeSinceLastSpawn += deltaTime;
        
        // Spawn new targets periodically
        if (timeSinceLastSpawn >= spawnInterval) {
            SpawnTarget();
            timeSinceLastSpawn = 0f;
        }
        
        // Update existing targets
        for (int i = activeTargets.Count - 1; i >= 0; i--) {
            Target target = activeTargets[i];
            target.lifetime -= deltaTime;
            
            // Target expired
            if (target.lifetime <= 0) {
                instance.failedActions++;
                instance.totalActions++;
                activeTargets.RemoveAt(i);
                DestroyTargetUI(target);
            }
        }
        
        // Adjust spawn rate based on difficulty
        spawnInterval = 2f / instance.difficulty;  // Higher difficulty = faster spawns
    }
    
    public override void HandleInput() {
        if (Input.GetMouseButtonDown(0)) {
            Vector2 mousePos = Input.mousePosition;
            
            // Check if clicked on any target
            for (int i = 0; i < activeTargets.Count; i++) {
                Target target = activeTargets[i];
                if (IsClickOnTarget(mousePos, target)) {
                    instance.successfulActions++;
                    instance.totalActions++;
                    activeTargets.RemoveAt(i);
                    DestroyTargetUI(target);
                    
                    // Spawn new target immediately for rhythm
                    SpawnTarget();
                    break;
                }
            }
        }
    }
    
    public override float CalculatePerformance() {
        // Base on accuracy
        float accuracy = CalculateAccuracyScore();
        
        // Bonus for speed (more actions = better)
        float actionsPerMinute = instance.totalActions / (instance.elapsedTime / 60f);
        float speedBonus = Mathf.Min(20f, actionsPerMinute * 2f);  // Max +20 points
        
        return Mathf.Clamp(accuracy + speedBonus - 20f, 0f, 100f);
    }
    
    public override void Cleanup() {
        // Destroy all active target UI elements
        foreach (var target in activeTargets) {
            DestroyTargetUI(target);
        }
        activeTargets.Clear();
    }
    
    private void SpawnTarget() {
        Target target = new Target {
            position = GetRandomScreenPosition(),
            lifetime = 3f,  // 3 seconds to click
            uiElement = CreateTargetUI()
        };
        activeTargets.Add(target);
    }
    
    private Vector2 GetRandomScreenPosition() {
        // Random position within safe screen bounds (avoid edges)
        float x = Random.Range(Screen.width * 0.1f, Screen.width * 0.9f);
        float y = Random.Range(Screen.height * 0.1f, Screen.height * 0.9f);
        return new Vector2(x, y);
    }
    
    private GameObject CreateTargetUI() {
        // Create simple UI element (circle/square) at target position
        // TODO: Implement actual UI instantiation
        return null;  // Placeholder
    }
    
    private void DestroyTargetUI(Target target) {
        // TODO: Destroy UI element
        if (target.uiElement != null) {
            Destroy(target.uiElement);
        }
    }
    
    private bool IsClickOnTarget(Vector2 clickPos, Target target) {
        // Simple distance check (assumes circular targets with radius 50)
        float distance = Vector2.Distance(clickPos, target.position);
        return distance < 50f;
    }
    
    private class Target {
        public Vector2 position;
        public float lifetime;
        public GameObject uiElement;
    }
}
```

---

### 2. StubMinigame (For Unimplemented Types)

**Purpose:** Returns semi-random scores so other types don't crash the game.

```csharp
public class StubMinigame : Minigame {
    private float baseScore = 60f;
    private float variance = 0f;
    
    public override void Initialize() {
        // Random base score between 50-70
        baseScore = Random.Range(50f, 70f);
    }
    
    public override void UpdateLogic(float deltaTime) {
        // Slowly drift performance up and down
        variance += (Random.value - 0.5f) * deltaTime * 10f;
        variance = Mathf.Clamp(variance, -15f, 15f);
    }
    
    public override void HandleInput() {
        // No input handling
    }
    
    public override float CalculatePerformance() {
        return Mathf.Clamp(baseScore + variance, 30f, 80f);
    }
    
    public override void Cleanup() {
        // Nothing to clean up
    }
}
```

---

### 3. Future Minigame Types (Implement Later)

**SequenceMatch:** Display pattern (red, blue, red, green), player repeats it
**TimingGame:** Moving bar, hit spacebar when in green zone
**EmailManagement:** Sort emails into folders, respond to urgent ones
**Driving:** Steering controls, stay in lane, avoid obstacles
**Streaming:** Respond to chat messages, maintain engagement
**Coding:** Syntax highlighting, match code patterns

---

## Edge Cases

1. **Minigame started twice with same ID:** Log warning, ignore second start
2. **GetPerformance() called for nonexistent minigame:** Return 50 (neutral)
3. **EndMinigame() called for already-ended minigame:** Log warning, return failure result
4. **Pause while paused:** No-op, don't fire event again
5. **Difficulty set mid-game:** Adjusts immediately without resetting progress
6. **Multiple minigames active simultaneously:** Each maintains independent state
7. **Minigame ID format doesn't match expected pattern:** Parse gracefully, default to ClickTargets

---

## Testing Checklist

```
[ ] StartMinigame creates instance and fires event
[ ] GetPerformance returns score in 0-100 range
[ ] ClickTargets spawns targets at regular intervals
[ ] Clicking target increments successfulActions
[ ] Missing target increments failedActions
[ ] Performance score updates based on accuracy
[ ] PauseMinigame freezes input and timers
[ ] ResumeMinigame continues from paused state
[ ] EndMinigame returns correct MinigameResult
[ ] EndMinigame removes instance from active list
[ ] Multiple minigames can run concurrently
[ ] Difficulty adjustment affects spawn rate
[ ] Performance changes >5 points fire OnPerformanceChanged event
[ ] StubMinigame returns reasonable scores (50-80)
[ ] Nonexistent minigame queries handled gracefully
```

---

## Performance Considerations

**Critical:** `GetPerformance()` is called every frame by ActivitySystem.

- **Must be O(1):** Use Dictionary lookup, not List search
- **No allocation:** Don't create new objects in this method
- **Fast calculation:** Performance score should be cached, not recalculated every call

**Good:**
```csharp
public float GetPerformance(string minigameId) {
    return activeMinigames.ContainsKey(minigameId) 
        ? activeMinigames[minigameId].currentPerformance 
        : 50f;
}
```

**Bad (DO NOT DO):**
```csharp
public float GetPerformance(string minigameId) {
    // Don't search through list every frame!
    var minigame = activeMinigames.Values.FirstOrDefault(m => m.minigameId == minigameId);
    
    // Don't recalculate score every frame!
    return minigame?.behavior.CalculatePerformance() ?? 50f;
}
```

---

## Integration with Other Systems

### ActivitySystem Calls
```csharp
// When activity starts
MinigameSystem.Instance.StartMinigame(minigameId, activityId);

// Every frame during activity
float performance = MinigameSystem.Instance.GetPerformance(minigameId);

// When activity ends
MinigameResult result = MinigameSystem.Instance.EndMinigame(minigameId);
```

### JobSystem Integration (Indirect via ActivitySystem)
```csharp
// JobSystem.StartShift() creates activity with minigameId
string minigameId = GetMinigameId(job.minigameType, jobId);
ActivitySystem.Instance.CreateActivity(ActivityType.Physical, minigameId, 8f);

// ActivitySystem internally calls:
MinigameSystem.Instance.StartMinigame(minigameId, activityId);
```

---

## File Structure

```
Assets/
├── Scripts/
│   └── Core/
│       └── MinigameSystem.cs (singleton + core logic)
├── Scripts/
│   └── Minigames/
│       ├── Minigame.cs (abstract base class)
│       ├── ClickTargetsMinigame.cs
│       ├── StubMinigame.cs
│       └── [Future minigame implementations]
└── Tests/
    └── Core/
        └── MinigameSystemTests.cs
```

---

## Minimal Viable Implementation (8-12 hours)

**Must Have:**
1. MinigameSystem singleton with all interface methods
2. ClickTargetsMinigame fully functional
3. StubMinigame for other types
4. Basic UI for click targets (circles/squares)
5. Performance calculation working
6. Events firing correctly

**Can Defer:**
- Other minigame types (use stubs)
- Visual polish and animations
- Audio feedback
- Advanced difficulty scaling
- Performance statistics tracking
- UI themes per minigame type

---

## Success Criteria

**System passes when:**
- Can start minigame and get performance score
- ClickTargets is playable (spawn, click, score)
- Performance score updates continuously (0-100)
- Multiple minigames can run simultaneously
- Pause/Resume works correctly
- EndMinigame returns accurate results
- GetPerformance() is fast (<1ms per call)

**Integration passes when:**
- JobSystem → ActivitySystem → MinigameSystem chain works
- Work shift performance affects pay
- Getting caught while multitasking affects score
- Skill level can adjust difficulty (future)
