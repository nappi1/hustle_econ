# V2 Update (Aligned to Current Codebase)

This V2 spec reflects the current implementation. See docs/IMPLEMENTATION_DISCREPANCIES.md for cross-system gaps.

## Implementation Notes

- Uses TimeEnergySystem.GetCurrentGameTime; RelationshipSystem.GetNPC/GetNPCs used for names.
- Uses ActivitySystem.CreateActivity overload for phone app activity.
- Notification data remains boolean-only.

---
# PHONE UI SPECIFICATION

**System:** PhoneUI  
**Namespace:** HustleEconomy.UI  
**Dependencies:** CameraController, InputManager, ActivitySystem, EconomySystem, RelationshipSystem  
**Purpose:** Phone overlay interface for secondary activities during gameplay

---

## **I. OVERVIEW**

### **What This System Does**

PhoneUI provides a visual phone interface overlay that:
- Displays as 25% of viewport center (world visible around edges)
- Shows apps (Messages, Drug Dealing, Banking, Social Media, etc.)
- Routes interactions to appropriate systems
- Triggers camera mode changes
- Enables multitasking tension (phone use while working/walking)

### **Design Philosophy**

- **Overlay, not fullscreen:** 75% of screen shows world for peripheral awareness
- **System router:** UI layer only, logic handled by ActivitySystem/EconomySystem
- **Context-aware:** Available during most activities except cutscenes
- **Simple first:** V1.0 focuses on text-based apps, visual polish later

---

## **II. INTEGRATION POINTS (CRITICAL)**

### **Resolves These TODOs:**

From IMPLEMENTATION_DISCREPANCIES.md:
- Line 7: "PhoneSystem missing" ??? PhoneUI is sufficient, no separate system needed
- Line 80: ActivitySystem "Camera mode switching is TODO" ??? PhoneUI triggers camera changes
- Line 83: ActivitySystem needs phone-based activities ??? PhoneUI routes to ActivitySystem

### **Requires These System APIs:**

