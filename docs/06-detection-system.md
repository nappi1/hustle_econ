6. DETECTION SYSTEM
Purpose
Determines when player gets caught doing inappropriate/illegal activities. Uses line-of-sight, proximity, observer patterns, and environmental positioning to create fair-but-tense detection. No UI warnings - players learn through environmental cues and consequences.

Interface
RegisterObserver
csharp
void RegisterObserver(string observerId, ObserverData data)
Purpose: Add NPC who can detect player (boss, cop, coworker).

Parameters:

observerId: Observer's unique ID
data: ObserverData containing detection parameters
Side effects:

Registers observer in active list
Starts patrol/detection loop for this observer
UnregisterObserver
csharp
void UnregisterObserver(string observerId)
Purpose: Remove observer (NPC left scene, died, etc.).

Parameters:

observerId: Observer ID
UpdateObserverPosition
csharp
void UpdateObserverPosition(string observerId, Vector3 position, Vector3 facing)
Purpose: Update observer's location and facing direction (for line-of-sight).

Parameters:

observerId: Observer ID
position: Current world position
facing: Direction they're looking
Called by: NPC movement system each frame

CheckDetection
csharp
DetectionResult CheckDetection(string playerId, string activityId)
Purpose: Check if player is currently detected doing an activity.

Parameters:

playerId: Player ID
activityId: What activity they're doing
Returns: DetectionResult struct

Called by: ActivitySystem (periodic checks while activity running)

GetDetectionRisk
csharp
float GetDetectionRisk(string playerId, string activityId, string locationId)
Purpose: Calculate current detection risk (0-1 scale) for UI display.

Parameters:

playerId: Player ID
activityId: Activity they might start
locationId: Where they are
Returns: Risk from 0 (safe) to 1 (certain detection)

Note: This is for optional risk indicator UI, not a warning

SetPatrolPattern
csharp
void SetPatrolPattern(string observerId, List<Vector3> waypoints, float intervalSeconds)
Purpose: Define observer's patrol route.

Parameters:

observerId: Observer ID
waypoints: Path they follow
intervalSeconds: Time between waypoints (with variance)
Side effects:

Observer follows pattern automatically
Adds slight randomness to timing (±10%)
Events
csharp
event Action<DetectionResult> OnPlayerDetected  // Fires when player gets caught
event Action<string, float> OnDetectionRisk     // Fires when risk level changes significantly
Data Structures
Observer
csharp
class Observer {
    string id;
    ObserverRole role;               // Boss, Cop, Coworker, Security
    Vector3 position;
    Vector3 facing;
    float visionRange;               // How far they can see (meters)
    float visionCone;                // Field of view angle (degrees)
    float audioSensitivity;          // How well they hear (0-1)
    bool caresAboutLegality;         // Will they report illegal activity?
    bool caresAboutJobPerformance;   // Will they report slacking off?
    List<Vector3> patrolWaypoints;
    int currentWaypointIndex;
    float nextPatrolTime;
}
ObserverRole (enum)
csharp
enum ObserverRole {
    Boss,       // High job performance scrutiny
    Cop,        // High legality scrutiny, wide vision
    Coworker,   // Low scrutiny, might not snitch
    Security,   // Medium scrutiny, follows patterns
    Civilian    // May report crimes but not slacking
}
DetectionResult
csharp
struct DetectionResult {
    bool detected;
    string observerId;      // Who caught you (nullable)
    float severity;         // 0-1, how bad the detection is
    string activityId;      // What you were caught doing
    DetectionReason reason; // Why you were caught
}
DetectionReason (enum)
csharp
enum DetectionReason {
    LineOfSight,   // Observer saw you
    Audio,         // Observer heard you
    Evidence,      // Left traces/evidence
    Witness,       // Another NPC snitched
    None
}
Dependencies
Reads from:

ActivitySystem (what player is doing, activity properties)
LocationSystem (player position, observer positions)
EntitySystem (observer entities)
Writes to:

ConsequenceSystem (triggers consequences when detected)
HeatSystem (increases heat when detected)
Subscribed to by:

JobSystem (detection at work → warnings/firing)
ReputationSystem (detection affects reputation)
Implementation Notes
Detection Logic:

csharp
DetectionResult CheckDetection(string playerId, string activityId) {
    Activity activity = ActivitySystem.Instance.GetActivity(activityId);
    Vector3 playerPos = GetPlayerPosition(playerId);
    string playerLocation = LocationSystem.Instance.GetPlayerLocation(playerId);
    
    foreach (var observer in activeObservers.Values) {
        // Skip if observer not in same location
        if (observer.currentLocation != playerLocation) continue;
        
        // 1. Distance check
        float distance = Vector3.Distance(observer.position, playerPos);
        if (distance > observer.visionRange) continue;
        
        // 2. Line of sight check
        if (!HasLineOfSight(observer.position, playerPos)) continue;
        
        // 3. Vision cone check
        Vector3 toPlayer = (playerPos - observer.position).normalized;
        float angle = Vector3.Angle(observer.facing, toPlayer);
        if (angle > observer.visionCone / 2f) continue;
        
        // 4. Does observer care about this activity?
        if (activity.isLegal && !observer.caresAboutJobPerformance) continue;
        if (!activity.isLegal && !observer.caresAboutLegality) continue;
        
        // 5. Is activity conspicuous enough to notice?
        float observerAwareness = observer.visionRange / distance;
        float detectionThreshold = activity.visualProfile;
        
        if (observerAwareness >= detectionThreshold) {
            // CAUGHT!
            return new DetectionResult {
                detected = true,
                observerId = observer.id,
                severity = CalculateSeverity(activity, observer),
                activityId = activityId,
                reason = DetectionReason.LineOfSight
            };
        }
    }
    
    return new DetectionResult { detected = false };
}
Line of Sight:

csharp
bool HasLineOfSight(Vector3 from, Vector3 to) {
    Vector3 direction = to - from;
    float distance = direction.magnitude;
    
    // Raycast to check for obstacles
    RaycastHit hit;
    if (Physics.Raycast(from, direction.normalized, out hit, distance)) {
        // Hit something - check if it's the player or an obstacle
        return hit.collider.CompareTag("Player");
    }
    
    return true; // Nothing blocking
}
Patrol System:

csharp
void Update() {
    float currentTime = Time.time;
    
    foreach (var observer in activeObservers.Values) {
        if (observer.patrolWaypoints.Count == 0) continue;
        
        // Time to move to next waypoint?
        if (currentTime >= observer.nextPatrolTime) {
            observer.currentWaypointIndex = (observer.currentWaypointIndex + 1) % observer.patrolWaypoints.Count;
            Vector3 nextWaypoint = observer.patrolWaypoints[observer.currentWaypointIndex];
            
            // Move observer (simplified - real version would use NavMesh)
            observer.position = nextWaypoint;
            
            // Schedule next patrol move (with variance)
            float interval = observer.patrolInterval;
            float variance = interval * 0.1f; // ±10%
            observer.nextPatrolTime = currentTime + interval + Random.Range(-variance, variance);
        }
    }
}
Severity Calculation:

csharp
float CalculateSeverity(Activity activity, Observer observer) {
    float severity = 0.5f; // Base
    
    // Illegal activity is more severe
    if (!activity.isLegal) severity += 0.3f;
    
    // Cops care more about crime
    if (observer.role == ObserverRole.Cop && !activity.isLegal) severity += 0.2f;
    
    // Boss cares more about slacking
    if (observer.role == ObserverRole.Boss && observer.caresAboutJobPerformance) severity += 0.2f;
    
    return Mathf.Clamp01(severity);
}
Environmental Cues (Audio System Integration):

csharp
void Update() {
    foreach (var observer in activeObservers.Values) {
        // Play footstep sounds based on proximity to player
        float distanceToPlayer = Vector3.Distance(observer.position, playerPosition);
        
        if (distanceToPlayer < 10f) {
            // Play footsteps (louder when closer)
            float volume = 1f - (distanceToPlayer / 10f);
            PlayFootstepSound(observer.id, volume);
        }
    }
}
```

---

## **Edge Cases**

1. **Player exactly at vision range edge:** Use <= for detection (edge counts)
2. **Multiple observers detect simultaneously:** Return first detected (priority by severity)
3. **Observer walks through wall (pathfinding bug):** Line of sight still blocks detection
4. **Activity has 0 visualProfile:** Can never be detected visually (perfect stealth)
5. **Observer with 360° vision cone:** Can see in all directions (security camera)

---

## **Testing Checklist**
```
[ ] Observer can be registered and unregistered
[ ] UpdateObserverPosition changes observer location
[ ] CheckDetection returns false when player out of range
[ ] CheckDetection returns false when no line of sight
[ ] CheckDetection returns false when outside vision cone
[ ] CheckDetection returns true when all conditions met
[ ] Boss detects slacking off (activity.isLegal but not job-related)
[ ] Cop detects illegal activity
[ ] Coworker doesn't snitch (caresAboutJobPerformance = false)
[ ] Patrol pattern moves observer through waypoints
[ ] Patrol timing has variance (not perfectly predictable)
[ ] OnPlayerDetected event fires with correct data
[ ] Severity calculation accounts for observer role and activity type
[ ] Environmental audio cues work (footsteps)