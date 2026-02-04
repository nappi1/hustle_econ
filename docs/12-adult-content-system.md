12. ADULT CONTENT SYSTEM (EXPANDED)
Purpose
Manages all adult content paths (streaming, escorting, sugar dynamics), clothing effects on income/reputation, intimacy mechanics, and consequences (blackmail, discovery, professional damage). Treats adult content as systemic economic choices with real stakes, not gratuitous content.

NEW Additions to Interface:
EquipClothing
csharp
void EquipClothing(string clothingId)
Purpose: Player equips clothing item.

Parameters:

clothingId: Clothing entity ID
Side effects:

Updates player appearance
Applies clothing modifiers (professional rep, social rep, vanity, OnlyFans income)
NPCs react based on context (office vs club vs home)
Broadcasts OnClothingChanged event
GetClothingModifier
csharp
float GetClothingModifier(ClothingModifierType type)
Purpose: Get current clothing's effect on specific metric.

Parameters:

type: ProfessionalRep, SocialRep, OnlyFansIncome, Attractiveness
Returns: Modifier value

TriggerBlackmail
csharp
void TriggerBlackmail(string blackmailerId, BlackmailType type, float demand)
Purpose: NPC or system triggers blackmail scenario.

Parameters:

blackmailerId: Who's blackmailing (NPC or "anonymous")
type: What they have (NudePhotos, StreamContent, WitnessedSexWork)
demand: Money they want
Side effects:

Creates blackmail event
Player chooses: Pay, Refuse (content leaked), Violent response
Broadcasts OnBlackmailTriggered event
StartEscortAppointment
csharp
AppointmentResult StartEscortAppointment(ClientType clientType)
Purpose: Begin escort work session.

Parameters:

clientType: ClientType enum (Safe, Demanding, Dangerous)
Returns: AppointmentResult (earnings, safety outcome, reputation impact)

Side effects:

High income
High risk (safety, police, reputation)
Time passes
May trigger events (dangerous client, police sting)
StartSugarRelationship
csharp
void StartSugarRelationship(string benefactorId, SugarTerms terms)
Purpose: Enter sugar baby/daddy arrangement with NPC.

Parameters:

benefactorId: Wealthy NPC providing support
terms: SugarTerms (allowance, expectations, boundaries)
Side effects:

Creates ongoing relationship
Regular income (monthly allowance)
Obligations (time together, intimacy, exclusivity?)
Affects other relationships (jealousy, judgment)
NEW Events:
csharp
event Action<string> OnClothingChanged              // (clothingId)
event Action<string, BlackmailType, float> OnBlackmailTriggered
event Action<AppointmentResult> OnEscortAppointmentComplete
event Action<string, SugarTerms> OnSugarRelationshipStarted
event Action<string> OnSugarRelationshipEnded
event Action<string> OnContentLeaked                // (contentType)
event Action<string> OnFamilyDiscoveredSexWork      // (familyMemberId)
NEW Data Structures:
ClothingCategory (enum)
csharp
enum ClothingCategory {
    Professional,   // Office wear
    Casual,         // Default clothing
    Provocative,    // Club wear, revealing
    Lingerie,       // Underwear, intimate
    Fetish,         // Niche content
    Nude            // No clothing
}
ClothingItem (extends Entity)
csharp
class ClothingItem : Entity {
    ClothingCategory category;
    float professionalMod;      // -20 to +20
    float socialMod;            // -20 to +20 (context-dependent)
    float vanityValue;          // 0-100
    float onlyFansIncomeMod;    // Multiplier (0.5x to 2.5x)
    float attractiveness Mod;    // -10 to +30
    bool allowedInOffice;       // Can wear to corporate job
    bool allowedInPublic;       // Legal to wear in public
}
ClothingModifierType (enum)
csharp
enum ClothingModifierType {
    ProfessionalRep,
    SocialRep,
    OnlyFansIncome,
    Attractiveness
}
BlackmailType (enum)
csharp
enum BlackmailType {
    NudePhotos,         // Private photos leaked
    StreamContent,      // OnlyFans content exposed
    WitnessedSexWork,   // Saw player escorting
    IntimateVideo       // Sex tape
}
BlackmailEvent
csharp
struct BlackmailEvent {
    string blackmailerId;
    BlackmailType type;
    float demand;               // Money demanded
    DateTime deadline;
    bool playerPaid;
    bool contentLeaked;
    bool violentResponse;       // Player attacked blackmailer
}
ClientType (enum - for escorting)
csharp
enum ClientType {
    Safe,       // Normal client, no issues
    Demanding,  // Wants extra services, pressure
    Dangerous,  // Risk of violence
    Police      // Sting operation
}
AppointmentResult
csharp
struct AppointmentResult {
    float earnings;
    bool safetyIssue;           // Was player hurt/threatened
    bool policeInvolved;        // Was it a sting
    float reputationDamage;
    ClientSatisfaction satisfaction;
}
ClientSatisfaction (enum)
csharp
enum ClientSatisfaction {
    Disappointed,    // Low tip, bad review
    Satisfied,       // Normal payment
    Delighted        // Bonus tip, repeat client
}
SugarTerms
csharp
struct SugarTerms {
    float monthlyAllowance;
    int hoursPerWeek;           // Time commitment
    bool intimacyExpected;
    bool exclusivityRequired;   // Can't see other people
    bool publicAppearances;     // Attend events as couple
}
Implementation Notes:
Clothing Effects (Context-Aware):
csharp
void EquipClothing(string clothingId) {
    ClothingItem item = EntitySystem.Instance.GetEntity(clothingId) as ClothingItem;
    currentClothing = item;
    
    // Apply immediate effects
    UpdatePlayerAppearance(item);
    
    // Context-aware reputation effects
    string currentLocation = LocationSystem.Instance.GetPlayerLocation();
    ApplyClothingReputationEffects(item, currentLocation);
    
    OnClothingChanged?.Invoke(clothingId);
}

