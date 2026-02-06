using System;
using System.Collections.Generic;
using System.Reflection;
using Core;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Core
{
    public class DetectionSystemTests
    {
        private GameObject detectionGameObject;
        private DetectionSystem system;

        [SetUp]
        public void SetUp()
        {
            ResetSingleton(typeof(DetectionSystem));
            detectionGameObject = new GameObject("DetectionSystem");
            system = detectionGameObject.AddComponent<DetectionSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            if (detectionGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(detectionGameObject);
            }
        }

        [Test]
        public void RegisterObserver_WithValidData_RegistersSuccessfully()
        {
            system.RegisterObserver("obs1", CreateObserverData(DetectionSystem.ObserverRole.Boss, "office"));
            Assert.IsTrue(HasObserver("obs1"));
        }

        [Test]
        public void UnregisterObserver_RemovesObserver()
        {
            system.RegisterObserver("obs1", CreateObserverData(DetectionSystem.ObserverRole.Boss, "office"));
            system.UnregisterObserver("obs1");
            Assert.IsFalse(HasObserver("obs1"));
        }

        [Test]
        public void UpdateObserverPosition_ChangesPositionAndFacing()
        {
            system.RegisterObserver("obs1", CreateObserverData(DetectionSystem.ObserverRole.Boss, "office"));
            system.UpdateObserverPosition("obs1", new Vector3(5, 0, 0), Vector3.right);
            DetectionSystem.Observer observer = GetObserver("obs1");
            Assert.AreEqual(new Vector3(5, 0, 0), observer.position);
            Assert.AreEqual(Vector3.right, observer.facing);
        }

        [Test]
        public void RegisterObserver_MultipleObservers_AllTracked()
        {
            system.RegisterObserver("obs1", CreateObserverData(DetectionSystem.ObserverRole.Boss, "office"));
            system.RegisterObserver("obs2", CreateObserverData(DetectionSystem.ObserverRole.Cop, "street"));
            Assert.IsTrue(HasObserver("obs1"));
            Assert.IsTrue(HasObserver("obs2"));
        }

        [Test]
        public void CheckDetection_PlayerOutOfRange_ReturnsNotDetected()
        {
            RegisterBasicObserver("obs1", DetectionSystem.ObserverRole.Boss, "office", 5f, 90f, true, false);
            system.SetPlayerPositionForTesting("player", new Vector3(0, 0, 10));
            system.SetPlayerLocationForTesting("player", "office");
            system.SetActivityForTesting("act", new DetectionSystem.Activity { id = "act", isLegal = true, visualProfile = 1f });

            var result = system.CheckDetection("player", "act");
            Assert.IsFalse(result.detected);
        }

        [Test]
        public void CheckDetection_PlayerInRange_WithLineOfSight_Detected()
        {
            RegisterBasicObserver("obs1", DetectionSystem.ObserverRole.Boss, "office", 10f, 90f, true, false);
            system.SetPlayerPositionForTesting("player", new Vector3(0, 0, 5));
            system.SetPlayerLocationForTesting("player", "office");
            system.SetActivityForTesting("act", new DetectionSystem.Activity { id = "act", isLegal = true, visualProfile = 0.1f });

            var result = system.CheckDetection("player", "act");
            Assert.IsTrue(result.detected);
            Assert.AreEqual("obs1", result.observerId);
        }

        [Test]
        public void CheckDetection_OutsideVisionCone_ReturnsNotDetected()
        {
            RegisterBasicObserver("obs1", DetectionSystem.ObserverRole.Security, "office", 10f, 90f, true, true);
            system.SetPlayerPositionForTesting("player", new Vector3(5, 0, 0));
            system.SetPlayerLocationForTesting("player", "office");
            system.SetActivityForTesting("act", new DetectionSystem.Activity { id = "act", isLegal = false, visualProfile = 1f });

            var result = system.CheckDetection("player", "act");
            Assert.IsFalse(result.detected);
        }

        [Test]
        public void CheckDetection_InsideVisionCone_Detected()
        {
            RegisterBasicObserver("obs1", DetectionSystem.ObserverRole.Security, "office", 10f, 90f, true, true);
            system.SetPlayerPositionForTesting("player", new Vector3(0, 0, 5));
            system.SetPlayerLocationForTesting("player", "office");
            system.SetActivityForTesting("act", new DetectionSystem.Activity { id = "act", isLegal = false, visualProfile = 0.2f });

            var result = system.CheckDetection("player", "act");
            Assert.IsTrue(result.detected);
        }

        [Test]
        public void CheckDetection_PlayerAtExactVisionRange_Detected()
        {
            RegisterBasicObserver("obs1", DetectionSystem.ObserverRole.Boss, "office", 10f, 90f, true, false);
            system.SetPlayerPositionForTesting("player", new Vector3(0, 0, 10));
            system.SetPlayerLocationForTesting("player", "office");
            system.SetActivityForTesting("act", new DetectionSystem.Activity { id = "act", isLegal = true, visualProfile = 0.1f });

            var result = system.CheckDetection("player", "act");
            Assert.IsTrue(result.detected);
        }

        [Test]
        public void CheckDetection_DifferentLocation_NotDetected()
        {
            RegisterBasicObserver("obs1", DetectionSystem.ObserverRole.Boss, "office", 10f, 90f, true, false);
            system.SetPlayerPositionForTesting("player", new Vector3(0, 0, 5));
            system.SetPlayerLocationForTesting("player", "street");
            system.SetActivityForTesting("act", new DetectionSystem.Activity { id = "act", isLegal = true, visualProfile = 1f });

            var result = system.CheckDetection("player", "act");
            Assert.IsFalse(result.detected);
        }

        [Test]
        public void CheckDetection_ZeroVisualProfile_NeverDetected()
        {
            RegisterBasicObserver("obs1", DetectionSystem.ObserverRole.Boss, "office", 10f, 180f, true, false);
            system.SetPlayerPositionForTesting("player", new Vector3(0, 0, 2));
            system.SetPlayerLocationForTesting("player", "office");
            system.SetActivityForTesting("act", new DetectionSystem.Activity { id = "act", isLegal = true, visualProfile = 0f });

            var result = system.CheckDetection("player", "act");
            Assert.IsFalse(result.detected);
        }

        [Test]
        public void CheckDetection_HighVisualProfile_EasilyDetected()
        {
            RegisterBasicObserver("obs1", DetectionSystem.ObserverRole.Boss, "office", 10f, 90f, true, false);
            system.SetPlayerPositionForTesting("player", new Vector3(0, 0, 9));
            system.SetPlayerLocationForTesting("player", "office");
            system.SetActivityForTesting("act", new DetectionSystem.Activity { id = "act", isLegal = true, visualProfile = 1f });

            var result = system.CheckDetection("player", "act");
            Assert.IsTrue(result.detected);
        }

        [Test]
        public void CheckDetection_AllConditionsMet_ReturnsDetected()
        {
            RegisterBasicObserver("obs1", DetectionSystem.ObserverRole.Cop, "street", 15f, 120f, false, true);
            system.SetPlayerPositionForTesting("player", new Vector3(0, 0, 5));
            system.SetPlayerLocationForTesting("player", "street");
            system.SetActivityForTesting("crime", new DetectionSystem.Activity { id = "crime", isLegal = false, visualProfile = 0.4f });

            var result = system.CheckDetection("player", "crime");
            Assert.IsTrue(result.detected);
        }

        [Test]
        public void CheckDetection_Boss_DetectsSlacking()
        {
            RegisterBasicObserver("boss", DetectionSystem.ObserverRole.Boss, "office", 10f, 90f, true, false);
            system.SetPlayerPositionForTesting("player", new Vector3(0, 0, 5));
            system.SetPlayerLocationForTesting("player", "office");
            system.SetActivityForTesting("slack", new DetectionSystem.Activity { id = "slack", isLegal = true, visualProfile = 0.3f });

            var result = system.CheckDetection("player", "slack");
            Assert.IsTrue(result.detected);
        }

        [Test]
        public void CheckDetection_Cop_DetectsIllegalActivity()
        {
            RegisterBasicObserver("cop", DetectionSystem.ObserverRole.Cop, "street", 12f, 120f, false, true);
            system.SetPlayerPositionForTesting("player", new Vector3(0, 0, 6));
            system.SetPlayerLocationForTesting("player", "street");
            system.SetActivityForTesting("crime", new DetectionSystem.Activity { id = "crime", isLegal = false, visualProfile = 0.3f });

            var result = system.CheckDetection("player", "crime");
            Assert.IsTrue(result.detected);
        }

        [Test]
        public void CheckDetection_Coworker_DoesntCareAboutSlacking()
        {
            RegisterBasicObserver("coworker", DetectionSystem.ObserverRole.Coworker, "office", 10f, 90f, false, false);
            system.SetPlayerPositionForTesting("player", new Vector3(0, 0, 5));
            system.SetPlayerLocationForTesting("player", "office");
            system.SetActivityForTesting("slack", new DetectionSystem.Activity { id = "slack", isLegal = true, visualProfile = 0.8f });

            var result = system.CheckDetection("player", "slack");
            Assert.IsFalse(result.detected);
        }

        [Test]
        public void CheckDetection_Civilian_DoesntCareAboutJobPerformance()
        {
            RegisterBasicObserver("civ", DetectionSystem.ObserverRole.Civilian, "street", 10f, 90f, false, false);
            system.SetPlayerPositionForTesting("player", new Vector3(0, 0, 5));
            system.SetPlayerLocationForTesting("player", "street");
            system.SetActivityForTesting("slack", new DetectionSystem.Activity { id = "slack", isLegal = true, visualProfile = 0.8f });

            var result = system.CheckDetection("player", "slack");
            Assert.IsFalse(result.detected);
        }

        [Test]
        public void CalculateSeverity_CopDetectingCrime_HighSeverity()
        {
            RegisterBasicObserver("cop", DetectionSystem.ObserverRole.Cop, "street", 10f, 90f, false, true);
            var observer = GetObserver("cop");
            var activity = new DetectionSystem.Activity { id = "crime", isLegal = false, visualProfile = 1f };
            float severity = InvokeCalculateSeverity(activity, observer);
            Assert.GreaterOrEqual(severity, 0.9f);
        }

        [Test]
        public void SetPatrolPattern_SetsWaypointsCorrectly()
        {
            system.RegisterObserver("guard", CreateObserverData(DetectionSystem.ObserverRole.Security, "yard"));
            var waypoints = new List<Vector3> { Vector3.zero, new Vector3(5, 0, 0) };
            system.SetPatrolPattern("guard", waypoints, 1f);
            var observer = GetObserver("guard");
            Assert.AreEqual(2, observer.patrolWaypoints.Count);
            Assert.AreEqual(1f, observer.patrolInterval, 0.01f);
        }

        [Test]
        public void Patrol_MovesObserverThroughWaypoints()
        {
            system.RegisterObserver("guard", CreateObserverData(DetectionSystem.ObserverRole.Security, "yard"));
            var waypoints = new List<Vector3> { Vector3.zero, new Vector3(5, 0, 0) };
            system.SetPatrolPattern("guard", waypoints, 1f);
            var observer = GetObserver("guard");
            observer.nextPatrolTime = -1f;

            InvokePrivateMethod("UpdatePatrols");

            var updated = GetObserver("guard");
            Assert.AreEqual(new Vector3(5, 0, 0), updated.position);
        }

        [Test]
        public void Patrol_LoopsBackToFirstWaypoint()
        {
            system.RegisterObserver("guard", CreateObserverData(DetectionSystem.ObserverRole.Security, "yard"));
            var waypoints = new List<Vector3> { Vector3.zero, new Vector3(5, 0, 0) };
            system.SetPatrolPattern("guard", waypoints, 1f);
            var observer = GetObserver("guard");
            observer.nextPatrolTime = -1f;

            InvokePrivateMethod("UpdatePatrols");
            GetObserver("guard").nextPatrolTime = -1f;
            InvokePrivateMethod("UpdatePatrols");

            var updated = GetObserver("guard");
            Assert.AreEqual(Vector3.zero, updated.position);
        }

        [Test]
        public void Patrol_HasTimingVariance()
        {
            system.RegisterObserver("guard", CreateObserverData(DetectionSystem.ObserverRole.Security, "yard"));
            var waypoints = new List<Vector3> { Vector3.zero, new Vector3(5, 0, 0) };
            system.SetPatrolPattern("guard", waypoints, 10f);
            var observer = GetObserver("guard");
            observer.nextPatrolTime = -1f;

            InvokePrivateMethod("UpdatePatrols");
            var updated = GetObserver("guard");
            float expected = 10f;
            Assert.IsTrue(updated.nextPatrolTime != Time.time + expected, "Patrol timing should have variance");
        }

        [Test]
        public void Patrol_UpdatesObserverFacing()
        {
            system.RegisterObserver("guard", CreateObserverData(DetectionSystem.ObserverRole.Security, "yard"));
            var waypoints = new List<Vector3> { Vector3.zero, new Vector3(5, 0, 0) };
            system.SetPatrolPattern("guard", waypoints, 1f);
            var observer = GetObserver("guard");
            observer.nextPatrolTime = -1f;

            InvokePrivateMethod("UpdatePatrols");
            var updated = GetObserver("guard");
            Assert.AreEqual(Vector3.right, updated.facing);
        }

        [Test]
        public void GetDetectionRisk_FarAway_ReturnsLowRisk()
        {
            RegisterBasicObserver("obs1", DetectionSystem.ObserverRole.Boss, "office", 10f, 90f, true, false);
            system.SetPlayerPositionForTesting("player", new Vector3(0, 0, 10));
            system.SetPlayerLocationForTesting("player", "office");
            system.SetActivityForTesting("act", new DetectionSystem.Activity { id = "act", isLegal = true, visualProfile = 1f });

            float risk = system.GetDetectionRisk("player", "act", "office");
            Assert.AreEqual(0f, risk, 0.001f);
        }

        [Test]
        public void GetDetectionRisk_CloseProximity_ReturnsHighRisk()
        {
            RegisterBasicObserver("obs1", DetectionSystem.ObserverRole.Boss, "office", 10f, 90f, true, false);
            system.SetPlayerPositionForTesting("player", new Vector3(0, 0, 1));
            system.SetPlayerLocationForTesting("player", "office");
            system.SetActivityForTesting("act", new DetectionSystem.Activity { id = "act", isLegal = true, visualProfile = 1f });

            float risk = system.GetDetectionRisk("player", "act", "office");
            Assert.Greater(risk, 0.8f);
        }

        [Test]
        public void GetDetectionRisk_MultipleObservers_ReturnsHighestRisk()
        {
            RegisterBasicObserver("obs1", DetectionSystem.ObserverRole.Boss, "office", 10f, 90f, true, false);
            RegisterBasicObserver("obs2", DetectionSystem.ObserverRole.Boss, "office", 10f, 90f, true, false, new Vector3(0, 0, 2));
            system.SetPlayerPositionForTesting("player", new Vector3(0, 0, 1));
            system.SetPlayerLocationForTesting("player", "office");
            system.SetActivityForTesting("act", new DetectionSystem.Activity { id = "act", isLegal = true, visualProfile = 1f });

            float risk = system.GetDetectionRisk("player", "act", "office");
            Assert.Greater(risk, 0.8f);
        }

        [Test]
        public void OnPlayerDetected_WhenDetected_FiresWithCorrectData()
        {
            RegisterBasicObserver("obs1", DetectionSystem.ObserverRole.Boss, "office", 10f, 90f, true, false);
            system.SetPlayerPositionForTesting("player", new Vector3(0, 0, 5));
            system.SetPlayerLocationForTesting("player", "office");
            system.SetActivityForTesting("act", new DetectionSystem.Activity { id = "act", isLegal = true, visualProfile = 0.1f });

            DetectionSystem.DetectionResult captured = new DetectionSystem.DetectionResult();
            system.OnPlayerDetected += result => captured = result;

            system.CheckDetection("player", "act");

            Assert.IsTrue(captured.detected);
            Assert.AreEqual("obs1", captured.observerId);
        }

        [Test]
        public void OnPlayerDetected_WhenNotDetected_DoesNotFire()
        {
            RegisterBasicObserver("obs1", DetectionSystem.ObserverRole.Boss, "office", 5f, 90f, true, false);
            system.SetPlayerPositionForTesting("player", new Vector3(0, 0, 10));
            system.SetPlayerLocationForTesting("player", "office");
            system.SetActivityForTesting("act", new DetectionSystem.Activity { id = "act", isLegal = true, visualProfile = 1f });

            bool fired = false;
            system.OnPlayerDetected += result => fired = true;

            system.CheckDetection("player", "act");
            Assert.IsFalse(fired);
        }

        [Test]
        public void CheckDetection_360VisionCone_DetectsAllDirections()
        {
            RegisterBasicObserver("obs1", DetectionSystem.ObserverRole.Security, "office", 10f, 360f, true, true);
            system.SetPlayerPositionForTesting("player", new Vector3(5, 0, 0));
            system.SetPlayerLocationForTesting("player", "office");
            system.SetActivityForTesting("act", new DetectionSystem.Activity { id = "act", isLegal = false, visualProfile = 1f });

            var result = system.CheckDetection("player", "act");
            Assert.IsTrue(result.detected);
        }

        [Test]
        public void CheckDetection_MultipleObservers_ReturnsFirstDetection()
        {
            RegisterBasicObserver("obs1", DetectionSystem.ObserverRole.Boss, "office", 10f, 90f, true, false, new Vector3(0, 0, 8));
            RegisterBasicObserver("obs2", DetectionSystem.ObserverRole.Boss, "office", 10f, 90f, true, false, new Vector3(0, 0, 5));
            system.SetPlayerPositionForTesting("player", new Vector3(0, 0, 6));
            system.SetPlayerLocationForTesting("player", "office");
            system.SetActivityForTesting("act", new DetectionSystem.Activity { id = "act", isLegal = true, visualProfile = 0.1f });

            var result = system.CheckDetection("player", "act");
            Assert.IsTrue(result.detected);
        }

        [Test]
        public void UnregisterObserver_NonExistent_HandlesGracefully()
        {
            system.UnregisterObserver("missing");
            Assert.Pass();
        }

        private DetectionSystem.ObserverData CreateObserverData(DetectionSystem.ObserverRole role, string location)
        {
            return new DetectionSystem.ObserverData
            {
                role = role,
                position = Vector3.zero,
                facing = Vector3.forward,
                visionRange = 10f,
                visionCone = 90f,
                audioSensitivity = 0f,
                caresAboutLegality = true,
                caresAboutJobPerformance = true,
                currentLocation = location
            };
        }

        private void RegisterBasicObserver(string observerId, DetectionSystem.ObserverRole role, string location, float range, float cone, bool caresJob, bool caresLegality)
        {
            RegisterBasicObserver(observerId, role, location, range, cone, caresJob, caresLegality, Vector3.zero);
        }

        private void RegisterBasicObserver(string observerId, DetectionSystem.ObserverRole role, string location, float range, float cone, bool caresJob, bool caresLegality, Vector3 position)
        {
            var data = new DetectionSystem.ObserverData
            {
                role = role,
                position = position,
                facing = Vector3.forward,
                visionRange = range,
                visionCone = cone,
                audioSensitivity = 0f,
                caresAboutLegality = caresLegality,
                caresAboutJobPerformance = caresJob,
                currentLocation = location
            };
            system.RegisterObserver(observerId, data);
        }

        private bool HasObserver(string observerId)
        {
            return GetObserver(observerId) != null;
        }

        private DetectionSystem.Observer GetObserver(string observerId)
        {
            FieldInfo field = typeof(DetectionSystem).GetField("activeObservers", BindingFlags.NonPublic | BindingFlags.Instance);
            var dict = field.GetValue(system) as Dictionary<string, DetectionSystem.Observer>;
            if (dict != null && dict.TryGetValue(observerId, out DetectionSystem.Observer observer))
            {
                return observer;
            }

            return null;
        }

        private void InvokePrivateMethod(string methodName)
        {
            MethodInfo method = typeof(DetectionSystem).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, $"Method {methodName} should exist");
            method.Invoke(system, null);
        }

        private float InvokeCalculateSeverity(DetectionSystem.Activity activity, DetectionSystem.Observer observer)
        {
            MethodInfo method = typeof(DetectionSystem).GetMethod("CalculateSeverity", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "CalculateSeverity should exist");
            object result = method.Invoke(system, new object[] { activity, observer });
            return (float)result;
        }

        private void ResetSingleton(Type systemType)
        {
            FieldInfo field = systemType.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(null, null);
            }
        }
    }
}
