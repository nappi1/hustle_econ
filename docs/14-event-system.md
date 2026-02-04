14. EVENT SYSTEM
Purpose
Manages scheduled events (social gatherings, birthdays, anniversaries, relationship obligations, job shifts). Player can attend or skip events with consequences. Integrates with Relationship System (NPCs care if you show up), Time System (events are scheduled), and Calendar UI.

Interface
CreateEvent
csharp
string CreateEvent(EventData data)
Purpose: Schedule a new event.

Parameters:

data: EventData struct with event details
Returns: Event ID

Side effects:

Adds to event calendar
Schedules reminder (if enabled)
Broadcasts OnEventCreated event
GetUpcomingEvents
csharp
List<GameEvent> GetUpcomingEvents(int daysAhead = 7)
Purpose: Get events happening soon.

Parameters:

daysAhead: How many days to look ahead
Returns: List of upcoming events

AttendEvent
csharp
void AttendEvent(string eventId)
Purpose: Player attends event.

Parameters:

eventId: Which event
Side effects:

Time advances (event duration)
Relationship with host/attendees increases
May trigger minigame/scene
Broadcasts OnEventAttended event
SkipEvent
csharp
void SkipEvent(string eventId)
Purpose: Player doesn't attend event.

Parameters:

eventId: Which event
Side effects:

Relationship damage with host/attendees
Event is marked as skipped
May trigger angry/disappointed dialogue
Broadcasts OnEventSkipped event
CancelEvent
csharp
void CancelEvent(string eventId, string reason)
Purpose: Remove scheduled event.

Parameters:

eventId: Which event
reason: Why cancelled
Events
csharp
event Action<GameEvent> OnEventCreated
event Action<GameEvent> OnEventReminder       // Fires 24 hours before event
event Action<string> OnEventAttended          // (eventId)
event Action<string> OnEventSkipped           // (eventId)
event Action<string, string> OnEventCancelled // (eventId, reason)
Data Structures
GameEvent
csharp
class GameEvent {
    string id;
    string name;
    EventType type;
    DateTime scheduledTime;
    float durationHours;
    
    // Who's involved
    string hostId;                  // Who organized (NPC or system)
    List<string> attendees;         // Other NPCs attending
    
    // Consequences
    float attendBonus;              // Relationship gain if attend
    float skipPenalty;              // Relationship loss if skip
    
    // Optional minigame/activity
    string minigameId;              // null if just cutscene/dialogue
    
    // State
    bool attended;
    bool skipped;
    bool reminded;
}
EventType (enum)
csharp
enum EventType {
    Birthday,
    Anniversary,
    Party,
    DateNight,
    FamilyDinner,
    WorkMeeting,
    SugarObligation,     // Sugar relationship requirement
    NetworkingEvent
}
EventData (for creation)
csharp
struct EventData {
    string name;
    EventType type;
    DateTime scheduledTime;
    float durationHours;
    string hostId;
    List<string> attendees;
    float attendBonus;
    float skipPenalty;
    string minigameId;
}
Dependencies
Reads from:

TimeSystem (scheduling, current time)
RelationshipSystem (who to invite, relationship consequences)
Writes to:

RelationshipSystem (attendance affects relationships)
TimeSystem (attending advances time)
Subscribed to by:

UI (calendar display, event notifications)
RelationshipSystem (creates relationship events)
Implementation Notes
Event Scheduling:

csharp
string CreateEvent(EventData data) {
    GameEvent evt = new GameEvent {
        id = GenerateEventId(),
        name = data.name,
        type = data.type,
        scheduledTime = data.scheduledTime,
        durationHours = data.durationHours,
        hostId = data.hostId,
        attendees = data.attendees,
        attendBonus = data.attendBonus,
        skipPenalty = data.skipPenalty,
        minigameId = data.minigameId,
        attended = false,
        skipped = false,
        reminded = false
    };
    
    upcomingEvents.Add(evt);
    
    // Schedule reminder (24 hours before)
    DateTime reminderTime = evt.scheduledTime.AddDays(-1);
    TimeSystem.Instance.ScheduleEvent(
        reminderTime,
        () => TriggerReminder(evt.id),
        $"Reminder: {evt.name}"
    );
    
    OnEventCreated?.Invoke(evt);
    
    return evt.id;
}
Event Reminder:

csharp
void TriggerReminder(string eventId) {
    GameEvent evt = GetEvent(eventId);
    if (evt == null || evt.attended || evt.skipped) return;
    
    evt.reminded = true;
    
    // UI notification
    OnEventReminder?.Invoke(evt);
    
    // Player can choose to attend or skip
}
Attending Event:

csharp
void AttendEvent(string eventId) {
    GameEvent evt = GetEvent(eventId);
    
    // Travel to event location (if not already there)
    // Time advances
    TimeSystem.Instance.AdvanceTime(evt.durationHours * 60f);
    
    // Mark attended
    evt.attended = true;
    
    // Relationship bonuses
    if (evt.hostId != null) {
        RelationshipSystem.Instance.ModifyRelationship(
            evt.hostId,
            evt.attendBonus,
            $"Attended {evt.name}"
        );
    }
    
    foreach (string attendeeId in evt.attendees) {
        RelationshipSystem.Instance.ModifyRelationship(
            attendeeId,
            evt.attendBonus * 0.5f,  // Smaller bonus for non-host
            $"Saw you at {evt.name}"
        );
    }
    
    // Trigger minigame/scene if exists
    if (!string.IsNullOrEmpty(evt.minigameId)) {
        ActivitySystem.Instance.CreateActivity(
            ActivityType.Physical,
            evt.minigameId,
            evt.durationHours
        );
    }
    
    // Memory for NPCs
    RelationshipSystem.Instance.ObservePlayerAction(new PlayerAction {
        type = ActionType.AttendedEvent,
        details = evt.name,
        timestamp = evt.scheduledTime,
        memorability = 5,
        isPositive = true
    });
    
    OnEventAttended?.Invoke(eventId);
    
    // Remove from upcoming
    upcomingEvents.Remove(evt);
}
Skipping Event:

csharp
void SkipEvent(string eventId) {
    GameEvent evt = GetEvent(eventId);
    
    // Mark skipped
    evt.skipped = true;
    
    // Relationship penalties
    if (evt.hostId != null) {
        RelationshipSystem.Instance.ModifyRelationship(
            evt.hostId,
            -evt.skipPenalty,
            $"Skipped {evt.name}"
        );
    }
    
    foreach (string attendeeId in evt.attendees) {
        RelationshipSystem.Instance.ModifyRelationship(
            attendeeId,
            -evt.skipPenalty * 0.3f,  // Smaller penalty for non-host
            $"You didn't show up to {evt.name}"
        );
    }
    
    // Memory (NPCs remember you bailed)
    RelationshipSystem.Instance.ObservePlayerAction(new PlayerAction {
        type = ActionType.MissedEvent,
        details = evt.name,
        timestamp = evt.scheduledTime,
        memorability = 6,  // More memorable than attending
        isPositive = false
    });
    
    OnEventSkipped?.Invoke(eventId);
    
    // Remove from upcoming
    upcomingEvents.Remove(evt);
}
Auto-Generated Events:

csharp
// RelationshipSystem calls this when creating NPC
public void GenerateRelationshipEvents(string npcId, NPCType type) {
    NPC npc = RelationshipSystem.Instance.GetNPC(npcId);
    DateTime now = TimeSystem.Instance.GetCurrentTime();
    
    if (type == NPCType.RomanticPartner) {
        // Anniversary (1 year from relationship start)
        CreateEvent(new EventData {
            name = $"Anniversary with {npc.name}",
            type = EventType.Anniversary,
            scheduledTime = now.AddYears(1),
            durationHours = 3f,
            hostId = npcId,
            attendees = new List<string>(),
            attendBonus = 20f,
            skipPenalty = 30f
        });
        
        // Regular date nights (monthly)
        for (int i = 1; i <= 12; i++) {
            CreateEvent(new EventData {
                name = $"Date Night with {npc.name}",
                type = EventType.DateNight,
                scheduledTime = now.AddMonths(i),
                durationHours = 2f,
                hostId = npcId,
                attendees = new List<string>(),
                attendBonus = 10f,
                skipPenalty = 15f
            });
        }
    }
    
    if (type == NPCType.Family) {
        // Family birthday
        CreateEvent(new EventData {
            name = $"{npc.name}'s Birthday",
            type = EventType.Birthday,
            scheduledTime = GetNextBirthday(npc),
            durationHours = 2f,
            hostId = npcId,
            attendees = GetFamilyMembers(),
            attendBonus = 15f,
            skipPenalty = 25f
        });
    }
}
```

---

## **Edge Cases**

1. **Event time conflicts:** Player must choose which to attend
2. **Event while in jail:** Auto-skipped, relationships damaged
3. **Event at closed location:** Event cancelled automatically
4. **Multiple events same day:** All appear, player chooses priority
5. **Recurring events:** Create multiple instances in advance

---

## **Testing Checklist**
```
[ ] CreateEvent schedules event correctly
[ ] GetUpcomingEvents returns events within timeframe
[ ] Event reminder fires 24 hours before
[ ] AttendEvent increases relationship with host
[ ] AttendEvent advances time by event duration
[ ] SkipEvent decreases relationship with host
[ ] SkipEvent creates negative memory for NPCs
[ ] Multiple events can be scheduled for same NPC
[ ] Events removed from calendar after attended/skipped
[ ] OnEventAttended and OnEventSkipped fire correctly
[ ] Relationship events auto-generate for new NPCs
[ ] Conflicting events both appear (player chooses)
