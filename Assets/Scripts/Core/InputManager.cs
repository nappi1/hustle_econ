using System;
using System.Collections.Generic;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Core
{
    public class InputManager : MonoBehaviour
    {
        public enum InputContext
        {
            Gameplay,
            UI,
            Phone,
            Minigame,
            Cutscene,
            Driving
        }

        public enum InputAction
        {
            MoveForward,
            MoveBackward,
            MoveLeft,
            MoveRight,
            Run,
            Interact,
            ToggleCamera,
            OpenPhone,
            OpenMenu,
            Submit,
            Cancel,
            NavigateUp,
            NavigateDown,
            NavigateLeft,
            NavigateRight,
            MinigameAction1,
            MinigameAction2,
            MinigameAction3,
            Accelerate,
            Brake,
            SteerLeft,
            SteerRight
        }

        private enum InputBackend
        {
            None,
            Legacy,
            NewInputSystem
        }

        [Serializable]
        public class KeyBinding
        {
            public InputAction action;
            public KeyCode primaryKey;
            public KeyCode alternateKey;
            public string gamepadButton;
        }

        [Serializable]
        public class InputSettings
        {
            public List<KeyBinding> keyBindings;
            public float mouseSensitivity = 1.0f;
            public bool invertYAxis = false;
            public float deadZone = 0.15f;
        }

        [Serializable]
        public class InputState
        {
            public InputContext currentContext;
            public Vector2 movementInput;
            public Vector2 mouseInput;
            public bool isRunning;
            public Dictionary<InputAction, bool> actionStates;
            public Dictionary<InputAction, bool> actionDowns;
            public Dictionary<InputAction, bool> actionUps;
        }

        [Header("Input Backend")]
        [SerializeField] private bool preferNewInputSystem = true;
        [SerializeField] private bool enableDebug = false;

#if ENABLE_INPUT_SYSTEM
        [Header("New Input System")]
        [SerializeField] private InputActionAsset actions;
        [SerializeField] private string playerActionMapName = "Player";
        [SerializeField] private string uiActionMapName = "UI";
        [SerializeField] private string moveActionName = "Move";
        [SerializeField] private string lookActionName = "Look";
        [SerializeField] private string runActionName = "Run";
        [SerializeField] private string interactActionName = "Interact";

        private InputActionMap playerActionMap;
        private InputActionMap uiActionMap;
        private UnityEngine.InputSystem.InputAction moveAction;
        private UnityEngine.InputSystem.InputAction lookAction;
        private readonly Dictionary<InputAction, UnityEngine.InputSystem.InputAction> mappedNewActions =
            new Dictionary<InputAction, UnityEngine.InputSystem.InputAction>();
#endif

        private static InputManager instance;
        public static InputManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<InputManager>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("InputManager");
                        instance = go.AddComponent<InputManager>();
                    }
                }
                return instance;
            }
        }

        public event Action<InputContext> OnContextChanged;
        public event Action<InputAction> OnActionPressed;
        public event Action<InputAction> OnActionReleased;
        public event Action<InputAction, KeyCode> OnKeyRebound;

        private InputSettings settings;
        private Dictionary<InputAction, KeyBinding> bindings;
        private InputState state;
        private Stack<InputContext> contextStack;

        private readonly Dictionary<InputAction, bool> simulatedActions = new Dictionary<InputAction, bool>();
        private Vector2 simulatedMovement;
        private bool useSimulatedMovement;
        private bool useSimulatedActions;

        private InputBackend activeBackend;
        private bool warnedNoInputAvailable;
        private bool warnedMissingInputAsset;
        private float lastDebugMovementLogTime;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }

        public void Initialize()
        {
            settings = new InputSettings();
            bindings = new Dictionary<InputAction, KeyBinding>();
            state = new InputState
            {
                actionStates = new Dictionary<InputAction, bool>(),
                actionDowns = new Dictionary<InputAction, bool>(),
                actionUps = new Dictionary<InputAction, bool>()
            };
            contextStack = new Stack<InputContext>();
            state.currentContext = InputContext.Gameplay;
            warnedMissingInputAsset = false;

            InitializeDefaultBindings();
            EnsureDefaultInputNames();
            RefreshBackend();
        }

        public bool IsUsingNewInputSystem()
        {
            return activeBackend == InputBackend.NewInputSystem;
        }

        public bool IsUsingLegacyInput()
        {
            return activeBackend == InputBackend.Legacy;
        }

        private void Update()
        {
            state.actionDowns.Clear();
            state.actionUps.Clear();

            ProcessMovementInput();
            ProcessActionInput();
            ProcessMouseInput();
            DebugLogMovementIfNeeded();
        }

        public void LoadKeyBindings(InputSettings inputSettings)
        {
            if (inputSettings == null)
            {
                Debug.LogWarning("LoadKeyBindings: settings is null");
                return;
            }

            settings = inputSettings;
            bindings.Clear();

            if (settings.keyBindings != null)
            {
                foreach (KeyBinding binding in settings.keyBindings)
                {
                    bindings[binding.action] = binding;
                }
            }
        }

        public void SetContext(InputContext context)
        {
            if (state.currentContext == context)
            {
                return;
            }

            state.currentContext = context;
            state.movementInput = Vector2.zero;
            state.isRunning = false;

            switch (context)
            {
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

        public InputContext GetContext()
        {
            return state.currentContext;
        }

        public void PushContext(InputContext context)
        {
            contextStack.Push(state.currentContext);
            SetContext(context);
        }

        public void PopContext()
        {
            if (contextStack.Count > 0)
            {
                InputContext previousContext = contextStack.Pop();
                SetContext(previousContext);
            }
            else
            {
                Debug.LogWarning("PopContext: context stack is empty");
            }
        }

        public Vector3 GetMovementInput()
        {
            return new Vector3(state.movementInput.x, 0f, state.movementInput.y);
        }

        public Vector2 GetMovementRaw()
        {
            return state.movementInput;
        }

        public bool IsRunning()
        {
            return state.isRunning;
        }

        public bool GetAction(InputAction action)
        {
            return state.actionStates.ContainsKey(action) && state.actionStates[action];
        }

        public bool GetActionDown(InputAction action)
        {
            return state.actionDowns.ContainsKey(action) && state.actionDowns[action];
        }

        public bool GetActionUp(InputAction action)
        {
            return state.actionUps.ContainsKey(action) && state.actionUps[action];
        }

        public Vector2 GetMouseDelta()
        {
            return state.mouseInput;
        }

        public Vector3 GetMousePosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (IsUsingNewInputSystem() && Mouse.current != null)
            {
                return Mouse.current.position.ReadValue();
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePosition;
#else
            return Vector3.zero;
#endif
        }

        public bool IsMouseOverUI()
        {
            return UnityEngine.EventSystems.EventSystem.current != null &&
                   UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
        }

        public void RebindKey(InputAction action, KeyCode newKey, bool isPrimary = true)
        {
            if (!bindings.TryGetValue(action, out KeyBinding binding))
            {
                Debug.LogWarning($"RebindKey: no binding found for {action}");
                return;
            }

            if (isPrimary)
            {
                binding.primaryKey = newKey;
            }
            else
            {
                binding.alternateKey = newKey;
            }

            bindings[action] = binding;
            OnKeyRebound?.Invoke(action, newKey);
        }

        public KeyBinding GetBinding(InputAction action)
        {
            bindings.TryGetValue(action, out KeyBinding binding);
            return binding;
        }

        public void ResetToDefaults()
        {
            InitializeDefaultBindings();
        }

        public void SimulateInput(InputAction action, bool pressed)
        {
            useSimulatedActions = true;
            bool wasPressed = simulatedActions.ContainsKey(action) && simulatedActions[action];
            simulatedActions[action] = pressed;

            state.actionStates[action] = pressed;

            if (pressed && !wasPressed)
            {
                state.actionDowns[action] = true;
                OnActionPressed?.Invoke(action);
            }

            if (!pressed && wasPressed)
            {
                state.actionUps[action] = true;
                OnActionReleased?.Invoke(action);
            }
        }

        public void SimulateMovement(Vector2 direction)
        {
            useSimulatedMovement = true;
            simulatedMovement = direction;
        }

        public void SetContextForTesting(InputContext context)
        {
            SetContext(context);
        }

        public void ClearFrameForTesting()
        {
            state.actionDowns.Clear();
            state.actionUps.Clear();
        }

        public InputState GetStateForTesting()
        {
            return state;
        }

        private void InitializeDefaultBindings()
        {
            settings.keyBindings = new List<KeyBinding>
            {
                new KeyBinding { action = InputAction.MoveForward, primaryKey = KeyCode.W, alternateKey = KeyCode.UpArrow },
                new KeyBinding { action = InputAction.MoveBackward, primaryKey = KeyCode.S, alternateKey = KeyCode.DownArrow },
                new KeyBinding { action = InputAction.MoveLeft, primaryKey = KeyCode.A, alternateKey = KeyCode.LeftArrow },
                new KeyBinding { action = InputAction.MoveRight, primaryKey = KeyCode.D, alternateKey = KeyCode.RightArrow },
                new KeyBinding { action = InputAction.Run, primaryKey = KeyCode.LeftShift, alternateKey = KeyCode.RightShift },
                new KeyBinding { action = InputAction.Interact, primaryKey = KeyCode.E, alternateKey = KeyCode.None },
                new KeyBinding { action = InputAction.ToggleCamera, primaryKey = KeyCode.F, alternateKey = KeyCode.None },
                new KeyBinding { action = InputAction.OpenPhone, primaryKey = KeyCode.P, alternateKey = KeyCode.Tab },
                new KeyBinding { action = InputAction.OpenMenu, primaryKey = KeyCode.Escape, alternateKey = KeyCode.None },
                new KeyBinding { action = InputAction.Submit, primaryKey = KeyCode.Return, alternateKey = KeyCode.Space },
                new KeyBinding { action = InputAction.Cancel, primaryKey = KeyCode.Escape, alternateKey = KeyCode.Backspace }
            };

            bindings.Clear();
            foreach (KeyBinding binding in settings.keyBindings)
            {
                bindings[binding.action] = binding;
            }
        }

        private void RefreshBackend()
        {
            warnedNoInputAvailable = false;
            activeBackend = InputBackend.None;
            EnsureDefaultInputNames();

#if ENABLE_INPUT_SYSTEM
            bool shouldTryNewInput = preferNewInputSystem;
#if !ENABLE_LEGACY_INPUT_MANAGER
            shouldTryNewInput = true;
#endif

            if (shouldTryNewInput && InitializeNewInputSystem())
            {
                activeBackend = InputBackend.NewInputSystem;
                DebugLogPath($"InputManager active path: NewInputSystem (map={GetActiveMapName()})");
                return;
            }
            DisableNewInputMaps();
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            activeBackend = InputBackend.Legacy;
            DebugLogPath("InputManager active path: Legacy");
#else
            WarnNoInputAvailable();
#endif
        }

        private void ProcessMovementInput()
        {
            if (state.currentContext != InputContext.Gameplay && state.currentContext != InputContext.Phone)
            {
                state.movementInput = Vector2.zero;
                state.isRunning = false;
                return;
            }

            if (useSimulatedMovement)
            {
                state.movementInput = simulatedMovement;
                if (state.movementInput.magnitude > 1f)
                {
                    state.movementInput.Normalize();
                }

                state.isRunning = useSimulatedActions &&
                                  simulatedActions.ContainsKey(InputAction.Run) &&
                                  simulatedActions[InputAction.Run];
                return;
            }

            Vector2 movement = Vector2.zero;
            bool running = false;

            if (activeBackend == InputBackend.NewInputSystem)
            {
                movement = ReadNewMovement();
                running = ReadNewRunPressed();
            }
            else if (activeBackend == InputBackend.Legacy)
            {
                movement = ReadLegacyMovement();
                running = GetLegacyKeyHeld(InputAction.Run);
            }
            else
            {
                WarnNoInputAvailable();
            }

            if (movement.magnitude > 1f)
            {
                movement.Normalize();
            }

            state.movementInput = movement;
            state.isRunning = running;
        }

        private void ProcessActionInput()
        {
            foreach (KeyValuePair<InputAction, KeyBinding> binding in bindings)
            {
                InputAction action = binding.Key;
                bool wasPressed = state.actionStates.ContainsKey(action) && state.actionStates[action];

                bool isPressed;
                bool down;
                bool up;

                if (useSimulatedActions)
                {
                    isPressed = simulatedActions.ContainsKey(action) && simulatedActions[action];
                    down = isPressed && !wasPressed;
                    up = !isPressed && wasPressed;
                }
                else if (activeBackend == InputBackend.NewInputSystem)
                {
                    isPressed = GetNewActionCurrent(action);
                    down = GetNewActionDown(action, wasPressed);
                    up = GetNewActionUp(action, wasPressed);
                }
                else if (activeBackend == InputBackend.Legacy)
                {
                    isPressed = GetLegacyKeyHeld(action);
                    down = isPressed && !wasPressed;
                    up = !isPressed && wasPressed;
                }
                else
                {
                    isPressed = false;
                    down = false;
                    up = false;
                }

                state.actionStates[action] = isPressed;

                if (down)
                {
                    state.actionDowns[action] = true;
                    OnActionPressed?.Invoke(action);
                }

                if (up)
                {
                    state.actionUps[action] = true;
                    OnActionReleased?.Invoke(action);
                }
            }
        }

        private void ProcessMouseInput()
        {
            if (activeBackend == InputBackend.NewInputSystem)
            {
                state.mouseInput = ReadNewLook();
                return;
            }

            if (activeBackend == InputBackend.Legacy)
            {
                state.mouseInput = ReadLegacyLook();
                return;
            }

            state.mouseInput = Vector2.zero;
        }

        private Vector2 ReadLegacyMovement()
        {
            float horizontal = 0f;
            float vertical = 0f;

            if (GetLegacyKeyHeld(InputAction.MoveForward)) vertical += 1f;
            if (GetLegacyKeyHeld(InputAction.MoveBackward)) vertical -= 1f;
            if (GetLegacyKeyHeld(InputAction.MoveLeft)) horizontal -= 1f;
            if (GetLegacyKeyHeld(InputAction.MoveRight)) horizontal += 1f;

            return new Vector2(horizontal, vertical);
        }

        private Vector2 ReadLegacyLook()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#else
            return Vector2.zero;
#endif
        }

        private bool GetLegacyKeyHeld(InputAction action)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (!bindings.ContainsKey(action))
            {
                return false;
            }

            KeyBinding binding = bindings[action];
            return Input.GetKey(binding.primaryKey) ||
                   (binding.alternateKey != KeyCode.None && Input.GetKey(binding.alternateKey));
#else
            return false;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private bool InitializeNewInputSystem()
        {
            mappedNewActions.Clear();
            playerActionMap = null;
            uiActionMap = null;
            moveAction = null;
            lookAction = null;

            if (actions == null)
            {
                actions = Resources.Load<InputActionAsset>("InputActions");
                if (actions == null)
                {
                    LogMissingInputAssetErrorOnce();
                    return false;
                }
            }

            playerActionMap = FindActionMap(actions, playerActionMapName, moveActionName, "Movement");
            if (playerActionMap == null)
            {
                Debug.LogError(
                    $"InputManager: action map '{playerActionMapName}' not found in InputActionAsset '{actions.name}'. " +
                    $"Available maps: {GetAvailableMapNames(actions)}");
                return false;
            }

            uiActionMap = FindActionMap(actions, uiActionMapName, "Submit", "Navigate");

            moveAction = FindAction(playerActionMap, moveActionName, "Movement");
            lookAction = FindAction(playerActionMap, lookActionName, "MouseDelta", "PointerDelta");

            if (moveAction == null)
            {
                Debug.LogError(
                    $"InputManager: action '{moveActionName}' not found in map '{playerActionMap.name}'. " +
                    $"Available actions: {GetAvailableActionNames(playerActionMap)}");
            }

            MapAction(InputAction.Run, playerActionMap, runActionName, "Sprint");
            MapAction(InputAction.Interact, playerActionMap, interactActionName, "Use");
            MapAction(InputAction.ToggleCamera, playerActionMap, "ToggleCamera", "SwitchCamera");
            MapAction(InputAction.OpenPhone, playerActionMap, "OpenPhone", "Phone");
            MapAction(InputAction.OpenMenu, playerActionMap, "OpenMenu", "Pause", "Menu");

            if (uiActionMap != null)
            {
                MapAction(InputAction.Submit, uiActionMap, "Submit");
                MapAction(InputAction.Cancel, uiActionMap, "Cancel", "Back");
                MapAction(InputAction.NavigateUp, uiActionMap, "NavigateUp");
                MapAction(InputAction.NavigateDown, uiActionMap, "NavigateDown");
                MapAction(InputAction.NavigateLeft, uiActionMap, "NavigateLeft");
                MapAction(InputAction.NavigateRight, uiActionMap, "NavigateRight");
            }

            playerActionMap.Enable();
            return true;
        }

        private void DisableNewInputMaps()
        {
            if (playerActionMap != null)
            {
                playerActionMap.Disable();
            }
        }

        private InputActionMap FindActionMap(InputActionAsset asset, string preferredName, params string[] requiredActionHints)
        {
            if (asset == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(preferredName))
            {
                InputActionMap map = asset.FindActionMap(preferredName, false);
                if (map != null)
                {
                    return map;
                }
            }

            foreach (InputActionMap map in asset.actionMaps)
            {
                if (requiredActionHints == null || requiredActionHints.Length == 0)
                {
                    return map;
                }

                foreach (string hint in requiredActionHints)
                {
                    if (FindAction(map, hint) != null)
                    {
                        return map;
                    }
                }
            }

            return null;
        }

        private UnityEngine.InputSystem.InputAction FindAction(InputActionMap map, params string[] names)
        {
            if (map == null || names == null)
            {
                return null;
            }

            foreach (string actionName in names)
            {
                if (string.IsNullOrEmpty(actionName))
                {
                    continue;
                }

                UnityEngine.InputSystem.InputAction action = map.FindAction(actionName, false);
                if (action != null)
                {
                    return action;
                }
            }

            return null;
        }

        private void MapAction(InputAction internalAction, InputActionMap map, params string[] names)
        {
            UnityEngine.InputSystem.InputAction found = FindAction(map, names);
            if (found != null)
            {
                mappedNewActions[internalAction] = found;
            }
            else
            {
                DebugLogPath($"InputManager: missing action mapping for {internalAction} ({string.Join("/", names)}) in map '{map?.name}'.");
            }
        }

        private Vector2 ReadNewMovement()
        {
            return moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        }

        private Vector2 ReadNewLook()
        {
            return lookAction != null ? lookAction.ReadValue<Vector2>() : Vector2.zero;
        }

        private bool ReadNewRunPressed()
        {
            return mappedNewActions.TryGetValue(InputAction.Run, out UnityEngine.InputSystem.InputAction run) &&
                   (run.IsPressed() || run.ReadValue<float>() > 0.5f);
        }

        private bool GetNewActionCurrent(InputAction action)
        {
            if (action == InputAction.MoveForward) return state.movementInput.y > 0.1f;
            if (action == InputAction.MoveBackward) return state.movementInput.y < -0.1f;
            if (action == InputAction.MoveLeft) return state.movementInput.x < -0.1f;
            if (action == InputAction.MoveRight) return state.movementInput.x > 0.1f;

            if (!mappedNewActions.TryGetValue(action, out UnityEngine.InputSystem.InputAction mapped) || mapped == null)
            {
                return false;
            }

            return mapped.IsPressed() || mapped.ReadValue<float>() > 0.5f;
        }

        private bool GetNewActionDown(InputAction action, bool wasPressed)
        {
            if (action == InputAction.MoveForward ||
                action == InputAction.MoveBackward ||
                action == InputAction.MoveLeft ||
                action == InputAction.MoveRight)
            {
                return GetNewActionCurrent(action) && !wasPressed;
            }

            if (!mappedNewActions.TryGetValue(action, out UnityEngine.InputSystem.InputAction mapped) || mapped == null)
            {
                return false;
            }

            return mapped.WasPressedThisFrame();
        }

        private bool GetNewActionUp(InputAction action, bool wasPressed)
        {
            if (action == InputAction.MoveForward ||
                action == InputAction.MoveBackward ||
                action == InputAction.MoveLeft ||
                action == InputAction.MoveRight)
            {
                return !GetNewActionCurrent(action) && wasPressed;
            }

            if (!mappedNewActions.TryGetValue(action, out UnityEngine.InputSystem.InputAction mapped) || mapped == null)
            {
                return false;
            }

            return mapped.WasReleasedThisFrame();
        }

        private string GetActiveMapName()
        {
            return playerActionMap != null ? playerActionMap.name : "none";
        }
 
        private static string GetAvailableMapNames(InputActionAsset asset)
        {
            if (asset == null || asset.actionMaps.Count == 0)
            {
                return "(none)";
            }

            List<string> names = new List<string>();
            foreach (InputActionMap map in asset.actionMaps)
            {
                names.Add(map.name);
            }

            return string.Join(", ", names);
        }

        private static string GetAvailableActionNames(InputActionMap map)
        {
            if (map == null || map.actions.Count == 0)
            {
                return "(none)";
            }

            List<string> names = new List<string>();
            foreach (UnityEngine.InputSystem.InputAction action in map.actions)
            {
                names.Add(action.name);
            }

            return string.Join(", ", names);
        }
#endif

        private void DebugLogPath(string message)
        {
            if (enableDebug)
            {
                Debug.Log(message);
            }
        }

        private void WarnNoInputAvailable()
        {
            if (warnedNoInputAvailable)
            {
                return;
            }

            warnedNoInputAvailable = true;
            Debug.LogWarning("InputManager: no available input backend (new input map unavailable, legacy disabled).");
        }

        private void EnsureDefaultInputNames()
        {
#if ENABLE_INPUT_SYSTEM
            if (string.IsNullOrWhiteSpace(playerActionMapName)) playerActionMapName = "Player";
            if (string.IsNullOrWhiteSpace(uiActionMapName)) uiActionMapName = "UI";
            if (string.IsNullOrWhiteSpace(moveActionName)) moveActionName = "Move";
            if (string.IsNullOrWhiteSpace(lookActionName)) lookActionName = "Look";
            if (string.IsNullOrWhiteSpace(runActionName)) runActionName = "Run";
            if (string.IsNullOrWhiteSpace(interactActionName)) interactActionName = "Interact";
#endif
        }

        private void LogMissingInputAssetErrorOnce()
        {
#if ENABLE_INPUT_SYSTEM
            if (warnedMissingInputAsset)
            {
                return;
            }

            warnedMissingInputAsset = true;
            Debug.LogError(
                "InputManager: InputActionAsset not found. " +
                "Assign an InputActionAsset in the InputManager inspector, or place " +
                "'InputActions.inputactions' at Assets/Resources/InputActions.inputactions.");
#endif
        }

        private void DebugLogMovementIfNeeded()
        {
            if (!enableDebug)
            {
                return;
            }

            if (state.movementInput.sqrMagnitude < 0.001f)
            {
                return;
            }

            if (Time.unscaledTime - lastDebugMovementLogTime < 0.5f)
            {
                return;
            }

            lastDebugMovementLogTime = Time.unscaledTime;
            Debug.Log($"InputManager movement: {state.movementInput} (run={state.isRunning})");
        }
    }
}
