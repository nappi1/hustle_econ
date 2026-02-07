using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Core;

namespace Tests.Core
{
    public class PlayerControllerTests
    {
        private GameObject playerGameObject;
        private GameObject timeGameObject;
        private PlayerController controller;

        [SetUp]
        public void SetUp()
        {
            ResetSingleton(typeof(PlayerController));
            ResetSingleton(typeof(TimeEnergySystem));

            timeGameObject = new GameObject("TimeEnergySystem");
            timeGameObject.AddComponent<TimeEnergySystem>();

            playerGameObject = new GameObject("PlayerController");
            playerGameObject.AddComponent<CharacterController>();
            controller = playerGameObject.AddComponent<PlayerController>();

            controller.Teleport(Vector3.zero, Quaternion.identity);
        }

        [TearDown]
        public void TearDown()
        {
            if (playerGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(playerGameObject);
            }

            if (timeGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(timeGameObject);
            }
        }

        [Test]
        public void Initialize_DefaultsToIdleStanding()
        {
            PlayerController.PlayerState state = controller.GetStateForTesting();
            Assert.AreEqual(PlayerController.PlayerMovementState.Idle, state.movementState, "Should default to Idle");
            Assert.AreEqual(PlayerController.PlayerPosture.Standing, state.posture, "Should default to Standing");
        }

        [Test]
        public void Initialize_SetsPlayerId()
        {
            controller.Initialize("player_test");
            PlayerController.PlayerState state = controller.GetStateForTesting();
            Assert.AreEqual("player_test", state.playerId, "PlayerId should be set");
        }

        [Test]
        public void SetMovementState_ChangesAndFiresEvent()
        {
            bool fired = false;
            controller.OnMovementStateChanged += _ => fired = true;
            controller.SetMovementState(PlayerController.PlayerMovementState.Walking);
            Assert.IsTrue(fired, "Event should fire");
            Assert.AreEqual(PlayerController.PlayerMovementState.Walking, controller.GetMovementState(), "State should change");
        }

        [Test]
        public void SetMovementState_Same_NoEvent()
        {
            int count = 0;
            controller.OnMovementStateChanged += _ => count++;
            controller.SetMovementState(PlayerController.PlayerMovementState.Idle);
            Assert.AreEqual(0, count, "Event should not fire when state is unchanged");
        }

        [Test]
        public void SetPosture_Sitting_DisablesMovement()
        {
            controller.SetPosture(PlayerController.PlayerPosture.Sitting);
            Assert.AreEqual(PlayerController.PlayerMovementState.Sitting, controller.GetMovementState(), "Should set movement state to Sitting");
            Assert.IsFalse(controller.CanMove(), "Cannot move while sitting");
        }

        [Test]
        public void SetPosture_Driving_DisablesMovement()
        {
            controller.SetPosture(PlayerController.PlayerPosture.Driving);
            Assert.AreEqual(PlayerController.PlayerMovementState.Driving, controller.GetMovementState(), "Should set movement state to Driving");
            Assert.IsFalse(controller.CanMove(), "Cannot move while driving");
        }

        [Test]
        public void SetPosture_Standing_EnablesMovement()
        {
            controller.SetPosture(PlayerController.PlayerPosture.Sitting);
            controller.SetPosture(PlayerController.PlayerPosture.Standing);
            Assert.IsTrue(controller.CanMove(), "Should be able to move while standing");
        }

        [Test]
        public void SetPosture_FiresEvent()
        {
            bool fired = false;
            controller.OnPostureChanged += _ => fired = true;
            controller.SetPosture(PlayerController.PlayerPosture.Sitting);
            Assert.IsTrue(fired, "Posture event should fire");
        }

        [Test]
        public void SetMovementEnabled_False_BlocksCanMove()
        {
            controller.SetMovementEnabled(false);
            Assert.IsFalse(controller.CanMove(), "Movement should be disabled");
        }

        [Test]
        public void SetMovementEnabled_True_AllowsCanMove()
        {
            controller.SetMovementEnabled(true);
            Assert.IsTrue(controller.CanMove(), "Movement should be enabled");
        }

        [Test]
        public void CanMove_Locked_False()
        {
            controller.SetMovementStateForTesting(PlayerController.PlayerMovementState.Locked);
            Assert.IsFalse(controller.CanMove(), "Locked state should prevent movement");
        }

        [Test]
        public void CanMove_Sitting_False()
        {
            controller.SetMovementStateForTesting(PlayerController.PlayerMovementState.Sitting);
            Assert.IsFalse(controller.CanMove(), "Sitting state should prevent movement");
        }

        [Test]
        public void CanMove_Driving_False()
        {
            controller.SetMovementStateForTesting(PlayerController.PlayerMovementState.Driving);
            Assert.IsFalse(controller.CanMove(), "Driving state should prevent movement");
        }

        [Test]
        public void CanMove_Idle_True()
        {
            controller.SetMovementStateForTesting(PlayerController.PlayerMovementState.Idle);
            Assert.IsTrue(controller.CanMove(), "Idle state should allow movement");
        }

        [Test]
        public void Teleport_UpdatesPositionAndRotation()
        {
            Vector3 targetPos = new Vector3(2f, 0f, -3f);
            Quaternion targetRot = Quaternion.Euler(0f, 90f, 0f);
            controller.Teleport(targetPos, targetRot);
            Assert.AreEqual(targetPos, controller.GetPosition(), "Position should update");
            float angle = Quaternion.Angle(targetRot, controller.GetRotation());
            Assert.LessOrEqual(angle, 0.01f, "Rotation should update");
        }

