using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Core;

namespace Tests.Core
{
    public class IntoxicationSystemTests
    {
        private GameObject intoxicationGameObject;
        private GameObject timeGameObject;
        private GameObject economyGameObject;
        private GameObject reputationGameObject;
        private IntoxicationSystem system;

        [SetUp]
        public void SetUp()
        {
            ResetSingleton(typeof(IntoxicationSystem));
            ResetSingleton(typeof(TimeEnergySystem));
            ResetSingleton(typeof(EconomySystem));
            ResetSingleton(typeof(ReputationSystem));

            timeGameObject = new GameObject("TimeEnergySystem");
            timeGameObject.AddComponent<TimeEnergySystem>();

            economyGameObject = new GameObject("EconomySystem");
            economyGameObject.AddComponent<EconomySystem>();

            reputationGameObject = new GameObject("ReputationSystem");
            reputationGameObject.AddComponent<ReputationSystem>();

            intoxicationGameObject = new GameObject("IntoxicationSystem");
            system = intoxicationGameObject.AddComponent<IntoxicationSystem>();

            system.CreateConsumableItem(new IntoxicationSystem.ConsumableItem
            {
                id = "beer",
                type = IntoxicationSystem.IntoxicationType.Alcohol,
                intoxicationIncrease = 0.05f,
                duration = 2f
            });
            system.CreateConsumableItem(new IntoxicationSystem.ConsumableItem
            {
                id = "wine",
                type = IntoxicationSystem.IntoxicationType.Alcohol,
                intoxicationIncrease = 0.08f,
                duration = 3f
            });
            system.CreateConsumableItem(new IntoxicationSystem.ConsumableItem
            {
                id = "spirits",
                type = IntoxicationSystem.IntoxicationType.Alcohol,
                intoxicationIncrease = 0.15f,
                duration = 4f
            });
        }

        [TearDown]
        public void TearDown()
        {
            if (intoxicationGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(intoxicationGameObject);
            }

            if (reputationGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(reputationGameObject);
            }

            if (economyGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(economyGameObject);
            }

            if (timeGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(timeGameObject);
            }
        }

        [Test]
        public void GetIntoxicationLevel_InitialZero()
        {
            Assert.AreEqual(0f, system.GetIntoxicationLevel(), 0.001f, "Initial level should be 0");
        }

        [Test]
        public void ModifyIntoxication_IncreasesLevel()
        {
            system.ModifyIntoxication(0.1f, IntoxicationSystem.IntoxicationType.Alcohol);
            Assert.AreEqual(0.1f, system.GetIntoxicationLevel(), 0.001f, "Level should increase");
        }

        [Test]
        public void ModifyIntoxication_ClampsAt0And1()
        {
            system.ModifyIntoxication(2f, IntoxicationSystem.IntoxicationType.Alcohol);
            Assert.AreEqual(1f, system.GetIntoxicationLevel(), 0.001f, "Clamp max 1");
            system.ModifyIntoxication(-2f, IntoxicationSystem.IntoxicationType.Alcohol);
            Assert.AreEqual(0f, system.GetIntoxicationLevel(), 0.001f, "Clamp min 0");
        }

        [Test]
        public void Consume_IncreasesLevel()
        {
            system.Consume("beer", 1f);
            Assert.AreEqual(0.05f, system.GetIntoxicationLevel(), 0.001f, "Beer should increase level");
        }

        [Test]
        public void Consume_Stacks()
        {
            system.Consume("beer", 2f);
            Assert.AreEqual(0.1f, system.GetIntoxicationLevel(), 0.001f, "Stacked consumption should increase");
        }

        [Test]
        public void Consume_MixingSubstances_TracksByType()
        {
            system.CreateConsumableItem(new IntoxicationSystem.ConsumableItem
            {
                id = "weed",
                type = IntoxicationSystem.IntoxicationType.Cannabis,
                intoxicationIncrease = 0.06f,
                duration = 2f
            });
            system.Consume("beer", 1f);
            system.Consume("weed", 1f);

            Dictionary<IntoxicationSystem.IntoxicationType, float> byType = GetByType();
            Assert.IsTrue(byType.ContainsKey(IntoxicationSystem.IntoxicationType.Alcohol), "Alcohol should be tracked");
            Assert.IsTrue(byType.ContainsKey(IntoxicationSystem.IntoxicationType.Cannabis), "Cannabis should be tracked");
        }

