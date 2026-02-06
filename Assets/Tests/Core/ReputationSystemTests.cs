using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Core
{
    public class ReputationSystemTests
    {
        private GameObject _reputationGameObject;
        private GameObject _timeGameObject;
        private ReputationSystem _reputationSystem;

        [SetUp]
        public void SetUp()
        {
            ResetSingleton(typeof(ReputationSystem));
            ResetSingleton(typeof(TimeEnergySystem));

            _timeGameObject = new GameObject("TimeEnergySystem");
            _timeGameObject.AddComponent<TimeEnergySystem>();

            _reputationGameObject = new GameObject("ReputationSystem");
            _reputationSystem = _reputationGameObject.AddComponent<ReputationSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_reputationGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_reputationGameObject);
            }

            if (_timeGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_timeGameObject);
            }
        }

        [Test]
        public void GetReputation_NewPlayer_ReturnsDefaultValues()
        {
            string player = "player";
            Assert.AreEqual(80f, _reputationSystem.GetReputation(player, ReputationSystem.ReputationTrack.Legal), 0.001f);
            Assert.AreEqual(0f, _reputationSystem.GetReputation(player, ReputationSystem.ReputationTrack.Criminal), 0.001f);
            Assert.AreEqual(50f, _reputationSystem.GetReputation(player, ReputationSystem.ReputationTrack.Professional), 0.001f);
            Assert.AreEqual(50f, _reputationSystem.GetReputation(player, ReputationSystem.ReputationTrack.Social), 0.001f);
        }

        [Test]
        public void GetReputation_LegalTrack_Returns80ByDefault()
        {
            float rep = _reputationSystem.GetReputation("player", ReputationSystem.ReputationTrack.Legal);
            Assert.AreEqual(80f, rep, 0.001f);
        }

        [Test]
        public void GetReputation_CriminalTrack_Returns0ByDefault()
        {
            float rep = _reputationSystem.GetReputation("player", ReputationSystem.ReputationTrack.Criminal);
            Assert.AreEqual(0f, rep, 0.001f);
        }

        [Test]
        public void ModifyReputation_PositiveDelta_IncreasesScore()
        {
            _reputationSystem.ModifyReputation("player", ReputationSystem.ReputationTrack.Social, 10f, "test");
            float rep = _reputationSystem.GetReputation("player", ReputationSystem.ReputationTrack.Social);
            Assert.AreEqual(60f, rep, 0.001f);
        }

        [Test]
        public void ModifyReputation_NegativeDelta_DecreasesScore()
        {
            _reputationSystem.ModifyReputation("player", ReputationSystem.ReputationTrack.Professional, -10f, "test");
            float rep = _reputationSystem.GetReputation("player", ReputationSystem.ReputationTrack.Professional);
            Assert.AreEqual(40f, rep, 0.001f);
        }

        [Test]
        public void ModifyReputation_ExceedingMax_ClampsAt100()
        {
            _reputationSystem.ModifyReputation("player", ReputationSystem.ReputationTrack.Legal, 50f, "test");
            float rep = _reputationSystem.GetReputation("player", ReputationSystem.ReputationTrack.Legal);
            Assert.AreEqual(100f, rep, 0.001f);
        }

        [Test]
        public void ModifyReputation_GoingBelowZero_ClampsAt0()
        {
            _reputationSystem.ModifyReputation("player", ReputationSystem.ReputationTrack.Criminal, -10f, "test");
            float rep = _reputationSystem.GetReputation("player", ReputationSystem.ReputationTrack.Criminal);
            Assert.AreEqual(0f, rep, 0.001f);
        }

        [Test]
        public void ModifyReputation_RecordsInHistory()
        {
            string player = "player";
            _reputationSystem.ModifyReputation(player, ReputationSystem.ReputationTrack.Legal, -5f, "test");

            List<object> history = GetHistory(player);
            Assert.AreEqual(1, history.Count, "History should record a change");
        }

        [Test]
        public void ModifyMultipleReputations_ChangesAllSpecifiedTracks()
        {
            string player = "player";
            Dictionary<ReputationSystem.ReputationTrack, float> changes = new Dictionary<ReputationSystem.ReputationTrack, float>
            {
                { ReputationSystem.ReputationTrack.Legal, -20f },
                { ReputationSystem.ReputationTrack.Criminal, 10f },
                { ReputationSystem.ReputationTrack.Professional, -15f },
                { ReputationSystem.ReputationTrack.Social, -10f }
            };

            _reputationSystem.ModifyMultipleReputations(player, changes, "arrest");

            Assert.AreEqual(60f, _reputationSystem.GetReputation(player, ReputationSystem.ReputationTrack.Legal), 0.01f);
            Assert.AreEqual(10f, _reputationSystem.GetReputation(player, ReputationSystem.ReputationTrack.Criminal), 0.01f);
            Assert.AreEqual(35f, _reputationSystem.GetReputation(player, ReputationSystem.ReputationTrack.Professional), 0.01f);
            Assert.AreEqual(40f, _reputationSystem.GetReputation(player, ReputationSystem.ReputationTrack.Social), 0.01f);
        }

        [Test]
        public void ModifyMultipleReputations_FiresEventForEachTrack()
        {
            int firedCount = 0;
            _reputationSystem.OnReputationChanged += (player, track, oldValue, newValue) => firedCount++;

            Dictionary<ReputationSystem.ReputationTrack, float> changes = new Dictionary<ReputationSystem.ReputationTrack, float>
            {
                { ReputationSystem.ReputationTrack.Legal, -5f },
                { ReputationSystem.ReputationTrack.Criminal, 5f }
            };

            _reputationSystem.ModifyMultipleReputations("player", changes, "multi");
            Assert.AreEqual(2, firedCount, "Should fire for each track");
        }

        [Test]
        public void ModifyMultipleReputations_ExampleArrest_AppliesCorrectChanges()
        {
            string player = "player";
            Dictionary<ReputationSystem.ReputationTrack, float> changes = new Dictionary<ReputationSystem.ReputationTrack, float>
            {
                { ReputationSystem.ReputationTrack.Legal, -20f },
                { ReputationSystem.ReputationTrack.Criminal, 10f },
                { ReputationSystem.ReputationTrack.Professional, -15f },
                { ReputationSystem.ReputationTrack.Social, -10f }
            };

            _reputationSystem.ModifyMultipleReputations(player, changes, "arrested");

            Assert.AreEqual(60f, _reputationSystem.GetReputation(player, ReputationSystem.ReputationTrack.Legal), 0.01f);
            Assert.AreEqual(10f, _reputationSystem.GetReputation(player, ReputationSystem.ReputationTrack.Criminal), 0.01f);
            Assert.AreEqual(35f, _reputationSystem.GetReputation(player, ReputationSystem.ReputationTrack.Professional), 0.01f);
            Assert.AreEqual(40f, _reputationSystem.GetReputation(player, ReputationSystem.ReputationTrack.Social), 0.01f);
        }

        [Test]
        public void CheckThreshold_GreaterThan_ReturnsTrueWhenAbove()
        {
            bool result = _reputationSystem.CheckThreshold("player", ReputationSystem.ReputationTrack.Legal, 70f, ReputationSystem.ThresholdComparison.GreaterThan);
            Assert.IsTrue(result, "Legal reputation should be above 70 by default");
        }

        [Test]
        public void CheckThreshold_LessThan_ReturnsTrueWhenBelow()
        {
            bool result = _reputationSystem.CheckThreshold("player", ReputationSystem.ReputationTrack.Criminal, 10f, ReputationSystem.ThresholdComparison.LessThan);
            Assert.IsTrue(result, "Criminal reputation should be below 10 by default");
        }

        [Test]
        public void CheckThreshold_EqualOrGreater_ReturnsTrueAtThreshold()
        {
            _reputationSystem.ModifyReputation("player", ReputationSystem.ReputationTrack.Professional, -10f, "test");
            bool result = _reputationSystem.CheckThreshold("player", ReputationSystem.ReputationTrack.Professional, 40f, ReputationSystem.ThresholdComparison.EqualOrGreater);
            Assert.IsTrue(result, "EqualOrGreater should succeed at threshold");
        }

        [Test]
        public void CheckThreshold_EqualOrLess_ReturnsTrueAtThreshold()
        {
            bool result = _reputationSystem.CheckThreshold("player", ReputationSystem.ReputationTrack.Legal, 80f, ReputationSystem.ThresholdComparison.EqualOrLess);
            Assert.IsTrue(result, "EqualOrLess should succeed at threshold");
        }

        [Test]
        public void CheckThreshold_ExactlyAtValue_HandledCorrectly()
        {
            bool greater = _reputationSystem.CheckThreshold("player", ReputationSystem.ReputationTrack.Legal, 80f, ReputationSystem.ThresholdComparison.GreaterThan);
            bool equalGreater = _reputationSystem.CheckThreshold("player", ReputationSystem.ReputationTrack.Legal, 80f, ReputationSystem.ThresholdComparison.EqualOrGreater);
            bool less = _reputationSystem.CheckThreshold("player", ReputationSystem.ReputationTrack.Legal, 80f, ReputationSystem.ThresholdComparison.LessThan);
            bool equalLess = _reputationSystem.CheckThreshold("player", ReputationSystem.ReputationTrack.Legal, 80f, ReputationSystem.ThresholdComparison.EqualOrLess);

            Assert.IsFalse(greater, "GreaterThan should be false at threshold");
            Assert.IsTrue(equalGreater, "EqualOrGreater should be true at threshold");
            Assert.IsFalse(less, "LessThan should be false at threshold");
            Assert.IsTrue(equalLess, "EqualOrLess should be true at threshold");
        }

        [Test]
        public void AddTemporaryModifier_AppliesImmediately()
        {
            string player = "player";
            float before = _reputationSystem.GetReputation(player, ReputationSystem.ReputationTrack.Legal);
            _reputationSystem.AddTemporaryModifier(player, ReputationSystem.ReputationTrack.Legal, 5f, 1f, "mod");
            float after = _reputationSystem.GetReputation(player, ReputationSystem.ReputationTrack.Legal);
            Assert.AreEqual(before + 5f, after, 0.01f, "Modifier should apply immediately");
        }

        [Test]
        public void AddTemporaryModifier_ReturnsModifierId()
        {
            string id = _reputationSystem.AddTemporaryModifier("player", ReputationSystem.ReputationTrack.Legal, 5f, 1f, "mod");
            Assert.IsFalse(string.IsNullOrEmpty(id), "Modifier id should be returned");
        }

        [Test]
        public void GetReputation_WithModifiers_ReturnsBaseAndModifiers()
        {
            string player = "player";
            _reputationSystem.AddTemporaryModifier(player, ReputationSystem.ReputationTrack.Professional, 10f, 1f, "mod");
            float rep = _reputationSystem.GetReputation(player, ReputationSystem.ReputationTrack.Professional);
            Assert.AreEqual(60f, rep, 0.01f, "Base plus modifier should be returned");
        }

        [Test]
        public void GetReputation_MultipleModifiers_SumsAll()
        {
            string player = "player";
            _reputationSystem.AddTemporaryModifier(player, ReputationSystem.ReputationTrack.Legal, 5f, 1f, "mod1");
            _reputationSystem.AddTemporaryModifier(player, ReputationSystem.ReputationTrack.Legal, 10f, 1f, "mod2");
            _reputationSystem.AddTemporaryModifier(player, ReputationSystem.ReputationTrack.Legal, -3f, 1f, "mod3");
            float rep = _reputationSystem.GetReputation(player, ReputationSystem.ReputationTrack.Legal);
            Assert.AreEqual(92f, rep, 0.01f, "Modifiers should sum");
        }

        [Test]
        public void GetReputationModifiers_ReturnsOnlyForSpecifiedTrack()
        {
            string player = "player";
            _reputationSystem.AddTemporaryModifier(player, ReputationSystem.ReputationTrack.Legal, 5f, 1f, "legal_mod");
            _reputationSystem.AddTemporaryModifier(player, ReputationSystem.ReputationTrack.Social, 5f, 1f, "social_mod");

            Dictionary<string, float> legalMods = _reputationSystem.GetReputationModifiers(player, ReputationSystem.ReputationTrack.Legal);
            Assert.IsTrue(legalMods.ContainsKey("legal_mod"), "Legal modifier should be returned");
            Assert.IsFalse(legalMods.ContainsKey("social_mod"), "Other track modifier should not be returned");
        }

        [Test]
        public void RemoveModifier_BeforeExpiration_RemovesModifier()
        {
            string player = "player";
            string id = _reputationSystem.AddTemporaryModifier(player, ReputationSystem.ReputationTrack.Legal, 5f, 1f, "mod");
            bool removed = _reputationSystem.RemoveModifier(player, id);
            float rep = _reputationSystem.GetReputation(player, ReputationSystem.ReputationTrack.Legal);
            Assert.IsTrue(removed, "RemoveModifier should return true");
            Assert.AreEqual(80f, rep, 0.01f, "Modifier should be removed");
        }

        [Test]
        public void RemoveModifier_InvalidId_ReturnsFalse()
        {
            bool result = _reputationSystem.RemoveModifier("player", "missing");
            Assert.IsFalse(result, "Removing invalid modifier should return false");
        }

        [Test]
        public void AddTemporaryModifier_ZeroDuration_TreatsAsPermanent()
        {
            string player = "player";
            string id = _reputationSystem.AddTemporaryModifier(player, ReputationSystem.ReputationTrack.Legal, 5f, 0f, "perm");
            TimeEnergySystem.Instance.AdvanceTime(24 * 60 + 1);
            InvokePrivateMethod("CheckExpiredModifiers");
            float rep = _reputationSystem.GetReputation(player, ReputationSystem.ReputationTrack.Legal);
            Assert.AreEqual(85f, rep, 0.01f, "Zero duration should be permanent");
            Assert.IsTrue(IsModifierActive(player, id), "Permanent modifier should remain active");
        }

        [Test]
        public void Modifier_ExpiresAtCorrectTime()
        {
            string player = "player";
            _reputationSystem.AddTemporaryModifier(player, ReputationSystem.ReputationTrack.Legal, 10f, 1f, "mod");
            float before = _reputationSystem.GetReputation(player, ReputationSystem.ReputationTrack.Legal);
            TimeEnergySystem.Instance.AdvanceTime(24 * 60 + 1);
            InvokePrivateMethod("CheckExpiredModifiers");
            float after = _reputationSystem.GetReputation(player, ReputationSystem.ReputationTrack.Legal);
            Assert.Less(after, before, "Modifier should expire after duration");
        }

        [Test]
        public void Modifier_Expiration_FiresOnReputationChanged()
        {
            string player = "player";
            bool fired = false;
            _reputationSystem.OnReputationChanged += (pid, track, oldValue, newValue) =>
            {
                if (pid == player && track == ReputationSystem.ReputationTrack.Legal && newValue < oldValue)
                {
                    fired = true;
                }
            };

            _reputationSystem.AddTemporaryModifier(player, ReputationSystem.ReputationTrack.Legal, 10f, 1f, "mod");
            TimeEnergySystem.Instance.AdvanceTime(24 * 60 + 1);
            InvokePrivateMethod("CheckExpiredModifiers");
            Assert.IsTrue(fired, "Expiration should fire reputation changed event");
        }

        [Test]
        public void Modifier_Permanent_NeverExpires()
        {
            string player = "player";
            _reputationSystem.AddTemporaryModifier(player, ReputationSystem.ReputationTrack.Legal, 10f, 0f, "perm");
            TimeEnergySystem.Instance.AdvanceTime(365 * 24 * 60);
            InvokePrivateMethod("CheckExpiredModifiers");
            float rep = _reputationSystem.GetReputation(player, ReputationSystem.ReputationTrack.Legal);
            Assert.AreEqual(90f, rep, 0.01f, "Permanent modifier should not expire");
        }

        [Test]
        public void MultipleModifiers_ExpireIndependently()
        {
            string player = "player";
            _reputationSystem.AddTemporaryModifier(player, ReputationSystem.ReputationTrack.Legal, 5f, 1f, "mod1");
            _reputationSystem.AddTemporaryModifier(player, ReputationSystem.ReputationTrack.Legal, 10f, 2f, "mod2");

            TimeEnergySystem.Instance.AdvanceTime(24 * 60 + 1);
            InvokePrivateMethod("CheckExpiredModifiers");
            float repAfterDay1 = _reputationSystem.GetReputation(player, ReputationSystem.ReputationTrack.Legal);

            TimeEnergySystem.Instance.AdvanceTime(24 * 60 + 1);
            InvokePrivateMethod("CheckExpiredModifiers");
            float repAfterDay2 = _reputationSystem.GetReputation(player, ReputationSystem.ReputationTrack.Legal);

            Assert.AreEqual(90f, repAfterDay1, 0.01f, "One modifier should expire after day 1");
            Assert.AreEqual(80f, repAfterDay2, 0.01f, "Second modifier should expire after day 2");
        }

        [Test]
        public void OnThresholdCrossed_CrossingUp_FiresAtThreshold()
        {
            string player = "player";
            float captured = -1f;
            _reputationSystem.OnThresholdCrossed += (pid, track, threshold) =>
            {
                if (pid == player && track == ReputationSystem.ReputationTrack.Criminal)
                {
                    captured = threshold;
                }
            };

            _reputationSystem.ModifyReputation(player, ReputationSystem.ReputationTrack.Criminal, 25f, "crime");
            Assert.AreEqual(20f, captured, 0.01f, "Should cross 20 threshold when moving up");
        }

        [Test]
        public void OnThresholdCrossed_CrossingDown_FiresAtThreshold()
        {
            string player = "player";
            List<float> thresholds = new List<float>();
            _reputationSystem.OnThresholdCrossed += (pid, track, threshold) =>
            {
                if (pid == player && track == ReputationSystem.ReputationTrack.Legal)
                {
                    thresholds.Add(threshold);
                }
            };

            _reputationSystem.ModifyReputation(player, ReputationSystem.ReputationTrack.Legal, -15f, "drop");
            Assert.IsTrue(thresholds.Contains(70f), "Should cross 70 when moving down");
        }

        [Test]
        public void OnThresholdCrossed_AllSignificantThresholds_Detected()
        {
            string player = "player";
            List<float> thresholds = new List<float>();
            _reputationSystem.OnThresholdCrossed += (pid, track, threshold) =>
            {
                if (pid == player && track == ReputationSystem.ReputationTrack.Legal)
                {
                    thresholds.Add(threshold);
                }
            };

            _reputationSystem.ModifyReputation(player, ReputationSystem.ReputationTrack.Legal, -80f, "drop");

            Assert.IsTrue(thresholds.Contains(70f), "Should cross 70");
            Assert.IsTrue(thresholds.Contains(50f), "Should cross 50");
            Assert.IsTrue(thresholds.Contains(30f), "Should cross 30");
            Assert.IsTrue(thresholds.Contains(20f), "Should cross 20");
        }

        [Test]
        public void OnThresholdCrossed_NotCrossing_DoesNotFire()
        {
            bool fired = false;
            _reputationSystem.OnThresholdCrossed += (pid, track, threshold) => fired = true;
            _reputationSystem.ModifyReputation("player", ReputationSystem.ReputationTrack.Social, 5f, "small");
            Assert.IsFalse(fired, "Should not fire when no threshold crossed");
        }

        [Test]
        public void OnThresholdCrossed_MultipleThresholds_FiresForEach()
        {
            string player = "player";
            List<float> thresholds = new List<float>();
            _reputationSystem.OnThresholdCrossed += (pid, track, threshold) =>
            {
                if (pid == player && track == ReputationSystem.ReputationTrack.Legal)
                {
                    thresholds.Add(threshold);
                }
            };

            _reputationSystem.ModifyReputation(player, ReputationSystem.ReputationTrack.Legal, -40f, "drop");

            Assert.IsTrue(thresholds.Contains(70f), "Should cross 70");
            Assert.IsTrue(thresholds.Contains(50f), "Should cross 50");
        }

        [Test]
        public void OnReputationChanged_WhenModified_FiresWithCorrectParameters()
        {
            string player = "player";
            ReputationSystem.ReputationTrack capturedTrack = ReputationSystem.ReputationTrack.Legal;
            float oldValue = -1f;
            float newValue = -1f;

            _reputationSystem.OnReputationChanged += (pid, track, oldVal, newVal) =>
            {
                if (pid == player && track == ReputationSystem.ReputationTrack.Professional)
                {
                    capturedTrack = track;
                    oldValue = oldVal;
                    newValue = newVal;
                }
            };

            _reputationSystem.ModifyReputation(player, ReputationSystem.ReputationTrack.Professional, 10f, "bonus");

            Assert.AreEqual(ReputationSystem.ReputationTrack.Professional, capturedTrack);
            Assert.AreEqual(50f, oldValue, 0.01f);
            Assert.AreEqual(60f, newValue, 0.01f);
        }

        [Test]
        public void OnReputationChanged_WhenModifierExpires_FiresWithCorrectValues()
        {
            string player = "player";
            float capturedOld = 0f;
            float capturedNew = 0f;
            _reputationSystem.OnReputationChanged += (pid, track, oldVal, newVal) =>
            {
                if (pid == player && track == ReputationSystem.ReputationTrack.Legal && newVal < oldVal)
                {
                    capturedOld = oldVal;
                    capturedNew = newVal;
                }
            };

            _reputationSystem.AddTemporaryModifier(player, ReputationSystem.ReputationTrack.Legal, 10f, 1f, "mod");
            TimeEnergySystem.Instance.AdvanceTime(24 * 60 + 1);
            InvokePrivateMethod("CheckExpiredModifiers");

            Assert.AreEqual(90f, capturedOld, 0.01f);
            Assert.AreEqual(80f, capturedNew, 0.01f);
        }

        [Test]
        public void AddTemporaryModifier_ClampsReputationWhenAbove100()
        {
            string player = "player";
            _reputationSystem.AddTemporaryModifier(player, ReputationSystem.ReputationTrack.Legal, 50f, 1f, "mod");
            float rep = _reputationSystem.GetReputation(player, ReputationSystem.ReputationTrack.Legal);
            Assert.AreEqual(100f, rep, 0.01f, "Reputation should clamp at 100");
        }

        [Test]
        public void AddTemporaryModifier_ClampsReputationWhenBelow0()
        {
            string player = "player";
            _reputationSystem.AddTemporaryModifier(player, ReputationSystem.ReputationTrack.Criminal, -50f, 1f, "mod");
            float rep = _reputationSystem.GetReputation(player, ReputationSystem.ReputationTrack.Criminal);
            Assert.AreEqual(0f, rep, 0.01f, "Reputation should clamp at 0");
        }

        private static void ResetSingleton(Type systemType)
        {
            FieldInfo field = systemType.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(null, null);
            }
        }

        private static void InvokePrivateMethod(string methodName)
        {
            MethodInfo method = typeof(ReputationSystem).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, $"Method {methodName} should exist");
            ReputationSystem instance = ReputationSystem.Instance;
            method.Invoke(instance, null);
        }

        private static bool IsModifierActive(string playerId, string modifierId)
        {
            object profileObj = GetProfileObject(playerId);
            if (profileObj == null)
            {
                return false;
            }

            FieldInfo modifiersField = profileObj.GetType().GetField("activeModifiers", BindingFlags.Public | BindingFlags.Instance);
            if (modifiersField == null)
            {
                modifiersField = profileObj.GetType().GetField("activeModifiers", BindingFlags.NonPublic | BindingFlags.Instance);
            }

            if (modifiersField == null)
            {
                return false;
            }

            object modifiersValue = modifiersField.GetValue(profileObj);
            if (modifiersValue is System.Collections.IDictionary dict)
            {
                return dict.Contains(modifierId);
            }

            return false;
        }

        private static List<object> GetHistory(string playerId)
        {
            object profileObj = GetProfileObject(playerId);
            if (profileObj == null)
            {
                return new List<object>();
            }

            FieldInfo historyField = profileObj.GetType().GetField("history", BindingFlags.Public | BindingFlags.Instance);
            if (historyField != null)
            {
                object historyObj = historyField.GetValue(profileObj);
                if (historyObj is System.Collections.IEnumerable enumerable)
                {
                    return enumerable.Cast<object>().ToList();
                }
            }

            return new List<object>();
        }

        private static object GetProfileObject(string playerId)
        {
            FieldInfo profilesField = typeof(ReputationSystem).GetField("profiles", BindingFlags.NonPublic | BindingFlags.Instance);
            ReputationSystem instance = ReputationSystem.Instance;
            object profilesObj = profilesField.GetValue(instance);
            if (profilesObj is System.Collections.IDictionary dict)
            {
                if (dict.Contains(playerId))
                {
                    return dict[playerId];
                }
            }

            return null;
        }
    }
}
