using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Core;

namespace Tests.Core
{
    public class AdultContentSystemTests
    {
        private GameObject adultGameObject;
        private GameObject timeGameObject;
        private GameObject economyGameObject;
        private GameObject reputationGameObject;
        private GameObject locationGameObject;
        private GameObject relationshipGameObject;
        private AdultContentSystem system;

        [SetUp]
        public void SetUp()
        {
            ResetSingleton(typeof(AdultContentSystem));
            ResetSingleton(typeof(TimeEnergySystem));
            ResetSingleton(typeof(EconomySystem));
            ResetSingleton(typeof(ReputationSystem));
            ResetSingleton(typeof(LocationSystem));
            ResetSingleton(typeof(RelationshipSystem));

            timeGameObject = new GameObject("TimeEnergySystem");
            timeGameObject.AddComponent<TimeEnergySystem>();

            economyGameObject = new GameObject("EconomySystem");
            economyGameObject.AddComponent<EconomySystem>();

            reputationGameObject = new GameObject("ReputationSystem");
            reputationGameObject.AddComponent<ReputationSystem>();

            relationshipGameObject = new GameObject("RelationshipSystem");
            relationshipGameObject.AddComponent<RelationshipSystem>();

            locationGameObject = new GameObject("LocationSystem");
            LocationSystem location = locationGameObject.AddComponent<LocationSystem>();
            location.CreateLocation(CreateLocation("office_main", LocationSystem.LocationType.Office));
            location.CreateLocation(CreateLocation("club_night", LocationSystem.LocationType.Club));
            location.SetPlayerLocationForTesting("player", "office_main");

            adultGameObject = new GameObject("AdultContentSystem");
            system = adultGameObject.AddComponent<AdultContentSystem>();
            system.SetPlayerIdForTesting("player");
        }

