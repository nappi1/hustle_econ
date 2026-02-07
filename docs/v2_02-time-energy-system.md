# V2 Update (Aligned to Current Codebase)

This V2 spec reflects the current implementation. See docs/IMPLEMENTATION_DISCREPANCIES.md for cross-system gaps.

## Implementation Notes

- TimeEnergySystem implements GetCurrentGameTime() and GetGameDate() wrappers.
- ScheduleRecurringEvent/CancelRecurringEvent exist; per-player energy APIs do not.
- Energy max is fixed at 100 in UI; no GetMaxEnergy(playerId).

---
2. TIME & ENERGY SYSTEM
Purpose
Manages the game's time progression (accelerated 24-hour clock) and player energy (0-100 scale). Creates opportunity cost by making time finite and activities draining. Handles scheduling, energy regeneration, and time-based events.

Interface
GetCurrentTime
csharp
DateTime GetCurrentTime()
Purpose: Returns current in-game date/time.

Returns: DateTime object with current game time

AdvanceTime
csharp
void AdvanceTime(float gameMinutes)
Purpose: Manually advances game clock (used for time skips like jail, sleep).

Parameters:

gameMinutes: How many game-minutes to advance
Side effects:

Updates current time
Processes scheduled events in time range
Broadcasts OnTimeAdvanced event
Triggers energy decay if player awake
GetEnergyLevel
csharp
float GetEnergyLevel()
Purpose: Returns current player energy (0-100).

Returns: Energy value

ModifyEnergy
csharp
void ModifyEnergy(float delta, string reason)
Purpose: Changes player energy (drain from activity, restore from sleep/coffee).

Parameters:

delta: Amount to change (negative = drain, positive = restore)
reason: Why energy changed (for debugging/logging)
Side effects:

Clamps energy to 0-100 range
Broadcasts OnEnergyChanged event
If energy hits 0, broadcasts OnEnergyDepleted event
ScheduleEvent
csharp
string ScheduleEvent(DateTime when, Action callback, string description)
Purpose: Schedule something to happen at specific game time.

Parameters:

when: Game DateTime when to trigger
callback: Function to call
description: What this event is (for debugging)
Returns: Event ID (for cancellation)

Example:

csharp
ScheduleEvent(
    GetCurrentTime().AddHours(8),
    () => JobSystem.Instance.TriggerShiftEnd(playerId),
    "End of janitorial shift"
);
CancelScheduledEvent
csharp
bool CancelScheduledEvent(string eventId)
Purpose: Cancel a scheduled event before it fires.

Parameters:

eventId: ID returned from ScheduleEvent
Returns: true if cancelled, false if event doesn't exist

SetTimeScale
csharp
void SetTimeScale(float scale)
Purpose: Adjust how fast game time passes relative to real time.

Parameters:

scale: Multiplier (1.0 = 1 real second = 1 game minute, 2.0 = 1 real second = 2 game minutes)
Default: 1.0 (1 real minute = 1 game hour, so 24 real minutes = 1 game day)

Sleep
csharp
void Sleep(float hours)
Purpose: Player sleeps, time skips forward, energy restores.

Parameters:

hours: How long to sleep (game hours)
Side effects:

Advances time by hours
Restores energy based on sleep quality
Processes any scheduled events during sleep
Broadcasts OnSleep event
Energy restoration formula: min(100, currentEnergy + (hours * 12.5f)) // 8 hours of sleep = full restore

Events
csharp
event Action<DateTime> OnTimeAdvanced          // Fires every game-minute
event Action<float> OnEnergyChanged            // Fires when energy changes
event Action OnEnergyDepleted                  // Fires when energy hits 0
event Action<float> OnSleep                    // Fires when player sleeps (hours slept)
event Action<DateTime> OnDayChanged            // Fires at midnight (new day)
Data Structures
ScheduledEvent
csharp
class ScheduledEvent {
    string id;
    DateTime scheduledTime;
    Action callback;
    string description;
    bool cancelled;
}
TimeState (for saving)
csharp
struct TimeState {
    DateTime currentTime;
    float energy;
    float timeScale;
    List<ScheduledEvent> pendingEvents;
}
Dependencies
Reads from:

ActivitySystem (what activities are running, how much energy they drain)
Writes to:

None directly (broadcasts events)
Subscribed to by:

ActivitySystem (updates activities each game-minute)
JobSystem (tracks shift times)
RelationshipSystem (time-based relationship decay)
EventSystem (social events have scheduled times)
Implementation Notes
Time Progression:

csharp
private DateTime currentTime;
private float timeScale = 1.0f;
private float realTimeAccumulator = 0f;

void Update() {
    realTimeAccumulator += Time.deltaTime * timeScale;
    
    while (realTimeAccumulator >= 60f) { // 60 real seconds = 1 game hour (at scale 1.0)
        realTimeAccumulator -= 60f;
        AdvanceGameTime(60); // 60 game minutes
    }
}

void AdvanceGameTime(int minutes) {
    DateTime oldTime = currentTime;
    currentTime = currentTime.AddMinutes(minutes);
    
    // Check for day change
    if (oldTime.Date != currentTime.Date) {
        OnDayChanged?.Invoke(currentTime);
    }
    
    // Process scheduled events
    ProcessScheduledEvents(oldTime, currentTime);
    
    // Broadcast
    OnTimeAdvanced?.Invoke(currentTime);
}
Energy System:

csharp
private float energy = 100f;
private const float PASSIVE_DRAIN_PER_HOUR = 2f; // Awake but idle

void Update() {
    // Passive energy drain (just being awake costs energy)
    if (!isSleeping) {
        float drainThisFrame = (PASSIVE_DRAIN_PER_HOUR / 3600f) * Time.deltaTime * timeScale;
        ModifyEnergy(-drainThisFrame, "passive_drain");
    }
}
Event Scheduling:

csharp
private Dictionary<string, ScheduledEvent> scheduledEvents = new Dictionary<string, ScheduledEvent>();

void ProcessScheduledEvents(DateTime fromTime, DateTime toTime) {
    var eventsToTrigger = scheduledEvents.Values
        .Where(e => !e.cancelled && e.scheduledTime >= fromTime && e.scheduledTime < toTime)
        .OrderBy(e => e.scheduledTime)
        .ToList();
    
    foreach (var evt in eventsToTrigger) {
        evt.callback?.Invoke();
        scheduledEvents.Remove(evt.id);
    }
}
```

---

## **Edge Cases**

1. **Energy goes negative:** Clamp to 0, trigger depletion event
2. **Time skip past multiple scheduled events:** Process all in chronological order
3. **Schedule event in the past:** Log warning, don't schedule
4. **Cancel already-triggered event:** Return false (event no longer exists)
5. **Sleep while energy is 100:** Still advance time, energy stays 100

---

## **Testing Checklist**
```
[ ] Time advances at correct rate (1 real minute = 1 game hour at default scale)
[ ] SetTimeScale changes progression speed correctly
[ ] GetCurrentTime returns accurate game time
[ ] Energy drains passively when awake
[ ] Energy clamps at 0 and 100
[ ] OnEnergyDepleted fires when energy hits 0
[ ] ScheduleEvent fires callback at correct time
[ ] Multiple scheduled events fire in correct order
[ ] CancelScheduledEvent prevents event from firing
[ ] Sleep advances time and restores energy
[ ] OnDayChanged fires at midnight
[ ] System persists across scene changes
