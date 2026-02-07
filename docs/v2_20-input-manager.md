# V2 Update (Aligned to Current Codebase)

This V2 spec reflects the current implementation. See docs/IMPLEMENTATION_DISCREPANCIES.md for cross-system gaps.

## Implementation Notes

- Legacy input is gated by ENABLE_LEGACY_INPUT_MANAGER; no native Input System bindings.
- Save/Load APIs not implemented.

---
# INPUT MANAGER SPECIFICATION

**System:** InputManager  
**Namespace:** HustleEconomy.Core  
**Dependencies:** None (foundational system)  
**Purpose:** Unified input handling for movement, interactions, and UI

---

## **I. OVERVIEW**

### **What This System Does**

InputManager handles all player input:
- Movement (WASD, controller stick)
- Actions (E to interact, F to toggle camera, Escape to open menu)
- UI navigation (mouse, keyboard, controller)
- Input context switching (gameplay vs UI vs phone)
- Rebindable controls

### **Design Philosophy**

- **Single source of truth:** All systems query InputManager, don't read Input directly
- **Context-aware:** Input behavior changes based on state (playing vs phone open vs menu)
- **Platform-agnostic:** Works with keyboard/mouse and gamepad
- **Simple first, expand later:** V1.0 focuses on keyboard/mouse, gamepad support later

---

## **II. DATA STRUCTURES**

### **Enums**

```csharp
public enum InputContext {
    Gameplay,       // Normal movement and interaction
    UI,             // Menu open, mouse cursor active
    Phone,          // Phone UI open, limited movement
    Minigame,       // Minigame active, custom controls
    Cutscene,       // No input except skip
    Driving         // Vehicle controls
}

public enum InputAction {
    // Movement
    MoveForward,
    MoveBackward,
    MoveLeft,
    MoveRight,
    Run,
    
    // Interaction
    Interact,
    ToggleCamera,
    OpenPhone,
    OpenMenu,
    
    // UI
    Submit,
    Cancel,
    NavigateUp,
    NavigateDown,
    NavigateLeft,
    NavigateRight,
    
    // Minigame (generic)
    MinigameAction1,
    MinigameAction2,
    MinigameAction3,
    
    // Driving
    Accelerate,
    Brake,
    SteerLeft,
    SteerRight
}
```

### **Classes**

```csharp
[System.Serializable]
public class KeyBinding {
    public InputAction action;
    public KeyCode primaryKey;
    public KeyCode alternateKey;
    public string gamepadButton;  // For future gamepad support
}

[System.Serializable]
public class InputSettings {
    public List<KeyBinding> keyBindings;
    public float mouseSensitivity = 1.0f;
    public bool invertYAxis = false;
    public float deadZone = 0.15f;  // For gamepad sticks
}

[System.Serializable]
public class InputState {
    public InputContext currentContext;
    public Vector2 movementInput;       // -1 to 1 for X/Y
    public Vector2 mouseInput;          // Mouse delta
    public bool isRunning;
    public Dictionary<InputAction, bool> actionStates;  // Held this frame
    public Dictionary<InputAction, bool> actionDowns;   // Pressed this frame
    public Dictionary<InputAction, bool> actionUps;     // Released this frame
}
```

---

## **III. PUBLIC API**

### **Core Methods**

```csharp
// Initialization
public void Initialize();
public void LoadKeyBindings(InputSettings settings);

// Context Management
public void SetContext(InputContext context);
public InputContext GetContext();
public void PushContext(InputContext context);  // Stack contexts
public void PopContext();                       // Return to previous

// Movement Input
public Vector3 GetMovementInput();  // Returns normalized direction
public Vector2 GetMovementRaw();    // Returns raw -1 to 1
public bool IsRunning();

// Action Input
public bool GetAction(InputAction action);           // Held
public bool GetActionDown(InputAction action);       // Pressed this frame
public bool GetActionUp(InputAction action);         // Released this frame

// Mouse/Cursor
public Vector2 GetMouseDelta();
public Vector3 GetMousePosition();
public bool IsMouseOverUI();

// Key Rebinding
public void RebindKey(InputAction action, KeyCode newKey, bool isPrimary = true);
public KeyBinding GetBinding(InputAction action);
public void ResetToDefaults();

// Testing Helpers
public void SimulateInput(InputAction action, bool pressed);
public void SimulateMovement(Vector2 direction);
public void SetContextForTesting(InputContext context);
public InputState GetStateForTesting();
```