        [TearDown]
        public void TearDown()
        {
            if (adultGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(adultGameObject);
            }

            if (locationGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(locationGameObject);
            }

            if (relationshipGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(relationshipGameObject);
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
        public void EquipClothing_FiresOnClothingChanged()
        {
            var item = CreateClothing("suit", AdultContentSystem.ClothingCategory.Professional, 15f, 0f, 0.5f, 5f);
            system.RegisterClothingItemForTesting(item);

            string captured = null;
            system.OnClothingChanged += id => captured = id;
            system.EquipClothing("suit");

            Assert.AreEqual("suit", captured, "Clothing change event should fire");
        }

        [Test]
        public void ClothingEffects_OfficeProfessional_IncreasesProfessionalRep()
        {
            var item = CreateClothing("suit", AdultContentSystem.ClothingCategory.Professional, 15f, 0f, 0.5f, 5f);
            system.RegisterClothingItemForTesting(item);
            LocationSystem.Instance.SetPlayerLocationForTesting("player", "office_main");

            float before = ReputationSystem.Instance.GetReputation("player", ReputationSystem.ReputationTrack.Professional);
            system.EquipClothing("suit");
            float after = ReputationSystem.Instance.GetReputation("player", ReputationSystem.ReputationTrack.Professional);

            Assert.AreEqual(before + 2f, after, 0.01f, "Professional attire should boost rep");
        }

        [Test]
        public void ClothingEffects_OfficeProvocative_DecreasesProfessionalRep()
        {
            var item = CreateClothing("lingerie", AdultContentSystem.ClothingCategory.Lingerie, -30f, -10f, 1.5f, 20f);
            system.RegisterClothingItemForTesting(item);
            LocationSystem.Instance.SetPlayerLocationForTesting("player", "office_main");

            float before = ReputationSystem.Instance.GetReputation("player", ReputationSystem.ReputationTrack.Professional);
            system.EquipClothing("lingerie");
            float after = ReputationSystem.Instance.GetReputation("player", ReputationSystem.ReputationTrack.Professional);

            Assert.AreEqual(before - 10f, after, 0.01f, "Inappropriate attire should reduce rep");
        }

        [Test]
        public void ClothingEffects_ClubProvocative_IncreasesSocialRep()
        {
            var item = CreateClothing("clubwear", AdultContentSystem.ClothingCategory.Provocative, 0f, 5f, 1.2f, 10f);
            system.RegisterClothingItemForTesting(item);
            LocationSystem.Instance.SetPlayerLocationForTesting("player", "club_night");

            float before = ReputationSystem.Instance.GetReputation("player", ReputationSystem.ReputationTrack.Social);
            system.EquipClothing("clubwear");
            float after = ReputationSystem.Instance.GetReputation("player", ReputationSystem.ReputationTrack.Social);

            Assert.AreEqual(before + 5f, after, 0.01f, "Club attire should boost social rep");
        }

        [Test]
        public void GetClothingModifier_NudeOnlyFansBoost()
        {
            float modifier = system.GetClothingModifier(AdultContentSystem.ClothingModifierType.OnlyFansIncome);
            Assert.Greater(modifier, 2f, "Nude should have high OnlyFans modifier");
        }

        [Test]
        public void GetClothingModifier_ProfessionalRep_FromItem()
        {
            var item = CreateClothing("suit", AdultContentSystem.ClothingCategory.Professional, 15f, 0f, 0.5f, 5f);
            system.RegisterClothingItemForTesting(item);
            system.EquipClothing("suit");

            float modifier = system.GetClothingModifier(AdultContentSystem.ClothingModifierType.ProfessionalRep);
            Assert.AreEqual(15f, modifier, 0.01f, "Professional modifier should match item");
        }

        [Test]
        public void BodyAttractiveness_AffectsOnlyFansModifier()
        {
            var item = CreateClothing("lingerie", AdultContentSystem.ClothingCategory.Lingerie, -30f, -10f, 1.5f, 20f);
            system.RegisterClothingItemForTesting(item);
            system.SetBodyAttractivenessForTesting(100f);
            system.EquipClothing("lingerie");

            float modifier = system.GetClothingModifier(AdultContentSystem.ClothingModifierType.OnlyFansIncome);
            Assert.Greater(modifier, 1.5f, "Body attractiveness should increase income modifier");
        }

        [Test]
        public void TriggerBlackmail_FiresEvent()
        {
            bool fired = false;
            system.OnBlackmailTriggered += (id, type, demand) => fired = true;
            system.TriggerBlackmail("npc1", AdultContentSystem.BlackmailType.NudePhotos, 500f);
            Assert.IsTrue(fired, "Blackmail event should fire");
        }

        [Test]
        public void RespondToBlackmail_Pay_RemovesBlackmail()
        {
            EconomySystem.Instance.AddIncome("player", 1000f, EconomySystem.IncomeSource.Salary, "seed");
            system.TriggerBlackmail("npc1", AdultContentSystem.BlackmailType.NudePhotos, 500f);
            system.RespondToBlackmail("npc1", AdultContentSystem.BlackmailResponse.Pay);

            bool exists = GetActiveBlackmail(system).ContainsKey("npc1");
            Assert.IsFalse(exists, "Blackmail should be removed after response");
        }

        [Test]
        public void RespondToBlackmail_Refuse_LeaksContent()
        {
            bool leaked = false;
            system.OnContentLeaked += content => leaked = true;
            system.TriggerBlackmail("npc1", AdultContentSystem.BlackmailType.StreamContent, 500f);
            system.RespondToBlackmail("npc1", AdultContentSystem.BlackmailResponse.Refuse);
            Assert.IsTrue(leaked, "Refuse should leak content");
        }

        [Test]
        public void RespondToBlackmail_Refuse_DamagesReputation()
        {
            float socialBefore = ReputationSystem.Instance.GetReputation("player", ReputationSystem.ReputationTrack.Social);
            float profBefore = ReputationSystem.Instance.GetReputation("player", ReputationSystem.ReputationTrack.Professional);

            system.TriggerBlackmail("npc1", AdultContentSystem.BlackmailType.StreamContent, 500f);
            system.RespondToBlackmail("npc1", AdultContentSystem.BlackmailResponse.Refuse);

            float socialAfter = ReputationSystem.Instance.GetReputation("player", ReputationSystem.ReputationTrack.Social);
            float profAfter = ReputationSystem.Instance.GetReputation("player", ReputationSystem.ReputationTrack.Professional);

            Assert.AreEqual(socialBefore - 30f, socialAfter, 0.01f, "Social rep should drop on leak");
            Assert.AreEqual(profBefore - 40f, profAfter, 0.01f, "Professional rep should drop on leak");
        }

        [Test]
        public void RespondToBlackmail_Violence_Success_IncreasesCriminalRep()
        {
            system.SetForcedRandomForTesting(0.9f);
            system.TriggerBlackmail("npc1", AdultContentSystem.BlackmailType.NudePhotos, 500f);
            float before = ReputationSystem.Instance.GetReputation("player", ReputationSystem.ReputationTrack.Criminal);
            system.RespondToBlackmail("npc1", AdultContentSystem.BlackmailResponse.Violence);
            float after = ReputationSystem.Instance.GetReputation("player", ReputationSystem.ReputationTrack.Criminal);
            Assert.AreEqual(before + 5f, after, 0.01f, "Violence success should boost criminal rep");
        }

        [Test]
        public void LeakContent_FamilyDiscovered_FiresEvent()
        {
            system.AddFamilyNpcForTesting("family1");
            bool fired = false;
            system.OnFamilyDiscoveredSexWork += id => fired = true;
            system.TriggerBlackmail("npc1", AdultContentSystem.BlackmailType.NudePhotos, 500f);
            system.RespondToBlackmail("npc1", AdultContentSystem.BlackmailResponse.Refuse);
            Assert.IsTrue(fired, "Family discovery event should fire");
        }

        [Test]
        public void Escort_SafeClient_GeneratesIncome()
        {
            float before = EconomySystem.Instance.GetBalance("player");
            var result = system.StartEscortAppointment(AdultContentSystem.ClientType.Safe);
            float after = EconomySystem.Instance.GetBalance("player");
            Assert.Greater(after, before, "Safe escort should pay");
            Assert.AreEqual(500f, result.earnings, 0.01f, "Safe base earnings should be 500");
        }

        [Test]
        public void Escort_DangerousClient_CanHurtPlayer()
        {
            system.SetForcedRandomForTesting(0.0f);
            var result = system.StartEscortAppointment(AdultContentSystem.ClientType.Dangerous);
            Assert.IsTrue(result.safetyIssue, "Dangerous client should cause safety issue with low random");
        }

        [Test]
        public void Escort_PoliceClient_Arrests()
        {
            float before = ReputationSystem.Instance.GetReputation("player", ReputationSystem.ReputationTrack.Legal);
            var result = system.StartEscortAppointment(AdultContentSystem.ClientType.Police);
            float after = ReputationSystem.Instance.GetReputation("player", ReputationSystem.ReputationTrack.Legal);
            Assert.IsTrue(result.policeInvolved, "Police client should flag police");
            Assert.AreEqual(before - 25f, after, 0.01f, "Police sting should hurt legal rep");
        }

        [Test]
        public void Escort_AppointmentEvent_Fires()
        {
            bool fired = false;
            system.OnEscortAppointmentComplete += res => fired = true;
            system.StartEscortAppointment(AdultContentSystem.ClientType.Safe);
            Assert.IsTrue(fired, "Escort appointment event should fire");
        }

        [Test]
        public void Escort_ReputationDamage_Applies()
        {
            float before = ReputationSystem.Instance.GetReputation("player", ReputationSystem.ReputationTrack.Social);
            system.StartEscortAppointment(AdultContentSystem.ClientType.Safe);
            float after = ReputationSystem.Instance.GetReputation("player", ReputationSystem.ReputationTrack.Social);
            Assert.AreEqual(before - 5f, after, 0.01f, "Escort should reduce social rep");
        }

        [Test]
        public void SugarRelationship_Starts_AndFiresEvent()
        {
            bool fired = false;
            system.OnSugarRelationshipStarted += (id, terms) => fired = true;
            system.StartSugarRelationship("benefactor", new AdultContentSystem.SugarTerms { monthlyAllowance = 1000f, hoursPerWeek = 4 });
            Assert.IsTrue(fired, "Sugar relationship start event should fire");
        }

        [Test]
        public void SugarRelationship_AllowanceAddsIncome()
        {
            system.StartSugarRelationship("benefactor", new AdultContentSystem.SugarTerms { monthlyAllowance = 1000f, hoursPerWeek = 4 });
            float before = EconomySystem.Instance.GetBalance("player");
            system.TriggerSugarAllowanceForTesting("benefactor");
            float after = EconomySystem.Instance.GetBalance("player");
            Assert.AreEqual(before + 1000f, after, 0.01f, "Allowance should add income");
        }

        [Test]
        public void SugarRelationship_End_FiresEvent()
        {
            bool fired = false;
            system.StartSugarRelationship("benefactor", new AdultContentSystem.SugarTerms { monthlyAllowance = 1000f, hoursPerWeek = 4 });
            system.OnSugarRelationshipEnded += id => fired = true;
            system.EndSugarRelationship("benefactor", AdultContentSystem.SugarEndReason.PlayerChoice);
            Assert.IsTrue(fired, "Sugar relationship end event should fire");
        }

        [Test]
        public void SugarRelationship_EndBenefactor_CanTriggerBlackmail()
        {
            system.SetForcedRandomForTesting(0.0f);
            bool blackmail = false;
            system.OnBlackmailTriggered += (id, type, demand) => blackmail = true;
            system.StartSugarRelationship("benefactor", new AdultContentSystem.SugarTerms { monthlyAllowance = 1000f, hoursPerWeek = 4 });
            system.EndSugarRelationship("benefactor", AdultContentSystem.SugarEndReason.BenefactorEnded);
            Assert.IsTrue(blackmail, "Benefactor end should be able to trigger blackmail");
        }

        [Test]
        public void GetClothingModifier_Attractiveness_FromItem()
        {
            var item = CreateClothing("dress", AdultContentSystem.ClothingCategory.Casual, 0f, 0f, 1.0f, 12f);
            system.RegisterClothingItemForTesting(item);
            system.EquipClothing("dress");
            float modifier = system.GetClothingModifier(AdultContentSystem.ClothingModifierType.Attractiveness);
            Assert.AreEqual(12f, modifier, 0.01f, "Attractiveness modifier should match item");
        }

        [Test]
        public void GetClothingModifier_NudeProfessionalPenalty()
        {
            float modifier = system.GetClothingModifier(AdultContentSystem.ClothingModifierType.ProfessionalRep);
            Assert.AreEqual(-50f, modifier, 0.01f, "Nude should have severe professional penalty");
        }

        [Test]
        public void RespondToBlackmail_Pay_CantAfford_Leaks()
        {
            bool leaked = false;
            system.OnContentLeaked += content => leaked = true;
            system.TriggerBlackmail("npc1", AdultContentSystem.BlackmailType.NudePhotos, 10000f);
            system.RespondToBlackmail("npc1", AdultContentSystem.BlackmailResponse.Pay);
            Assert.IsTrue(leaked, "If can't pay, content should leak");
        }

        private static AdultContentSystem.ClothingItem CreateClothing(
            string id,
            AdultContentSystem.ClothingCategory category,
            float professionalMod,
            float socialMod,
            float onlyFansIncomeMod,
            float attractivenessMod
        )
        {
            return new AdultContentSystem.ClothingItem
            {
                id = id,
                category = category,
                professionalMod = professionalMod,
                socialMod = socialMod,
                onlyFansIncomeMod = onlyFansIncomeMod,
                attractivenessMod = attractivenessMod,
                allowedInOffice = true,
                allowedInPublic = true
            };
        }

        private static LocationSystem.LocationData CreateLocation(string id, LocationSystem.LocationType type)
        {
            return new LocationSystem.LocationData
            {
                id = id,
                name = id,
                type = type,
                isPublic = true,
                requiresInvitation = false,
                allowedPlayers = new System.Collections.Generic.List<string>(),
                allowedActivities = new System.Collections.Generic.List<LocationSystem.ActivityType>(),
                detectionSensitivity = 0.5f,
                residingNPCs = new System.Collections.Generic.List<string>(),
                patrollingObservers = new System.Collections.Generic.List<string>(),
                sceneName = id,
                spawnPosition = Vector3.zero,
                openTime = TimeSpan.Zero,
                closeTime = TimeSpan.Zero
            };
        }

        private static Dictionary<string, AdultContentSystem.BlackmailEvent> GetActiveBlackmail(AdultContentSystem system)
        {
            FieldInfo field = typeof(AdultContentSystem).GetField("activeBlackmail", BindingFlags.Instance | BindingFlags.NonPublic);
            return (Dictionary<string, AdultContentSystem.BlackmailEvent>)field.GetValue(system);
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
