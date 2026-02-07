# V2 Update (Aligned to Current Codebase)

This V2 spec reflects the current implementation. See docs/IMPLEMENTATION_DISCREPANCIES.md for cross-system gaps.

## Implementation Notes

- Save/Load APIs not implemented; relies on InputManager and TimeEnergySystem.
- Uses legacy input path via InputManager (new Input System not yet wired).

---
# PLAYER CONTROLLER SPECIFICATION

**System:** PlayerController  
**Namespace:** HustleEconomy.Core  
**Dependencies:** CameraController, InputManager, EntitySystem, TimeEnergySystem, LocationSystem, ActivitySystem  
**Purpose:** Handle player character movement, physics, and state in 3D world

---

## **I. OVERVIEW**

### **What This System Does**

PlayerController manages the player character's:
- 3D movement (walk, run, sit, stand)
- Physics interactions (gravity, collisions)
- Animation state (idle, walking, running, sitting)
- Interaction raycast (what player is looking at)
- Position synchronization with LocationSystem

### **Design Philosophy**

- **Grounded movement:** No jumping, no sprint-sliding, realistic walking speed
- **Context-aware:** Movement disabled when sitting, driving, or in activities
- **Energy-integrated:** Running drains energy, walking is free
- **Simple controls:** WASD + Shift to run + E to interact

---

## **II. DATA STRUCTURES**

### **Enums**

```csharp
public enum PlayerMovementState {
    Idle,           // Standing still
    Walking,        // Moving at normal speed
    Running,        // Moving at sprint speed (drains energy)
    Sitting,        // Seated (no movement allowed)
    Driving,        // In vehicle (no movement allowed)
    Locked          // Activity in progress (no movement allowed)
}

public enum PlayerPosture {
    Standing,
    Sitting,
    Driving
}
```

### **Classes**

```csharp
[System.Serializable]
public class MovementSettings {
    public float walkSpeed = 3.5f;           // m/s
    public float runSpeed = 6.0f;            // m/s
    public float rotationSpeed = 720f;       // degrees/s
    public float runEnergyDrainRate = 5f;    // energy/minute while running
    public float gravity = -9.81f;           // Unity gravity
    public float groundCheckDistance = 0.2f; // Raycast distance for ground
}

[System.Serializable]
public class PlayerState {
    public string playerId;
    public PlayerMovementState movementState;
    public PlayerPosture posture;
    public Vector3 position;
    public Quaternion rotation;
    public bool isGrounded;
    public float currentSpeed;
}
```

---

## **III. PUBLIC API**

### **Core Methods**

```csharp
// Initialization
public void Initialize(string playerId);

// Movement Control
public void SetMovementEnabled(bool enabled);
public void SetMovementState(PlayerMovementState state);
public void SetPosture(PlayerPosture posture);

// Position Management
public void Teleport(Vector3 position, Quaternion rotation);
public Vector3 GetPosition();
public Quaternion GetRotation();
public Transform GetTransform();

// Interaction
public RaycastHit GetLookTarget(float maxDistance = 3f);
public bool IsLookingAt(GameObject target, float maxDistance = 3f);

// State Queries
public PlayerMovementState GetMovementState();
public PlayerPosture GetPosture();
public bool IsGrounded();
public bool CanMove();
public float GetCurrentSpeed();

// Testing Helpers
public void SetPositionForTesting(Vector3 position);
public void SetMovementStateForTesting(PlayerMovementState state);
public PlayerState GetStateForTesting();
```

### **Events**

```csharp
// Movement Events
public event Action<PlayerMovementState> OnMovementStateChanged;
public event Action<PlayerPosture> OnPostureChanged;
public event Action<Vector3> OnPositionChanged;

// Interaction Events
public event Action<GameObject> OnInteractionTargetChanged;
public event Action<RaycastHit> OnLookTargetChanged;

// Energy Events
public event Action<float> OnRunEnergyDrained;  // Amount drained this frame
```

---

## **IV. IMPLEMENTATION DETAILS**

### **Singleton Pattern**