### **Events**

```csharp
// Context Events
public event Action<InputContext> OnContextChanged;

// Action Events
public event Action<InputAction> OnActionPressed;
public event Action<InputAction> OnActionReleased;

// Rebinding Events
public event Action<InputAction, KeyCode> OnKeyRebound;
```

---

## **IV. IMPLEMENTATION DETAILS**

### **Singleton Pattern**

```csharp
using UnityEngine;
using System.Collections.Generic;

namespace HustleEconomy.Core {
    public class InputManager : MonoBehaviour {
        // Singleton
        private static InputManager instance;
        public static InputManager Instance {
            get {
                if (instance == null) {
                    instance = FindObjectOfType<InputManager>();
                    if (instance == null) {
                        GameObject go = new GameObject("InputManager");
                        instance = go.AddComponent<InputManager>();
                    }
                }
                return instance;
            }
        }

        // Settings
        private InputSettings settings;
        private Dictionary<InputAction, KeyBinding> bindings;

        // State
        private InputState state;
        private Stack<InputContext> contextStack;

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
            settings = new InputSettings();
            bindings = new Dictionary<InputAction, KeyBinding>();
            state = new InputState {
                actionStates = new Dictionary<InputAction, bool>(),
                actionDowns = new Dictionary<InputAction, bool>(),
                actionUps = new Dictionary<InputAction, bool>()
            };
            contextStack = new Stack<InputContext>();
            state.currentContext = InputContext.Gameplay;

            InitializeDefaultBindings();
        }

        // ... (implementation continues)
    }
}
```

### **Default Key Bindings**

```csharp
private void InitializeDefaultBindings() {
    settings.keyBindings = new List<KeyBinding> {
        // Movement
        new KeyBinding { action = InputAction.MoveForward, primaryKey = KeyCode.W, alternateKey = KeyCode.UpArrow },
        new KeyBinding { action = InputAction.MoveBackward, primaryKey = KeyCode.S, alternateKey = KeyCode.DownArrow },
        new KeyBinding { action = InputAction.MoveLeft, primaryKey = KeyCode.A, alternateKey = KeyCode.LeftArrow },
        new KeyBinding { action = InputAction.MoveRight, primaryKey = KeyCode.D, alternateKey = KeyCode.RightArrow },
        new KeyBinding { action = InputAction.Run, primaryKey = KeyCode.LeftShift, alternateKey = KeyCode.RightShift },
        
        // Interaction
        new KeyBinding { action = InputAction.Interact, primaryKey = KeyCode.E, alternateKey = KeyCode.None },
        new KeyBinding { action = InputAction.ToggleCamera, primaryKey = KeyCode.F, alternateKey = KeyCode.None },
        new KeyBinding { action = InputAction.OpenPhone, primaryKey = KeyCode.P, alternateKey = KeyCode.Tab },
        new KeyBinding { action = InputAction.OpenMenu, primaryKey = KeyCode.Escape, alternateKey = KeyCode.None },
        
        // UI
        new KeyBinding { action = InputAction.Submit, primaryKey = KeyCode.Return, alternateKey = KeyCode.Space },
        new KeyBinding { action = InputAction.Cancel, primaryKey = KeyCode.Escape, alternateKey = KeyCode.Backspace },
    };

    // Build lookup dictionary
    foreach (var binding in settings.keyBindings) {
        bindings[binding.action] = binding;
    }
}
```

### **Input Processing**

