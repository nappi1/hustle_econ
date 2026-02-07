# CAMERA CONTROLLER SPECIFICATION

**System:** CameraController  
**Namespace:** HustleEconomy.Core  
**Dependencies:** PlayerController, ActivitySystem, PhoneUI  
**Purpose:** Manage camera modes, switching rules, and player view control

---

## **I. OVERVIEW**

### **What This System Does**

CameraController manages:
- Two camera modes: Third-person (default), First-person (jobs/phone/intimacy)
- Auto-switching based on player actions
- Manual toggle (F key) when not locked
- Camera positioning and field of view
- Smooth transitions between modes

### **Design Philosophy**

- **Third-person for exploration:** See character, easier navigation, life sim feel
- **First-person for focus:** Jobs, phone, computer work enables peripheral awareness gameplay
- **Environmental awareness:** First-person doesn't take fullscreen - world visible around UI
- **Player choice:** Can toggle unless locked (intimacy scenes)

---

## **II. DATA STRUCTURES**

### **Enums**

```csharp
public enum CameraMode {
    ThirdPerson,    // Over-shoulder, default
    FirstPerson     // Eye-level, jobs/phone/desk
}

public enum CameraTransitionSpeed {
    Instant,        // No lerp (teleport, load)
    Fast,           // 0.2s (toggle)
    Smooth          // 0.5s (auto-switch)
}
```

### **Classes**

```csharp
[System.Serializable]
public class CameraSettings {
    // Third-Person Settings
    public Vector3 thirdPersonOffset = new Vector3(0.5f, 1.5f, -3f);  // Over right shoulder
    public float thirdPersonFOV = 60f;
    public float thirdPersonRotationSpeed = 720f;  // degrees/s
    
    // First-Person Settings
    public Vector3 firstPersonOffset = new Vector3(0f, 1.6f, 0f);  // Eye level
    public float firstPersonFOV = 90f;  // Wider for peripheral awareness
    
    // Transition
    public float smoothTransitionTime = 0.5f;
    public float fastTransitionTime = 0.2f;
}

[System.Serializable]
public class CameraState {
    public CameraMode currentMode;
    public CameraMode targetMode;
    public bool isLocked;              // Cannot toggle during intimacy
    public float lockDuration;         // How long locked (for intimacy scenes)
    public bool isTransitioning;
    public float transitionProgress;   // 0-1
}
```

---

## **III. PUBLIC API**

### **Core Methods**

```csharp
// Initialization
public void Initialize();
public void SetTarget(Transform playerTransform);

// Mode Control
public void SetMode(CameraMode mode, bool locked = false, float lockDuration = 0f);
public void ToggleMode();
public void ForceMode(CameraMode mode);  // Ignores lock (for loading saves)

// Auto-Switch Triggers (called by other systems)
public void OnJobStarted();
public void OnJobEnded();
public void OnPhoneOpened();
public void OnPhoneClosed();
public void OnDeskSatDown();
public void OnDeskStoodUp();
public void OnIntimacyStarted();
public void OnIntimacyEnded();

// State Queries
public CameraMode GetCurrentMode();
public bool IsLocked();
public bool IsTransitioning();
public float GetCurrentFOV();

// Manual Control (for specific scenarios)
public void SetFOV(float fov);
public void SetPosition(Vector3 offset);

// Testing Helpers
public void SetModeForTesting(CameraMode mode);
public void SetLockedForTesting(bool locked);
public CameraState GetStateForTesting();
```

### **Events**

```csharp
// Mode Events
public event Action<CameraMode> OnModeChanged;
public event Action<CameraMode> OnModeChangeStarted;
public event Action<CameraMode> OnModeChangeCompleted;

// Lock Events
public event Action OnCameraLocked;
public event Action OnCameraUnlocked;

// Transition Events
public event Action OnTransitionStarted;
public event Action OnTransitionCompleted;
```

---

## **IV. IMPLEMENTATION DETAILS**

### **Singleton Pattern**

