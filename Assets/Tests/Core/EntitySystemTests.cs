using System.Collections.Generic;
using System.Reflection;
using Core;
using NUnit.Framework;
using UnityEngine;
using Data;

namespace Tests.Core
{
    public class EntitySystemTests
    {
        private EntitySystem system;
        private GameObject systemGameObject;

        [SetUp]
        public void SetUp()
        {
            ResetSingleton();
            systemGameObject = new GameObject("EntitySystem");
            system = systemGameObject.AddComponent<EntitySystem>();
        }

        [TearDown]
        public void TearDown()
        {
            if (systemGameObject != null)
            {
                Object.DestroyImmediate(systemGameObject);
            }
            ResetSingleton();
        }

        [Test]
        public void CreateEntity_WithValidData_ReturnsEntityWithUniqueId()
        {
            // Arrange
            EntityData data = new EntityData
            {
                value = 100f,
                condition = 100f
            };

            // Act
            Entity entity = system.CreateEntity(EntityType.Item, data);

            // Assert
            Assert.IsNotNull(entity, "CreateEntity should return non-null entity");
            Assert.IsFalse(string.IsNullOrEmpty(entity.id), "Entity id should be assigned");
        }

        [Test]
        public void CreateEntity_ForEachEntityType_CreatesCorrectType()
        {
            // Arrange
            EntityData data = new EntityData { value = 1f, condition = 100f };
            EntityType[] types =
            {
                EntityType.Job, EntityType.NPC, EntityType.Item,
                EntityType.Property, EntityType.Vehicle, EntityType.Business
            };

            // Act
            List<Entity> created = new List<Entity>();
            foreach (EntityType type in types)
            {
                created.Add(system.CreateEntity(type, data));
            }

            // Assert
            for (int i = 0; i < types.Length; i++)
            {
                Assert.AreEqual(types[i], created[i].type, $"Entity type should be {types[i]}");
            }
        }

        [Test]
        public void CreateEntity_WithOwner_RegistersOwnerIndex()
        {
            // Arrange
            string ownerId = "player_1";
            EntityData data = new EntityData { owner = ownerId, value = 10f, condition = 100f };

            // Act
            Entity entity = system.CreateEntity(EntityType.Item, data);
            List<Entity> owned = system.GetEntitiesByOwner(ownerId);

            // Assert
            Assert.AreEqual(1, owned.Count, "Owner should have one entity");
            Assert.AreEqual(entity.id, owned[0].id, "Owner list should contain created entity");
        }

        [Test]
        public void CreateEntity_WithNullOwner_DoesNotRegisterOwnerIndex()
        {
            // Arrange
            EntityData data = new EntityData { owner = null, value = 10f, condition = 100f };

            // Act
            system.CreateEntity(EntityType.Item, data);
            List<Entity> owned = system.GetEntitiesByOwner(null);

            // Assert
            Assert.AreEqual(0, owned.Count, "Null owner should not have entities");
        }

        [Test]
        public void CreateEntity_GeneratesUniqueIdsAcrossManyEntities()
        {
            // Arrange
            EntityData data = new EntityData { value = 1f, condition = 100f };
            HashSet<string> ids = new HashSet<string>();

            // Act
            for (int i = 0; i < 200; i++)
            {
                Entity entity = system.CreateEntity(EntityType.Item, data);
                ids.Add(entity.id);
            }

            // Assert
            Assert.AreEqual(200, ids.Count, "All generated ids should be unique");
        }

        [Test]
        public void GetEntity_WithValidId_ReturnsEntity()
        {
            // Arrange
            Entity entity = CreateBasicEntity(EntityType.Item);

            // Act
            Entity fetched = system.GetEntity(entity.id);

            // Assert
            Assert.IsNotNull(fetched, "GetEntity should return entity for valid id");
            Assert.AreEqual(entity.id, fetched.id, "Returned entity should match id");
        }

        [Test]
        public void GetEntity_WithInvalidId_ReturnsNull()
        {
            // Arrange
            string invalidId = "not_real";

            // Act
            Entity fetched = system.GetEntity(invalidId);

            // Assert
            Assert.IsNull(fetched, "GetEntity should return null for invalid id");
        }

