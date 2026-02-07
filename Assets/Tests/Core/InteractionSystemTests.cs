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
        private GameObject interactionObject;
        private GameObject inventoryObject;
        private GameObject entityObject;
        private GameObject playerObject;
        private GameObject phoneObject;
        private GameObject hudObject;
        private InteractionSystem system;

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

            entityObject = new GameObject("EntitySystem");
            entityObject.AddComponent<EntitySystem>();

            inventoryObject = new GameObject("InventorySystem");
            inventoryObject.AddComponent<InventorySystem>();

            playerObject = new GameObject("PlayerController");
            playerObject.AddComponent<CharacterController>();
            playerObject.AddComponent<PlayerController>();

            phoneObject = new GameObject("PhoneUI");
            phoneObject.AddComponent<PhoneUI>();

            hudObject = new GameObject("HUDController");
            hudObject.AddComponent<HUDController>();

            interactionObject = new GameObject("InteractionSystem");
            system = interactionObject.AddComponent<InteractionSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            if (interactionObject != null) UnityEngine.Object.DestroyImmediate(interactionObject);
            if (inventoryObject != null) UnityEngine.Object.DestroyImmediate(inventoryObject);
            if (entityObject != null) UnityEngine.Object.DestroyImmediate(entityObject);
            if (playerObject != null) UnityEngine.Object.DestroyImmediate(playerObject);
            if (phoneObject != null) UnityEngine.Object.DestroyImmediate(phoneObject);
            if (hudObject != null) UnityEngine.Object.DestroyImmediate(hudObject);
        }

        [Test]
        public void SetCurrentTargetForTesting_SetsTarget()
        {
            var data = new InteractionSystem.InteractionData { type = InteractionSystem.InteractionType.Examine };
            system.SetCurrentTargetForTesting(data);
            Assert.AreEqual(data, system.GetCurrentTarget(), "Target should be set");
        }

        [Test]
        public void TryInteract_NoTarget_ReturnsUnavailable()
        {
            system.SetCurrentTargetForTesting(null);
            var result = system.TryInteract();
            Assert.AreEqual(InteractionSystem.InteractionResult.Unavailable, result, "No target should be unavailable");
        }

        [Test]
        public void TryInteract_TooSoon_ReturnsCancelled()
        {
            var data = new InteractionSystem.InteractionData { type = InteractionSystem.InteractionType.Examine, isAvailable = true };
            system.SetCurrentTargetForTesting(data);
            system.TryInteract();
            var result = system.TryInteract();
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
            system.SetCurrentTargetForTesting(data);
            system.OnInteractionFailed += (_, reason) =>
            {
                if (reason == "Blocked")
                {
                    fired = true;
                }
            };
            var result = system.TryInteract();
            Assert.AreEqual(InteractionSystem.InteractionResult.Failed, result, "Should fail");
            Assert.IsTrue(fired, "Failure event should fire");
        }

        [Test]
        public void Interact_UsePhone_TogglesPhone()
        {
            var data = new InteractionSystem.InteractionData { type = InteractionSystem.InteractionType.UsePhone, isAvailable = true };
            system.Interact(data);
            Assert.IsTrue(PhoneUI.Instance.IsPhoneOpen(), "Phone should open");
        }

        [Test]
        public void Interact_SitStand_UpdatesPosture()
        {
            var sitData = new InteractionSystem.InteractionData { type = InteractionSystem.InteractionType.SitDown, isAvailable = true };
            system.Interact(sitData);
            Assert.AreEqual(PlayerController.PlayerPosture.Sitting, PlayerController.Instance.GetPosture(), "Should sit");

            var standData = new InteractionSystem.InteractionData { type = InteractionSystem.InteractionType.StandUp, isAvailable = true };
            system.Interact(standData);
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
            var result = system.Interact(data);
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
            bool can = system.CanInteract(data, out string reason);
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
            bool can = system.CanInteract(data, out string reason);
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
