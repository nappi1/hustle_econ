13. BODY SYSTEM
Purpose
Manages player character's physical appearance, fitness, and grooming. Affects attractiveness to NPCs, adult content income, energy levels, and reputation. Supports vanity mechanics and relationship compatibility based on physical preferences.

Interface
SetBodyType
csharp
void SetBodyType(BodyType type)
Purpose: Set player's body type.

Parameters:

type: BodyType enum (Slim, Athletic, Average, Curvy, Heavy)
Side effects:

Updates character model
Affects NPC attraction calculations
Affects certain job opportunities (modeling, some sex work)
Broadcasts OnBodyTypeChanged event
ModifyFitness
csharp
void ModifyFitness(float delta)
Purpose: Change fitness level (working out, neglect, diet).

Parameters:

delta: Amount to change (+10 from workout, -5 from poor diet, etc.)
Side effects:

Updates fitness stat (0-100)
Affects energy max
Affects attractiveness
Changes body type if thresholds crossed
Broadcasts OnFitnessChanged event
SetGrooming
csharp
void SetGrooming(GroomingLevel level)
Purpose: Set grooming/hygiene level.

Parameters:

level: GroomingLevel enum (Unkempt, Basic, Well-Groomed, Professional, Glamorous)
Side effects:

Affects professional reputation
Affects social reputation
Affects NPC attraction
Costs money for higher levels (haircuts, makeup, etc.)
GetAttractiveness
csharp
float GetAttractiveness(string npcId = null)
Purpose: Calculate attractiveness score (optionally for specific NPC).

Parameters:

npcId: Optional - calculate for specific NPC's preferences
Returns: Attractiveness score (0-100)

Calculation:

Base from body type
Modified by fitness
Modified by grooming
Modified by clothing
Modified by NPC preferences (if specified)
GetEnergyMax
csharp
float GetEnergyMax()
Purpose: Calculate maximum energy based on fitness.

Returns: Energy max (100 base + fitness modifiers)

Formula: 100 + (fitness * 0.2) // Fitness 100 = 120 max energy // Fitness 0 = 100 max energy

Events
csharp
event Action<BodyType> OnBodyTypeChanged
event Action<float> OnFitnessChanged         // (newFitness)
event Action<GroomingLevel> OnGroomingChanged
event Action<float> OnAttractivenessChanged
Data Structures
BodyType (enum)
csharp
enum BodyType {
    Slim,      // Low muscle, low fat
    Athletic,  // High muscle, low fat
    Average,   // Medium muscle, medium fat
    Curvy,     // Low muscle, higher fat (attractive to some NPCs)
    Heavy      // Low muscle, high fat
}
GroomingLevel (enum)
csharp
enum GroomingLevel {
    Unkempt,      // Negative effects across the board
    Basic,        // Neutral
    WellGroomed,  // Positive social effects
    Professional, // Positive professional effects
    Glamorous     // Maximum attractiveness, high cost
}
BodyState
csharp
struct BodyState {
    BodyType bodyType;
    float fitness;              // 0-100
    GroomingLevel grooming;
    
    // Derived stats
    float attractiveness;       // Calculated
    float energyMax;           // Calculated
    
    // Costs
    float groomingCost;        // Monthly grooming maintenance
}
Dependencies
Reads from:

TimeSystem (fitness decays slowly without exercise)
EconomySystem (grooming costs money)
ClothingSystem (clothing affects attractiveness)
Writes to:

TimeSystem (modifies energy max)
ReputationSystem (grooming affects professional/social rep)
RelationshipSystem (attractiveness affects NPC reactions)
Subscribed to by:

UI (display fitness, grooming level, attractiveness)
AdultContentSystem (attractiveness affects OnlyFans income)
JobSystem (some jobs require fitness/grooming)
Implementation Notes
Attractiveness Calculation:

csharp
float GetAttractiveness(string npcId = null) {
    float base_attractiveness = 50f;
    
    // Body type contributes
    switch (bodyState.bodyType) {
        case BodyType.Slim: base_attractiveness += 10f; break;
        case BodyType.Athletic: base_attractiveness += 20f; break;
        case BodyType.Average: base_attractiveness += 5f; break;
        case BodyType.Curvy: base_attractiveness += 15f; break;
        case BodyType.Heavy: base_attractiveness -= 5f; break;
    }
    
    // Fitness contributes
    base_attractiveness += (bodyState.fitness * 0.3f);  // +30 at fitness 100
    
    // Grooming contributes
    switch (bodyState.grooming) {
        case GroomingLevel.Unkempt: base_attractiveness -= 20f; break;
        case GroomingLevel.Basic: break;  // Neutral
        case GroomingLevel.WellGroomed: base_attractiveness += 10f; break;
        case GroomingLevel.Professional: base_attractiveness += 15f; break;
        case GroomingLevel.Glamorous: base_attractiveness += 25f; break;
    }
    
    // Clothing contributes (if wearing something)
    ClothingItem clothing = ClothingSystem.Instance.GetCurrentClothing();
    if (clothing != null) {
        base_attractiveness += clothing.vanityValue * 0.2f;
    }
    
    // NPC-specific preferences
    if (npcId != null) {
        NPC npc = RelationshipSystem.Instance.GetNPC(npcId);
        float preferenceBonus = CalculatePreferenceBonus(npc, bodyState.bodyType);
        base_attractiveness += preferenceBonus;
    }
    
    return Mathf.Clamp(base_attractiveness, 0f, 100f);
}