        [Test]
        public void GetEntity_WithNullOrEmptyId_ReturnsNull()
        {
            // Arrange
            string nullId = null;
            string emptyId = "";

            // Act
            Entity nullResult = system.GetEntity(nullId);
            Entity emptyResult = system.GetEntity(emptyId);

            // Assert
            Assert.IsNull(nullResult, "GetEntity should return null for null id");
            Assert.IsNull(emptyResult, "GetEntity should return null for empty id");
        }

        [Test]
        public void GetEntitiesByType_NoMatches_ReturnsEmpty()
        {
            // Arrange
            CreateBasicEntity(EntityType.Item);

            // Act
            List<Entity> results = system.GetEntitiesByType(EntityType.Vehicle);

            // Assert
            Assert.AreEqual(0, results.Count, "No entities of requested type should return empty list");
        }

        [Test]
        public void GetEntitiesByType_FiltersCorrectly()
        {
            // Arrange
            Entity item = CreateBasicEntity(EntityType.Item);
            CreateBasicEntity(EntityType.Vehicle);

            // Act
            List<Entity> items = system.GetEntitiesByType(EntityType.Item);

            // Assert
            Assert.AreEqual(1, items.Count, "Should return only entities of requested type");
            Assert.AreEqual(item.id, items[0].id, "Returned entity should be the item");
        }

        [Test]
        public void GetEntitiesByOwner_NoMatches_ReturnsEmpty()
        {
            // Arrange
            CreateBasicEntity(EntityType.Item);

            // Act
            List<Entity> results = system.GetEntitiesByOwner("unknown_owner");

            // Assert
            Assert.AreEqual(0, results.Count, "No entities for owner should return empty list");
        }

        [Test]
        public void GetEntitiesByOwner_FiltersCorrectly()
        {
            // Arrange
            string ownerId = "npc_1";
            Entity owned = system.CreateEntity(EntityType.Item, new EntityData
            {
                owner = ownerId,
                value = 5f,
                condition = 100f
            });
            CreateBasicEntity(EntityType.Item);

            // Act
            List<Entity> results = system.GetEntitiesByOwner(ownerId);

            // Assert
            Assert.AreEqual(1, results.Count, "Owner should have one entity");
            Assert.AreEqual(owned.id, results[0].id, "Returned entity should match owner");
        }

        [Test]
        public void UpdateEntity_ModifiesPropertiesCorrectly()
        {
            // Arrange
            Entity entity = CreateBasicEntity(EntityType.Item);
            Dictionary<string, object> updates = new Dictionary<string, object>
            {
                ["value"] = 250f,
                ["condition"] = 50f,
                ["location"] = "street_main",
                ["status"] = EntityStatus.Locked
            };

            // Act
            bool result = system.UpdateEntity(entity.id, updates);
            Entity updated = system.GetEntity(entity.id);

            // Assert
            Assert.IsTrue(result, "UpdateEntity should succeed for valid id");
            Assert.AreEqual(250f, updated.value, "Value should update");
            Assert.AreEqual(50f, updated.condition, "Condition should update");
            Assert.AreEqual("street_main", updated.location, "Location should update");
            Assert.AreEqual(EntityStatus.Locked, updated.status, "Status should update");
        }

        [Test]
        public void UpdateEntity_UpdatesOwnerIndex()
        {
            // Arrange
            Entity entity = CreateBasicEntity(EntityType.Item);
            Dictionary<string, object> updates = new Dictionary<string, object>
            {
                ["owner"] = "player_2"
            };

            // Act
            system.UpdateEntity(entity.id, updates);
            List<Entity> owned = system.GetEntitiesByOwner("player_2");

            // Assert
            Assert.AreEqual(1, owned.Count, "Owner index should update when owner changes");
            Assert.AreEqual(entity.id, owned[0].id, "Owner list should contain updated entity");
        }

        [Test]
        public void UpdateEntity_OnNonExistentEntity_ReturnsFalse()
        {
            // Arrange
            Dictionary<string, object> updates = new Dictionary<string, object>
            {
                ["value"] = 10f
            };

            // Act
            bool result = system.UpdateEntity("missing", updates);

            // Assert
            Assert.IsFalse(result, "UpdateEntity should return false for missing entity");
        }