void ApplyClothingReputationEffects(ClothingItem item, string location) {
    if (location == "office") {
        // Professional context
        if (item.category == ClothingCategory.Professional) {
            // Positive professional rep
            ReputationSystem.Instance.ModifyReputation(playerId, ReputationTrack.Professional, +2f, "Professional attire");
        }
        else if (item.category == ClothingCategory.Provocative || item.category == ClothingCategory.Lingerie) {
            // Negative professional rep, may get fired
            ReputationSystem.Instance.ModifyReputation(playerId, ReputationTrack.Professional, -10f, "Inappropriate attire");
            JobSystem.Instance.TriggerWarning(playerId, "Dress code violation");
        }
    }
    else if (location == "club") {
        // Social context - provocative is positive
        if (item.category == ClothingCategory.Provocative) {
            ReputationSystem.Instance.ModifyReputation(playerId, ReputationTrack.Social, +5f, "Stylish club attire");
        }
    }
    else if (location == "home_stream") {
        // Streaming context - income effects
        // (handled by GetClothingModifier when calculating stream income)
    }
}

float GetClothingModifier(ClothingModifierType type) {
    if (currentClothing == null) {
        // Nude = maximum OnlyFans income, massive reputation risk
        if (type == ClothingModifierType.OnlyFansIncome) return 2.5f;
        if (type == ClothingModifierType.ProfessionalRep) return -50f;  // Catastrophic
        return 0f;
    }
    
    switch (type) {
        case ClothingModifierType.ProfessionalRep:
            return currentClothing.professionalMod;
        case ClothingModifierType.SocialRep:
            return currentClothing.socialMod;
        case ClothingModifierType.OnlyFansIncome:
            return currentClothing.onlyFansIncomeMod;
        case ClothingModifierType.Attractiveness:
            return currentClothing.attractivenessMod;
        default:
            return 0f;
    }
}
Blackmail System:
csharp
void TriggerBlackmail(string blackmailerId, BlackmailType type, float demand) {
    BlackmailEvent blackmail = new BlackmailEvent {
        blackmailerId = blackmailerId,
        type = type,
        demand = demand,
        deadline = TimeSystem.Instance.GetCurrentTime().AddDays(7),  // 1 week to pay
        playerPaid = false,
        contentLeaked = false,
        violentResponse = false
    };
    
    activeBlackmail.Add(blackmail);
    
    OnBlackmailTriggered?.Invoke(blackmailerId, type, demand);
    
    // Create UI event for player choice
    EventSystem.Instance.TriggerEvent("blackmail_decision", blackmail);
}

