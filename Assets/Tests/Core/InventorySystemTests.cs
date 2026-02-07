using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Core;
using Data;

namespace Tests.Core
{
    public class InventorySystemTests
    {
        private GameObject inventoryGameObject;
        private GameObject entityGameObject;
        private GameObject locationGameObject;
        private InventorySystem system;

        [SetUp]
        public void SetUp()
        {
            ResetSingleton(typeof(InventorySystem));
            ResetSingleton(typeof(EntitySystem));
            ResetSingleton(typeof(LocationSystem));

            entityGameObject = new GameObject("EntitySystem");
            entityGameObject.AddComponent<EntitySystem>();

            locationGameObject = new GameObject("LocationSystem");
            locationGameObject.AddComponent<LocationSystem>();

            inventoryGameObject = new GameObject("InventorySystem");
            system = inventoryGameObject.AddComponent<InventorySystem>();
        }

        [TearDown]
        public void TearDown()
        {
            if (inventoryGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(inventoryGameObject);
            }

            if (locationGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(locationGameObject);
            }

            if (entityGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(entityGameObject);
            }
        }

        [Test]
        public void GetInventory_ReturnsAllOwnedItems()
        {
            Entity a = EntitySystem.Instance.CreateEntity(EntityType.Item, new EntityData { owner = "player" });
            Entity b = EntitySystem.Instance.CreateEntity(EntityType.Vehicle, new EntityData { owner = "player" });

            List<Entity> items = system.GetInventory("player");
            Assert.AreEqual(2, items.Count, "Inventory should include all owned entities");
        }

        [Test]
        public void GetItemsByCategory_FiltersCorrectly()
        {
            EntitySystem.Instance.CreateEntity(EntityType.Item, new EntityData { owner = "player" });
            EntitySystem.Instance.CreateEntity(EntityType.Vehicle, new EntityData { owner = "player" });
            List<Entity> items = system.GetItemsByCategory("player", EntityType.Item);
            Assert.AreEqual(1, items.Count, "Should return only matching category");
        }

        [Test]
        public void AddItem_AddsToInventoryAndSetsOwnership()
        {
            Entity entity = EntitySystem.Instance.CreateEntity(EntityType.Item, new EntityData { owner = null });
            system.AddItem("player", entity.id);
            Entity updated = EntitySystem.Instance.GetEntity(entity.id);
            Assert.AreEqual("player", updated.owner, "Ownership should be transferred");
            Assert.IsTrue(system.HasItem("player", entity.id), "HasItem should return true");
        }

        [Test]
        public void AddItem_FiresOnItemAdded()
        {
            Entity entity = EntitySystem.Instance.CreateEntity(EntityType.Item, new EntityData { owner = null });
            bool fired = false;
            system.OnItemAdded += (player, id) => fired = true;
            system.AddItem("player", entity.id);
            Assert.IsTrue(fired, "OnItemAdded should fire");
        }

        [Test]
        public void RemoveItem_RemovesOwnershipAndInventory()
        {
            Entity entity = EntitySystem.Instance.CreateEntity(EntityType.Item, new EntityData { owner = "player" });
            system.AddItem("player", entity.id);
            system.RemoveItem("player", entity.id);
            Entity updated = EntitySystem.Instance.GetEntity(entity.id);
            Assert.IsNull(updated.owner, "Ownership should be cleared");
            Assert.IsFalse(system.HasItem("player", entity.id), "Item should be removed");
        }

        [Test]
        public void RemoveItem_FiresOnItemRemoved()
        {
            Entity entity = EntitySystem.Instance.CreateEntity(EntityType.Item, new EntityData { owner = "player" });
            system.AddItem("player", entity.id);
            bool fired = false;
            system.OnItemRemoved += (player, id) => fired = true;
            system.RemoveItem("player", entity.id);
            Assert.IsTrue(fired, "OnItemRemoved should fire");
        }

