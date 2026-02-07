using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Core;

namespace Tests.Core
{
    public class CameraControllerTests
    {
        private GameObject cameraGameObject;
        private GameObject playerGameObject;
        private CameraController controller;

        [SetUp]
        public void SetUp()
        {
            ResetSingleton(typeof(CameraController));
            ResetSingleton(typeof(PlayerController));

            cameraGameObject = new GameObject("CameraController");
            controller = cameraGameObject.AddComponent<CameraController>();

            playerGameObject = new GameObject("Player");
            playerGameObject.AddComponent<CharacterController>();
            var playerController = playerGameObject.AddComponent<PlayerController>();
            controller.SetTarget(playerGameObject.transform);
        }

        [TearDown]
        public void TearDown()
        {
            if (cameraGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(cameraGameObject);
            }

            if (playerGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(playerGameObject);
            }
        }

        [Test]
        public void Initialize_DefaultsToThirdPerson()
        {
            Assert.AreEqual(CameraController.CameraMode.ThirdPerson, controller.GetCurrentMode(), "Default mode should be ThirdPerson");
        }

        [Test]
        public void SetMode_SetsTargetAndTransitions()
        {
            controller.SetMode(CameraController.CameraMode.FirstPerson, false);
            CameraController.CameraState state = controller.GetStateForTesting();
            Assert.AreEqual(CameraController.CameraMode.FirstPerson, state.targetMode, "Target mode should be FirstPerson");
            Assert.IsTrue(state.isTransitioning, "Should start transition");
        }

        [Test]
        public void ToggleMode_AlternatesModes()
        {
            controller.SetModeForTesting(CameraController.CameraMode.ThirdPerson);
            controller.ToggleMode();
            CameraController.CameraState state = controller.GetStateForTesting();
            Assert.AreEqual(CameraController.CameraMode.FirstPerson, state.targetMode, "Toggle should target FirstPerson");
        }

        [Test]
        public void ToggleMode_Locked_DoesNotChange()
        {
            controller.SetModeForTesting(CameraController.CameraMode.ThirdPerson);
            controller.SetLockedForTesting(true);
            controller.ToggleMode();
            CameraController.CameraState state = controller.GetStateForTesting();
            Assert.AreEqual(CameraController.CameraMode.ThirdPerson, state.currentMode, "Mode should remain unchanged when locked");
        }

        [Test]
        public void ForceMode_OverridesLock()
        {
            controller.SetLockedForTesting(true);
            controller.ForceMode(CameraController.CameraMode.FirstPerson);
            Assert.AreEqual(CameraController.CameraMode.FirstPerson, controller.GetCurrentMode(), "ForceMode should apply regardless of lock");
        }

        [Test]
        public void SetMode_LocksCamera()
        {
            bool locked = false;
            controller.OnCameraLocked += () => locked = true;
            controller.SetMode(CameraController.CameraMode.FirstPerson, true, 1f);
            Assert.IsTrue(controller.IsLocked(), "Camera should be locked");
            Assert.IsTrue(locked, "Lock event should fire");
        }

        [Test]
        public void LockExpires_UnlocksCamera()
        {
            bool unlocked = false;
            controller.OnCameraUnlocked += () => unlocked = true;
            controller.SetMode(CameraController.CameraMode.FirstPerson, true, 0f);

            InvokeUpdateLockTimer(controller);

            Assert.IsFalse(controller.IsLocked(), "Camera should unlock when duration elapsed");
            Assert.IsTrue(unlocked, "Unlock event should fire");
        }

        [Test]
        public void SetMode_FiresModeChangeStarted()
        {
            bool fired = false;
            controller.OnModeChangeStarted += _ => fired = true;
            controller.SetMode(CameraController.CameraMode.FirstPerson, false);
            Assert.IsTrue(fired, "ModeChangeStarted should fire");
        }

        [Test]
        public void TransitionComplete_FiresEvents()
        {
            bool modeChanged = false;
            bool transitionCompleted = false;
            controller.OnModeChanged += _ => modeChanged = true;
            controller.OnTransitionCompleted += () => transitionCompleted = true;

            controller.SetMode(CameraController.CameraMode.FirstPerson, false);
            InvokeCompleteTransition(controller);

            Assert.IsTrue(modeChanged, "ModeChanged should fire");
            Assert.IsTrue(transitionCompleted, "TransitionCompleted should fire");
        }

