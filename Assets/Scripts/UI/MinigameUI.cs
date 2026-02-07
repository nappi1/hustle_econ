using System;
using System.Collections.Generic;
using UnityEngine;
using Core;
using Minigames;

namespace UI
{
    public class MinigameUI : MonoBehaviour
    {
        public enum MinigameUIContext
        {
            Fullscreen,
            PhoneScreen,
            ComputerScreen,
            WorldSpace
        }

        public enum MinigameUIState
        {
            Hidden,
            Initializing,
            Active,
            Paused,
            Ending
        }

        [System.Serializable]
        public class MinigameUIConfig
        {
            public MinigameType minigameType;
            public MinigameUIContext context;
            public RectTransform containerBounds;
            public Canvas targetCanvas;
            public Color primaryColor = Color.white;
            public bool showTimer = true;
            public bool showScore;
            public bool showPerformance = true;
        }

        [System.Serializable]
        public class ClickTargetVisual
        {
            public GameObject visualObject;
            public Vector3 worldPosition;
            public Vector2 screenPosition;
            public float radius;
            public bool isActive;
            public int targetIndex;
        }

        [System.Serializable]
        public class MinigameUIInstance
        {
            public string instanceId;
            public MinigameType type;
            public MinigameUIContext context;
            public MinigameUIState state;
            public GameObject rootObject;
            public List<ClickTargetVisual> targets;
            public float startTime;
            public float currentPerformance;
        }