        [Test]
        public void HasItem_ReturnsTrueForOwned()
        {
            Entity entity = EntitySystem.Instance.CreateEntity(EntityType.Item, new EntityData { owner = "player" });
            Assert.IsTrue(system.HasItem("player", entity.id), "HasItem should be true");
        }

        [Test]
        public void GetItemCount_CountsByType()
        {
            EntitySystem.Instance.CreateEntity(EntityType.Item, new EntityData { owner = "player" });
            EntitySystem.Instance.CreateEntity(EntityType.Item, new EntityData { owner = "player" });
            EntitySystem.Instance.CreateEntity(EntityType.Vehicle, new EntityData { owner = "player" });
            Assert.AreEqual(2, system.GetItemCount("player", EntityType.Item), "Count should match");
        }

        [Test]
        public void EquipItem_EquipsToSlot()
        {
            Entity entity = EntitySystem.Instance.CreateEntity(EntityType.Item, new EntityData { owner = "player" });
            system.EquipItem("player", entity.id, InventorySystem.EquipSlot.Clothing);
            Entity equipped = system.GetEquippedItem("player", InventorySystem.EquipSlot.Clothing);
            Assert.AreEqual(entity.id, equipped.id, "Equipped item should match");
        }

        [Test]
        public void EquipItem_UnequipsPrevious()
        {
            Entity a = EntitySystem.Instance.CreateEntity(EntityType.Item, new EntityData { owner = "player" });
            Entity b = EntitySystem.Instance.CreateEntity(EntityType.Item, new EntityData { owner = "player" });
            system.EquipItem("player", a.id, InventorySystem.EquipSlot.Clothing);
            system.EquipItem("player", b.id, InventorySystem.EquipSlot.Clothing);
            Entity equipped = system.GetEquippedItem("player", InventorySystem.EquipSlot.Clothing);
            Assert.AreEqual(b.id, equipped.id, "Equipped item should be replaced");
        }

        [Test]
        public void UnequipItem_ClearsSlot()
        {
            Entity entity = EntitySystem.Instance.CreateEntity(EntityType.Item, new EntityData { owner = "player" });
            system.EquipItem("player", entity.id, InventorySystem.EquipSlot.Clothing);
            system.UnequipItem("player", InventorySystem.EquipSlot.Clothing);
            Assert.IsNull(system.GetEquippedItem("player", InventorySystem.EquipSlot.Clothing), "Slot should be cleared");
        }

        [Test]
        public void EquipItem_NotOwned_DoesNothing()
        {
            Entity entity = EntitySystem.Instance.CreateEntity(EntityType.Item, new EntityData { owner = "other" });
            system.EquipItem("player", entity.id, InventorySystem.EquipSlot.Clothing);
            Assert.IsNull(system.GetEquippedItem("player", InventorySystem.EquipSlot.Clothing), "Should not equip unowned item");
        }

        [Test]
        public void RemoveItem_AutoUnequips()
        {
            Entity entity = EntitySystem.Instance.CreateEntity(EntityType.Item, new EntityData { owner = "player" });
            system.EquipItem("player", entity.id, InventorySystem.EquipSlot.Clothing);
            system.RemoveItem("player", entity.id);
            Assert.IsNull(system.GetEquippedItem("player", InventorySystem.EquipSlot.Clothing), "Should auto-unequip on remove");
        }

        [Test]
        public void EquipItem_WhenItemAtDifferentLocation_Fails()
        {
            Entity entity = EntitySystem.Instance.CreateEntity(EntityType.Item, new EntityData { owner = "player" });
            LocationSystem.Instance.SetPlayerLocationForTesting("player", "street");
            system.SetItemLocation("player", entity.id, "apartment");
            system.EquipItem("player", entity.id, InventorySystem.EquipSlot.Clothing);
            Assert.IsNull(system.GetEquippedItem("player", InventorySystem.EquipSlot.Clothing), "Should not equip if not accessible");
        }