void RespondToBlackmail(BlackmailEvent blackmail, BlackmailResponse response) {
    switch (response) {
        case BlackmailResponse.Pay:
            // Pay the demand
            bool canAfford = EconomySystem.Instance.DeductExpense(
                playerId,
                blackmail.demand,
                ExpenseType.Blackmail,
                $"Blackmail payment to {blackmail.blackmailerId}"
            );
            
            if (canAfford) {
                blackmail.playerPaid = true;
                // Content stays private... for now
            } else {
                // Can't pay = content leaks
                LeakContent(blackmail.type);
                blackmail.contentLeaked = true;
            }
            break;
        
        case BlackmailResponse.Refuse:
            // Refuse to pay = content leaks
            LeakContent(blackmail.type);
            blackmail.contentLeaked = true;
            break;
        
        case BlackmailResponse.Violence:
            // Attack blackmailer (increases criminal risk)
            blackmail.violentResponse = true;
            
            // Resolve violence (may succeed, may fail, may get arrested)
            bool success = Random.value > 0.5f;
            if (success) {
                // Blackmailer silenced, content destroyed
                ReputationSystem.Instance.ModifyReputation(playerId, ReputationTrack.Criminal, +5f, "Violent resolution");
            } else {
                // Failed, now arrested for assault
                CriminalRecordSystem.Instance.AddOffense(playerId, OffenseType.Assault, "Attacked blackmailer");
                LeakContent(blackmail.type);  // And content still leaks
            }
            break;
    }
    
    activeBlackmail.Remove(blackmail);
}

void LeakContent(BlackmailType type) {
    OnContentLeaked?.Invoke(type.ToString());
    
    // Massive reputation damage
    ReputationSystem.Instance.ModifyReputation(playerId, ReputationTrack.Social, -30f, "Content leaked");
    ReputationSystem.Instance.ModifyReputation(playerId, ReputationTrack.Professional, -40f, "Adult content exposed");
    
    // Family discovers
    foreach (var npc in RelationshipSystem.Instance.GetNPCsByType(NPCType.Family)) {
        OnFamilyDiscoveredSexWork?.Invoke(npc.id);
        
        // Severe relationship damage
        float damage = npc.values[NPCValue.Morality] * -80f;  // Higher morality = worse reaction
        RelationshipSystem.Instance.ModifyRelationship(npc.id, damage, "Adult content leaked");
        
        // Create permanent memory
        RelationshipSystem.Instance.ObservePlayerAction(new PlayerAction {
            type = ActionType.ContentLeaked,
            details = type.ToString(),
            timestamp = TimeSystem.Instance.GetCurrentTime(),
            memorability = 10,  // PERMANENT
            isPositive = false
        });
    }
    
    // May lose job
    JobSystem.Instance.CheckTerminationForSexWork(playerId);
}
Escorting:
csharp
AppointmentResult StartEscortAppointment(ClientType clientType) {
    AppointmentResult result = new AppointmentResult();
    
    // Base earnings
    float baseEarnings = 500f;
    
    // Time passes (1-3 hours)
    float duration = Random.Range(1f, 3f);
    TimeSystem.Instance.AdvanceTime(duration * 60f);
    
    switch (clientType) {
        case ClientType.Safe:
            result.earnings = baseEarnings;
            result.safetyIssue = false;
            result.policeInvolved = false;
            result.satisfaction = ClientSatisfaction.Satisfied;
            break;
        
        case ClientType.Demanding:
            // Wants extra services - player choice
            // (trigger event, player decides to comply or refuse)
            result.earnings = baseEarnings * (Random.value > 0.5f ? 1.5f : 0.7f);
            result.safetyIssue = Random.value < 0.2f;  // 20% chance of issue
            break;
        
        case ClientType.Dangerous:
            // High risk of violence
            result.safetyIssue = Random.value < 0.7f;  // 70% chance
            
            if (result.safetyIssue) {
                // Player hurt, no payment
                result.earnings = 0;
                TimeSystem.Instance.ModifyEnergy(-50f, "Escorting violence");
                
                // May go to hospital, police report
                EventSystem.Instance.TriggerEvent("escort_violence", result);
            } else {
                result.earnings = baseEarnings * 2f;  // Hazard pay
            }
            break;
        
        case ClientType.Police:
            // Sting operation - arrested
            result.policeInvolved = true;
            result.earnings = 0;
            
            // Arrested for prostitution
            CriminalRecordSystem.Instance.AddOffense(playerId, OffenseType.Prostitution, "Solicitation arrest");
            ReputationSystem.Instance.ModifyReputation(playerId, ReputationTrack.Legal, -25f, "Prostitution arrest");
            
            // Jail time
            TimeSystem.Instance.AdvanceTime(24f * 60f);  // 1 day
            break;
    }
    
    // Add income (if any)
    if (result.earnings > 0) {
        EconomySystem.Instance.AddIncome(
            playerId,
            result.earnings,
            IncomeSource.SexWork,
            "Escort appointment"
        );
    }
    
    // Reputation damage (even if safe)
    result.reputationDamage = 5f;
    ReputationSystem.Instance.ModifyReputation(playerId, ReputationTrack.Social, -result.reputationDamage, "Sex work");
    
    OnEscortAppointmentComplete?.Invoke(result);
    
    return result;
}
Sugar Relationships:
csharp
void StartSugarRelationship(string benefactorId, SugarTerms terms) {
    // Create NPC if not exists
    NPC benefactor = RelationshipSystem.Instance.GetNPC(benefactorId);
    
    // Set up recurring income
    ScheduleRecurringAllowance(benefactorId, terms.monthlyAllowance);
    
    // Create obligations
    ScheduleSugarObligations(benefactorId, terms);
    
    // Relationship starts at moderate level
    RelationshipSystem.Instance.ModifyRelationship(benefactorId, +30f, "Sugar relationship started");
    
    OnSugarRelationshipStarted?.Invoke(benefactorId, terms);
}