        [Test]
        public void Consume_BlackoutAtHighLevel()
        {
            bool blackout = false;
            system.OnBlackout += () => blackout = true;
            system.Consume("spirits", 6f);
            Assert.IsTrue(blackout, "Blackout should trigger at high level");
        }

        [Test]
        public void OnConsumed_Fires()
        {
            bool fired = false;
            system.OnConsumed += type => fired = true;
            system.Consume("beer", 1f);
            Assert.IsTrue(fired, "OnConsumed should fire");
        }

        [Test]
        public void OnIntoxicationChanged_Fires()
        {
            bool fired = false;
            system.OnIntoxicationChanged += level => fired = true;
            system.Consume("beer", 1f);
            Assert.IsTrue(fired, "OnIntoxicationChanged should fire");
        }

        [Test]
        public void Metabolism_DecaysOverTime()
        {
            system.SetIntoxicationLevelForTesting(0.2f);
            system.ProcessMetabolismForTesting(1f);
            Assert.AreEqual(0.18f, system.GetIntoxicationLevel(), 0.001f, "Should decay 0.02 per hour");
        }

        [Test]
        public void Metabolism_ClampsAtZero()
        {
            system.SetIntoxicationLevelForTesting(0.01f);
            system.ProcessMetabolismForTesting(1f);
            Assert.AreEqual(0f, system.GetIntoxicationLevel(), 0.001f, "Should not go below 0");
        }

        [Test]
        public void OnSobrietyAchieved_Fires()
        {
            bool fired = false;
            system.SetIntoxicationLevelForTesting(0.01f);
            system.OnSobrietyAchieved += () => fired = true;
            system.ProcessMetabolismForTesting(1f);
            Assert.IsTrue(fired, "Sobriety event should fire");
        }

        [Test]
        public void GetImpairment_Driving_Calculation()
        {
            system.SetIntoxicationLevelForTesting(0.2f);
            float impairment = system.GetImpairmentLevel(IntoxicationSystem.ImpairmentType.Driving);
            Assert.AreEqual(0.7f, impairment, 0.01f, "Driving impairment formula should apply");
        }

        [Test]
        public void GetImpairment_Coordination_Calculation()
        {
            system.SetIntoxicationLevelForTesting(0.2f);
            float impairment = system.GetImpairmentLevel(IntoxicationSystem.ImpairmentType.Coordination);
            Assert.AreEqual(0.8f, impairment, 0.01f, "Coordination impairment should be linear");
        }

        [Test]
        public void GetImpairment_Judgment_Calculation()
        {
            system.SetIntoxicationLevelForTesting(0.2f);
            float impairment = system.GetImpairmentLevel(IntoxicationSystem.ImpairmentType.Judgment);
            Assert.AreEqual(0.76f, impairment, 0.01f, "Judgment impairment should be 1 - intox*1.2");
        }

        [Test]
        public void GetImpairment_Perception_Calculation()
        {
            system.SetIntoxicationLevelForTesting(0.2f);
            float impairment = system.GetImpairmentLevel(IntoxicationSystem.ImpairmentType.Perception);
            Assert.AreEqual(0.8f, impairment, 0.01f, "Perception impairment should be linear");
        }

        [Test]
        public void CheckDUI_UnderLimit_ReturnsFalse()
        {
            system.SetIntoxicationLevelForTesting(0.05f);
            system.SetDrivingStatusForTesting(true);
            bool result = system.CheckDUI("cop1");
            Assert.IsFalse(result, "Under limit should not trigger DUI");
        }

        [Test]
        public void CheckDUI_NotDriving_ReturnsFalse()
        {
            system.SetIntoxicationLevelForTesting(0.2f);
            system.SetDrivingStatusForTesting(false);
            bool result = system.CheckDUI("cop1");
            Assert.IsFalse(result, "Not driving should not trigger DUI");
        }

        [Test]
        public void CheckDUI_OverLimit_CanCatch()
        {
            system.SetIntoxicationLevelForTesting(0.2f);
            system.SetDrivingStatusForTesting(true);
            system.SetDuiCatchResultForTesting(true);
            bool result = system.CheckDUI("cop1");
            Assert.IsTrue(result, "Over limit should trigger DUI when caught");
        }

