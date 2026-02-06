using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Core
{
    public class ReputationSystem : MonoBehaviour
    {
        public enum ReputationTrack
        {
            Legal,
            Criminal,
            Professional,
            Social
        }

        public enum ThresholdComparison
        {
            GreaterThan,
            LessThan,
            EqualOrGreater,
            EqualOrLess
        }

        [System.Serializable]
        public class ReputationModifier
        {
            public string id;
            public ReputationTrack track;
            public float value;
            public DateTime expiresAt;
            public string description;
            public bool isPermanent;
        }

        [System.Serializable]
        public struct ReputationChange
        {
            public DateTime timestamp;
            public ReputationTrack track;
            public float delta;
            public string reason;
        }

        [System.Serializable]
        public class ReputationProfile
        {
            public string playerId;
            public Dictionary<ReputationTrack, float> baseScores;
            public Dictionary<string, ReputationModifier> activeModifiers;
            public List<ReputationChange> history;
        }

        private static ReputationSystem instance;
        public static ReputationSystem Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<ReputationSystem>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("ReputationSystem");
                        instance = go.AddComponent<ReputationSystem>();
                    }
                }
                return instance;
            }
        }

        public event Action<string, ReputationTrack, float, float> OnReputationChanged;
        public event Action<string, ReputationTrack, float> OnThresholdCrossed;

        private Dictionary<string, ReputationProfile> profiles;
        private float updateInterval = 1f;
        private float timeSinceUpdate = 0f;

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
            profiles = new Dictionary<string, ReputationProfile>();
            timeSinceUpdate = 0f;
        }

        private void Update()
        {
            timeSinceUpdate += Time.deltaTime;
            if (timeSinceUpdate >= updateInterval)
            {
                timeSinceUpdate = 0f;
                CheckExpiredModifiers();
            }
        }

        public float GetReputation(string playerId, ReputationTrack track)
        {
            ReputationProfile profile = GetOrCreateProfile(playerId);
            float baseScore = profile.baseScores[track];
            float modifierSum = profile.activeModifiers.Values
                .Where(mod => mod.track == track)
                .Sum(mod => mod.value);
            return Mathf.Clamp(baseScore + modifierSum, 0f, 100f);
        }

        public void ModifyReputation(string playerId, ReputationTrack track, float delta, string reason)
        {
            float oldValue = GetReputation(playerId, track);

            ReputationProfile profile = GetOrCreateProfile(playerId);
            profile.baseScores[track] = Mathf.Clamp(profile.baseScores[track] + delta, 0f, 100f);

            profile.history.Add(new ReputationChange
            {
                timestamp = TimeEnergySystem.Instance.GetCurrentTime(),
                track = track,
                delta = delta,
                reason = reason
            });

            float newValue = GetReputation(playerId, track);

            CheckThresholdCrossings(playerId, track, oldValue, newValue);
            OnReputationChanged?.Invoke(playerId, track, oldValue, newValue);
        }

        public void ModifyMultipleReputations(string playerId, Dictionary<ReputationTrack, float> changes, string reason)
        {
            if (changes == null)
            {
                return;
            }

            foreach (KeyValuePair<ReputationTrack, float> change in changes)
            {
                ModifyReputation(playerId, change.Key, change.Value, reason);
            }
        }

        public bool CheckThreshold(string playerId, ReputationTrack track, float threshold, ThresholdComparison comparison)
        {
            float rep = GetReputation(playerId, track);

            switch (comparison)
            {
                case ThresholdComparison.GreaterThan:
                    return rep > threshold;
                case ThresholdComparison.LessThan:
                    return rep < threshold;
                case ThresholdComparison.EqualOrGreater:
                    return rep >= threshold;
                case ThresholdComparison.EqualOrLess:
                    return rep <= threshold;
                default:
                    return false;
            }
        }

        public Dictionary<string, float> GetReputationModifiers(string playerId, ReputationTrack track)
        {
            ReputationProfile profile = GetOrCreateProfile(playerId);
            return profile.activeModifiers.Values
                .Where(mod => mod.track == track)
                .ToDictionary(mod => mod.description, mod => mod.value);
        }

        public string AddTemporaryModifier(string playerId, ReputationTrack track, float modifier, float durationDays, string description)
        {
            ReputationProfile profile = GetOrCreateProfile(playerId);
            float oldValue = GetReputation(playerId, track);

            bool isPermanent = durationDays <= 0f;
            DateTime expiresAt = isPermanent
                ? DateTime.MaxValue
                : TimeEnergySystem.Instance.GetCurrentTime().AddDays(durationDays);

            string id = Guid.NewGuid().ToString("N");
            ReputationModifier mod = new ReputationModifier
            {
                id = id,
                track = track,
                value = modifier,
                expiresAt = expiresAt,
                description = description,
                isPermanent = isPermanent
            };

            profile.activeModifiers[id] = mod;

            float newValue = GetReputation(playerId, track);
            OnReputationChanged?.Invoke(playerId, track, oldValue, newValue);

            return id;
        }

        public bool RemoveModifier(string playerId, string modifierId)
        {
            ReputationProfile profile = GetOrCreateProfile(playerId);
            if (!profile.activeModifiers.ContainsKey(modifierId))
            {
                Debug.LogWarning($"RemoveModifier: Modifier {modifierId} not found");
                return false;
            }

            ReputationModifier mod = profile.activeModifiers[modifierId];
            float oldValue = GetReputation(playerId, mod.track);

            profile.activeModifiers.Remove(modifierId);

            float newValue = GetReputation(playerId, mod.track);
            OnReputationChanged?.Invoke(playerId, mod.track, oldValue, newValue);
            return true;
        }

        private ReputationProfile GetOrCreateProfile(string playerId)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                Debug.LogWarning("GetOrCreateProfile: playerId is null or empty");
                playerId = "player";
            }

            if (!profiles.TryGetValue(playerId, out ReputationProfile profile))
            {
                profile = new ReputationProfile
                {
                    playerId = playerId,
                    baseScores = new Dictionary<ReputationTrack, float>
                    {
                        { ReputationTrack.Legal, 80f },
                        { ReputationTrack.Criminal, 0f },
                        { ReputationTrack.Professional, 50f },
                        { ReputationTrack.Social, 50f }
                    },
                    activeModifiers = new Dictionary<string, ReputationModifier>(),
                    history = new List<ReputationChange>()
                };
                profiles[playerId] = profile;
            }

            return profile;
        }

        private void CheckExpiredModifiers()
        {
            DateTime now = TimeEnergySystem.Instance.GetCurrentTime();
            foreach (ReputationProfile profile in profiles.Values)
            {
                List<ReputationModifier> expired = profile.activeModifiers.Values
                    .Where(mod => !mod.isPermanent && mod.expiresAt <= now)
                    .ToList();

                foreach (ReputationModifier mod in expired)
                {
                    float oldValue = GetReputation(profile.playerId, mod.track);
                    profile.activeModifiers.Remove(mod.id);
                    float newValue = GetReputation(profile.playerId, mod.track);
                    OnReputationChanged?.Invoke(profile.playerId, mod.track, oldValue, newValue);
                }
            }
        }

        private void CheckThresholdCrossings(string playerId, ReputationTrack track, float oldValue, float newValue)
        {
            float[] thresholds = { 20f, 30f, 50f, 70f, 80f };
            foreach (float threshold in thresholds)
            {
                bool crossedUp = oldValue < threshold && newValue >= threshold;
                bool crossedDown = oldValue >= threshold && newValue < threshold;
                if (crossedUp || crossedDown)
                {
                    OnThresholdCrossed?.Invoke(playerId, track, threshold);
                }
            }
        }
    }
}
