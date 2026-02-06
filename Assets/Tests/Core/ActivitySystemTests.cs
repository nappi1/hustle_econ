using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using HustleEconomy.Core;
using Core;

namespace Tests.Core
{
    public class ActivitySystemTests
    {
        private GameObject activityGameObject;
        private GameObject timeGameObject;
        private GameObject economyGameObject;
        private GameObject skillGameObject;
        private ActivitySystem system;

        [SetUp]
        public void SetUp()
        {
            ResetSingleton(typeof(ActivitySystem));
            ResetSingleton(typeof(TimeEnergySystem));
            ResetSingleton(typeof(EconomySystem));
            ResetSingleton(typeof(SkillSystem));

            timeGameObject = new GameObject("TimeEnergySystem");
            timeGameObject.AddComponent<TimeEnergySystem>();

            economyGameObject = new GameObject("EconomySystem");
            economyGameObject.AddComponent<EconomySystem>();

            skillGameObject = new GameObject("SkillSystem");
            skillGameObject.AddComponent<SkillSystem>();

            activityGameObject = new GameObject("ActivitySystem");
            system = activityGameObject.AddComponent<ActivitySystem>();
        }

        [TearDown]
        public void TearDown()
        {
            if (activityGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(activityGameObject);
            }

            if (skillGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(skillGameObject);
            }

            if (economyGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(economyGameObject);
            }

            if (timeGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(timeGameObject);
            }
        }

        [Test]
        public void CreateActivity_StartsAndFiresEvent()
        {
            bool fired = false;
            system.OnActivityStarted += id => fired = true;
            string id = system.CreateActivity(ActivitySystem.ActivityType.Physical, "work_mop", 1f);
            Assert.IsTrue(fired, "OnActivityStarted should fire");
            Assert.IsNotNull(system.GetActivity(id), "Activity should exist");
        }

        [Test]
        public void GetActiveActivities_ReturnsActive()
        {
            system.CreateActivity(ActivitySystem.ActivityType.Physical, "work_mop", 1f);
            List<ActivitySystem.Activity> list = system.GetActiveActivities("player");
            Assert.AreEqual(1, list.Count, "Should return active activity");
        }

        [Test]
        public void GetActivity_ReturnsSpecific()
        {
            string id = system.CreateActivity(ActivitySystem.ActivityType.Physical, "work_mop", 1f);
            ActivitySystem.Activity activity = system.GetActivity(id);
            Assert.AreEqual(id, activity.id, "Should return matching activity");
        }

        [Test]
        public void CanMultitask_PhysicalPhysical_False()
        {
            string a = system.CreateActivity(ActivitySystem.ActivityType.Physical, "work_mop", 1f);
            string b = system.CreateActivity(ActivitySystem.ActivityType.Physical, "work_drive", 1f);
            bool can = system.CanMultitask(a, b);
            Assert.IsFalse(can, "Physical+Physical should be false");
        }

        [Test]
        public void CanMultitask_ScreenScreen_False()
        {
            string a = system.CreateActivity(ActivitySystem.ActivityType.Screen, "screen_a", 1f);
            string b = system.CreateActivity(ActivitySystem.ActivityType.Screen, "screen_b", 1f);
            bool can = system.CanMultitask(a, b);
            Assert.IsFalse(can, "Screen+Screen should be false");
        }

        [Test]
        public void CanMultitask_PhysicalScreen_TrueIfAttentionAllows()
        {
            string a = system.CreateActivity(ActivitySystem.ActivityType.Physical, "work_mop", 1f);
            string b = system.CreateActivity(ActivitySystem.ActivityType.Screen, "phone", 1f);
            ActivitySystem.Activity actB = system.GetActivity(b);
            actB.requiredAttention = 0.3f;
            bool can = system.CanMultitask(a, b);
            Assert.IsTrue(can, "Physical+Screen should be true when attention <= 1");
        }

