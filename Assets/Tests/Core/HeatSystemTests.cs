using System;
using System.Collections.Generic;
using System.Reflection;
using Core;
using NUnit.Framework;
using UnityEngine;


namespace Tests.Core
{
    public class HeatSystemTests
    {
        private GameObject heatGameObject;
        private GameObject timeGameObject;
        private GameObject economyGameObject;
        private HeatSystem heatSystem;

        [SetUp]
        public void SetUp()
        {
            ResetSingleton(typeof(HeatSystem));
            ResetSingleton(typeof(TimeEnergySystem));
            ResetSingleton(typeof(EconomySystem));

            timeGameObject = new GameObject("TimeEnergySystem");
            timeGameObject.AddComponent<TimeEnergySystem>();

            economyGameObject = new GameObject("EconomySystem");
            economyGameObject.AddComponent<EconomySystem>();

            heatGameObject = new GameObject("HeatSystem");
            heatSystem = heatGameObject.AddComponent<HeatSystem>();
            heatSystem.SetPlayerIdForTesting("player");
        }

        [TearDown]
        public void TearDown()
        {
            if (heatGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(heatGameObject);
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
        public void GetHeatLevel_InitialZero()
        {
            Assert.AreEqual(0f, heatSystem.GetHeatLevel(), 0.001f, "Heat should start at 0");
        }

        [Test]
        public void AddHeat_IncreasesLevel()
        {
            heatSystem.AddHeat(10f, "test");
            Assert.AreEqual(10f, heatSystem.GetHeatLevel(), 0.001f, "Heat should increase");
        }

        [Test]
        public void AddHeat_ClampsTo100()
        {
            heatSystem.AddHeat(200f, "test");
            Assert.AreEqual(100f, heatSystem.GetHeatLevel(), 0.001f, "Heat should clamp to 100");
        }

        [Test]
        public void ReduceHeat_DecreasesLevel()
        {
            heatSystem.AddHeat(20f, "test");
            heatSystem.ReduceHeat(5f, "test");
            Assert.AreEqual(15f, heatSystem.GetHeatLevel(), 0.001f, "Heat should decrease");
        }

        [Test]
        public void ReduceHeat_ClampsTo0()
        {
            heatSystem.AddHeat(5f, "test");
            heatSystem.ReduceHeat(10f, "test");
            Assert.AreEqual(0f, heatSystem.GetHeatLevel(), 0.001f, "Heat should clamp to 0");
        }

        [Test]
        public void AddHeat_TracksSources()
        {
            heatSystem.AddHeat(10f, "drug_dealing");
            Dictionary<string, float> sources = heatSystem.GetHeatSources();
            Assert.IsTrue(sources.ContainsKey("drug_dealing"), "Source should be tracked");
            Assert.AreEqual(10f, sources["drug_dealing"], 0.001f, "Source amount should match");
        }

        [Test]
        public void OnHeatIncreased_Fires()
        {
            bool fired = false;
            heatSystem.OnHeatIncreased += (amount, source) => fired = true;
            heatSystem.AddHeat(5f, "test");
            Assert.IsTrue(fired, "OnHeatIncreased should fire");
        }

        [Test]
        public void OnHeatDecreased_Fires()
        {
            bool fired = false;
            heatSystem.AddHeat(5f, "test");
            heatSystem.OnHeatDecreased += amount => fired = true;
            heatSystem.ReduceHeat(2f, "test");
            Assert.IsTrue(fired, "OnHeatDecreased should fire");
        }

        [Test]
        public void OnHeatCleared_FiresWhenZero()
        {
            bool fired = false;
            heatSystem.AddHeat(5f, "test");
            heatSystem.OnHeatCleared += () => fired = true;
            heatSystem.ReduceHeat(5f, "test");
            Assert.IsTrue(fired, "OnHeatCleared should fire when heat hits 0");
        }

        [Test]
        public void HeatDecay_Before7Days_UsesBaseRate()
        {
            heatSystem.SetHeatLevelForTesting(24f);
            heatSystem.SetLastIncreaseForTesting(TimeEnergySystem.Instance.GetCurrentTime().AddDays(-2));
            heatSystem.ProcessHeatDecayForTesting(24f, TimeEnergySystem.Instance.GetCurrentTime());
            Assert.AreEqual(23f, heatSystem.GetHeatLevel(), 0.01f, "Base decay should be 1 per day");
        }

        [Test]
        public void HeatDecay_After7Days_Doubles()
        {
            heatSystem.SetHeatLevelForTesting(24f);
            heatSystem.SetLastIncreaseForTesting(TimeEnergySystem.Instance.GetCurrentTime().AddDays(-10));
            heatSystem.ProcessHeatDecayForTesting(24f, TimeEnergySystem.Instance.GetCurrentTime());
            Assert.AreEqual(22f, heatSystem.GetHeatLevel(), 0.01f, "Decay should double after 7 days");
        }

        [Test]
        public void HeatDecay_After30Days_Triples()
        {
            heatSystem.SetHeatLevelForTesting(24f);
            heatSystem.SetLastIncreaseForTesting(TimeEnergySystem.Instance.GetCurrentTime().AddDays(-40));
            heatSystem.ProcessHeatDecayForTesting(24f, TimeEnergySystem.Instance.GetCurrentTime());
            Assert.AreEqual(21f, heatSystem.GetHeatLevel(), 0.01f, "Decay should triple after 30 days");
        }

        [Test]
        public void HeatDecay_NoHeat_NoEvent()
        {
            bool fired = false;
            heatSystem.OnHeatDecreased += amount => fired = true;
            heatSystem.ProcessHeatDecayForTesting(24f, TimeEnergySystem.Instance.GetCurrentTime());
            Assert.IsFalse(fired, "No decay event when heat is zero");
        }

        [Test]
        public void Threshold30_IncreasesPatrolFrequency()
        {
            heatSystem.AddHeat(35f, "test");
            float patrolMultiplier = GetPrivateField<float>(heatSystem, "patrolFrequencyMultiplier");
            Assert.AreEqual(1.2f, patrolMultiplier, 0.001f, "Patrol frequency should increase at 30");
        }

        [Test]
        public void Threshold50_TriggersSurveillanceInvestigation()
        {
            HeatSystem.InvestigationType captured = HeatSystem.InvestigationType.Raid;
            heatSystem.OnInvestigationTriggered += type => captured = type;
            heatSystem.AddHeat(55f, "test");
            Assert.AreEqual(HeatSystem.InvestigationType.Surveillance, captured, "Surveillance should trigger at 50");
        }

        [Test]
        public void Threshold70_LowLegitimacy_TriggersAudit()
        {
            EconomySystem.Instance.AddIncome("player", 100f, EconomySystem.IncomeSource.DrugSale, "deal");
            HeatSystem.InvestigationType captured = HeatSystem.InvestigationType.Raid;
            heatSystem.OnInvestigationTriggered += type => captured = type;
            heatSystem.AddHeat(75f, "test");
            Assert.AreEqual(HeatSystem.InvestigationType.IRS_Audit, captured, "IRS audit should trigger at 70");
        }

        [Test]
        public void Threshold70_HighLegitimacy_NoAudit()
        {
            EconomySystem.Instance.AddIncome("player", 100f, EconomySystem.IncomeSource.Salary, "pay");
            bool audit = false;
            heatSystem.OnInvestigationTriggered += type =>
            {
                if (type == HeatSystem.InvestigationType.IRS_Audit)
                {
                    audit = true;
                }
            };
            heatSystem.AddHeat(75f, "test");
            Assert.IsFalse(audit, "IRS audit should not trigger with high legitimacy");
        }

        [Test]
        public void Threshold90_WithEvidence_TriggersRaid()
        {
            heatSystem.SetHasEvidenceForTesting(true);
            HeatSystem.InvestigationType captured = HeatSystem.InvestigationType.Surveillance;
            heatSystem.OnInvestigationTriggered += type => captured = type;
            heatSystem.AddHeat(95f, "test");
            Assert.AreEqual(HeatSystem.InvestigationType.Raid, captured, "Raid should trigger with evidence at 90");
        }

        [Test]
        public void Threshold90_NoEvidence_TriggersWarrant()
        {
            heatSystem.SetHasEvidenceForTesting(false);
            HeatSystem.InvestigationType captured = HeatSystem.InvestigationType.Surveillance;
            heatSystem.OnInvestigationTriggered += type => captured = type;
            heatSystem.AddHeat(95f, "test");
            Assert.AreEqual(HeatSystem.InvestigationType.Arrest_Warrant, captured, "Warrant should trigger without evidence at 90");
        }

        [Test]
        public void Investigation_Surveillance_IncreasesDetectionSensitivity()
        {
            heatSystem.TriggerInvestigation(HeatSystem.InvestigationType.Surveillance);
            float detectionMultiplier = GetPrivateField<float>(heatSystem, "detectionSensitivityMultiplier");
            Assert.AreEqual(1.3f, detectionMultiplier, 0.001f, "Detection sensitivity should increase");
        }

        [Test]
        public void Investigation_Audit_FreezeAssetsAmountSet()
        {
            EconomySystem.Instance.AddIncome("player", 1000f, EconomySystem.IncomeSource.Salary, "pay");
            heatSystem.TriggerInvestigation(HeatSystem.InvestigationType.IRS_Audit);
            float frozen = GetPrivateField<float>(heatSystem, "auditFrozenAmount");
            Assert.AreEqual(300f, frozen, 0.01f, "Audit should freeze 30% of balance");
        }

        [Test]
        public void Investigation_Audit_SetsResolutionTime()
        {
            heatSystem.TriggerInvestigation(HeatSystem.InvestigationType.IRS_Audit);
            DateTime resolution = GetPrivateField<DateTime>(heatSystem, "auditResolutionTime");
            Assert.AreNotEqual(DateTime.MinValue, resolution, "Audit should set resolution time");
        }

        [Test]
        public void ResolveAudit_LegitHigh_UnfreezesNoFine()
        {
            EconomySystem.Instance.AddIncome("player", 1000f, EconomySystem.IncomeSource.Salary, "pay");
            heatSystem.TriggerInvestigation(HeatSystem.InvestigationType.IRS_Audit);
            float before = EconomySystem.Instance.GetBalance("player");

            SetPrivateField(heatSystem, "auditActive", true);
            heatSystem.ResolveAuditForTesting();

            float after = EconomySystem.Instance.GetBalance("player");
            Assert.AreEqual(before, after, 0.01f, "High legitimacy audit should not fine");
        }

        [Test]
        public void ResolveAudit_LegitLow_Fines()
        {
            EconomySystem.Instance.AddIncome("player", 1000f, EconomySystem.IncomeSource.DrugSale, "deal");
            heatSystem.TriggerInvestigation(HeatSystem.InvestigationType.IRS_Audit);

            SetPrivateField(heatSystem, "auditActive", true);
            heatSystem.ResolveAuditForTesting();

            float after = EconomySystem.Instance.GetBalance("player");
            Assert.AreEqual(800f, after, 0.01f, "Low legitimacy audit should fine 20%");
        }

        [Test]
        public void OnHeatThresholdCrossed_FiresCorrectThreshold()
        {
            float captured = 0f;
            heatSystem.OnHeatThresholdCrossed += threshold => captured = threshold;
            heatSystem.AddHeat(35f, "test");
            Assert.AreEqual(30f, captured, 0.001f, "Should fire threshold 30");
        }

        [Test]
        public void OnInvestigationTriggered_Fires()
        {
            bool fired = false;
            heatSystem.OnInvestigationTriggered += type => fired = true;
            heatSystem.TriggerInvestigation(HeatSystem.InvestigationType.Surveillance);
            Assert.IsTrue(fired, "OnInvestigationTriggered should fire");
        }

        [Test]
        public void HeatDecay_After30Days_FiresOnHeatDecreased()
        {
            heatSystem.SetHeatLevelForTesting(10f);
            heatSystem.SetLastIncreaseForTesting(TimeEnergySystem.Instance.GetCurrentTime().AddDays(-40));
            bool fired = false;
            heatSystem.OnHeatDecreased += amount => fired = true;
            heatSystem.ProcessHeatDecayForTesting(24f, TimeEnergySystem.Instance.GetCurrentTime());
            Assert.IsTrue(fired, "Decay should fire OnHeatDecreased");
        }

        [Test]
        public void OnSuspiciousTransaction_LargeCashAddsHeat()
        {
            heatSystem.OnSuspiciousTransaction(10000f, "Other");
            Assert.Greater(heatSystem.GetHeatLevel(), 0f, "Large cash deposit should add heat");
        }

        [Test]
        public void OnFlashyPurchase_AddsHeat()
        {
            heatSystem.OnFlashyPurchase(90f);
            Assert.Greater(heatSystem.GetHeatLevel(), 0f, "Flashy purchase should add heat");
        }

        [Test]
        public void HeatClearsAfterDecay_OnHeatClearedFires()
        {
            bool fired = false;
            heatSystem.AddHeat(1f, "test");
            heatSystem.SetLastIncreaseForTesting(TimeEnergySystem.Instance.GetCurrentTime().AddDays(-40));
            heatSystem.OnHeatCleared += () => fired = true;
            heatSystem.ProcessHeatDecayForTesting(24f, TimeEnergySystem.Instance.GetCurrentTime());
            Assert.IsTrue(fired, "Heat cleared event should fire");
        }

        [Test]
        public void HeatSources_CanReduceWithReason()
        {
            heatSystem.AddHeat(10f, "flashy_purchase");
            heatSystem.ReduceHeat(5f, "flashy_purchase");
            Dictionary<string, float> sources = heatSystem.GetHeatSources();
            Assert.AreEqual(5f, sources["flashy_purchase"], 0.01f, "Source should reduce");
        }

        [Test]
        public void HeatSources_ReduceLargestWhenReasonMissing()
        {
            heatSystem.AddHeat(10f, "a");
            heatSystem.AddHeat(5f, "b");
            heatSystem.ReduceHeat(3f, "missing");
            Dictionary<string, float> sources = heatSystem.GetHeatSources();
            Assert.AreEqual(7f, sources["a"], 0.01f, "Largest source should reduce");
        }

        [Test]
        public void ReduceHeat_WithZeroAmount_DoesNothing()
        {
            heatSystem.AddHeat(10f, "test");
            heatSystem.ReduceHeat(0f, "test");
            Assert.AreEqual(10f, heatSystem.GetHeatLevel(), 0.01f, "Zero reduction should do nothing");
        }

        [Test]
        public void AddHeat_WithZeroAmount_DoesNothing()
        {
            heatSystem.AddHeat(0f, "test");
            Assert.AreEqual(0f, heatSystem.GetHeatLevel(), 0.01f, "Zero add should do nothing");
        }

        private static void ResetSingleton(Type systemType)
        {
            FieldInfo field = systemType.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(null, null);
            }
        }

        private static T GetPrivateField<T>(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return (T)field.GetValue(instance);
        }

        private static void SetPrivateField(object instance, string fieldName, object value)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(instance, value);
        }
    }
}
