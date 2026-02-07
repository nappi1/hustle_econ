# V2 Update (Aligned to Current Codebase)

This V2 spec reflects the current implementation. See docs/IMPLEMENTATION_DISCREPANCIES.md for cross-system gaps.

## Implementation Notes

- TravelToLocation uses taxi cost () and stubbed vehicle logic.
- Scene load calls GameManager.LoadScene; scenes must be in Build Settings.
- CanTravelTo exists; invitation system remains TODO.

---
10. LOCATION SYSTEM
Purpose
Manages game locations (apartments, offices, streets, clubs, etc.), player position, travel between locations, and location-specific rules (what activities are allowed where, who can enter, detection sensitivity). Supports instanced locations (your apartment) and shared spaces (streets, clubs).

Interface
GetPlayerLocation
csharp
string GetPlayerLocation(string playerId)
Purpose: Returns player's current location ID.

Parameters:

playerId: Player ID
Returns: Location ID (e.g., "apartment_player", "office_downtown", "street_main")

TravelToLocation
csharp
bool TravelToLocation(string playerId, string locationId)
Purpose: Move player to new location.

Parameters:

playerId: Player ID
locationId: Destination location
Returns: true if travel succeeded, false if blocked

Side effects:

Time advances based on travel distance
May cost money (taxi, gas)
Updates player position
Loads new scene/location
Broadcasts OnLocationChanged event
GetLocationData
csharp
LocationData GetLocationData(string locationId)
Purpose: Get information about a location.

Parameters:

locationId: Which location
Returns: LocationData struct

CheckLocationAccess
csharp
bool CheckLocationAccess(string playerId, string locationId)
Purpose: Can player enter this location?

Parameters:

playerId: Player ID
locationId: Location to check
Returns: true if player can enter

Checks:

Ownership (own this property?)
Employment (work here?)
Reputation (banned from club?)
Time of day (closed?)
GetAllowedActivities
csharp
List<ActivityType> GetAllowedActivities(string locationId)
Purpose: What activities can be done here?

Parameters:

locationId: Which location
Returns: List of allowed activities

Example:

Apartment: Sleep, stream, intimacy, drug storage
Office: Work, computer activities
Street: Travel, gig work, drug deals
Events
csharp
event Action<string, string, string> OnLocationChanged  // (playerId, fromLocation, toLocation)
event Action<string, string> OnLocationEntered          // (playerId, locationId)
event Action<string, string> OnLocationExited           // (playerId, locationId)
event Action<string> OnLocationLocked                   // (locationId) - evicted, fired, banned
Data Structures
LocationData
csharp
struct LocationData {
    string id;
    string name;
    LocationType type;
    
    // Access
    List<string> allowedPlayers;     // Who can enter (ownership, employment)
    bool isPublic;                    // Anyone can enter
    bool requiresInvitation;          // Need NPC permission
    
    // Activities
    List<ActivityType> allowedActivities;
    float detectionSensitivity;       // How easily caught doing illegal stuff (0-1)
    
    // NPCs
    List<string> residingNPCs;        // NPCs who are here
    List<string> patrollingObservers; // Cops, security, bosses
    
    // Environment
    Vector3 spawnPosition;
    string sceneName;                 // Unity scene to load
    
    // Timing
    TimeSpan openTime;                // When location opens (if applicable)
    TimeSpan closeTime;               // When it closes
}
LocationType (enum)
csharp
enum LocationType {
    Apartment,      // Player's home
    Office,         // Workplace
    Street,         // Public space
    Club,           // Social venue
    Store,          // Shopping
    NPCHome,        // NPC's apartment/house
    Vehicle         // Inside car (special location)
}
Dependencies
Reads from:

EntitySystem (location ownership, property entities)
TimeSystem (travel time, location hours)
EconomySystem (travel costs)
Writes to:

DetectionSystem (location affects detection sensitivity)
ActivitySystem (location determines allowed activities)
Subscribed to by:

UI (location display, map)
DetectionSystem (observers in location)
NPCSystem (NPCs in location)
Implementation Notes
Travel System:

csharp
bool TravelToLocation(string playerId, string locationId) {
    string currentLocation = GetPlayerLocation(playerId);
    
    // Check access
    if (!CheckLocationAccess(playerId, locationId)) {
        return false;  // Can't enter
    }
    
    LocationData destination = GetLocationData(locationId);
    
    // Calculate travel time and cost
    float travelTime = CalculateTravelTime(currentLocation, locationId);
    float travelCost = CalculateTravelCost(currentLocation, locationId);
    
    // Deduct cost
    bool canAfford = EconomySystem.Instance.DeductExpense(
        playerId,
        travelCost,
        ExpenseType.Transportation,
        $"Travel to {destination.name}"
    );
    
    if (!canAfford && travelCost > 0) {
        return false;  // Can't afford travel
    }
    
    // Advance time
    TimeSystem.Instance.AdvanceTime(travelTime);
    
    // Exit current location
    OnLocationExited?.Invoke(playerId, currentLocation);
    
    // Update player location
    playerLocations[playerId] = locationId;
    
    // Load new scene
    LoadLocation(destination);
    
    // Enter new location
    OnLocationEntered?.Invoke(playerId, locationId);
    OnLocationChanged?.Invoke(playerId, currentLocation, locationId);
    
    return true;
}

float CalculateTravelTime(string from, string to) {
    // Simple distance-based (can be enhanced with actual map later)
    // For now: adjacent = 10 min, across town = 30 min
    return 15f;  // 15 game minutes average
}

float CalculateTravelCost(string from, string to) {
    // Walking = free, driving = gas, taxi = expensive
    bool hasVehicle = InventorySystem.Instance.HasVehicle(playerId);
    
    if (hasVehicle) {
        return 5f;  // Gas cost
    } else {
        return 15f;  // Taxi/bus cost
    }
}
Location Access Control:

csharp
bool CheckLocationAccess(string playerId, string locationId) {
    LocationData location = GetLocationData(locationId);
    
    // Public locations - anyone can enter
    if (location.isPublic) {
        // But check if player is banned
        if (IsBanned(playerId, locationId)) {
            return false;
        }
        
        // Check if open
        if (!IsOpen(locationId)) {
            return false;  // Closed
        }
        
        return true;
    }
    
    // Private locations - must be allowed
    if (location.allowedPlayers.Contains(playerId)) {
        return true;  // Owner or employee
    }
    
    // Invitation-based
    if (location.requiresInvitation) {
        // Check if NPC invited player
        return HasInvitation(playerId, locationId);
    }
    
    return false;  // Not allowed
}

bool IsOpen(string locationId) {
    LocationData location = GetLocationData(locationId);
    
    // 24-hour locations (apartments, streets)
    if (location.openTime == TimeSpan.Zero && location.closeTime == TimeSpan.Zero) {
        return true;
    }
    
    TimeSpan currentTime = TimeSystem.Instance.GetCurrentTime().TimeOfDay;
    
    // Check if within business hours
    if (currentTime >= location.openTime && currentTime < location.closeTime) {
        return true;
    }
    
    return false;
}
Detection Sensitivity by Location:

csharp
public float GetDetectionSensitivity(string locationId) {
    LocationData location = GetLocationData(locationId);
    return location.detectionSensitivity;
}

// Examples:
// Apartment (player's): 0.1 (very safe, privacy)
// Street: 0.5 (moderate risk)
// Office: 0.8 (high risk, boss watching)
// Police station: 1.0 (maximum risk)
NPCs in Locations:

csharp
public List<string> GetNPCsInLocation(string locationId) {
    LocationData location = GetLocationData(locationId);
    return location.residingNPCs;
}

public void AddNPCToLocation(string npcId, string locationId) {
    LocationData location = GetLocationData(locationId);
    if (!location.residingNPCs.Contains(npcId)) {
        location.residingNPCs.Add(npcId);
    }
}

public void RemoveNPCFromLocation(string npcId, string locationId) {
    LocationData location = GetLocationData(locationId);
    location.residingNPCs.Remove(npcId);
}
```

---

## **Edge Cases**

1. **Travel while in activity:** Activity is cancelled/paused
2. **Travel to closed location:** Entry denied, time/money not spent
3. **Travel to location player owns but is evicted from:** Access denied
4. **Multiple players in same location (future MMO):** Shared instance
5. **Vehicle as location:** Special case, can travel while "in vehicle location"

---

## **Testing Checklist**
```
[ ] GetPlayerLocation returns current location
[ ] TravelToLocation moves player to new location
[ ] Travel advances time correctly
[ ] Travel costs money (gas or taxi)
[ ] Can't travel if can't afford
[ ] CheckLocationAccess denies entry to private locations
[ ] Public locations allow entry when open
[ ] Closed locations deny entry
[ ] Banned players can't enter even if public
[ ] GetAllowedActivities returns correct activities for location
[ ] Detection sensitivity varies by location type
[ ] NPCs can be added/removed from locations
[ ] OnLocationChanged fires with correct parameters
[ ] Scene loads when entering new location
