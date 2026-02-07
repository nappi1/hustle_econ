using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core
{
    public class HeatSystem : MonoBehaviour
    {
        public enum InvestigationType
        {
            Surveillance,
            IRS_Audit,
            Raid,
            Arrest_Warrant
        }

        [System.Serializable]
        public struct HeatModifier
        {
            public string source;
            public float amount;
            public DateTime expiresAt;
            public bool isPermanent;
        }

        [System.Serializable]
        public struct HeatState
        {
            public float level;
            public Dictionary<string, float> sources;
            public DateTime lastIncrease;
            public float decayRate;
            public List<HeatModifier> activeModifiers;
        }

        public static class HeatSources
        {
            public const string DRUG_DEALING = "drug_dealing";
            public const string FLASHY_PURCHASE = "flashy_purchase";
            public const string ARREST = "recent_arrest";
            public const string CASH_DEPOSIT = "cash_deposit";
            public const string SUSPICIOUS_INCOME = "suspicious_income";
            public const string REPEAT_OFFENDER = "repeat_offender";
        }

        private static HeatSystem instance;
        public static HeatSystem Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<HeatSystem>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("HeatSystem");
                        instance = go.AddComponent<HeatSystem>();
                    }
                }
                return instance;
            }
        }

        public event Action<float, string> OnHeatIncreased;
        public event Action<float> OnHeatDecreased;
        public event Action<float> OnHeatThresholdCrossed;
        public event Action<InvestigationType> OnInvestigationTriggered;
        public event Action OnHeatCleared;

        private HeatState heatState;
        private float updateInterval = 1f;
        private float timeSinceUpdate = 0f;

        private string playerId = "player";
        private float patrolFrequencyMultiplier = 1f;
        private float detectionSensitivityMultiplier = 1f;
        private bool activeWarrant = false;
        private bool forceEvidence = false;

        private bool auditActive = false;
        private float auditFrozenAmount = 0f;
        private DateTime auditResolutionTime = DateTime.MinValue;

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
            heatState = new HeatState
            {
                level = 0f,
                sources = new Dictionary<string, float>(),
                lastIncrease = TimeEnergySystem.Instance.GetCurrentTime(),
                decayRate = 1f,
                activeModifiers = new List<HeatModifier>()
            };
            timeSinceUpdate = 0f;
            patrolFrequencyMultiplier = 1f;
            detectionSensitivityMultiplier = 1f;
            activeWarrant = false;
            auditActive = false;
            auditFrozenAmount = 0f;
            auditResolutionTime = DateTime.MinValue;
        }

        private void Update()
        {
            timeSinceUpdate += Time.deltaTime;
            if (timeSinceUpdate >= updateInterval)
            {
                timeSinceUpdate = 0f;
                float gameHoursPassed = Time.deltaTime / 60f;
                ProcessHeatDecay(gameHoursPassed, TimeEnergySystem.Instance.GetCurrentTime());
                ResolveAuditIfNeeded();
            }
        }

        public float GetHeatLevel()
        {
            return heatState.level;
        }

        public void AddHeat(float amount, string source)
        {
            if (amount <= 0f)
            {
                return;
            }

            float oldLevel = heatState.level;
            heatState.level = Mathf.Clamp(heatState.level + amount, 0f, 100f);
            heatState.lastIncrease = TimeEnergySystem.Instance.GetCurrentTime();

            if (string.IsNullOrEmpty(source))
            {
                source = "unknown";
            }

            if (!heatState.sources.ContainsKey(source))
            {
                heatState.sources[source] = 0f;
            }
            heatState.sources[source] += amount;

            CheckHeatThresholds(oldLevel, heatState.level);
            OnHeatIncreased?.Invoke(amount, source);
        }

        public void ReduceHeat(float amount, string reason)
        {
            if (amount <= 0f)
            {
                return;
            }

            float oldLevel = heatState.level;
            heatState.level = Mathf.Clamp(heatState.level - amount, 0f, 100f);
            ApplyHeatSourceReduction(amount, reason);

            if (heatState.level <= 0f && oldLevel > 0f)
            {
                OnHeatCleared?.Invoke();
            }

            if (!Mathf.Approximately(oldLevel, heatState.level))
            {
                OnHeatDecreased?.Invoke(amount);
            }
        }

        public Dictionary<string, float> GetHeatSources()
        {
            return new Dictionary<string, float>(heatState.sources);
        }

        public void TriggerInvestigation(InvestigationType type)
        {
            OnInvestigationTriggered?.Invoke(type);

            switch (type)
            {
                case InvestigationType.Surveillance:
                    IncreasePatrolFrequency(1.5f);
                    IncreaseDetectionSensitivity(1.3f);
                    Debug.LogWarning("TODO: EventSystem surveillance_notice");
                    break;
                case InvestigationType.IRS_Audit:
                    auditActive = true;
                    float balance = EconomySystem.Instance.GetBalance(playerId);
                    auditFrozenAmount = balance * 0.3f;
                    auditResolutionTime = TimeEnergySystem.Instance.GetCurrentTime().AddDays(30);
                    Debug.LogWarning("TODO: EconomySystem FreezeAssets");
                    break;
                case InvestigationType.Raid:
                    Debug.LogWarning("TODO: EventSystem police_raid");
                    if (CheckForEvidence())
                    {
                        Debug.LogWarning("TODO: CriminalRecordSystem AddOffense (Raid evidence)");
                        Debug.LogWarning("TODO: JobSystem FireAllJobs (Arrested)");
                    }
                    heatState.level *= 0.5f;
                    break;
                case InvestigationType.Arrest_Warrant:
                    activeWarrant = true;
                    IncreasePatrolFrequency(2f);
                    break;
            }
        }

        public void OnSuspiciousTransaction(float amount, string source)
        {
            if (amount > 5000f)
            {
                float heatAmount = (amount / 10000f) * 5f;
                AddHeat(heatAmount, HeatSources.CASH_DEPOSIT);
            }

            if (string.Equals(source, "DrugSale", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(source, "SexWork", StringComparison.OrdinalIgnoreCase))
            {
                AddHeat(2f, HeatSources.SUSPICIOUS_INCOME);
            }
        }

        public void OnFlashyPurchase(float vanityValue)
        {
            if (vanityValue > 70f)
            {
                float heatAmount = (vanityValue / 100f) * 10f;
                AddHeat(heatAmount, HeatSources.FLASHY_PURCHASE);
            }
        }

        public void SetHeatLevelForTesting(float level)
        {
            heatState.level = Mathf.Clamp(level, 0f, 100f);
        }

        public void SetLastIncreaseForTesting(DateTime lastIncrease)
        {
            heatState.lastIncrease = lastIncrease;
        }

        public void SetPlayerIdForTesting(string id)
        {
            playerId = string.IsNullOrEmpty(id) ? "player" : id;
        }

        public void SetHasEvidenceForTesting(bool hasEvidence)
        {
            forceEvidence = hasEvidence;
        }

        public void ProcessHeatDecayForTesting(float gameHoursPassed, DateTime now)
        {
            ProcessHeatDecay(gameHoursPassed, now);
        }

        public void ResolveAuditForTesting()
        {
            ResolveAudit();
        }

        private void ProcessHeatDecay(float gameHoursPassed, DateTime now)
        {
            if (heatState.level <= 0f)
            {
                return;
            }

            float timeSinceIncrease = (float)(now - heatState.lastIncrease).TotalDays;
            float decayMultiplier = 1f;

            if (timeSinceIncrease < 1f)
            {
                decayMultiplier = 0.5f;
            }
            else if (timeSinceIncrease > 30f)
            {
                decayMultiplier = 3f;
            }
            else if (timeSinceIncrease > 7f)
            {
                decayMultiplier = 2f;
            }

            float baseDecay = 1f / 24f;
            float decayAmount = baseDecay * decayMultiplier * gameHoursPassed;

            float oldHeat = heatState.level;
            heatState.level = Mathf.Max(0f, heatState.level - decayAmount);

            if (heatState.level <= 0f && oldHeat > 0f)
            {
                OnHeatCleared?.Invoke();
            }

            if (!Mathf.Approximately(oldHeat, heatState.level))
            {
                OnHeatDecreased?.Invoke(decayAmount);
            }
        }

        private void CheckHeatThresholds(float oldLevel, float newLevel)
        {
            float[] thresholds = { 30f, 50f, 70f, 90f };
            foreach (float threshold in thresholds)
            {
                if (oldLevel < threshold && newLevel >= threshold)
                {
                    OnHeatThresholdCrossed?.Invoke(threshold);
                    HandleThresholdEffects(threshold);
                }
            }
        }

        private void HandleThresholdEffects(float threshold)
        {
            if (threshold >= 30f)
            {
                IncreasePatrolFrequency(1.2f);
            }

            if (threshold >= 50f)
            {
                TriggerInvestigation(InvestigationType.Surveillance);
            }

            if (threshold >= 70f)
            {
                float legitimacy = EconomySystem.Instance.GetLegitimacyScore(playerId);
                if (legitimacy < 0.6f)
                {
                    TriggerInvestigation(InvestigationType.IRS_Audit);
                }
            }

            if (threshold >= 90f)
            {
                if (CheckForEvidence())
                {
                    TriggerInvestigation(InvestigationType.Raid);
                }
                else
                {
                    TriggerInvestigation(InvestigationType.Arrest_Warrant);
                }
            }
        }

        private void IncreasePatrolFrequency(float multiplier)
        {
            patrolFrequencyMultiplier *= multiplier;
            if (DetectionSystem.Instance != null)
            {
                DetectionSystem.Instance.SetPatrolFrequency(patrolFrequencyMultiplier);
            }
        }

        private void IncreaseDetectionSensitivity(float multiplier)
        {
            detectionSensitivityMultiplier *= multiplier;
            if (DetectionSystem.Instance != null)
            {
                DetectionSystem.Instance.SetDetectionSensitivity(detectionSensitivityMultiplier);
            }
        }

        private void ApplyHeatSourceReduction(float amount, string reason)
        {
            if (heatState.sources.Count == 0)
            {
                return;
            }

            if (!string.IsNullOrEmpty(reason) && heatState.sources.ContainsKey(reason))
            {
                heatState.sources[reason] = Mathf.Max(0f, heatState.sources[reason] - amount);
                return;
            }

            string largestKey = null;
            float largestValue = 0f;
            foreach (KeyValuePair<string, float> entry in heatState.sources)
            {
                if (entry.Value > largestValue)
                {
                    largestValue = entry.Value;
                    largestKey = entry.Key;
                }
            }

            if (largestKey != null)
            {
                heatState.sources[largestKey] = Mathf.Max(0f, heatState.sources[largestKey] - amount);
            }
        }

        private bool CheckForEvidence()
        {
            return forceEvidence;
        }

        private void ResolveAuditIfNeeded()
        {
            if (auditActive && auditResolutionTime != DateTime.MinValue)
            {
                DateTime now = TimeEnergySystem.Instance.GetCurrentTime();
                if (now >= auditResolutionTime)
                {
                    ResolveAudit();
                }
            }
        }

        private void ResolveAudit()
        {
            if (!auditActive)
            {
                return;
            }

            float legitimacy = EconomySystem.Instance.GetLegitimacyScore(playerId);
            if (legitimacy > 0.7f)
            {
                auditFrozenAmount = 0f;
                Debug.LogWarning("TODO: EconomySystem UnfreezeAssets");
                Debug.LogWarning("TODO: EventSystem audit_cleared");
            }
            else
            {
                float balance = EconomySystem.Instance.GetBalance(playerId);
                float fine = balance * 0.2f;
                EconomySystem.Instance.DeductExpense(playerId, fine, EconomySystem.ExpenseType.Fine, "Tax evasion penalty");
                auditFrozenAmount = 0f;
                Debug.LogWarning("TODO: EconomySystem UnfreezeAssets");
                Debug.LogWarning("TODO: CriminalRecordSystem AddOffense (TaxEvasion)");
            }

            auditActive = false;
            auditResolutionTime = DateTime.MinValue;
        }
    }
}
