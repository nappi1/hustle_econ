using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Core
{
    public class RelationshipSystem : MonoBehaviour
    {
        public enum NPCType
        {
            RomanticPartner,
            Family,
            Friend
        }

        public enum NPCPersonality
        {
            Supportive,
            Demanding,
            Distant,
            Volatile
        }

        public enum NPCValue
        {
            Stability,
            Ambition,
            Loyalty,
            Morality,
            Family,
            Independence,
            Vanity,
            Hedonism
        }

        public enum NPCTolerance
        {
            WorkHours,
            IllegalActivity,
            Risk,
            Neglect,
            SexualBoundaries,
            MoralFlexibility
        }

        public enum ToleranceLevel
        {
            Low,
            Medium,
            High
        }

        public enum SexualBoundaryType
        {
            StrictMonogamy,
            Monogamous,
            Open,
            SexWorkAccepting,
            Collaborative
        }

        public enum RelationshipStatus
        {
            Active,
            Strained,
            Broken,
            Reconciling
        }

        public enum MemoryTier
        {
            Volatile,
            Standard,
            Permanent
        }

        public enum RelationshipEventType
        {
            Birthday,
            Anniversary,
            Request,
            Ultimatum
        }

        public enum ActionType
        {
            MissedEvent,
            WorkedOvertime,
            LiedAboutLocation,
            Cheated,
            Arrested,
            GotPromoted,
            BoughtExpensiveItem
        }

        public enum ThresholdComparison
        {
            GreaterThan,
            LessThan,
            EqualOrGreater,
            EqualOrLess
        }

        [System.Serializable]
        public struct PlayerAction
        {
            public ActionType type;
            public string details;
            public DateTime timestamp;
            public int memorability;
        }

        [System.Serializable]
        public struct ObservedEvent
        {
            public PlayerAction action;
            public DateTime timestamp;
            public int memorability;
            public float emotionalIntensity;
            public float initialIntensity;
            public bool isPermanent;
            public MemoryTier tier;
        }

        [System.Serializable]
        public struct PatternData
        {
            public string patternType;
            public int count;
            public DateTime firstOccurrence;
            public DateTime lastOccurrence;
            public int memorability;
        }

        [System.Serializable]
        public struct NPCData
        {
            public string name;
            public NPCPersonality personality;
            public Dictionary<NPCValue, float> values;
            public Dictionary<NPCTolerance, ToleranceLevel> tolerances;
            public SexualBoundaryType sexualBoundary;
        }

        [System.Serializable]
        public class NPC
        {
            public string id;
            public string name;
            public NPCType type;
            public float relationshipScore;
            public NPCPersonality personality;
            public Dictionary<NPCValue, float> values;
            public Dictionary<NPCTolerance, ToleranceLevel> tolerances;
            public SexualBoundaryType sexualBoundary;
            public RelationshipStatus status;
            public List<ObservedEvent> memory;
            public int memoryCapacity = 50;
            public Dictionary<string, PatternData> detectedPatterns;
        }

        private static RelationshipSystem instance;
        public static RelationshipSystem Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<RelationshipSystem>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("RelationshipSystem");
                        instance = go.AddComponent<RelationshipSystem>();
                    }
                }
                return instance;
            }
        }

        public event Action<NPC> OnNPCCreated;
        public event Action<string, float, float> OnRelationshipChanged;
        public event Action<string, RelationshipEventType> OnRelationshipEvent;
        public event Action<string> OnBreakup;
        public event Action<string> OnReconciliation;
        public event Action<string, ObservedEvent> OnMemoryAdded;

        private Dictionary<string, NPC> npcs;
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
            npcs = new Dictionary<string, NPC>();
            timeSinceUpdate = 0f;
        }

        private void Update()
        {
            timeSinceUpdate += Time.deltaTime;
            if (timeSinceUpdate >= updateInterval)
            {
                timeSinceUpdate = 0f;
                DecayMemories();
            }
        }

        public NPC CreateNPC(NPCType type, NPCData data)
        {
            string id = Guid.NewGuid().ToString("N");
            NPC npc = new NPC
            {
                id = id,
                name = data.name,
                type = type,
                relationshipScore = 50f,
                personality = data.personality,
                values = data.values ?? new Dictionary<NPCValue, float>(),
                tolerances = data.tolerances ?? new Dictionary<NPCTolerance, ToleranceLevel>(),
                sexualBoundary = data.sexualBoundary,
                status = RelationshipStatus.Active,
                memory = new List<ObservedEvent>(),
                memoryCapacity = 50,
                detectedPatterns = new Dictionary<string, PatternData>()
            };

            npcs[id] = npc;
            OnNPCCreated?.Invoke(npc);
            return npc;
        }

        public float GetRelationshipScore(string npcId)
        {
            NPC npc = GetNPC(npcId);
            if (npc == null)
            {
                return 0f;
            }
            return npc.relationshipScore;
        }

        public void ModifyRelationship(string npcId, float delta, string reason)
        {
            NPC npc = GetNPC(npcId);
            if (npc == null)
            {
                return;
            }

            float oldScore = npc.relationshipScore;
            npc.relationshipScore = Mathf.Clamp(npc.relationshipScore + delta, 0f, 100f);
            float newScore = npc.relationshipScore;

            UpdateRelationshipStatus(npc, oldScore, newScore);

            OnRelationshipChanged?.Invoke(npcId, oldScore, newScore);
        }

        public void ObservePlayerAction(PlayerAction action)
        {
            if (action.timestamp == default)
            {
                action.timestamp = TimeEnergySystem.Instance.GetCurrentTime();
            }

            foreach (NPC npc in npcs.Values)
            {
                float impact = CalculateActionImpact(npc, action);
                if (Mathf.Abs(impact) > 0f)
                {
                    ModifyRelationship(npc.id, impact, action.type.ToString());
                }

                if (ShouldRemember(action, npc))
                {
                    AddToMemory(npc, action);
                }

                UpdatePatternDetection(npc, action);
            }
        }

        public List<ObservedEvent> RecallMemory(string npcId, int count = 10)
        {
            NPC npc = GetNPC(npcId);
            if (npc == null)
            {
                return new List<ObservedEvent>();
            }

            return npc.memory
                .OrderByDescending(m => m.emotionalIntensity)
                .ThenByDescending(m => m.timestamp)
                .Take(count)
                .ToList();
        }

        public bool CheckRelationshipThreshold(string npcId, float threshold, ThresholdComparison comparison)
        {
            NPC npc = GetNPC(npcId);
            if (npc == null)
            {
                return false;
            }

            float score = npc.relationshipScore;
            switch (comparison)
            {
                case ThresholdComparison.GreaterThan:
                    return score > threshold;
                case ThresholdComparison.LessThan:
                    return score < threshold;
                case ThresholdComparison.EqualOrGreater:
                    return score >= threshold;
                case ThresholdComparison.EqualOrLess:
                    return score <= threshold;
                default:
                    return false;
            }
        }

        public void TriggerEvent(string npcId, RelationshipEventType eventType)
        {
            NPC npc = GetNPC(npcId);
            if (npc == null)
            {
                return;
            }

            OnRelationshipEvent?.Invoke(npcId, eventType);
        }

        public string GetNPCDialogue(string npcId, string scenario)
        {
            NPC npc = GetNPC(npcId);
            if (npc == null)
            {
                return "NPC not found";
            }

            if (npc.relationshipScore > 70f)
            {
                return $"{npc.name} (Happy): {scenario} response";
            }

            if (npc.relationshipScore > 30f)
            {
                return $"{npc.name} (Neutral): {scenario} response";
            }

            return $"{npc.name} (Upset): {scenario} response";
        }

        private NPC GetNPC(string npcId)
        {
            if (string.IsNullOrEmpty(npcId) || !npcs.ContainsKey(npcId))
            {
                Debug.LogWarning($"GetNPC: NPC {npcId} not found");
                return null;
            }

            return npcs[npcId];
        }

        private void UpdateRelationshipStatus(NPC npc, float oldScore, float newScore)
        {
            if (newScore < 20f && npc.status != RelationshipStatus.Broken)
            {
                npc.status = RelationshipStatus.Broken;
                OnBreakup?.Invoke(npc.id);
            }
            else if (newScore < 30f && newScore >= 20f)
            {
                npc.status = RelationshipStatus.Strained;
            }
            else if (newScore >= 30f && oldScore < 20f)
            {
                npc.status = RelationshipStatus.Reconciling;
                OnReconciliation?.Invoke(npc.id);
            }
            else if (newScore >= 50f)
            {
                npc.status = RelationshipStatus.Active;
            }
        }

        private float CalculateActionImpact(NPC npc, PlayerAction action)
        {
            float impact = 0f;

            switch (action.type)
            {
                case ActionType.Arrested:
                    impact -= GetValue(npc, NPCValue.Morality) * 20f;
                    impact -= GetValue(npc, NPCValue.Stability) * 15f;
                    break;
                case ActionType.GotPromoted:
                    impact += GetValue(npc, NPCValue.Ambition) * 10f;
                    impact += GetValue(npc, NPCValue.Stability) * 5f;
                    break;
                case ActionType.MissedEvent:
                    impact -= GetValue(npc, NPCValue.Loyalty) * 15f;
                    impact -= GetValue(npc, NPCValue.Family) * 10f;
                    break;
                case ActionType.BoughtExpensiveItem:
                    impact += GetValue(npc, NPCValue.Vanity) * 8f;
                    impact -= GetValue(npc, NPCValue.Stability) * 5f;
                    break;
            }

            return impact;
        }

        private bool ShouldRemember(PlayerAction action, NPC npc)
        {
            if (action.memorability >= 7)
            {
                return true;
            }

            float impact = CalculateActionImpact(npc, action);
            if (Mathf.Abs(impact) > 10f)
            {
                return true;
            }

            if (action.memorability < 3 && Mathf.Abs(impact) < 5f)
            {
                return false;
            }

            return action.memorability >= 4;
        }

        private void AddToMemory(NPC npc, PlayerAction action)
        {
            MemoryTier tier = action.memorability >= 7
                ? MemoryTier.Permanent
                : action.memorability >= 4
                    ? MemoryTier.Standard
                    : MemoryTier.Volatile;

            float intensity = Mathf.Abs(CalculateActionImpact(npc, action)) * 10f;

            ObservedEvent memory = new ObservedEvent
            {
                action = action,
                timestamp = action.timestamp,
                memorability = action.memorability,
                emotionalIntensity = intensity,
                initialIntensity = intensity,
                isPermanent = tier == MemoryTier.Permanent,
                tier = tier
            };

            npc.memory.Add(memory);

            if (npc.memory.Count > npc.memoryCapacity)
            {
                TrimMemory(npc);
            }

            OnMemoryAdded?.Invoke(npc.id, memory);
        }

        private void TrimMemory(NPC npc)
        {
            List<ObservedEvent> permanent = npc.memory.Where(m => m.isPermanent).ToList();
            List<ObservedEvent> nonPermanent = npc.memory
                .Where(m => !m.isPermanent)
                .OrderByDescending(m => m.emotionalIntensity)
                .Take(Mathf.Max(0, npc.memoryCapacity - permanent.Count))
                .ToList();

            npc.memory = permanent.Concat(nonPermanent).ToList();
        }

        private void DecayMemories()
        {
            DateTime now = TimeEnergySystem.Instance.GetCurrentTime();
            foreach (NPC npc in npcs.Values)
            {
                List<int> indicesToRemove = new List<int>();
                for (int i = 0; i < npc.memory.Count; i++)
                {
                    ObservedEvent memory = npc.memory[i];

                    if (memory.isPermanent)
                    {
                        float daysSince = (float)(now - memory.timestamp).TotalDays;
                        float decayRate = 0.05f;
                        float targetFloor = memory.initialIntensity * 0.2f;
                        memory.emotionalIntensity = Mathf.Max(
                            targetFloor,
                            memory.emotionalIntensity - (decayRate * Time.deltaTime)
                        );
                        npc.memory[i] = memory;
                        continue;
                    }

                    if (memory.tier == MemoryTier.Volatile)
                    {
                        float decayRate = 0.2f;
                        memory.emotionalIntensity -= decayRate * Time.deltaTime;
                        if (memory.emotionalIntensity <= 0f)
                        {
                            indicesToRemove.Add(i);
                        }
                        else
                        {
                            npc.memory[i] = memory;
                        }
                    }
                }

                for (int i = indicesToRemove.Count - 1; i >= 0; i--)
                {
                    npc.memory.RemoveAt(indicesToRemove[i]);
                }
            }
        }

        private void UpdatePatternDetection(NPC npc, PlayerAction action)
        {
            string patternKey = GetPatternKey(action.type);
            if (patternKey == null)
            {
                return;
            }

            if (!npc.detectedPatterns.ContainsKey(patternKey))
            {
                npc.detectedPatterns[patternKey] = new PatternData
                {
                    patternType = patternKey,
                    count = 0,
                    firstOccurrence = action.timestamp
                };
            }

            PatternData pattern = npc.detectedPatterns[patternKey];
            pattern.count++;
            pattern.lastOccurrence = action.timestamp;
            if (pattern.count >= 3)
            {
                pattern.memorability = Mathf.Min(10, 5 + pattern.count);
            }

            npc.detectedPatterns[patternKey] = pattern;
        }

        private string GetPatternKey(ActionType type)
        {
            switch (type)
            {
                case ActionType.MissedEvent:
                    return "missed_events";
                case ActionType.WorkedOvertime:
                    return "overworking";
                case ActionType.LiedAboutLocation:
                    return "lying";
                case ActionType.Cheated:
                    return "infidelity";
                default:
                    return null;
            }
        }

        private float GetValue(NPC npc, NPCValue value)
        {
            if (npc.values != null && npc.values.TryGetValue(value, out float stored))
            {
                return stored;
            }
            return 0.5f;
        }
    }
}