```csharp
private void Update() {
    // Clear frame-specific states
    state.actionDowns.Clear();
    state.actionUps.Clear();

    ProcessMovementInput();
    ProcessActionInput();
    ProcessMouseInput();
}

private void ProcessMovementInput() {
    // Only process movement in Gameplay or Phone contexts
    if (state.currentContext != InputContext.Gameplay && 
        state.currentContext != InputContext.Phone) {
        state.movementInput = Vector2.zero;
        state.isRunning = false;
        return;
    }

    float horizontal = 0f;
    float vertical = 0f;

    // WASD / Arrow keys
    if (GetKeyHeld(InputAction.MoveForward)) vertical += 1f;
    if (GetKeyHeld(InputAction.MoveBackward)) vertical -= 1f;
    if (GetKeyHeld(InputAction.MoveLeft)) horizontal -= 1f;
    if (GetKeyHeld(InputAction.MoveRight)) horizontal += 1f;

    state.movementInput = new Vector2(horizontal, vertical);
    
    // Normalize diagonal movement
    if (state.movementInput.magnitude > 1f) {
        state.movementInput.Normalize();
    }

    // Running
    state.isRunning = GetKeyHeld(InputAction.Run);
}

private void ProcessActionInput() {
    foreach (var binding in bindings) {
        InputAction action = binding.Key;
        
        bool wasPressed = state.actionStates.ContainsKey(action) && state.actionStates[action];
        bool isPressed = GetKeyHeld(action);

        // Update held state
        state.actionStates[action] = isPressed;

        // Detect down (wasn't pressed last frame, is pressed this frame)
        if (isPressed && !wasPressed) {
            state.actionDowns[action] = true;
            OnActionPressed?.Invoke(action);
        }

        // Detect up (was pressed last frame, isn't pressed this frame)
        if (!isPressed && wasPressed) {
            state.actionUps[action] = true;
            OnActionReleased?.Invoke(action);
        }
    }
}

private void ProcessMouseInput() {
    // Mouse delta for camera rotation (future feature)
    state.mouseInput = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
}

private bool GetKeyHeld(InputAction action) {
    if (!bindings.ContainsKey(action)) return false;

    KeyBinding binding = bindings[action];
    
    return Input.GetKey(binding.primaryKey) || 
           (binding.alternateKey != KeyCode.None && Input.GetKey(binding.alternateKey));
}
```

### **Public Input Queries**

```csharp
public Vector3 GetMovementInput() {
    // Convert 2D input to 3D direction (Y becomes Z)
    return new Vector3(state.movementInput.x, 0f, state.movementInput.y);
}

public Vector2 GetMovementRaw() {
    return state.movementInput;
}

public bool IsRunning() {
    return state.isRunning;
}

public bool GetAction(InputAction action) {
    return state.actionStates.ContainsKey(action) && state.actionStates[action];
}

public bool GetActionDown(InputAction action) {
    return state.actionDowns.ContainsKey(action) && state.actionDowns[action];
}

public bool GetActionUp(InputAction action) {
    return state.actionUps.ContainsKey(action) && state.actionUps[action];
}
```

### **Context Management**

```csharp
public void SetContext(InputContext context) {
    if (state.currentContext == context) return;

    InputContext previousContext = state.currentContext;
    state.currentContext = context;

    // Clear input state when switching contexts
    state.movementInput = Vector2.zero;
    state.isRunning = false;

    // Show/hide cursor based on context
    switch (context) {
        case InputContext.Gameplay:
        case InputContext.Driving:
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            break;

        case InputContext.UI:
        case InputContext.Phone:
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            break;

        case InputContext.Cutscene:
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            break;
    }

    OnContextChanged?.Invoke(context);
}

public void PushContext(InputContext context) {
    contextStack.Push(state.currentContext);
    SetContext(context);
}

public void PopContext() {
    if (contextStack.Count > 0) {
        InputContext previousContext = contextStack.Pop();
        SetContext(previousContext);
    } else {
        Debug.LogWarning("Cannot pop context - stack is empty");
    }
}
```