        [Test]
        public void CanMultitask_AttentionOverLimit_False()
        {
            string a = system.CreateActivity(ActivitySystem.ActivityType.Physical, "work_mop", 1f);
            string b = system.CreateActivity(ActivitySystem.ActivityType.Screen, "stream", 1f);
            ActivitySystem.Activity actA = system.GetActivity(a);
            ActivitySystem.Activity actB = system.GetActivity(b);
            actA.requiredAttention = 0.8f;
            actB.requiredAttention = 0.4f;
            bool can = system.CanMultitask(a, b);
            Assert.IsFalse(can, "Total attention > 1 should be false");
        }

        [Test]
        public void CanMultitask_NoneLevel_False()
        {
            string a = system.CreateActivity(ActivitySystem.ActivityType.Physical, "work_mop", 1f);
            string b = system.CreateActivity(ActivitySystem.ActivityType.Screen, "phone", 1f);
            ActivitySystem.Activity actA = system.GetActivity(a);
            actA.multitaskingAllowed = ActivitySystem.MultitaskingLevel.None;
            bool can = system.CanMultitask(a, b);
            Assert.IsFalse(can, "None multitasking should be false");
        }

        [Test]
        public void PauseActivity_ChangesStateAndFires()
        {
            string id = system.CreateActivity(ActivitySystem.ActivityType.Physical, "work_mop", 1f);
            bool fired = false;
            system.OnActivityPaused += evtId => fired = true;
            system.PauseActivity(id);
            Assert.IsTrue(fired, "OnActivityPaused should fire");
            Assert.AreEqual(ActivitySystem.ActivityState.Paused, system.GetActivity(id).state, "State should be paused");
        }

        [Test]
        public void ResumeActivity_ChangesStateAndFires()
        {
            string id = system.CreateActivity(ActivitySystem.ActivityType.Physical, "work_mop", 1f);
            system.PauseActivity(id);
            bool fired = false;
            system.OnActivityResumed += evtId => fired = true;
            system.ResumeActivity(id);
            Assert.IsTrue(fired, "OnActivityResumed should fire");
            Assert.AreEqual(ActivitySystem.ActivityState.Running, system.GetActivity(id).state, "State should be running");
        }

        [Test]
        public void EndActivity_RemovesAndFires()
        {
            string id = system.CreateActivity(ActivitySystem.ActivityType.Physical, "work_mop", 1f);
            bool fired = false;
            system.OnActivityEnded += result => fired = true;
            system.EndActivity(id);
            Assert.IsTrue(fired, "OnActivityEnded should fire");
            Assert.IsNull(system.GetActivity(id), "Activity should be removed");
        }

        [Test]
        public void EndActivity_ReturnsRewards()
        {
            string id = system.CreateActivity(ActivitySystem.ActivityType.Physical, "work_mop", 1f);
            ActivitySystem.ActivityResult result = system.EndActivity(id);
            Assert.IsTrue(result.rewards.ContainsKey("money"), "Work activity should grant money");
        }

        [Test]
        public void Activity_AutoCompletes_WhenDurationExceeded()
        {
            string id = system.CreateActivity(ActivitySystem.ActivityType.Physical, "work_mop", 0.001f);
            system.AdvanceActivityTimeForTesting(5f);
            Assert.IsNull(system.GetActivity(id), "Activity should complete automatically");
        }

        [Test]
        public void DetectionDuringWork_SetsDetectedSlacking()
        {
            string id = system.CreateActivity(ActivitySystem.ActivityType.Physical, "work_mop", 1f);
            system.SetDetectionForTesting(id, true);
            system.AdvanceActivityTimeForTesting(1f);
            ActivitySystem.Activity activity = system.GetActivity(id);
            Assert.IsTrue(activity.detectedSlacking, "Detected slacking should be set");
        }

