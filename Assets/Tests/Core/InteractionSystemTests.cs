using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Core;
using UI;

namespace Tests.Core
{
    public class InteractionSystemTests
    {
        private GameObject _interactionObject;
        private GameObject _inventoryObject;
        private GameObject _entityObject;
        private GameObject _playerObject;
        private GameObject _phoneObject;
        private GameObject _hudObject;
        private InteractionSystem _system;

        [SetUp]
        public void SetUp()
        {
            ResetSingleton(typeof(InteractionSystem));
            ResetSingleton(typeof(InventorySystem));
            ResetSingleton(typeof(EntitySystem));
            ResetSingleton(typeof(PlayerController));
            ResetSingleton(typeof(PhoneUI));
            ResetSingleton(typeof(HUDController));
            ResetSingleton(typeof(JobSystem));
            ResetSingleton(typeof(LocationSystem));
            ResetSingleton(typeof(InputManager));

            _entityObject = new GameObject("EntitySystem");
            _entityObject.AddComponent<EntitySystem>();

            _inventoryObject = new GameObject("InventorySystem");
            _inventoryObject.AddComponent<InventorySystem>();

            _playerObject = new GameObject("PlayerController");
            _playerObject.AddComponent<CharacterController>();
            _playerObject.AddComponent<PlayerController>();

            _phoneObject = new GameObject("PhoneUI");
            _phoneObject.AddComponent<PhoneUI>();

            _hudObject = new GameObject("HUDController");
            _hudObject.AddComponent<HUDController>();

            _interactionObject = new GameObject("InteractionSystem");
            _system = _interactionObject.AddComponent<InteractionSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_interactionObject != null) UnityEngine.Object.DestroyImmediate(_interactionObject);
            if (_inventoryObject != null) UnityEngine.Object.DestroyImmediate(_inventoryObject);
            if (_entityObject != null) UnityEngine.Object.DestroyImmediate(_entityObject);
            if (_playerObject != null) UnityEngine.Object.DestroyImmediate(_playerObject);
            if (_phoneObject != null) UnityEngine.Object.DestroyImmediate(_phoneObject);
            if (_hudObject != null) UnityEngine.Object.DestroyImmediate(_hudObject);
        }

        [Test]
        public void SetCurrentTargetForTesting_SetsTarget()
        {
            var data = new InteractionSystem.InteractionData { type = InteractionSystem.InteractionType.Examine };
            _system.SetCurrentTargetForTesting(data);
            Assert.AreEqual(data, _system.GetCurrentTarget(), "Target should be set");
        }

        [Test]
        public void TryInteract_NoTarget_ReturnsUnavailable()
        {
            _system.SetCurrentTargetForTesting(null);
            var result = _system.TryInteract();
            Assert.AreEqual(InteractionSystem.InteractionResult.Unavailable, result, "No target should be unavailable");
        }

        [Test]
        public void TryInteract_TooSoon_ReturnsCancelled()
        {
            var data = new InteractionSystem.InteractionData { type = InteractionSystem.InteractionType.Examine, isAvailable = true };
            _system.SetCurrentTargetForTesting(data);
            _system.TryInteract();
            var result = _system.TryInteract();
            Assert.AreEqual(InteractionSystem.InteractionResult.Cancelled, result, "Cooldown should cancel");
        }

        [Test]
        public void TryInteract_Unavailable_FiresFailed()
        {
            bool fired = false;
            var data = new InteractionSystem.InteractionData
            {
                type = InteractionSystem.InteractionType.Examine,
                isAvailable = false,
                unavailableReason = "Blocked"
            };
            _system.SetCurrentTargetForTesting(data);
            _system.OnInteractionFailed += (_, reason) =>
            {
                if (reason == "Blocked")
                {
                    fired = true;
                }
            };
            var result = _system.TryInteract();
            Assert.AreEqual(InteractionSystem.InteractionResult.Failed, result, "Should fail");
            Assert.IsTrue(fired, "Failure event should fire");
        }

        [Test]
        public void Interact_UsePhone_TogglesPhone()
        {
            var data = new InteractionSystem.InteractionData { type = InteractionSystem.InteractionType.UsePhone, isAvailable = true };
            _system.Interact(data);
            Assert.IsTrue(PhoneUI.Instance.IsPhoneOpen(), "Phone should open");
        }

        [Test]
        public void Interact_SitStand_UpdatesPosture()
        {
            var sitData = new InteractionSystem.InteractionData { type = InteractionSystem.InteractionType.SitDown, isAvailable = true };
            _system.Interact(sitData);
            Assert.AreEqual(PlayerController.PlayerPosture.Sitting, PlayerController.Instance.GetPosture(), "Should sit");

            var standData = new InteractionSystem.InteractionData { type = InteractionSystem.InteractionType.StandUp, isAvailable = true };
            _system.Interact(standData);
            Assert.AreEqual(PlayerController.PlayerPosture.Standing, PlayerController.Instance.GetPosture(), "Should stand");
        }

        [Test]
        public void Interact_Examine_ShowsNotification()
        {
            var entity = EntitySystem.Instance.CreateEntity(
                HustleEconomy.Data.EntityType.Item,
                new HustleEconomy.Data.EntityData
                {
                    owner = null,
                    value = 0f,
                    condition = 100f,
                    location = null,
                    status = HustleEconomy.Data.EntityStatus.Active,
                    customProperties = null
                });
            var data = new InteractionSystem.InteractionData { type = InteractionSystem.InteractionType.Examine, targetId = entity.id, isAvailable = true };
            var result = _system.Interact(data);
            Assert.AreEqual(InteractionSystem.InteractionResult.Success, result, "Examine should succeed");
        }

        [Test]
        public void CanInteract_DistanceTooFar_False()
        {
            var data = new InteractionSystem.InteractionData
            {
                type = InteractionSystem.InteractionType.Examine,
                distance = 100f,
                interactableComponent = null
            };
            bool can = _system.CanInteract(data, out string reason);
            Assert.IsFalse(can, "Too far should fail");
            Assert.AreEqual("Too far away", reason, "Reason should indicate distance");
        }

        [Test]
        public void CanInteract_RequiredItemMissing_False()
        {
            var interactable = new GameObject("Interactable").AddComponent<Interactable>();
            interactable.requiresLineOfSight = false;
            interactable.requiredItems.Add("key");
            var data = new InteractionSystem.InteractionData
            {
                type = InteractionSystem.InteractionType.Examine,
                distance = 1f,
                interactableComponent = interactable,
                interactableObject = interactable.gameObject
            };
            bool can = _system.CanInteract(data, out string reason);
            Assert.IsFalse(can, "Missing required item should fail");
            Assert.AreEqual("Missing required item", reason, "Reason should indicate missing item");
            UnityEngine.Object.DestroyImmediate(interactable.gameObject);
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