```csharp
using UnityEngine;
using HustleEconomy.Core;

namespace HustleEconomy.Core {
    public class CameraController : MonoBehaviour {
        // Singleton
        private static CameraController instance;
        public static CameraController Instance {
            get {
                if (instance == null) {
                    instance = FindObjectOfType<CameraController>();
                    if (instance == null) {
                        GameObject go = new GameObject("CameraController");
                        instance = go.AddComponent<CameraController>();
                    }
                }
                return instance;
            }
        }

        // Unity Components
        private Camera mainCamera;
        private Transform playerTransform;

        // Settings
        [SerializeField] private CameraSettings settings = new CameraSettings();

        // State
        private CameraState state = new CameraState();
        private Vector3 currentOffset;
        private float currentFOV;
        private float lockEndTime;

        private void Awake() {
            if (instance != null && instance != this) {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);

            Initialize();
        }

        public void Initialize() {
            mainCamera = Camera.main;
            if (mainCamera == null) {
                mainCamera = gameObject.AddComponent<Camera>();
            }

            state.currentMode = CameraMode.ThirdPerson;
            state.targetMode = CameraMode.ThirdPerson;
            currentOffset = settings.thirdPersonOffset;
            currentFOV = settings.thirdPersonFOV;

            ApplyCameraSettings(CameraMode.ThirdPerson, instant: true);
        }

        // ... (implementation continues)
    }
}
```

### **Mode Switching Logic**

```csharp
public void SetMode(CameraMode mode, bool locked = false, float lockDuration = 0f) {
    // Cannot switch if locked (unless this call is locking it)
    if (state.isLocked && !locked) {
        Debug.LogWarning($"Cannot switch camera mode - locked for {lockEndTime - Time.time:F1}s");
        return;
    }

    // Apply lock if requested
    if (locked) {
        state.isLocked = true;
        state.lockDuration = lockDuration;
        lockEndTime = Time.time + lockDuration;
        OnCameraLocked?.Invoke();
    }

    // Set target mode
    state.targetMode = mode;

    // If not transitioning, start transition
    if (!state.isTransitioning && state.currentMode != state.targetMode) {
        StartTransition(CameraTransitionSpeed.Smooth);
    }

    OnModeChangeStarted?.Invoke(mode);
}

public void ToggleMode() {
    if (state.isLocked) {
        Debug.LogWarning("Cannot toggle camera - locked");
        return;
    }

    CameraMode newMode = state.currentMode == CameraMode.ThirdPerson 
        ? CameraMode.FirstPerson 
        : CameraMode.ThirdPerson;

    state.targetMode = newMode;
    StartTransition(CameraTransitionSpeed.Fast);
}

private void StartTransition(CameraTransitionSpeed speed) {
    state.isTransitioning = true;
    state.transitionProgress = 0f;

    OnTransitionStarted?.Invoke();

    float transitionTime = speed == CameraTransitionSpeed.Fast 
        ? settings.fastTransitionTime 
        : settings.smoothTransitionTime;

    if (speed == CameraTransitionSpeed.Instant) {
        ApplyCameraSettings(state.targetMode, instant: true);
        CompleteTransition();
    } else {
        StartCoroutine(TransitionCoroutine(transitionTime));
    }
}

private IEnumerator TransitionCoroutine(float duration) {
    Vector3 startOffset = currentOffset;
    float startFOV = currentFOV;

    Vector3 targetOffset = state.targetMode == CameraMode.ThirdPerson 
        ? settings.thirdPersonOffset 
        : settings.firstPersonOffset;

    float targetFOV = state.targetMode == CameraMode.ThirdPerson 
        ? settings.thirdPersonFOV 
        : settings.firstPersonFOV;

    float elapsed = 0f;

    while (elapsed < duration) {
        elapsed += Time.deltaTime;
        state.transitionProgress = Mathf.Clamp01(elapsed / duration);

        // Smooth interpolation
        currentOffset = Vector3.Lerp(startOffset, targetOffset, state.transitionProgress);
        currentFOV = Mathf.Lerp(startFOV, targetFOV, state.transitionProgress);

        ApplyCurrentSettings();

        yield return null;
    }

    // Ensure final values
    currentOffset = targetOffset;
    currentFOV = targetFOV;
    ApplyCurrentSettings();

    CompleteTransition();
}

private void CompleteTransition() {
    state.currentMode = state.targetMode;
    state.isTransitioning = false;
    state.transitionProgress = 1f;

    OnModeChangeCompleted?.Invoke(state.currentMode);
    OnTransitionCompleted?.Invoke();
    OnModeChanged?.Invoke(state.currentMode);
}
```

