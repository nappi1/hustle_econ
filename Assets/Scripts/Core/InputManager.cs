using System;
using System.Collections.Generic;
using UnityEngine;

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

        [System.Serializable]
        public class KeyBinding
        {
            public InputAction action;
            public KeyCode primaryKey;
            public KeyCode alternateKey;
            public string gamepadButton;
        }

        [System.Serializable]
        public class InputSettings
        {
            public List<KeyBinding> keyBindings;
            public float mouseSensitivity = 1.0f;
            public bool invertYAxis = false;
            public float deadZone = 0.15f;
        }

        [System.Serializable]
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

        private Vector2 simulatedMovement;
        private Dictionary<InputAction, bool> simulatedActions = new Dictionary<InputAction, bool>();
        private bool useSimulatedMovement;
        private bool useSimulatedActions;

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

            InitializeDefaultBindings();
        }

        private void Update()
        {
            state.actionDowns.Clear();
            state.actionUps.Clear();

            ProcessMovementInput();
            ProcessActionInput();
            ProcessMouseInput();
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
                foreach (var binding in settings.keyBindings)
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
            return Input.mousePosition;
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
            state.movementInput = direction;
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
            foreach (var binding in settings.keyBindings)
            {
                bindings[binding.action] = binding;
            }
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
                if (useSimulatedActions)
                {
                    state.isRunning = simulatedActions.ContainsKey(InputAction.Run) && simulatedActions[InputAction.Run];
                }
                else
                {
                    state.isRunning = false;
                }
                return;
            }

            float horizontal = 0f;
            float vertical = 0f;

            if (GetKeyHeld(InputAction.MoveForward)) vertical += 1f;
            if (GetKeyHeld(InputAction.MoveBackward)) vertical -= 1f;
            if (GetKeyHeld(InputAction.MoveLeft)) horizontal -= 1f;
            if (GetKeyHeld(InputAction.MoveRight)) horizontal += 1f;

            state.movementInput = new Vector2(horizontal, vertical);

            if (state.movementInput.magnitude > 1f)
            {
                state.movementInput.Normalize();
            }

            bool runHeld = useSimulatedActions && simulatedActions.ContainsKey(InputAction.Run)
                ? simulatedActions[InputAction.Run]
                : GetKeyHeld(InputAction.Run);
            state.isRunning = runHeld;
        }

        private void ProcessActionInput()
        {
            foreach (var binding in bindings)
            {
                InputAction action = binding.Key;

                bool wasPressed = state.actionStates.ContainsKey(action) && state.actionStates[action];
                bool isPressed;
                if (useSimulatedActions)
                {
                    isPressed = simulatedActions.ContainsKey(action) && simulatedActions[action];
                }
                else
                {
                    isPressed = GetKeyHeld(action);
                }

                state.actionStates[action] = isPressed;

                if (isPressed && !wasPressed)
                {
                    state.actionDowns[action] = true;
                    OnActionPressed?.Invoke(action);
                }

                if (!isPressed && wasPressed)
                {
                    state.actionUps[action] = true;
                    OnActionReleased?.Invoke(action);
                }
            }
        }

        private void ProcessMouseInput()
        {
            state.mouseInput = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        }

        private bool GetKeyHeld(InputAction action)
        {
            if (!bindings.ContainsKey(action))
            {
                return false;
            }

            KeyBinding binding = bindings[action];
            return Input.GetKey(binding.primaryKey) ||
                   (binding.alternateKey != KeyCode.None && Input.GetKey(binding.alternateKey));
        }
    }
}
