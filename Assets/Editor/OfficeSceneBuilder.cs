#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using Core;

namespace HustleEconomy.Editor {
    /// <summary>
    /// Editor script to build the Office test scene with placeholder geometry and interactables.
    /// Usage: Unity menu bar > Hustle Economy > Build Office Scene
    /// </summary>
    public class OfficeSceneBuilder : MonoBehaviour {
        
        [MenuItem("Hustle Economy/Build Office Scene")]
        public static void BuildOfficeScene() {
            // Create new scene
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            
            Debug.Log("=== Building Office Scene ===");
            
            // Create environment
            CreateEnvironment();
            
            // Create interactables
            CreateJanitorialCloset();
            CreateDirtySpots();
            CreateExitDoor();
            CreateDesk();
            
            // Create NPCs
            CreateBossNPC();
            
            // Create player spawn point
            CreatePlayerSpawn();
            
            // Save scene
            string scenePath = "Assets/Scenes/Office.unity";
            
            if (!System.IO.Directory.Exists("Assets/Scenes")) {
                System.IO.Directory.CreateDirectory("Assets/Scenes");
            }
            
            EditorSceneManager.SaveScene(newScene, scenePath);
            
            Debug.Log($"✅ Office scene created: {scenePath}");
            Debug.Log("Remember to:");
            Debug.Log("  1. Add scene to Build Settings");
            Debug.Log("  2. Replace primitive placeholders with Synty assets");
            Debug.Log("  3. Bake NavMesh for boss patrol");
            Debug.Log("  4. Add lighting and polish");
        }
        
        private static void CreateEnvironment() {
            Debug.Log("Creating environment...");
            
            // Floor (10x10m)
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(1, 1, 1); // 10x10 units
            
            // Add floor material tag
            floor.tag = "Ground";
            
            // Walls
            CreateWall("WallNorth", new Vector3(0, 1.5f, 5), new Vector3(10, 3, 0.2f));
            CreateWall("WallSouth", new Vector3(0, 1.5f, -5), new Vector3(10, 3, 0.2f));
            CreateWall("WallEast", new Vector3(5, 1.5f, 0), new Vector3(0.2f, 3, 10));
            CreateWall("WallWest", new Vector3(-5, 1.5f, 0), new Vector3(0.2f, 3, 10));
            
            Debug.Log("✅ Environment created (floor + 4 walls)");
        }
        
        private static void CreateWall(string name, Vector3 position, Vector3 scale) {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.position = position;
            wall.transform.localScale = scale;
            
            // Make walls gray
            var renderer = wall.GetComponent<Renderer>();
            if (renderer != null) {
                renderer.material.color = new Color(0.7f, 0.7f, 0.7f);
            }
        }
        
        private static void CreateJanitorialCloset() {
            Debug.Log("Creating janitorial closet...");
            
            GameObject closet = GameObject.CreatePrimitive(PrimitiveType.Cube);
            closet.name = "JanitorialCloset";
            closet.transform.position = new Vector3(-4, 0.5f, -4);
            closet.transform.localScale = new Vector3(1, 1, 1);
            
            // Color it blue for visibility
            var renderer = closet.GetComponent<Renderer>();
            if (renderer != null) {
                renderer.material.color = new Color(0.2f, 0.4f, 0.8f);
            }
            
            // Add Interactable component
            Interactable interactable = closet.AddComponent<Interactable>();
            interactable.interactionType = InteractionSystem.InteractionType.StartJob;
            interactable.targetId = "job1";
            interactable.promptText = "Press E to start shift";
            interactable.interactionRange = 2.5f;
            interactable.requiresLineOfSight = true;
            
            Debug.Log("✅ Janitorial closet created with Interactable");
        }
        
        private static void CreateDirtySpots() {
            Debug.Log("Creating dirty spots for minigame...");
            
            Vector3[] spotPositions = new Vector3[] {
                new Vector3(-2, 0.1f, 2),
                new Vector3(1, 0.1f, 3),
                new Vector3(3, 0.1f, -1),
                new Vector3(-1, 0.1f, -2),
                new Vector3(2, 0.1f, 1)
            };
            
            for (int i = 0; i < spotPositions.Length; i++) {
                GameObject spot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                spot.name = $"DirtySpot_{i + 1}";
                spot.transform.position = spotPositions[i];
                spot.transform.localScale = new Vector3(0.3f, 0.1f, 0.3f);
                
                // Color it brown/dirty
                var renderer = spot.GetComponent<Renderer>();
                if (renderer != null) {
                    renderer.material.color = new Color(0.4f, 0.3f, 0.2f);
                }
                
                // Tag for minigame system
                spot.tag = "DirtySpot";
            }
            
            Debug.Log($"✅ Created {spotPositions.Length} dirty spots");
        }
        
