#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor;
using Core;
using System.Collections.Generic;

namespace HustleEconomy.Editor {
    /// <summary>
    /// Editor script to populate test data (jobs, NPCs, locations) for prototype testing.
    /// Usage: Unity menu bar > Hustle Economy > Setup Test Data
    /// Note: Must be run while in Play mode so systems are initialized
    /// </summary>
    public class TestDataSetup : MonoBehaviour {
        
        [MenuItem("Hustle Economy/Setup Test Data")]
        public static void SetupTestData() {
            if (!Application.isPlaying) {
                Debug.LogWarning("⚠️ Test data setup requires Play mode. Enter Play mode first!");
                return;
            }
            
            Debug.Log("=== Setting Up Test Data ===");
            
            // Setup locations
            SetupLocations();
            
            // Setup jobs
            SetupJobs();
            
            // Setup NPCs
            SetupNPCs();
            
            // Setup initial player data
            SetupPlayerData();
            
            Debug.Log("=== Test Data Setup Complete! ===");
        }
        
        private static void SetupLocations() {
            Debug.Log("Creating locations...");
            
            // Create Office location
            LocationSystem.Instance.CreateLocation(CreateLocation(
                "office_main",
                "Office Building",
                LocationSystem.LocationType.Office,
                "Office"
            ));
            
            // Create Apartment location
            LocationSystem.Instance.CreateLocation(CreateLocation(
                "apartment_player",
                "Your Apartment",
                LocationSystem.LocationType.Apartment,
                "Apartment"
            ));
            
            // Set player starting location
            LocationSystem.Instance.SetPlayerLocationForTesting("player", "apartment_player");
            
            Debug.Log("✅ Created 2 locations (office, apartment)");
        }
        
        private static void SetupJobs() {
            Debug.Log("Creating jobs...");
            
            // Create Janitorial Job
            JobSystem.Job job = new JobSystem.Job
            {
                id = "job1",
                title = "Janitor",
                hourlyWage = 15f,
                type = JobSystem.JobType.Janitorial,
                shifts = new List<JobSystem.ShiftSchedule>
                {
                    new JobSystem.ShiftSchedule
                    {
                        day = DayOfWeek.Monday,
                        startTime = new System.TimeSpan(9, 0, 0),
                        durationHours = 8f
                    }
                },
                hoursPerWeek = 40f,
                promotionThreshold = 80f,
                detectionSensitivity = 1f,
                isActive = false
            };

            JobSystem.Instance.CreateJob(job);
            JobSystem.Instance.ApplyForJob("player", job.id);
            Debug.Log($"✅ Created job: {job.id} ({job.title}) ${job.hourlyWage}/hr");
        }
        
        private static void SetupNPCs() {
            Debug.Log("Creating NPCs...");
            
            // Create Boss NPC
            RelationshipSystem.NPC boss = RelationshipSystem.Instance.CreateNPC(
                RelationshipSystem.NPCType.Friend,
                new RelationshipSystem.NPCData
                {
                    name = "Mr. Henderson",
                    personality = RelationshipSystem.NPCPersonality.Supportive,
                    values = new Dictionary<RelationshipSystem.NPCValue, float>(),
                    tolerances = new Dictionary<RelationshipSystem.NPCTolerance, RelationshipSystem.ToleranceLevel>(),
                    sexualBoundary = RelationshipSystem.SexualBoundaryType.Monogamous
                }
            );

            if (boss != null)
            {
                DetectionSystem.Instance.RegisterObserver(boss.id, new DetectionSystem.ObserverData
                {
                    role = DetectionSystem.ObserverRole.Boss,
                    position = new Vector3(0, 0, 0),
                    facing = Vector3.forward,
                    visionRange = 6f,
                    visionCone = 120f,
                    audioSensitivity = 1f,
                    caresAboutLegality = false,
                    caresAboutJobPerformance = true,
                    currentLocation = "office_main"
                });

                DetectionSystem.Instance.UpdateObserverPosition(boss.id, new Vector3(0, 0, 0), Vector3.forward);
                Debug.Log($"✅ Created NPC: {boss.id} (Boss observer in office)");
            }
            
            // Boss already logged when created; avoid duplicate log.
            
            // Create a few additional NPCs for testing
            CreateTestNPC("Alice", DetectionSystem.ObserverRole.Coworker);
            CreateTestNPC("Bob", DetectionSystem.ObserverRole.Coworker);
        }
        
