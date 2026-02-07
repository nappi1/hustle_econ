# V2 Update (Aligned to Current Codebase)

This V2 spec reflects the current implementation. See docs/IMPLEMENTATION_DISCREPANCIES.md for cross-system gaps.

## Implementation Notes

- Reputation flows remain as implemented; no spec deltas recorded in discrepancies.

---
4. REPUTATION SYSTEM
Purpose
Tracks player standing across multiple domains (legal record, criminal notoriety, professional credibility, social connections). Actions modify multiple reputation tracks simultaneously, creating realistic consequences. Reputation gates opportunities and affects NPC reactions.

Interface
GetReputation
csharp
float GetReputation(string playerId, ReputationTrack track)
Purpose: Returns player's reputation score for a specific track.

Parameters:

playerId: Player ID
track: Which reputation (Legal, Criminal, Professional, Social)
Returns: Score from 0-100

ModifyReputation
csharp
void ModifyReputation(string playerId, ReputationTrack track, float delta, string reason)
Purpose: Change reputation score.

Parameters:

playerId: Player ID
track: Which reputation to modify
delta: Amount to change (positive or negative)
reason: Why it changed (for logging/debugging)
Side effects:

Clamps score to 0-100
Records change in history
Broadcasts OnReputationChanged event
May trigger threshold events (e.g., reputation drops below 30 ??? cops investigate)
ModifyMultipleReputations
csharp
void ModifyMultipleReputations(string playerId, Dictionary<ReputationTrack, float> changes, string reason)
Purpose: Modify several reputations at once (common - actions affect multiple tracks).

Parameters:

playerId: Player ID
changes: Dictionary of track ??? delta
reason: Why
Example:

csharp
// Getting promoted affects multiple reputations
ModifyMultipleReputations(playerId, new Dictionary<ReputationTrack, float> {
    { ReputationTrack.Professional, +10f },
    { ReputationTrack.Social, +5f },
    { ReputationTrack.Criminal, -5f }  // Less time for crime
}, "Promoted to supervisor");
CheckThreshold
csharp
bool CheckThreshold(string playerId, ReputationTrack track, float threshold, ThresholdComparison comparison)
Purpose: Check if reputation meets a requirement.

Parameters:

playerId: Player ID
track: Which reputation
threshold: Required score
comparison: GreaterThan, LessThan, EqualOrGreater, EqualOrLess
Returns: true if condition met

Example:

csharp
// Can player apply for police job?
bool canApply = CheckThreshold(playerId, ReputationTrack.Legal, 70f, ThresholdComparison.GreaterThan);
GetReputationModifiers
csharp
Dictionary<string, float> GetReputationModifiers(string playerId, ReputationTrack track)
Purpose: Get active modifiers affecting a reputation (temporary boosts/penalties).

Parameters:

playerId: Player ID
track: Which reputation
Returns: Dictionary of modifier name ??? value

Example:

csharp
// Player might have:
// "recent_arrest": -10
// "charity_donation": +5
// "probation": -15
AddTemporaryModifier
csharp
string AddTemporaryModifier(string playerId, ReputationTrack track, float modifier, float durationDays, string description)
Purpose: Add temporary reputation modifier (probation, publicity boost, etc.).

Parameters:

playerId: Player ID
track: Which reputation
modifier: Amount (positive or negative)
durationDays: How long it lasts (game days)
description: What this is
Returns: Modifier ID (for early removal)

Side effects:

Applies modifier immediately
Schedules automatic removal after duration
Broadcasts OnReputationChanged event
RemoveModifier
csharp
bool RemoveModifier(string playerId, string modifierId)
Purpose: Remove a temporary modifier early.

Parameters:

playerId: Player ID
modifierId: ID from AddTemporaryModifier
Returns: true if removed, false if doesn't exist

Events
csharp
event Action<string, ReputationTrack, float, float> OnReputationChanged  
// (playerId, track, oldValue, newValue)

event Action<string, ReputationTrack, float> OnThresholdCrossed
// (playerId, track, threshold) - fires when crossing significant thresholds
Data Structures
ReputationTrack (enum)
csharp
enum ReputationTrack {
    Legal,        // Clean record (100) to wanted criminal (0)
    Criminal,     // Unknown (0) to notorious (100)
    Professional, // Unreliable (0) to industry leader (100)
    Social        // Outcast (0) to well-connected (100)
}
ThresholdComparison (enum)
csharp
enum ThresholdComparison {
    GreaterThan,
    LessThan,
    EqualOrGreater,
    EqualOrLess
}
ReputationModifier
csharp
class ReputationModifier {
    string id;
    ReputationTrack track;
    float value;
    DateTime expiresAt;      // When this modifier ends
    string description;
    bool isPermanent;        // If true, never expires
}
ReputationProfile
csharp
class ReputationProfile {
    string playerId;
    Dictionary<ReputationTrack, float> baseScores;        // Permanent scores
    Dictionary<string, ReputationModifier> activeModifiers; // Temporary modifiers
    List<ReputationChange> history;                       // For debugging/save
}
ReputationChange (history)
csharp
struct ReputationChange {
    DateTime timestamp;
    ReputationTrack track;
    float delta;
    string reason;
}
Dependencies
Reads from:

TimeSystem (for expiring temporary modifiers)
Writes to:

None directly (broadcasts events)
Subscribed to by:

JobSystem (check reputation requirements for jobs)
RelationshipSystem (NPCs react to reputation changes)
DetectionSystem (low legal rep = cops watch you more)
EconomySystem (professional rep affects salary, social rep affects networking)
Implementation Notes
Reputation Calculation:

csharp
float GetReputation(string playerId, ReputationTrack track) {
    ReputationProfile profile = GetProfile(playerId);
    float base_score = profile.baseScores[track];
    
    // Apply active modifiers
    float modifierSum = profile.activeModifiers.Values
        .Where(m => m.track == track)
        .Sum(m => m.value);
    
    return Mathf.Clamp(base_score + modifierSum, 0f, 100f);
}
Modifier Expiration:

csharp
void Update() {
    DateTime now = TimeSystem.Instance.GetCurrentTime();
    
    foreach (var profile in allProfiles.Values) {
        var expiredModifiers = profile.activeModifiers.Values
            .Where(m => !m.isPermanent && m.expiresAt <= now)
            .ToList();
        
        foreach (var mod in expiredModifiers) {
            profile.activeModifiers.Remove(mod.id);
            OnReputationChanged?.Invoke(
                profile.playerId,
                mod.track,
                GetReputation(profile.playerId, mod.track) + mod.value, // old
                GetReputation(profile.playerId, mod.track)              // new
            );
        }
    }
}
Multi-Track Modifications:

csharp
// Example: Getting arrested affects multiple tracks
void OnPlayerArrested(string playerId) {
    ModifyMultipleReputations(playerId, new Dictionary<ReputationTrack, float> {
        { ReputationTrack.Legal, -20f },      // Criminal record
        { ReputationTrack.Criminal, +10f },   // Street cred
        { ReputationTrack.Professional, -15f }, // Harder to get job
        { ReputationTrack.Social, -10f }      // Family disappointed
    }, "Arrested for drug possession");
}
Threshold Detection:

csharp
void ModifyReputation(string playerId, ReputationTrack track, float delta, string reason) {
    float oldValue = GetReputation(playerId, track);
    
    // Modify base score
    ReputationProfile profile = GetProfile(playerId);
    profile.baseScores[track] = Mathf.Clamp(profile.baseScores[track] + delta, 0f, 100f);
    
    float newValue = GetReputation(playerId, track);
    
    // Check for threshold crossings
    CheckThresholdCrossings(playerId, track, oldValue, newValue);
    
    // Broadcast
    OnReputationChanged?.Invoke(playerId, track, oldValue, newValue);
}

void CheckThresholdCrossings(string playerId, ReputationTrack track, float oldValue, float newValue) {
    // Significant thresholds
    float[] thresholds = { 20f, 30f, 50f, 70f, 80f };
    
    foreach (float threshold in thresholds) {
        // Did we cross this threshold?
        if ((oldValue < threshold && newValue >= threshold) ||
            (oldValue >= threshold && newValue < threshold)) {
            OnThresholdCrossed?.Invoke(playerId, track, threshold);
        }
    }
}
Reputation-Gated Opportunities:

csharp
// Example: Police job requires high legal reputation
bool CanApplyForPoliceJob(string playerId) {
    return CheckThreshold(playerId, ReputationTrack.Legal, 70f, ThresholdComparison.GreaterThan);
}

// Example: Cartel deals require high criminal reputation
bool CanAccessCartel(string playerId) {
    return CheckThreshold(playerId, ReputationTrack.Criminal, 60f, ThresholdComparison.GreaterThan);
}
```

---

## **Edge Cases**

1. **Modifier makes reputation go outside 0-100:** Clamp to valid range
2. **Remove modifier that doesn't exist:** Return false, log warning
3. **Add modifier with 0 duration:** Treat as permanent
4. **Multiple modifiers affecting same track:** Sum them all
5. **Reputation exactly at threshold:** EqualOrGreater/EqualOrLess handle this

---

## **Testing Checklist**
```
[ ] All tracks default to reasonable starting values
[ ] ModifyReputation clamps to 0-100
[ ] GetReputation returns base + modifiers correctly
[ ] Modifiers expire at correct game time
[ ] OnReputationChanged fires when reputation changes
[ ] OnThresholdCrossed fires when crossing 20, 30, 50, 70, 80
[ ] CheckThreshold correctly evaluates all comparison types
[ ] ModifyMultipleReputations changes all specified tracks
[ ] AddTemporaryModifier applies immediately
[ ] RemoveModifier removes before expiration
[ ] Reputation persists across save/load
[ ] Multiple modifiers stack correctly
