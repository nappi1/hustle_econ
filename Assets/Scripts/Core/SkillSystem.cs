using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Core
{
    public class SkillSystem : MonoBehaviour
    {
        public enum SkillType
        {
            Driving,
            Stealth,
            Persuasion,
            Cooking,
            Fitness,
            Mechanical,
            Cleaning,
            Social,
            Trading,
            Programming
        }

        [System.Serializable]
        public struct Skill
        {
            public SkillType type;
            public float level;
            public float xp;
            public DateTime lastUsed;
            public float decayRate;
            public bool canDecay;
            public float peakLevel;
        }

        [System.Serializable]
        public class SkillProfile
        {
            public string playerId;
            public Dictionary<SkillType, Skill> skills;
        }

        private static SkillSystem instance;
        public static SkillSystem Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<SkillSystem>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("SkillSystem");
                        instance = go.AddComponent<SkillSystem>();
                    }
                }
                return instance;
            }
        }

        public event Action<string, SkillType, float, float> OnSkillChanged;
        public event Action<string, SkillType, float> OnSkillImproved;
        public event Action<string, SkillType, int> OnSkillMilestone;
        public event Action<string, SkillType> OnSkillDecayed;

        private Dictionary<string, SkillProfile> profiles;
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
            profiles = new Dictionary<string, SkillProfile>();
            timeSinceUpdate = 0f;
        }

        private void Update()
        {
            timeSinceUpdate += Time.deltaTime;
            if (timeSinceUpdate >= updateInterval)
            {
                timeSinceUpdate = 0f;
                ProcessSkillDecay();
            }
        }

        public float GetSkillLevel(string playerId, SkillType skill)
        {
            SkillProfile profile = GetOrCreateProfile(playerId);
            if (!profile.skills.ContainsKey(skill))
            {
                return 0f;
            }

            return profile.skills[skill].level;
        }

        public void ModifySkill(string playerId, SkillType skill, float delta, string reason)
        {
            SkillProfile profile = GetOrCreateProfile(playerId);
            if (!profile.skills.ContainsKey(skill))
            {
                Debug.LogWarning($"ModifySkill: Skill {skill} not found for player {playerId}");
                return;
            }

            Skill skillData = profile.skills[skill];
            float oldLevel = skillData.level;
            skillData.level = Mathf.Clamp(skillData.level + delta, 0f, 100f);

            if (skillData.level > skillData.peakLevel)
            {
                skillData.peakLevel = skillData.level;
            }

            skillData.lastUsed = TimeEnergySystem.Instance.GetCurrentTime();
            profile.skills[skill] = skillData;

            float newLevel = skillData.level;
            CheckMilestone(playerId, skill, oldLevel, newLevel);

            OnSkillChanged?.Invoke(playerId, skill, oldLevel, newLevel);
        }

        public void ImproveSkillFromUse(string playerId, SkillType skill, float baseGain)
        {
            SkillProfile profile = GetOrCreateProfile(playerId);
            if (!profile.skills.ContainsKey(skill))
            {
                Debug.LogWarning($"ImproveSkillFromUse: Skill {skill} not found");
                return;
            }

            Skill skillData = profile.skills[skill];

            float scalingFactor = 1.0f - (skillData.level / 150f);
            float relearningBonus = 1.0f;
            if (skillData.level < skillData.peakLevel * 0.9f)
            {
                relearningBonus = 2.0f;
            }

            float actualGain = baseGain * Mathf.Max(0.1f, scalingFactor) * relearningBonus;

            skillData.xp += actualGain;
            skillData.lastUsed = TimeEnergySystem.Instance.GetCurrentTime();

            const float xpNeeded = 100f;
            if (skillData.xp >= xpNeeded && skillData.level < 100f)
            {
                float oldLevel = skillData.level;
                skillData.level = Mathf.Min(100f, skillData.level + 1f);
                skillData.xp -= xpNeeded;

                if (skillData.level > skillData.peakLevel)
                {
                    skillData.peakLevel = skillData.level;
                }

                profile.skills[skill] = skillData;

                CheckMilestone(playerId, skill, oldLevel, skillData.level);
                OnSkillChanged?.Invoke(playerId, skill, oldLevel, skillData.level);
            }
            else
            {
                profile.skills[skill] = skillData;
            }

            OnSkillImproved?.Invoke(playerId, skill, actualGain);
        }

        public bool CheckSkillRequirement(string playerId, SkillType skill, float required)
        {
            float currentLevel = GetSkillLevel(playerId, skill);
            return currentLevel >= required;
        }

        public void SetSkillLevelForTesting(string playerId, SkillType skill, float level)
        {
            SkillProfile profile = GetOrCreateProfile(playerId);
            if (!profile.skills.ContainsKey(skill))
            {
                profile.skills[skill] = new Skill
                {
                    type = skill,
                    level = 0f,
                    xp = 0f,
                    lastUsed = TimeEnergySystem.Instance.GetCurrentTime(),
                    decayRate = GetDecayRate(skill),
                    canDecay = CanDecay(skill),
                    peakLevel = 0f
                };
            }

            Skill skillData = profile.skills[skill];
            skillData.level = Mathf.Clamp(level, 0f, 100f);
            skillData.peakLevel = Mathf.Max(skillData.peakLevel, skillData.level);
            profile.skills[skill] = skillData;
        }

        public void SetSkillLastUsedForTesting(string playerId, SkillType skill, DateTime lastUsed)
        {
            SkillProfile profile = GetOrCreateProfile(playerId);
            if (profile.skills.ContainsKey(skill))
            {
                Skill skillData = profile.skills[skill];
                skillData.lastUsed = lastUsed;
                profile.skills[skill] = skillData;
            }
        }

        public void ProcessSkillDecayForTesting(float deltaTime)
        {
            ProcessSkillDecay(deltaTime);
        }

        private Skill GetSkill(string playerId, SkillType skill)
        {
            SkillProfile profile = GetOrCreateProfile(playerId);
            if (!profile.skills.ContainsKey(skill))
            {
                Debug.LogWarning($"GetSkill: Skill {skill} not found for player {playerId}");
                return new Skill { level = 0f };
            }

            return profile.skills[skill];
        }

        private SkillProfile GetOrCreateProfile(string playerId)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                playerId = "player";
            }

            if (!profiles.ContainsKey(playerId))
            {
                SkillProfile profile = new SkillProfile
                {
                    playerId = playerId,
                    skills = new Dictionary<SkillType, Skill>()
                };

                foreach (SkillType skillType in Enum.GetValues(typeof(SkillType)))
                {
                    profile.skills[skillType] = new Skill
                    {
                        type = skillType,
                        level = 0f,
                        xp = 0f,
                        lastUsed = TimeEnergySystem.Instance.GetCurrentTime(),
                        decayRate = GetDecayRate(skillType),
                        canDecay = CanDecay(skillType),
                        peakLevel = 0f
                    };
                }

                profiles[playerId] = profile;
            }

            return profiles[playerId];
        }

        private float GetDecayRate(SkillType skill)
        {
            switch (skill)
            {
                case SkillType.Fitness:
                    return 0.2f;
                case SkillType.Driving:
                    return 0.05f;
                case SkillType.Social:
                    return 0.1f;
                case SkillType.Programming:
                    return 0.08f;
                default:
                    return 0.1f;
            }
        }

        private bool CanDecay(SkillType skill)
        {
            switch (skill)
            {
                case SkillType.Driving:
                case SkillType.Cooking:
                    return false;
                default:
                    return true;
            }
        }

        private void CheckMilestone(string playerId, SkillType skill, float oldLevel, float newLevel)
        {
            int[] milestones = { 25, 50, 75, 100 };
            foreach (int milestone in milestones)
            {
                if (oldLevel < milestone && newLevel >= milestone)
                {
                    OnSkillMilestone?.Invoke(playerId, skill, milestone);
                    UnlockSkillOpportunities(playerId, skill, milestone);
                }
            }
        }

        private void UnlockSkillOpportunities(string playerId, SkillType skill, int milestone)
        {
            switch (skill)
            {
                case SkillType.Driving:
                    if (milestone == 50)
                    {
                        Debug.Log("Unlock: Rideshare driver job available");
                    }
                    break;
                case SkillType.Stealth:
                    if (milestone == 75)
                    {
                        Debug.Log("Unlock: High-level heists available");
                    }
                    break;
                case SkillType.Social:
                    if (milestone == 50)
                    {
                        Debug.Log("Unlock: Networking events available");
                    }
                    break;
                case SkillType.Mechanical:
                    if (milestone == 50)
                    {
                        Debug.Log("Unlock: Auto mechanic job available");
                    }
                    break;
                case SkillType.Programming:
                    if (milestone == 75)
                    {
                        Debug.Log("Unlock: Hacking activities available");
                    }
                    break;
            }
        }

        private void ProcessSkillDecay()
        {
            ProcessSkillDecay(Time.deltaTime);
        }

        private void ProcessSkillDecay(float deltaTime)
        {
            DateTime now = TimeEnergySystem.Instance.GetCurrentTime();

            foreach (SkillProfile profile in profiles.Values)
            {
                List<SkillType> keys = profile.skills.Keys.ToList();
                foreach (SkillType skillType in keys)
                {
                    Skill skill = profile.skills[skillType];

                    if (!skill.canDecay)
                    {
                        continue;
                    }

                    float daysSinceUse = (float)(now - skill.lastUsed).TotalDays;
                    if (daysSinceUse > 30f)
                    {
                        float daysOverThreshold = daysSinceUse - 30f;
                        float decayAmount = skill.decayRate * daysOverThreshold * deltaTime;
                        float minLevel = skill.peakLevel * 0.6f;

                        float oldLevel = skill.level;
                        skill.level = Mathf.Max(minLevel, skill.level - decayAmount);
                        profile.skills[skillType] = skill;

                        if (skill.level < oldLevel)
                        {
                            OnSkillDecayed?.Invoke(profile.playerId, skill.type);
                            OnSkillChanged?.Invoke(profile.playerId, skill.type, oldLevel, skill.level);
                        }
                    }
                }
            }
        }
    }
}
