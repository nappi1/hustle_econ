5. RELATIONSHIP SYSTEM (UPDATED)
Purpose
Manages relationships between player and NPCs (romantic partners, family, friends). NPCs observe player actions, react based on their values/tolerances, remember significant events, and relationship scores change accordingly. Features deep memory system and contextually-aware dialogue generation.

Interface
CreateNPC
csharp
NPC CreateNPC(NPCType type, NPCData data)
Purpose: Creates a new NPC with personality, values, tolerances, and memory capacity.

Parameters:

type: NPCType enum (RomanticPartner, Family, Friend)
data: NPCData containing personality configuration
Returns: NPC instance

Side effects:

Registers NPC in system
Sets initial relationship score (default 50)
Initializes empty memory array
Broadcasts OnNPCCreated event
GetRelationshipScore
csharp
float GetRelationshipScore(string npcId)
Purpose: Returns current relationship score with NPC.

Parameters:

npcId: NPC ID
Returns: Score from 0-100

ModifyRelationship
csharp
void ModifyRelationship(string npcId, float delta, string reason)
Purpose: Change relationship score.

Parameters:

npcId: NPC ID
delta: Amount to change
reason: Why it changed
Side effects:

Updates relationship score
Checks threshold crossings
Broadcasts OnRelationshipChanged event
May trigger relationship events (warnings, ultimatums, breakups)
ObservePlayerAction
csharp
void ObservePlayerAction(PlayerAction action)
Purpose: NPCs observe player actions, add to memory, and react based on their values.

Parameters:

action: PlayerAction struct containing what player did
Side effects:

Each NPC evaluates action against their values
Adds event to NPC memory (if significant)
Modifies relationships accordingly
May trigger dialogue/events
Example:

csharp
ObservePlayerAction(new PlayerAction {
    type = ActionType.Arrested,
    details = "drug possession",
    timestamp = DateTime.Now,
    memorability = 9  // High - never forgotten
});
RecallMemory
csharp
List<ObservedEvent> RecallMemory(string npcId, int count = 10)
Purpose: Get NPC's most recent/relevant memories.

Parameters:

npcId: NPC ID
count: How many memories to return
Returns: List of ObservedEvent, sorted by recency and emotional intensity

CheckRelationshipThreshold
csharp
bool CheckRelationshipThreshold(string npcId, float threshold, ThresholdComparison comparison)
Purpose: Check if relationship meets requirement.

Parameters:

npcId: NPC ID
threshold: Required score
comparison: GreaterThan, LessThan, etc.
Returns: true if condition met

TriggerEvent
csharp
void TriggerEvent(string npcId, RelationshipEventType eventType)
Purpose: Manually trigger relationship event (birthday, anniversary, request).

Parameters:

npcId: NPC ID
eventType: What kind of event
Side effects:

Creates calendar event (via EventSystem)
Attending/ignoring affects relationship
Creates high-memorability event in NPC memory
Broadcasts OnRelationshipEvent event
GetNPCDialogue
csharp
string GetNPCDialogue(string npcId, string scenario)
Purpose: Generate contextual dialogue based on relationship state, recent actions, and memory.

Parameters:

npcId: NPC ID
scenario: Dialogue scenario key
Returns: Dialogue string (selected from pre-generated pool based on context)

Implementation:

Checks if NPC response is "outlier" (unusual for their values)
Filters dialogue pool by appropriate tags
References recent memories where relevant
Returns contextually appropriate dialogue
Events
csharp
event Action<NPC> OnNPCCreated
event Action<string, float, float> OnRelationshipChanged  // (npcId, oldScore, newScore)
event Action<string, RelationshipEventType> OnRelationshipEvent
event Action<string> OnBreakup
event Action<string> OnReconciliation
event Action<string, ObservedEvent> OnMemoryAdded  // NEW - when significant event recorded
Data Structures
NPC
csharp
class NPC {
    string id;
    string name;
    NPCType type;
    float relationshipScore;         // 0-100
    NPCPersonality personality;
    
    // Values - what they care about (0-1 scale)
    Dictionary<NPCValue, float> values;
    
    // Tolerances - what they'll accept
    Dictionary<NPCTolerance, ToleranceLevel> tolerances;
    
    RelationshipStatus status;
    
    // Memory System
    List<ObservedEvent> memory;
    int memoryCapacity = 50;        // Max events remembered
    