### **Camera Positioning**

```csharp
private void LateUpdate() {
    if (playerTransform == null) return;

    UpdateLockTimer();
    UpdateCameraPosition();
}

private void UpdateCameraPosition() {
    // Calculate desired position based on current offset
    Vector3 targetPosition = playerTransform.position + 
                            playerTransform.TransformDirection(currentOffset);

    // Apply position
    mainCamera.transform.position = targetPosition;

    // Look at player head level for third-person
    if (state.currentMode == CameraMode.ThirdPerson) {
        Vector3 lookTarget = playerTransform.position + Vector3.up * 1.6f;
        mainCamera.transform.LookAt(lookTarget);
    } else {
        // First-person: rotation matches player
        mainCamera.transform.rotation = playerTransform.rotation;
    }

    // Apply FOV
    mainCamera.fieldOfView = currentFOV;
}

private void ApplyCameraSettings(CameraMode mode, bool instant = false) {
    if (instant) {
        currentOffset = mode == CameraMode.ThirdPerson 
            ? settings.thirdPersonOffset 
            : settings.firstPersonOffset;

        currentFOV = mode == CameraMode.ThirdPerson 
            ? settings.thirdPersonFOV 
            : settings.firstPersonFOV;

        ApplyCurrentSettings();
    }
}

private void ApplyCurrentSettings() {
    if (mainCamera != null) {
        mainCamera.fieldOfView = currentFOV;
    }
}

private void UpdateLockTimer() {
    if (state.isLocked && Time.time >= lockEndTime) {
        state.isLocked = false;
        OnCameraUnlocked?.Invoke();
    }
}
```

### **Auto-Switch Triggers**

```csharp
public void OnJobStarted() {
    SetMode(CameraMode.FirstPerson, locked: false);
}

public void OnJobEnded() {
    SetMode(CameraMode.ThirdPerson, locked: false);
}

public void OnPhoneOpened() {
    SetMode(CameraMode.FirstPerson, locked: false);
}

public void OnPhoneClosed() {
    // Only switch back if not doing something else that requires first-person
    if (!IsPlayerWorking() && !IsPlayerAtDesk()) {
        SetMode(CameraMode.ThirdPerson, locked: false);
    }
}

public void OnDeskSatDown() {
    SetMode(CameraMode.FirstPerson, locked: false);
}

public void OnDeskStoodUp() {
    if (!IsPlayerWorking() && !PhoneUI.Instance.IsPhoneOpen()) {
        SetMode(CameraMode.ThirdPerson, locked: false);
    }
}

public void OnIntimacyStarted() {
    SetMode(CameraMode.FirstPerson, locked: true, lockDuration: 2f);
}

public void OnIntimacyEnded() {
    // Lock auto-expires after 2 seconds, then player can toggle
    SetMode(CameraMode.ThirdPerson, locked: false);
}

// Helper queries
private bool IsPlayerWorking() {
    var activity = ActivitySystem.Instance.GetActiveActivity("player");
    return activity != null && activity.type == ActivityType.Work;
}

private bool IsPlayerAtDesk() {
    // TODO: Check PlayerController posture or position
    return PlayerController.Instance.GetPosture() == PlayerPosture.Sitting;
}
```

---

## **V. INTEGRATION POINTS**

### **With PlayerController**

```csharp
void Start() {
    // Camera follows player
    CameraController.Instance.SetTarget(PlayerController.Instance.GetTransform());
    
    // Player posture changes trigger camera
    PlayerController.Instance.OnPostureChanged += HandlePostureChange;
}

void HandlePostureChange(PlayerPosture posture) {
    switch (posture) {
        case PlayerPosture.Sitting:
            CameraController.Instance.OnDeskSatDown();
            break;
        case PlayerPosture.Standing:
            CameraController.Instance.OnDeskStoodUp();
            break;
    }
}
```

