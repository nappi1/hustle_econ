7. JOB SYSTEM
Purpose
Manages player employment, career progression, wages, shifts, performance tracking, warnings, and termination. Integrates with Activity System (work is an activity), Detection System (getting caught slacking), and Reputation System (professional advancement).

Interface
ApplyForJob
csharp
bool ApplyForJob(string jobId)
Purpose: Player applies for a job.

Parameters:

jobId: Job entity ID
Returns: true if hired, false if rejected

Requirements checked:

Reputation thresholds
Skills required
Criminal record (some jobs check)
Side effects:

If hired, creates employment record
Schedules first shift
Broadcasts OnJobHired event
QuitJob
csharp
void QuitJob(string jobId)
Purpose: Player voluntarily leaves job.

Parameters:

jobId: Job to quit
Side effects:

Ends employment
Affects professional reputation (quitting without notice = negative)
Cancels scheduled shifts
Broadcasts OnJobQuit event
StartShift
csharp
void StartShift(string jobId)
Purpose: Begin work shift.

Parameters:

jobId: Which job
Side effects:

Creates activity (work minigame)
Locks player into shift duration
Tracks performance
Broadcasts OnShiftStarted event
EndShift
csharp
ShiftResults EndShift(string jobId)
Purpose: Complete work shift, calculate pay and performance.

Parameters:

jobId: Which job
Returns: ShiftResults (pay earned, performance score, warnings)

Side effects:

Adds income to economy
Updates job performance history
May trigger warnings if performance poor
May trigger promotion if performance excellent
Broadcasts OnShiftEnded event
GetWarningCount
csharp
int GetWarningCount(string jobId)
Purpose: How many warnings player has at this job.

Parameters:

jobId: Which job
Returns: Warning count (0-2 usually, 3 = fired)

TriggerWarning
csharp
void TriggerWarning(string jobId, string reason)
Purpose: Issue formal warning (late, caught slacking, dress code, etc.).

Parameters:

jobId: Which job
reason: Why warned
Side effects:

Increments warning count
If count reaches 3, triggers termination
Creates permanent record in job history
Broadcasts OnWarningIssued event
GetPromotion
csharp
void GetPromotion(string jobId, string newTitle)
Purpose: Player promoted to higher position.

Parameters:

jobId: Current job
newTitle: New job title
Side effects:

Increases wage
May reduce shift frequency (management works less hours)
May increase slack-off tolerance (boss has more freedom)
Updates professional reputation
Broadcasts OnPromotion event
Events
csharp
event Action<string> OnJobHired            // (jobId)
event Action<string> OnJobQuit             // (jobId)
event Action<string, string> OnJobFired    // (jobId, reason)
event Action<string> OnShiftStarted        // (jobId)
event Action<ShiftResults> OnShiftEnded
event Action<string, string> OnWarningIssued  // (jobId, reason)
event Action<string, string> OnPromotion   // (jobId, newTitle)
Data Structures
Job (extends Entity)
csharp
class Job : Entity {
    string title;
    float hourlyWage;
    JobType type;
    
    // Schedule
    List<ShiftSchedule> shifts;    // When player works
    float hoursPerWeek;
    
    // Requirements
    Dictionary<ReputationTrack, float> reputationRequired;
    Dictionary<SkillType, float> skillsRequired;
    bool requiresCleanRecord;      // Criminal record blocks hiring
    
    // Minigame
    MinigameType minigameType;
    float detectionSensitivity;    // How easily boss catches slacking (0-1)
    
    // Progression
    List<string> promotionPath;    // Worker → Supervisor → Manager
    float promotionThreshold;      // Performance score needed
    
