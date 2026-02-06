using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Core;

namespace Tests.Core
{
    public class EventSystemTests
    {
        private GameObject eventGameObject;
        private GameObject timeGameObject;
        private GameObject relationshipGameObject;
        private EventSystem system;

        [SetUp]
        public void SetUp()
        {
            ResetSingleton(typeof(EventSystem));
            ResetSingleton(typeof(TimeEnergySystem));
            ResetSingleton(typeof(RelationshipSystem));

            timeGameObject = new GameObject("TimeEnergySystem");
            timeGameObject.AddComponent<TimeEnergySystem>();

            relationshipGameObject = new GameObject("RelationshipSystem");
            relationshipGameObject.AddComponent<RelationshipSystem>();

            eventGameObject = new GameObject("EventSystem");
            system = eventGameObject.AddComponent<EventSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            if (eventGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(eventGameObject);
            }

            if (relationshipGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(relationshipGameObject);
            }

            if (timeGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(timeGameObject);
            }
        }

        [Test]
        public void CreateEvent_SchedulesEventCorrectly()
        {
            EventSystem.EventData data = CreateData("Event A", DateTime.Now.AddDays(2));
            string id = system.CreateEvent(data);
            EventSystem.GameEvent evt = system.GetEventForTesting(id);
            Assert.AreEqual("Event A", evt.name, "Event name should match");
            Assert.AreEqual(EventSystem.EventType.Party, evt.type, "Event type should match");
        }

        [Test]
        public void CreateEvent_FiresOnEventCreated()
        {
            bool fired = false;
            system.OnEventCreated += evt => fired = true;
            system.CreateEvent(CreateData("Event A", DateTime.Now.AddDays(2)));
            Assert.IsTrue(fired, "OnEventCreated should fire");
        }

        [Test]
        public void GetUpcomingEvents_ReturnsWithinTimeframe()
        {
            DateTime now = TimeEnergySystem.Instance.GetCurrentTime();
            system.CreateEvent(CreateData("Soon", now.AddDays(3)));
            system.CreateEvent(CreateData("Later", now.AddDays(10)));
            List<EventSystem.GameEvent> upcoming = system.GetUpcomingEvents(7);
            Assert.AreEqual(1, upcoming.Count, "Should only return events within 7 days");
        }

        [Test]
        public void GetUpcomingEvents_SortsByTime()
        {
            DateTime now = TimeEnergySystem.Instance.GetCurrentTime();
            system.CreateEvent(CreateData("Later", now.AddDays(5)));
            system.CreateEvent(CreateData("Soon", now.AddDays(2)));
            List<EventSystem.GameEvent> upcoming = system.GetUpcomingEvents(7);
            Assert.AreEqual("Soon", upcoming[0].name, "Events should be sorted by time");
        }

        [Test]
        public void TriggerReminder_FiresOnEventReminder()
        {
            bool fired = false;
            system.OnEventReminder += evt => fired = true;
            string id = system.CreateEvent(CreateData("Event A", DateTime.Now.AddDays(2)));
            system.TriggerReminder(id);
            Assert.IsTrue(fired, "Reminder should fire");
        }

        [Test]
        public void TriggerReminder_DoesNotFireForAttended()
        {
            bool fired = false;
            system.OnEventReminder += evt => fired = true;
            string id = system.CreateEvent(CreateData("Event A", DateTime.Now.AddDays(2)));
            system.AttendEvent(id);
            system.TriggerReminder(id);
            Assert.IsFalse(fired, "Reminder should not fire for attended event");
        }

        [Test]
        public void AttendEvent_AdvancesTime()
        {
            DateTime before = TimeEnergySystem.Instance.GetCurrentTime();
            string id = system.CreateEvent(CreateData("Event A", DateTime.Now.AddDays(2), durationHours: 3f));
            system.AttendEvent(id);
            DateTime after = TimeEnergySystem.Instance.GetCurrentTime();
            Assert.Greater(after, before, "Attend should advance time");
        }