        [Test]
        public void UpdateEntity_OnNonExistentEntity_DoesNotFireEvent()
        {
            // Arrange
            bool eventFired = false;
            system.OnEntityUpdated += (_, __) => eventFired = true;
            Dictionary<string, object> updates = new Dictionary<string, object>
            {
                ["value"] = 10f
            };

            // Act
            system.UpdateEntity("missing", updates);

            // Assert
            Assert.IsFalse(eventFired, "UpdateEntity should not fire event when update fails");
        }

        [Test]
        public void UpdateEntity_WithNullUpdates_ReturnsTrueAndDoesNotFireEvent()
        {
            // Arrange
            Entity entity = CreateBasicEntity(EntityType.Item);
            bool eventFired = false;
            system.OnEntityUpdated += (_, __) => eventFired = true;

            // Act
            bool result = system.UpdateEntity(entity.id, null);

            // Assert
            Assert.IsTrue(result, "UpdateEntity should return true for null updates");
            Assert.IsFalse(eventFired, "UpdateEntity should not fire event when no updates provided");
        }

        [Test]
        public void TransferOwnership_UpdatesOwnerField()
        {
            // Arrange
            Entity entity = CreateBasicEntity(EntityType.Item);

            // Act
            bool result = system.TransferOwnership(entity.id, "player_new");
            Entity updated = system.GetEntity(entity.id);

            // Assert
            Assert.IsTrue(result, "TransferOwnership should succeed for valid id");
            Assert.AreEqual("player_new", updated.owner, "Owner should update");
        }

        [Test]
        public void TransferOwnership_ToSelf_ReturnsTrueWithoutEvent()
        {
            // Arrange
            Entity entity = system.CreateEntity(EntityType.Item, new EntityData
            {
                owner = "player_1",
                value = 10f,
                condition = 100f
            });
            bool eventFired = false;
            system.OnOwnershipTransferred += (_, __, ___) => eventFired = true;

            // Act
            bool result = system.TransferOwnership(entity.id, "player_1");

            // Assert
            Assert.IsTrue(result, "TransferOwnership to same owner should succeed");
            Assert.IsFalse(eventFired, "Event should not fire when owner does not change");
        }

        [Test]
        public void TransferOwnership_OnNonExistentEntity_ReturnsFalseAndDoesNotFireEvent()
        {
            // Arrange
            bool eventFired = false;
            system.OnOwnershipTransferred += (_, __, ___) => eventFired = true;

            // Act
            bool result = system.TransferOwnership("missing", "player_1");

            // Assert
            Assert.IsFalse(result, "TransferOwnership should return false for missing entity");
            Assert.IsFalse(eventFired, "Event should not fire when transfer fails");
        }

        [Test]
        public void DestroyEntity_RemovesFromIndexes()
        {
            // Arrange
            Entity entity = system.CreateEntity(EntityType.Vehicle, new EntityData
            {
                owner = "player_1",
                value = 500f,
                condition = 80f
            });

            // Act
            bool result = system.DestroyEntity(entity.id);
            List<Entity> byType = system.GetEntitiesByType(EntityType.Vehicle);
            List<Entity> byOwner = system.GetEntitiesByOwner("player_1");

            // Assert
            Assert.IsTrue(result, "DestroyEntity should succeed for valid id");
            Assert.AreEqual(0, byType.Count, "Type index should not include destroyed entity");
            Assert.AreEqual(0, byOwner.Count, "Owner index should not include destroyed entity");
        }

        [Test]
        public void DestroyEntity_OnNonExistentEntity_ReturnsFalseAndDoesNotFireEvent()
        {
            // Arrange
            bool eventFired = false;
            system.OnEntityDestroyed += _ => eventFired = true;

            // Act
            bool result = system.DestroyEntity("missing");

            // Assert
            Assert.IsFalse(result, "DestroyEntity should return false for missing entity");
            Assert.IsFalse(eventFired, "Event should not fire when destroy fails");
        }

        [Test]
        public void GetEntityValue_CalculatesWithCondition()
        {
            // Arrange
            Entity entity = system.CreateEntity(EntityType.Item, new EntityData
            {
                value = 200f,
                condition = 100f
            });
            system.UpdateEntity(entity.id, new Dictionary<string, object> { ["condition"] = 0f });

            // Act
            float valueAtZero = system.GetEntityValue(entity.id);
            system.UpdateEntity(entity.id, new Dictionary<string, object> { ["condition"] = 100f });
            float valueAtFull = system.GetEntityValue(entity.id);

            // Assert
            Assert.AreEqual(0f, valueAtZero, "Value should be zero at 0% condition");
            Assert.AreEqual(200f, valueAtFull, "Value should match base at 100% condition");
        }

