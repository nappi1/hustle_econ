#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Core;

namespace HustleEconomy.Editor {
    /// <summary>
    /// Editor script to generate essential prefabs (Player, Camera rig, Interactable templates).
    /// Usage: Unity menu bar > Hustle Economy > Create Player Prefab
    /// </summary>
    public class PrefabGenerator : MonoBehaviour {
        
        [MenuItem("Hustle Economy/Create Player Prefab")]
        public static void CreatePlayerPrefab() {
            Debug.Log("=== Creating Player Prefab ===");
            
            // Create player GameObject
            GameObject player = new GameObject("Player");
            player.tag = "Player";
            player.layer = LayerMask.NameToLayer("Default");
            
            // Add CharacterController for movement
            CharacterController controller = player.AddComponent<CharacterController>();
            controller.radius = 0.4f;
            controller.height = 1.8f;
            controller.center = new Vector3(0, 0.9f, 0);
            controller.slopeLimit = 45f;
            controller.stepOffset = 0.3f;
            controller.skinWidth = 0.08f;
            controller.minMoveDistance = 0.001f;
            
            // Add PlayerController
            PlayerController playerController = player.AddComponent<PlayerController>();
            
            // Create camera rig as child
            GameObject cameraRig = new GameObject("CameraRig");
            cameraRig.transform.SetParent(player.transform, false);
            cameraRig.transform.localPosition = new Vector3(0, 1.6f, 0); // Eye level
            
            // Create camera
            GameObject cameraObj = new GameObject("MainCamera");
            cameraObj.transform.SetParent(cameraRig.transform, false);
            cameraObj.tag = "MainCamera";
            
            Camera cam = cameraObj.AddComponent<Camera>();
            cam.fieldOfView = 60f; // Default third-person FOV
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1000f;
            cam.clearFlags = CameraClearFlags.Skybox;
            
            // Add AudioListener
            cameraObj.AddComponent<AudioListener>();
            
            // Add CameraController
            CameraController camController = cameraObj.AddComponent<CameraController>();
            
            // Position camera for third-person view
            cameraObj.transform.localPosition = new Vector3(0, 0.5f, -3f);
            cameraObj.transform.localRotation = Quaternion.Euler(10, 0, 0);
            
            // Create placeholder visual (capsule)
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "PlayerVisual";
            visual.transform.SetParent(player.transform, false);
            visual.transform.localPosition = new Vector3(0, 0.9f, 0);
            visual.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);
            
            // Remove collider from visual (CharacterController handles collision)
            DestroyImmediate(visual.GetComponent<Collider>());
            
            // Color player blue
            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null) {
                renderer.material.color = new Color(0.3f, 0.5f, 0.9f);
            }
            
            // Ensure Prefabs directory exists
            if (!System.IO.Directory.Exists("Assets/Prefabs")) {
                System.IO.Directory.CreateDirectory("Assets/Prefabs");
            }
            
            // Save as prefab
            string prefabPath = "Assets/Prefabs/Player.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(player, prefabPath);
            
            // Clean up scene instance
            DestroyImmediate(player);
            
            Debug.Log($"✅ Player prefab created: {prefabPath}");
            Debug.Log("Remember to:");
            Debug.Log("  1. Replace PlayerVisual capsule with character model");
            Debug.Log("  2. Adjust camera positions for your character height");
            
            // Select the prefab
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }
        
        [MenuItem("Hustle Economy/Create Interactable Template")]
        public static void CreateInteractableTemplate() {
            Debug.Log("=== Creating Interactable Template ===");
            
            GameObject template = GameObject.CreatePrimitive(PrimitiveType.Cube);
            template.name = "InteractableTemplate";
            template.transform.localScale = new Vector3(1, 1, 1);
            
            // Add Interactable component
            Interactable interactable = template.AddComponent<Interactable>();
            interactable.interactionType = InteractionSystem.InteractionType.Examine;
            interactable.targetId = "object_id";
            interactable.promptText = "Press E to interact";
            interactable.interactionRange = 2f;
            interactable.requiresLineOfSight = true;
            
            // Color it yellow for visibility
            var renderer = template.GetComponent<Renderer>();
            if (renderer != null) {
                renderer.material.color = new Color(1f, 0.9f, 0.3f);
            }
            
            // Ensure Prefabs directory exists
            if (!System.IO.Directory.Exists("Assets/Prefabs")) {
                System.IO.Directory.CreateDirectory("Assets/Prefabs");
            }
            
            // Save as prefab
            string prefabPath = "Assets/Prefabs/InteractableTemplate.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(template, prefabPath);
            
            // Clean up scene instance
            DestroyImmediate(template);
            
            Debug.Log($"✅ Interactable template created: {prefabPath}");
            Debug.Log("Drag this into scenes and customize the Interactable component settings");
            
            // Select the prefab
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }
        
        [MenuItem("Hustle Economy/Create NPC Template")]
        public static void CreateNPCTemplate() {
            Debug.Log("=== Creating NPC Template ===");
            
            GameObject npc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            npc.name = "NPC_Template";
            npc.tag = "NPC";
            npc.transform.localScale = new Vector3(0.5f, 1f, 0.5f);
            
            // Add NavMeshAgent (for patrol/movement)
            UnityEngine.AI.NavMeshAgent agent = npc.AddComponent<UnityEngine.AI.NavMeshAgent>();
            agent.speed = 1.5f;
            agent.angularSpeed = 120f;
            agent.acceleration = 8f;
            agent.stoppingDistance = 0.5f;
            agent.radius = 0.25f;
            agent.height = 1.8f;
            
            // Color it gray
            var renderer = npc.GetComponent<Renderer>();
            if (renderer != null) {
                renderer.material.color = new Color(0.6f, 0.6f, 0.6f);
            }
            
            // Ensure Prefabs directory exists
            if (!System.IO.Directory.Exists("Assets/Prefabs")) {
                System.IO.Directory.CreateDirectory("Assets/Prefabs");
            }
            
            // Save as prefab
            string prefabPath = "Assets/Prefabs/NPC_Template.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(npc, prefabPath);
            
            // Clean up scene instance
            DestroyImmediate(npc);
            
            Debug.Log($"✅ NPC template created: {prefabPath}");
            Debug.Log("Remember to:");
            Debug.Log("  1. Replace capsule with character model");
            Debug.Log("  2. Bake NavMesh in scene for NPC navigation");
            Debug.Log("  3. Register NPC with DetectionSystem and RelationshipSystem");
            
            // Select the prefab
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }
        
        [MenuItem("Hustle Economy/Create All Prefabs")]
        public static void CreateAllPrefabs() {
            Debug.Log("=== Creating All Prefabs ===");
            
            CreatePlayerPrefab();
            CreateInteractableTemplate();
            CreateNPCTemplate();
            
            Debug.Log("=== All prefabs created! ===");
        }
    }
}
#endif