        [Test]
        public void HasAccessToItem_ReturnsTrueWhenNoLocation()
        {
            Entity entity = EntitySystem.Instance.CreateEntity(EntityType.Item, new EntityData { owner = "player" });
            Assert.IsTrue(system.HasAccessToItem("player", entity.id), "No location means accessible");
        }

        [Test]
        public void SetItemLocation_TrackedCorrectly()
        {
            Entity entity = EntitySystem.Instance.CreateEntity(EntityType.Item, new EntityData { owner = "player" });
            system.SetItemLocation("player", entity.id, "apartment");
            Assert.AreEqual("apartment", system.GetItemLocation("player", entity.id), "Location should be tracked");
        }

        [Test]
        public void GetEquippedItem_ReturnsNullWhenNotEquipped()
        {
            Assert.IsNull(system.GetEquippedItem("player", InventorySystem.EquipSlot.Clothing), "Should return null");
        }

        [Test]
        public void OnItemEquipped_Fires()
        {
            Entity entity = EntitySystem.Instance.CreateEntity(EntityType.Item, new EntityData { owner = "player" });
            bool fired = false;
            system.OnItemEquipped += (player, id, slot) => fired = true;
            system.EquipItem("player", entity.id, InventorySystem.EquipSlot.Clothing);
            Assert.IsTrue(fired, "OnItemEquipped should fire");
        }

        [Test]
        public void OnItemUnequipped_Fires()
        {
            Entity entity = EntitySystem.Instance.CreateEntity(EntityType.Item, new EntityData { owner = "player" });
            system.EquipItem("player", entity.id, InventorySystem.EquipSlot.Clothing);
            bool fired = false;
            system.OnItemUnequipped += (player, slot) => fired = true;
            system.UnequipItem("player", InventorySystem.EquipSlot.Clothing);
            Assert.IsTrue(fired, "OnItemUnequipped should fire");
        }

        [Test]
        public void GetInventory_UsesEntitySystemOwner()
        {
            Entity entity = EntitySystem.Instance.CreateEntity(EntityType.Item, new EntityData { owner = "player" });
            List<Entity> items = system.GetInventory("player");
            Assert.AreEqual(entity.id, items[0].id, "Inventory should use EntitySystem ownership");
        }

        [Test]
        public void EquipItem_EmptyLocationAllowed()
        {
            Entity entity = EntitySystem.Instance.CreateEntity(EntityType.Item, new EntityData { owner = "player" });
            system.EquipItem("player", entity.id, InventorySystem.EquipSlot.Clothing);
            Assert.IsNotNull(system.GetEquippedItem("player", InventorySystem.EquipSlot.Clothing), "Should equip when no location set");
        }

        [Test]
        public void RemoveItem_NotOwned_NoThrow()
        {
            system.RemoveItem("player", "missing");
            Assert.Pass("No exception on missing item");
        }

        [Test]
        public void EquipItem_ReplacesPreviousInSlot()
        {
            Entity a = EntitySystem.Instance.CreateEntity(EntityType.Item, new EntityData { owner = "player" });
            Entity b = EntitySystem.Instance.CreateEntity(EntityType.Item, new EntityData { owner = "player" });
            system.EquipItem("player", a.id, InventorySystem.EquipSlot.Clothing);
            system.EquipItem("player", b.id, InventorySystem.EquipSlot.Clothing);
            Entity equipped = system.GetEquippedItem("player", InventorySystem.EquipSlot.Clothing);
            Assert.AreEqual(b.id, equipped.id, "Slot should contain only last equipped item");
        }

        [Test]
        public void GetItemsByCategory_EmptyWhenNone()
        {
            List<Entity> items = system.GetItemsByCategory("player", EntityType.Vehicle);
            Assert.AreEqual(0, items.Count, "Empty category should return empty list");
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

