# V2 Update (Aligned to Current Codebase)

This V2 spec reflects the current implementation. See docs/IMPLEMENTATION_DISCREPANCIES.md for cross-system gaps.

## Implementation Notes

- Minigame, camera, and detection wiring are implemented.
- Work earnings use JobSystem.GetCurrentJob when available; fallback is 20/hr.
- Skill mapping is heuristic by minigameId.

---
16. ACTIVITY SYSTEM
Purpose
Manages all player activities (work shifts, streaming, drug dealing, driving, etc.). Tracks what player is doing, handles multitasking rules, manages minigames, and coordinates with other systems. This is the core of the "juggling multiple things" gameplay loop.

Interface
CreateActivity
csharp
string CreateActivity(ActivityType type, string minigameId, float durationHours)
Purpose: Start a new activity.

Parameters:

type: Physical, Screen, or Passive
minigameId: Which minigame to run
durationHours: How long activity lasts
Returns: Activity ID

Side effects:

Creates activity instance
Starts minigame
Broadcasts OnActivityStarted event
GetActiveActivities
csharp
List<Activity> GetActiveActivities(string playerId)
Purpose: Get all activities player is currently doing.

Parameters:

playerId: Player ID
Returns: List of active activities

GetActivity
csharp
Activity GetActivity(string activityId)
Purpose: Get specific activity.

Parameters:

activityId: Activity ID
Returns: Activity instance

PauseActivity
csharp
void PauseActivity(string activityId)
Purpose: Temporarily pause activity.

Parameters:

activityId: Which activity
Side effects:

Activity state ??? Paused
Minigame freezes
Broadcasts OnActivityPaused event
ResumeActivity
csharp
void ResumeActivity(string activityId)
Purpose: Resume paused activity.

Parameters:

activityId: Which activity
Side effects:

Activity state ??? Running
Minigame resumes
Broadcasts OnActivityResumed event
EndActivity
csharp
ActivityResult EndActivity(string activityId)
Purpose: Complete or cancel activity.

Parameters:

activityId: Which activity
Returns: ActivityResult (performance score, rewards, etc.)

Side effects:

Activity state ??? Completed
Minigame ends
Calculates rewards
Broadcasts OnActivityEnded event
CanMultitask
csharp
bool CanMultitask(string activityId1, string activityId2)
Purpose: Check if two activities can run simultaneously.

Parameters:

activityId1: First activity
activityId2: Second activity
Returns: true if compatible

Logic:

Physical + Screen = YES (work + phone)
Physical + Physical = NO (can't mop and drive)
Screen + Screen = NO (can't use two computers)
Attention budget check (total attention ??? 1.0)
Events
csharp
event Action<string> OnActivityStarted         // (activityId)
event Action<string> OnActivityPaused          // (activityId)
event Action<string> OnActivityResumed         // (activityId)
event Action<ActivityResult> OnActivityEnded
event Action<string, string> OnMultitaskAttempt  // (activityId1, activityId2) - may fail
Data Structures
Activity
csharp
class Activity {
    string id;
    ActivityType type;                 // Physical, Screen, Passive
    MultitaskingLevel multitaskingAllowed;
    
    string minigameId;
    float requiredAttention;           // 0-1 scale
    float durationHours;
    
    ActivityState state;               // Active, Running, Paused, Failed, Completed
    DateTime startTime;
    float elapsedTime;
    
    // Performance
    float performanceScore;            // 0-100
    bool detectedSlacking;
    
    // Multitasking
    List<string> concurrentActivities; // Other activities running alongside
    
    // Downtime phases (streaming, writing)
    bool hasDowntimePhases;
    List<ActivityPhase> phases;
    int currentPhaseIndex;
}
ActivityType (enum)
csharp
enum ActivityType {
    Physical,    // In 3D space, can multitask with devices
    Screen,      // Inside screen/interface, limited multitasking
    Passive      // Runs in background, minimal attention
}
MultitaskingLevel (enum)
csharp
enum MultitaskingLevel {
    Full,        // Can do anything else (dog walking)
    Partial,     // Can check phone briefly (office work)
    Breaks,      // Can multitask during scheduled breaks (streaming)
    None         // Cannot multitask (active streaming performance)
}
ActivityState (enum)
csharp
enum ActivityState {
    Active,      // Player directly controlling
    Running,     // Background, needs periodic attention
    Paused,      // Frozen
    Failed,      // Missed critical event
    Completed    // Successfully finished
}
ActivityPhase (for complex activities like streaming)
csharp
struct ActivityPhase {
    string name;                  // "setup", "active", "break"
    float durationMinutes;
    bool canMultitask;
    float attentionRequired;
}
ActivityResult
csharp
struct ActivityResult {
    string activityId;
    float performanceScore;       // 0-100
    float timeSpent;
    bool completed;               // vs failed/cancelled
    Dictionary<string, float> rewards;  // "money": 150, "skill_xp": 20
}
Dependencies
Reads from:

TimeSystem (activity duration, elapsed time)
DetectionSystem (slacking detected during work activities)
SkillSystem (skill level affects performance)
Writes to:

TimeSystem (activities consume time)
SkillSystem (activities improve skills)
JobSystem (work activities generate pay)
Subscribed to by:

UI (activity display, multitasking indicators)
MinigameSystem (runs minigames for activities)
CameraController (switches camera mode based on activity type)
Implementation Notes
Activity Creation:

csharp
string CreateActivity(ActivityType type, string minigameId, float durationHours) {
    Activity activity = new Activity {
        id = GenerateActivityId(),
        type = type,
        minigameId = minigameId,
        requiredAttention = GetAttentionRequirement(minigameId),
        durationHours = durationHours,
        state = ActivityState.Active,
        startTime = TimeSystem.Instance.GetCurrentTime(),
        elapsedTime = 0f,
        performanceScore = 50f,
        detectedSlacking = false,
        concurrentActivities = new List<string>()
    };
    
    // Check multitasking compatibility with existing activities
    List<Activity> activeActivities = GetActiveActivities(playerId);
    foreach (var existing in activeActivities) {
        if (!CanMultitask(activity.id, existing.id)) {
            // Must pause one activity to start this one
            PauseActivity(existing.id);
        } else {
            // Track concurrent activities
            activity.concurrentActivities.Add(existing.id);
            existing.concurrentActivities.Add(activity.id);
        }
    }
    
    // Start minigame
    MinigameSystem.Instance.StartMinigame(minigameId, activity.id);
    
    // Switch camera if needed
    if (type == ActivityType.Screen) {
        CameraController.Instance.SetMode(CameraMode.FirstPerson);
    }
    
    activeActivities.Add(activity);
    
    OnActivityStarted?.Invoke(activity.id);
    
    return activity.id;
}
Multitasking Logic:

csharp
bool CanMultitask(string activityId1, string activityId2) {
    Activity act1 = GetActivity(activityId1);
    Activity act2 = GetActivity(activityId2);
    
    // Two physical activities = NO
    if (act1.type == ActivityType.Physical && act2.type == ActivityType.Physical) {
        return false;
    }
    
    // Two screen activities = NO
    if (act1.type == ActivityType.Screen && act2.type == ActivityType.Screen) {
        return false;
    }
    
    // Check attention budget
    float totalAttention = act1.requiredAttention + act2.requiredAttention;
    if (totalAttention > 1.0f) {
        return false;  // Too much attention required
    }
    
    // Check multitasking levels
    if (act1.multitaskingAllowed == MultitaskingLevel.None || 
        act2.multitaskingAllowed == MultitaskingLevel.None) {
        return false;
    }
    
    return true;
}
Activity Update Loop:

csharp
void Update() {
    float deltaTime = Time.deltaTime;
    
    foreach (var activity in activeActivities.ToList()) {
        if (activity.state != ActivityState.Running && activity.state != ActivityState.Active) {
            continue;
        }
        
        // Track elapsed time
        activity.elapsedTime += deltaTime;
        
        // Check if duration exceeded
        float durationSeconds = activity.durationHours * 3600f;
        if (activity.elapsedTime >= durationSeconds) {
            // Activity complete
            EndActivity(activity.id);
            continue;
        }
        
        // Update minigame performance
        float minigamePerformance = MinigameSystem.Instance.GetPerformance(activity.minigameId);
        activity.performanceScore = (activity.performanceScore * 0.9f) + (minigamePerformance * 0.1f);
        
        // Check for detection (if work activity)
        if (activity.minigameId.Contains("work")) {
            DetectionResult detection = DetectionSystem.Instance.CheckDetection(playerId, activity.id);
            if (detection.detected) {
                activity.detectedSlacking = true;
            }
        }
        
        // Handle phases (streaming, complex activities)
        if (activity.hasDowntimePhases) {
            UpdateActivityPhases(activity);
        }
    }
}
Phase System (Streaming):

csharp
void UpdateActivityPhases(Activity activity) {
    if (activity.phases == null || activity.phases.Count == 0) return;
    
    ActivityPhase currentPhase = activity.phases[activity.currentPhaseIndex];
    
    // Check if phase complete
    float phaseElapsed = activity.elapsedTime - GetPhaseStartTime(activity);
    if (phaseElapsed >= currentPhase.durationMinutes * 60f) {
        // Move to next phase
        activity.currentPhaseIndex = (activity.currentPhaseIndex + 1) % activity.phases.Count;
        
        ActivityPhase nextPhase = activity.phases[activity.currentPhaseIndex];
        
        // Update multitasking based on phase
        activity.multitaskingAllowed = nextPhase.canMultitask 
            ? MultitaskingLevel.Breaks 
            : MultitaskingLevel.None;
        
        activity.requiredAttention = nextPhase.attentionRequired;
    }
}
Ending Activity:

csharp
ActivityResult EndActivity(string activityId) {
    Activity activity = GetActivity(activityId);
    
    activity.state = ActivityState.Completed;
    
    // Calculate rewards
    Dictionary<string, float> rewards = new Dictionary<string, float>();
    
    // Money (if work activity)
    if (activity.minigameId.Contains("work")) {
        Job job = JobSystem.Instance.GetCurrentJob(playerId);
        float earnings = job.hourlyWage * activity.durationHours;
        rewards["money"] = earnings;
    }
    
    // Skill XP
    SkillType skill = GetSkillForActivity(activity.minigameId);
    float skillGain = activity.performanceScore * 0.1f;
    rewards["skill_xp"] = skillGain;
    SkillSystem.Instance.ImproveSkillFromUse(skill, skillGain);
    
    // End minigame
    MinigameSystem.Instance.EndMinigame(activity.minigameId);
    
    // Remove from active
    activeActivities.Remove(activity);
    
    ActivityResult result = new ActivityResult {
        activityId = activityId,
        performanceScore = activity.performanceScore,
        timeSpent = activity.elapsedTime / 3600f,  // Convert to hours
        completed = true,
        rewards = rewards
    };
    
    OnActivityEnded?.Invoke(result);
    
    return result;
}
```

---

## **Edge Cases**

1. **Start activity while at attention limit:** Pause least important activity
2. **Activity interrupted (arrested, evicted):** All activities auto-fail
3. **Multitask two high-attention activities:** Both performance scores drop
4. **Activity duration = 0:** Instant complete (one-time actions)
5. **Pause activity during "no multitask" phase:** Not allowed, must complete phase

---

## **Testing Checklist**
```
[ ] CreateActivity starts minigame
[ ] CreateActivity switches camera for Screen activities
[ ] Can't multitask two Physical activities
[ ] Can't multitask two Screen activities
[ ] Can multitask Physical + Screen if attention allows
[ ] PauseActivity freezes minigame
[ ] ResumeActivity unfreezes minigame
[ ] EndActivity calculates rewards correctly
[ ] Activity auto-completes when duration exceeded
[ ] Detection during work activity sets detectedSlacking flag
[ ] Phase system transitions correctly (streaming)
[ ] Multitasking level changes between phases
[ ] OnActivityEnded fires with correct results
[ ] Skill XP awarded based on performance