        private static void CreateTestNPC(string npcName, DetectionSystem.ObserverRole role) {
            RelationshipSystem.NPC npc = RelationshipSystem.Instance.CreateNPC(
                RelationshipSystem.NPCType.Friend,
                new RelationshipSystem.NPCData
                {
                    name = npcName,
                    personality = RelationshipSystem.NPCPersonality.Supportive,
                    values = new Dictionary<RelationshipSystem.NPCValue, float>(),
                    tolerances = new Dictionary<RelationshipSystem.NPCTolerance, RelationshipSystem.ToleranceLevel>(),
                    sexualBoundary = RelationshipSystem.SexualBoundaryType.Monogamous
                }
            );

            if (npc == null)
            {
                return;
            }

            DetectionSystem.Instance.RegisterObserver(npc.id, new DetectionSystem.ObserverData
            {
                role = role,
                position = new Vector3(0, 0, 0),
                facing = Vector3.forward,
                visionRange = 4f,
                visionCone = 100f,
                audioSensitivity = 0.6f,
                caresAboutLegality = false,
                caresAboutJobPerformance = role == DetectionSystem.ObserverRole.Coworker,
                currentLocation = "office_main"
            });

            Debug.Log($"✅ Created NPC: {npc.name} ({role})");
        }
        
        private static void SetupPlayerData() {
            Debug.Log("Initializing player data...");
            
            // Set starting money
            SetBalance("player", 100f);
            
            // Set starting energy
            SetEnergy(100f);
            
            // Set starting time (8:00 AM)
            SetTimeToHour(8);
            
            Debug.Log("✅ Player initialized:");
            Debug.Log($"   - Money: ${EconomySystem.Instance.GetBalance("player")}");
            Debug.Log($"   - Energy: {TimeEnergySystem.Instance.GetEnergyLevel()}");
            Debug.Log($"   - Time: {TimeEnergySystem.Instance.GetCurrentGameTime()}:00");
        }
        
        [MenuItem("Hustle Economy/Setup Test Data", true)]
        private static bool ValidateSetupTestData() {
            // Only enable menu item in Play mode
            return Application.isPlaying;
        }
        
        [MenuItem("Hustle Economy/Clear All Test Data")]
        public static void ClearTestData() {
            if (!Application.isPlaying) {
                Debug.LogWarning("⚠️ Clear data requires Play mode. Enter Play mode first!");
                return;
            }
            
            Debug.Log("=== Clearing Test Data ===");
            
            // Reset player money
            SetBalance("player", 0f);
            
            // Reset player energy
            SetEnergy(100f);
            
            // Reset time
            SetTimeToHour(0);
            
            Debug.Log("✅ Test data cleared");
        }
        
        [MenuItem("Hustle Economy/Clear All Test Data", true)]
        private static bool ValidateClearTestData() {
            return Application.isPlaying;
        }

        private static LocationSystem.LocationData CreateLocation(string id, string name, LocationSystem.LocationType type, string sceneName)
        {
            return new LocationSystem.LocationData
            {
                id = id,
                name = name,
                type = type,
                isPublic = true,
                requiresInvitation = false,
                allowedPlayers = new List<string>(),
                allowedActivities = new List<LocationSystem.ActivityType>(),
                detectionSensitivity = 0.1f,
                residingNPCs = new List<string>(),
                patrollingObservers = new List<string>(),
                spawnPosition = Vector3.zero,
                sceneName = sceneName,
                openTime = System.TimeSpan.Zero,
                closeTime = System.TimeSpan.Zero
            };
        }

        private static void SetBalance(string playerId, float target)
        {
            float current = EconomySystem.Instance.GetBalance(playerId);
            float delta = target - current;
            if (Mathf.Approximately(delta, 0f))
            {
                return;
            }

            if (delta > 0f)
            {
                EconomySystem.Instance.AddIncome(playerId, delta, EconomySystem.IncomeSource.Other, "Debug set balance");
            }
            else
            {
                EconomySystem.Instance.DeductExpense(playerId, -delta, EconomySystem.ExpenseType.Other, "Debug set balance");
            }
        }

        private static void SetEnergy(float target)
        {
            float current = TimeEnergySystem.Instance.GetEnergyLevel();
            float delta = target - current;
            if (Mathf.Approximately(delta, 0f))
            {
                return;
            }

            TimeEnergySystem.Instance.ModifyEnergy(delta, "debug");
        }

        private static void SetTimeToHour(int hour)
        {
            DateTime now = TimeEnergySystem.Instance.GetCurrentTime();
            DateTime target = new DateTime(now.Year, now.Month, now.Day, Mathf.Clamp(hour, 0, 23), 0, 0);
            if (target <= now)
            {
                target = target.AddDays(1);
            }

            double minutes = (target - now).TotalMinutes;
            if (minutes > 0)
            {
                TimeEnergySystem.Instance.AdvanceTime((float)minutes);
            }
        }
    }
}
#endif