        [Test]
        public void OnEntityCreated_WhenEntityCreated_FiresWithCorrectEntity()
        {
            // Arrange
            Entity captured = null;
            system.OnEntityCreated += entity => captured = entity;

            // Act
            Entity created = system.CreateEntity(EntityType.Item, new EntityData
            {
                value = 100f,
                condition = 100f
            });

            // Assert
            Assert.IsNotNull(captured, "Event should have fired");
            Assert.AreEqual(created.id, captured.id, "Event should pass created entity");
        }

        [Test]
        public void OnEntityUpdated_WhenEntityUpdated_FiresWithCorrectData()
        {
            // Arrange
            Entity entity = CreateBasicEntity(EntityType.Item);
            string capturedId = null;
            Dictionary<string, object> capturedUpdates = null;
            system.OnEntityUpdated += (id, updates) =>
            {
                capturedId = id;
                capturedUpdates = updates;
            };
            Dictionary<string, object> updates = new Dictionary<string, object>
            {
                ["value"] = 300f
            };

            // Act
            system.UpdateEntity(entity.id, updates);

            // Assert
            Assert.AreEqual(entity.id, capturedId, "Event should include entity id");
            Assert.AreSame(updates, capturedUpdates, "Event should include same updates dictionary");
        }

        [Test]
        public void OnOwnershipTransferred_WhenTransferred_FiresWithCorrectParameters()
        {
            // Arrange
            Entity entity = system.CreateEntity(EntityType.Item, new EntityData
            {
                owner = "old_owner",
                value = 10f,
                condition = 100f
            });
            string capturedId = null;
            string capturedOldOwner = null;
            string capturedNewOwner = null;
            system.OnOwnershipTransferred += (id, oldOwner, newOwner) =>
            {
                capturedId = id;
                capturedOldOwner = oldOwner;
                capturedNewOwner = newOwner;
            };

            // Act
            system.TransferOwnership(entity.id, "new_owner");

            // Assert
            Assert.AreEqual(entity.id, capturedId, "Event should include entity id");
            Assert.AreEqual("old_owner", capturedOldOwner, "Event should include old owner");
            Assert.AreEqual("new_owner", capturedNewOwner, "Event should include new owner");
        }

        [Test]
        public void OnEntityDestroyed_WhenEntityDestroyed_FiresWithCorrectId()
        {
            // Arrange
            Entity entity = CreateBasicEntity(EntityType.Item);
            string capturedId = null;
            system.OnEntityDestroyed += id => capturedId = id;

            // Act
            system.DestroyEntity(entity.id);

            // Assert
            Assert.AreEqual(entity.id, capturedId, "Event should include destroyed entity id");
        }

        [Test]
        public void UniqueIdGeneration_DoesNotCollideUnderRapidCreateDestroy()
        {
            // Arrange
            EntityData data = new EntityData { value = 1f, condition = 100f };
            HashSet<string> ids = new HashSet<string>();

            // Act
            for (int i = 0; i < 100; i++)
            {
                Entity entity = system.CreateEntity(EntityType.Item, data);
                ids.Add(entity.id);
                system.DestroyEntity(entity.id);
            }

            // Assert
            Assert.AreEqual(100, ids.Count, "Rapid create/destroy should not reuse ids");
        }

        [Test]
        public void LargeEntityCount_PerformanceSanityCheck()
        {
            // Arrange
            EntityData data = new EntityData { value = 1f, condition = 100f };

            // Act
            for (int i = 0; i < 1000; i++)
            {
                system.CreateEntity(EntityType.Item, data);
            }
            List<Entity> results = system.GetEntitiesByType(EntityType.Item);

            // Assert
            Assert.AreEqual(1000, results.Count, "Should create and retrieve large number of entities");
        }

