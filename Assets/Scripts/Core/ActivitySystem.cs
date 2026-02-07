using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Core
{
    public class ActivitySystem : MonoBehaviour
    {
        public enum ActivityType
        {
            Physical,
            Screen,
            Passive
        }

        public enum MultitaskingLevel
        {
            Full,
            Partial,
            Breaks,
            None
        }

        public enum ActivityState
        {
            Active,
            Running,
            Paused,
            Failed,
            Completed
        }

        [System.Serializable]
        public struct ActivityPhase
        {
            public string name;
            public float durationMinutes;
            public bool canMultitask;
            public float attentionRequired;
        }

        [System.Serializable]
        public class Activity
        {
            public string id;
            public string playerId;
            public ActivityType type;
            public MultitaskingLevel multitaskingAllowed;
            public string minigameId;
            public float requiredAttention;
            public float durationHours;
            public ActivityState state;
            public DateTime startTime;
            public float elapsedTime;
            public float performanceScore;
            public bool detectedSlacking;
            public List<string> concurrentActivities;
            public bool hasDowntimePhases;
            public List<ActivityPhase> phases;
            public int currentPhaseIndex;
        }

        [System.Serializable]
        public struct ActivityResult
        {
            public string activityId;
            public float performanceScore;
            public float timeSpent;
            public bool completed;
            public Dictionary<string, float> rewards;
        }

        private static ActivitySystem instance;
        public static ActivitySystem Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<ActivitySystem>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("ActivitySystem");
                        instance = go.AddComponent<ActivitySystem>();
                    }
                }
                return instance;
            }
        }

        public event Action<string> OnActivityStarted;
        public event Action<string> OnActivityPaused;
        public event Action<string> OnActivityResumed;
        public event Action<ActivityResult> OnActivityEnded;
        public event Action<string, string> OnMultitaskAttempt;

        private Dictionary<string, Activity> activities;
        private Dictionary<string, List<string>> playerActivities;
        private float updateInterval = 0.2f;
        private float timeSinceUpdate = 0f;
        private Dictionary<string, float> testMinigamePerformance;
        private HashSet<string> testDetectionIds;

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
            activities = new Dictionary<string, Activity>();
            playerActivities = new Dictionary<string, List<string>>();
            testMinigamePerformance = new Dictionary<string, float>();
            testDetectionIds = new HashSet<string>();
        }

        private void Update()
        {
            timeSinceUpdate += Time.deltaTime;
            if (timeSinceUpdate >= updateInterval)
            {
                float deltaTime = timeSinceUpdate;
                timeSinceUpdate = 0f;
                UpdateActivities(deltaTime);
            }
        }

        public string CreateActivity(ActivityType type, string minigameId, float durationHours)
        {
            string activityId = Guid.NewGuid().ToString("N");
            Activity activity = new Activity
            {
                id = activityId,
                playerId = "player",
                type = type,
                minigameId = minigameId,
                requiredAttention = GetAttentionRequirement(minigameId),
                durationHours = durationHours,
                state = ActivityState.Active,
                startTime = TimeEnergySystem.Instance.GetCurrentTime(),
                elapsedTime = 0f,
                performanceScore = 50f,
                detectedSlacking = false,
                concurrentActivities = new List<string>(),
                multitaskingAllowed = MultitaskingLevel.Partial,
                hasDowntimePhases = false,
                phases = new List<ActivityPhase>(),
                currentPhaseIndex = 0
            };

            List<Activity> activeActivities = GetActiveActivities(activity.playerId);
            foreach (Activity existing in activeActivities)
            {
                OnMultitaskAttempt?.Invoke(activityId, existing.id);
                if (!CanMultitask(activityId, existing.id, activity, existing))
                {
                    PauseActivity(existing.id);
                }
                else
                {
                    activity.concurrentActivities.Add(existing.id);
                    existing.concurrentActivities.Add(activityId);
                }
            }

            activities[activityId] = activity;
            if (!playerActivities.ContainsKey(activity.playerId))
            {
                playerActivities[activity.playerId] = new List<string>();
            }
            playerActivities[activity.playerId].Add(activityId);

            if (!string.IsNullOrEmpty(activity.minigameId))
            {
                MinigameSystem.Instance.StartMinigame(activity.minigameId, activityId);
            }

            if (RequiresFirstPerson(activity))
            {
                CameraController.Instance.OnJobStarted();
            }

            OnActivityStarted?.Invoke(activityId);
            return activityId;
        }

        public string CreateActivity(string playerId, ActivityType type, string context)
        {
            string resolvedPlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            string minigameId = MapContextToMinigameId(context);
            float durationHours = GetDefaultDurationHours(context, type);

            string activityId = CreateActivity(type, minigameId, durationHours);
            Activity activity = GetActivity(activityId);
            if (activity != null)
            {
                activity.playerId = resolvedPlayerId;
            }

            return activityId;
        }

        public List<Activity> GetActiveActivities(string playerId)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                return new List<Activity>();
            }

            if (!playerActivities.TryGetValue(playerId, out List<string> ids))
            {
                return new List<Activity>();
            }

            List<Activity> list = new List<Activity>();
            foreach (string id in ids)
            {
                if (activities.TryGetValue(id, out Activity activity))
                {
                    if (activity.state == ActivityState.Active || activity.state == ActivityState.Running)
                    {
                        list.Add(activity);
                    }
                }
            }
            return list;
        }

        public Activity GetActivity(string activityId)
        {
            if (string.IsNullOrEmpty(activityId))
            {
                return null;
            }

            activities.TryGetValue(activityId, out Activity activity);
            return activity;
        }

        public void PauseActivity(string activityId)
        {
            Activity activity = GetActivity(activityId);
            if (activity == null)
            {
                return;
            }

            activity.state = ActivityState.Paused;
            OnActivityPaused?.Invoke(activityId);
            if (!string.IsNullOrEmpty(activity.minigameId))
            {
                MinigameSystem.Instance.PauseMinigame(activity.minigameId);
            }
        }

        public void ResumeActivity(string activityId)
        {
            Activity activity = GetActivity(activityId);
            if (activity == null)
            {
                return;
            }

            activity.state = ActivityState.Running;
            OnActivityResumed?.Invoke(activityId);
            if (!string.IsNullOrEmpty(activity.minigameId))
            {
                MinigameSystem.Instance.ResumeMinigame(activity.minigameId);
            }
        }

        public ActivityResult EndActivity(string activityId)
        {
            Activity activity = GetActivity(activityId);
            if (activity == null)
            {
                return new ActivityResult
                {
                    activityId = activityId,
                    performanceScore = 0f,
                    timeSpent = 0f,
                    completed = false,
                    rewards = new Dictionary<string, float>()
                };
            }

            activity.state = ActivityState.Completed;

            Dictionary<string, float> rewards = new Dictionary<string, float>();
            if (!string.IsNullOrEmpty(activity.minigameId) && activity.minigameId.Contains("work"))
            {
                JobSystem.Job currentJob = JobSystem.Instance.GetCurrentJob(activity.playerId);
                if (currentJob != null)
                {
                    float pay = currentJob.hourlyWage * activity.durationHours;
                    rewards["money"] = pay;
                    EconomySystem.Instance.AddIncome(activity.playerId, pay, EconomySystem.IncomeSource.Salary, $"{currentJob.title} shift");
                }
                else
                {
                    float earnings = activity.durationHours * 20f;
                    rewards["money"] = earnings;
                    EconomySystem.Instance.AddIncome(activity.playerId, earnings, EconomySystem.IncomeSource.Salary, "Work activity");
                }
            }

            SkillSystem.SkillType skillType = MapMinigameToSkill(activity.minigameId);
            float skillGain = activity.performanceScore * 0.1f;
            rewards["skill_xp"] = skillGain;
            SkillSystem.Instance.ImproveSkillFromUse(activity.playerId, skillType, skillGain);

            if (!string.IsNullOrEmpty(activity.minigameId))
            {
                MinigameSystem.Instance.EndMinigame(activity.minigameId);
            }

            if (RequiresFirstPerson(activity))
            {
                CameraController.Instance.OnJobEnded();
            }

            RemoveActivity(activityId);

            ActivityResult result = new ActivityResult
            {
                activityId = activityId,
                performanceScore = activity.performanceScore,
                timeSpent = activity.elapsedTime / 3600f,
                completed = true,
                rewards = rewards
            };

            OnActivityEnded?.Invoke(result);
            return result;
        }

        public bool CanMultitask(string activityId1, string activityId2)
        {
            Activity act1 = GetActivity(activityId1);
            Activity act2 = GetActivity(activityId2);
            if (act1 == null || act2 == null)
            {
                return false;
            }

            return CanMultitask(activityId1, activityId2, act1, act2);
        }

        public void SetMinigamePerformanceForTesting(string minigameId, float performance)
        {
            testMinigamePerformance[minigameId] = Mathf.Clamp(performance, 0f, 100f);
        }

        public void SetDetectionForTesting(string activityId, bool detected)
        {
            if (detected)
            {
                testDetectionIds.Add(activityId);
            }
            else
            {
                testDetectionIds.Remove(activityId);
            }
        }

        public void AdvanceActivityTimeForTesting(float deltaSeconds)
        {
            UpdateActivities(deltaSeconds);
        }

        public void SetActivityPhaseForTesting(string activityId, List<ActivityPhase> phases)
        {
            Activity activity = GetActivity(activityId);
            if (activity == null)
            {
                return;
            }

            activity.hasDowntimePhases = phases != null && phases.Count > 0;
            activity.phases = phases ?? new List<ActivityPhase>();
            activity.currentPhaseIndex = 0;
        }

        private void UpdateActivities(float deltaTime)
        {
            foreach (Activity activity in activities.Values.ToList())
            {
                if (activity.state != ActivityState.Running && activity.state != ActivityState.Active)
                {
                    continue;
                }

                activity.elapsedTime += deltaTime;

                float durationSeconds = activity.durationHours * 3600f;
                if (durationSeconds <= 0f || activity.elapsedTime >= durationSeconds)
                {
                    EndActivity(activity.id);
                    continue;
                }

                float minigamePerformance = GetMinigamePerformance(activity);
                activity.performanceScore = (activity.performanceScore * 0.9f) + (minigamePerformance * 0.1f);

                if (IsWorkActivity(activity))
                {
                    bool detectedThisTick = false;
                    if (testDetectionIds.Contains(activity.id))
                    {
                        detectedThisTick = true;
                    }
                    else if (!activity.detectedSlacking)
                    {
                        DetectionSystem.DetectionResult result = DetectionSystem.Instance.CheckDetection(activity.playerId, activity.id);
                        detectedThisTick = result.detected;
                    }

                    if (detectedThisTick && !activity.detectedSlacking)
                    {
                        activity.detectedSlacking = true;
                        JobSystem.Instance.TriggerWarning(activity.playerId, "Caught slacking");
                        activity.performanceScore = Mathf.Max(0f, activity.performanceScore - 0.2f);
                    }
                }

                if (activity.hasDowntimePhases)
                {
                    UpdateActivityPhases(activity);
                }
            }
        }

        private void UpdateActivityPhases(Activity activity)
        {
            if (activity.phases == null || activity.phases.Count == 0)
            {
                return;
            }

            ActivityPhase currentPhase = activity.phases[activity.currentPhaseIndex];
            float phaseDurationSeconds = currentPhase.durationMinutes * 60f;
            float phaseStart = GetPhaseStartTime(activity);
            if (activity.elapsedTime - phaseStart >= phaseDurationSeconds)
            {
                activity.currentPhaseIndex = (activity.currentPhaseIndex + 1) % activity.phases.Count;
                ActivityPhase nextPhase = activity.phases[activity.currentPhaseIndex];
                activity.multitaskingAllowed = nextPhase.canMultitask ? MultitaskingLevel.Breaks : MultitaskingLevel.None;
                activity.requiredAttention = nextPhase.attentionRequired;
            }
        }

        private float GetPhaseStartTime(Activity activity)
        {
            if (activity.phases == null || activity.phases.Count == 0)
            {
                return 0f;
            }

            float time = 0f;
            for (int i = 0; i < activity.currentPhaseIndex; i++)
            {
                time += activity.phases[i].durationMinutes * 60f;
            }

            return time;
        }

        private float GetMinigamePerformance(Activity activity)
        {
            if (activity == null || string.IsNullOrEmpty(activity.minigameId))
            {
                return 50f;
            }

            if (testMinigamePerformance.TryGetValue(activity.minigameId, out float performance))
            {
                return performance;
            }

            if (MinigameSystem.Instance.IsMinigameActive(activity.minigameId))
            {
                return MinigameSystem.Instance.GetPerformance(activity.minigameId);
            }

            return 50f;
        }

        private float GetAttentionRequirement(string minigameId)
        {
            if (string.IsNullOrEmpty(minigameId))
            {
                return 0.4f;
            }

            if (minigameId.Contains("stream"))
            {
                return 0.8f;
            }

            if (minigameId.Contains("work"))
            {
                return 0.6f;
            }

            return 0.5f;
        }

        private static SkillSystem.SkillType MapMinigameToSkill(string minigameId)
        {
            if (string.IsNullOrEmpty(minigameId))
            {
                return SkillSystem.SkillType.Social;
            }

            string id = minigameId.ToLowerInvariant();
            if (id.Contains("work") || id.Contains("mop") || id.Contains("clean"))
            {
                return SkillSystem.SkillType.Cleaning;
            }

            if (id.Contains("drive"))
            {
                return SkillSystem.SkillType.Driving;
            }

            if (id.Contains("code") || id.Contains("program"))
            {
                return SkillSystem.SkillType.Programming;
            }

            if (id.Contains("trade") || id.Contains("deal"))
            {
                return SkillSystem.SkillType.Trading;
            }

            if (id.Contains("stream") || id.Contains("social"))
            {
                return SkillSystem.SkillType.Social;
            }

            return SkillSystem.SkillType.Social;
        }

        private static string MapContextToMinigameId(string context)
        {
            if (string.IsNullOrEmpty(context))
            {
                return "idle";
            }

            string ctx = context.ToLowerInvariant();
            if (ctx.Contains("drug"))
            {
                return "phone_drug_dealing";
            }

            if (ctx.Contains("work"))
            {
                return "work_mop";
            }

            if (ctx.Contains("stream"))
            {
                return "stream";
            }

            return context;
        }

        private static float GetDefaultDurationHours(string context, ActivityType type)
        {
            if (!string.IsNullOrEmpty(context))
            {
                string ctx = context.ToLowerInvariant();
                if (ctx.Contains("work"))
                {
                    return 8f;
                }
                if (ctx.Contains("stream"))
                {
                    return 2f;
                }
                if (ctx.Contains("phone"))
                {
                    return 1f;
                }
            }

            switch (type)
            {
                case ActivityType.Screen:
                    return 1f;
                case ActivityType.Physical:
                    return 2f;
                default:
                    return 0.5f;
            }
        }

        private static bool RequiresFirstPerson(Activity activity)
        {
            return IsWorkActivity(activity);
        }

        private static bool IsWorkActivity(Activity activity)
        {
            return activity != null
                && !string.IsNullOrEmpty(activity.minigameId)
                && activity.minigameId.IndexOf("work", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool CanMultitask(string activityId1, string activityId2, Activity act1, Activity act2)
        {
            if (act1.type == ActivityType.Physical && act2.type == ActivityType.Physical)
            {
                return false;
            }

            if (act1.type == ActivityType.Screen && act2.type == ActivityType.Screen)
            {
                return false;
            }

            float totalAttention = act1.requiredAttention + act2.requiredAttention;
            if (totalAttention > 1.0f)
            {
                return false;
            }

            if (act1.multitaskingAllowed == MultitaskingLevel.None || act2.multitaskingAllowed == MultitaskingLevel.None)
            {
                return false;
            }

            return true;
        }

        private void RemoveActivity(string activityId)
        {
            if (!activities.TryGetValue(activityId, out Activity activity))
            {
                return;
            }

            if (playerActivities.TryGetValue(activity.playerId, out List<string> ids))
            {
                ids.Remove(activityId);
            }

            activities.Remove(activityId);
        }
    }
}
