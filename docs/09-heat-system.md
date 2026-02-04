9. HEAT SYSTEM
Purpose
Tracks immediate attention/suspicion from authorities and investigators. Separate from Reputation (which is long-term standing). Heat represents "how hot you are right now" - increased police presence, more frequent checks, heightened surveillance. Decays over time if player lays low, but spikes from illegal activities, flashy behavior, and patterns.

Interface
GetHeatLevel
csharp
float GetHeatLevel()
Purpose: Returns current heat level.

Returns: Heat from 0 (no attention) to 100 (maximum surveillance)

AddHeat
csharp
void AddHeat(float amount, string source)
Purpose: Increase heat (committed crime, flashy purchase, suspicious behavior).

Parameters:

amount: How much heat to add
source: Why heat increased (for tracking)
Side effects:

Updates heat level
May trigger heat threshold events (investigations, raids)
Broadcasts OnHeatIncreased event
ReduceHeat
csharp
void ReduceHeat(float amount, string reason)
Purpose: Manually reduce heat (bribe, lay low, move away).

Parameters:

amount: How much to reduce
reason: Why (bribe, relocation, time)
Side effects:

Updates heat level
Broadcasts OnHeatDecreased event
GetHeatSources
csharp
Dictionary<string, float> GetHeatSources()
```
**Purpose:** Get breakdown of what's causing heat.

**Returns:** Dictionary of source → heat amount

**Example:**
```
{
  "drug_dealing": 30,
  "flashy_purchases": 15,
  "recent_arrest": 20,
  "frequent_cash_deposits": 10
}
TriggerInvestigation
csharp
void TriggerInvestigation(InvestigationType type)
Purpose: Heat threshold reached, authorities investigate.

Parameters:

type: InvestigationType enum (Surveillance, Audit, Raid)
Side effects:

Increases detection sensitivity
May freeze assets (audit)
May trigger arrest (raid)
Broadcasts OnInvestigationTriggered event
Events
csharp
event Action<float, string> OnHeatIncreased      // (amount, source)
event Action<float> OnHeatDecreased              // (amount)
event Action<float> OnHeatThresholdCrossed       // (threshold) - at 30, 50, 70, 90
event Action<InvestigationType> OnInvestigationTriggered
event Action OnHeatCleared                        // Heat returned to 0
Data Structures
HeatState
csharp
struct HeatState {
    float level;                                  // 0-100
    Dictionary<string, float> sources;            // What's causing heat
    DateTime lastIncrease;
    float decayRate;                              // How fast heat decays
    List<HeatModifier> activeModifiers;           // Temporary heat increases
}
HeatModifier
csharp
struct HeatModifier {
    string source;
    float amount;
    DateTime expiresAt;                           // When this heat expires
    bool isPermanent;                             // Some heat doesn't decay
}
InvestigationType (enum)
csharp
enum InvestigationType {
    Surveillance,     // Increased cop presence, more checks
    IRS_Audit,        // Financial investigation
    Raid,             // Police raid on property
    Arrest_Warrant    // Active warrant issued
}
Dependencies
Reads from:

TimeSystem (heat decays over time)
EconomySystem (large cash transactions increase heat)
ReputationSystem (criminal reputation affects heat gain rate)
Writes to:

DetectionSystem (high heat = more observers, higher sensitivity)
EconomySystem (audit can freeze assets)
Subscribed to by:

DetectionSystem (adjusts patrol frequency based on heat)
JobSystem (some jobs unavailable at high heat)
UI (heat meter display)
Implementation Notes
Heat Sources and Amounts:

csharp
// Common heat sources
public static class HeatSources {
    public const string DRUG_DEALING = "drug_dealing";
    public const string FLASHY_PURCHASE = "flashy_purchase";
    public const string ARREST = "recent_arrest";
    public const string CASH_DEPOSIT = "cash_deposit";
    public const string SUSPICIOUS_INCOME = "suspicious_income";
    public const string REPEAT_OFFENDER = "repeat_offender";
}

