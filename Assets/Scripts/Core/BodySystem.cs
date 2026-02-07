using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core
{
    public class BodySystem : MonoBehaviour
    {
        public enum BodyType
        {
            Slim,
            Athletic,
            Average,
            Curvy,
            Heavy
        }

        public enum GroomingLevel
        {
            Unkempt,
            Basic,
            WellGroomed,
            Professional,
            Glamorous
        }

        [System.Serializable]
        public struct BodyState
        {
            public BodyType bodyType;
            public float fitness;
            public GroomingLevel grooming;
            public float attractiveness;
            public float energyMax;
            public float groomingCost;
        }

        private static BodySystem instance;
        public static BodySystem Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<BodySystem>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("BodySystem");
                        instance = go.AddComponent<BodySystem>();
                    }
                }
                return instance;
            }
        }

        public event Action<BodyType> OnBodyTypeChanged;
        public event Action<float> OnFitnessChanged;
        public event Action<GroomingLevel> OnGroomingChanged;
        public event Action<float> OnAttractivenessChanged;

        private BodyState bodyState;
        private string playerId = "player";
        private float updateInterval = 1f;
        private float timeSinceUpdate = 0f;
        private Dictionary<string, Dictionary<BodyType, float>> npcPreferenceOverrides;
        private float forcedClothingVanity = 0f;
        private bool useForcedClothingVanity = false;

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
            bodyState = new BodyState
            {
                bodyType = BodyType.Average,
                fitness = 50f,
                grooming = GroomingLevel.Basic,
                attractiveness = 50f,
                energyMax = 110f,
                groomingCost = GetMaintenanceCost(GroomingLevel.Basic)
            };
            npcPreferenceOverrides = new Dictionary<string, Dictionary<BodyType, float>>();
            RecalculateDerivedStats(null);
        }

        private void Update()
        {
            timeSinceUpdate += Time.deltaTime;
            if (timeSinceUpdate >= updateInterval)
            {
                timeSinceUpdate = 0f;
                float gameHoursPassed = Time.deltaTime / 60f;
                ProcessFitnessDecay(gameHoursPassed);
            }
        }

        public void SetBodyType(BodyType type)
        {
            if (bodyState.bodyType == type)
            {
                return;
            }

            bodyState.bodyType = type;
            RecalculateDerivedStats(null);
            OnBodyTypeChanged?.Invoke(type);
        }

        public void ModifyFitness(float delta)
        {
            float oldFitness = bodyState.fitness;
            bodyState.fitness = Mathf.Clamp(bodyState.fitness + delta, 0f, 100f);

            if (!Mathf.Approximately(oldFitness, bodyState.fitness))
            {
                UpdateBodyTypeFromFitness();
                RecalculateDerivedStats(null);
                OnFitnessChanged?.Invoke(bodyState.fitness);
            }
        }

        public void SetGrooming(GroomingLevel level)
        {
            GroomingLevel oldLevel = bodyState.grooming;
            bodyState.grooming = level;

            float cost = GetGroomingCost(level);
            EconomySystem.Instance.DeductExpense(
                playerId,
                cost,
                EconomySystem.ExpenseType.Personal,
                $"Grooming upgrade to {level}"
            );

            bodyState.groomingCost = GetMaintenanceCost(level);
            ApplyGroomingReputationEffects(oldLevel, level);

            RecalculateDerivedStats(null);
            OnGroomingChanged?.Invoke(level);
        }

        public float GetAttractiveness(string npcId = null)
        {
            float attractiveness = CalculateAttractiveness(npcId);
            return attractiveness;
        }

        public float GetEnergyMax()
        {
            return bodyState.energyMax;
        }

        public void SetPlayerIdForTesting(string id)
        {
            playerId = string.IsNullOrEmpty(id) ? "player" : id;
        }

        public void SetFitnessForTesting(float fitness)
        {
            bodyState.fitness = Mathf.Clamp(fitness, 0f, 100f);
            UpdateBodyTypeFromFitness();
            RecalculateDerivedStats(null);
        }

        public void SetBodyTypeForTesting(BodyType type)
        {
            bodyState.bodyType = type;
            RecalculateDerivedStats(null);
        }

        public void SetGroomingForTesting(GroomingLevel level)
        {
            bodyState.grooming = level;
            bodyState.groomingCost = GetMaintenanceCost(level);
            RecalculateDerivedStats(null);
        }

        public void SetNpcPreferenceForTesting(string npcId, BodyType type, float preference)
        {
            if (!npcPreferenceOverrides.ContainsKey(npcId))
            {
                npcPreferenceOverrides[npcId] = new Dictionary<BodyType, float>();
            }

            npcPreferenceOverrides[npcId][type] = Mathf.Clamp01(preference);
        }

        public void SetClothingVanityForTesting(float vanityValue)
        {
            useForcedClothingVanity = true;
            forcedClothingVanity = vanityValue;
            RecalculateDerivedStats(null);
        }

        public void ClearClothingVanityOverrideForTesting()
        {
            useForcedClothingVanity = false;
            forcedClothingVanity = 0f;
            RecalculateDerivedStats(null);
        }

        public void ProcessFitnessDecayForTesting(float gameHoursPassed)
        {
            ProcessFitnessDecay(gameHoursPassed);
        }

        public void ApplyGroomingMaintenanceForTesting()
        {
            ApplyGroomingMaintenance();
        }

        public BodyState GetStateForTesting()
        {
            return bodyState;
        }

        private void ProcessFitnessDecay(float gameHoursPassed)
        {
            if (gameHoursPassed <= 0f)
            {
                return;
            }

            const float decayRatePerDay = 0.5f;
            float decayAmount = decayRatePerDay * (gameHoursPassed / 24f);
            if (decayAmount <= 0f)
            {
                return;
            }

            float oldFitness = bodyState.fitness;
            bodyState.fitness = Mathf.Max(0f, bodyState.fitness - decayAmount);

            if (!Mathf.Approximately(oldFitness, bodyState.fitness))
            {
                UpdateBodyTypeFromFitness();
                RecalculateDerivedStats(null);
                OnFitnessChanged?.Invoke(bodyState.fitness);
            }
        }

        private void UpdateBodyTypeFromFitness()
        {
            BodyType newType = bodyState.bodyType;

            if (bodyState.fitness > 80f)
            {
                newType = BodyType.Athletic;
            }
            else if (bodyState.fitness > 60f)
            {
                newType = bodyState.bodyType == BodyType.Heavy ? BodyType.Average : BodyType.Slim;
            }
            else if (bodyState.fitness > 40f)
            {
                newType = BodyType.Average;
            }
            else if (bodyState.fitness > 20f)
            {
                newType = bodyState.bodyType == BodyType.Slim ? BodyType.Average : BodyType.Curvy;
            }
            else
            {
                newType = BodyType.Heavy;
            }

            if (newType != bodyState.bodyType)
            {
                bodyState.bodyType = newType;
                OnBodyTypeChanged?.Invoke(newType);
            }
        }

        private void ApplyGroomingReputationEffects(GroomingLevel oldLevel, GroomingLevel newLevel)
        {
            if (newLevel == oldLevel)
            {
                return;
            }

            float professionalDelta = 0f;
            float socialDelta = 0f;

            switch (newLevel)
            {
                case GroomingLevel.Unkempt:
                    professionalDelta = -5f;
                    socialDelta = -5f;
                    break;
                case GroomingLevel.Basic:
                    break;
                case GroomingLevel.WellGroomed:
                    socialDelta = 5f;
                    break;
                case GroomingLevel.Professional:
                    professionalDelta = 5f;
                    break;
                case GroomingLevel.Glamorous:
                    professionalDelta = 3f;
                    socialDelta = 8f;
                    break;
            }

            if (!Mathf.Approximately(professionalDelta, 0f))
            {
                ReputationSystem.Instance.ModifyReputation(playerId, ReputationSystem.ReputationTrack.Professional, professionalDelta, "Grooming");
            }

            if (!Mathf.Approximately(socialDelta, 0f))
            {
                ReputationSystem.Instance.ModifyReputation(playerId, ReputationSystem.ReputationTrack.Social, socialDelta, "Grooming");
            }
        }

        private float GetGroomingCost(GroomingLevel level)
        {
            switch (level)
            {
                case GroomingLevel.Unkempt:
                    return 0f;
                case GroomingLevel.Basic:
                    return 50f;
                case GroomingLevel.WellGroomed:
                    return 200f;
                case GroomingLevel.Professional:
                    return 500f;
                case GroomingLevel.Glamorous:
                    return 1500f;
                default:
                    return 0f;
            }
        }

        private float GetMaintenanceCost(GroomingLevel level)
        {
            switch (level)
            {
                case GroomingLevel.Unkempt:
                    return 0f;
                case GroomingLevel.Basic:
                    return 20f;
                case GroomingLevel.WellGroomed:
                    return 100f;
                case GroomingLevel.Professional:
                    return 300f;
                case GroomingLevel.Glamorous:
                    return 800f;
                default:
                    return 0f;
            }
        }

        private void ApplyGroomingMaintenance()
        {
            if (bodyState.groomingCost <= 0f)
            {
                return;
            }

            float balance = EconomySystem.Instance.GetBalance(playerId);
            if (balance < bodyState.groomingCost)
            {
                DowngradeGrooming();
                return;
            }

            EconomySystem.Instance.DeductExpense(
                playerId,
                bodyState.groomingCost,
                EconomySystem.ExpenseType.Personal,
                "Grooming maintenance"
            );

        }

        private void DowngradeGrooming()
        {
            if (bodyState.grooming == GroomingLevel.Unkempt)
            {
                return;
            }

            GroomingLevel newLevel = bodyState.grooming - 1;
            bodyState.grooming = newLevel;
            bodyState.groomingCost = GetMaintenanceCost(newLevel);
            ApplyGroomingReputationEffects(bodyState.grooming + 1, newLevel);
            RecalculateDerivedStats(null);
            OnGroomingChanged?.Invoke(newLevel);
        }

        private void RecalculateDerivedStats(string npcId)
        {
            bodyState.energyMax = 100f + (bodyState.fitness * 0.2f);
            float attractiveness = CalculateAttractiveness(npcId);
            bodyState.attractiveness = attractiveness;
            OnAttractivenessChanged?.Invoke(attractiveness);
        }

        private float CalculateAttractiveness(string npcId)
        {
            float baseAttractiveness = 50f;

            switch (bodyState.bodyType)
            {
                case BodyType.Slim:
                    baseAttractiveness += 10f;
                    break;
                case BodyType.Athletic:
                    baseAttractiveness += 20f;
                    break;
                case BodyType.Average:
                    baseAttractiveness += 5f;
                    break;
                case BodyType.Curvy:
                    baseAttractiveness += 15f;
                    break;
                case BodyType.Heavy:
                    baseAttractiveness -= 5f;
                    break;
            }

            baseAttractiveness += bodyState.fitness * 0.3f;

            switch (bodyState.grooming)
            {
                case GroomingLevel.Unkempt:
                    baseAttractiveness -= 20f;
                    break;
                case GroomingLevel.Basic:
                    break;
                case GroomingLevel.WellGroomed:
                    baseAttractiveness += 10f;
                    break;
                case GroomingLevel.Professional:
                    baseAttractiveness += 15f;
                    break;
                case GroomingLevel.Glamorous:
                    baseAttractiveness += 25f;
                    break;
            }

            float clothingVanity = GetClothingVanity();
            baseAttractiveness += clothingVanity * 0.2f;

            if (!string.IsNullOrEmpty(npcId))
            {
                baseAttractiveness += CalculatePreferenceBonus(npcId, bodyState.bodyType);
            }

            return Mathf.Clamp(baseAttractiveness, 0f, 100f);
        }

        private float GetClothingVanity()
        {
            if (useForcedClothingVanity)
            {
                return forcedClothingVanity;
            }

            Debug.LogWarning("TODO: ClothingSystem integration for vanity value");
            return 0f;
        }

        private float CalculatePreferenceBonus(string npcId, BodyType playerBody)
        {
            if (!npcPreferenceOverrides.TryGetValue(npcId, out Dictionary<BodyType, float> prefs))
            {
                return 0f;
            }

            if (!prefs.TryGetValue(playerBody, out float preference))
            {
                return 0f;
            }

            return (preference - 0.5f) * 40f;
        }
    }
}