    // Pattern Detection
    Dictionary<string, PatternData> detectedPatterns;  // "missed_dates": count
}
NPCType (enum)
csharp
enum NPCType {
    RomanticPartner,
    Family,
    Friend
}
NPCPersonality (enum)
csharp
enum NPCPersonality {
    Supportive,   // High tolerance, encourages player
    Demanding,    // Low tolerance, expects attention
    Distant,      // Doesn't react much, independent
    Volatile      // Big reactions, mood swings
}
NPCValue (enum) - UPDATED
csharp
enum NPCValue {
    Stability,      // Wants steady income, safe choices
    Ambition,       // Respects risk-taking, success
    Loyalty,        // Wants commitment, time together
    Morality,       // Cares about legality, ethics
    Family,         // Prioritizes family time
    Independence,   // Respects space, freedom
    Vanity,         // NEW - Cares about appearance, status, material success
    Hedonism        // NEW - Values pleasure, experiences, living in moment
}
NPCTolerance (enum) - UPDATED
csharp
enum NPCTolerance {
    WorkHours,          // How much work is acceptable
    IllegalActivity,    // How much crime they'll tolerate
    Risk,               // Tolerance for dangerous choices
    Neglect,            // How long they'll accept being ignored
    SexualBoundaries,   // NEW - Monogamy, open relationships, sex work
    MoralFlexibility    // NEW - Can they rationalize bad behavior
}
ToleranceLevel (enum)
csharp
enum ToleranceLevel {
    Low,
    Medium,
    High
}
SexualBoundaryType (enum) - NEW
csharp
enum SexualBoundaryType {
    StrictMonogamy,      // Zero tolerance for anything outside relationship
    Monogamous,          // Expects monogamy but more forgiving
    Open,                // Open to non-monogamy with communication
    SexWorkAccepting,    // Okay with partner doing sex work
    Collaborative        // Willing to participate in partner's sex work
}
RelationshipStatus (enum)
csharp
enum RelationshipStatus {
    Active,
    Strained,     // < 30, on thin ice
    Broken,       // < 20, broke up
    Reconciling   // Trying to rebuild
}
ObservedEvent (memory entry) - NEW
csharp
struct ObservedEvent {
    PlayerAction action;
    DateTime timestamp;
    int memorability;           // 1-10 scale (how significant)
    float emotionalIntensity;   // 0-100, current emotional weight
    float initialIntensity;     // Original emotional weight (for comparison)
    bool isPermanent;           // Never forgotten (arrests, betrayals)
    MemoryTier tier;            // Volatile, Standard, or Permanent
}
MemoryTier (enum) - NEW
csharp
enum MemoryTier {
    Volatile,    // Memorability 1-3, fades quickly
    Standard,    // Memorability 4-6, fades slowly
    Permanent    // Memorability 7-10, never fades
}
PatternData - NEW
csharp
struct PatternData {
    string patternType;      // "missed_dates", "lies_about_work", etc.
    int count;               // How many times observed
    DateTime firstOccurrence;
    DateTime lastOccurrence;
    int memorability;        // Pattern itself has memorability (even if events don't)
}
PlayerAction (observation)
csharp
struct PlayerAction {
    ActionType type;
    string details;
    DateTime timestamp;
    int memorability;       // NEW - How memorable this event is (1-10)
    bool isPositive;        // From NPC's perspective
}
ActionType (enum)
csharp
enum ActionType {
    WorkedOvertime,
    MissedEvent,
    GotPromoted,
    GotFired,
    Arrested,
    BoughtExpensiveItem,
    AttendedEvent,
    SpentTimeTogether,
    Cheated,            // NEW
    LiedAboutLocation,  // NEW
    StartedSexWork,     // NEW
    Other
}
DialogueOption - NEW
csharp
struct DialogueOption {
    string text;
    List<string> tags;                       // ["outlier_aware", "conflicted", etc.]
    Dictionary<string, string> requirements; // "moral_flexibility": ">70"
}
DialogueTags - NEW
csharp
// Common tags for dialogue filtering
const string[] DIALOGUE_TAGS = {
    "outlier_aware",    // NPC acknowledges unusual response
    "standard",         // Default response for NPC's values
    "conflicted",       // Wants to say yes but can't
    "firm_refusal",     // Clear, unwavering no
    "apologetic",       // NPC feels bad about response
    "defensive",        // NPC justifies their position
    "references_memory", // Mentions past event
    "pattern_aware"     // Acknowledges repeated behavior
};
Dependencies
Reads from:

TimeSystem (track time since last interaction, emotional intensity decay)
ReputationSystem (criminal/professional rep affects some NPCs)
EconomySystem (financial status affects relationships)
EventSystem (social events player attends/skips)
Writes to:

EventSystem (creates relationship events like birthdays)
Subscribed to by:

UI (display relationship status, memories)
EventSystem (relationship events affect calendar)
Implementation Notes
Memory System:
csharp
void ObservePlayerAction(PlayerAction action) {
    foreach (var npc in allNPCs.Values) {
        float impact = CalculateActionImpact(npc, action);
        
        if (impact != 0) {
            // Modify relationship
            ModifyRelationship(npc.id, impact, action.type.ToString());
            
            // Add to memory if significant
            if (ShouldRemember(action, npc)) {
                AddToMemory(npc, action);
            }
            
            // Check for patterns
            UpdatePatternDetection(npc, action);
        }
    }
}

bool ShouldRemember(PlayerAction action, NPC npc) {
    // Always remember high memorability events
    if (action.memorability >= 7) return true;
    
    // Remember if emotionally significant to THIS NPC
    float impact = CalculateActionImpact(npc, action);
    if (Mathf.Abs(impact) > 10f) return true;
    
    // Don't remember trivial events
    if (action.memorability < 3 && Mathf.Abs(impact) < 5f) return false;
    
    return action.memorability >= 4;
}

void AddToMemory(NPC npc, PlayerAction action) {
    // Determine memory tier
    MemoryTier tier = action.memorability >= 7 ? MemoryTier.Permanent :
                      action.memorability >= 4 ? MemoryTier.Standard :
                      MemoryTier.Volatile;
    
    // Calculate emotional intensity
    float intensity = Mathf.Abs(CalculateActionImpact(npc, action)) * 10f;
    
    ObservedEvent memory = new ObservedEvent {
        action = action,
        timestamp = action.timestamp,
        memorability = action.memorability,
        emotionalIntensity = intensity,
        initialIntensity = intensity,
        isPermanent = (tier == MemoryTier.Permanent),
        tier = tier
    };
    
    // Add to memory
    npc.memory.Add(memory);
    
    // Trim if over capacity (keep permanent + highest intensity)
    if (npc.memory.Count > npc.memoryCapacity) {
        TrimMemory(npc);
    }
    
    OnMemoryAdded?.Invoke(npc.id, memory);
}

void TrimMemory(NPC npc) {
    // Never remove permanent memories
    var permanentMemories = npc.memory.Where(m => m.isPermanent).ToList();
    
    // From non-permanent, keep highest emotional intensity
    var volatileMemories = npc.memory
        .Where(m => !m.isPermanent)
        .OrderByDescending(m => m.emotionalIntensity)
        .Take(npc.memoryCapacity - permanentMemories.Count)
        .ToList();
    
    npc.memory = permanentMemories.Concat(volatileMemories).ToList();
}
Emotional Intensity Decay:
csharp
void Update() {
    float deltaTime = Time.deltaTime;
    DateTime now = TimeSystem.Instance.GetCurrentTime();
    
    foreach (var npc in allNPCs.Values) {
        foreach (var memory in npc.memory) {
            if (memory.isPermanent && memory.emotionalIntensity > 0) {
                // Permanent memories: fact stays, emotion fades
                float daysSince = (float)(now - memory.timestamp).TotalDays;
                float decayRate = 0.05f;  // 5% per game day
                
                memory.emotionalIntensity = Mathf.Max(
                    memory.initialIntensity * 0.2f,  // Never drops below 20% of initial
                    memory.emotionalIntensity - (decayRate * deltaTime)
                );
            }
            else if (memory.tier == MemoryTier.Volatile) {
                // Volatile memories: fade fast
                float hoursSince = (float)(now - memory.timestamp).TotalHours;
                float decayRate = 0.2f;  // 20% per game hour
                
                memory.emotionalIntensity -= decayRate * deltaTime;
                
                // Remove when intensity hits 0
                if (memory.emotionalIntensity <= 0) {
                    npc.memory.Remove(memory);
                }
            }
        }
    }
}
Pattern Detection:
csharp
void UpdatePatternDetection(NPC npc, PlayerAction action) {
    // Define patterns to track
    string patternKey = GetPatternKey(action.type);
    if (patternKey == null) return;
    
    if (!npc.detectedPatterns.ContainsKey(patternKey)) {
        npc.detectedPatterns[patternKey] = new PatternData {
            patternType = patternKey,
            count = 0,
            firstOccurrence = action.timestamp
        };
    }
    
    PatternData pattern = npc.detectedPatterns[patternKey];
    pattern.count++;
    pattern.lastOccurrence = action.timestamp;
    
    // Pattern becomes memorable if repeated enough
    if (pattern.count >= 3) {
        pattern.memorability = Mathf.Min(10, 5 + pattern.count);  // Caps at 10
    }
}

