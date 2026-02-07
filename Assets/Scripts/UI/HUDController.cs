using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Core;

namespace UI
{
    public class HUDController : MonoBehaviour
    {
        public enum HUDVisibility
        {
            Visible,
            Hidden,
            Minimal
        }

        public enum NotificationType
        {
            Info,
            Success,
            Warning,
            Error,
            Message
        }

        [System.Serializable]
        public class HUDNotification
        {
            public string id;
            public NotificationType type;
            public string title;
            public string message;
            public float timestamp;
            public float duration;
            public bool isDismissed;
            public Sprite icon;
        }

        [System.Serializable]
        public class HUDState
        {
            public HUDVisibility visibility;
            public string currentTime;
            public string currentDate;
            public float currentEnergy;
            public float maxEnergy;
            public float currentMoney;
            public string currentActivity;
            public int unreadMessages;
            public List<HUDNotification> activeNotifications;
        }

        private static HUDController instance;
        public static HUDController Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<HUDController>();
                    if (instance == null)
                    {
                        Debug.LogError("HUDController instance not found in scene");
                    }
                }
                return instance;
            }
        }

        public event Action<HUDVisibility> OnVisibilityChanged;
        public event Action<HUDNotification> OnNotificationShown;
        public event Action<string> OnNotificationDismissed;

        [Header("HUD Elements")]
        [SerializeField] private Canvas hudCanvas;
        [SerializeField] private TextMeshProUGUI timeText;
        [SerializeField] private TextMeshProUGUI dateText;
        [SerializeField] private Slider energySlider;
        [SerializeField] private TextMeshProUGUI energyText;
        [SerializeField] private TextMeshProUGUI moneyText;
        [SerializeField] private TextMeshProUGUI activityText;
        [SerializeField] private GameObject messageNotificationBadge;
        [SerializeField] private TextMeshProUGUI messageCountText;

        [Header("Notifications")]
        [SerializeField] private RectTransform notificationContainer;
        [SerializeField] private GameObject notificationPrefab;

        private HUDState state;
        private Dictionary<string, GameObject> activeNotificationObjects;

        private Action<DateTime> timeAdvancedHandler;
        private Action<float> energyChangedHandler;
        private Action<string> activityStartedHandler;
        private Action<ActivitySystem.ActivityResult> activityEndedHandler;
        private Action<string> phoneMessageHandler;
        private Action<DetectionSystem.DetectionResult> detectionHandler;
        private PhoneUI phoneInstance;

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
            state = new HUDState
            {
                visibility = HUDVisibility.Visible,
                activeNotifications = new List<HUDNotification>(),
                currentTime = string.Empty,
                currentDate = string.Empty,
                currentEnergy = 0f,
                maxEnergy = 100f,
                currentMoney = 0f,
                currentActivity = string.Empty,
                unreadMessages = 0
            };

            activeNotificationObjects = new Dictionary<string, GameObject>();
            SubscribeToEvents();
            UpdateAllDisplays();
        }

        private void SubscribeToEvents()
        {
            if (TimeEnergySystem.Instance != null)
            {
                timeAdvancedHandler = _ => UpdateTimeDisplay();
                energyChangedHandler = _ => UpdateEnergyDisplay();
                TimeEnergySystem.Instance.OnTimeAdvanced += timeAdvancedHandler;
                TimeEnergySystem.Instance.OnEnergyChanged += energyChangedHandler;
            }

            if (ActivitySystem.Instance != null)
            {
                activityStartedHandler = _ => UpdateActivityDisplay();
                activityEndedHandler = _ => UpdateActivityDisplay();
                ActivitySystem.Instance.OnActivityStarted += activityStartedHandler;
                ActivitySystem.Instance.OnActivityEnded += activityEndedHandler;
            }

            phoneInstance = FindAnyObjectByType<PhoneUI>();
            if (phoneInstance != null)
            {
                phoneMessageHandler = _ =>
                {
                    UpdateMessageCount();
                    ShowNotification("New Message", "You have a new message", NotificationType.Message, 3f);
                };
                phoneInstance.OnMessageReceived += phoneMessageHandler;
            }

            if (DetectionSystem.Instance != null)
            {
                detectionHandler = result =>
                {
                    if (result.severity > 0.7f)
                    {
                        ShowNotification("Caught!", "You were caught slacking", NotificationType.Error, 3f);
                    }
                };
                DetectionSystem.Instance.OnPlayerDetected += detectionHandler;
            }
        }

        private void OnDestroy()
        {
            if (TimeEnergySystem.Instance != null)
            {
                if (timeAdvancedHandler != null)
                {
                    TimeEnergySystem.Instance.OnTimeAdvanced -= timeAdvancedHandler;
                }
                if (energyChangedHandler != null)
                {
                    TimeEnergySystem.Instance.OnEnergyChanged -= energyChangedHandler;
                }
            }

            if (ActivitySystem.Instance != null)
            {
                if (activityStartedHandler != null)
                {
                    ActivitySystem.Instance.OnActivityStarted -= activityStartedHandler;
                }
                if (activityEndedHandler != null)
                {
                    ActivitySystem.Instance.OnActivityEnded -= activityEndedHandler;
                }
            }

            if (phoneInstance != null && phoneMessageHandler != null)
            {
                phoneInstance.OnMessageReceived -= phoneMessageHandler;
            }

            if (DetectionSystem.Instance != null && detectionHandler != null)
            {
                DetectionSystem.Instance.OnPlayerDetected -= detectionHandler;
            }
        }

        public void SetVisibility(HUDVisibility visibility)
        {
            if (state.visibility == visibility)
            {
                return;
            }

            state.visibility = visibility;
            if (hudCanvas != null)
            {
                hudCanvas.enabled = visibility != HUDVisibility.Hidden;
            }

            if (visibility == HUDVisibility.Minimal && activityText != null)
            {
                activityText.gameObject.SetActive(false);
            }

            OnVisibilityChanged?.Invoke(visibility);
        }

        public void Show()
        {
            SetVisibility(HUDVisibility.Visible);
        }

        public void Hide()
        {
            SetVisibility(HUDVisibility.Hidden);
        }

        public HUDVisibility GetVisibility()
        {
            return state.visibility;
        }

        public void UpdateTimeDisplay()
        {
            if (TimeEnergySystem.Instance == null)
            {
                return;
            }

            float currentHours = TimeEnergySystem.Instance.GetCurrentGameTime();
            int hours = Mathf.FloorToInt(currentHours);
            int minutes = Mathf.FloorToInt((currentHours - hours) * 60f);
            state.currentTime = $"{hours:D2}:{minutes:D2}";
            state.currentDate = TimeEnergySystem.Instance.GetGameDate();

            if (timeText != null)
            {
                timeText.text = state.currentTime;
            }

            if (dateText != null)
            {
                dateText.text = state.currentDate;
            }
        }

        public void UpdateEnergyDisplay()
        {
            if (TimeEnergySystem.Instance == null)
            {
                return;
            }

            state.currentEnergy = TimeEnergySystem.Instance.GetEnergyLevel();
            state.maxEnergy = 100f; // TODO: TimeEnergySystem max energy API

            if (energySlider != null)
            {
                energySlider.maxValue = state.maxEnergy;
                energySlider.value = state.currentEnergy;
            }

            if (energyText != null)
            {
                energyText.text = $"{state.currentEnergy:F0}/{state.maxEnergy:F0}";
            }
        }

        public void UpdateMoneyDisplay()
        {
            if (EconomySystem.Instance == null)
            {
                return;
            }

            state.currentMoney = EconomySystem.Instance.GetBalance("player");

            if (moneyText != null)
            {
                moneyText.text = $"${state.currentMoney:F2}";
            }
        }

        public void UpdateActivityDisplay()
        {
            if (ActivitySystem.Instance == null)
            {
                return;
            }

            List<ActivitySystem.Activity> active = ActivitySystem.Instance.GetActiveActivities("player");
            if (active.Count > 0)
            {
                state.currentActivity = GetActivityDisplayName(active[0].type);
            }
            else
            {
                state.currentActivity = string.Empty;
            }

            if (activityText != null)
            {
                activityText.text = state.currentActivity;
                activityText.gameObject.SetActive(!string.IsNullOrEmpty(state.currentActivity));
            }
        }

        public void UpdateMessageCount()
        {
            if (PhoneUI.Instance == null)
            {
                state.unreadMessages = 0;
                return;
            }

            state.unreadMessages = PhoneUI.Instance.GetUnreadCount();

            if (messageNotificationBadge != null)
            {
                messageNotificationBadge.SetActive(state.unreadMessages > 0);
            }

            if (messageCountText != null)
            {
                messageCountText.text = state.unreadMessages.ToString();
            }
        }

        public string ShowNotification(string title, string message, NotificationType type, float duration = 3f)
        {
            HUDNotification notification = new HUDNotification
            {
                id = Guid.NewGuid().ToString("N"),
                type = type,
                title = title,
                message = message,
                timestamp = Time.time,
                duration = duration,
                isDismissed = false
            };

            state.activeNotifications.Add(notification);

            if (notificationPrefab != null && notificationContainer != null)
            {
                GameObject notifObject = Instantiate(notificationPrefab, notificationContainer);
                activeNotificationObjects[notification.id] = notifObject;
            }

            if (duration > 0f && Application.isPlaying)
            {
                StartCoroutine(AutoDismissNotification(notification.id, duration));
            }

            OnNotificationShown?.Invoke(notification);
            return notification.id;
        }

        public void DismissNotification(string notificationId)
        {
            HUDNotification notification = state.activeNotifications.Find(n => n.id == notificationId);
            if (notification == null)
            {
                return;
            }

            notification.isDismissed = true;

            if (activeNotificationObjects.ContainsKey(notificationId))
            {
                Destroy(activeNotificationObjects[notificationId]);
                activeNotificationObjects.Remove(notificationId);
            }

            state.activeNotifications.Remove(notification);
            OnNotificationDismissed?.Invoke(notificationId);
        }

        public void DismissAllNotifications()
        {
            var ids = new List<string>(activeNotificationObjects.Keys);
            foreach (string id in ids)
            {
                DismissNotification(id);
            }

            state.activeNotifications.Clear();
        }

        public HUDState GetState()
        {
            return state;
        }

        public void SetStateForTesting(HUDState newState)
        {
            state = newState;
        }

        public void ForceUpdateForTesting()
        {
            UpdateTimeDisplay();
            UpdateEnergyDisplay();
            UpdateMoneyDisplay();
            UpdateActivityDisplay();
            UpdateMessageCount();
        }

        private void UpdateAllDisplays()
        {
            UpdateTimeDisplay();
            UpdateEnergyDisplay();
            UpdateMoneyDisplay();
            UpdateActivityDisplay();
            UpdateMessageCount();
        }

        private IEnumerator AutoDismissNotification(string notificationId, float duration)
        {
            yield return new WaitForSeconds(duration);
            DismissNotification(notificationId);
        }

        private string GetActivityDisplayName(ActivitySystem.ActivityType type)
        {
            switch (type)
            {
                case ActivitySystem.ActivityType.Physical:
                    return "Active";
                case ActivitySystem.ActivityType.Screen:
                    return "Screen";
                case ActivitySystem.ActivityType.Passive:
                    return "Passive";
                default:
                    return string.Empty;
            }
        }
    }
}
