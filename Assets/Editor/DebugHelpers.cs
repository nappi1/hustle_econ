#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor;
using Core;

namespace HustleEconomy.Editor {
    /// <summary>
    /// Debug utilities for development and testing.
    /// Provides shortcuts to test game flows without manual setup.
    /// </summary>
    public class DebugHelpers : MonoBehaviour {
        
        [MenuItem("Hustle Economy/Debug/Quick Start Game")]
        public static void QuickStartGame() {
            if (!Application.isPlaying) {
                Debug.LogWarning("⚠️ Enter Play mode first!");
                return;
            }
            
            Debug.Log("=== Quick Start ===");
            
            // Setup test data
            TestDataSetup.SetupTestData();
            
            // Give player some starting resources
            SetBalance("player", 500f);
            SetEnergy(100f);
            SetTimeToHour(8); // 8 AM
            
            Debug.Log("✅ Game ready! Player has $500, 100 energy, time is 8:00 AM");
        }
        
        [MenuItem("Hustle Economy/Debug/Simulate Full Workday")]
        public static void SimulateWorkday() {
            if (!Application.isPlaying) {
                Debug.LogWarning("⚠️ Enter Play mode first!");
                return;
            }
            
            Debug.Log("=== Simulating Workday ===");
            
            // Start job
            JobSystem.Instance.StartShift("player", "job1");
            
            // Create work activity
            string activityId = ActivitySystem.Instance.CreateActivity(
                ActivitySystem.ActivityType.Physical,
                "work_mop",
                8f // 8 hour shift
            );

            // Simulate good performance
            ActivitySystem.Instance.SetMinigamePerformanceForTesting("work_mop", 80f);
            
            // Advance time 8 hours
            ActivitySystem.Instance.AdvanceActivityTimeForTesting(8f * 3600f);
            
            // End shift
            ActivitySystem.Instance.EndActivity(activityId);
            
            float balance = EconomySystem.Instance.GetBalance("player");
            Debug.Log($"✅ Workday complete! Player earned money. New balance: ${balance}");
        }
        
        [MenuItem("Hustle Economy/Debug/Test Detection System")]
        public static void TestDetection() {
            if (!Application.isPlaying) {
                Debug.LogWarning("⚠️ Enter Play mode first!");
                return;
            }
            
            Debug.Log("=== Testing Detection ===");
            
            // Trigger detection
            bool detected = DetectionSystem.Instance.CheckDetection("player", "work_mop").detected;
            
            if (detected) {
                Debug.Log("⚠️ Player detected! Boss caught you slacking.");
            } else {
                Debug.Log("✅ Player not detected. Coast is clear.");
            }
        }
        
        [MenuItem("Hustle Economy/Debug/Add Money ($100)")]
        public static void AddMoney() {
            if (!Application.isPlaying) {
                Debug.LogWarning("⚠️ Enter Play mode first!");
                return;
            }
            
            float current = EconomySystem.Instance.GetBalance("player");
            EconomySystem.Instance.AddIncome("player", 100f, EconomySystem.IncomeSource.Other, "Debug cheat");
            float newBalance = EconomySystem.Instance.GetBalance("player");
            
            Debug.Log($"💰 Added $100. Balance: ${current} → ${newBalance}");
        }
        
        [MenuItem("Hustle Economy/Debug/Restore Energy")]
        public static void RestoreEnergy() {
            if (!Application.isPlaying) {
                Debug.LogWarning("⚠️ Enter Play mode first!");
                return;
            }
            
            SetEnergy(100f);
            Debug.Log("⚡ Energy restored to 100");
        }
        
        [MenuItem("Hustle Economy/Debug/Advance Time (1 hour)")]
        public static void AdvanceTime() {
            if (!Application.isPlaying) {
                Debug.LogWarning("⚠️ Enter Play mode first!");
                return;
            }
            
            float currentTime = TimeEnergySystem.Instance.GetCurrentGameTime();
            TimeEnergySystem.Instance.AdvanceTime(60f); // 60 minutes
            float newTime = TimeEnergySystem.Instance.GetCurrentGameTime();
            
            Debug.Log($"⏰ Time advanced: {currentTime:F1}:00 → {newTime:F1}:00");
        }
        
        [MenuItem("Hustle Economy/Debug/Print System Status")]
        public static void PrintSystemStatus() {
            if (!Application.isPlaying) {
                Debug.LogWarning("⚠️ Enter Play mode first!");
                return;
            }
            
            Debug.Log("=== SYSTEM STATUS ===");
            Debug.Log($"Time: {TimeEnergySystem.Instance.GetCurrentGameTime():F2}:00");
            Debug.Log($"Energy: {TimeEnergySystem.Instance.GetEnergyLevel():F1}/100");
            Debug.Log($"Money: ${EconomySystem.Instance.GetBalance("player"):F2}");
            Debug.Log($"Location: {LocationSystem.Instance.GetPlayerLocation("player")}");
            
            var activities = ActivitySystem.Instance.GetActiveActivities("player");
            Debug.Log($"Active Activities: {activities.Count}");
            
            foreach (var activity in activities) {
                Debug.Log($"  - {activity.type} (Performance: {activity.performanceScore:F2})");
            }
            
            Debug.Log("==================");
        }
        
        [MenuItem("Hustle Economy/Debug/Load Office Scene")]
        public static void LoadOffice() {
            if (Application.isPlaying) {
                GameManager.Instance.LoadScene("Office");
            } else {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Office.unity");
            }
        }
        
        [MenuItem("Hustle Economy/Debug/Load Apartment Scene")]
        public static void LoadApartment() {
            if (Application.isPlaying) {
                GameManager.Instance.LoadScene("Apartment");
            } else {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Apartment.unity");
            }
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
        
        // Validate menu items (only show in Play mode)
        [MenuItem("Hustle Economy/Debug/Quick Start Game", true)]
        [MenuItem("Hustle Economy/Debug/Simulate Full Workday", true)]
        [MenuItem("Hustle Economy/Debug/Test Detection System", true)]
        [MenuItem("Hustle Economy/Debug/Add Money ($100)", true)]
        [MenuItem("Hustle Economy/Debug/Restore Energy", true)]
        [MenuItem("Hustle Economy/Debug/Advance Time (1 hour)", true)]
        [MenuItem("Hustle Economy/Debug/Print System Status", true)]
        private static bool ValidatePlayModeCommands() {
            return Application.isPlaying;
        }
    }
}
#endif
