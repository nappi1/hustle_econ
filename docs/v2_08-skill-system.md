# V2 Update (Aligned to Current Codebase)

This V2 spec reflects the current implementation. See docs/IMPLEMENTATION_DISCREPANCIES.md for cross-system gaps.

## Implementation Notes

- Skill improvements are driven by ActivitySystem calls; mapping is heuristic by minigameId.
- No NPC-specific unlock APIs; unlocks still log via Debug.Log.

---
8. SKILL SYSTEM
Purpose
Tracks player proficiency in various skills (driving, cooking, persuasion, stealth, etc.). Skills improve through use, affect minigame performance, unlock opportunities, and decay if unused. Supports "learning by doing" progression.

Interface
GetSkillLevel
csharp
float GetSkillLevel(SkillType skill)
Purpose: Returns player's current skill level.

Parameters:

skill: Which skill
Returns: Skill level (0-100)

ModifySkill
csharp
void ModifySkill(SkillType skill, float delta, string reason)
Purpose: Change skill level (practice, use, decay).

Parameters:

skill: Which skill
delta: Amount to change
reason: Why it changed (for logging)
Side effects:

Updates skill level (clamped 0-100)
Checks for skill milestones (25, 50, 75, 100)
Broadcasts OnSkillChanged event
May unlock new opportunities
ImproveSkillFromUse
csharp
void ImproveSkillFromUse(SkillType skill, float baseGain)
Purpose: Skill improves from doing activity.

Parameters:

skill: Which skill
baseGain: Base improvement amount
Side effects:

Improvement scales with current level (lower skill = faster gain)
Updates skill XP
Broadcasts OnSkillImproved event
Formula: actualGain = baseGain * (1.0 - (currentSkill / 150)) // At skill 0: gain = baseGain // At skill 75: gain = baseGain * 0.5 // At skill 100: gain still possible but very slow

CheckSkillRequirement
csharp
bool CheckSkillRequirement(SkillType skill, float required)
Purpose: Check if player meets skill threshold.

Parameters:

skill: Which skill
required: Minimum level needed
Returns: true if player skill >= required

Events
csharp
event Action<SkillType, float, float> OnSkillChanged     // (skill, oldLevel, newLevel)
event Action<SkillType, float> OnSkillImproved           // (skill, gainAmount)
event Action<SkillType, int> OnSkillMilestone            // (skill, milestone) - fires at 25, 50, 75, 100
event Action<SkillType> OnSkillDecayed                   // (skill)
Data Structures
SkillType (enum)
csharp
enum SkillType {
    Driving,
    Stealth,
    Persuasion,
    Cooking,
    Fitness,
    Mechanical,    // Fixing things
    Cleaning,      // Janitorial work
    Social,        // Networking, charm
    Trading,       // Stock market, negotiation
    Programming    // Hacking, tech work (future)
}
Skill
csharp
struct Skill {
    SkillType type;
    float level;               // 0-100
    float xp;                  // Tracks partial progress
    DateTime lastUsed;
    float decayRate;           // How fast it decays if unused
    bool canDecay;             // Some skills don't decay (riding bike)
}
SkillProfile
csharp
class SkillProfile {
    string playerId;
    Dictionary<SkillType, Skill> skills;
}
Dependencies
Reads from:

TimeSystem (skill decay over time)
Writes to:

None directly (broadcasts events)
Subscribed to by:

JobSystem (skills unlock jobs)
MinigameSystem (skills affect performance)
UI (skill display, progression)
Implementation Notes
Skill Improvement:

csharp
void ImproveSkillFromUse(SkillType skillType, float baseGain) {
    Skill skill = GetSkill(skillType);
    
    // Diminishing returns as skill increases
    float scalingFactor = 1.0f - (skill.level / 150f);
    float actualGain = baseGain * Mathf.Max(0.1f, scalingFactor);  // Min 10% of base gain
    
    skill.xp += actualGain;
    skill.lastUsed = TimeSystem.Instance.GetCurrentTime();
    
    // Level up if XP threshold reached
    float xpNeeded = 100f;  // Fixed XP per level
    if (skill.xp >= xpNeeded) {
        float oldLevel = skill.level;
        skill.level = Mathf.Min(100f, skill.level + 1f);
        skill.xp -= xpNeeded;
        
        // Check milestones
        CheckMilestone(skillType, oldLevel, skill.level);
        
        OnSkillChanged?.Invoke(skillType, oldLevel, skill.level);
    }
    
    OnSkillImproved?.Invoke(skillType, actualGain);
}

void CheckMilestone(SkillType skillType, float oldLevel, float newLevel) {
    int[] milestones = { 25, 50, 75, 100 };
    
    foreach (int milestone in milestones) {
        if (oldLevel < milestone && newLevel >= milestone) {
            OnSkillMilestone?.Invoke(skillType, milestone);
            
            // Unlock opportunities
            UnlockSkillOpportunities(skillType, milestone);
        }
    }
}

void UnlockSkillOpportunities(SkillType skillType, int milestone) {
    // Examples:
    if (skillType == SkillType.Driving && milestone == 50) {
        // Unlock rideshare gig work
        JobSystem.Instance.UnlockJob("rideshare_driver");
    }
    
    if (skillType == SkillType.Stealth && milestone == 75) {
        // Unlock high-level heists (future content)
    }
    
    if (skillType == SkillType.Social && milestone == 50) {
        // Unlock networking events, better relationship gains
    }
}
Skill Decay:

csharp
void Update() {
    DateTime now = TimeSystem.Instance.GetCurrentTime();
    
    foreach (var skillPair in playerSkills.skills) {
        Skill skill = skillPair.Value;
        
        if (!skill.canDecay) continue;
        
        // Check time since last use
        float daysSinceUse = (float)(now - skill.lastUsed).TotalDays;
        
        // Decay starts after 30 days
        if (daysSinceUse > 30f) {
            float decayAmount = skill.decayRate * (daysSinceUse - 30f);
            
            // Never decay below 60% of max achieved
            float minLevel = skill.level * 0.6f;
            float oldLevel = skill.level;
            skill.level = Mathf.Max(minLevel, skill.level - decayAmount);
            
            if (skill.level < oldLevel) {
                OnSkillDecayed?.Invoke(skill.type);
                OnSkillChanged?.Invoke(skill.type, oldLevel, skill.level);
            }
        }
    }
}
Skill Relearning (Faster Second Time):

csharp
void ImproveSkillFromUse(SkillType skillType, float baseGain) {
    Skill skill = GetSkill(skillType);
    
    // If skill has decayed, relearning is 2x faster
    float relearningBonus = 1.0f;
    if (skill.level < skill.peakLevel * 0.9f) {  // If dropped more than 10% from peak
        relearningBonus = 2.0f;  // Muscle memory
    }
    
    float actualGain = baseGain * relearningBonus * scalingFactor;
    
    // ... rest of improvement logic
}
```

---

## **Edge Cases**

1. **Skill at 100:** Can still gain XP (very slowly), no further levels
2. **Decay below 0:** Clamp to 0, but remembers skill existed (can relearn faster)
3. **Using skill while intoxicated:** Gain reduced (impaired learning)
4. **Skill requirement exactly met:** Player qualifies
5. **Multiple skills improve from one activity:** Possible (driving + stealth from getaway)

---

## **Testing Checklist**
```
[ ] GetSkillLevel returns correct level
[ ] ModifySkill updates level correctly
[ ] ImproveSkillFromUse scales with current level (diminishing returns)
[ ] Skill XP accumulates and levels up at threshold
[ ] Milestones trigger at 25, 50, 75, 100
[ ] Skill decay starts after 30 days unused
[ ] Decay never drops below 60% of peak
[ ] Relearning is 2x faster than initial learning
[ ] OnSkillMilestone unlocks opportunities
[ ] CheckSkillRequirement correctly evaluates thresholds
[ ] Skills used during activities improve
[ ] Skills persist across save/load
