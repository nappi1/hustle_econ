using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Core
{
    public class TimeEnergySystem : MonoBehaviour
    {
        [System.Serializable]
        public class ScheduledEvent
        {
            public string id;
            public DateTime scheduledTime;
            public Action callback;
            public string description;
            public bool cancelled;
        }

        [System.Serializable]
        public struct TimeState
        {
            public DateTime currentTime;
            public float energy;
            public float timeScale;
            public List<ScheduledEvent> pendingEvents;
        }

        private static TimeEnergySystem instance;
        public static TimeEnergySystem Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<TimeEnergySystem>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("TimeEnergySystem");
                        instance = go.AddComponent<TimeEnergySystem>();
                    }
                }
                return instance;
            }
        }

        public event Action<DateTime> OnTimeAdvanced;
        public event Action<float> OnEnergyChanged;
        public event Action OnEnergyDepleted;
        public event Action<float> OnSleep;
        public event Action<DateTime> OnDayChanged;

        private const float PASSIVE_DRAIN_PER_HOUR = 2f;

        private DateTime currentTime;
        private DateTime initialTime;
        private float timeScale = 1.0f;
        private float realTimeAccumulator = 0f;
        private float energy = 100f;
        private bool isSleeping = false;
        private Dictionary<string, ScheduledEvent> scheduledEvents;
        private Dictionary<string, float> recurringEventIntervals;
        private Dictionary<string, DateTime> recurringEventNextTimes;

        public event Action<string> OnRecurringEvent;

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
            currentTime = new DateTime(2024, 1, 1, 8, 0, 0);
            initialTime = currentTime;
            energy = 100f;
            timeScale = 1.0f;
            realTimeAccumulator = 0f;
            isSleeping = false;
            scheduledEvents = new Dictionary<string, ScheduledEvent>();
            recurringEventIntervals = new Dictionary<string, float>();
            recurringEventNextTimes = new Dictionary<string, DateTime>();
        }

        private void Update()
        {
            realTimeAccumulator += Time.deltaTime * timeScale;

            while (realTimeAccumulator >= 60f)
            {
                realTimeAccumulator -= 60f;
                AdvanceGameTime(60f);
            }

            if (!isSleeping)
            {
                float drainThisFrame = (PASSIVE_DRAIN_PER_HOUR / 3600f) * Time.deltaTime * timeScale;
                if (drainThisFrame > 0f)
                {
                    ModifyEnergy(-drainThisFrame, "passive_drain");
                }
            }
        }

        public DateTime GetCurrentTime()
        {
            return currentTime;
        }

        public float GetDeltaGameHours(float timestamp)
        {
            float currentHours = (float)(currentTime - initialTime).TotalHours;
            return currentHours - timestamp;
        }

        public void ScheduleRecurringEvent(string eventId, float intervalHours)
        {
            if (string.IsNullOrEmpty(eventId))
            {
                Debug.LogWarning("ScheduleRecurringEvent: eventId is null or empty");
                return;
            }

            if (intervalHours <= 0f)
            {
                Debug.LogWarning("ScheduleRecurringEvent: intervalHours must be positive");
                return;
            }

            recurringEventIntervals[eventId] = intervalHours;
            recurringEventNextTimes[eventId] = currentTime.AddHours(intervalHours);
        }

        public void CancelRecurringEvent(string eventId)
        {
            if (string.IsNullOrEmpty(eventId))
            {
                Debug.LogWarning("CancelRecurringEvent: eventId is null or empty");
                return;
            }

            recurringEventIntervals.Remove(eventId);
            recurringEventNextTimes.Remove(eventId);
        }

        public void AdvanceTime(float gameMinutes)
        {
            if (gameMinutes <= 0f)
            {
                return;
            }

            DateTime oldTime = currentTime;
            currentTime = currentTime.AddMinutes(gameMinutes);

            if (!isSleeping)
            {
                float drain = PASSIVE_DRAIN_PER_HOUR * (gameMinutes / 60f);
                if (drain > 0f)
                {
                    ModifyEnergy(-drain, "passive_drain_skip");
                }
            }

            HandleDayChanges(oldTime, currentTime);
            ProcessScheduledEvents(oldTime, currentTime);
            ProcessRecurringEvents(oldTime, currentTime);
            OnTimeAdvanced?.Invoke(currentTime);
        }

        public float GetEnergyLevel()
        {
            return energy;
        }

        public void ModifyEnergy(float delta, string reason)
        {
            float previousEnergy = energy;
            energy = Mathf.Clamp(energy + delta, 0f, 100f);

            if (!Mathf.Approximately(previousEnergy, energy))
            {
                OnEnergyChanged?.Invoke(energy);
            }

            if (previousEnergy > 0f && Mathf.Approximately(energy, 0f))
            {
                OnEnergyDepleted?.Invoke();
            }
        }

        public string ScheduleEvent(DateTime when, Action callback, string description)
        {
            if (when <= currentTime)
            {
                Debug.LogWarning("ScheduleEvent: cannot schedule event in the past");
                return null;
            }

            string id = Guid.NewGuid().ToString("N");
            ScheduledEvent scheduledEvent = new ScheduledEvent
            {
                id = id,
                scheduledTime = when,
                callback = callback,
                description = description,
                cancelled = false
            };

            scheduledEvents[id] = scheduledEvent;
            return id;
        }

        public bool CancelScheduledEvent(string eventId)
        {
            if (string.IsNullOrEmpty(eventId))
            {
                Debug.LogWarning("CancelScheduledEvent: eventId is null or empty");
                return false;
            }

            if (!scheduledEvents.TryGetValue(eventId, out ScheduledEvent scheduledEvent))
            {
                return false;
            }

            scheduledEvent.cancelled = true;
            scheduledEvents.Remove(eventId);
            return true;
        }

        public void SetTimeScale(float scale)
        {
            if (scale < 0f)
            {
                Debug.LogWarning("SetTimeScale: scale cannot be negative");
                scale = 0f;
            }

            timeScale = scale;
        }

        public void Sleep(float hours)
        {
            if (hours <= 0f)
            {
                return;
            }

            isSleeping = true;
            AdvanceTime(hours * 60f);
            ModifyEnergy(hours * 12.5f, "sleep");
            OnSleep?.Invoke(hours);
            isSleeping = false;
        }

        private void AdvanceGameTime(float minutes)
        {
            if (minutes <= 0f)
            {
                return;
            }

            DateTime oldTime = currentTime;
            currentTime = currentTime.AddMinutes(minutes);

            HandleDayChanges(oldTime, currentTime);
            ProcessScheduledEvents(oldTime, currentTime);
            ProcessRecurringEvents(oldTime, currentTime);
            OnTimeAdvanced?.Invoke(currentTime);
        }

        private void HandleDayChanges(DateTime oldTime, DateTime newTime)
        {
            if (oldTime.Date == newTime.Date)
            {
                return;
            }

            int daysCrossed = (newTime.Date - oldTime.Date).Days;
            for (int i = 1; i <= daysCrossed; i++)
            {
                DateTime dayTime = oldTime.Date.AddDays(i);
                OnDayChanged?.Invoke(dayTime);
            }
        }

        private void ProcessScheduledEvents(DateTime fromTime, DateTime toTime)
        {
            if (scheduledEvents.Count == 0)
            {
                return;
            }

            List<ScheduledEvent> eventsToTrigger = scheduledEvents.Values
                .Where(e => !e.cancelled && e.scheduledTime >= fromTime && e.scheduledTime <= toTime)
                .OrderBy(e => e.scheduledTime)
                .ToList();

            foreach (ScheduledEvent scheduledEvent in eventsToTrigger)
            {
                scheduledEvent.callback?.Invoke();
                scheduledEvents.Remove(scheduledEvent.id);
            }
        }

        private void ProcessRecurringEvents(DateTime fromTime, DateTime toTime)
        {
            if (recurringEventIntervals.Count == 0)
            {
                return;
            }

            List<string> eventIds = new List<string>(recurringEventIntervals.Keys);
            foreach (string eventId in eventIds)
            {
                if (!recurringEventNextTimes.TryGetValue(eventId, out DateTime nextTime))
                {
                    continue;
                }

                float intervalHours = recurringEventIntervals[eventId];
                if (intervalHours <= 0f)
                {
                    continue;
                }

                while (nextTime >= fromTime && nextTime <= toTime)
                {
                    OnRecurringEvent?.Invoke(eventId);
                    nextTime = nextTime.AddHours(intervalHours);
                }

                recurringEventNextTimes[eventId] = nextTime;
            }
        }
    }
}