// Heat amounts by source
void AddHeat(float amount, string source) {
    heatState.level += amount;
    heatState.level = Mathf.Clamp(heatState.level, 0f, 100f);
    heatState.lastIncrease = TimeSystem.Instance.GetCurrentTime();
    
    // Track source
    if (!heatState.sources.ContainsKey(source)) {
        heatState.sources[source] = 0f;
    }
    heatState.sources[source] += amount;
    
    // Check thresholds
    CheckHeatThresholds(heatState.level - amount, heatState.level);
    
    OnHeatIncreased?.Invoke(amount, source);
}
Heat Decay:

csharp
void Update() {
    if (heatState.level > 0) {
        float deltaTime = Time.deltaTime;
        
        // Base decay rate: 1 point per game day
        float baseDecay = 1f / 24f;  // Per game hour
        float gameHoursPassed = TimeSystem.Instance.GetDeltaGameHours();
        
        // Decay faster if laying low (no recent illegal activity)
        float timeSinceIncrease = (TimeSystem.Instance.GetCurrentTime() - heatState.lastIncrease).TotalDays;
        float decayMultiplier = 1f;
        
        if (timeSinceIncrease > 7f) {
            decayMultiplier = 2f;  // Double decay after 1 week clean
        }
        if (timeSinceIncrease > 30f) {
            decayMultiplier = 3f;  // Triple decay after 1 month clean
        }
        
        float decayAmount = baseDecay * decayMultiplier * gameHoursPassed;
        
        float oldHeat = heatState.level;
        heatState.level = Mathf.Max(0f, heatState.level - decayAmount);
        
        if (heatState.level == 0f && oldHeat > 0f) {
            OnHeatCleared?.Invoke();
        }
        
        if (oldHeat != heatState.level) {
            OnHeatDecreased?.Invoke(decayAmount);
        }
    }
}
Threshold Events:

csharp
void CheckHeatThresholds(float oldLevel, float newLevel) {
    float[] thresholds = { 30f, 50f, 70f, 90f };
    
    foreach (float threshold in thresholds) {
        // Crossed threshold going up
        if (oldLevel < threshold && newLevel >= threshold) {
            OnHeatThresholdCrossed?.Invoke(threshold);
            HandleThresholdEffects(threshold);
        }
    }
}

void HandleThresholdEffects(float threshold) {
    if (threshold >= 30f) {
        // Increased cop presence
        DetectionSystem.Instance.IncreasePatrolFrequency(1.2f);
    }
    
    if (threshold >= 50f) {
        // Active surveillance
        TriggerInvestigation(InvestigationType.Surveillance);
    }
    
    if (threshold >= 70f) {
        // IRS audit if suspicious income
        float legitimacy = EconomySystem.Instance.GetLegitimacyScore(playerId);
        if (legitimacy < 0.6f) {
            TriggerInvestigation(InvestigationType.IRS_Audit);
        }
    }
    
    if (threshold >= 90f) {
        // Imminent raid or arrest warrant
        bool hasEvidence = CheckForEvidence();
        if (hasEvidence) {
            TriggerInvestigation(InvestigationType.Raid);
        } else {
            TriggerInvestigation(InvestigationType.Arrest_Warrant);
        }
    }
}
Investigation Effects:

csharp
void TriggerInvestigation(InvestigationType type) {
    OnInvestigationTriggered?.Invoke(type);
    
    switch (type) {
        case InvestigationType.Surveillance:
            // More cops, higher detection sensitivity
            DetectionSystem.Instance.IncreasePatrolFrequency(1.5f);
            DetectionSystem.Instance.IncreaseDetectionSensitivity(1.3f);
            
            // Notification to player
            EventSystem.Instance.TriggerEvent("surveillance_notice", null);
            break;
        
        case InvestigationType.IRS_Audit:
            // Freeze portion of assets
            float playerBalance = EconomySystem.Instance.GetBalance(playerId);
            float frozenAmount = playerBalance * 0.3f;  // Freeze 30%
            
            EconomySystem.Instance.FreezeAssets(playerId, frozenAmount);
            
            // Investigation takes time, then resolves
            TimeSystem.Instance.ScheduleEvent(
                TimeSystem.Instance.GetCurrentTime().AddDays(30),
                () => ResolveAudit(),
                "IRS Audit Resolution"
            );
            break;
        
        case InvestigationType.Raid:
            // Police raid property
            // Seize illegal items, arrest if present
            EventSystem.Instance.TriggerEvent("police_raid", null);
            
            // Outcome depends on what they find
            bool foundEvidence = CheckForEvidence();
            if (foundEvidence) {
                // Arrested
                CriminalRecordSystem.Instance.AddOffense(playerId, OffenseType.Possession, "Raid evidence");
                TimeSystem.Instance.AdvanceTime(24f * 60f * 30f);  // 30 days jail
                
                // Lose job, relationships damaged
                JobSystem.Instance.FireAllJobs(playerId, "Arrested");
            }
            
            // Heat reduced after raid (investigated)
            heatState.level *= 0.5f;
            break;
        
        case InvestigationType.Arrest_Warrant:
            // Active warrant - if caught by cops, instant arrest
            activeWarrant = true;
            
            // Higher cop presence
            DetectionSystem.Instance.IncreasePatrolFrequency(2f);
            break;
    }
}

void ResolveAudit() {
    float legitimacy = EconomySystem.Instance.GetLegitimacyScore(playerId);
    
    if (legitimacy > 0.7f) {
        // Clean audit - assets unfrozen
        EconomySystem.Instance.UnfreezeAssets(playerId);
        EventSystem.Instance.TriggerEvent("audit_cleared", null);
    } else {
        // Failed audit - fined, assets partially seized
        float balance = EconomySystem.Instance.GetBalance(playerId);
        float fine = balance * 0.2f;  // 20% fine
        
        EconomySystem.Instance.DeductExpense(playerId, fine, ExpenseType.Fine, "Tax evasion penalty");
        EconomySystem.Instance.UnfreezeAssets(playerId);
        
        // Criminal record
        CriminalRecordSystem.Instance.AddOffense(playerId, OffenseType.TaxEvasion, "Failed IRS audit");
    }
}
Heat from Economic Activity:

csharp
// EconomySystem calls this when suspicious transactions occur
public void OnSuspiciousTransaction(float amount, string source) {
    // Large cash deposits increase heat
    if (amount > 5000f) {
        float heatAmount = (amount / 10000f) * 5f;  // $10k = 5 heat
        AddHeat(heatAmount, HeatSources.CASH_DEPOSIT);
    }
    
    // Illegal income increases heat
    if (source == IncomeSource.DrugSale || source == IncomeSource.SexWork) {
        AddHeat(2f, HeatSources.SUSPICIOUS_INCOME);
    }
}

// AdultContentSystem / VanitySystem calls this
public void OnFlashyPurchase(float vanityValue) {
    // High vanity purchases increase heat
    if (vanityValue > 70f) {
        float heatAmount = (vanityValue / 100f) * 10f;  // 100 vanity = 10 heat
        AddHeat(heatAmount, HeatSources.FLASHY_PURCHASE);
    }
}
```

---

## **Edge Cases**

1. **Heat at 100:** Max surveillance, raids likely, very hard to operate
2. **Heat decay while still committing crimes:** Decay rate reduced
3. **Multiple investigations simultaneously:** All can be active (audit + surveillance)
4. **Bribing to reduce heat:** Costs money, reduces heat by 20-30 points
5. **Moving to new city (future):** Resets heat but not reputation

---

## **Testing Checklist**
```
[ ] GetHeatLevel returns current level
[ ] AddHeat increases level and tracks sources
[ ] Heat decays over time when laying low
[ ] Decay accelerates after 1+ weeks clean
[ ] Threshold at 30 increases patrol frequency
[ ] Threshold at 50 triggers surveillance
[ ] Threshold at 70 triggers audit if low legitimacy
[ ] Threshold at 90 triggers raid or warrant
[ ] Surveillance increases detection sensitivity
[ ] Audit freezes assets for 30 days
[ ] Failed audit results in fine and criminal record
[ ] Raid seizes evidence and arrests if found
[ ] Flashy purchases increase heat
[ ] Large cash deposits increase heat
[ ] Heat clears completely after extended clean period
[ ] OnHeatCleared fires when heat returns to 0