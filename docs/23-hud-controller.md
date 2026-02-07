# HUD CONTROLLER SPECIFICATION

**System:** HUDController  
**Namespace:** HustleEconomy.UI  
**Dependencies:** TimeEnergySystem, EconomySystem, ActivitySystem, DetectionSystem, PhoneUI  
**Purpose:** Display persistent on-screen information (time, energy, money, notifications)

---

## **I. OVERVIEW**

### **What This System Does**

HUDController manages the heads-up display:
- Time and date (game time, not real time)
- Energy bar (current/max)
- Money (current balance)
- Active activity indicator
- Notifications (messages, warnings, events)
- Detection warning (optional, subtle)

### **Design Philosophy**

- **Minimal and non-intrusive:** Information available but not distracting
- **Context-aware:** Elements hide during cutscenes, phone use
- **No hand-holding:** Environmental cues > UI warnings
- **Diegetic where possible:** Phone shows time, not arbitrary UI element

---

## **II. INTEGRATION POINTS (CRITICAL)**

### **Resolves These TODOs:**

None explicitly, but completes the presentation layer by providing essential feedback.

### **Requires These System APIs:**

**TimeEnergySystem (existing):**
- `TimeEnergySystem.GetCurrentGameTime()` → Current time
- `TimeEnergySystem.GetGameDate()` → Current date (day, month, year)
- `TimeEnergySystem.GetEnergy(playerId)` → Current energy
- `TimeEnergySystem.GetMaxEnergy(playerId)` → Max energy
- `TimeEnergySystem.OnTimeAdvanced` → Event for time changes
- `TimeEnergySystem.OnEnergyChanged` → Event for energy changes

**EconomySystem (existing):**
- `EconomySystem.GetBalance(playerId)` → Current money
- `EconomySystem.OnBalanceChanged` → Event for money changes

**ActivitySystem (existing):**
- `ActivitySystem.GetActiveActivity(playerId)` → Current activity
- `ActivitySystem.OnActivityStarted` → Event for activity start
- `ActivitySystem.OnActivityEnded` → Event for activity end

**DetectionSystem (existing):**
- `DetectionSystem.OnPlayerDetected` → Event when caught (optional warning)
- `DetectionSystem.GetNearbyObservers(playerId)` → NPCs nearby (subtle indicator)

