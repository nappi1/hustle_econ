using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Core;

namespace Tests.Core
{
    public class BodySystemTests
    {
        private GameObject bodyGameObject;
        private GameObject timeGameObject;
        private GameObject economyGameObject;
        private GameObject reputationGameObject;
        private BodySystem system;

        [SetUp]
        public void SetUp()
        {
            ResetSingleton(typeof(BodySystem));
            ResetSingleton(typeof(TimeEnergySystem));
            ResetSingleton(typeof(EconomySystem));
            ResetSingleton(typeof(ReputationSystem));

            timeGameObject = new GameObject("TimeEnergySystem");
            timeGameObject.AddComponent<TimeEnergySystem>();

            economyGameObject = new GameObject("EconomySystem");
            economyGameObject.AddComponent<EconomySystem>();

            reputationGameObject = new GameObject("ReputationSystem");
            reputationGameObject.AddComponent<ReputationSystem>();

            bodyGameObject = new GameObject("BodySystem");
            system = bodyGameObject.AddComponent<BodySystem>();
            system.SetPlayerIdForTesting("player");
        }

        [TearDown]
        public void TearDown()
        {
            if (bodyGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(bodyGameObject);
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
        public void SetBodyType_ChangesTypeAndFiresEvent()
        {
            BodySystem.BodyType captured = BodySystem.BodyType.Average;
            system.OnBodyTypeChanged += type => captured = type;
            system.SetBodyType(BodySystem.BodyType.Athletic);
            Assert.AreEqual(BodySystem.BodyType.Athletic, captured, "Event should fire with new body type");
        }

        [Test]
        public void ModifyFitness_UpdatesFitness()
        {
            system.SetFitnessForTesting(50f);
            system.ModifyFitness(10f);
            BodySystem.BodyState state = system.GetStateForTesting();
            Assert.AreEqual(60f, state.fitness, 0.01f, "Fitness should increase");
        }

        [Test]
        public void ModifyFitness_ClampsAt0And100()
        {
            system.SetFitnessForTesting(5f);
            system.ModifyFitness(-10f);
            Assert.AreEqual(0f, system.GetStateForTesting().fitness, 0.01f, "Fitness should clamp to 0");
            system.ModifyFitness(200f);
            Assert.AreEqual(100f, system.GetStateForTesting().fitness, 0.01f, "Fitness should clamp to 100");
        }

        [Test]
        public void FitnessAffectsEnergyMax()
        {
            system.SetFitnessForTesting(100f);
            Assert.AreEqual(120f, system.GetEnergyMax(), 0.01f, "Energy max should be 120 at fitness 100");
            system.SetFitnessForTesting(0f);
            Assert.AreEqual(100f, system.GetEnergyMax(), 0.01f, "Energy max should be 100 at fitness 0");
        }

        [Test]
        public void FitnessDecaysOverTime()
        {
            system.SetFitnessForTesting(50f);
            system.ProcessFitnessDecayForTesting(24f);
            Assert.AreEqual(49.5f, system.GetStateForTesting().fitness, 0.01f, "Fitness should decay by 0.5 per day");
        }

        [Test]
        public void BodyTypeChanges_WhenFitnessCrossesThreshold()
        {
            system.SetBodyTypeForTesting(BodySystem.BodyType.Average);
            system.SetFitnessForTesting(85f);
            Assert.AreEqual(BodySystem.BodyType.Athletic, system.GetStateForTesting().bodyType, "High fitness should be Athletic");
        }

        [Test]
        public void BodyTypeChanges_LowFitnessBecomesHeavy()
        {
            system.SetBodyTypeForTesting(BodySystem.BodyType.Average);
            system.SetFitnessForTesting(10f);
            Assert.AreEqual(BodySystem.BodyType.Heavy, system.GetStateForTesting().bodyType, "Low fitness should be Heavy");
        }

        [Test]
        public void GetAttractiveness_BaseCalculation()
        {
            system.SetFitnessForTesting(0f);
            system.SetBodyTypeForTesting(BodySystem.BodyType.Average);
            system.SetGroomingForTesting(BodySystem.GroomingLevel.Basic);
            system.SetClothingVanityForTesting(0f);
            float attractiveness = system.GetAttractiveness();
            Assert.AreEqual(55f, attractiveness, 0.01f, "Average body should add 5 to base 50");
        }

        [Test]
        public void GetAttractiveness_IncludesFitness()
        {
            system.SetFitnessForTesting(100f);
            system.SetBodyTypeForTesting(BodySystem.BodyType.Average);
            system.SetGroomingForTesting(BodySystem.GroomingLevel.Basic);
            system.SetClothingVanityForTesting(0f);
            float attractiveness = system.GetAttractiveness();
            Assert.AreEqual(85f, attractiveness, 0.01f, "Fitness should add 30");
        }

        [Test]
        public void GetAttractiveness_IncludesGrooming()
        {
            system.SetFitnessForTesting(0f);
            system.SetBodyTypeForTesting(BodySystem.BodyType.Slim);
            system.SetGroomingForTesting(BodySystem.GroomingLevel.Glamorous);
            system.SetClothingVanityForTesting(0f);
            float attractiveness = system.GetAttractiveness();
            Assert.AreEqual(85f, attractiveness, 0.01f, "Glamorous should add 25");
        }

        [Test]
        public void GetAttractiveness_IncludesClothingVanity()
        {
            system.SetFitnessForTesting(0f);
            system.SetBodyTypeForTesting(BodySystem.BodyType.Average);
            system.SetGroomingForTesting(BodySystem.GroomingLevel.Basic);
            system.SetClothingVanityForTesting(50f);
            float attractiveness = system.GetAttractiveness();
            Assert.AreEqual(65f, attractiveness, 0.01f, "Clothing vanity should add 10");
        }

        [Test]
        public void GetAttractiveness_WithNpcPreference()
        {
            system.SetFitnessForTesting(0f);
            system.SetBodyTypeForTesting(BodySystem.BodyType.Slim);
            system.SetGroomingForTesting(BodySystem.GroomingLevel.Basic);
            system.SetClothingVanityForTesting(0f);
            system.SetNpcPreferenceForTesting("npc1", BodySystem.BodyType.Slim, 1.0f);
            float attractiveness = system.GetAttractiveness("npc1");
            Assert.AreEqual(80f, attractiveness, 0.01f, "Preference should add +20");
        }

        [Test]
        public void GetAttractiveness_NoNpcPreference_NoBonus()
        {
            system.SetFitnessForTesting(0f);
            system.SetBodyTypeForTesting(BodySystem.BodyType.Slim);
            system.SetGroomingForTesting(BodySystem.GroomingLevel.Basic);
            system.SetClothingVanityForTesting(0f);
            float attractiveness = system.GetAttractiveness("npc_missing");
            Assert.AreEqual(60f, attractiveness, 0.01f, "No preference should have no bonus");
        }

        [Test]
        public void SetGrooming_CostsMoney()
        {
            EconomySystem.Instance.AddIncome("player", 1000f, EconomySystem.IncomeSource.Salary, "seed");
            float before = EconomySystem.Instance.GetBalance("player");
            system.SetGrooming(BodySystem.GroomingLevel.Professional);
            float after = EconomySystem.Instance.GetBalance("player");
            Assert.AreEqual(before - 500f, after, 0.01f, "Professional grooming should cost 500");
        }

        [Test]
        public void Grooming_AffectsProfessionalReputation()
        {
            float before = ReputationSystem.Instance.GetReputation("player", ReputationSystem.ReputationTrack.Professional);
            system.SetGrooming(BodySystem.GroomingLevel.Professional);
            float after = ReputationSystem.Instance.GetReputation("player", ReputationSystem.ReputationTrack.Professional);
            Assert.AreEqual(before + 5f, after, 0.01f, "Professional grooming should increase professional rep");
        }

        [Test]
        public void Grooming_AffectsSocialReputation()
        {
            float before = ReputationSystem.Instance.GetReputation("player", ReputationSystem.ReputationTrack.Social);
            system.SetGrooming(BodySystem.GroomingLevel.WellGroomed);
            float after = ReputationSystem.Instance.GetReputation("player", ReputationSystem.ReputationTrack.Social);
            Assert.AreEqual(before + 5f, after, 0.01f, "WellGroomed should increase social rep");
        }

        [Test]
        public void GroomingMaintenance_DeductedMonthly()
        {
            EconomySystem.Instance.AddIncome("player", 1000f, EconomySystem.IncomeSource.Salary, "seed");
            system.SetGrooming(BodySystem.GroomingLevel.WellGroomed);
            float before = EconomySystem.Instance.GetBalance("player");
            system.ApplyGroomingMaintenanceForTesting();
            float after = EconomySystem.Instance.GetBalance("player");
            Assert.AreEqual(before - 100f, after, 0.01f, "Maintenance should cost 100");
        }

        [Test]
        public void GroomingMaintenance_CannotAfford_Downgrades()
        {
            system.SetGrooming(BodySystem.GroomingLevel.Glamorous);
            system.ApplyGroomingMaintenanceForTesting();
            Assert.AreEqual(BodySystem.GroomingLevel.Professional, system.GetStateForTesting().grooming, "Should downgrade grooming");
        }

        [Test]
        public void OnFitnessChanged_Fires()
        {
            bool fired = false;
            system.OnFitnessChanged += value => fired = true;
            system.ModifyFitness(5f);
            Assert.IsTrue(fired, "OnFitnessChanged should fire");
        }

        [Test]
        public void OnGroomingChanged_Fires()
        {
            bool fired = false;
            system.OnGroomingChanged += level => fired = true;
            system.SetGrooming(BodySystem.GroomingLevel.Basic);
            Assert.IsTrue(fired, "OnGroomingChanged should fire");
        }

        [Test]
        public void OnAttractivenessChanged_Fires()
        {
            bool fired = false;
            system.OnAttractivenessChanged += value => fired = true;
            system.SetFitnessForTesting(70f);
            Assert.IsTrue(fired, "OnAttractivenessChanged should fire");
        }

        [Test]
        public void GroomingDowngrade_NoCost()
        {
            system.SetGroomingForTesting(BodySystem.GroomingLevel.Professional);
            float before = EconomySystem.Instance.GetBalance("player");
            system.ApplyGroomingMaintenanceForTesting();
            float after = EconomySystem.Instance.GetBalance("player");
            Assert.AreEqual(before, after, 0.01f, "Downgrade should not cost extra");
        }

        [Test]
        public void BodyTypeTransition_FromHeavyWithMidFitness_ToAverage()
        {
            system.SetBodyTypeForTesting(BodySystem.BodyType.Heavy);
            system.SetFitnessForTesting(65f);
            Assert.AreEqual(BodySystem.BodyType.Average, system.GetStateForTesting().bodyType, "Heavy with mid fitness should become Average");
        }

        [Test]
        public void BodyTypeTransition_FromSlimLowFitness_ToAverage()
        {
            system.SetBodyTypeForTesting(BodySystem.BodyType.Slim);
            system.SetFitnessForTesting(30f);
            Assert.AreEqual(BodySystem.BodyType.Average, system.GetStateForTesting().bodyType, "Slim with low fitness should move to Average");
        }

        [Test]
        public void Grooming_Unkempt_ReducesReputation()
        {
            float beforeProf = ReputationSystem.Instance.GetReputation("player", ReputationSystem.ReputationTrack.Professional);
            float beforeSocial = ReputationSystem.Instance.GetReputation("player", ReputationSystem.ReputationTrack.Social);
            system.SetGrooming(BodySystem.GroomingLevel.Unkempt);
            float afterProf = ReputationSystem.Instance.GetReputation("player", ReputationSystem.ReputationTrack.Professional);
            float afterSocial = ReputationSystem.Instance.GetReputation("player", ReputationSystem.ReputationTrack.Social);
            Assert.AreEqual(beforeProf - 5f, afterProf, 0.01f, "Unkempt should reduce professional rep");
            Assert.AreEqual(beforeSocial - 5f, afterSocial, 0.01f, "Unkempt should reduce social rep");
        }

        [Test]
        public void Attractiveness_ClampsTo100()
        {
            system.SetBodyTypeForTesting(BodySystem.BodyType.Athletic);
            system.SetFitnessForTesting(100f);
            system.SetGroomingForTesting(BodySystem.GroomingLevel.Glamorous);
            system.SetClothingVanityForTesting(200f);
            float attractiveness = system.GetAttractiveness();
            Assert.AreEqual(100f, attractiveness, 0.01f, "Attractiveness should clamp at 100");
        }

        [Test]
        public void Attractiveness_ClampsTo0()
        {
            system.SetBodyTypeForTesting(BodySystem.BodyType.Heavy);
            system.SetFitnessForTesting(0f);
            system.SetGroomingForTesting(BodySystem.GroomingLevel.Unkempt);
            system.SetClothingVanityForTesting(-300f);
            float attractiveness = system.GetAttractiveness();
            Assert.AreEqual(0f, attractiveness, 0.01f, "Attractiveness should clamp at 0");
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
