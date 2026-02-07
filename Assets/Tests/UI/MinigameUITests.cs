using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Core;
using UI;
using Minigames;

namespace Tests.UI
{
    public class MinigameUITests
    {
        private GameObject uiGameObject;
        private GameObject minigameGameObject;
        private GameObject activityGameObject;
        private GameObject inputGameObject;
        private MinigameUI ui;

        [SetUp]
        public void SetUp()
        {
            ResetSingleton(typeof(MinigameUI));
            ResetSingleton(typeof(MinigameSystem));
            ResetSingleton(typeof(ActivitySystem));
            ResetSingleton(typeof(InputManager));

            minigameGameObject = new GameObject("MinigameSystem");
            minigameGameObject.AddComponent<MinigameSystem>();

            activityGameObject = new GameObject("ActivitySystem");
            activityGameObject.AddComponent<ActivitySystem>();

            inputGameObject = new GameObject("InputManager");
            inputGameObject.AddComponent<InputManager>();

            uiGameObject = new GameObject("MinigameUI");
            ui = uiGameObject.AddComponent<MinigameUI>();
        }

        [TearDown]
        public void TearDown()
        {
            if (uiGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(uiGameObject);
            }

            if (minigameGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(minigameGameObject);
            }

            if (activityGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(activityGameObject);
            }

            if (inputGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(inputGameObject);
            }
        }

        [Test]
        public void CreateMinigameUI_CreatesInstance()
        {
            string instanceId = ui.CreateMinigameUI("ClickTargets_test", MinigameUI.MinigameUIContext.Fullscreen, CreateConfig());
            Assert.IsNotNull(instanceId, "Instance id should be returned");
            Assert.IsTrue(ui.IsMinigameActive(instanceId), "Instance should be tracked");
        }

        [Test]
        public void CreateMinigameUI_StartsMinigameSystem()
        {
            string instanceId = ui.CreateMinigameUI("ClickTargets_test", MinigameUI.MinigameUIContext.Fullscreen, CreateConfig());
            Assert.IsTrue(MinigameSystem.Instance.IsMinigameActive(instanceId), "MinigameSystem should have active minigame");
        }

        [Test]
        public void DestroyMinigameUI_RemovesInstanceAndEnds()
        {
            string instanceId = ui.CreateMinigameUI("ClickTargets_test", MinigameUI.MinigameUIContext.Fullscreen, CreateConfig());
            ui.DestroyMinigameUI(instanceId);
            Assert.IsFalse(ui.IsMinigameActive(instanceId), "Instance should be removed");
            Assert.IsFalse(MinigameSystem.Instance.IsMinigameActive(instanceId), "MinigameSystem should end minigame");
        }

        [Test]
        public void HandleClick_HitTarget_DeactivatesAndFires()
        {
            string instanceId = ui.CreateMinigameUI("ClickTargets_test", MinigameUI.MinigameUIContext.Fullscreen, CreateConfig());
            var instance = ui.GetInstanceForTesting(instanceId);
            instance.targets.Clear();
            instance.targets.Add(new MinigameUI.ClickTargetVisual
            {
                screenPosition = new Vector2(100f, 100f),
                radius = 50f,
                isActive = true
            });

            bool fired = false;
            ui.OnTargetClicked += (id, success) =>
            {
                if (id == instanceId && success)
                {
                    fired = true;
                }
            };

            ui.HandleClick(new Vector3(100f, 100f, 0f));

            Assert.IsTrue(fired, "OnTargetClicked should fire");
            Assert.IsFalse(instance.targets[0].isActive, "Target should be deactivated");
        }

        [Test]
        public void UpdateMinigameUI_UpdatesPerformance()
        {
            string instanceId = ui.CreateMinigameUI("ClickTargets_test", MinigameUI.MinigameUIContext.Fullscreen, CreateConfig());
            MinigameInstance minigame = MinigameSystem.Instance.GetMinigameForTesting(instanceId);
            minigame.currentPerformance = 77f;
            ui.UpdateMinigameUI(instanceId);
            Assert.AreEqual(77f, ui.GetPerformance(instanceId), 0.01f, "Performance should sync");
        }

        [Test]
        public void PauseResume_ChangesStateAndEvents()
        {
            string instanceId = ui.CreateMinigameUI("ClickTargets_test", MinigameUI.MinigameUIContext.Fullscreen, CreateConfig());
            bool paused = false;
            bool resumed = false;
            ui.OnMinigamePaused += id => { if (id == instanceId) paused = true; };
            ui.OnMinigameResumed += id => { if (id == instanceId) resumed = true; };

            ui.PauseMinigameUI(instanceId);
            Assert.AreEqual(MinigameUI.MinigameUIState.Paused, ui.GetState(instanceId), "State should be Paused");
            Assert.IsTrue(paused, "Paused event should fire");

            ui.ResumeMinigameUI(instanceId);
            Assert.AreEqual(MinigameUI.MinigameUIState.Active, ui.GetState(instanceId), "State should be Active");
            Assert.IsTrue(resumed, "Resumed event should fire");
        }

        private MinigameUI.MinigameUIConfig CreateConfig()
        {
            return new MinigameUI.MinigameUIConfig
            {
                minigameType = MinigameType.ClickTargets,
                context = MinigameUI.MinigameUIContext.Fullscreen,
                showPerformance = true
            };
        }

        private static void ResetSingleton(Type type)
        {
            FieldInfo field = type.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(null, null);
            }
        }
    }
}