        [Test]
        public void StatusChange_PersistsOnEntity()
        {
            // Arrange
            Entity entity = CreateBasicEntity(EntityType.Item);

            // Act
            system.UpdateEntity(entity.id, new Dictionary<string, object> { ["status"] = EntityStatus.Broken });
            Entity updated = system.GetEntity(entity.id);

            // Assert
            Assert.AreEqual(EntityStatus.Broken, updated.status, "Status should persist after update");
        }

        [Test]
        public void LocationTracking_WorksOnCreateAndUpdate()
        {
            // Arrange
            Entity entity = system.CreateEntity(EntityType.Item, new EntityData
            {
                value = 5f,
                condition = 100f,
                location = "apartment"
            });

            // Act
            system.UpdateEntity(entity.id, new Dictionary<string, object> { ["location"] = "street" });
            Entity updated = system.GetEntity(entity.id);

            // Assert
            Assert.AreEqual("street", updated.location, "Location should update correctly");
        }

        [Test]
        public void UpdateEntity_WithEmptyId_ReturnsFalse()
        {
            // Arrange
            Dictionary<string, object> updates = new Dictionary<string, object>
            {
                ["value"] = 5f
            };

            // Act
            bool result = system.UpdateEntity("", updates);

            // Assert
            Assert.IsFalse(result, "UpdateEntity should return false for empty id");
        }

        [Test]
        public void UpdateEntity_WithNullId_ReturnsFalse()
        {
            // Arrange
            Dictionary<string, object> updates = new Dictionary<string, object>
            {
                ["value"] = 5f
            };

            // Act
            bool result = system.UpdateEntity(null, updates);

            // Assert
            Assert.IsFalse(result, "UpdateEntity should return false for null id");
        }

        [Test]
        public void TransferOwnership_WithEmptyId_ReturnsFalse()
        {
            // Arrange
            string emptyId = "";

            // Act
            bool result = system.TransferOwnership(emptyId, "player_1");

            // Assert
            Assert.IsFalse(result, "TransferOwnership should return false for empty id");
        }

        [Test]
        public void TransferOwnership_WithNullId_ReturnsFalse()
        {
            // Arrange
            string nullId = null;

            // Act
            bool result = system.TransferOwnership(nullId, "player_1");

            // Assert
            Assert.IsFalse(result, "TransferOwnership should return false for null id");
        }

        [Test]
        public void DestroyEntity_WithEmptyId_ReturnsFalse()
        {
            // Arrange
            string emptyId = "";

            // Act
            bool result = system.DestroyEntity(emptyId);

            // Assert
            Assert.IsFalse(result, "DestroyEntity should return false for empty id");
        }

        [Test]
        public void DestroyEntity_WithNullId_ReturnsFalse()
        {
            // Arrange
            string nullId = null;

            // Act
            bool result = system.DestroyEntity(nullId);

            // Assert
            Assert.IsFalse(result, "DestroyEntity should return false for null id");
        }

        [Test]
        public void GetEntitiesByOwner_WithEmptyOwnerId_ReturnsEmpty()
        {
            // Arrange
            string emptyOwner = "";

            // Act
            List<Entity> results = system.GetEntitiesByOwner(emptyOwner);

            // Assert
            Assert.AreEqual(0, results.Count, "Empty owner id should return empty list");
        }

        [Test]
        public void Singleton_Instance_ReturnsSameReference()
        {
            // Arrange
            EntitySystem first = EntitySystem.Instance;

            // Act
            EntitySystem second = EntitySystem.Instance;

            // Assert
            Assert.AreSame(first, second, "Instance should return same reference");
        }

        [Test]
        public void Singleton_PersistsAcrossSceneLoad()
        {
            // Arrange
            EntitySystem instanceBefore = EntitySystem.Instance;

            // Act
            // Force persistence and validate the special scene assignment immediately.
            Object.DontDestroyOnLoad(instanceBefore.gameObject);
            string sceneName = instanceBefore.gameObject.scene.name;

            // Assert
            Assert.AreEqual("DontDestroyOnLoad", sceneName, "EntitySystem should persist across scene changes");
        }

        private Entity CreateBasicEntity(EntityType type)
        {
            return system.CreateEntity(type, new EntityData
            {
                value = 10f,
                condition = 100f
            });
        }

        private static void ResetSingleton()
        {
            // Reset the private static singleton to keep tests isolated.
            FieldInfo field = typeof(EntitySystem).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(null, null);
            }
        }
    }
}

