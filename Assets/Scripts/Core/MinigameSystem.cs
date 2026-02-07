using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Minigames;
using Minigames;

namespace Core
{
    public class MinigameSystem : MonoBehaviour
    {
        private static MinigameSystem instance;
        public static MinigameSystem Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<MinigameSystem>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("MinigameSystem");
                        instance = go.AddComponent<MinigameSystem>();
                    }
                }
                return instance;
            }
        }

        public event Action<string> OnMinigameStarted;
        public event Action<string> OnMinigamePaused;
        public event Action<string> OnMinigameResumed;
        public event Action<MinigameResult> OnMinigameEnded;
        public event Action<string, float> OnPerformanceChanged;

        private Dictionary<string, MinigameInstance> activeMinigames;

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
            activeMinigames = new Dictionary<string, MinigameInstance>();
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            foreach (MinigameInstance minigame in activeMinigames.Values.ToList())
            {
                if (minigame.state != MinigameState.Running)
                {
                    continue;
                }

                minigame.elapsedTime += deltaTime;

                minigame.behavior.HandleInput();
                minigame.behavior.UpdateLogic(deltaTime);

                float oldPerformance = minigame.currentPerformance;
                minigame.currentPerformance = minigame.behavior.CalculatePerformance();

                if (Mathf.Abs(minigame.currentPerformance - oldPerformance) > 5f)
                {
                    OnPerformanceChanged?.Invoke(minigame.minigameId, minigame.currentPerformance);
                }
            }
        }

        public void StartMinigame(string minigameId, string activityId)
        {
            if (string.IsNullOrEmpty(minigameId))
            {
                Debug.LogWarning("StartMinigame: minigameId is null or empty");
                return;
            }

            if (activeMinigames.ContainsKey(minigameId))
            {
                Debug.LogWarning($"Minigame {minigameId} already active");
                return;
            }

            MinigameType type = ParseMinigameType(minigameId);

            MinigameInstance minigame = new MinigameInstance
            {
                minigameId = minigameId,
                activityId = activityId,
                type = type,
                state = MinigameState.Running,
                currentPerformance = 50f,
                difficulty = 1.0f,
                startTime = DateTime.UtcNow,
                elapsedTime = 0f,
                successfulActions = 0,
                failedActions = 0,
                totalActions = 0
            };

            minigame.behavior = CreateMinigameBehavior(type, minigame);
            minigame.behavior.Initialize();

            activeMinigames[minigameId] = minigame;
            OnMinigameStarted?.Invoke(minigameId);
        }

        public void PauseMinigame(string minigameId)
        {
            MinigameInstance minigame = GetMinigame(minigameId);
            if (minigame == null)
            {
                return;
            }

            if (minigame.state == MinigameState.Paused)
            {
                return;
            }

            minigame.state = MinigameState.Paused;
            OnMinigamePaused?.Invoke(minigameId);
        }

        public void ResumeMinigame(string minigameId)
        {
            MinigameInstance minigame = GetMinigame(minigameId);
            if (minigame == null)
            {
                return;
            }

            if (minigame.state == MinigameState.Running)
            {
                return;
            }

            minigame.state = MinigameState.Running;
            OnMinigameResumed?.Invoke(minigameId);
        }

        public MinigameResult EndMinigame(string minigameId)
        {
            MinigameInstance minigame = GetMinigame(minigameId);
            if (minigame == null)
            {
                Debug.LogWarning($"EndMinigame: Minigame {minigameId} not found");
                return new MinigameResult { minigameId = minigameId, completedSuccessfully = false };
            }

            minigame.state = MinigameState.Completed;
            minigame.behavior.Cleanup();

            int total = minigame.successfulActions + minigame.failedActions;
            float accuracy = total == 0 ? 0f : minigame.successfulActions / (float)total;

            MinigameResult result = new MinigameResult
            {
                minigameId = minigame.minigameId,
                finalPerformance = minigame.currentPerformance,
                accuracy = accuracy,
                successfulActions = minigame.successfulActions,
                failedActions = minigame.failedActions,
                timeElapsed = minigame.elapsedTime,
                completedSuccessfully = true
            };

            activeMinigames.Remove(minigameId);
            OnMinigameEnded?.Invoke(result);
            return result;
        }

        public float GetPerformance(string minigameId)
        {
            if (string.IsNullOrEmpty(minigameId))
            {
                return 50f;
            }

            if (!activeMinigames.ContainsKey(minigameId))
            {
                Debug.LogWarning($"GetPerformance: Minigame {minigameId} not found");
                return 50f;
            }

            return activeMinigames[minigameId].currentPerformance;
        }

        public bool IsMinigameActive(string minigameId)
        {
            MinigameInstance minigame = GetMinigame(minigameId);
            return minigame != null && minigame.state == MinigameState.Running;
        }

        public void SetDifficulty(string minigameId, float difficulty)
        {
            MinigameInstance minigame = GetMinigame(minigameId);
            if (minigame == null)
            {
                return;
            }

            minigame.difficulty = Mathf.Clamp(difficulty, 0.5f, 2.0f);
        }

        public MinigameInstance GetMinigameForTesting(string minigameId)
        {
            return GetMinigame(minigameId);
        }

        public void AdvanceMinigameTimeForTesting(float deltaTime)
        {
            foreach (MinigameInstance minigame in activeMinigames.Values.ToList())
            {
                if (minigame.state != MinigameState.Running)
                {
                    continue;
                }

                minigame.elapsedTime += deltaTime;
                minigame.behavior.UpdateLogic(deltaTime);
                float oldPerformance = minigame.currentPerformance;
                minigame.currentPerformance = minigame.behavior.CalculatePerformance();
                if (Mathf.Abs(minigame.currentPerformance - oldPerformance) > 5f)
                {
                    OnPerformanceChanged?.Invoke(minigame.minigameId, minigame.currentPerformance);
                }
            }
        }

        private MinigameInstance GetMinigame(string minigameId)
        {
            if (string.IsNullOrEmpty(minigameId))
            {
                return null;
            }

            activeMinigames.TryGetValue(minigameId, out MinigameInstance minigame);
            return minigame;
        }

        private MinigameType ParseMinigameType(string minigameId)
        {
            string[] parts = minigameId.Split('_');
            string typeStr = parts.Length > 0 ? parts[0] : minigameId;

            if (Enum.TryParse(typeStr, true, out MinigameType type))
            {
                return type;
            }

            Debug.LogWarning($"Unknown minigame type in ID: {minigameId}, defaulting to ClickTargets");
            return MinigameType.ClickTargets;
        }

        private Minigame CreateMinigameBehavior(MinigameType type, MinigameInstance instance)
        {
            switch (type)
            {
                case MinigameType.ClickTargets:
                    return new ClickTargetsMinigame(instance);
                default:
                    return new StubMinigame(instance);
            }
        }
    }
}

