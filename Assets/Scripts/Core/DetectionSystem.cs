using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core
{
    public class DetectionSystem : MonoBehaviour
    {
        public enum ObserverRole
        {
            Boss,
            Cop,
            Coworker,
            Security,
            Civilian
        }

        public enum DetectionReason
        {
            LineOfSight,
            Audio,
            Evidence,
            Witness,
            None
        }

        [System.Serializable]
        public class Observer
        {
            public string id;
            public ObserverRole role;
            public Vector3 position;
            public Vector3 facing;
            public float visionRange;
            public float visionCone;
            public float audioSensitivity;
            public bool caresAboutLegality;
            public bool caresAboutJobPerformance;
            public string currentLocation;
            public List<Vector3> patrolWaypoints;
            public int currentWaypointIndex;
            public float nextPatrolTime;
            public float patrolInterval;
        }

        [System.Serializable]
        public struct ObserverData
        {
            public ObserverRole role;
            public Vector3 position;
            public Vector3 facing;
            public float visionRange;
            public float visionCone;
            public float audioSensitivity;
            public bool caresAboutLegality;
            public bool caresAboutJobPerformance;
            public string currentLocation;
        }

        [System.Serializable]
        public struct DetectionResult
        {
            public bool detected;
            public string observerId;
            public float severity;
            public string activityId;
            public DetectionReason reason;
        }

        [System.Serializable]
        public struct Activity
        {
            public string id;
            public bool isLegal;
            public float visualProfile;
        }

        private static DetectionSystem instance;
        public static DetectionSystem Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<DetectionSystem>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("DetectionSystem");
                        instance = go.AddComponent<DetectionSystem>();
                    }
                }
                return instance;
            }
        }

        public event Action<DetectionResult> OnPlayerDetected;
        public event Action<string, float> OnDetectionRisk;

        private Dictionary<string, Observer> activeObservers;
        private Dictionary<string, Vector3> testPlayerPositions;
        private Dictionary<string, string> testPlayerLocations;
        private Dictionary<string, Activity> testActivities;

        private float updateInterval = 0.1f;
        private float timeSinceUpdate = 0f;
        private float patrolFrequencyMultiplier = 1f;
        private float detectionSensitivityMultiplier = 1f;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }

        private void Initialize()
        {
            activeObservers = new Dictionary<string, Observer>();
            testPlayerPositions = new Dictionary<string, Vector3>();
            testPlayerLocations = new Dictionary<string, string>();
            testActivities = new Dictionary<string, Activity>();
        }

        private void Update()
        {
            timeSinceUpdate += Time.deltaTime;
            if (timeSinceUpdate >= updateInterval)
            {
                timeSinceUpdate = 0f;
                UpdatePatrols();
            }
        }

        public void RegisterObserver(string observerId, ObserverData data)
        {
            if (string.IsNullOrEmpty(observerId))
            {
                Debug.LogWarning("RegisterObserver: observerId is null or empty");
                return;
            }

            Observer observer = new Observer
            {
                id = observerId,
                role = data.role,
                position = data.position,
                facing = data.facing,
                visionRange = data.visionRange,
                visionCone = data.visionCone,
                audioSensitivity = data.audioSensitivity,
                caresAboutLegality = data.caresAboutLegality,
                caresAboutJobPerformance = data.caresAboutJobPerformance,
                currentLocation = data.currentLocation,
                patrolWaypoints = new List<Vector3>(),
                currentWaypointIndex = 0,
                nextPatrolTime = 0f,
                patrolInterval = 0f
            };

            activeObservers[observerId] = observer;
        }

        public void UnregisterObserver(string observerId)
        {
            if (string.IsNullOrEmpty(observerId))
            {
                Debug.LogWarning("UnregisterObserver: observerId is null or empty");
                return;
            }

            activeObservers.Remove(observerId);
        }

        public void UpdateObserverPosition(string observerId, Vector3 position, Vector3 facing)
        {
            if (!activeObservers.TryGetValue(observerId, out Observer observer))
            {
                Debug.LogWarning($"UpdateObserverPosition: Observer {observerId} not found");
                return;
            }

            observer.position = position;
            observer.facing = facing;
        }

        public DetectionResult CheckDetection(string playerId, string activityId)
        {
            Activity activity = GetActivity(activityId);
            Vector3 playerPos = GetPlayerPosition(playerId);
            string playerLocation = GetPlayerLocation(playerId);

            foreach (Observer observer in activeObservers.Values)
            {
                if (observer.currentLocation != playerLocation)
                {
                    continue;
                }

                float distance = Vector3.Distance(observer.position, playerPos);
                if (distance > observer.visionRange)
                {
                    continue;
                }

                if (!HasLineOfSight(observer.position, playerPos))
                {
                    continue;
                }

                Vector3 toPlayer = (playerPos - observer.position).normalized;
                float angle = Vector3.Angle(observer.facing, toPlayer);
                if (angle > observer.visionCone / 2f)
                {
                    continue;
                }

                if (activity.isLegal && !observer.caresAboutJobPerformance)
                {
                    continue;
                }

                if (!activity.isLegal && !observer.caresAboutLegality)
                {
                    continue;
                }

                if (activity.visualProfile <= 0f)
                {
                    continue;
                }

                float observerAwareness = observer.visionRange / Mathf.Max(distance, 0.001f);
                float detectionThreshold = activity.visualProfile;

                if ((observerAwareness * detectionSensitivityMultiplier) >= detectionThreshold)
                {
                    DetectionResult result = new DetectionResult
                    {
                        detected = true,
                        observerId = observer.id,
                        severity = CalculateSeverity(activity, observer),
                        activityId = activityId,
                        reason = DetectionReason.LineOfSight
                    };

                    OnPlayerDetected?.Invoke(result);
                    return result;
                }
            }

            return new DetectionResult
            {
                detected = false,
                observerId = null,
                severity = 0f,
                activityId = activityId,
                reason = DetectionReason.None
            };
        }

        public float GetDetectionRisk(string playerId, string activityId, string locationId)
        {
            Activity activity = GetActivity(activityId);
            Vector3 playerPos = GetPlayerPosition(playerId);
            float maxRisk = 0f;

            foreach (Observer observer in activeObservers.Values)
            {
                if (observer.currentLocation != locationId)
                {
                    continue;
                }

                float distance = Vector3.Distance(observer.position, playerPos);
                if (distance > observer.visionRange)
                {
                    continue;
                }

                if (activity.isLegal && !observer.caresAboutJobPerformance)
                {
                    continue;
                }

                if (!activity.isLegal && !observer.caresAboutLegality)
                {
                    continue;
                }

                float proximityRisk = 1f - (distance / observer.visionRange);
                float awarenessRisk = activity.visualProfile;
                float risk = proximityRisk * awarenessRisk * detectionSensitivityMultiplier;

                if (risk > maxRisk)
                {
                    maxRisk = risk;
                }
            }

            float clampedRisk = Mathf.Clamp01(maxRisk);
            OnDetectionRisk?.Invoke(playerId, clampedRisk);
            return clampedRisk;
        }

        public void SetPatrolPattern(string observerId, List<Vector3> waypoints, float intervalSeconds)
        {
            if (!activeObservers.TryGetValue(observerId, out Observer observer))
            {
                Debug.LogWarning($"SetPatrolPattern: Observer {observerId} not found");
                return;
            }

            observer.patrolWaypoints = waypoints != null ? new List<Vector3>(waypoints) : new List<Vector3>();
            observer.currentWaypointIndex = 0;
            observer.patrolInterval = intervalSeconds;
            observer.nextPatrolTime = Time.time + intervalSeconds;
        }

        public void SetPatrolFrequency(float multiplier)
        {
            patrolFrequencyMultiplier = Mathf.Max(0.01f, multiplier);
        }

        public void SetDetectionSensitivity(float multiplier)
        {
            detectionSensitivityMultiplier = Mathf.Max(0.01f, multiplier);
        }

        public void SetPlayerPositionForTesting(string playerId, Vector3 position)
        {
            testPlayerPositions[playerId] = position;
        }

        public void SetPlayerLocationForTesting(string playerId, string location)
        {
            testPlayerLocations[playerId] = location;
        }

        public void SetActivityForTesting(string activityId, Activity activity)
        {
            testActivities[activityId] = activity;
        }

        private void UpdatePatrols()
        {
            float currentTime = Time.time;
            foreach (Observer observer in activeObservers.Values)
            {
                if (observer.patrolWaypoints == null || observer.patrolWaypoints.Count == 0)
                {
                    continue;
                }

                if (currentTime >= observer.nextPatrolTime)
                {
                    observer.currentWaypointIndex = (observer.currentWaypointIndex + 1) % observer.patrolWaypoints.Count;
                    Vector3 nextWaypoint = observer.patrolWaypoints[observer.currentWaypointIndex];
                    Vector3 direction = (nextWaypoint - observer.position).normalized;
                    observer.position = nextWaypoint;
                    if (direction != Vector3.zero)
                    {
                        observer.facing = direction;
                    }

                    float interval = observer.patrolInterval;
                    if (patrolFrequencyMultiplier > 0f)
                    {
                        interval /= patrolFrequencyMultiplier;
                    }
                    float variance = interval * 0.1f;
                    observer.nextPatrolTime = currentTime + interval + UnityEngine.Random.Range(-variance, variance);
                }
            }
        }

        private bool HasLineOfSight(Vector3 from, Vector3 to)
        {
            Vector3 direction = to - from;
            float distance = direction.magnitude;

            if (Physics.Raycast(from, direction.normalized, out RaycastHit hit, distance))
            {
                return hit.collider != null && hit.collider.CompareTag("Player");
            }

            return true;
        }

        private float CalculateSeverity(Activity activity, Observer observer)
        {
            float severity = 0.5f;
            if (!activity.isLegal)
            {
                severity += 0.3f;
            }

            if (observer.role == ObserverRole.Cop && !activity.isLegal)
            {
                severity += 0.2f;
            }

            if (observer.role == ObserverRole.Boss && observer.caresAboutJobPerformance)
            {
                severity += 0.2f;
            }

            return Mathf.Clamp01(severity);
        }

        private Vector3 GetPlayerPosition(string playerId)
        {
            if (!string.IsNullOrEmpty(playerId) && testPlayerPositions.TryGetValue(playerId, out Vector3 position))
            {
                return position;
            }

            return Vector3.zero;
        }

        private string GetPlayerLocation(string playerId)
        {
            if (!string.IsNullOrEmpty(playerId) && testPlayerLocations.TryGetValue(playerId, out string location))
            {
                return location;
            }

            return "default_location";
        }

        private Activity GetActivity(string activityId)
        {
            if (!string.IsNullOrEmpty(activityId) && testActivities.TryGetValue(activityId, out Activity activity))
            {
                return activity;
            }

            return new Activity
            {
                id = activityId,
                isLegal = true,
                visualProfile = 0.5f
            };
        }
    }
}
