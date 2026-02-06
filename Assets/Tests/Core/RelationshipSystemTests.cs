using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Core
{           
    public class RelationshipSystemTests
    {
        private GameObject relationshipGameObject;
        private GameObject timeGameObject;
        private RelationshipSystem system;

        [SetUp]
        public void SetUp()
        {
            ResetSingleton(typeof(RelationshipSystem));
            ResetSingleton(typeof(TimeEnergySystem));

            timeGameObject = new GameObject("TimeEnergySystem");
            timeGameObject.AddComponent<TimeEnergySystem>();

            relationshipGameObject = new GameObject("RelationshipSystem");
            system = relationshipGameObject.AddComponent<RelationshipSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            if (relationshipGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(relationshipGameObject);
            }

            if (timeGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(timeGameObject);
            }
        }

        [Test]
        public void CreateNPC_WithValidData_ReturnsNPCWithCorrectProperties()
        {
            var data = new RelationshipSystem.NPCData
            {
                name = "Alex",
                personality = RelationshipSystem.NPCPersonality.Supportive,
                values = new Dictionary<RelationshipSystem.NPCValue, float>
                {
                    { RelationshipSystem.NPCValue.Loyalty, 1f }
                },
                tolerances = new Dictionary<RelationshipSystem.NPCTolerance, RelationshipSystem.ToleranceLevel>
                {
                    { RelationshipSystem.NPCTolerance.Neglect, RelationshipSystem.ToleranceLevel.Low }
                },
                sexualBoundary = RelationshipSystem.SexualBoundaryType.Monogamous
            };

            RelationshipSystem.NPC npc = system.CreateNPC(RelationshipSystem.NPCType.Friend, data);

            Assert.AreEqual("Alex", npc.name);
            Assert.AreEqual(RelationshipSystem.NPCType.Friend, npc.type);
            Assert.AreEqual(RelationshipSystem.NPCPersonality.Supportive, npc.personality);
            Assert.AreEqual(RelationshipSystem.SexualBoundaryType.Monogamous, npc.sexualBoundary);
            Assert.AreEqual(RelationshipSystem.RelationshipStatus.Active, npc.status);
        }

        [Test]
        public void CreateNPC_DefaultRelationshipScore_Is50()
        {
            RelationshipSystem.NPC npc = system.CreateNPC(RelationshipSystem.NPCType.Family, CreateBasicData("Sam"));
            Assert.AreEqual(50f, npc.relationshipScore, 0.01f);
        }

        [Test]
        public void CreateNPC_InitializesEmptyMemory()
        {
            RelationshipSystem.NPC npc = system.CreateNPC(RelationshipSystem.NPCType.Family, CreateBasicData("Kim"));
            Assert.AreEqual(0, npc.memory.Count);
        }

        [Test]
        public void CreateNPC_FiresOnNPCCreatedEvent()
        {
            RelationshipSystem.NPC captured = null;
            system.OnNPCCreated += npc => captured = npc;
            RelationshipSystem.NPC npc = system.CreateNPC(RelationshipSystem.NPCType.Friend, CreateBasicData("Zoe"));
            Assert.AreEqual(npc.id, captured.id);
        }

        [Test]
        public void GetRelationshipScore_NewNPC_Returns50()
        {
            RelationshipSystem.NPC npc = system.CreateNPC(RelationshipSystem.NPCType.Friend, CreateBasicData("Pat"));
            Assert.AreEqual(50f, system.GetRelationshipScore(npc.id), 0.01f);
        }

        [Test]
        public void ModifyRelationship_PositiveDelta_IncreasesScore()
        {
            RelationshipSystem.NPC npc = system.CreateNPC(RelationshipSystem.NPCType.Friend, CreateBasicData("Pat"));
            system.ModifyRelationship(npc.id, 10f, "test");
            Assert.AreEqual(60f, system.GetRelationshipScore(npc.id), 0.01f);
        }

        [Test]
        public void ModifyRelationship_NegativeDelta_DecreasesScore()
        {
            RelationshipSystem.NPC npc = system.CreateNPC(RelationshipSystem.NPCType.Friend, CreateBasicData("Pat"));
            system.ModifyRelationship(npc.id, -15f, "test");
            Assert.AreEqual(35f, system.GetRelationshipScore(npc.id), 0.01f);
        }

        [Test]
        public void ModifyRelationship_ClampsAt0And100()
        {
            RelationshipSystem.NPC npc = system.CreateNPC(RelationshipSystem.NPCType.Friend, CreateBasicData("Pat"));
            system.ModifyRelationship(npc.id, 1000f, "test");
            Assert.AreEqual(100f, system.GetRelationshipScore(npc.id), 0.01f);
            system.ModifyRelationship(npc.id, -200f, "test");
            Assert.AreEqual(0f, system.GetRelationshipScore(npc.id), 0.01f);
        }

        [Test]
        public void ModifyRelationship_Below20_ChangesStatusToBroken()
        {
            RelationshipSystem.NPC npc = system.CreateNPC(RelationshipSystem.NPCType.Friend, CreateBasicData("Pat"));
            system.ModifyRelationship(npc.id, -40f, "test");
            Assert.AreEqual(RelationshipSystem.RelationshipStatus.Broken, npc.status);
        }

        [Test]
        public void ModifyRelationship_RecoveringFrom20_FiresOnReconciliation()
        {
            RelationshipSystem.NPC npc = system.CreateNPC(RelationshipSystem.NPCType.Friend, CreateBasicData("Pat"));
            bool fired = false;
            system.OnReconciliation += id => fired = true;
            system.ModifyRelationship(npc.id, -40f, "test");
            system.ModifyRelationship(npc.id, 20f, "recover");
            Assert.IsTrue(fired, "Reconciliation should fire when recovering from broken");
            Assert.AreEqual(RelationshipSystem.RelationshipStatus.Reconciling, npc.status);
        }

        [Test]
        public void ObservePlayerAction_HighMemorability_AlwaysAddsToMemory()
        {
            RelationshipSystem.NPC npc = system.CreateNPC(RelationshipSystem.NPCType.Friend, CreateBasicData("Pat"));
            system.ObservePlayerAction(new RelationshipSystem.PlayerAction
            {
                type = RelationshipSystem.ActionType.Arrested,
                memorability = 9,
                timestamp = TimeEnergySystem.Instance.GetCurrentTime()
            });

            Assert.AreEqual(1, npc.memory.Count);
        }

        [Test]
        public void ObservePlayerAction_LowMemorability_OnlyAddsIfSignificant()
        {
            RelationshipSystem.NPC npc = system.CreateNPC(RelationshipSystem.NPCType.Friend, CreateBasicData("Pat"));
            system.ObservePlayerAction(new RelationshipSystem.PlayerAction
            {
                type = RelationshipSystem.ActionType.WorkedOvertime,
                memorability = 2,
                timestamp = TimeEnergySystem.Instance.GetCurrentTime()
            });

            Assert.AreEqual(0, npc.memory.Count);
        }

        [Test]
        public void ObservePlayerAction_CalculatesImpactBasedOnNPCValues()
        {
            var data = CreateBasicData("Pat");
            data.values = new Dictionary<RelationshipSystem.NPCValue, float>
            {
                { RelationshipSystem.NPCValue.Morality, 1f },
                { RelationshipSystem.NPCValue.Stability, 1f }
            };
            RelationshipSystem.NPC npc = system.CreateNPC(RelationshipSystem.NPCType.Family, data);
            float before = npc.relationshipScore;

            system.ObservePlayerAction(new RelationshipSystem.PlayerAction
            {
                type = RelationshipSystem.ActionType.Arrested,
                memorability = 8,
                timestamp = TimeEnergySystem.Instance.GetCurrentTime()
            });

            float after = npc.relationshipScore;
            Assert.AreEqual(before - 35f, after, 0.01f, "Impact should reflect morality and stability values");
        }

        [Test]
        public void AddToMemory_DeterminesCorrectMemoryTier()
        {
            var data = CreateBasicData("Pat");
            data.values = new Dictionary<RelationshipSystem.NPCValue, float>
            {
                { RelationshipSystem.NPCValue.Morality, 1f },
                { RelationshipSystem.NPCValue.Stability, 1f }
            };
            RelationshipSystem.NPC npc = system.CreateNPC(RelationshipSystem.NPCType.Friend, data);

            system.ObservePlayerAction(new RelationshipSystem.PlayerAction
            {
                type = RelationshipSystem.ActionType.Arrested,
                memorability = 9,
                timestamp = TimeEnergySystem.Instance.GetCurrentTime()
            });

            system.ObservePlayerAction(new RelationshipSystem.PlayerAction
            {
                type = RelationshipSystem.ActionType.MissedEvent,
                memorability = 5,
                timestamp = TimeEnergySystem.Instance.GetCurrentTime()
            });

            system.ObservePlayerAction(new RelationshipSystem.PlayerAction
            {
                type = RelationshipSystem.ActionType.Arrested,
                memorability = 2,
                timestamp = TimeEnergySystem.Instance.GetCurrentTime()
            });

            int permanent = npc.memory.Count(m => m.tier == RelationshipSystem.MemoryTier.Permanent);
            int standard = npc.memory.Count(m => m.tier == RelationshipSystem.MemoryTier.Standard);
            int volatileCount = npc.memory.Count(m => m.tier == RelationshipSystem.MemoryTier.Volatile);

            Assert.AreEqual(1, permanent);
            Assert.AreEqual(1, standard);
            Assert.AreEqual(1, volatileCount);
        }

        [Test]
        public void AddToMemory_Permanent_NeverRemoved()
        {
            RelationshipSystem.NPC npc = system.CreateNPC(RelationshipSystem.NPCType.Friend, CreateBasicData("Pat"));

            system.ObservePlayerAction(new RelationshipSystem.PlayerAction
            {
                type = RelationshipSystem.ActionType.Arrested,
                memorability = 9,
                timestamp = TimeEnergySystem.Instance.GetCurrentTime()
            });

            npc.memoryCapacity = 1;

            system.ObservePlayerAction(new RelationshipSystem.PlayerAction
            {
                type = RelationshipSystem.ActionType.MissedEvent,
                memorability = 5,
                timestamp = TimeEnergySystem.Instance.GetCurrentTime()
            });

            Assert.IsTrue(npc.memory.Any(m => m.isPermanent), "Permanent memory should remain after trimming");
        }

        [Test]
        public void AddToMemory_ExceedsCapacity_TrimsLowIntensityNonPermanent()
        {
            var data = CreateBasicData("Pat");
            data.values = new Dictionary<RelationshipSystem.NPCValue, float>
            {
                { RelationshipSystem.NPCValue.Loyalty, 1f },
                { RelationshipSystem.NPCValue.Family, 1f }
            };
            RelationshipSystem.NPC npc = system.CreateNPC(RelationshipSystem.NPCType.Friend, data);
            npc.memoryCapacity = 1;

            system.ObservePlayerAction(new RelationshipSystem.PlayerAction
            {
                type = RelationshipSystem.ActionType.MissedEvent,
                memorability = 5,
                timestamp = TimeEnergySystem.Instance.GetCurrentTime()
            });

            system.ObservePlayerAction(new RelationshipSystem.PlayerAction
            {
                type = RelationshipSystem.ActionType.GotPromoted,
                memorability = 5,
                timestamp = TimeEnergySystem.Instance.GetCurrentTime()
            });

            Assert.AreEqual(1, npc.memory.Count, "Memory should be trimmed to capacity");
        }

        [Test]
        public void Memory_PermanentTier_EmotionDecaysFactRemains()
        {
            RelationshipSystem.NPC npc = system.CreateNPC(RelationshipSystem.NPCType.Friend, CreateBasicData("Pat"));
            system.ObservePlayerAction(new RelationshipSystem.PlayerAction
            {
                type = RelationshipSystem.ActionType.Arrested,
                memorability = 9,
                timestamp = TimeEnergySystem.Instance.GetCurrentTime()
            });

            float before = npc.memory[0].emotionalIntensity;
            InvokePrivateMethod("DecayMemories");
            float after = npc.memory[0].emotionalIntensity;

            Assert.LessOrEqual(after, before, "Permanent memory intensity should not increase");
            Assert.GreaterOrEqual(after, npc.memory[0].initialIntensity * 0.2f, "Permanent memory should not decay below 20%");
        }
        [Test]
        public void Memory_VolatileTier_FadesQuickly()
        {
            var data = CreateBasicData("Pat");
            data.values = new Dictionary<RelationshipSystem.NPCValue, float>
            {
                { RelationshipSystem.NPCValue.Morality, 1f },
                { RelationshipSystem.NPCValue.Stability, 1f }
            };
            RelationshipSystem.NPC npc = system.CreateNPC(RelationshipSystem.NPCType.Friend, data);
            system.ObservePlayerAction(new RelationshipSystem.PlayerAction
            {
                type = RelationshipSystem.ActionType.Arrested,
                memorability = 2,
                timestamp = TimeEnergySystem.Instance.GetCurrentTime()
            });

            int index = npc.memory.FindIndex(m => m.tier == RelationshipSystem.MemoryTier.Volatile);
            float before = npc.memory[index].emotionalIntensity;
            InvokePrivateMethod("DecayMemories");
            float after = npc.memory[index].emotionalIntensity;

            Assert.LessOrEqual(after, before, "Volatile memory should not increase");
        }

        [Test]
        public void Memory_VolatileTier_RemovedWhenIntensityHitsZero()
        {
            var data = CreateBasicData("Pat");
            data.values = new Dictionary<RelationshipSystem.NPCValue, float>
            {
                { RelationshipSystem.NPCValue.Morality, 1f },
                { RelationshipSystem.NPCValue.Stability, 1f }
            };
            RelationshipSystem.NPC npc = system.CreateNPC(RelationshipSystem.NPCType.Friend, data);
            system.ObservePlayerAction(new RelationshipSystem.PlayerAction
            {
                type = RelationshipSystem.ActionType.Arrested,
                memorability = 2,
                timestamp = TimeEnergySystem.Instance.GetCurrentTime()
            });

            int index = npc.memory.FindIndex(m => m.tier == RelationshipSystem.MemoryTier.Volatile);
            RelationshipSystem.ObservedEvent memory = npc.memory[index];
            memory.emotionalIntensity = 0f;
            npc.memory[index] = memory;

            InvokePrivateMethod("DecayMemories");
            Assert.IsFalse(npc.memory.Any(m => m.tier == RelationshipSystem.MemoryTier.Volatile && m.action.type == RelationshipSystem.ActionType.Arrested));
        }

        [Test]
        public void RecallMemory_SortsByIntensityAndRecency()
        {
            RelationshipSystem.NPC npc = system.CreateNPC(RelationshipSystem.NPCType.Friend, CreateBasicData("Pat"));
            system.ObservePlayerAction(new RelationshipSystem.PlayerAction
            {
                type = RelationshipSystem.ActionType.Arrested,
                memorability = 9,
                timestamp = TimeEnergySystem.Instance.GetCurrentTime()
            });

            system.ObservePlayerAction(new RelationshipSystem.PlayerAction
            {
                type = RelationshipSystem.ActionType.MissedEvent,
                memorability = 5,
                timestamp = TimeEnergySystem.Instance.GetCurrentTime().AddMinutes(5)
            });

            var memories = system.RecallMemory(npc.id, 2);
            Assert.AreEqual(RelationshipSystem.ActionType.Arrested, memories[0].action.type);
        }

        [Test]
        public void RecallMemory_RespectsCountLimit()
        {
            RelationshipSystem.NPC npc = system.CreateNPC(RelationshipSystem.NPCType.Friend, CreateBasicData("Pat"));
            system.ObservePlayerAction(new RelationshipSystem.PlayerAction
            {
                type = RelationshipSystem.ActionType.Arrested,
                memorability = 9,
                timestamp = TimeEnergySystem.Instance.GetCurrentTime()
            });
            system.ObservePlayerAction(new RelationshipSystem.PlayerAction
            {
                type = RelationshipSystem.ActionType.MissedEvent,
                memorability = 5,
                timestamp = TimeEnergySystem.Instance.GetCurrentTime()
            });

            var memories = system.RecallMemory(npc.id, 1);
            Assert.AreEqual(1, memories.Count);
        }

        [Test]
        public void OnMemoryAdded_WhenSignificantEventOccurs_Fires()
        {
            RelationshipSystem.NPC npc = system.CreateNPC(RelationshipSystem.NPCType.Friend, CreateBasicData("Pat"));
            bool fired = false;
            system.OnMemoryAdded += (id, memory) =>
            {
                if (id == npc.id)
                {
                    fired = true;
                }
            };

            system.ObservePlayerAction(new RelationshipSystem.PlayerAction
            {
                type = RelationshipSystem.ActionType.Arrested,
                memorability = 9,
                timestamp = TimeEnergySystem.Instance.GetCurrentTime()
            });

            Assert.IsTrue(fired, "OnMemoryAdded should fire for significant memory");
        }

        [Test]
        public void ObservePlayerAction_Arrest_HighMorality_LargePenalty()
        {
            var data = CreateBasicData("Pat");
            data.values = new Dictionary<RelationshipSystem.NPCValue, float>
            {
                { RelationshipSystem.NPCValue.Morality, 1f }
            };
            RelationshipSystem.NPC npc = system.CreateNPC(RelationshipSystem.NPCType.Family, data);
            float before = npc.relationshipScore;
            system.ObservePlayerAction(new RelationshipSystem.PlayerAction
            {
                type = RelationshipSystem.ActionType.Arrested,
                memorability = 8,
                timestamp = TimeEnergySystem.Instance.GetCurrentTime()
            });

            Assert.Less(npc.relationshipScore, before - 10f);
        }

        [Test]
        public void ObservePlayerAction_Arrest_LowMorality_SmallPenalty()
        {
            var data = CreateBasicData("Pat");
            data.values = new Dictionary<RelationshipSystem.NPCValue, float>
            {
                { RelationshipSystem.NPCValue.Morality, 0f },
                { RelationshipSystem.NPCValue.Stability, 0f }
            };
            RelationshipSystem.NPC npc = system.CreateNPC(RelationshipSystem.NPCType.Family, data);
            float before = npc.relationshipScore;
            system.ObservePlayerAction(new RelationshipSystem.PlayerAction
            {
                type = RelationshipSystem.ActionType.Arrested,
                memorability = 8,
                timestamp = TimeEnergySystem.Instance.GetCurrentTime()
            });

            Assert.AreEqual(before, npc.relationshipScore, 0.01f);
        }

        [Test]
        public void ObservePlayerAction_Promotion_HighAmbition_LargeBonus()
        {
            var data = CreateBasicData("Pat");
            data.values = new Dictionary<RelationshipSystem.NPCValue, float>
            {
                { RelationshipSystem.NPCValue.Ambition, 1f },
                { RelationshipSystem.NPCValue.Stability, 1f }
            };
            RelationshipSystem.NPC npc = system.CreateNPC(RelationshipSystem.NPCType.Family, data);
            float before = npc.relationshipScore;
            system.ObservePlayerAction(new RelationshipSystem.PlayerAction
            {
                type = RelationshipSystem.ActionType.GotPromoted,
                memorability = 6,
                timestamp = TimeEnergySystem.Instance.GetCurrentTime()
            });

            Assert.Greater(npc.relationshipScore, before + 10f);
        }

        [Test]
        public void ObservePlayerAction_ExpensivePurchase_HighVanity_Positive()
        {
            var data = CreateBasicData("Pat");
            data.values = new Dictionary<RelationshipSystem.NPCValue, float>
            {
                { RelationshipSystem.NPCValue.Vanity, 1f },
                { RelationshipSystem.NPCValue.Stability, 0f }
            };
            RelationshipSystem.NPC npc = system.CreateNPC(RelationshipSystem.NPCType.Friend, data);
            float before = npc.relationshipScore;
            system.ObservePlayerAction(new RelationshipSystem.PlayerAction
            {
                type = RelationshipSystem.ActionType.BoughtExpensiveItem,
                memorability = 6,
                timestamp = TimeEnergySystem.Instance.GetCurrentTime()
            });

            Assert.Greater(npc.relationshipScore, before);
        }

        [Test]
        public void ObservePlayerAction_ExpensivePurchase_LowVanity_Negative()
        {
            var data = CreateBasicData("Pat");
            data.values = new Dictionary<RelationshipSystem.NPCValue, float>
            {
                { RelationshipSystem.NPCValue.Vanity, 0f },
                { RelationshipSystem.NPCValue.Stability, 1f }
            };
            RelationshipSystem.NPC npc = system.CreateNPC(RelationshipSystem.NPCType.Friend, data);
            float before = npc.relationshipScore;
            system.ObservePlayerAction(new RelationshipSystem.PlayerAction
            {
                type = RelationshipSystem.ActionType.BoughtExpensiveItem,
                memorability = 6,
                timestamp = TimeEnergySystem.Instance.GetCurrentTime()
            });

            Assert.Less(npc.relationshipScore, before);
        }

        [Test]
        public void ObservePlayerAction_MissedEvent_HighLoyalty_LargePenalty()
        {
            var data = CreateBasicData("Pat");
            data.values = new Dictionary<RelationshipSystem.NPCValue, float>
            {
                { RelationshipSystem.NPCValue.Loyalty, 1f },
                { RelationshipSystem.NPCValue.Family, 1f }
            };
            RelationshipSystem.NPC npc = system.CreateNPC(RelationshipSystem.NPCType.Family, data);
            float before = npc.relationshipScore;
            system.ObservePlayerAction(new RelationshipSystem.PlayerAction
            {
                type = RelationshipSystem.ActionType.MissedEvent,
                memorability = 6,
                timestamp = TimeEnergySystem.Instance.GetCurrentTime()
            });

            Assert.Less(npc.relationshipScore, before - 10f);
        }
        [Test]
        public void PatternDetection_RepeatAction_IncrementsCount()
        {
            RelationshipSystem.NPC npc = system.CreateNPC(RelationshipSystem.NPCType.Friend, CreateBasicData("Pat"));
            for (int i = 0; i < 2; i++)
            {
                system.ObservePlayerAction(new RelationshipSystem.PlayerAction
                {
                    type = RelationshipSystem.ActionType.MissedEvent,
                    memorability = 6,
                    timestamp = TimeEnergySystem.Instance.GetCurrentTime()
                });
            }

            var pattern = GetPatternData(npc, "missed_events");
            Assert.AreEqual(2, pattern.count);
        }

        [Test]
        public void PatternDetection_ThreeOccurrences_BecomesMemorableAt()
        {
            RelationshipSystem.NPC npc = system.CreateNPC(RelationshipSystem.NPCType.Friend, CreateBasicData("Pat"));
            for (int i = 0; i < 3; i++)
            {
                system.ObservePlayerAction(new RelationshipSystem.PlayerAction
                {
                    type = RelationshipSystem.ActionType.MissedEvent,
                    memorability = 6,
                    timestamp = TimeEnergySystem.Instance.GetCurrentTime()
                });
            }

            var pattern = GetPatternData(npc, "missed_events");
            Assert.GreaterOrEqual(pattern.memorability, 8);
        }

        [Test]
        public void PatternDetection_DifferentPatterns_TrackedIndependently()
        {
            RelationshipSystem.NPC npc = system.CreateNPC(RelationshipSystem.NPCType.Friend, CreateBasicData("Pat"));
            system.ObservePlayerAction(new RelationshipSystem.PlayerAction
            {
                type = RelationshipSystem.ActionType.MissedEvent,
                memorability = 6,
                timestamp = TimeEnergySystem.Instance.GetCurrentTime()
            });
            system.ObservePlayerAction(new RelationshipSystem.PlayerAction
            {
                type = RelationshipSystem.ActionType.WorkedOvertime,
                memorability = 6,
                timestamp = TimeEnergySystem.Instance.GetCurrentTime()
            });

            Assert.AreEqual(1, GetPatternData(npc, "missed_events").count);
            Assert.AreEqual(1, GetPatternData(npc, "overworking").count);
        }

        [Test]
        public void PatternDetection_RecordsFirstAndLastOccurrence()
        {
            RelationshipSystem.NPC npc = system.CreateNPC(RelationshipSystem.NPCType.Friend, CreateBasicData("Pat"));
            DateTime first = TimeEnergySystem.Instance.GetCurrentTime();
            system.ObservePlayerAction(new RelationshipSystem.PlayerAction
            {
                type = RelationshipSystem.ActionType.MissedEvent,
                memorability = 6,
                timestamp = first
            });

            DateTime later = first.AddHours(2);
            system.ObservePlayerAction(new RelationshipSystem.PlayerAction
            {
                type = RelationshipSystem.ActionType.MissedEvent,
                memorability = 6,
                timestamp = later
            });

            var pattern = GetPatternData(npc, "missed_events");
            Assert.AreEqual(first, pattern.firstOccurrence);
            Assert.AreEqual(later, pattern.lastOccurrence);
        }

        [Test]
        public void PatternDetection_MemorabilityIncreasesWithCount()
        {
            RelationshipSystem.NPC npc = system.CreateNPC(RelationshipSystem.NPCType.Friend, CreateBasicData("Pat"));
            for (int i = 0; i < 5; i++)
            {
                system.ObservePlayerAction(new RelationshipSystem.PlayerAction
                {
                    type = RelationshipSystem.ActionType.MissedEvent,
                    memorability = 6,
                    timestamp = TimeEnergySystem.Instance.GetCurrentTime()
                });
            }

            var pattern = GetPatternData(npc, "missed_events");
            Assert.Greater(pattern.memorability, 5);
        }

        [Test]
        public void CheckRelationshipThreshold_AllComparisons_WorkCorrectly()
        {
            RelationshipSystem.NPC npc = system.CreateNPC(RelationshipSystem.NPCType.Friend, CreateBasicData("Pat"));
            Assert.IsTrue(system.CheckRelationshipThreshold(npc.id, 40f, RelationshipSystem.ThresholdComparison.GreaterThan));
            Assert.IsFalse(system.CheckRelationshipThreshold(npc.id, 60f, RelationshipSystem.ThresholdComparison.GreaterThan));
            Assert.IsTrue(system.CheckRelationshipThreshold(npc.id, 60f, RelationshipSystem.ThresholdComparison.LessThan));
            Assert.IsTrue(system.CheckRelationshipThreshold(npc.id, 50f, RelationshipSystem.ThresholdComparison.EqualOrGreater));
            Assert.IsTrue(system.CheckRelationshipThreshold(npc.id, 50f, RelationshipSystem.ThresholdComparison.EqualOrLess));
        }

        [Test]
        public void CheckRelationshipThreshold_GreaterThan_ReturnsCorrectly()
        {
            RelationshipSystem.NPC npc = system.CreateNPC(RelationshipSystem.NPCType.Friend, CreateBasicData("Pat"));
            Assert.IsTrue(system.CheckRelationshipThreshold(npc.id, 40f, RelationshipSystem.ThresholdComparison.GreaterThan));
        }

        [Test]
        public void CheckRelationshipThreshold_EqualOrGreater_HandlesEquality()
        {
            RelationshipSystem.NPC npc = system.CreateNPC(RelationshipSystem.NPCType.Friend, CreateBasicData("Pat"));
            Assert.IsTrue(system.CheckRelationshipThreshold(npc.id, 50f, RelationshipSystem.ThresholdComparison.EqualOrGreater));
        }

        [Test]
        public void CheckRelationshipThreshold_InvalidNPC_ReturnsFalse()
        {
            Assert.IsFalse(system.CheckRelationshipThreshold("missing", 50f, RelationshipSystem.ThresholdComparison.EqualOrGreater));
        }

        [Test]
        public void OnRelationshipChanged_WhenModified_FiresWithCorrectParameters()
        {
            RelationshipSystem.NPC npc = system.CreateNPC(RelationshipSystem.NPCType.Friend, CreateBasicData("Pat"));
            float oldScore = 0f;
            float newScore = 0f;
            system.OnRelationshipChanged += (id, oldVal, newVal) =>
            {
                if (id == npc.id)
                {
                    oldScore = oldVal;
                    newScore = newVal;
                }
            };

            system.ModifyRelationship(npc.id, 10f, "test");
            Assert.AreEqual(50f, oldScore, 0.01f);
            Assert.AreEqual(60f, newScore, 0.01f);
        }

        [Test]
        public void OnBreakup_WhenScoreDropsBelow20_Fires()
        {
            RelationshipSystem.NPC npc = system.CreateNPC(RelationshipSystem.NPCType.Friend, CreateBasicData("Pat"));
            bool fired = false;
            system.OnBreakup += id => fired = true;
            system.ModifyRelationship(npc.id, -40f, "test");
            Assert.IsTrue(fired, "OnBreakup should fire when score drops below 20");
        }

        [Test]
        public void OnReconciliation_WhenRecovering_Fires()
        {
            var npc = system.CreateNPC(RelationshipSystem.NPCType.Friend, CreateBasicData("Pat"));
            var fired = false;
            system.OnReconciliation += id => fired = true;
            system.ModifyRelationship(npc.id, -40f, "test");
            system.ModifyRelationship(npc.id, 20f, "recover");
            Assert.IsTrue(fired, "OnReconciliation should fire when recovering from broken");
        }

        [Test]
        public void TriggerEvent_FiresOnRelationshipEvent()
        {
            var npc = system.CreateNPC(RelationshipSystem.NPCType.Friend, CreateBasicData("Pat"));
            var captured = RelationshipSystem.RelationshipEventType.Birthday;
            system.OnRelationshipEvent += (id, evt) => captured = evt;
            system.TriggerEvent(npc.id, RelationshipSystem.RelationshipEventType.Anniversary);
            Assert.AreEqual(RelationshipSystem.RelationshipEventType.Anniversary, captured);
        }

        [Test]
        public void GetNPCDialogue_ReturnsMoodBasedText()
        {
            var npc = system.CreateNPC(RelationshipSystem.NPCType.Friend, CreateBasicData("Pat"));
            var dialogue = system.GetNPCDialogue(npc.id, "test");
            Assert.IsTrue(dialogue.Contains("Neutral"));
            system.ModifyRelationship(npc.id, 30f, "boost");
            var happyDialogue = system.GetNPCDialogue(npc.id, "test");
            Assert.IsTrue(happyDialogue.Contains("Happy"));
            system.ModifyRelationship(npc.id, -80f, "drop");
            var upsetDialogue = system.GetNPCDialogue(npc.id, "test");
            Assert.IsTrue(upsetDialogue.Contains("Upset"));
        }

        private static RelationshipSystem.NPCData CreateBasicData(string name)
        {
            return new RelationshipSystem.NPCData
            {
                name = name,
                personality = RelationshipSystem.NPCPersonality.Supportive,
                values = new Dictionary<RelationshipSystem.NPCValue, float>(),
                tolerances = new Dictionary<RelationshipSystem.NPCTolerance, RelationshipSystem.ToleranceLevel>(),
                sexualBoundary = RelationshipSystem.SexualBoundaryType.Monogamous
            };
        }

        private static RelationshipSystem.PatternData GetPatternData(RelationshipSystem.NPC npc, string key)
        {
            if (npc.detectedPatterns.TryGetValue(key, out RelationshipSystem.PatternData pattern))
            {
                return pattern;
            }

            return new RelationshipSystem.PatternData();
        }

        private static void InvokePrivateMethod(string methodName)
        {
            MethodInfo method = typeof(RelationshipSystem).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, $"Method {methodName} should exist");
            method.Invoke(RelationshipSystem.Instance, null);
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