**CameraController (already spec'd):**
- `CameraController.OnPhoneOpened()` - Switch to first-person
- `CameraController.OnPhoneClosed()` - Return to third-person (context-aware)

**ActivitySystem (existing):**
- `ActivitySystem.StartActivity(activityId)` - Start phone-based activity
- `ActivitySystem.EndActivity(activityId)` - End phone activity
- `ActivitySystem.GetActiveActivity(playerId)` - Check if working (determines camera return)

**EconomySystem (existing):**
- `EconomySystem.GetBalance(playerId)` - Display current money
- `EconomySystem.GetTransactions(playerId, count)` - Banking app history

**RelationshipSystem (existing):**
- `RelationshipSystem.GetNPCs(playerId)` - Contacts list ?????? MISSING API (line 10)
- `RelationshipSystem.GetRelationship(playerId, npcId)` - Message context

**InputManager (already spec'd):**
- `InputManager.GetActionDown(InputAction.OpenPhone)` - Detect P key
- `InputManager.SetContext(InputContext.Phone)` - Mouse cursor visible

### **New System Integration:**

**MinigameUI (spec in progress):**
- Phone-based minigames (drug dealing) render inside phone screen
- PhoneUI provides phone screen bounds, MinigameUI renders within

---

## **III. DATA STRUCTURES**

### **Enums**

```csharp
public enum PhoneApp {
    Home,           // Main screen with app icons
    Messages,       // Text messages with NPCs
    DrugDealing,    // Drug deal management (routes to ActivitySystem)
    Banking,        // View balance, transactions
    SocialMedia,    // Post updates, view feed (future)
    Calendar,       // View scheduled events (EventSystem integration)
    Contacts,       // NPC list with relationship scores
    Settings        // Volume, notifications, etc.
}

public enum PhoneState {
    Closed,         // Not visible
    Opening,        // Transition animation
    Open,           // Fully visible, interactive
    Closing         // Transition animation
}
```

### **Classes**

```csharp
[System.Serializable]
public class PhoneMessage {
    public string npcId;
    public string npcName;
    public string messageText;
    public float timestamp;      // Game time when sent
    public bool isRead;
    public bool isPlayerMessage; // True if player sent, false if NPC sent
}

[System.Serializable]
public class PhoneAppData {
    public PhoneApp appType;
    public string displayName;
    public Sprite iconSprite;
    public bool hasNotification;     // Red dot on icon
    public int notificationCount;    // Badge number
    public bool isLocked;            // Requires unlock (relationship/progression)
}

[System.Serializable]
public class PhoneUIState {
    public PhoneState state;
    public PhoneApp currentApp;
    public PhoneApp previousApp;     // For back button
    public List<PhoneMessage> messages;
    public float openedAt;           // Game time
    public bool isActivityRunning;   // Phone-based activity active
}
```

---

## **IV. PUBLIC API**

### **Core Methods**

```csharp
// Initialization
public void Initialize();

// Phone Control
public void OpenPhone();
public void ClosePhone();
public void TogglePhone();          // P key handler
public bool IsPhoneOpen();

// Navigation
public void OpenApp(PhoneApp app);
public void GoBack();               // Return to previous app
public void GoHome();               // Return to home screen

// Messages
public void SendMessage(string npcId, string messageText);
public List<PhoneMessage> GetMessagesWithNPC(string npcId);
public void MarkMessagesRead(string npcId);
public int GetUnreadCount();

// Notifications
public void ShowNotification(string title, string message);
public void ClearNotification(PhoneApp app);

// State Queries
public PhoneState GetState();
public PhoneApp GetCurrentApp();
public bool IsActivityRunning();

// Testing Helpers
public void SetStateForTesting(PhoneState state);
public void AddMessageForTesting(PhoneMessage message);
public PhoneUIState GetStateForTesting();
```

### **Events**

```csharp
// Phone State Events
public event Action OnPhoneOpened;
public event Action OnPhoneClosed;
public event Action<PhoneApp> OnAppOpened;

// Message Events
public event Action<string> OnMessageSent;      // npcId
public event Action<string> OnMessageReceived;  // npcId

// Notification Events
public event Action<PhoneApp> OnNotificationShown;
```

---

## **V. IMPLEMENTATION DETAILS**

### **Singleton Pattern**

```csharp
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using HustleEconomy.Core;

namespace HustleEconomy.UI {
    public class PhoneUI : MonoBehaviour {
        // Singleton
        private static PhoneUI instance;
        public static PhoneUI Instance {
            get {
                if (instance == null) {
                    instance = FindObjectOfType<PhoneUI>();
                    if (instance == null) {
                        Debug.LogError("PhoneUI instance not found in scene");
                    }
                }
                return instance;
            }
        }

        // UI References (assigned in Inspector)
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

        // State
        private PhoneUIState state;
        private Dictionary<PhoneApp, GameObject> appScreens;

        private void Awake() {
            if (instance != null && instance != this) {
                Destroy(gameObject);
                return;
            }
            instance = this;

            Initialize();
        }

        public void Initialize() {
            state = new PhoneUIState {
                state = PhoneState.Closed,
                currentApp = PhoneApp.Home,
                messages = new List<PhoneMessage>()
            };

            // Map app enums to screen GameObjects
            appScreens = new Dictionary<PhoneApp, GameObject> {
                { PhoneApp.Home, homeScreen },
                { PhoneApp.Messages, messagesScreen },
                { PhoneApp.Banking, bankingScreen },
                { PhoneApp.Contacts, contactsScreen }
            };

            // Start with phone closed
            phoneCanvas.enabled = false;
        }

        // ... (implementation continues)
    }
}
```

### **Opening/Closing Phone**

```csharp
private void Update() {
    // Listen for phone toggle input
    if (InputManager.Instance.GetActionDown(InputAction.OpenPhone)) {
        TogglePhone();
    }
}

public void TogglePhone() {
    if (state.state == PhoneState.Open) {
        ClosePhone();
    } else if (state.state == PhoneState.Closed) {
        OpenPhone();
    }
}

public void OpenPhone() {
    if (state.state != PhoneState.Closed) return;

    state.state = PhoneState.Opening;
    state.openedAt = TimeEnergySystem.Instance.GetCurrentGameTime();

    // Trigger camera switch
    CameraController.Instance.OnPhoneOpened();

    // Change input context (mouse visible, movement still works)
    InputManager.Instance.PushContext(InputContext.Phone);

    // Animate phone appearing
    StartCoroutine(PhoneTransitionCoroutine(true));

    OnPhoneOpened?.Invoke();
}

public void ClosePhone() {
    if (state.state != PhoneState.Open) return;

    state.state = PhoneState.Closing;

    // Trigger camera return (context-aware)
    CameraController.Instance.OnPhoneClosed();

    // Restore previous input context
    InputManager.Instance.PopContext();

    // Animate phone disappearing
    StartCoroutine(PhoneTransitionCoroutine(false));

    // End any phone-based activity
    if (state.isActivityRunning) {
        var activity = ActivitySystem.Instance.GetActiveActivity("player");
        if (activity != null && IsPhoneActivity(activity.type)) {
            ActivitySystem.Instance.EndActivity(activity.id);
            state.isActivityRunning = false;
        }
    }

    OnPhoneClosed?.Invoke();
}

private IEnumerator PhoneTransitionCoroutine(bool opening) {
    float elapsed = 0f;
    Vector3 startScale = opening ? Vector3.zero : Vector3.one;
    Vector3 endScale = opening ? Vector3.one : Vector3.zero;

    phoneCanvas.enabled = true;

    while (elapsed < transitionDuration) {
        elapsed += Time.deltaTime;
        float t = elapsed / transitionDuration;
        
        phoneScreen.localScale = Vector3.Lerp(startScale, endScale, t);
        
        yield return null;
    }

    phoneScreen.localScale = endScale;

    if (opening) {
        state.state = PhoneState.Open;
    } else {
        state.state = PhoneState.Closed;
        phoneCanvas.enabled = false;
    }
}

private bool IsPhoneActivity(ActivityType type) {
    return type == ActivityType.DrugDealing || 
           type == ActivityType.Trading ||
           type == ActivityType.SocialMedia;
}
```

### **App Navigation**

```csharp
public void OpenApp(PhoneApp app) {
    if (state.state != PhoneState.Open) {
        Debug.LogWarning("Cannot open app - phone is closed");
        return;
    }

    // Save previous app for back button
    state.previousApp = state.currentApp;
    state.currentApp = app;

    // Hide all screens
    foreach (var screen in appScreens.Values) {
        screen.SetActive(false);
    }

    // Show target screen
    if (appScreens.ContainsKey(app)) {
        appScreens[app].SetActive(true);
    }

    // Handle app-specific logic
    switch (app) {
        case PhoneApp.DrugDealing:
            // Start drug dealing activity
            string activityId = ActivitySystem.Instance.CreateActivity(
                "player",
                ActivityType.DrugDealing,
                "phone_drug_dealing"
            );
            ActivitySystem.Instance.StartActivity(activityId);
            state.isActivityRunning = true;
            break;

        case PhoneApp.Banking:
            RefreshBankingData();
            break;

        case PhoneApp.Messages:
            MarkCurrentMessagesRead();
            break;

        case PhoneApp.Contacts:
            RefreshContactsList();
            break;
    }

    OnAppOpened?.Invoke(app);
}

public void GoBack() {
    if (state.previousApp != state.currentApp) {
        OpenApp(state.previousApp);
    } else {
        GoHome();
    }
}

public void GoHome() {
    OpenApp(PhoneApp.Home);
}
```

### **Messages System**

```csharp
public void SendMessage(string npcId, string messageText) {
    PhoneMessage message = new PhoneMessage {
        npcId = npcId,
        npcName = GetNPCName(npcId),
        messageText = messageText,
        timestamp = TimeEnergySystem.Instance.GetCurrentGameTime(),
        isRead = true,
        isPlayerMessage = true
    };

    state.messages.Add(message);

    // TODO: RelationshipSystem.ObservePlayerAction for messaging frequency
    // This affects relationship over time

    OnMessageSent?.Invoke(npcId);
}

public void ReceiveMessage(string npcId, string messageText) {
    PhoneMessage message = new PhoneMessage {
        npcId = npcId,
        npcName = GetNPCName(npcId),
        messageText = messageText,
        timestamp = TimeEnergySystem.Instance.GetCurrentGameTime(),
        isRead = false,
        isPlayerMessage = false
    };

    state.messages.Add(message);

    // Show notification if phone is closed
    if (state.state == PhoneState.Closed) {
        ShowNotification("New Message", $"{message.npcName}: {messageText}");
    }

    OnMessageReceived?.Invoke(npcId);
}

public List<PhoneMessage> GetMessagesWithNPC(string npcId) {
    return state.messages.FindAll(m => m.npcId == npcId);
}

public void MarkMessagesRead(string npcId) {
    foreach (var message in state.messages) {
        if (message.npcId == npcId && !message.isRead) {
            message.isRead = true;
        }
    }
}

public int GetUnreadCount() {
    return state.messages.FindAll(m => !m.isRead).Count;
}

private string GetNPCName(string npcId) {
    // ?????? REQUIRES: RelationshipSystem.GetNPC(npcId) - currently missing (line 10)
    // For now, use EntitySystem as fallback
    var entity = EntitySystem.Instance.GetEntity(npcId);
    return entity != null ? entity.name : "Unknown";
}
```

### **Banking App**

```csharp
private void RefreshBankingData() {
    // Display current balance
    float balance = EconomySystem.Instance.GetBalance("player");
    
    // Display recent transactions
    var transactions = EconomySystem.Instance.GetTransactionHistory("player", 10);
    
    // Update UI (implementation depends on UI framework)
    // This would populate TextMeshPro components, etc.
}
```

### **Contacts App**

```csharp
private void RefreshContactsList() {
    // ?????? REQUIRES: RelationshipSystem.GetNPCs(playerId) - currently missing (line 10)
    // For V1.0 prototype, stub with placeholder data
    
    // TODO: Query RelationshipSystem for all NPCs
    // TODO: Display with relationship scores
    // TODO: Quick actions (call, message, view profile)
}
```

---

## **VI. UI LAYOUT**

### **Phone Screen Positioning**

```csharp
private void PositionPhoneScreen() {
    // Phone appears in center 25% of viewport
    phoneCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
    phoneCanvas.sortingOrder = 100; // Above HUD
    
    phoneScreen.anchorMin = new Vector2(0.375f, 0.25f);  // Center-ish
    phoneScreen.anchorMax = new Vector2(0.625f, 0.75f);
    phoneScreen.sizeDelta = phoneScreenSize;
    
    // World still visible 75% around edges
}
```

### **App Icons Layout (Home Screen)**

```
?????????????????????????????????????????????????????????????????????
???  Phone Home Screen  ???
?????????????????????????????????????????????????????????????????????
???                     ???
???  [????] [????] [????]     ???  Row 1: Messages, Banking, Calendar
???  Messages Bank Cal  ???
???                     ???
???  [????] [????] [??????]     ???  Row 2: Drugs, Contacts, Settings
???  Deals Cont. Set.   ???
???                     ???
???  [Time: 14:30]      ???  Status bar (game time)
???  [$1,250]           ???  Balance quick view
?????????????????????????????????????????????????????????????????????
```

---

## **VII. INTEGRATION TESTS (POST-IMPLEMENTATION)**

### **Test Scenarios**

**Test 1: Phone Opens During Work**
```
1. Player starts janitorial job (ActivitySystem, CameraController)
2. Camera switches to first-person
3. Player presses P
4. PhoneUI opens (camera stays first-person)
5. World visible around phone edges
6. Boss approaches (visible in peripheral)
7. Player closes phone
8. Camera stays first-person (still working)
```

**Test 2: Phone Triggers Activity**
```
1. Player opens phone
2. Opens Drug Dealing app
3. ActivitySystem.StartActivity called
4. MinigameUI renders inside phone screen
5. Player completes deal
6. ActivitySystem.EndActivity called
7. Phone returns to home screen
```

**Test 3: Message Notification**
```
1. Phone closed
2. NPC sends message via RelationshipSystem
3. PhoneUI.ReceiveMessage called
4. Notification appears on HUD
5. Player opens phone
6. Messages app shows unread badge
7. Player opens messages
8. Message marked read
9. Badge cleared
```

**Test 4: Context-Aware Camera Return**
```
1. Player working (first-person)
2. Opens phone (stays first-person)
3. Closes phone while working (stays first-person) ???
4. Ends work shift
5. Camera returns to third-person ???
```

---

## **VIII. MISSING APIS REQUIRED**

### **To Be Added During Integration:**

**RelationshipSystem:**
```csharp
// Line 10: Missing APIs
public List<NPC> GetNPCs(string playerId);
public NPC GetNPC(string npcId);
```

**ActivitySystem:**
```csharp
// Already exists, just document usage
public string CreateActivity(string playerId, ActivityType type, string context);
public void StartActivity(string activityId);
public void EndActivity(string activityId);
public Activity GetActiveActivity(string playerId);
```

---

## **IX. TESTING CHECKLIST**

### **Unit Tests (30 minimum)**

**Phone State Tests (8):**
- [ ] Phone opens from closed state
- [ ] Phone closes from open state
- [ ] Toggle switches between open/closed
- [ ] Cannot open when already open
- [ ] Cannot close when already closed
- [ ] Opening fires event
- [ ] Closing fires event
- [ ] Transition duration respects setting

**App Navigation Tests (6):**
- [ ] OpenApp switches to target app
- [ ] GoBack returns to previous app
- [ ] GoHome returns to home screen
- [ ] Opening app fires event
- [ ] App screens hide/show correctly
- [ ] Cannot open app when phone closed

**Message Tests (8):**
- [ ] SendMessage adds to messages list
- [ ] ReceiveMessage adds to messages list
- [ ] GetMessagesWithNPC filters correctly
- [ ] MarkMessagesRead updates read status
- [ ] GetUnreadCount returns correct count
- [ ] Message received fires event
- [ ] Message sent fires event
- [ ] Notification shown when phone closed

**Integration Tests (8):**
- [ ] Opening phone triggers CameraController
- [ ] Closing phone triggers CameraController
- [ ] Opening phone changes InputManager context
- [ ] Closing phone restores InputManager context
- [ ] Drug Dealing app starts ActivitySystem activity
- [ ] Closing phone ends phone activities
- [ ] Banking app displays EconomySystem balance
- [ ] Contacts app displays RelationshipSystem NPCs (when API added)

---

## **X. KNOWN LIMITATIONS**

- No phone customization (themes, wallpapers) in V1.0
- No voice calls, only text messages
- No camera/photos app
- No multiplayer messaging
- Text-only messages (no images/emojis)
- Simple notification system (no persistence)

---

## **XI. FUTURE ENHANCEMENTS (Post-V1.0)**

- Voice calls with dialogue trees
- Camera app (selfies affect BodySystem vanity)
- Social media posting (reputation effects)
- Photo gallery
- GPS/maps integration
- Phone upgrades (unlock apps via progression)
- Custom ringtones and themes

---

**END OF SPECIFICATION**