        [Test]
        public void AttendEvent_IncreasesHostRelationship()
        {
            RelationshipSystem.NPC npc = RelationshipSystem.Instance.CreateNPC(RelationshipSystem.NPCType.Friend, new RelationshipSystem.NPCData
            {
                name = "Host",
                personality = RelationshipSystem.NPCPersonality.Supportive,
                values = new Dictionary<RelationshipSystem.NPCValue, float>(),
                tolerances = new Dictionary<RelationshipSystem.NPCTolerance, RelationshipSystem.ToleranceLevel>(),
                sexualBoundary = RelationshipSystem.SexualBoundaryType.Monogamous
            });

            string id = system.CreateEvent(new EventSystem.EventData
            {
                name = "Event A",
                type = EventSystem.EventType.Party,
                scheduledTime = DateTime.Now.AddDays(2),
                durationHours = 2f,
                hostId = npc.id,
                attendees = new List<string>(),
                attendBonus = 10f,
                skipPenalty = 5f,
                minigameId = null
            });

            float before = RelationshipSystem.Instance.GetRelationshipScore(npc.id);
            system.AttendEvent(id);
            float after = RelationshipSystem.Instance.GetRelationshipScore(npc.id);
            Assert.AreEqual(before + 10f, after, 0.01f, "Attend should increase host relationship");
        }

        [Test]
        public void AttendEvent_RemovesFromCalendar()
        {
            string id = system.CreateEvent(CreateData("Event A", DateTime.Now.AddDays(2)));
            system.AttendEvent(id);
            Assert.IsNull(system.GetEventForTesting(id), "Attended event should be removed");
        }

        [Test]
        public void OnEventAttended_Fires()
        {
            bool fired = false;
            system.OnEventAttended += evtId => fired = true;
            string id = system.CreateEvent(CreateData("Event A", DateTime.Now.AddDays(2)));
            system.AttendEvent(id);
            Assert.IsTrue(fired, "OnEventAttended should fire");
        }

        [Test]
        public void SkipEvent_DecreasesHostRelationship()
        {
            RelationshipSystem.NPC npc = RelationshipSystem.Instance.CreateNPC(RelationshipSystem.NPCType.Friend, new RelationshipSystem.NPCData
            {
                name = "Host",
                personality = RelationshipSystem.NPCPersonality.Supportive,
                values = new Dictionary<RelationshipSystem.NPCValue, float>(),
                tolerances = new Dictionary<RelationshipSystem.NPCTolerance, RelationshipSystem.ToleranceLevel>(),
                sexualBoundary = RelationshipSystem.SexualBoundaryType.Monogamous
            });

            string id = system.CreateEvent(new EventSystem.EventData
            {
                name = "Event A",
                type = EventSystem.EventType.Party,
                scheduledTime = DateTime.Now.AddDays(2),
                durationHours = 2f,
                hostId = npc.id,
                attendees = new List<string>(),
                attendBonus = 10f,
                skipPenalty = 8f,
                minigameId = null
            });

            float before = RelationshipSystem.Instance.GetRelationshipScore(npc.id);
            system.SkipEvent(id);
            float after = RelationshipSystem.Instance.GetRelationshipScore(npc.id);
            float expected = before - 8f - 12.5f;
            Assert.AreEqual(expected, after, 0.01f, "Skip should decrease host relationship including memory impact");
        }

        [Test]
        public void SkipEvent_RemovesFromCalendar()
        {
            string id = system.CreateEvent(CreateData("Event A", DateTime.Now.AddDays(2)));
            system.SkipEvent(id);
            Assert.IsNull(system.GetEventForTesting(id), "Skipped event should be removed");
        }

        [Test]
        public void OnEventSkipped_Fires()
        {
            bool fired = false;
            system.OnEventSkipped += evtId => fired = true;
            string id = system.CreateEvent(CreateData("Event A", DateTime.Now.AddDays(2)));
            system.SkipEvent(id);
            Assert.IsTrue(fired, "OnEventSkipped should fire");
        }