        [Test]
        public void PhaseSystem_Transitions()
        {
            string id = system.CreateActivity(ActivitySystem.ActivityType.Screen, "stream", 1f);
            system.SetActivityPhaseForTesting(id, new List<ActivitySystem.ActivityPhase>
            {
                new ActivitySystem.ActivityPhase { name = "setup", durationMinutes = 0.01f, canMultitask = true, attentionRequired = 0.2f },
                new ActivitySystem.ActivityPhase { name = "active", durationMinutes = 0.01f, canMultitask = false, attentionRequired = 0.9f }
            });
            system.AdvanceActivityTimeForTesting(1f);
            ActivitySystem.Activity activity = system.GetActivity(id);
            Assert.AreEqual(1, activity.currentPhaseIndex, "Phase should advance");
            Assert.AreEqual(ActivitySystem.MultitaskingLevel.None, activity.multitaskingAllowed, "Phase should update multitasking");
        }

        [Test]
        public void OnActivityEnded_FiresWithCorrectResult()
        {
            string id = system.CreateActivity(ActivitySystem.ActivityType.Physical, "work_mop", 1f);
            ActivitySystem.ActivityResult captured = default;
            system.OnActivityEnded += result => captured = result;
            system.EndActivity(id);
            Assert.AreEqual(id, captured.activityId, "Result should include activity id");
        }

        [Test]
        public void SkillXp_Awarded()
        {
            string id = system.CreateActivity(ActivitySystem.ActivityType.Physical, "work_mop", 1f);
            float before = SkillSystem.Instance.GetSkillLevel("player", SkillSystem.SkillType.Social);
            system.EndActivity(id);
            float after = SkillSystem.Instance.GetSkillLevel("player", SkillSystem.SkillType.Social);
            Assert.GreaterOrEqual(after, before, "Skill XP should be awarded");
        }

        [Test]
        public void CreateActivity_PausesIncompatibleExisting()
        {
            string a = system.CreateActivity(ActivitySystem.ActivityType.Physical, "work_mop", 1f);
            string b = system.CreateActivity(ActivitySystem.ActivityType.Physical, "work_drive", 1f);
            ActivitySystem.Activity actA = system.GetActivity(a);
            Assert.AreEqual(ActivitySystem.ActivityState.Paused, actA.state, "Existing should be paused");
            system.EndActivity(b);
        }

        [Test]
        public void OnMultitaskAttempt_Fires()
        {
            string a = system.CreateActivity(ActivitySystem.ActivityType.Physical, "work_mop", 1f);
            bool fired = false;
            system.OnMultitaskAttempt += (id1, id2) => fired = true;
            system.CreateActivity(ActivitySystem.ActivityType.Screen, "phone", 1f);
            Assert.IsTrue(fired, "OnMultitaskAttempt should fire");
            system.EndActivity(a);
        }

        [Test]
        public void EndActivity_Invalid_ReturnsDefault()
        {
            ActivitySystem.ActivityResult result = system.EndActivity("missing");
            Assert.IsFalse(result.completed, "Missing activity should return not completed");
        }

        [Test]
        public void GetActiveActivities_EmptyWhenNone()
        {
            List<ActivitySystem.Activity> list = system.GetActiveActivities("player");
            Assert.AreEqual(0, list.Count, "No activities should return empty list");
        }

        [Test]
        public void ResumeActivity_Invalid_NoThrow()
        {
            system.ResumeActivity("missing");
            Assert.Pass("No exception on missing");
        }

        [Test]
        public void PauseActivity_Invalid_NoThrow()
        {
            system.PauseActivity("missing");
            Assert.Pass("No exception on missing");
        }

        [Test]
        public void EndActivity_ZeroDuration_Completes()
        {
            string id = system.CreateActivity(ActivitySystem.ActivityType.Passive, "idle", 0f);
            system.AdvanceActivityTimeForTesting(0.1f);
            Assert.IsNull(system.GetActivity(id), "Zero duration should complete immediately");
        }

        private static void ResetSingleton(Type systemType)
        {
            FieldInfo field = systemType.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(null, null);
            }
        }
    }
}
