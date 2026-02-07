using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Core;
using HustleEconomy.Core;

namespace HustleEconomy.UI
{
    public class PhoneUI : MonoBehaviour
    {
        public enum PhoneApp
        {
            Home,
            Messages,
            DrugDealing,
            Banking,
            SocialMedia,
            Calendar,
            Contacts,
            Settings
        }

        public enum PhoneState
        {
            Closed,
            Opening,
            Open,
            Closing
        }

        [System.Serializable]
        public class PhoneMessage
        {
            public string npcId;
            public string npcName;
            public string messageText;
            public float timestamp;
            public bool isRead;
            public bool isPlayerMessage;
        }

        [System.Serializable]
        public class PhoneAppData
        {
            public PhoneApp appType;
            public string displayName;
            public Sprite iconSprite;
            public bool hasNotification;
            public int notificationCount;
            public bool isLocked;
        }

        [System.Serializable]
        public class PhoneUIState
        {
            public PhoneState state;
            public PhoneApp currentApp;
            public PhoneApp previousApp;
            public List<PhoneMessage> messages;
            public float openedAt;
            public bool isActivityRunning;
        }

        private static PhoneUI instance;
        public static PhoneUI Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<PhoneUI>();
                    if (instance == null)
                    {
                        Debug.LogError("PhoneUI instance not found in scene");
                    }
                }
                return instance;
            }
        }

        public event Action OnPhoneOpened;
        public event Action OnPhoneClosed;
        public event Action<PhoneApp> OnAppOpened;
        public event Action<string> OnMessageSent;
        public event Action<string> OnMessageReceived;
        public event Action<PhoneApp> OnNotificationShown;

        [Header("UI Components")]
        [SerializeField] private Canvas phoneCanvas;
        [SerializeField] private RectTransform phoneScreen;
        [SerializeField] private GameObject homeScreen;
        [SerializeField] private GameObject messagesScreen;
        [SerializeField] private GameObject bankingScreen;
        [SerializeField] private GameObject contactsScreen;

        [Header("Settings")]
        [SerializeField] private float transitionDuration = 0.3f;
        [SerializeField] private Vector2 phoneScreenSize = new Vector2(400f, 800f);

        private PhoneUIState state;
        private Dictionary<PhoneApp, GameObject> appScreens;
        private Dictionary<PhoneApp, bool> appNotifications;
        private string currentPhoneActivityId;

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
            state = new PhoneUIState
            {
                state = PhoneState.Closed,
                currentApp = PhoneApp.Home,
                previousApp = PhoneApp.Home,
                messages = new List<PhoneMessage>(),
                openedAt = 0f,
                isActivityRunning = false
            };

            appScreens = new Dictionary<PhoneApp, GameObject>
            {
                { PhoneApp.Home, homeScreen },
                { PhoneApp.Messages, messagesScreen },
                { PhoneApp.Banking, bankingScreen },
                { PhoneApp.Contacts, contactsScreen }
            };

            appNotifications = new Dictionary<PhoneApp, bool>();
            foreach (PhoneApp app in Enum.GetValues(typeof(PhoneApp)))
            {
                appNotifications[app] = false;
            }

            if (phoneCanvas != null)
            {
                phoneCanvas.enabled = false;
            }
        }

        private void Update()
        {
            if (InputManager.Instance != null && InputManager.Instance.GetActionDown(InputManager.InputAction.OpenPhone))
            {
                TogglePhone();
            }
        }

        public void OpenPhone()
        {
            if (state.state != PhoneState.Closed)
            {
                return;
            }

            state.state = PhoneState.Opening;
            state.openedAt = GetGameTimeHours();

            if (CameraController.Instance != null)
            {
                CameraController.Instance.OnPhoneOpened();
            }

            if (InputManager.Instance != null)
            {
                InputManager.Instance.PushContext(InputManager.InputContext.Phone);
            }

            if (!Application.isPlaying || transitionDuration <= 0f || phoneCanvas == null || phoneScreen == null)
            {
                CompleteOpen();
            }
            else
            {
                StartCoroutine(PhoneTransitionCoroutine(true));
            }

            OnPhoneOpened?.Invoke();
        }

        public void ClosePhone()
        {
            if (state.state != PhoneState.Open)
            {
                return;
            }

            state.state = PhoneState.Closing;

            if (CameraController.Instance != null)
            {
                CameraController.Instance.OnPhoneClosed();
            }

            if (InputManager.Instance != null)
            {
                InputManager.Instance.PopContext();
            }

            if (state.isActivityRunning && !string.IsNullOrEmpty(currentPhoneActivityId))
            {
                if (ActivitySystem.Instance != null)
                {
                    ActivitySystem.Instance.EndActivity(currentPhoneActivityId);
                }

                state.isActivityRunning = false;
                currentPhoneActivityId = null;
            }

            if (!Application.isPlaying || transitionDuration <= 0f || phoneCanvas == null || phoneScreen == null)
            {
                CompleteClose();
            }
            else
            {
                StartCoroutine(PhoneTransitionCoroutine(false));
            }

            OnPhoneClosed?.Invoke();
        }

        public void TogglePhone()
        {
            if (state.state == PhoneState.Open)
            {
                ClosePhone();
            }
            else if (state.state == PhoneState.Closed)
            {
                OpenPhone();
            }
        }

        public bool IsPhoneOpen()
        {
            return state.state == PhoneState.Open || state.state == PhoneState.Opening;
        }

        public void OpenApp(PhoneApp app)
        {
            if (state.state != PhoneState.Open)
            {
                Debug.LogWarning("Cannot open app - phone is closed");
                return;
            }

            state.previousApp = state.currentApp;
            state.currentApp = app;

            foreach (var screen in appScreens.Values)
            {
                if (screen != null)
                {
                    screen.SetActive(false);
                }
            }

            if (appScreens.TryGetValue(app, out GameObject target) && target != null)
            {
                target.SetActive(true);
            }

            switch (app)
            {
                case PhoneApp.DrugDealing:
                    StartPhoneActivity();
                    break;

                case PhoneApp.Banking:
                    RefreshBankingData();
                    break;

                case PhoneApp.Messages:
                    MarkMessagesRead(GetCurrentContactId());
                    break;

                case PhoneApp.Contacts:
                    RefreshContactsList();
                    break;
            }

            OnAppOpened?.Invoke(app);
        }

        public void GoBack()
        {
            if (state.previousApp != state.currentApp)
            {
                OpenApp(state.previousApp);
            }
            else
            {
                GoHome();
            }
        }

        public void GoHome()
        {
            OpenApp(PhoneApp.Home);
        }

        public void SendMessage(string npcId, string messageText)
        {
            PhoneMessage message = new PhoneMessage
            {
                npcId = npcId,
                npcName = GetNPCName(npcId),
                messageText = messageText,
                timestamp = GetGameTimeHours(),
                isRead = true,
                isPlayerMessage = true
            };

            state.messages.Add(message);
            OnMessageSent?.Invoke(npcId);
        }

        public void ReceiveMessage(string npcId, string messageText)
        {
            PhoneMessage message = new PhoneMessage
            {
                npcId = npcId,
                npcName = GetNPCName(npcId),
                messageText = messageText,
                timestamp = GetGameTimeHours(),
                isRead = false,
                isPlayerMessage = false
            };

            state.messages.Add(message);

            if (state.state == PhoneState.Closed)
            {
                ShowNotification("New Message", $"{message.npcName}: {messageText}");
            }

            OnMessageReceived?.Invoke(npcId);
        }

        public List<PhoneMessage> GetMessagesWithNPC(string npcId)
        {
            return state.messages.FindAll(m => m.npcId == npcId);
        }

        public void MarkMessagesRead(string npcId)
        {
            if (string.IsNullOrEmpty(npcId))
            {
                return;
            }

            foreach (var message in state.messages)
            {
                if (message.npcId == npcId && !message.isRead)
                {
                    message.isRead = true;
                }
            }
        }

        public int GetUnreadCount()
        {
            return state.messages.FindAll(m => !m.isRead).Count;
        }

        public void ShowNotification(string title, string message)
        {
            appNotifications[PhoneApp.Messages] = true;
            OnNotificationShown?.Invoke(PhoneApp.Messages);
        }

        public void ClearNotification(PhoneApp app)
        {
            appNotifications[app] = false;
        }

        public PhoneState GetState()
        {
            return state.state;
        }

        public PhoneApp GetCurrentApp()
        {
            return state.currentApp;
        }

        public bool IsActivityRunning()
        {
            return state.isActivityRunning;
        }

        public void SetStateForTesting(PhoneState phoneState)
        {
            state.state = phoneState;
        }

        public void AddMessageForTesting(PhoneMessage message)
        {
            if (message != null)
            {
                state.messages.Add(message);
            }
        }

        public PhoneUIState GetStateForTesting()
        {
            return state;
        }

        private IEnumerator PhoneTransitionCoroutine(bool opening)
        {
            float elapsed = 0f;
            Vector3 startScale = opening ? Vector3.zero : Vector3.one;
            Vector3 endScale = opening ? Vector3.one : Vector3.zero;

            if (phoneCanvas != null)
            {
                phoneCanvas.enabled = true;
            }

            if (phoneScreen != null)
            {
                phoneScreen.localScale = startScale;
            }

            while (elapsed < transitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / transitionDuration);

                if (phoneScreen != null)
                {
                    phoneScreen.localScale = Vector3.Lerp(startScale, endScale, t);
                }

                yield return null;
            }

            if (phoneScreen != null)
            {
                phoneScreen.localScale = endScale;
            }

            if (opening)
            {
                CompleteOpen();
            }
            else
            {
                CompleteClose();
            }
        }

        private void CompleteOpen()
        {
            if (phoneCanvas != null)
            {
                phoneCanvas.enabled = true;
            }

            state.state = PhoneState.Open;
            PositionPhoneScreen();
        }

        private void CompleteClose()
        {
            state.state = PhoneState.Closed;
            if (phoneCanvas != null)
            {
                phoneCanvas.enabled = false;
            }
        }

        private void PositionPhoneScreen()
        {
            if (phoneCanvas == null || phoneScreen == null)
            {
                return;
            }

            phoneCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            phoneCanvas.sortingOrder = 100;

            phoneScreen.anchorMin = new Vector2(0.375f, 0.25f);
            phoneScreen.anchorMax = new Vector2(0.625f, 0.75f);
            phoneScreen.sizeDelta = phoneScreenSize;
        }

        private void RefreshBankingData()
        {
            if (EconomySystem.Instance == null)
            {
                return;
            }

            EconomySystem.Instance.GetBalance("player");
            EconomySystem.Instance.GetTransactionHistory("player", 10);
        }

        private void RefreshContactsList()
        {
            // TODO: RelationshipSystem.GetNPCs(playerId)
        }

        private void StartPhoneActivity()
        {
            if (ActivitySystem.Instance == null)
            {
                return;
            }

            // TODO: Phone spec expects CreateActivity(playerId, ActivityType, context).
            // Current ActivitySystem signature: CreateActivity(ActivityType type, string minigameId, float durationHours)
            string activityId = ActivitySystem.Instance.CreateActivity(ActivitySystem.ActivityType.Screen, "phone_drug_dealing", 1f);
            currentPhoneActivityId = activityId;
            state.isActivityRunning = true;
        }

        private string GetNPCName(string npcId)
        {
            // TODO: RelationshipSystem.GetNPC(npcId)
            if (EntitySystem.Instance != null)
            {
                var entity = EntitySystem.Instance.GetEntity(npcId);
                if (entity != null)
                {
                    return entity.id;
                }
            }

            return "Unknown";
        }

        private float GetGameTimeHours()
        {
            if (TimeEnergySystem.Instance == null)
            {
                return 0f;
            }

            DateTime now = TimeEnergySystem.Instance.GetCurrentTime();
            return (float)now.TimeOfDay.TotalHours;
        }

        private string GetCurrentContactId()
        {
            if (state.messages.Count == 0)
            {
                return string.Empty;
            }

            return state.messages[state.messages.Count - 1].npcId;
        }
    }
}