**PhoneUI (already spec'd):**
- `PhoneUI.GetUnreadCount()` → Unread messages count
- `PhoneUI.OnMessageReceived` → Event for new messages

### **Events Subscribed To:**

All HUD updates are event-driven (no polling in Update):
```csharp
TimeEnergySystem.OnTimeAdvanced += UpdateTimeDisplay;
TimeEnergySystem.OnEnergyChanged += UpdateEnergyDisplay;
EconomySystem.OnBalanceChanged += UpdateMoneyDisplay;
ActivitySystem.OnActivityStarted += UpdateActivityDisplay;
PhoneUI.OnMessageReceived += ShowNotification;
```

---

## **III. DATA STRUCTURES**

### **Enums**

```csharp
public enum HUDVisibility {
    Visible,        // Normal gameplay
    Hidden,         // Cutscenes, menus
    Minimal         // Phone open, some elements hidden
}

public enum NotificationType {
    Info,           // Neutral (event reminder)
    Success,        // Positive (job completed, money earned)
    Warning,        // Caution (low energy, boss nearby)
    Error,          // Negative (fired, caught)
    Message         // Phone message
}
```

### **Classes**

```csharp
[System.Serializable]
public class HUDNotification {
    public string id;
    public NotificationType type;
    public string title;
    public string message;
    public float timestamp;
    public float duration;          // Auto-dismiss after X seconds
    public bool isDismissed;
    public Sprite icon;
}

[System.Serializable]
public class HUDState {
    public HUDVisibility visibility;
    public string currentTime;      // Formatted: "14:30"
    public string currentDate;      // Formatted: "Mon, Jan 15"
    public float currentEnergy;
    public float maxEnergy;
    public float currentMoney;
    public string currentActivity;  // "Working" or ""
    public int unreadMessages;
    public List<HUDNotification> activeNotifications;
}
```

---

## **IV. PUBLIC API**

### **Core Methods**

```csharp
// Initialization
public void Initialize();

// Visibility Control
public void SetVisibility(HUDVisibility visibility);
public void Show();
public void Hide();
public HUDVisibility GetVisibility();

// Manual Updates (event-driven preferred)
public void UpdateTimeDisplay();
public void UpdateEnergyDisplay();
public void UpdateMoneyDisplay();
public void UpdateActivityDisplay();
public void UpdateMessageCount();

// Notifications
public string ShowNotification(string title, string message, NotificationType type, float duration = 3f);
public void DismissNotification(string notificationId);
public void DismissAllNotifications();

// State Queries
public HUDState GetState();

// Testing Helpers
public void SetStateForTesting(HUDState state);
public void ForceUpdateForTesting();
```

### **Events**

```csharp
// Visibility Events
public event Action<HUDVisibility> OnVisibilityChanged;

// Notification Events
public event Action<HUDNotification> OnNotificationShown;
public event Action<string> OnNotificationDismissed;
```

---

## **V. IMPLEMENTATION DETAILS**

### **Singleton Pattern**

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using HustleEconomy.Core;

namespace HustleEconomy.UI {
    public class HUDController : MonoBehaviour {
        // Singleton
        private static HUDController instance;
        public static HUDController Instance {
            get {
                if (instance == null) {
                    instance = FindObjectOfType<HUDController>();
                    if (instance == null) {
                        Debug.LogError("HUDController instance not found in scene");
                    }
                }
                return instance;
            }
        }

        // UI References (assigned in Inspector)
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

        // State
        private HUDState state;
        private Dictionary<string, GameObject> activeNotificationObjects;

        private void Awake() {
            if (instance != null && instance != this) {
                Destroy(gameObject);
                return;
            }
            instance = this;

            Initialize();
        }

        public void Initialize() {
            state = new HUDState {
                visibility = HUDVisibility.Visible,
                activeNotifications = new List<HUDNotification>()
            };

            activeNotificationObjects = new Dictionary<string, GameObject>();

            // Subscribe to system events
            SubscribeToEvents();

            // Initial update
            UpdateAllDisplays();
        }

        // ... (implementation continues)
    }
}
```

### **Event Subscription**

```csharp
private void SubscribeToEvents() {
    // Time/Energy
    TimeEnergySystem.Instance.OnTimeAdvanced += (playerId, hours) => UpdateTimeDisplay();
    TimeEnergySystem.Instance.OnEnergyChanged += (playerId, delta, reason) => UpdateEnergyDisplay();

    // Economy
    EconomySystem.Instance.OnBalanceChanged += (playerId, newBalance) => UpdateMoneyDisplay();

    // Activities
    ActivitySystem.Instance.OnActivityStarted += (activityId) => UpdateActivityDisplay();
    ActivitySystem.Instance.OnActivityEnded += (activityId) => UpdateActivityDisplay();

    // Phone
    PhoneUI.Instance.OnMessageReceived += (npcId) => {
        UpdateMessageCount();
        ShowNotification("New Message", "You have a new message", NotificationType.Message, 3f);
    };

    // Detection (optional subtle warning)
    DetectionSystem.Instance.OnPlayerDetected += (playerId, observerId, severity) => {
        if (severity > 0.7f) {
            ShowNotification("Caught!", "You were caught slacking", NotificationType.Error, 3f);
        }
    };
}

private void OnDestroy() {
    // Unsubscribe from all events
    if (TimeEnergySystem.Instance != null) {
        TimeEnergySystem.Instance.OnTimeAdvanced -= (playerId, hours) => UpdateTimeDisplay();
    }
    // ... (unsubscribe from all others)
}
```

### **Display Updates**

```csharp
private void UpdateAllDisplays() {
    UpdateTimeDisplay();
    UpdateEnergyDisplay();
    UpdateMoneyDisplay();
    UpdateActivityDisplay();
    UpdateMessageCount();
}

public void UpdateTimeDisplay() {
    float gameTime = TimeEnergySystem.Instance.GetCurrentGameTime();
    
    // Convert game time to hours:minutes
    int hours = Mathf.FloorToInt(gameTime);
    int minutes = Mathf.FloorToInt((gameTime - hours) * 60f);
    
    state.currentTime = $"{hours:D2}:{minutes:D2}";
    
    if (timeText != null) {
        timeText.text = state.currentTime;
    }

    // Date (if implemented in TimeEnergySystem)
    // For V1.0, might be stubbed
    var gameDate = TimeEnergySystem.Instance.GetGameDate();
    state.currentDate = $"{gameDate.dayOfWeek}, {gameDate.monthName} {gameDate.day}";
    
    if (dateText != null) {
        dateText.text = state.currentDate;
    }
}

public void UpdateEnergyDisplay() {
    state.currentEnergy = TimeEnergySystem.Instance.GetEnergy("player");
    state.maxEnergy = TimeEnergySystem.Instance.GetMaxEnergy("player");

    if (energySlider != null) {
        energySlider.maxValue = state.maxEnergy;
        energySlider.value = state.currentEnergy;
    }

    if (energyText != null) {
        energyText.text = $"{state.currentEnergy:F0}/{state.maxEnergy:F0}";
    }

    // Color warning if low
    if (state.currentEnergy < state.maxEnergy * 0.2f) {
        if (energySlider != null) {
            energySlider.fillRect.GetComponent<Image>().color = Color.red;
        }
    } else {
        if (energySlider != null) {
            energySlider.fillRect.GetComponent<Image>().color = Color.green;
        }
    }
}

public void UpdateMoneyDisplay() {
    state.currentMoney = EconomySystem.Instance.GetBalance("player");

    if (moneyText != null) {
        moneyText.text = $"${state.currentMoney:F2}";
    }
}

public void UpdateActivityDisplay() {
    var activity = ActivitySystem.Instance.GetActiveActivity("player");
    
    if (activity != null) {
        state.currentActivity = GetActivityDisplayName(activity.type);
    } else {
        state.currentActivity = "";
    }

    if (activityText != null) {
        activityText.text = state.currentActivity;
        activityText.gameObject.SetActive(!string.IsNullOrEmpty(state.currentActivity));
    }
}

public void UpdateMessageCount() {
    state.unreadMessages = PhoneUI.Instance.GetUnreadCount();

    if (messageNotificationBadge != null) {
        messageNotificationBadge.SetActive(state.unreadMessages > 0);
    }

    if (messageCountText != null) {
        messageCountText.text = state.unreadMessages.ToString();
    }
}

private string GetActivityDisplayName(ActivityType type) {
    switch (type) {
        case ActivityType.Work: return "Working";
        case ActivityType.DrugDealing: return "Dealing";
        case ActivityType.Streaming: return "Streaming";
        case ActivityType.DogWalking: return "Walking Dogs";
        default: return "";
    }
}
```

### **Notification System**

```csharp
public string ShowNotification(string title, string message, NotificationType type, float duration = 3f) {
    // Create notification data
    HUDNotification notification = new HUDNotification {
        id = System.Guid.NewGuid().ToString(),
        type = type,
        title = title,
        message = message,
        timestamp = Time.time,
        duration = duration,
        isDismissed = false
    };

    state.activeNotifications.Add(notification);

    // Create visual
    GameObject notifObject = Instantiate(notificationPrefab, notificationContainer);
    
    // Setup text
    var titleText = notifObject.transform.Find("Title").GetComponent<TextMeshProUGUI>();
    var messageText = notifObject.transform.Find("Message").GetComponent<TextMeshProUGUI>();
    
    if (titleText != null) titleText.text = title;
    if (messageText != null) messageText.text = message;

    // Color based on type
    Image background = notifObject.GetComponent<Image>();
    if (background != null) {
        background.color = GetNotificationColor(type);
    }

    // Track object
    activeNotificationObjects[notification.id] = notifObject;

    // Auto-dismiss after duration
    if (duration > 0f) {
        StartCoroutine(AutoDismissNotification(notification.id, duration));
    }

    OnNotificationShown?.Invoke(notification);
    return notification.id;
}

private Color GetNotificationColor(NotificationType type) {
    switch (type) {
        case NotificationType.Info: return new Color(0.2f, 0.4f, 0.8f, 0.9f);
        case NotificationType.Success: return new Color(0.2f, 0.8f, 0.2f, 0.9f);
        case NotificationType.Warning: return new Color(0.9f, 0.7f, 0.2f, 0.9f);
        case NotificationType.Error: return new Color(0.9f, 0.2f, 0.2f, 0.9f);
        case NotificationType.Message: return new Color(0.4f, 0.2f, 0.8f, 0.9f);
        default: return Color.gray;
    }
}

private IEnumerator AutoDismissNotification(string notificationId, float duration) {
    yield return new WaitForSeconds(duration);
    DismissNotification(notificationId);
}

public void DismissNotification(string notificationId) {
    // Find notification
    var notification = state.activeNotifications.Find(n => n.id == notificationId);
    if (notification == null) return;

    notification.isDismissed = true;

    // Destroy visual
    if (activeNotificationObjects.ContainsKey(notificationId)) {
        Destroy(activeNotificationObjects[notificationId]);
        activeNotificationObjects.Remove(notificationId);
    }

    // Remove from list
    state.activeNotifications.Remove(notification);

    OnNotificationDismissed?.Invoke(notificationId);
}

public void DismissAllNotifications() {
    var notificationIds = new List<string>(activeNotificationObjects.Keys);
    foreach (var id in notificationIds) {
        DismissNotification(id);
    }
}
```

### **Visibility Control**

```csharp
public void SetVisibility(HUDVisibility visibility) {
    if (state.visibility == visibility) return;

    state.visibility = visibility;

    switch (visibility) {
        case HUDVisibility.Visible:
            hudCanvas.enabled = true;
            // Show all elements
            break;

        case HUDVisibility.Hidden:
            hudCanvas.enabled = false;
            break;

        case HUDVisibility.Minimal:
            hudCanvas.enabled = true;
            // Hide some elements (e.g., activity text when phone open)
            if (activityText != null) {
                activityText.gameObject.SetActive(false);
            }
            break;
    }

    OnVisibilityChanged?.Invoke(visibility);
}

public void Show() {
    SetVisibility(HUDVisibility.Visible);
}

public void Hide() {
    SetVisibility(HUDVisibility.Hidden);
}
```

---

## **VI. UI LAYOUT**

### **HUD Positioning**

```
┌─────────────────────────────────────────┐
│ [Time: 14:30]  [Date: Mon, Jan 15]     │  Top-left: Time/Date
│                                         │
│                       [Energy: ▓▓▓▓▓░░] │  Top-right: Energy bar
│                       [$1,250.50]       │  Top-right: Money
│                       [📱 3]            │  Top-right: Message badge
│                                         │
│                                         │
│           [GAME WORLD]                  │
│                                         │
│                                         │
│ [Working]                               │  Bottom-left: Activity
│                                         │
│ [Notification: "New message received"]  │  Bottom-center: Notifications
└─────────────────────────────────────────┘
```

---

## **VII. INTEGRATION TESTS (POST-IMPLEMENTATION)**

### **Test Scenarios**

**Test 1: Time Update**
```
1. TimeEnergySystem advances 1 hour
2. OnTimeAdvanced event fires
3. HUDController.UpdateTimeDisplay() called
4. Time text updates: "14:30" → "15:30" ✅
```

**Test 2: Energy Depletion**
```
1. Player runs (drains energy)
2. TimeEnergySystem.OnEnergyChanged fires
3. HUDController.UpdateEnergyDisplay() called
4. Energy slider updates: 100 → 75
5. Energy < 20% → bar turns red ✅
```

**Test 3: Money Change**
```
1. Player earns $100
2. EconomySystem.OnBalanceChanged fires
3. HUDController.UpdateMoneyDisplay() called
4. Money text updates: "$1,250.50" → "$1,350.50" ✅
```

**Test 4: Activity Start/End**
```
1. Player starts job
2. ActivitySystem.OnActivityStarted fires
3. HUDController shows "Working"
4. Player ends job
5. ActivitySystem.OnActivityEnded fires
6. HUDController hides activity text ✅
```

**Test 5: Phone Message**
```
1. NPC sends message
2. PhoneUI.OnMessageReceived fires
3. HUDController shows notification
4. Message badge shows: 📱 1
5. After 3 seconds, notification auto-dismisses ✅
```

---

## **VIII. TESTING CHECKLIST**

### **Unit Tests (30 minimum)**

**Display Update Tests (10):**
- [ ] UpdateTimeDisplay formats time correctly
- [ ] UpdateEnergyDisplay updates slider value
- [ ] UpdateEnergyDisplay changes color when low
- [ ] UpdateMoneyDisplay formats currency correctly
- [ ] UpdateActivityDisplay shows activity name
- [ ] UpdateActivityDisplay hides when no activity
- [ ] UpdateMessageCount shows unread count
- [ ] UpdateMessageCount hides badge when zero
- [ ] Time event triggers update
- [ ] Energy event triggers update

**Notification Tests (8):**
- [ ] ShowNotification creates notification object
- [ ] ShowNotification returns notification ID
- [ ] DismissNotification removes notification
- [ ] DismissAllNotifications clears all
- [ ] Auto-dismiss triggers after duration
- [ ] Notification color matches type
- [ ] Multiple notifications stack correctly
- [ ] Notification event fires on show

**Visibility Tests (4):**
- [ ] SetVisibility(Visible) shows HUD
- [ ] SetVisibility(Hidden) hides HUD
- [ ] SetVisibility(Minimal) hides some elements
- [ ] Visibility event fires on change

**Integration Tests (8):**
- [ ] TimeEnergySystem event updates time display
- [ ] EconomySystem event updates money display
- [ ] ActivitySystem event updates activity display
- [ ] PhoneUI event shows notification
- [ ] DetectionSystem event shows warning (optional)
- [ ] All displays update on Initialize
- [ ] HUD persists across scenes
- [ ] Events unsubscribe on destroy

---

## **IX. KNOWN LIMITATIONS**

- No minimap
- No quest tracker
- No compass/waypoint system
- Simple notification stack (no categories)
- No HUD customization (position, size)

---

## **X. FUTURE ENHANCEMENTS (Post-V1.0)**

- HUD customization options
- Minimap with location markers
- Quest/objective tracker
- Notification categories/filtering
- Animated transitions for value changes
- Health bar (if combat added)
- Wanted level indicator (Heat visualization)

---

**END OF SPECIFICATION**