void ScheduleRecurringAllowance(string benefactorId, float amount) {
    // Every game month, add allowance
    TimeSystem.Instance.ScheduleRecurringEvent(
        30f,  // Every 30 days
        () => {
            EconomySystem.Instance.AddIncome(
                playerId,
                amount,
                IncomeSource.SugarRelationship,
                $"Monthly allowance from {benefactorId}"
            );
        },
        "Sugar allowance"
    );
}

void ScheduleSugarObligations(string benefactorId, SugarTerms terms) {
    // Schedule time together (weekly dates, etc.)
    float hoursPerWeek = terms.hoursPerWeek;
    
    // Create weekly event - player must attend or relationship suffers
    EventSystem.Instance.CreateRecurringEvent(new RelationshipEvent {
        npcId = benefactorId,
        type = RelationshipEventType.SugarDate,
        frequency = EventFrequency.Weekly,
        duration = hoursPerWeek,
        skipPenalty = -20f  // Miss date = big relationship hit
    });
}

void EndSugarRelationship(string benefactorId, SugarEndReason reason) {
    // Stop allowance
    TimeSystem.Instance.CancelRecurringEvent($"Sugar allowance from {benefactorId}");
    
    // Relationship consequences
    float relationshipChange = reason == SugarEndReason.PlayerChoice ? -10f : -30f;
    RelationshipSystem.Instance.ModifyRelationship(benefactorId, relationshipChange, "Sugar relationship ended");
    
    // If benefactor ends it, may have consequences (blackmail, vengeful)
    if (reason == SugarEndReason.BenefactorEnded) {
        // 30% chance of blackmail attempt
        if (Random.value < 0.3f) {
            TriggerBlackmail(benefactorId, BlackmailType.IntimateVideo, Random.Range(5000f, 20000f));
        }
    }
    
    OnSugarRelationshipEnded?.Invoke(benefactorId);
}
Clothing Data (Examples):
json
{
  "professional_suit": {
    "category": "Professional",
    "professionalMod": 15,
    "socialMod": 0,
    "vanityValue": 40,
    "onlyFansIncomeMod": 0.5,
    "attractivenessMod": 5,
    "allowedInOffice": true,
    "allowedInPublic": true,
    "cost": 500
  },
  "lingerie": {
    "category": "Lingerie",
    "professionalMod": -30,
    "socialMod": -10,
    "vanityValue": 70,
    "onlyFansIncomeMod": 1.5,
    "attractivenessMod": 20,
    "allowedInOffice": false,
    "allowedInPublic": false,
    "cost": 200
  },
  "nude": {
    "category": "Nude",
    "professionalMod": -50,
    "socialMod": -40,
    "vanityValue": 100,
    "onlyFansIncomeMod": 2.5,
    "attractivenessMod": 30,
    "allowedInOffice": false,
    "allowedInPublic": false,
    "cost": 0
  }
}
```

---

## **Testing Checklist (UPDATED):**
```
[ ] EquipClothing changes player appearance
[ ] Clothing effects are context-aware (office vs club vs home)
[ ] Provocative clothing in office triggers warning/firing
[ ] Lingerie/nude in OnlyFans stream increases income
[ ] Professional clothing improves corporate reputation
[ ] Blackmail can be triggered by leaked content
[ ] Player can choose to pay, refuse, or use violence
[ ] Refusing blackmail leaks content and damages reputation
[ ] Family NPCs react to leaked content based on morality
[ ] Escorting generates high income but high risk
[ ] Dangerous clients can hurt player
[ ] Police sting operations result in arrest
[ ] Sugar relationships provide monthly allowance
[ ] Missing sugar obligations damages relationship
[ ] Sugar relationships can end with blackmail
[ ] Body attractiveness affects OnlyFans income
[ ] Fitness level affects energy max
[ ] Grooming costs money and affects reputation