float CalculatePreferenceBonus(NPC npc, BodyType playerBody) {
    if (!npc.bodyPreferences.ContainsKey(playerBody)) return 0f;
    
    // NPC preference is 0-1 scale, convert to bonus
    float preference = npc.bodyPreferences[playerBody];
    return (preference - 0.5f) * 40f;  // -20 to +20 based on preference
}
Fitness Decay:

csharp
void Update() {
    float gameHoursPassed = TimeSystem.Instance.GetDeltaGameHours();
    
    // Fitness decays slowly without maintenance
    const float DECAY_RATE = 0.5f;  // 0.5 points per game day
    bodyState.fitness -= DECAY_RATE * (gameHoursPassed / 24f);
    bodyState.fitness = Mathf.Max(0, bodyState.fitness);
    
    // Body type changes based on fitness thresholds
    UpdateBodyTypeFromFitness();
}

void UpdateBodyTypeFromFitness() {
    BodyType newType = bodyState.bodyType;
    
    if (bodyState.fitness > 80f) {
        newType = BodyType.Athletic;
    }
    else if (bodyState.fitness > 60f) {
        newType = bodyState.bodyType == BodyType.Heavy ? BodyType.Average : BodyType.Slim;
    }
    else if (bodyState.fitness > 40f) {
        newType = BodyType.Average;
    }
    else if (bodyState.fitness > 20f) {
        newType = bodyState.bodyType == BodyType.Slim ? BodyType.Average : BodyType.Curvy;
    }
    else {
        newType = BodyType.Heavy;
    }
    
    if (newType != bodyState.bodyType) {
        SetBodyType(newType);
    }
}
Grooming Costs:

csharp
void SetGrooming(GroomingLevel level) {
    GroomingLevel oldLevel = bodyState.grooming;
    bodyState.grooming = level;
    
    // One-time cost to achieve level
    float cost = GetGroomingCost(level);
    EconomySystem.Instance.DeductExpense(
        playerId,
        cost,
        ExpenseType.Personal,
        $"Grooming upgrade to {level}"
    );
    
    // Set monthly maintenance cost
    bodyState.groomingCost = GetMaintenanceCost(level);
    
    // Reputation effects
    ApplyGroomingReputationEffects(oldLevel, level);
    
    OnGroomingChanged?.Invoke(level);
}

float GetGroomingCost(GroomingLevel level) {
    switch (level) {
        case GroomingLevel.Unkempt: return 0f;
        case GroomingLevel.Basic: return 50f;
        case GroomingLevel.WellGroomed: return 200f;
        case GroomingLevel.Professional: return 500f;
        case GroomingLevel.Glamorous: return 1500f;
        default: return 0f;
    }
}

float GetMaintenanceCost(GroomingLevel level) {
    switch (level) {
        case GroomingLevel.Unkempt: return 0f;
        case GroomingLevel.Basic: return 20f;       // Monthly
        case GroomingLevel.WellGroomed: return 100f;
        case GroomingLevel.Professional: return 300f;
        case GroomingLevel.Glamorous: return 800f;
        default: return 0f;
    }
}
```

---

## **Edge Cases**

1. **Fitness below 0:** Clamp to 0, apply health penalties
2. **Attractiveness calculation with no NPC:** Use general attractiveness (no preference bonus)
3. **Grooming downgrade:** Costs nothing, just degrades appearance
4. **Body type transition:** Smooth transition, not instant (visual model interpolation)
5. **Can't afford grooming maintenance:** Automatically downgrades to affordable level

---

## **Testing Checklist**
```
[ ] SetBodyType changes character model
[ ] ModifyFitness updates fitness stat
[ ] Fitness affects energy max correctly
[ ] Fitness decays over time without exercise
[ ] Body type changes when fitness crosses thresholds
[ ] GetAttractiveness calculates correctly
[ ] NPC body preferences affect attractiveness calculation
[ ] SetGrooming costs money
[ ] Grooming affects professional/social reputation
[ ] Grooming maintenance costs deducted monthly
[ ] Can't afford grooming = automatic downgrade
[ ] OnBodyTypeChanged fires correctly