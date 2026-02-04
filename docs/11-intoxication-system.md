11. INTOXICATION SYSTEM
Purpose
Manages player intoxication from alcohol/drug consumption. Affects player performance (driving, minigames), creates risk (DUI arrests), and models consequences of substance use. Supports party/social event gameplay and cautionary tale story arcs ("DUI Spiral").

Interface
ModifyIntoxication
csharp
void ModifyIntoxication(float delta, IntoxicationType type)
Purpose: Change player's intoxication level.

Parameters:

delta: Amount to change (positive = more intoxicated)
type: What substance (Alcohol, Cannabis, Stimulant, etc.)
Side effects:

Updates intoxication level (clamped 0-1)
Applies impairment effects if above threshold
Broadcasts OnIntoxicationChanged event
GetIntoxicationLevel
csharp
float GetIntoxicationLevel()
Purpose: Returns current intoxication level.

Returns: Value from 0 (sober) to 1 (blackout)

GetImpairmentLevel
csharp
float GetImpairmentLevel(ImpairmentType type)
Purpose: Get specific impairment modifier.

Parameters:

type: Driving, Coordination, Judgment, etc.
Returns: Modifier from 0 (fully impaired) to 1 (no impairment)

Example:

csharp
float drivingImpairment = GetImpairmentLevel(ImpairmentType.Driving);
// 0.5 = driving is 50% harder
Consume
csharp
void Consume(string itemId, float amount)
Purpose: Player consumes alcohol/substance.

Parameters:

itemId: What they're consuming (beer, wine, drugs)
amount: How much (1 drink, 2 drinks, etc.)
Side effects:

Increases intoxication based on item strength
Applies substance-specific effects
May trigger events (blackout, overdose)
CheckDUI
csharp
bool CheckDUI(string observerId)
Purpose: Determine if player gets caught for DUI.

Parameters:

observerId: Cop/checkpoint that's checking
Returns: true if player is over legal limit AND gets caught

Side effects:

If caught, broadcasts OnDUIArrest event
Creates criminal record
May confiscate license
Events
csharp
event Action<float> OnIntoxicationChanged      // (newLevel)
event Action<IntoxicationType> OnConsumed      // (substanceType)
event Action OnBlackout                        // Player too intoxicated
event Action<string> OnDUIArrest               // (observerId)
event Action OnSobrietyAchieved                // Returned to 0 intoxication
Data Structures
IntoxicationType (enum)
csharp
enum IntoxicationType {
    Alcohol,
    Cannabis,
    Stimulant,
    Depressant,
    Psychedelic
}
ImpairmentType (enum)
csharp
enum ImpairmentType {
    Driving,        // Affects vehicle control
    Coordination,   // Affects minigame performance
    Judgment,       // Affects decision-making (could make risky choices)
    Perception      // Visual effects, slower reactions
}
IntoxicationState
csharp
struct IntoxicationState {
    float level;                                    // 0-1 scale
    Dictionary<IntoxicationType, float> byType;     // Track each substance separately
    float peakLevel;                                // Highest level reached (for consequences)
    DateTime lastConsumption;
    bool hasLicense;                                // Can be revoked for DUI
}
ConsumableItem
csharp
struct ConsumableItem {
    string id;
    IntoxicationType type;
    float intoxicationIncrease;   // How much it raises level
    float duration;               // How long effects last (game hours)
}
Dependencies
Reads from:

TimeSystem (intoxication decays over time)
EntitySystem (consumable items)
Writes to:

ReputationSystem (DUI affects legal reputation)
EconomySystem (fines for DUI)
CriminalRecordSystem (DUI creates record)
Subscribed to by:

VehicleSystem (driving difficulty affected by intoxication)
MinigameSystem (performance affected by coordination impairment)
DetectionSystem (cops check for DUI)
Implementation Notes
Intoxication Metabolism:

csharp
void Update() {
    if (intoxicationState.level > 0) {
        float deltaTime = Time.deltaTime;
        float metabolismRate = 0.02f;  // 2% per game hour
        
        // Decay intoxication
        intoxicationState.level -= metabolismRate * deltaTime;
        intoxicationState.level = Mathf.Max(0, intoxicationState.level);
        
        // Check if returned to sobriety
        if (intoxicationState.level == 0 && previousLevel > 0) {
            OnSobrietyAchieved?.Invoke();
        }
        
        OnIntoxicationChanged?.Invoke(intoxicationState.level);
    }
}
Impairment Calculation:

csharp
float GetImpairmentLevel(ImpairmentType type) {
    float baseImpairment = 1.0f - intoxicationState.level;  // Higher intox = more impairment
    
    switch (type) {
        case ImpairmentType.Driving:
            // Driving impaired earlier than other skills
            return Mathf.Clamp01(1.0f - (intoxicationState.level * 1.5f));
        
        case ImpairmentType.Coordination:
            return baseImpairment;
        
        case ImpairmentType.Judgment:
            // Judgment impaired at lower levels
            return Mathf.Clamp01(1.0f - (intoxicationState.level * 1.2f));
        
        case ImpairmentType.Perception:
            return baseImpairment;
        
        default:
            return baseImpairment;
    }
}
DUI Detection:

csharp
bool CheckDUI(string observerId) {
    const float LEGAL_LIMIT = 0.08f;  // Standard DUI threshold
    
    if (intoxicationState.level < LEGAL_LIMIT) {
        return false;  // Under legal limit
    }
    
    // Cop checks if player is driving impaired
    Observer cop = DetectionSystem.Instance.GetObserver(observerId);
    
    // Visual check (weaving, speeding)
    float detectionChance = (intoxicationState.level - LEGAL_LIMIT) * 5f;  // Higher = more obvious
    
    if (Random.value < detectionChance) {
        // Caught!
        OnDUIArrest?.Invoke(observerId);
        ApplyDUIConsequences();
        return true;
    }
    
    return false;
}

void ApplyDUIConsequences() {
    // Fine
    EconomySystem.Instance.DeductExpense(
        playerId,
        Random.Range(1000f, 5000f),
        ExpenseType.Fine,
        "DUI fine"
    );
    
    // Criminal record
    CriminalRecordSystem.Instance.AddOffense(
        playerId,
        OffenseType.DUI,
        "Driving under the influence"
    );
    
    // License suspension
    intoxicationState.hasLicense = false;
    
    // Reputation hit
    ReputationSystem.Instance.ModifyReputation(
        playerId,
        ReputationTrack.Legal,
        -15f,
        "DUI arrest"
    );
    
    // Jail time (minor)
    TimeSystem.Instance.AdvanceTime(24f * 60f);  // 1 day in jail
}
Substance Consumption:

csharp
void Consume(string itemId, float amount) {
    ConsumableItem item = GetConsumableItem(itemId);
    
    float intoxIncrease = item.intoxicationIncrease * amount;
    
    // Add to total intoxication
    intoxicationState.level += intoxIncrease;
    intoxicationState.level = Mathf.Clamp01(intoxicationState.level);
    
    // Track by type
    if (!intoxicationState.byType.ContainsKey(item.type)) {
        intoxicationState.byType[item.type] = 0;
    }
    intoxicationState.byType[item.type] += intoxIncrease;
    
    // Update peak
    intoxicationState.peakLevel = Mathf.Max(intoxicationState.peakLevel, intoxicationState.level);
    intoxicationState.lastConsumption = TimeSystem.Instance.GetCurrentTime();
    
    // Check for blackout
    if (intoxicationState.level >= 0.9f) {
        OnBlackout?.Invoke();
    }
    
    OnConsumed?.Invoke(item.type);
    OnIntoxicationChanged?.Invoke(intoxicationState.level);
}
Visual/Gameplay Effects:

csharp
// Applied to camera/controls based on intoxication
void ApplyIntoxicationEffects() {
    if (intoxicationState.level < 0.1f) return;  // Minimal effects below 0.1
    
    // Visual effects
    float blurAmount = intoxicationState.level * 0.5f;
    CameraEffects.SetBlur(blurAmount);
    
    float wobbleAmount = intoxicationState.level * 10f;
    CameraEffects.SetWobble(wobbleAmount);
    
    // Control effects
    if (player.isDriving) {
        float steeringImpairment = GetImpairmentLevel(ImpairmentType.Driving);
        VehicleController.SetSteeringSensitivity(steeringImpairment);
    }
    
    // Minigame effects
    if (player.isInMinigame) {
        float coordImpairment = GetImpairmentLevel(ImpairmentType.Coordination);
        MinigameSystem.SetPerformanceModifier(coordImpairment);
    }
}
```

---

## **Edge Cases**

1. **Consume while already intoxicated:** Levels stack, can exceed 1.0 → blackout
2. **DUI check while not driving:** Returns false (can't get DUI while walking)
3. **License already suspended:** Additional DUI creates heavier penalties
4. **Intoxication at job:** Performance suffers, may get fired if noticed
5. **Mixing substances:** Effects compound (alcohol + stimulant = unpredictable)

---

## **Testing Checklist**
```
[ ] Consuming alcohol increases intoxication level
[ ] Intoxication decays over time
[ ] Intoxication level clamps to 0-1
[ ] Driving impairment increases with intoxication
[ ] Minigame performance decreases with intoxication
[ ] DUI check returns false when under legal limit
[ ] DUI check can catch player when over limit
[ ] DUI arrest creates criminal record
[ ] DUI arrest results in fine and reputation loss
[ ] License suspension prevents legal driving
[ ] Blackout triggers at high intoxication
[ ] Visual effects (blur, wobble) scale with intoxication
[ ] OnSobrietyAchieved fires when returning to 0