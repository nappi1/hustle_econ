#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Core;
using UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace HustleEconomy.Editor {
    /// <summary>
    /// Editor script to automatically build the CoreSystems scene with all persistent systems and UI.
    /// Usage: Unity menu bar > Hustle Economy > Build Core Scene
    /// </summary>
    public class CoreSceneBuilder : MonoBehaviour {
        
        [MenuItem("Hustle Economy/Build Core Scene")]
        public static void BuildCoreScene() {
            // Create new empty scene
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            
            Debug.Log("=== Building Core Scene ===");
            
            // Create Systems GameObject
            CreateSystemsGameObject();
            
            // Create UI Canvas
            CreateUICanvas();
            
            // Create EventSystem for UI input
            CreateEventSystem();
            
            // Save scene
            string scenePath = "Assets/Scenes/CoreSystems.unity";
            
            // Ensure Scenes directory exists
            if (!System.IO.Directory.Exists("Assets/Scenes")) {
                System.IO.Directory.CreateDirectory("Assets/Scenes");
            }
            
            EditorSceneManager.SaveScene(newScene, scenePath);
            
            Debug.Log($"✅ Core scene created: {scenePath}");
            Debug.Log("Remember to:");
            Debug.Log("  1. Add scene to Build Settings (or use menu: Hustle Economy > Add Core Scene to Build Settings)");
            Debug.Log("  2. Assign UI prefabs/references in Inspector if needed");
        }
        
        private static void CreateSystemsGameObject() {
            GameObject systems = new GameObject("CoreSystems");
            
            Debug.Log("Adding core systems...");
            
            // Phase 1: Foundational Systems
            systems.AddComponent<EntitySystem>();
            systems.AddComponent<TimeEnergySystem>();
            systems.AddComponent<EconomySystem>();
            
            // Phase 2: NPC Systems
            systems.AddComponent<ReputationSystem>();
            systems.AddComponent<RelationshipSystem>();
            systems.AddComponent<DetectionSystem>();
            
            // Phase 3: Activity Systems
            systems.AddComponent<SkillSystem>();
            systems.AddComponent<JobSystem>();
            systems.AddComponent<ActivitySystem>();
            systems.AddComponent<MinigameSystem>();
            
            // Phase 4: Content Systems
            systems.AddComponent<LocationSystem>();
            systems.AddComponent<InventorySystem>();
            systems.AddComponent<EventSystem>();
            systems.AddComponent<HeatSystem>();
            systems.AddComponent<IntoxicationSystem>();
            systems.AddComponent<BodySystem>();
            systems.AddComponent<AdultContentSystem>();
            
            // Phase 5: Presentation Layer
            systems.AddComponent<InputManager>();
            systems.AddComponent<GameManager>();
            systems.AddComponent<InteractionSystem>();
            
            Debug.Log($"✅ Added {systems.GetComponents<Component>().Length - 1} system components");
        }
        
        private static void CreateUICanvas() {
            Debug.Log("Creating UI Canvas...");
            
            // Create root UI GameObject
            GameObject uiRoot = new GameObject("UI");
            
            // Add Canvas
            Canvas canvas = uiRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            
            // Add CanvasScaler
            UnityEngine.UI.CanvasScaler scaler = uiRoot.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            
            // Add GraphicRaycaster
            uiRoot.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            
            // Create HUD
            GameObject hud = new GameObject("HUD");
            hud.transform.SetParent(uiRoot.transform, false);
            hud.AddComponent<HUDController>();
            
            // Create PhoneUI
            GameObject phone = new GameObject("Phone");
            phone.transform.SetParent(uiRoot.transform, false);
            phone.AddComponent<PhoneUI>();
            
            // Create MinigameUI
            GameObject minigame = new GameObject("MinigameUI");
            minigame.transform.SetParent(uiRoot.transform, false);
            minigame.AddComponent<MinigameUI>();
            
            Debug.Log("✅ UI Canvas created with HUD, Phone, and MinigameUI");
        }
        
        private static void CreateEventSystem() {
            UnityEngine.EventSystems.EventSystem eventSystem = UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
            GameObject eventSystemGO = eventSystem != null ? eventSystem.gameObject : new GameObject("EventSystem");
            if (eventSystem == null)
            {
                eventSystem = eventSystemGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            }

#if ENABLE_INPUT_SYSTEM
            if (eventSystemGO.GetComponent<InputSystemUIInputModule>() == null)
            {
                eventSystemGO.AddComponent<InputSystemUIInputModule>();
            }

            UnityEngine.EventSystems.StandaloneInputModule legacyModule = eventSystemGO.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            if (legacyModule != null)
            {
                UnityEngine.Object.DestroyImmediate(legacyModule);
            }

            Debug.Log("✅ EventSystem configured with InputSystemUIInputModule");
#else
            if (eventSystemGO.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>() == null)
            {
                eventSystemGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            Debug.Log("✅ EventSystem configured with StandaloneInputModule");
#endif
        }
        
        [MenuItem("Hustle Economy/Add Core Scene to Build Settings")]
        public static void AddToBuildSettings() {
            string scenePath = "Assets/Scenes/CoreSystems.unity";
            
            if (!System.IO.File.Exists(scenePath)) {
                Debug.LogError("CoreSystems.unity not found! Build the scene first.");
                return;
            }
            
            // Get current scenes in build settings
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            
            // Check if already added
            bool alreadyAdded = false;
            foreach (var scene in scenes) {
                if (scene.path == scenePath) {
                    alreadyAdded = true;
                    break;
                }
            }
            
            if (!alreadyAdded) {
                scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
                Debug.Log($"✅ Added {scenePath} to Build Settings at index 0");
            } else {
                Debug.Log($"Scene {scenePath} already in Build Settings");
            }
        }
    }
}
#endif