        [Test]
        public void CheckDUI_FiresOnDUIArrest()
        {
            bool fired = false;
            system.OnDUIArrest += id => fired = true;
            system.SetIntoxicationLevelForTesting(0.2f);
            system.SetDrivingStatusForTesting(true);
            system.SetDuiCatchResultForTesting(true);
            system.CheckDUI("cop1");
            Assert.IsTrue(fired, "OnDUIArrest should fire");
        }

        [Test]
        public void DUI_TriggersFine()
        {
            EconomySystem.Instance.AddIncome("player", 6000f, EconomySystem.IncomeSource.Salary, "seed");
            float before = EconomySystem.Instance.GetBalance("player");

            system.SetIntoxicationLevelForTesting(0.2f);
            system.SetDrivingStatusForTesting(true);
            system.SetDuiCatchResultForTesting(true);
            system.CheckDUI("cop1");

            float after = EconomySystem.Instance.GetBalance("player");
            Assert.Less(after, before, "DUI should deduct fine");
        }

        [Test]
        public void DUI_UpdatesReputation()
        {
            float before = ReputationSystem.Instance.GetReputation("player", ReputationSystem.ReputationTrack.Legal);

            system.SetIntoxicationLevelForTesting(0.2f);
            system.SetDrivingStatusForTesting(true);
            system.SetDuiCatchResultForTesting(true);
            system.CheckDUI("cop1");

            float after = ReputationSystem.Instance.GetReputation("player", ReputationSystem.ReputationTrack.Legal);
            Assert.AreEqual(before - 15f, after, 0.01f, "DUI should reduce legal reputation");
        }

        [Test]
        public void DUI_SuspendsLicense()
        {
            system.SetIntoxicationLevelForTesting(0.2f);
            system.SetDrivingStatusForTesting(true);
            system.SetDuiCatchResultForTesting(true);
            system.CheckDUI("cop1");
            bool hasLicense = GetHasLicense();
            Assert.IsFalse(hasLicense, "DUI should suspend license");
        }

        [Test]
        public void DUI_AdvancesTimeByOneDay()
        {
            DateTime before = TimeEnergySystem.Instance.GetCurrentTime();
            system.SetIntoxicationLevelForTesting(0.2f);
            system.SetDrivingStatusForTesting(true);
            system.SetDuiCatchResultForTesting(true);
            system.CheckDUI("cop1");
            DateTime after = TimeEnergySystem.Instance.GetCurrentTime();
            Assert.Greater(after, before, "DUI should advance time");
        }

        [Test]
        public void ModifyIntoxication_CanTriggerBlackout()
        {
            bool blackout = false;
            system.OnBlackout += () => blackout = true;
            system.ModifyIntoxication(0.95f, IntoxicationSystem.IntoxicationType.Alcohol);
            Assert.IsTrue(blackout, "ModifyIntoxication should trigger blackout");
        }

        [Test]
        public void Consume_ClampsAtOne()
        {
            system.Consume("spirits", 10f);
            Assert.AreEqual(1f, system.GetIntoxicationLevel(), 0.001f, "Level should clamp at 1");
        }

        [Test]
        public void ModifyIntoxication_MaintainsPeakLevel()
        {
            system.ModifyIntoxication(0.5f, IntoxicationSystem.IntoxicationType.Alcohol);
            system.ModifyIntoxication(-0.2f, IntoxicationSystem.IntoxicationType.Alcohol);
            float peak = GetPeakLevel();
            Assert.AreEqual(0.5f, peak, 0.001f, "Peak level should be maintained");
        }

        private Dictionary<IntoxicationSystem.IntoxicationType, float> GetByType()
        {
            IntoxicationSystem.IntoxicationState state = GetState();
            return state.byType;
        }

        private bool GetHasLicense()
        {
            IntoxicationSystem.IntoxicationState state = GetState();
            return state.hasLicense;
        }

        private float GetPeakLevel()
        {
            IntoxicationSystem.IntoxicationState state = GetState();
            return state.peakLevel;
        }

        private IntoxicationSystem.IntoxicationState GetState()
        {
            FieldInfo field = typeof(IntoxicationSystem).GetField("intoxicationState", BindingFlags.Instance | BindingFlags.NonPublic);
            return (IntoxicationSystem.IntoxicationState)field.GetValue(system);
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