        [Test]
        public void GetCurrentFOV_DefaultsToThirdPersonFOV()
        {
            Assert.AreEqual(60f, controller.GetCurrentFOV(), 0.01f, "Default FOV should be 60");
        }

        [Test]
        public void SetFOV_UpdatesValue()
        {
            controller.SetFOV(75f);
            Assert.AreEqual(75f, controller.GetCurrentFOV(), 0.01f, "FOV should update");
        }

        [Test]
        public void SetModeForTesting_InstantSwitch()
        {
            controller.SetModeForTesting(CameraController.CameraMode.FirstPerson);
            Assert.AreEqual(CameraController.CameraMode.FirstPerson, controller.GetCurrentMode(), "Mode should switch immediately");
            Assert.IsFalse(controller.IsTransitioning(), "Should not be transitioning");
        }

        [Test]
        public void OnJobStarted_SetsFirstPerson()
        {
            controller.SetModeForTesting(CameraController.CameraMode.ThirdPerson);
            controller.OnJobStarted();
            Assert.AreEqual(CameraController.CameraMode.FirstPerson, controller.GetStateForTesting().targetMode, "Job start should target first-person");
        }

        [Test]
        public void OnJobEnded_SetsThirdPerson()
        {
            controller.SetModeForTesting(CameraController.CameraMode.FirstPerson);
            controller.OnJobEnded();
            Assert.AreEqual(CameraController.CameraMode.ThirdPerson, controller.GetStateForTesting().targetMode, "Job end should target third-person");
        }

        [Test]
        public void OnPhoneOpened_SetsFirstPerson()
        {
            controller.SetModeForTesting(CameraController.CameraMode.ThirdPerson);
            controller.OnPhoneOpened();
            Assert.AreEqual(CameraController.CameraMode.FirstPerson, controller.GetStateForTesting().targetMode, "Phone open should target first-person");
        }

        [Test]
        public void OnDeskSatDown_SetsFirstPerson()
        {
            controller.SetModeForTesting(CameraController.CameraMode.ThirdPerson);
            controller.OnDeskSatDown();
            Assert.AreEqual(CameraController.CameraMode.FirstPerson, controller.GetStateForTesting().targetMode, "Desk sit should target first-person");
        }

        [Test]
        public void UpdateCameraPosition_AppliesOffset()
        {
            playerGameObject.transform.position = new Vector3(1f, 0f, 1f);
            controller.SetModeForTesting(CameraController.CameraMode.ThirdPerson);

            InvokeUpdateCameraPosition(controller);

            Camera cam = cameraGameObject.GetComponent<Camera>();
            Assert.IsNotNull(cam, "Camera should exist");
            Vector3 expected = playerGameObject.transform.position + new Vector3(0.5f, 1.5f, -3f);
            Assert.AreEqual(expected, cam.transform.position, "Camera position should match offset");
        }

        [Test]
        public void UpdateCameraPosition_FirstPersonMatchesRotation()
        {
            playerGameObject.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            controller.SetModeForTesting(CameraController.CameraMode.FirstPerson);

            InvokeUpdateCameraPosition(controller);

            Camera cam = cameraGameObject.GetComponent<Camera>();
            Assert.IsNotNull(cam, "Camera should exist");
            float angle = Quaternion.Angle(playerGameObject.transform.rotation, cam.transform.rotation);
            Assert.LessOrEqual(angle, 0.01f, "Camera rotation should match player in first-person");
        }

        [Test]
        public void UpdateCameraPosition_UpdatesFOV()
        {
            controller.SetModeForTesting(CameraController.CameraMode.FirstPerson);
            InvokeUpdateCameraPosition(controller);
            Camera cam = cameraGameObject.GetComponent<Camera>();
            Assert.AreEqual(90f, cam.fieldOfView, 0.01f, "FOV should match first-person setting");
        }

        private void InvokeCompleteTransition(CameraController target)
        {
            MethodInfo method = typeof(CameraController).GetMethod("CompleteTransition", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(target, null);
        }

        private void InvokeUpdateCameraPosition(CameraController target)
        {
            MethodInfo method = typeof(CameraController).GetMethod("UpdateCameraPosition", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(target, null);
        }

        private void InvokeUpdateLockTimer(CameraController target)
        {
            MethodInfo method = typeof(CameraController).GetMethod("UpdateLockTimer", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(target, null);
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
