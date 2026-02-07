#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HustleEconomy.Editor
{
    public static class InputActionsGenerator
    {
        private const string PrimaryAssetPath = "Assets/Settings/Input/InputActions.inputactions";
        private const string ResourcesAssetPath = "Assets/Resources/InputActions.inputactions";

        [MenuItem("Tools/Input/Generate or Update InputActions")]
        public static void GenerateOrUpdateInputActions()
        {
#if ENABLE_INPUT_SYSTEM
            EnsureFolders();

            InputActionAsset primary = LoadOrCreateAsset(PrimaryAssetPath);
            ConfigureAsset(primary);
            SaveAsset(primary);

            InputActionAsset resourcesCopy = LoadOrCreateAsset(ResourcesAssetPath);
            ConfigureAsset(resourcesCopy);
            SaveAsset(resourcesCopy);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "InputActions generated/updated:\n" +
                $"- {PrimaryAssetPath}\n" +
                $"- {ResourcesAssetPath}");
#else
            Debug.LogWarning(
                "Generate or Update InputActions skipped: ENABLE_INPUT_SYSTEM is not defined. " +
                "Install/enable the Unity Input System package first.");
#endif
        }

        [MenuItem("Tools/Input/Ensure Resources InputActions")]
        public static void EnsureResourcesInputActions()
        {
#if ENABLE_INPUT_SYSTEM
            EnsureFolders();

            if (!File.Exists(PrimaryAssetPath))
            {
                Debug.LogWarning(
                    $"Ensure Resources InputActions: source asset missing at '{PrimaryAssetPath}'. " +
                    "Run Tools/Input/Generate or Update InputActions first.");
                return;
            }

            if (File.Exists(ResourcesAssetPath))
            {
                AssetDatabase.DeleteAsset(ResourcesAssetPath);
            }

            bool copied = AssetDatabase.CopyAsset(PrimaryAssetPath, ResourcesAssetPath);
            if (!copied)
            {
                Debug.LogError(
                    $"Ensure Resources InputActions: failed to copy '{PrimaryAssetPath}' to '{ResourcesAssetPath}'.");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Ensured Resources InputActions: {ResourcesAssetPath}");
#else
            Debug.LogWarning(
                "Ensure Resources InputActions skipped: ENABLE_INPUT_SYSTEM is not defined. " +
                "Install/enable the Unity Input System package first.");
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Settings"))
            {
                AssetDatabase.CreateFolder("Assets", "Settings");
            }

            if (!AssetDatabase.IsValidFolder("Assets/Settings/Input"))
            {
                AssetDatabase.CreateFolder("Assets/Settings", "Input");
            }

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }
        }

        private static InputActionAsset LoadOrCreateAsset(string path)
        {
            InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            if (asset != null)
            {
                if (asset.name != "InputActions")
                {
                    asset.name = "InputActions";
                }
                return asset;
            }

            InputActionAsset created = ScriptableObject.CreateInstance<InputActionAsset>();
            created.name = "InputActions";
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static void ConfigureAsset(InputActionAsset asset)
        {
            asset.name = "InputActions";

            InputActionMap playerMap = EnsureMap(asset, "Player");
            ConfigurePlayerMap(playerMap);

            InputActionMap uiMap = EnsureMap(asset, "UI");
            ConfigureUiMap(uiMap);

            EditorUtility.SetDirty(asset);
        }

        private static void ConfigurePlayerMap(InputActionMap map)
        {
            InputAction move = EnsureAction(map, "Move", InputActionType.Value, "Vector2");
            Ensure2DComposite(move, "<Keyboard>/w", "<Keyboard>/s", "<Keyboard>/a", "<Keyboard>/d");
            Ensure2DComposite(move, "<Keyboard>/upArrow", "<Keyboard>/downArrow", "<Keyboard>/leftArrow", "<Keyboard>/rightArrow");
            EnsureBinding(move, "<Gamepad>/leftStick");

            InputAction look = EnsureAction(map, "Look", InputActionType.Value, "Vector2");
            EnsureBinding(look, "<Mouse>/delta");
            EnsureBinding(look, "<Gamepad>/rightStick");

            InputAction run = EnsureAction(map, "Run", InputActionType.Button, "Button");
            EnsureBinding(run, "<Keyboard>/leftShift");
            EnsureBinding(run, "<Keyboard>/rightShift");
            EnsureBinding(run, "<Gamepad>/leftStickPress");

            InputAction interact = EnsureAction(map, "Interact", InputActionType.Button, "Button");
            EnsureBinding(interact, "<Keyboard>/e");
            EnsureBinding(interact, "<Gamepad>/buttonSouth");

            InputAction pause = EnsureAction(map, "Pause", InputActionType.Button, "Button");
            EnsureBinding(pause, "<Keyboard>/escape");
            EnsureBinding(pause, "<Gamepad>/start");
        }

        private static void ConfigureUiMap(InputActionMap map)
        {
            InputAction navigate = EnsureAction(map, "Navigate", InputActionType.Value, "Vector2");
            Ensure2DComposite(
                navigate,
                "<Keyboard>/upArrow",
                "<Keyboard>/downArrow",
                "<Keyboard>/leftArrow",
                "<Keyboard>/rightArrow");
            EnsureBinding(navigate, "<Gamepad>/dpad");
            EnsureBinding(navigate, "<Gamepad>/leftStick");

            InputAction submit = EnsureAction(map, "Submit", InputActionType.Button, "Button");
            EnsureBinding(submit, "<Keyboard>/enter");
            EnsureBinding(submit, "<Gamepad>/buttonSouth");

            InputAction cancel = EnsureAction(map, "Cancel", InputActionType.Button, "Button");
            EnsureBinding(cancel, "<Keyboard>/escape");
            EnsureBinding(cancel, "<Gamepad>/buttonEast");

            InputAction point = EnsureAction(map, "Point", InputActionType.PassThrough, "Vector2");
            EnsureBinding(point, "<Mouse>/position");

            InputAction click = EnsureAction(map, "Click", InputActionType.PassThrough, "Button");
            EnsureBinding(click, "<Mouse>/leftButton");

            InputAction rightClick = EnsureAction(map, "RightClick", InputActionType.PassThrough, "Button");
            EnsureBinding(rightClick, "<Mouse>/rightButton");

            InputAction middleClick = EnsureAction(map, "MiddleClick", InputActionType.PassThrough, "Button");
            EnsureBinding(middleClick, "<Mouse>/middleButton");

            InputAction scrollWheel = EnsureAction(map, "ScrollWheel", InputActionType.PassThrough, "Vector2");
            EnsureBinding(scrollWheel, "<Mouse>/scroll");

            InputAction trackedPos = EnsureAction(map, "TrackedDevicePosition", InputActionType.PassThrough, "Vector3");
            EnsureBinding(trackedPos, "<XRController>/devicePosition");

            InputAction trackedRot = EnsureAction(map, "TrackedDeviceOrientation", InputActionType.PassThrough, "Quaternion");
            EnsureBinding(trackedRot, "<XRController>/deviceRotation");
        }

        private static InputActionMap EnsureMap(InputActionAsset asset, string mapName)
        {
            InputActionMap map = asset.FindActionMap(mapName, false);
            if (map != null)
            {
                return map;
            }

            return asset.AddActionMap(mapName);
        }

        private static InputAction EnsureAction(
            InputActionMap map,
            string actionName,
            InputActionType type,
            string expectedControlType)
        {
            InputAction action = map.FindAction(actionName, false);
            if (action == null)
            {
                action = map.AddAction(actionName, type);
                if (!string.IsNullOrEmpty(expectedControlType))
                {
                    action.expectedControlType = expectedControlType;
                }
                return action;
            }

            // Non-destructive update: keep existing type to avoid mutating authored assets
            // across Input System versions where action.type may be read-only.

            if (action.expectedControlType != expectedControlType)
            {
                action.expectedControlType = expectedControlType;
            }

            return action;
        }

        private static void EnsureBinding(InputAction action, string path)
        {
            if (HasBinding(action, path, null, false, false))
            {
                return;
            }

            action.AddBinding(path);
        }

        private static void Ensure2DComposite(
            InputAction action,
            string up,
            string down,
            string left,
            string right)
        {
            if (Has2DComposite(action, up, down, left, right))
            {
                return;
            }

            action.AddCompositeBinding("2DVector")
                .With("Up", up)
                .With("Down", down)
                .With("Left", left)
                .With("Right", right);
        }

        private static bool Has2DComposite(InputAction action, string up, string down, string left, string right)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding b = action.bindings[i];
                if (!b.isComposite || b.path != "2DVector")
                {
                    continue;
                }

                string foundUp = string.Empty;
                string foundDown = string.Empty;
                string foundLeft = string.Empty;
                string foundRight = string.Empty;

                for (int j = i + 1; j < action.bindings.Count; j++)
                {
                    InputBinding part = action.bindings[j];
                    if (!part.isPartOfComposite)
                    {
                        break;
                    }

                    if (part.name == "Up") foundUp = part.path;
                    if (part.name == "Down") foundDown = part.path;
                    if (part.name == "Left") foundLeft = part.path;
                    if (part.name == "Right") foundRight = part.path;
                }

                if (foundUp == up && foundDown == down && foundLeft == left && foundRight == right)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasBinding(
            InputAction action,
            string path,
            string name,
            bool isComposite,
            bool isPartOfComposite)
        {
            foreach (InputBinding binding in action.bindings)
            {
                if (binding.path != path)
                {
                    continue;
                }

                if (binding.isComposite != isComposite || binding.isPartOfComposite != isPartOfComposite)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(name) && binding.name != name)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static void SaveAsset(InputActionAsset asset)
        {
            EditorUtility.SetDirty(asset);
            string path = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                AssetDatabase.ImportAsset(path);
            }
        }
#endif
    }
}
#endif