        private static void CreateExitDoor() {
            Debug.Log("Creating exit door...");
            
            GameObject door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            door.name = "ExitDoor";
            door.transform.position = new Vector3(4.5f, 1f, 0);
            door.transform.localScale = new Vector3(0.2f, 2f, 1f);
            
            // Color it green (exit)
            var renderer = door.GetComponent<Renderer>();
            if (renderer != null) {
                renderer.material.color = new Color(0.2f, 0.8f, 0.3f);
            }
            
            // Add Interactable component
            Interactable interactable = door.AddComponent<Interactable>();
            interactable.interactionType = InteractionSystem.InteractionType.OpenDoor;
            interactable.targetId = "apartment_player";
            interactable.promptText = "Press E to go home";
            interactable.interactionRange = 2f;
            interactable.requiresLineOfSight = true;
            
            Debug.Log("✅ Exit door created with Interactable");
        }
        
        private static void CreateDesk() {
            Debug.Log("Creating desk...");
            
            // Desk surface
            GameObject desk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            desk.name = "Desk";
            desk.transform.position = new Vector3(3, 0.75f, 3);
            desk.transform.localScale = new Vector3(1.5f, 0.1f, 0.8f);
            
            // Desk legs
            for (int i = 0; i < 4; i++) {
                GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leg.name = $"DeskLeg_{i + 1}";
                leg.transform.SetParent(desk.transform);
                float xOffset = (i % 2 == 0) ? -0.6f : 0.6f;
                float zOffset = (i < 2) ? -0.3f : 0.3f;
                leg.transform.localPosition = new Vector3(xOffset, -0.7f, zOffset);
                leg.transform.localScale = new Vector3(0.1f, 1.4f, 0.1f);
            }
            
            // Computer (placeholder cube)
            GameObject computer = GameObject.CreatePrimitive(PrimitiveType.Cube);
            computer.name = "Computer";
            computer.transform.SetParent(desk.transform);
            computer.transform.localPosition = new Vector3(0, 0.3f, 0);
            computer.transform.localScale = new Vector3(0.4f, 0.4f, 0.05f);
            
            // Color computer black
            var renderer = computer.GetComponent<Renderer>();
            if (renderer != null) {
                renderer.material.color = Color.black;
            }
            
            // Add Interactable to computer
            Interactable interactable = computer.AddComponent<Interactable>();
            interactable.interactionType = InteractionSystem.InteractionType.UseComputer;
            interactable.targetId = "office_computer";
            interactable.promptText = "Press E to use computer";
            interactable.interactionRange = 1.5f;
            interactable.requiresLineOfSight = true;
            
            Debug.Log("✅ Desk created with computer Interactable");
        }
        
        private static void CreateBossNPC() {
            Debug.Log("Creating boss NPC...");
            
            GameObject boss = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            boss.name = "Boss";
            boss.transform.position = new Vector3(0, 1, 0);
            boss.transform.localScale = new Vector3(0.5f, 1f, 0.5f);
            
            // Color it red for visibility
            var renderer = boss.GetComponent<Renderer>();
            if (renderer != null) {
                renderer.material.color = new Color(0.8f, 0.2f, 0.2f);
            }
            
            // Add NavMeshAgent for patrol (requires NavMesh to be baked)
            NavMeshAgent agent = boss.AddComponent<NavMeshAgent>();
            agent.speed = 1.5f;
            agent.angularSpeed = 120f;
            agent.acceleration = 8f;
            agent.stoppingDistance = 0.5f;
            
            // Tag for DetectionSystem
            boss.tag = "NPC";
            
            Debug.Log("✅ Boss NPC created (remember to bake NavMesh for patrol)");
        }
        
        private static void CreatePlayerSpawn() {
            Debug.Log("Creating player spawn point...");
            
            GameObject spawn = new GameObject("PlayerSpawn");
            spawn.transform.position = new Vector3(-3, 0, 0);
            spawn.tag = "PlayerSpawn";
            
            // Add visual marker (small sphere)
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "SpawnMarker";
            marker.transform.SetParent(spawn.transform);
            marker.transform.localPosition = Vector3.zero;
            marker.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            
            var renderer = marker.GetComponent<Renderer>();
            if (renderer != null) {
                renderer.material.color = new Color(0, 1, 0, 0.3f); // Transparent green
            }
            
            Debug.Log("✅ Player spawn point created");
        }
        
        [MenuItem("Hustle Economy/Add Office Scene to Build Settings")]
        public static void AddToBuildSettings() {
            string scenePath = "Assets/Scenes/Office.unity";
            
            if (!System.IO.File.Exists(scenePath)) {
                Debug.LogError("Office.unity not found! Build the scene first.");
                return;
            }
            
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            
            bool alreadyAdded = false;
            foreach (var scene in scenes) {
                if (scene.path == scenePath) {
                    alreadyAdded = true;
                    break;
                }
            }
            
            if (!alreadyAdded) {
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
                Debug.Log($"✅ Added {scenePath} to Build Settings");
            } else {
                Debug.Log($"Scene {scenePath} already in Build Settings");
            }
        }
    }
}
#endif