        [Test]
        public void Teleport_FiresPositionChanged()
        {
            bool fired = false;
            controller.OnPositionChanged += _ => fired = true;
            controller.Teleport(new Vector3(1f, 0f, 1f), Quaternion.identity);
            Assert.IsTrue(fired, "Position change event should fire");
        }

        [Test]
        public void SetPositionForTesting_UpdatesPosition()
        {
            Vector3 targetPos = new Vector3(5f, 0f, 5f);
            controller.SetPositionForTesting(targetPos);
            Assert.AreEqual(targetPos, controller.GetPosition(), "Position should update");
        }

        [Test]
        public void GetPosition_ReturnsTransformPosition()
        {
            Vector3 targetPos = new Vector3(1f, 0f, 2f);
            controller.SetPositionForTesting(targetPos);
            Assert.AreEqual(targetPos, controller.GetPosition(), "GetPosition should return transform position");
        }

        [Test]
        public void GetRotation_ReturnsTransformRotation()
        {
            Quaternion rot = Quaternion.Euler(0f, 45f, 0f);
            controller.Teleport(Vector3.zero, rot);
            Assert.AreEqual(rot, controller.GetRotation(), "GetRotation should return transform rotation");
        }

        [Test]
        public void GetTransform_ReturnsTransform()
        {
            Assert.AreEqual(playerGameObject.transform, controller.GetTransform(), "Transform should be the player transform");
        }

        [Test]
        public void GetCurrentSpeed_DefaultZero()
        {
            Assert.AreEqual(0f, controller.GetCurrentSpeed(), 0.001f, "Default speed should be zero");
        }

        [Test]
        public void GetMovementState_ReturnsCurrent()
        {
            controller.SetMovementState(PlayerController.PlayerMovementState.Walking);
            Assert.AreEqual(PlayerController.PlayerMovementState.Walking, controller.GetMovementState(), "Should return current movement state");
        }

        [Test]
        public void GetPosture_ReturnsCurrent()
        {
            controller.SetPosture(PlayerController.PlayerPosture.Sitting);
            Assert.AreEqual(PlayerController.PlayerPosture.Sitting, controller.GetPosture(), "Should return current posture");
        }

        [Test]
        public void GetLookTarget_HitsWhenTargetInFront()
        {
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.transform.position = new Vector3(0f, 1.6f, 2f);
            Physics.SyncTransforms();

            RaycastHit hit = controller.GetLookTarget(3f);
            Assert.IsNotNull(hit.collider, "Should hit target");
            Assert.AreEqual(target, hit.collider.gameObject, "Hit should be the target");

            UnityEngine.Object.DestroyImmediate(target);
        }

        [Test]
        public void GetLookTarget_NoHitReturnsDefault()
        {
            RaycastHit hit = controller.GetLookTarget(3f);
            Assert.IsNull(hit.collider, "Should not hit anything");
        }

        [Test]
        public void IsLookingAt_TargetInFrontTrue()
        {
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.transform.position = new Vector3(0f, 1.6f, 2f);
            Physics.SyncTransforms();

            bool looking = controller.IsLookingAt(target, 3f);
            Assert.IsTrue(looking, "Should be looking at target");

            UnityEngine.Object.DestroyImmediate(target);
        }

        [Test]
        public void IsLookingAt_TargetBehindFalse()
        {
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.transform.position = new Vector3(0f, 1.6f, 2f);
            controller.Teleport(Vector3.zero, Quaternion.Euler(0f, 180f, 0f));

            bool looking = controller.IsLookingAt(target, 3f);
            Assert.IsFalse(looking, "Should not be looking at target");

            UnityEngine.Object.DestroyImmediate(target);
        }

        [Test]
        public void DrainRunEnergy_RunningDecreasesEnergy()
        {
            controller.SetMovementStateForTesting(PlayerController.PlayerMovementState.Running);
            float before = TimeEnergySystem.Instance.GetEnergyLevel();

            InvokeDrainRunEnergy(controller, 60f);

            float after = TimeEnergySystem.Instance.GetEnergyLevel();
            Assert.AreEqual(before - 5f, after, 0.01f, "Energy should decrease by 5 over 60 seconds");
        }

        [Test]
        public void DrainRunEnergy_FiresEvent()
        {
            controller.SetMovementStateForTesting(PlayerController.PlayerMovementState.Running);
            float amount = 0f;
            controller.OnRunEnergyDrained += drained => amount = drained;

            InvokeDrainRunEnergy(controller, 60f);

            Assert.AreEqual(5f, amount, 0.01f, "Run energy drained amount should be 5");
        }

        [Test]
        public void DrainRunEnergy_NotRunning_NoEnergyChange()
        {
            controller.SetMovementStateForTesting(PlayerController.PlayerMovementState.Idle);
            float before = TimeEnergySystem.Instance.GetEnergyLevel();

            InvokeDrainRunEnergy(controller, 60f);

            float after = TimeEnergySystem.Instance.GetEnergyLevel();
            Assert.AreEqual(before, after, 0.01f, "Energy should not change when not running");
        }

        private void InvokeDrainRunEnergy(PlayerController target, float deltaTime)
        {
            MethodInfo method = typeof(PlayerController).GetMethod("DrainRunEnergy", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(target, new object[] { deltaTime });
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