        [Test]
        public void CancelEvent_RemovesFromCalendarAndFires()
        {
            bool fired = false;
            system.OnEventCancelled += (evtId, reason) => fired = true;
            string id = system.CreateEvent(CreateData("Event A", DateTime.Now.AddDays(2)));
            system.CancelEvent(id, "cancel");
            Assert.IsTrue(fired, "OnEventCancelled should fire");
            Assert.IsNull(system.GetEventForTesting(id), "Cancelled event should be removed");
        }

        [Test]
        public void MultipleEventsForSameNpc_CanBeScheduled()
        {
            DateTime now = TimeEnergySystem.Instance.GetCurrentTime();
            system.CreateEvent(CreateData("Event A", now.AddDays(2)));
            system.CreateEvent(CreateData("Event B", now.AddDays(3)));
            Assert.AreEqual(2, system.GetUpcomingEvents(7).Count, "Multiple events should be scheduled");
        }

        [Test]
        public void ConflictingEvents_BothAppear()
        {
            DateTime now = TimeEnergySystem.Instance.GetCurrentTime();
            system.CreateEvent(CreateData("Event A", now.AddDays(2)));
            system.CreateEvent(CreateData("Event B", now.AddDays(2)));
            Assert.AreEqual(2, system.GetUpcomingEvents(7).Count, "Conflicting events should both appear");
        }

        [Test]
        public void GenerateRelationshipEvents_RomanticPartner_CreatesThirteenEvents()
        {
            system.GenerateRelationshipEvents("npc1", RelationshipSystem.NPCType.RomanticPartner);
            Assert.AreEqual(13, system.GetUpcomingEvents(400).Count, "Romantic partner should create 13 events");
        }

        [Test]
        public void GenerateRelationshipEvents_Family_CreatesBirthday()
        {
            system.GenerateRelationshipEvents("npc2", RelationshipSystem.NPCType.Family);
            Assert.AreEqual(1, system.GetUpcomingEvents(400).Count, "Family should create one event");
        }

        [Test]
        public void TriggerReminder_DoesNotFireForSkipped()
        {
            bool fired = false;
            system.OnEventReminder += evt => fired = true;
            string id = system.CreateEvent(CreateData("Event A", DateTime.Now.AddDays(2)));
            system.SkipEvent(id);
            system.TriggerReminder(id);
            Assert.IsFalse(fired, "Reminder should not fire for skipped event");
        }

        [Test]
        public void GetUpcomingEvents_ZeroDays_ReturnsEmpty()
        {
            DateTime now = TimeEnergySystem.Instance.GetCurrentTime();
            system.CreateEvent(CreateData("Event A", now.AddDays(1)));
            Assert.AreEqual(0, system.GetUpcomingEvents(0).Count, "Zero days should return empty list");
        }

        [Test]
        public void GetUpcomingEvents_IncludesLimitDay()
        {
            DateTime now = TimeEnergySystem.Instance.GetCurrentTime();
            system.CreateEvent(CreateData("Event A", now.AddDays(7)));
            Assert.AreEqual(1, system.GetUpcomingEvents(7).Count, "Limit day event should be included");
        }

        [Test]
        public void AttendEvent_NullId_NoThrow()
        {
            system.AttendEvent(null);
            Assert.Pass("No exception for null id");
        }

        [Test]
        public void SkipEvent_InvalidId_NoThrow()
        {
            system.SkipEvent("missing");
            Assert.Pass("No exception for missing id");
        }

        [Test]
        public void CancelEvent_InvalidId_NoThrow()
        {
            system.CancelEvent("missing", "reason");
            Assert.Pass("No exception for missing id");
        }

        private static EventSystem.EventData CreateData(string name, DateTime time, float durationHours = 2f)
        {
            return new EventSystem.EventData
            {
                name = name,
                type = EventSystem.EventType.Party,
                scheduledTime = time,
                durationHours = durationHours,
                hostId = null,
                attendees = new List<string>(),
                attendBonus = 10f,
                skipPenalty = 5f,
                minigameId = null
            };
        }

        private static void ResetSingleton(Type systemType)
        {
            FieldInfo field = systemType.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(null, null);
            }
        }
    }
}