### **With ActivitySystem**

```csharp
// Activity start/end triggers camera mode
ActivitySystem.Instance.OnActivityStarted += (activityId) => {
    var activity = ActivitySystem.Instance.GetActivity(activityId);
    if (activity.type == ActivityType.Work) {
        CameraController.Instance.OnJobStarted();
    }
};

ActivitySystem.Instance.OnActivityEnded += (activityId) => {
    var activity = ActivitySystem.Instance.GetActivity(activityId);
    if (activity.type == ActivityType.Work) {
        CameraController.Instance.OnJobEnded();
    }
};
```

### **With PhoneUI**

```csharp
// Phone open/close triggers camera
PhoneUI.Instance.OnPhoneOpened += () => {
    CameraController.Instance.OnPhoneOpened();
};

PhoneUI.Instance.OnPhoneClosed += () => {
    CameraController.Instance.OnPhoneClosed();
};
```

---

## **VI. TESTING CHECKLIST**

### **Unit Tests (30 minimum)**

**Mode Switching Tests (10):**
- [ ] SetMode switches to third-person correctly
- [ ] SetMode switches to first-person correctly
- [ ] ToggleMode alternates between modes
- [ ] Cannot switch when locked
- [ ] Lock expires after duration
- [ ] ForceMode overrides lock
- [ ] Mode change fires events
- [ ] Transition completes after duration
- [ ] Instant mode change skips transition
- [ ] Current mode returns correct value

**Auto-Switch Tests (8):**
- [ ] Job start switches to first-person
- [ ] Job end switches to third-person
- [ ] Phone open switches to first-person
- [ ] Phone close switches to third-person (if not working)
- [ ] Desk sit switches to first-person
- [ ] Desk stand switches to third-person (if not working/phone)
- [ ] Intimacy locks camera for 2 seconds
- [ ] Intimacy ends unlocks camera

**Lock Tests (5):**
- [ ] Locked state prevents toggle
- [ ] Locked state prevents SetMode
- [ ] Lock expires after duration
- [ ] Lock fires lock/unlock events
- [ ] ForceMode bypasses lock

**Positioning Tests (4):**
- [ ] Third-person offset applied correctly
- [ ] First-person offset applied correctly
- [ ] FOV changes between modes
- [ ] Camera follows player transform

**Integration Tests (3):**
- [ ] Camera auto-switches when job starts
- [ ] Camera auto-switches when phone opens
- [ ] Camera stays first-person when both job and phone active

---

## **VII. PERFORMANCE CONSIDERATIONS**

**Optimization:**
- LateUpdate for smooth camera following
- Lerp transitions only when needed (not every frame)
- No raycasting or physics (pure transform math)
- Minimal GC allocations (Vector3 calculations cached)

---

## **VIII. SAVE/LOAD**

```csharp
[System.Serializable]
public class CameraControllerSaveData {
    public CameraMode currentMode;
    public bool wasLocked;  // Don't restore lock duration
}

public CameraControllerSaveData GetSaveData() {
    return new CameraControllerSaveData {
        currentMode = state.currentMode,
        wasLocked = state.isLocked
    };
}

public void LoadSaveData(CameraControllerSaveData data) {
    // Force mode without transition on load
    ForceMode(data.currentMode);
    
    // Don't restore lock (lock is temporary state)
}
```

---

## **IX. KNOWN LIMITATIONS**

- No camera collision detection (walks through walls in tight spaces)
- No dynamic FOV (motion blur, zoom effects)
- No camera shake effects
- No look-around while in first-person (head rotation locked to player)
- Single target only (multiplayer would need multiple cameras)

---

## **X. FUTURE ENHANCEMENTS (Post-V1.0)**

- Camera collision (pull closer when wall behind in third-person)
- Dynamic FOV (run faster = slightly wider FOV)
- Camera shake (drunk, car crash, explosions)
- Free look in first-person (mouse look independent of player rotation)
- Cinematic camera angles for cutscenes

---

**END OF SPECIFICATION**