string GetPatternKey(ActionType type) {
    switch (type) {
        case ActionType.MissedEvent: return "missed_events";
        case ActionType.WorkedOvertime: return "overworking";
        case ActionType.LiedAboutLocation: return "lying";
        case ActionType.Cheated: return "infidelity";
        default: return null;
    }
}
Outlier-Aware Dialogue:
csharp
string GetNPCDialogue(string npcId, string scenario) {
    NPC npc = GetNPC(npcId);
    
    // Load dialogue pool for this scenario
    List<DialogueOption> pool = LoadDialoguePool(scenario);
    
    // Determine player's request/action in scenario
    string responseType = DetermineResponseType(npc, scenario);
    
    // Is this response an outlier for this NPC?
    bool isOutlier = IsOutlierResponse(npc, responseType);
    
    // Filter dialogue by requirements
    var filtered = pool.Where(option => 
        MeetsRequirements(option.requirements, npc, responseType)
    ).ToList();
    
    // Prioritize outlier-aware dialogue if applicable
    if (isOutlier && npc.relationshipScore > 60) {
        var outlierDialogue = filtered
            .Where(d => d.tags.Contains("outlier_aware"))
            .ToList();
        
        if (outlierDialogue.Count > 0) {
            return SelectRandom(outlierDialogue).text;
        }
    }
    
    // Check for recent relevant memories
    var recentMemories = RecallRelevantMemories(npc, scenario);
    if (recentMemories.Count > 0) {
        var memoryDialogue = filtered
            .Where(d => d.tags.Contains("references_memory"))
            .ToList();
        
        if (memoryDialogue.Count > 0) {
            string dialogue = SelectRandom(memoryDialogue).text;
            // Inject memory reference
            return InjectMemoryReference(dialogue, recentMemories[0]);
        }
    }
    
    // Standard dialogue
    var standardDialogue = filtered
        .Where(d => d.tags.Contains("standard"))
        .ToList();
    
    return SelectRandom(standardDialogue).text;
}

bool IsOutlierResponse(NPC npc, string responseType) {
    // Examples of outlier detection:
    
    // High moral flexibility but refusing illegal request
    if (npc.values[NPCValue.MoralFlexibility] > 0.7f && responseType == "refuses_illegal") {
        return true;
    }
    
    // High loyalty but betraying player
    if (npc.values[NPCValue.Loyalty] > 0.8f && responseType == "betrays") {
        return true;
    }
    
    // Low vanity but impressed by flashy purchase
    if (npc.values[NPCValue.Vanity] < 0.3f && responseType == "impressed_by_item") {
        return true;
    }
    
    return false;
}

string InjectMemoryReference(string dialogue, ObservedEvent memory) {
    // Replace placeholders with actual memory details
    dialogue = dialogue.Replace("{memory_event}", memory.action.type.ToString());
    dialogue = dialogue.Replace("{memory_time}", GetTimeAgoString(memory.timestamp));
    return dialogue;
}
```

---

## **Edge Cases**

1. **Memory capacity exceeded:** Keep permanent memories + highest intensity
2. **Multiple patterns detected:** NPCs reference most recent/severe
3. **Outlier dialogue unavailable:** Fall back to standard dialogue
4. **Emotional intensity negative:** Clamp to 0, remove volatile memories
5. **Permanent memory with 0 intensity:** Keep fact, emotion fully faded

---

## **Testing Checklist**
```
[ ] CreateNPC generates NPC with correct values/tolerances
[ ] NPCs with different values react differently to same action
[ ] High memorability events (7-10) always added to memory
[ ] Low memorability events (1-3) only added if emotionally significant
[ ] Permanent memories never removed from memory array
[ ] Volatile memories decay and are removed when intensity hits 0
[ ] Emotional intensity decays over time for permanent memories
[ ] Pattern detection increments count correctly
[ ] Patterns become memorable after 3+ occurrences
[ ] GetNPCDialogue returns outlier-aware dialogue when appropriate
[ ] Dialogue references recent memories when relevant
[ ] Memory capacity trimming keeps permanent + highest intensity
[ ] Sexual boundary values affect reactions to intimacy/sex work
[ ] Vanity values affect reactions to expensive purchases
[ ] Moral flexibility affects rationalization of illegal activities