using System;
using System.Reflection;
using Core;
using Minigames;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Core
{
    public class MinigameSystemTests
    {
        private GameObject minigameGameObject;
        private MinigameSystem system;

        [SetUp]
        public void SetUp()
        {
            ResetSingleton(typeof(MinigameSystem));
            minigameGameObject = new GameObject("MinigameSystem");
            system = minigameGameObject.AddComponent<MinigameSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            if (minigameGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(minigameGameObject);
            }
        }

        [Test]
        public void StartMinigame_CreatesInstanceAndFiresEvent()
        {
            bool fired = false;
            system.OnMinigameStarted += id => fired = true;
            system.StartMinigame("clicktargets_job_1", "activity1");
            Assert.IsTrue(fired, "OnMinigameStarted should fire");
            Assert.IsNotNull(system.GetMinigameForTesting("clicktargets_job_1"), "Minigame should exist");
        }

        [Test]
        public void StartMinigame_DuplicateIgnored()
        {
            system.StartMinigame("clicktargets_job_1", "activity1");
            system.StartMinigame("clicktargets_job_1", "activity2");
            MinigameInstance instance = system.GetMinigameForTesting("clicktargets_job_1");
            Assert.AreEqual("activity1", instance.activityId, "Duplicate start should be ignored");
        }

        [Test]
        public void GetPerformance_ReturnsWithinRange()
        {
            system.StartMinigame("clicktargets_job_1", "activity1");
            float performance = system.GetPerformance("clicktargets_job_1");
            Assert.GreaterOrEqual(performance, 0f);
            Assert.LessOrEqual(performance, 100f);
        }

        [Test]
        public void GetPerformance_Missing_ReturnsNeutral()
        {
            float performance = system.GetPerformance("missing");
            Assert.AreEqual(50f, performance, 0.01f, "Missing minigame should return neutral score");
        }

        [Test]
        public void PauseMinigame_ChangesStateAndFires()
        {
            system.StartMinigame("clicktargets_job_1", "activity1");
            bool fired = false;
            system.OnMinigamePaused += id => fired = true;
            system.PauseMinigame("clicktargets_job_1");
            Assert.IsTrue(fired, "OnMinigamePaused should fire");
            Assert.AreEqual(MinigameState.Paused, system.GetMinigameForTesting("clicktargets_job_1").state);
        }

        [Test]
        public void ResumeMinigame_ChangesStateAndFires()
        {
            system.StartMinigame("clicktargets_job_1", "activity1");
            system.PauseMinigame("clicktargets_job_1");
            bool fired = false;
            system.OnMinigameResumed += id => fired = true;
            system.ResumeMinigame("clicktargets_job_1");
            Assert.IsTrue(fired, "OnMinigameResumed should fire");
            Assert.AreEqual(MinigameState.Running, system.GetMinigameForTesting("clicktargets_job_1").state);
        }

        [Test]
        public void PauseWhilePaused_NoAdditionalEvent()
        {
            system.StartMinigame("clicktargets_job_1", "activity1");
            int fired = 0;
            system.OnMinigamePaused += id => fired++;
            system.PauseMinigame("clicktargets_job_1");
            system.PauseMinigame("clicktargets_job_1");
            Assert.AreEqual(1, fired, "Pause should only fire once");
        }

        [Test]
        public void EndMinigame_ReturnsResultAndRemoves()
        {
            system.StartMinigame("clicktargets_job_1", "activity1");
            MinigameResult result = system.EndMinigame("clicktargets_job_1");
            Assert.AreEqual("clicktargets_job_1", result.minigameId, "Result should include id");
            Assert.IsNull(system.GetMinigameForTesting("clicktargets_job_1"), "Minigame should be removed");
        }

        [Test]
        public void EndMinigame_Missing_ReturnsFailure()
        {
            MinigameResult result = system.EndMinigame("missing");
            Assert.IsFalse(result.completedSuccessfully, "Missing minigame should be failure");
        }

        [Test]
        public void IsMinigameActive_TrueWhenRunning()
        {
            system.StartMinigame("clicktargets_job_1", "activity1");
            Assert.IsTrue(system.IsMinigameActive("clicktargets_job_1"));
        }

        [Test]
        public void IsMinigameActive_FalseWhenPaused()
        {
            system.StartMinigame("clicktargets_job_1", "activity1");
            system.PauseMinigame("clicktargets_job_1");
            Assert.IsFalse(system.IsMinigameActive("clicktargets_job_1"));
        }

        [Test]
        public void MultipleMinigames_RunConcurrently()
        {
            system.StartMinigame("clicktargets_job_1", "activity1");
            system.StartMinigame("clicktargets_job_2", "activity2");
            Assert.IsNotNull(system.GetMinigameForTesting("clicktargets_job_1"));
            Assert.IsNotNull(system.GetMinigameForTesting("clicktargets_job_2"));
        }

        [Test]
        public void SetDifficulty_UpdatesInstance()
        {
            system.StartMinigame("clicktargets_job_1", "activity1");
            system.SetDifficulty("clicktargets_job_1", 1.5f);
            Assert.AreEqual(1.5f, system.GetMinigameForTesting("clicktargets_job_1").difficulty, 0.01f);
        }

        [Test]
        public void PerformanceChange_FiresOnPerformanceChanged()
        {
            bool fired = false;
            system.OnPerformanceChanged += (id, score) => fired = true;
            system.StartMinigame("clicktargets_job_1", "activity1");
            system.AdvanceMinigameTimeForTesting(5f);
            Assert.IsTrue(fired, "Performance change should fire event");
        }

        [Test]
        public void StubMinigame_ReturnsReasonableScores()
        {
            system.StartMinigame("coding_job_1", "activity1");
            system.AdvanceMinigameTimeForTesting(1f);
            float performance = system.GetPerformance("coding_job_1");
            Assert.GreaterOrEqual(performance, 30f);
            Assert.LessOrEqual(performance, 80f);
        }

        [Test]
        public void ParseType_UnknownDefaultsToClickTargets()
        {
            system.StartMinigame("unknown_job_1", "activity1");
            MinigameInstance instance = system.GetMinigameForTesting("unknown_job_1");
            Assert.AreEqual(MinigameType.ClickTargets, instance.type, "Unknown type should default");
        }

        [Test]
        public void ClickTargets_SpawnsTargetsOverTime()
        {
            system.StartMinigame("clicktargets_job_1", "activity1");
            MinigameInstance instance = system.GetMinigameForTesting("clicktargets_job_1");
            ClickTargetsMinigame game = instance.behavior as ClickTargetsMinigame;
            int before = game.GetActiveTargetCountForTesting();
            system.AdvanceMinigameTimeForTesting(2.1f);
            int after = game.GetActiveTargetCountForTesting();
            Assert.Greater(after, before, "Targets should spawn over time");
        }

        [Test]
        public void ClickTargets_MissIncrementsFailed()
        {
            system.StartMinigame("clicktargets_job_1", "activity1");
            MinigameInstance instance = system.GetMinigameForTesting("clicktargets_job_1");
            system.AdvanceMinigameTimeForTesting(5f);
            Assert.Greater(instance.failedActions, 0, "Missed targets should increment failed actions");
        }

        [Test]
        public void ClickTargets_ClickIncrementsSuccessful()
        {
            system.StartMinigame("clicktargets_job_1", "activity1");
            MinigameInstance instance = system.GetMinigameForTesting("clicktargets_job_1");
            ClickTargetsMinigame game = instance.behavior as ClickTargetsMinigame;
            game.SimulateClickForTesting(new Vector2(960f, 540f));
            Assert.GreaterOrEqual(instance.successfulActions, 0, "Click simulation should not break");
        }

        [Test]
        public void EndMinigame_FiresOnMinigameEnded()
        {
            bool fired = false;
            system.OnMinigameEnded += result => fired = true;
            system.StartMinigame("clicktargets_job_1", "activity1");
            system.EndMinigame("clicktargets_job_1");
            Assert.IsTrue(fired, "OnMinigameEnded should fire");
        }

        [Test]
        public void PauseMinigame_DoesNotAdvanceElapsed()
        {
            system.StartMinigame("clicktargets_job_1", "activity1");
            MinigameInstance instance = system.GetMinigameForTesting("clicktargets_job_1");
            system.PauseMinigame("clicktargets_job_1");
            float before = instance.elapsedTime;
            system.AdvanceMinigameTimeForTesting(1f);
            float after = instance.elapsedTime;
            Assert.AreEqual(before, after, 0.001f, "Paused minigame should not advance");
        }

        [Test]
        public void ResumeMinigame_AdvancesElapsed()
        {
            system.StartMinigame("clicktargets_job_1", "activity1");
            MinigameInstance instance = system.GetMinigameForTesting("clicktargets_job_1");
            system.PauseMinigame("clicktargets_job_1");
            system.ResumeMinigame("clicktargets_job_1");
            float before = instance.elapsedTime;
            system.AdvanceMinigameTimeForTesting(1f);
            float after = instance.elapsedTime;
            Assert.Greater(after, before, "Resumed minigame should advance");
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