```csharp
using UnityEngine;
using HustleEconomy.Core;

namespace HustleEconomy.Core {
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour {
        // Singleton
        private static PlayerController instance;
        public static PlayerController Instance {
            get {
                if (instance == null) {
                    instance = FindObjectOfType<PlayerController>();
                    if (instance == null) {
                        GameObject go = new GameObject("PlayerController");
                        instance = go.AddComponent<PlayerController>();
                    }
                }
                return instance;
            }
        }

        // Unity Components
        private CharacterController characterController;
        private Animator animator;

        // Settings
        [SerializeField] private MovementSettings settings = new MovementSettings();

        // State
        private PlayerState state = new PlayerState();
        private bool movementEnabled = true;
        private Vector3 velocity;
        private float lastRunEnergyDrain;

        private void Awake() {
            if (instance != null && instance != this) {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);

            characterController = GetComponent<CharacterController>();
            animator = GetComponent<Animator>();

            Initialize("player");
        }

        // ... (implementation continues)
    }
}
```

### **Movement Logic**

```csharp
private void Update() {
    if (!movementEnabled || !CanMove()) {
        return;
    }

    ProcessInput();
    ApplyGravity();
    UpdateAnimator();
    CheckGroundedState();
    DrainRunEnergy();
}

private void ProcessInput() {
    // Get input from InputManager
    Vector3 inputDirection = InputManager.Instance.GetMovementInput();
    bool isRunning = InputManager.Instance.IsRunning() && 
                     TimeEnergySystem.Instance.GetEnergy(state.playerId) > 10f;

    // Calculate movement
    float targetSpeed = isRunning ? settings.runSpeed : settings.walkSpeed;
    Vector3 moveDirection = transform.TransformDirection(inputDirection);
    Vector3 move = moveDirection * targetSpeed;

    // Apply movement
    characterController.Move(move * Time.deltaTime);

    // Update state
    PlayerMovementState newState = PlayerMovementState.Idle;
    if (inputDirection.magnitude > 0.1f) {
        newState = isRunning ? PlayerMovementState.Running : PlayerMovementState.Walking;
    }

    if (newState != state.movementState) {
        SetMovementState(newState);
    }

    // Rotate toward movement direction
    if (inputDirection.magnitude > 0.1f) {
        Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            settings.rotationSpeed * Time.deltaTime
        );
    }

    state.currentSpeed = characterController.velocity.magnitude;
}

private void ApplyGravity() {
    if (!characterController.isGrounded) {
        velocity.y += settings.gravity * Time.deltaTime;
    } else {
        velocity.y = -2f; // Small downward force to keep grounded
    }
    
    characterController.Move(velocity * Time.deltaTime);
}

private void DrainRunEnergy() {
    if (state.movementState == PlayerMovementState.Running) {
        float drainAmount = settings.runEnergyDrainRate * (Time.deltaTime / 60f);
        TimeEnergySystem.Instance.ChangeEnergy(state.playerId, -drainAmount, "Running");
        
        OnRunEnergyDrained?.Invoke(drainAmount);
        
        lastRunEnergyDrain = Time.time;
    }
}
```

### **Posture Management**

```csharp
public void SetPosture(PlayerPosture newPosture) {
    if (state.posture == newPosture) return;

    state.posture = newPosture;

    switch (newPosture) {
        case PlayerPosture.Sitting:
            SetMovementState(PlayerMovementState.Sitting);
            movementEnabled = false;
            animator?.SetBool("Sitting", true);
            break;

        case PlayerPosture.Driving:
            SetMovementState(PlayerMovementState.Driving);
            movementEnabled = false;
            animator?.SetBool("Driving", true);
            break;

        case PlayerPosture.Standing:
            movementEnabled = true;
            animator?.SetBool("Sitting", false);
            animator?.SetBool("Driving", false);
            if (state.movementState == PlayerMovementState.Sitting || 
                state.movementState == PlayerMovementState.Driving) {
                SetMovementState(PlayerMovementState.Idle);
            }
            break;
    }

    OnPostureChanged?.Invoke(newPosture);
}
```

### **Interaction Raycast**

```csharp
public RaycastHit GetLookTarget(float maxDistance = 3f) {
    Ray ray = new Ray(transform.position + Vector3.up * 1.6f, transform.forward);
    RaycastHit hit;
    
    if (Physics.Raycast(ray, out hit, maxDistance)) {
        return hit;
    }
    
    return default(RaycastHit);
}

public bool IsLookingAt(GameObject target, float maxDistance = 3f) {
    RaycastHit hit = GetLookTarget(maxDistance);
    return hit.collider != null && hit.collider.gameObject == target;
}
```