---

## **V. INTEGRATION POINTS**

### **With PlayerController**

```csharp
// PlayerController queries InputManager every frame
void Update() {
    Vector3 moveInput = InputManager.Instance.GetMovementInput();
    bool isRunning = InputManager.Instance.IsRunning();
    
    ProcessMovement(moveInput, isRunning);
}
```

### **With UI Systems**

```csharp
// When phone opens, change input context
void OpenPhone() {
    InputManager.Instance.PushContext(InputContext.Phone);
    // Movement still works, but mouse is visible
}

void ClosePhone() {
    InputManager.Instance.PopContext();  // Returns to Gameplay
}

// When menu opens, block all gameplay input
void OpenMenu() {
    InputManager.Instance.PushContext(InputContext.UI);
    // No movement, mouse visible
}
```

### **With CameraController**

```csharp
// Listen for toggle camera input
void Update() {
    if (InputManager.Instance.GetActionDown(InputAction.ToggleCamera)) {
        CameraController.Instance.ToggleMode();
    }
}
```

---

## **VI. TESTING CHECKLIST**

### **Unit Tests (30 minimum)**

**Movement Input Tests (8):**
- [ ] W key sets forward movement
- [ ] S key sets backward movement
- [ ] A key sets left movement
- [ ] D key sets right movement
- [ ] Diagonal movement normalized correctly
- [ ] Running state detected when Shift held
- [ ] Movement zero when no keys pressed
- [ ] Movement blocked in UI context

**Action Input Tests (8):**
- [ ] GetAction returns true when key held
- [ ] GetActionDown returns true on press frame only
- [ ] GetActionUp returns true on release frame only
- [ ] Actions fire events correctly
- [ ] Alternate keys work same as primary
- [ ] Actions work in correct contexts
- [ ] Actions blocked in wrong contexts
- [ ] Multiple actions can be held simultaneously

**Context Tests (6):**
- [ ] SetContext changes current context
- [ ] SetContext fires event
- [ ] PushContext saves previous context
- [ ] PopContext restores previous context
- [ ] Cursor hidden in Gameplay context
- [ ] Cursor visible in UI context

**Rebinding Tests (5):**
- [ ] RebindKey updates binding
- [ ] RebindKey fires event
- [ ] New binding works immediately
- [ ] GetBinding returns correct binding
- [ ] ResetToDefaults restores original keys

**Integration Tests (3):**
- [ ] PlayerController receives movement input
- [ ] CameraController receives toggle input
- [ ] UI systems change context correctly

---

## **VII. PERFORMANCE CONSIDERATIONS**

**Optimization:**
- Dictionary lookups for bindings (O(1))
- Clear-and-rebuild pattern for frame states (minimal GC)
- No string comparisons (enum-based)
- Context switching is rare (no per-frame cost)

---

## **VIII. SAVE/LOAD**

```csharp
[System.Serializable]
public class InputManagerSaveData {
    public InputSettings settings;
}

public InputManagerSaveData GetSaveData() {
    return new InputManagerSaveData {
        settings = settings
    };
}

public void LoadSaveData(InputManagerSaveData data) {
    LoadKeyBindings(data.settings);
}
```

---

## **IX. KNOWN LIMITATIONS**

- No gamepad support in V1.0 (keyboard/mouse only)
- No mouse sensitivity settings (fixed at 1.0)
- No input recording/replay (for testing)
- No combo/sequence detection (for special moves)
- No rebind conflict detection (can rebind to same key)

---

## **X. FUTURE ENHANCEMENTS (Post-V1.0)**

- Full gamepad support (Xbox, PlayStation, Switch)
- Mouse sensitivity slider
- Invert Y axis option
- Input hints system (show context-appropriate prompts)
- Accessibility options (one-hand mode, sticky keys)
- Macro support (bind sequences to single key)

---

**END OF SPECIFICATION**

