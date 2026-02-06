using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core
{
    public class IntoxicationSystem : MonoBehaviour
    {
        public enum IntoxicationType
        {
            Alcohol,
            Cannabis,
            Stimulant,
            Depressant,
            Psychedelic
        }

        public enum ImpairmentType
        {
            Driving,
            Coordination,
            Judgment,
            Perception
        }

        [System.Serializable]
        public struct IntoxicationState
        {
            public float level;
            public Dictionary<IntoxicationType, float> byType;
            public float peakLevel;
            public DateTime lastConsumption;
            public bool hasLicense;
        }

        [System.Serializable]
        public struct ConsumableItem
        {
            public string id;
            public IntoxicationType type;
            public float intoxicationIncrease;
            public float duration;
        }

        private static IntoxicationSystem instance;
        public static IntoxicationSystem Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<IntoxicationSystem>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("IntoxicationSystem");
                        instance = go.AddComponent<IntoxicationSystem>();
                    }
                }
                return instance;
            }
        }

        public event Action<float> OnIntoxicationChanged;
        public event Action<IntoxicationType> OnConsumed;
        public event Action OnBlackout;
        public event Action<string> OnDUIArrest;
        public event Action OnSobrietyAchieved;

        private IntoxicationState intoxicationState;
        private Dictionary<string, ConsumableItem> consumables;
        private float updateInterval = 1f;
        private float timeSinceUpdate = 0f;
        private string playerId = "player";
        private bool isDriving = true;
        private bool forceDuiCatch = false;
        private bool useForcedDuiCatch = false;

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
            intoxicationState = new IntoxicationState
            {
                level = 0f,
                byType = new Dictionary<IntoxicationType, float>(),
                peakLevel = 0f,
                lastConsumption = TimeEnergySystem.Instance.GetCurrentTime(),
                hasLicense = true
            };
            consumables = new Dictionary<string, ConsumableItem>();
            timeSinceUpdate = 0f;
        }

        private void Update()
        {
            timeSinceUpdate += Time.deltaTime;
            if (timeSinceUpdate >= updateInterval)
            {
                timeSinceUpdate = 0f;
                ProcessMetabolism(Time.deltaTime / 60f);
            }
        }

        public void ModifyIntoxication(float delta, IntoxicationType type)
        {
            float oldLevel = intoxicationState.level;
            intoxicationState.level = Mathf.Clamp01(intoxicationState.level + delta);

            if (!intoxicationState.byType.ContainsKey(type))
            {
                intoxicationState.byType[type] = 0f;
            }
            intoxicationState.byType[type] += delta;

            if (intoxicationState.level > intoxicationState.peakLevel)
            {
                intoxicationState.peakLevel = intoxicationState.level;
            }

            intoxicationState.lastConsumption = TimeEnergySystem.Instance.GetCurrentTime();

            if (intoxicationState.level >= 0.9f && oldLevel < 0.9f)
            {
                OnBlackout?.Invoke();
            }

            OnIntoxicationChanged?.Invoke(intoxicationState.level);
        }

        public float GetIntoxicationLevel()
        {
            return intoxicationState.level;
        }

        public float GetImpairmentLevel(ImpairmentType type)
        {
            float baseImpairment = 1.0f - intoxicationState.level;
            switch (type)
            {
                case ImpairmentType.Driving:
                    return Mathf.Clamp01(1.0f - (intoxicationState.level * 1.5f));
                case ImpairmentType.Judgment:
                    return Mathf.Clamp01(1.0f - (intoxicationState.level * 1.2f));
                case ImpairmentType.Coordination:
                case ImpairmentType.Perception:
                default:
                    return baseImpairment;
            }
        }

        public void Consume(string itemId, float amount)
        {
            if (string.IsNullOrEmpty(itemId) || amount <= 0f)
            {
                return;
            }

            ConsumableItem item = GetConsumableItem(itemId);
            float intoxIncrease = item.intoxicationIncrease * amount;

            float oldLevel = intoxicationState.level;
            intoxicationState.level = Mathf.Clamp01(intoxicationState.level + intoxIncrease);

            if (!intoxicationState.byType.ContainsKey(item.type))
            {
                intoxicationState.byType[item.type] = 0f;
            }
            intoxicationState.byType[item.type] += intoxIncrease;

            intoxicationState.peakLevel = Mathf.Max(intoxicationState.peakLevel, intoxicationState.level);
            intoxicationState.lastConsumption = TimeEnergySystem.Instance.GetCurrentTime();

            if (intoxicationState.level >= 0.9f && oldLevel < 0.9f)
            {
                OnBlackout?.Invoke();
            }

            OnConsumed?.Invoke(item.type);
            OnIntoxicationChanged?.Invoke(intoxicationState.level);
        }

        public bool CheckDUI(string observerId)
        {
            const float legalLimit = 0.08f;
            if (!isDriving)
            {
                return false;
            }

            if (intoxicationState.level < legalLimit)
            {
                return false;
            }

            float detectionChance = (intoxicationState.level - legalLimit) * 5f;
            bool caught = useForcedDuiCatch ? forceDuiCatch : UnityEngine.Random.value < detectionChance;

            if (caught)
            {
                OnDUIArrest?.Invoke(observerId);
                ApplyDUIConsequences();
                return true;
            }

            return false;
        }

        public void CreateConsumableItem(ConsumableItem item)
        {
            if (string.IsNullOrEmpty(item.id))
            {
                return;
            }

            consumables[item.id] = item;
        }

        public void SetIntoxicationLevelForTesting(float level)
        {
            intoxicationState.level = Mathf.Clamp01(level);
        }

        public void SetLicenseStatusForTesting(bool hasLicense)
        {
            intoxicationState.hasLicense = hasLicense;
        }

        public void SetDrivingStatusForTesting(bool driving)
        {
            isDriving = driving;
        }

        public void SetDuiCatchResultForTesting(bool caught)
        {
            useForcedDuiCatch = true;
            forceDuiCatch = caught;
        }

        public void ClearDuiCatchOverrideForTesting()
        {
            useForcedDuiCatch = false;
        }

        public void ProcessMetabolismForTesting(float gameHours)
        {
            ProcessMetabolism(gameHours);
        }

        private void ProcessMetabolism(float gameHours)
        {
            if (intoxicationState.level <= 0f)
            {
                return;
            }

            float previousLevel = intoxicationState.level;
            float metabolismRate = 0.02f;
            intoxicationState.level = Mathf.Max(0f, intoxicationState.level - (metabolismRate * gameHours));

            if (intoxicationState.level <= 0f && previousLevel > 0f)
            {
                OnSobrietyAchieved?.Invoke();
            }

            if (!Mathf.Approximately(previousLevel, intoxicationState.level))
            {
                OnIntoxicationChanged?.Invoke(intoxicationState.level);
            }
        }

        private void ApplyDUIConsequences()
        {
            float fine = UnityEngine.Random.Range(1000f, 5000f);
            EconomySystem.Instance.DeductExpense(
                playerId,
                fine,
                EconomySystem.ExpenseType.Fine,
                "DUI fine"
            );

            Debug.LogWarning("TODO: CriminalRecordSystem AddOffense (DUI)");

            intoxicationState.hasLicense = false;

            ReputationSystem.Instance.ModifyReputation(
                playerId,
                ReputationSystem.ReputationTrack.Legal,
                -15f,
                "DUI arrest"
            );

            TimeEnergySystem.Instance.AdvanceTime(24f * 60f);
        }

        private ConsumableItem GetConsumableItem(string itemId)
        {
            if (consumables.TryGetValue(itemId, out ConsumableItem item))
            {
                return item;
            }

            return new ConsumableItem
            {
                id = itemId,
                type = IntoxicationType.Alcohol,
                intoxicationIncrease = 0.05f,
                duration = 2f
            };
        }
    }
}