---

## **V. INTEGRATION POINTS**

### **With CameraController**

```csharp
// CameraController needs PlayerController transform
void Start() {
    CameraController.Instance.SetTarget(PlayerController.Instance.GetTransform());
}
```

### **With LocationSystem**

```csharp
// Update location when player moves between zones
private void OnTriggerEnter(Collider other) {
    if (other.CompareTag("LocationZone")) {
        string locationId = other.GetComponent<LocationZone>().locationId;
        LocationSystem.Instance.TravelTo(state.playerId, locationId);
    }
}
```

### **With ActivitySystem**

```csharp
// When activity starts, lock movement
void OnActivityStarted(string activityId) {
    if (ActivitySystem.Instance.RequiresPlayerLock(activityId)) {
        SetMovementState(PlayerMovementState.Locked);
    }
}
```

---

## **VI. TESTING CHECKLIST**

### **Unit Tests (30 minimum)**

**Movement Tests (10):**
- [ ] Player can walk forward/backward/left/right
- [ ] Player rotates toward movement direction
- [ ] Running is faster than walking
- [ ] Movement respects enabled/disabled state
- [ ] Sitting prevents movement
- [ ] Driving prevents movement
- [ ] Locked state prevents movement
- [ ] Walking does not drain energy
- [ ] Running drains energy at correct rate
- [ ] Movement stops when energy reaches zero while running

**Posture Tests (5):**
- [ ] Setting sitting posture disables movement
- [ ] Setting driving posture disables movement
- [ ] Setting standing posture enables movement
- [ ] Posture change fires event
- [ ] Cannot walk while sitting

**Interaction Tests (5):**
- [ ] GetLookTarget returns correct object within range
- [ ] GetLookTarget returns null beyond max distance
- [ ] IsLookingAt returns true when facing target
- [ ] IsLookingAt returns false when facing away
- [ ] Raycast origin is at eye level (1.6m)

**State Tests (5):**
- [ ] GetPosition returns current position
- [ ] GetRotation returns current rotation
- [ ] Teleport updates position and rotation instantly
- [ ] GetMovementState returns current state
- [ ] IsGrounded returns correct ground state

**Integration Tests (5):**
- [ ] Movement state change fires event
- [ ] Posture change fires event
- [ ] Running drains energy event fires
- [ ] Position change fires event (when moved >0.1m)
- [ ] LocationSystem updated when entering zone

---

## **VII. PERFORMANCE CONSIDERATIONS**

**Update Optimization:**
- Only process input when movement enabled
- Raycast pooling for interaction checks (max 1 per frame)
- Animator updates batched by Unity

**Physics Optimization:**
- CharacterController more efficient than Rigidbody for player
- Sphere collision for performance
- Limit raycast distance to 3m (interaction range)

---

## **VIII. SAVE/LOAD**

```csharp
[System.Serializable]
public class PlayerControllerSaveData {
    public string playerId;
    public Vector3 position;
    public Quaternion rotation;
    public PlayerMovementState movementState;
    public PlayerPosture posture;
}

public PlayerControllerSaveData GetSaveData() {
    return new PlayerControllerSaveData {
        playerId = state.playerId,
        position = transform.position,
        rotation = transform.rotation,
        movementState = state.movementState,
        posture = state.posture
    };
}

public void LoadSaveData(PlayerControllerSaveData data) {
    state.playerId = data.playerId;
    Teleport(data.position, data.rotation);
    SetMovementState(data.movementState);
    SetPosture(data.posture);
}
```

---

## **IX. KNOWN LIMITATIONS**

- No jumping (intentional design choice - grounded realism)
- No crouching (not needed for gameplay)
- No ledge climbing (keeps movement simple)
- No swimming (water is instant-kill or prevented)
- Single player only (multiplayer not in scope)

---

## **X. FUTURE ENHANCEMENTS (Post-V1.0)**

- Drunk stumbling animation when intoxicated
- Injury system affecting movement speed
- Fitness level affecting run speed/duration
- Weather affecting movement (rain slows down)
- Carry weight affecting speed (holding items)

---

**END OF SPECIFICATION**