    // Employment data (instance-specific)
    int warningCount;
    float performanceScore;        // Running average
    DateTime hireDate;
    DateTime lastShift;
}
JobType (enum)
csharp
enum JobType {
    Janitorial,
    Retail,
    Restaurant,
    Office,
    Warehouse,
    Gig,           // Rideshare, delivery
    Creative       // Streaming, writing
}
ShiftSchedule
csharp
struct ShiftSchedule {
    DayOfWeek day;
    TimeSpan startTime;
    float durationHours;
}
MinigameType (enum)
csharp
enum MinigameType {
    ClickTargets,     // Mopping, stocking
    SequenceMatch,    // Assembly line
    TimingGame,       // Restaurant orders
    EmailManagement,  // Office work
    Driving           // Gig work
}
ShiftResults
csharp
struct ShiftResults {
    float payEarned;
    float performanceScore;   // 0-100
    int warningsIssued;
    bool caughtSlacking;
    float bonusPay;           // Overtime, tips, performance bonus
}
Dependencies
Reads from:

ReputationSystem (check if qualified for job)
SkillSystem (check required skills)
CriminalRecordSystem (some jobs check background)
DetectionSystem (catching player slacking during shift)
Writes to:

EconomySystem (paycheck)
ReputationSystem (professional rep from performance/promotion)
TimeSystem (shift schedules)
ActivitySystem (work shift is an activity)
Subscribed to by:

UI (shift schedule, warnings, pay)
RelationshipSystem (overworking affects relationships)
Implementation Notes
Job Application:

csharp
bool ApplyForJob(string jobId) {
    Job job = EntitySystem.Instance.GetEntity(jobId) as Job;
    
    // Check reputation requirements
    foreach (var req in job.reputationRequired) {
        float playerRep = ReputationSystem.Instance.GetReputation(playerId, req.Key);
        if (playerRep < req.Value) {
            return false;  // Doesn't meet reputation requirement
        }
    }
    
    // Check skills
    foreach (var req in job.skillsRequired) {
        float playerSkill = SkillSystem.Instance.GetSkillLevel(playerId, req.Key);
        if (playerSkill < req.Value) {
            return false;  // Doesn't meet skill requirement
        }
    }
    
    // Check criminal record
    if (job.requiresCleanRecord) {
        bool hasRecord = CriminalRecordSystem.Instance.HasCriminalRecord(playerId);
        if (hasRecord) {
            return false;  // Criminal record blocks hiring
        }
    }
    
    // HIRED!
    job.hireDate = TimeSystem.Instance.GetCurrentTime();
    job.warningCount = 0;
    job.performanceScore = 50f;  // Start neutral
    
    // Schedule first shift
    ScheduleNextShift(job);
    
    OnJobHired?.Invoke(jobId);
    
    return true;
}
Shift Performance:

csharp
ShiftResults EndShift(string jobId) {
    Job job = EntitySystem.Instance.GetEntity(jobId) as Job;
    Activity shiftActivity = ActivitySystem.Instance.GetActivity($"shift_{jobId}");
    
    // Calculate performance based on minigame results
    float performanceScore = shiftActivity.performanceScore;  // 0-100 from minigame
    
    // Check if caught slacking
    bool caughtSlacking = shiftActivity.detectedSlacking;
    
    // Calculate pay
    float hoursWorked = shiftActivity.durationHours;
    float basePay = job.hourlyWage * hoursWorked;
    float bonusPay = 0f;
    
    // Performance bonus (if score > 80)
    if (performanceScore > 80f) {
        bonusPay = basePay * 0.1f;  // 10% bonus
    }
    
    float totalPay = basePay + bonusPay;
    
    // Add income
    EconomySystem.Instance.AddIncome(
        playerId,
        totalPay,
        IncomeSource.Salary,
        $"{job.title} shift"
    );
    
    // Update job performance history
    job.performanceScore = (job.performanceScore * 0.8f) + (performanceScore * 0.2f);  // Weighted average
    job.lastShift = TimeSystem.Instance.GetCurrentTime();
    
    // Handle warnings
    int warningsThisShift = 0;
    if (caughtSlacking) {
        TriggerWarning(jobId, "Caught slacking off");
        warningsThisShift++;
    }
    if (performanceScore < 30f) {
        TriggerWarning(jobId, "Poor performance");
        warningsThisShift++;
    }
    
    // Check for promotion
    if (job.performanceScore > job.promotionThreshold && job.warningCount == 0) {
        CheckPromotion(job);
    }
    
    // Schedule next shift
    ScheduleNextShift(job);
    
    ShiftResults results = new ShiftResults {
        payEarned = totalPay,
        performanceScore = performanceScore,
        warningsIssued = warningsThisShift,
        caughtSlacking = caughtSlacking,
        bonusPay = bonusPay
    };
    
    OnShiftEnded?.Invoke(results);
    
    return results;
}
Warning System:

csharp
void TriggerWarning(string jobId, string reason) {
    Job job = EntitySystem.Instance.GetEntity(jobId) as Job;
    
    job.warningCount++;
    
    OnWarningIssued?.Invoke(jobId, reason);
    
    // Three strikes = fired
    if (job.warningCount >= 3) {
        FirePlayer(jobId, "Exceeded warning limit");
    }
}

void FirePlayer(string jobId, string reason) {
    Job job = EntitySystem.Instance.GetEntity(jobId) as Job;
    
    // Cancel all future shifts
    CancelScheduledShifts(job);
    
    // Reputation hit
    ReputationSystem.Instance.ModifyReputation(
        playerId,
        ReputationTrack.Professional,
        -20f,
        $"Fired from {job.title}"
    );
    
    // Create permanent record (harder to get hired elsewhere)
    AddToJobHistory(job, "Terminated: " + reason);
    
    OnJobFired?.Invoke(jobId, reason);
}
Promotion:

csharp
void CheckPromotion(Job job) {
    if (job.promotionPath.Count == 0) return;  // No promotion path
    
    string currentTitle = job.title;
    int currentIndex = job.promotionPath.IndexOf(currentTitle);
    
    if (currentIndex < 0 || currentIndex >= job.promotionPath.Count - 1) {
        return;  // Already at top or not on path
    }
    
    string newTitle = job.promotionPath[currentIndex + 1];
    GetPromotion(job.id, newTitle);
}

void GetPromotion(string jobId, string newTitle) {
    Job job = EntitySystem.Instance.GetEntity(jobId) as Job;
    
    string oldTitle = job.title;
    job.title = newTitle;
    
    // Increase wage (20-40% raise)
    job.hourlyWage *= Random.Range(1.2f, 1.4f);
    
    // Management perks (less hours, more freedom)
    if (newTitle.Contains("Manager") || newTitle.Contains("Supervisor")) {
        job.hoursPerWeek *= 0.8f;  // Work 20% fewer hours
        job.detectionSensitivity *= 0.7f;  // Boss checks you less
    }
    
    // Reputation boost
    ReputationSystem.Instance.ModifyReputation(
        playerId,
        ReputationTrack.Professional,
        +15f,
        $"Promoted to {newTitle}"
    );
    
    // NPCs react (family proud, partner impressed)
    RelationshipSystem.Instance.ObservePlayerAction(new PlayerAction {
        type = ActionType.GotPromoted,
        details = $"{oldTitle} → {newTitle}",
        timestamp = TimeSystem.Instance.GetCurrentTime(),
        memorability = 6,
        isPositive = true
    });
    
    OnPromotion?.Invoke(jobId, newTitle);
}
```

---

## **Edge Cases**

1. **Miss scheduled shift:** Auto-warning, may auto-fire if pattern
2. **Work multiple jobs:** Shifts can't overlap (player chooses which to attend)
3. **Arrested during work week:** Miss shifts, auto-fired
4. **Performance exactly at promotion threshold:** Random chance (50/50)
5. **Quit job with 0 notice:** Professional rep hit

---

## **Testing Checklist**
```
[ ] ApplyForJob checks reputation requirements
[ ] ApplyForJob rejects if criminal record (for jobs requiring clean record)
[ ] StartShift creates work activity
[ ] EndShift calculates pay correctly (hourly wage * hours)
[ ] Performance bonus applied if score > 80
[ ] Warning issued if caught slacking
[ ] Warning issued if performance < 30
[ ] Three warnings = automatic termination
[ ] Termination affects professional reputation
[ ] Promotion increases wage and reduces detection
[ ] Promotion affects relationship NPCs positively
[ ] Scheduled shifts appear in calendar
[ ] Missing shift issues warning
[ ] Can work multiple jobs if shifts don't overlap