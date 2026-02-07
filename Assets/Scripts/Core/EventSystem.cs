using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Core
{
    public class EventSystem : MonoBehaviour
    {
        public enum EventType
        {
            Birthday,
            Anniversary,
            Party,
            DateNight,
            FamilyDinner,
            WorkMeeting,
            SugarObligation,
            NetworkingEvent
        }

        [System.Serializable]
        public class GameEvent
        {
            public string id;
            public string name;
            public EventType type;
            public DateTime scheduledTime;
            public float durationHours;
            public string hostId;
            public List<string> attendees;
            public float attendBonus;
            public float skipPenalty;
            public string minigameId;
            public bool attended;
            public bool skipped;
            public bool reminded;
        }

        [System.Serializable]
        public struct EventData
        {
            public string name;
            public EventType type;
            public DateTime scheduledTime;
            public float durationHours;
            public string hostId;
            public List<string> attendees;
            public float attendBonus;
            public float skipPenalty;
            public string minigameId;
        }

        private static EventSystem instance;
        public static EventSystem Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<EventSystem>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("EventSystem");
                        instance = go.AddComponent<EventSystem>();
                    }
                }
                return instance;
            }
        }

        public event Action<GameEvent> OnEventCreated;
        public event Action<GameEvent> OnEventReminder;
        public event Action<string> OnEventAttended;
        public event Action<string> OnEventSkipped;
        public event Action<string, string> OnEventCancelled;

        private List<GameEvent> upcomingEvents;

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
            upcomingEvents = new List<GameEvent>();
        }

        public string CreateEvent(EventData data)
        {
            GameEvent evt = new GameEvent
            {
                id = Guid.NewGuid().ToString("N"),
                name = data.name,
                type = data.type,
                scheduledTime = data.scheduledTime,
                durationHours = data.durationHours,
                hostId = data.hostId,
                attendees = data.attendees != null ? new List<string>(data.attendees) : new List<string>(),
                attendBonus = data.attendBonus,
                skipPenalty = data.skipPenalty,
                minigameId = data.minigameId,
                attended = false,
                skipped = false,
                reminded = false
            };

            upcomingEvents.Add(evt);

            DateTime reminderTime = evt.scheduledTime.AddDays(-1);
            if (reminderTime > TimeEnergySystem.Instance.GetCurrentTime())
            {
                TimeEnergySystem.Instance.ScheduleEvent(
                    reminderTime,
                    () => TriggerReminder(evt.id),
                    $"Reminder: {evt.name}"
                );
            }

            OnEventCreated?.Invoke(evt);
            return evt.id;
        }

        public List<GameEvent> GetUpcomingEvents(int daysAhead = 7)
        {
            DateTime now = TimeEnergySystem.Instance.GetCurrentTime();
            DateTime limit = now.AddDays(daysAhead);
            return upcomingEvents
                .Where(evt => evt.scheduledTime >= now && evt.scheduledTime <= limit)
                .OrderBy(evt => evt.scheduledTime)
                .ToList();
        }

        public void AttendEvent(string eventId)
        {
            GameEvent evt = GetEvent(eventId);
            if (evt == null)
            {
                return;
            }

            TimeEnergySystem.Instance.AdvanceTime(evt.durationHours * 60f);
            evt.attended = true;

            if (!string.IsNullOrEmpty(evt.hostId))
            {
                RelationshipSystem.Instance.ModifyRelationship(
                    evt.hostId,
                    evt.attendBonus,
                    $"Attended {evt.name}"
                );
            }

            if (evt.attendees != null)
            {
                foreach (string attendeeId in evt.attendees)
                {
                    RelationshipSystem.Instance.ModifyRelationship(
                        attendeeId,
                        evt.attendBonus * 0.5f,
                        $"Saw you at {evt.name}"
                    );
                }
            }

            if (!string.IsNullOrEmpty(evt.minigameId))
            {
                Debug.LogWarning("TODO: ActivitySystem CreateActivity for event minigame");
            }

            RelationshipSystem.Instance.ObservePlayerAction(new RelationshipSystem.PlayerAction
            {
                type = RelationshipSystem.ActionType.AttendedEvent,
                details = evt.name,
                timestamp = evt.scheduledTime,
                memorability = 4,
                isPositive = true
            });

            OnEventAttended?.Invoke(eventId);
            upcomingEvents.Remove(evt);
        }

        public void SkipEvent(string eventId)
        {
            GameEvent evt = GetEvent(eventId);
            if (evt == null)
            {
                return;
            }

            evt.skipped = true;

            if (!string.IsNullOrEmpty(evt.hostId))
            {
                RelationshipSystem.Instance.ModifyRelationship(
                    evt.hostId,
                    -evt.skipPenalty,
                    $"Skipped {evt.name}"
                );
            }

            if (evt.attendees != null)
            {
                foreach (string attendeeId in evt.attendees)
                {
                    RelationshipSystem.Instance.ModifyRelationship(
                        attendeeId,
                        -evt.skipPenalty * 0.3f,
                        $"You didn't show up to {evt.name}"
                    );
                }
            }

            RelationshipSystem.Instance.ObservePlayerAction(new RelationshipSystem.PlayerAction
            {
                type = RelationshipSystem.ActionType.MissedEvent,
                details = evt.name,
                timestamp = evt.scheduledTime,
                memorability = 6
            });

            OnEventSkipped?.Invoke(eventId);
            upcomingEvents.Remove(evt);
        }

        public void CancelEvent(string eventId, string reason)
        {
            GameEvent evt = GetEvent(eventId);
            if (evt == null)
            {
                return;
            }

            upcomingEvents.Remove(evt);
            OnEventCancelled?.Invoke(eventId, reason);
        }

        public void TriggerReminder(string eventId)
        {
            GameEvent evt = GetEvent(eventId);
            if (evt == null || evt.attended || evt.skipped)
            {
                return;
            }

            evt.reminded = true;
            OnEventReminder?.Invoke(evt);
        }

        public void GenerateRelationshipEvents(string npcId, RelationshipSystem.NPCType type)
        {
            RelationshipSystem.NPC npc = RelationshipSystem.Instance.GetNPC(npcId);
            string npcName = npc != null && !string.IsNullOrEmpty(npc.name) ? npc.name : npcId;

            DateTime now = TimeEnergySystem.Instance.GetCurrentTime();

            if (type == RelationshipSystem.NPCType.RomanticPartner)
            {
                CreateEvent(new EventData
                {
                    name = $"Anniversary with {npcName}",
                    type = EventType.Anniversary,
                    scheduledTime = now.AddYears(1),
                    durationHours = 3f,
                    hostId = npcId,
                    attendees = new List<string>(),
                    attendBonus = 20f,
                    skipPenalty = 30f,
                    minigameId = null
                });

                for (int i = 1; i <= 12; i++)
                {
                    CreateEvent(new EventData
                    {
                        name = $"Date Night with {npcName}",
                        type = EventType.DateNight,
                        scheduledTime = now.AddMonths(i),
                        durationHours = 2f,
                        hostId = npcId,
                        attendees = new List<string>(),
                        attendBonus = 10f,
                        skipPenalty = 15f,
                        minigameId = null
                    });
                }
            }

            if (type == RelationshipSystem.NPCType.Family)
            {
                CreateEvent(new EventData
                {
                    name = $"{npcName}'s Birthday",
                    type = EventType.Birthday,
                    scheduledTime = now.AddDays(30),
                    durationHours = 2f,
                    hostId = npcId,
                    attendees = new List<string>(),
                    attendBonus = 15f,
                    skipPenalty = 25f,
                    minigameId = null
                });
            }
        }

        public GameEvent GetEventForTesting(string eventId)
        {
            return GetEvent(eventId);
        }

        private GameEvent GetEvent(string eventId)
        {
            return upcomingEvents.Find(evt => evt.id == eventId);
        }
    }
}
