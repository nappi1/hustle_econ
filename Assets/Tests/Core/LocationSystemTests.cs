using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Core;

namespace Tests.Core
{
    public class LocationSystemTests
    {
        private GameObject locationGameObject;
        private GameObject timeGameObject;
        private GameObject economyGameObject;
        private LocationSystem system;

        [SetUp]
        public void SetUp()
        {
            ResetSingleton(typeof(LocationSystem));
            ResetSingleton(typeof(TimeEnergySystem));
            ResetSingleton(typeof(EconomySystem));

            timeGameObject = new GameObject("TimeEnergySystem");
            timeGameObject.AddComponent<TimeEnergySystem>();

            economyGameObject = new GameObject("EconomySystem");
            economyGameObject.AddComponent<EconomySystem>();

            locationGameObject = new GameObject("LocationSystem");
            system = locationGameObject.AddComponent<LocationSystem>();

            system.CreateLocation(CreatePublicLocation("street_main", LocationSystem.LocationType.Street));
            system.CreateLocation(CreatePublicLocation("club_night", LocationSystem.LocationType.Club, new TimeSpan(20, 0, 0), new TimeSpan(23, 0, 0)));
            system.CreateLocation(CreatePrivateLocation("apartment_player"));
        }

        [TearDown]
        public void TearDown()
        {
            if (locationGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(locationGameObject);
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
        public void GetPlayerLocation_ReturnsCurrentLocation()
        {
            system.SetPlayerLocationForTesting("player", "street_main");
            Assert.AreEqual("street_main", system.GetPlayerLocation("player"), "Location should match");
        }

        [Test]
        public void TravelToLocation_MovesPlayer()
        {
            system.SetPlayerLocationForTesting("player", "street_main");
            AllowPlayerInApartment();
            EconomySystem.Instance.AddIncome("player", 100f, EconomySystem.IncomeSource.Salary, "seed");
            bool result = system.TravelToLocation("player", "apartment_player");
            Assert.IsTrue(result, "Travel should succeed");
            Assert.AreEqual("apartment_player", system.GetPlayerLocation("player"), "Player should be moved");
        }

        [Test]
        public void TravelToLocation_AdvancesTime()
        {
            DateTime before = TimeEnergySystem.Instance.GetCurrentTime();
            system.SetPlayerLocationForTesting("player", "street_main");
            AllowPlayerInApartment();
            EconomySystem.Instance.AddIncome("player", 100f, EconomySystem.IncomeSource.Salary, "seed");
            system.TravelToLocation("player", "apartment_player");
            DateTime after = TimeEnergySystem.Instance.GetCurrentTime();
            Assert.Greater(after, before, "Travel should advance time");
        }

        [Test]
        public void TravelToLocation_DeductsMoney()
        {
            EconomySystem.Instance.AddIncome("player", 100f, EconomySystem.IncomeSource.Salary, "seed");
            float before = EconomySystem.Instance.GetBalance("player");
            system.SetPlayerLocationForTesting("player", "street_main");
            AllowPlayerInApartment();
            system.TravelToLocation("player", "apartment_player");
            float after = EconomySystem.Instance.GetBalance("player");
            Assert.Less(after, before, "Travel should cost money");
        }

        [Test]
        public void TravelToLocation_CannotAfford_ReturnsFalse()
        {
            system.SetPlayerLocationForTesting("player", "street_main");
            AllowPlayerInApartment();
            bool result = system.TravelToLocation("player", "apartment_player");
            Assert.IsFalse(result, "Travel should fail when can't afford");
        }

        [Test]
        public void CheckLocationAccess_PrivateLocationDenied()
        {
            bool access = system.CheckLocationAccess("player", "apartment_player");
            Assert.IsFalse(access, "Private location should deny if not allowed");
        }

        [Test]
        public void CheckLocationAccess_PublicOpen_Allows()
        {
            bool access = system.CheckLocationAccess("player", "street_main");
            Assert.IsTrue(access, "Public open location should allow");
        }

        [Test]
        public void CheckLocationAccess_PublicClosed_Denies()
        {
            DateTime now = TimeEnergySystem.Instance.GetCurrentTime();
            system.SetPlayerLocationForTesting("player", "street_main");
            bool access = system.CheckLocationAccess("player", "club_night");
            Assert.IsFalse(access, "Closed club should deny access");
        }

        [Test]
        public void CheckLocationAccess_BannedPlayer_Denies()
        {
            system.AddBannedPlayerForTesting("player", "street_main");
            bool access = system.CheckLocationAccess("player", "street_main");
            Assert.IsFalse(access, "Banned player should be denied");
        }

        [Test]
        public void GetAllowedActivities_ReturnsCorrectList()
        {
            LocationSystem.LocationData location = system.GetLocationData("street_main");
            location.allowedActivities = new List<LocationSystem.ActivityType> { LocationSystem.ActivityType.Travel };
            system.CreateLocation(location);

            List<LocationSystem.ActivityType> activities = system.GetAllowedActivities("street_main");
            Assert.AreEqual(1, activities.Count, "Should return allowed activities");
            Assert.AreEqual(LocationSystem.ActivityType.Travel, activities[0], "Activity should match");
        }

        [Test]
        public void GetDetectionSensitivity_ReturnsValue()
        {
            LocationSystem.LocationData location = system.GetLocationData("street_main");
            location.detectionSensitivity = 0.5f;
            system.CreateLocation(location);
            Assert.AreEqual(0.5f, system.GetDetectionSensitivity("street_main"), 0.001f, "Sensitivity should match");
        }

        [Test]
        public void NPCs_CanBeAddedAndRemoved()
        {
            system.AddNPCToLocation("npc1", "street_main");
            List<string> npcs = system.GetNPCsInLocation("street_main");
            Assert.IsTrue(npcs.Contains("npc1"), "NPC should be added");
            system.RemoveNPCFromLocation("npc1", "street_main");
            npcs = system.GetNPCsInLocation("street_main");
            Assert.IsFalse(npcs.Contains("npc1"), "NPC should be removed");
        }

        [Test]
        public void OnLocationChanged_Fires()
        {
            string from = null;
            string to = null;
            system.OnLocationChanged += (player, oldLoc, newLoc) =>
            {
                from = oldLoc;
                to = newLoc;
            };
            system.SetPlayerLocationForTesting("player", "street_main");
            EconomySystem.Instance.AddIncome("player", 100f, EconomySystem.IncomeSource.Salary, "seed");
            AllowPlayerInApartment();
            system.TravelToLocation("player", "apartment_player");
            Assert.AreEqual("street_main", from, "From should match");
            Assert.AreEqual("apartment_player", to, "To should match");
        }

        [Test]
        public void OnLocationEntered_Fires()
        {
            string entered = null;
            system.OnLocationEntered += (player, loc) => entered = loc;
            system.SetPlayerLocationForTesting("player", "street_main");
            EconomySystem.Instance.AddIncome("player", 100f, EconomySystem.IncomeSource.Salary, "seed");
            AllowPlayerInApartment();
            system.TravelToLocation("player", "apartment_player");
            Assert.AreEqual("apartment_player", entered, "Entered should match");
        }

        [Test]
        public void OnLocationExited_Fires()
        {
            string exited = null;
            system.OnLocationExited += (player, loc) => exited = loc;
            system.SetPlayerLocationForTesting("player", "street_main");
            EconomySystem.Instance.AddIncome("player", 100f, EconomySystem.IncomeSource.Salary, "seed");
            AllowPlayerInApartment();
            system.TravelToLocation("player", "apartment_player");
            Assert.AreEqual("street_main", exited, "Exited should match");
        }

        [Test]
        public void TravelToLocation_DeniedWhenAccessFails()
        {
            bool result = system.TravelToLocation("player", "apartment_player");
            Assert.IsFalse(result, "Travel should fail if access denied");
        }

        [Test]
        public void TravelToLocation_DoesNotChangeLocationOnFailure()
        {
            system.SetPlayerLocationForTesting("player", "street_main");
            AllowPlayerInApartment();
            bool result = system.TravelToLocation("player", "apartment_player");
            Assert.IsFalse(result, "Travel should fail if can't afford");
            Assert.AreEqual("street_main", system.GetPlayerLocation("player"), "Location should remain");
        }

        [Test]
        public void TravelToLocation_RequiresLocationExists()
        {
            bool result = system.TravelToLocation("player", "missing_location");
            Assert.IsFalse(result, "Travel should fail for missing location");
        }

        [Test]
        public void CheckLocationAccess_AllowedPlayer_Allows()
        {
            LocationSystem.LocationData location = system.GetLocationData("apartment_player");
            location.allowedPlayers.Add("player");
            system.CreateLocation(location);
            bool access = system.CheckLocationAccess("player", "apartment_player");
            Assert.IsTrue(access, "Allowed player should access private location");
        }

        [Test]
        public void CheckLocationAccess_InvitationRequired_Denies()
        {
            LocationSystem.LocationData location = system.GetLocationData("apartment_player");
            location.requiresInvitation = true;
            system.CreateLocation(location);
            bool access = system.CheckLocationAccess("player", "apartment_player");
            Assert.IsFalse(access, "Invitation required should deny");
        }

        [Test]
        public void OnLocationLocked_Fires()
        {
            bool fired = false;
            system.OnLocationLocked += locationId => fired = true;
            system.TriggerLocationLockedForTesting("apartment_player");
            Assert.IsTrue(fired, "OnLocationLocked should fire");
        }

        [Test]
        public void GetNPCsInLocation_ReturnsEmptyWhenNone()
        {
            List<string> npcs = system.GetNPCsInLocation("street_main");
            Assert.AreEqual(0, npcs.Count, "Should return empty list");
        }

        [Test]
        public void GetAllowedActivities_EmptyWhenNone()
        {
            List<LocationSystem.ActivityType> activities = system.GetAllowedActivities("street_main");
            Assert.AreEqual(0, activities.Count, "Should return empty when none defined");
        }

        [Test]
        public void GetDetectionSensitivity_DefaultIsZero()
        {
            Assert.AreEqual(0f, system.GetDetectionSensitivity("street_main"), 0.001f, "Default sensitivity is 0");
        }

        [Test]
        public void TravelToLocation_UsesTaxiCost()
        {
            EconomySystem.Instance.AddIncome("player", 100f, EconomySystem.IncomeSource.Salary, "seed");
            float before = EconomySystem.Instance.GetBalance("player");
            system.SetPlayerLocationForTesting("player", "street_main");
            AllowPlayerInApartment();
            system.TravelToLocation("player", "apartment_player");
            float after = EconomySystem.Instance.GetBalance("player");
            Assert.AreEqual(before - 15f, after, 0.01f, "Taxi cost should be 15");
        }

        [Test]
        public void CanTravelTo_FailsWhenAccessDenied()
        {
            bool canTravel = system.CanTravelTo("player", "apartment_player");
            Assert.IsFalse(canTravel, "Should not travel without access");
        }

        [Test]
        public void CanTravelTo_FailsWhenInsufficientFunds()
        {
            system.SetPlayerLocationForTesting("player", "street_main");
            AllowPlayerInApartment();
            bool canTravel = system.CanTravelTo("player", "apartment_player");
            Assert.IsFalse(canTravel, "Should not travel without funds");
        }

        [Test]
        public void CanTravelTo_AllowsWhenAccessAndFunds()
        {
            EconomySystem.Instance.AddIncome("player", 100f, EconomySystem.IncomeSource.Salary, "seed");
            system.SetPlayerLocationForTesting("player", "street_main");
            AllowPlayerInApartment();
            bool canTravel = system.CanTravelTo("player", "apartment_player");
            Assert.IsTrue(canTravel, "Should travel when access and funds are available");
        }

        [Test]
        public void GetLocationData_ReturnsDefaultWhenMissing()
        {
            LocationSystem.LocationData data = system.GetLocationData("missing");
            Assert.IsTrue(string.IsNullOrEmpty(data.id), "Missing location should return default");
        }

        private static LocationSystem.LocationData CreatePublicLocation(string id, LocationSystem.LocationType type, TimeSpan? open = null, TimeSpan? close = null)
        {
            return new LocationSystem.LocationData
            {
                id = id,
                name = id,
                type = type,
                isPublic = true,
                requiresInvitation = false,
                allowedPlayers = new List<string>(),
                allowedActivities = new List<LocationSystem.ActivityType>(),
                detectionSensitivity = 0f,
                residingNPCs = new List<string>(),
                patrollingObservers = new List<string>(),
                sceneName = "Scene_" + id,
                spawnPosition = Vector3.zero,
                openTime = open ?? TimeSpan.Zero,
                closeTime = close ?? TimeSpan.Zero
            };
        }

        private static LocationSystem.LocationData CreatePrivateLocation(string id)
        {
            return new LocationSystem.LocationData
            {
                id = id,
                name = id,
                type = LocationSystem.LocationType.Apartment,
                isPublic = false,
                requiresInvitation = false,
                allowedPlayers = new List<string>(),
                allowedActivities = new List<LocationSystem.ActivityType>(),
                detectionSensitivity = 0.1f,
                residingNPCs = new List<string>(),
                patrollingObservers = new List<string>(),
                sceneName = "Scene_" + id,
                spawnPosition = Vector3.zero,
                openTime = TimeSpan.Zero,
                closeTime = TimeSpan.Zero
            };
        }

        private void AllowPlayerInApartment()
        {
            LocationSystem.LocationData location = system.GetLocationData("apartment_player");
            if (location.allowedPlayers == null)
            {
                location.allowedPlayers = new List<string>();
            }
            if (!location.allowedPlayers.Contains("player"))
            {
                location.allowedPlayers.Add("player");
            }
            system.CreateLocation(location);
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
    }
}