        private static MinigameUI instance;
        public static MinigameUI Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<MinigameUI>();
                    if (instance == null)
                    {
                        Debug.LogError("MinigameUI instance not found in scene");
                    }
                }
                return instance;
            }
        }

        public event Action<string> OnMinigameUICreated;
        public event Action<string> OnMinigameUIDestroyed;
        public event Action<string, bool> OnTargetClicked;
        public event Action<string> OnMinigamePaused;
        public event Action<string> OnMinigameResumed;

        [Header("Prefabs")]
        [SerializeField] private GameObject clickTargetPrefab;
        [SerializeField] private GameObject successFeedbackPrefab;
        [SerializeField] private GameObject failureFeedbackPrefab;
        [SerializeField] private GameObject performanceHUDPrefab;

        [Header("Canvases")]
        [SerializeField] private Canvas worldCanvas;
        [SerializeField] private Canvas screenCanvas;
        [SerializeField] private Canvas phoneCanvas;
        [SerializeField] private Canvas computerCanvas;

        private Dictionary<string, MinigameUIInstance> instances;
        private Dictionary<string, string> activityToInstance;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;

            Initialize();
        }

        public void Initialize()
        {
            instances = new Dictionary<string, MinigameUIInstance>();
            activityToInstance = new Dictionary<string, string>();

            if (ActivitySystem.Instance != null)
            {
                ActivitySystem.Instance.OnActivityStarted += HandleActivityStarted;
                ActivitySystem.Instance.OnActivityEnded += HandleActivityEnded;
            }
        }

        private void OnDestroy()
        {
            if (ActivitySystem.Instance != null)
            {
                ActivitySystem.Instance.OnActivityStarted -= HandleActivityStarted;
                ActivitySystem.Instance.OnActivityEnded -= HandleActivityEnded;
            }
        }

        private void Update()
        {
            if (InputManager.Instance != null &&
                InputManager.Instance.GetActionDown(InputManager.InputAction.MinigameAction1))
            {
                Vector3 clickPosition = InputManager.Instance.GetMousePosition();
                HandleClick(clickPosition);
            }

            foreach (var instanceEntry in instances.Values)
            {
                if (instanceEntry.state == MinigameUIState.Active)
                {
                    UpdateMinigameUI(instanceEntry.instanceId);
                }
            }
        }

        public string CreateMinigameUI(string minigameId, MinigameUIContext context, MinigameUIConfig config)
        {
            if (string.IsNullOrEmpty(minigameId))
            {
                Debug.LogWarning("CreateMinigameUI: minigameId is null or empty");
                return null;
            }

            if (instances.ContainsKey(minigameId))
            {
                return minigameId;
            }

            if (MinigameSystem.Instance != null)
            {
                MinigameSystem.Instance.StartMinigame(minigameId, string.Empty);
            }

            MinigameUIInstance uiInstance = new MinigameUIInstance
            {
                instanceId = minigameId,
                type = config != null ? config.minigameType : MinigameType.ClickTargets,
                context = context,
                state = MinigameUIState.Initializing,
                targets = new List<ClickTargetVisual>(),
                startTime = Time.time,
                currentPerformance = 0f
            };

            uiInstance.rootObject = new GameObject($"MinigameUI_{minigameId}");

            switch (uiInstance.type)
            {
                case MinigameType.ClickTargets:
                    SetupClickTargetsUI(uiInstance, config);
                    break;
                default:
                    Debug.LogWarning($"MinigameUI: Type {uiInstance.type} not implemented");
                    break;
            }

            uiInstance.state = MinigameUIState.Active;
            instances[minigameId] = uiInstance;

            OnMinigameUICreated?.Invoke(minigameId);
            return minigameId;
        }

        public void DestroyMinigameUI(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId) || !instances.TryGetValue(instanceId, out MinigameUIInstance uiInstance))
            {
                return;
            }

            uiInstance.state = MinigameUIState.Ending;

            foreach (var target in uiInstance.targets)
            {
                if (target.visualObject != null)
                {
                    Destroy(target.visualObject);
                }
            }

            if (uiInstance.rootObject != null)
            {
                Destroy(uiInstance.rootObject);
            }

            instances.Remove(instanceId);

            if (MinigameSystem.Instance != null)
            {
                MinigameSystem.Instance.EndMinigame(instanceId);
            }

            OnMinigameUIDestroyed?.Invoke(instanceId);
        }

        public void PauseMinigameUI(string instanceId)
        {
            if (!instances.TryGetValue(instanceId, out MinigameUIInstance uiInstance))
            {
                return;
            }

            uiInstance.state = MinigameUIState.Paused;

            if (MinigameSystem.Instance != null)
            {
                MinigameSystem.Instance.PauseMinigame(instanceId);
            }

            OnMinigamePaused?.Invoke(instanceId);
        }

        public void ResumeMinigameUI(string instanceId)
        {
            if (!instances.TryGetValue(instanceId, out MinigameUIInstance uiInstance))
            {
                return;
            }

            uiInstance.state = MinigameUIState.Active;

            if (MinigameSystem.Instance != null)
            {
                MinigameSystem.Instance.ResumeMinigame(instanceId);
            }

            OnMinigameResumed?.Invoke(instanceId);
        }

        public void UpdateMinigameUI(string instanceId)
        {
            if (!instances.TryGetValue(instanceId, out MinigameUIInstance uiInstance))
            {
                return;
            }

            if (MinigameSystem.Instance != null)
            {
                uiInstance.currentPerformance = MinigameSystem.Instance.GetPerformance(instanceId);

                MinigameInstance minigame = MinigameSystem.Instance.GetMinigameForTesting(instanceId);
                if (minigame == null || minigame.state == MinigameState.Completed || minigame.state == MinigameState.Failed)
                {
                    DestroyMinigameUI(instanceId);
                }
            }

            UpdatePerformanceDisplay(instanceId, uiInstance.currentPerformance);
        }

        public void HandleClick(Vector3 clickPosition)
        {
            foreach (var uiInstance in instances.Values)
            {
                if (uiInstance.state != MinigameUIState.Active)
                {
                    continue;
                }

                foreach (var target in uiInstance.targets)
                {
                    if (!target.isActive)
                    {
                        continue;
                    }

                    bool hit = CheckClickHit(clickPosition, target, uiInstance.context);
                    if (hit)
                    {
                        if (MinigameSystem.Instance != null)
                        {
                            MinigameInstance minigame = MinigameSystem.Instance.GetMinigameForTesting(uiInstance.instanceId);
                            if (minigame != null)
                            {
                                minigame.successfulActions++;
                                minigame.totalActions++;
                            }
                        }

                        ShowSuccess(uiInstance.instanceId, target.worldPosition);
                        target.isActive = false;
                        UpdateTargetVisual(target.visualObject, false);
                        OnTargetClicked?.Invoke(uiInstance.instanceId, true);
                        break;
                    }
                }
            }
        }

        public void HandleKeyPress(KeyCode key)
        {
            // TODO: Key-based minigames (sequence/timing)
        }

        public MinigameUIState GetState(string instanceId)
        {
            if (instances.TryGetValue(instanceId, out MinigameUIInstance uiInstance))
            {
                return uiInstance.state;
            }

            return MinigameUIState.Hidden;
        }

        public float GetPerformance(string instanceId)
        {
            if (instances.TryGetValue(instanceId, out MinigameUIInstance uiInstance))
            {
                return uiInstance.currentPerformance;
            }

            return 0f;
        }

        public bool IsMinigameActive(string instanceId)
        {
            return instances.ContainsKey(instanceId);
        }

        public void ShowSuccess(string instanceId, Vector3 position)
        {
            if (successFeedbackPrefab == null)
            {
                return;
            }

            Instantiate(successFeedbackPrefab, position, Quaternion.identity);
        }

        public void ShowFailure(string instanceId, Vector3 position)
        {
            if (failureFeedbackPrefab == null)
            {
                return;
            }

            Instantiate(failureFeedbackPrefab, position, Quaternion.identity);
        }

        public void UpdatePerformanceDisplay(string instanceId, float performance)
        {
            // TODO: Hook up performance HUD visuals
        }

        public void SetStateForTesting(string instanceId, MinigameUIState state)
        {
            if (instances.TryGetValue(instanceId, out MinigameUIInstance uiInstance))
            {
                uiInstance.state = state;
            }
        }

        public MinigameUIInstance GetInstanceForTesting(string instanceId)
        {
            instances.TryGetValue(instanceId, out MinigameUIInstance uiInstance);
            return uiInstance;
        }

        private void HandleActivityStarted(string activityId)
        {
            if (ActivitySystem.Instance == null)
            {
                return;
            }

            ActivitySystem.Activity activity = ActivitySystem.Instance.GetActivity(activityId);
            if (activity == null || string.IsNullOrEmpty(activity.minigameId))
            {
                return;
            }

            MinigameUIContext context = DetermineContext(activity);
            MinigameUIConfig config = new MinigameUIConfig
            {
                minigameType = GetMinigameType(activity.minigameId),
                context = context,
                showTimer = true,
                showScore = false,
                showPerformance = true
            };

            string instanceId = CreateMinigameUI(activity.minigameId, context, config);
            if (!string.IsNullOrEmpty(instanceId))
            {
                activityToInstance[activityId] = instanceId;
            }
        }

        private void HandleActivityEnded(ActivitySystem.ActivityResult result)
        {
            if (activityToInstance.TryGetValue(result.activityId, out string instanceId))
            {
                DestroyMinigameUI(instanceId);
                activityToInstance.Remove(result.activityId);
            }
        }

        private MinigameUIContext DetermineContext(ActivitySystem.Activity activity)
        {
            // TODO: Map activity contexts when ActivitySystem has richer types
            switch (activity.type)
            {
                case ActivitySystem.ActivityType.Physical:
                    return MinigameUIContext.WorldSpace;
                case ActivitySystem.ActivityType.Screen:
                    return MinigameUIContext.PhoneScreen;
                default:
                    return MinigameUIContext.Fullscreen;
            }
        }

        private MinigameType GetMinigameType(string minigameId)
        {
            if (string.IsNullOrEmpty(minigameId))
            {
                return MinigameType.ClickTargets;
            }

            string[] parts = minigameId.Split('_');
            string typeStr = parts.Length > 0 ? parts[0] : minigameId;
            if (Enum.TryParse(typeStr, true, out MinigameType type))
            {
                return type;
            }

            return MinigameType.ClickTargets;
        }

        private void SetupClickTargetsUI(MinigameUIInstance uiInstance, MinigameUIConfig config)
        {
            int targetCount = 3;
            for (int i = 0; i < targetCount; i++)
            {
                Vector2 screenPos = new Vector2(100f + (i * 60f), 100f + (i * 40f));
                Vector3 worldPos = new Vector3(screenPos.x, 0f, screenPos.y);

                GameObject visual = null;
                if (clickTargetPrefab != null)
                {
                    visual = Instantiate(clickTargetPrefab);
                    if (uiInstance.context == MinigameUIContext.WorldSpace)
                    {
                        visual.transform.position = worldPos;
                        visual.transform.SetParent(uiInstance.rootObject.transform);
                    }
                    else
                    {
                        Canvas canvas = GetCanvas(uiInstance.context, config);
                        if (canvas != null)
                        {
                            visual.transform.SetParent(canvas.transform, false);
                        }
                    }
                }

                uiInstance.targets.Add(new ClickTargetVisual
                {
                    visualObject = visual,
                    worldPosition = worldPos,
                    screenPosition = screenPos,
                    radius = 50f,
                    isActive = true,
                    targetIndex = i
                });
            }
        }

        private void UpdateTargetVisual(GameObject targetVisual, bool isActive)
        {
            if (targetVisual == null)
            {
                return;
            }

            targetVisual.SetActive(isActive);
        }

        private bool CheckClickHit(Vector3 clickPosition, ClickTargetVisual target, MinigameUIContext context)
        {
            if (context == MinigameUIContext.WorldSpace)
            {
                if (Camera.main != null)
                {
                    Ray ray = Camera.main.ScreenPointToRay(clickPosition);
                    if (Physics.Raycast(ray, out RaycastHit hit))
                    {
                        return Vector3.Distance(hit.point, target.worldPosition) < target.radius;
                    }
                }

                return Vector3.Distance(clickPosition, target.worldPosition) < target.radius;
            }

            Vector2 clickPos2D = new Vector2(clickPosition.x, clickPosition.y);
            float distance = Vector2.Distance(clickPos2D, target.screenPosition);
            return distance < target.radius;
        }

        private Canvas GetCanvas(MinigameUIContext context, MinigameUIConfig config)
        {
            if (config != null && config.targetCanvas != null)
            {
                return config.targetCanvas;
            }

            switch (context)
            {
                case MinigameUIContext.WorldSpace:
                    return worldCanvas;
                case MinigameUIContext.PhoneScreen:
                    return phoneCanvas;
                case MinigameUIContext.ComputerScreen:
                    return computerCanvas;
                default:
                    return screenCanvas;
            }
        }
    }
}
