using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core
{
    public class LocationSystem : MonoBehaviour
    {
        public enum LocationType
        {
            Apartment,
            Office,
            Street,
            Club,
            Store,
            NPCHome,
            Vehicle
        }

        [System.Serializable]
        public struct LocationData
        {
            public string id;
            public string name;
            public LocationType type;

            public List<string> allowedPlayers;
            public bool isPublic;
            public bool requiresInvitation;

            public List<ActivityType> allowedActivities;
            public float detectionSensitivity;

            public List<string> residingNPCs;
            public List<string> patrollingObservers;

            public Vector3 spawnPosition;
            public string sceneName;

            public TimeSpan openTime;
            public TimeSpan closeTime;
        }

        public enum ActivityType
        {
            Sleep,
            Work,
            Stream,
            Intimacy,
            DrugStorage,
            Computer,
            Travel,
            GigWork,
            DrugDeal,
            Shopping,
            Socialize
        }

        private static LocationSystem instance;
        public static LocationSystem Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<LocationSystem>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("LocationSystem");
                        instance = go.AddComponent<LocationSystem>();
                    }
                }
                return instance;
            }
        }

        public event Action<string, string, string> OnLocationChanged;
        public event Action<string, string> OnLocationEntered;
        public event Action<string, string> OnLocationExited;
        public event Action<string> OnLocationLocked;

        private Dictionary<string, LocationData> locations;
        private Dictionary<string, string> playerLocations;
        private Dictionary<string, HashSet<string>> bannedPlayers;

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
            locations = new Dictionary<string, LocationData>();
            playerLocations = new Dictionary<string, string>();
            bannedPlayers = new Dictionary<string, HashSet<string>>();
        }

        public string GetPlayerLocation(string playerId)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                Debug.LogWarning("GetPlayerLocation: playerId is null or empty");
                return null;
            }

            if (playerLocations.TryGetValue(playerId, out string locationId))
            {
                return locationId;
            }

            return null;
        }

        public bool TravelToLocation(string playerId, string locationId)
        {
            if (string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(locationId))
            {
                Debug.LogWarning("TravelToLocation: playerId or locationId is null or empty");
                return false;
            }

            string currentLocation = GetPlayerLocation(playerId);
            if (!CheckLocationAccess(playerId, locationId))
            {
                return false;
            }

            LocationData destination = GetLocationData(locationId);

            float travelTime = CalculateTravelTime(currentLocation, locationId);
            float travelCost = CalculateTravelCost(currentLocation, locationId, playerId);

            bool canAfford = EconomySystem.Instance.DeductExpense(
                playerId,
                travelCost,
                EconomySystem.ExpenseType.Transportation,
                $"Travel to {destination.name}"
            );

            if (!canAfford && travelCost > 0f)
            {
                return false;
            }

            TimeEnergySystem.Instance.AdvanceTime(travelTime);

            if (!string.IsNullOrEmpty(currentLocation))
            {
                OnLocationExited?.Invoke(playerId, currentLocation);
            }

            playerLocations[playerId] = locationId;

            LoadLocation(destination);

            OnLocationEntered?.Invoke(playerId, locationId);
            OnLocationChanged?.Invoke(playerId, currentLocation, locationId);

            return true;
        }

        public LocationData GetLocationData(string locationId)
        {
            if (string.IsNullOrEmpty(locationId))
            {
                Debug.LogWarning("GetLocationData: locationId is null or empty");
                return default;
            }

            if (locations.TryGetValue(locationId, out LocationData location))
            {
                return location;
            }

            Debug.LogWarning($"GetLocationData: Location {locationId} not found");
            return default;
        }

        public bool CheckLocationAccess(string playerId, string locationId)
        {
            LocationData location = GetLocationData(locationId);
            if (string.IsNullOrEmpty(location.id))
            {
                return false;
            }

            if (location.isPublic)
            {
                if (IsBanned(playerId, locationId))
                {
                    return false;
                }

                if (!IsOpen(locationId))
                {
                    return false;
                }

                return true;
            }

            if (location.allowedPlayers != null && location.allowedPlayers.Contains(playerId))
            {
                return true;
            }

            if (location.requiresInvitation)
            {
                return HasInvitation(playerId, locationId);
            }

            return false;
        }

        public List<ActivityType> GetAllowedActivities(string locationId)
        {
            LocationData location = GetLocationData(locationId);
            return location.allowedActivities != null ? new List<ActivityType>(location.allowedActivities) : new List<ActivityType>();
        }

        public float GetDetectionSensitivity(string locationId)
        {
            LocationData location = GetLocationData(locationId);
            return location.detectionSensitivity;
        }

        public List<string> GetNPCsInLocation(string locationId)
        {
            LocationData location = GetLocationData(locationId);
            return location.residingNPCs != null ? new List<string>(location.residingNPCs) : new List<string>();
        }

        public void AddNPCToLocation(string npcId, string locationId)
        {
            LocationData location = GetLocationData(locationId);
            if (string.IsNullOrEmpty(location.id))
            {
                return;
            }

            if (location.residingNPCs == null)
            {
                location.residingNPCs = new List<string>();
            }

            if (!location.residingNPCs.Contains(npcId))
            {
                location.residingNPCs.Add(npcId);
                locations[locationId] = location;
            }
        }

        public void RemoveNPCFromLocation(string npcId, string locationId)
        {
            LocationData location = GetLocationData(locationId);
            if (string.IsNullOrEmpty(location.id) || location.residingNPCs == null)
            {
                return;
            }

            location.residingNPCs.Remove(npcId);
            locations[locationId] = location;
        }

        public void CreateLocation(LocationData location)
        {
            if (string.IsNullOrEmpty(location.id))
            {
                Debug.LogWarning("CreateLocation: location id is null or empty");
                return;
            }

            locations[location.id] = location;
        }

        public void SetPlayerLocationForTesting(string playerId, string locationId)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                return;
            }

            playerLocations[playerId] = locationId;
        }

        public void AddBannedPlayerForTesting(string playerId, string locationId)
        {
            if (string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(locationId))
            {
                return;
            }

            if (!bannedPlayers.ContainsKey(locationId))
            {
                bannedPlayers[locationId] = new HashSet<string>();
            }

            bannedPlayers[locationId].Add(playerId);
        }

        public void TriggerLocationLockedForTesting(string locationId)
        {
            OnLocationLocked?.Invoke(locationId);
        }

        private float CalculateTravelTime(string from, string to)
        {
            return 15f;
        }

        private float CalculateTravelCost(string from, string to, string playerId)
        {
            bool hasVehicle = false;
            if (hasVehicle)
            {
                return 5f;
            }

            return 15f;
        }

        private bool IsOpen(string locationId)
        {
            LocationData location = GetLocationData(locationId);
            if (string.IsNullOrEmpty(location.id))
            {
                return false;
            }

            if (location.openTime == TimeSpan.Zero && location.closeTime == TimeSpan.Zero)
            {
                return true;
            }

            TimeSpan currentTime = TimeEnergySystem.Instance.GetCurrentTime().TimeOfDay;
            return currentTime >= location.openTime && currentTime < location.closeTime;
        }

        private bool IsBanned(string playerId, string locationId)
        {
            if (bannedPlayers.TryGetValue(locationId, out HashSet<string> banned))
            {
                return banned.Contains(playerId);
            }

            return false;
        }

        private bool HasInvitation(string playerId, string locationId)
        {
            Debug.LogWarning("TODO: Invitation system not implemented");
            return false;
        }

        private void LoadLocation(LocationData location)
        {
            if (string.IsNullOrEmpty(location.sceneName))
            {
                Debug.LogWarning("LoadLocation: sceneName not set");
                return;
            }

            Debug.Log($"LoadLocation: {location.sceneName}");
        }
    }
}
