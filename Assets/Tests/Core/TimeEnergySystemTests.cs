using System;
using System.Collections.Generic;
using System.Reflection;
using Core;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Core
{
    public class TimeEnergySystemTests
    {
        private TimeEnergySystem _system;
        private GameObject _systemGameObject;

        [SetUp]
        public void SetUp()
        {
            _systemGameObject = new GameObject("TimeEnergySystem");
            _system = _systemGameObject.AddComponent<TimeEnergySystem>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_systemGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_systemGameObject);
            }
        }

        [Test]
        public void GetCurrentTime_AfterInitialization_ReturnsValidDateTime()
        {
            DateTime currentTime = _system.GetCurrentTime();
            Assert.AreEqual(2024, currentTime.Year, "Year should match initialization");
            Assert.AreEqual(1, currentTime.Month, "Month should match initialization");
            Assert.AreEqual(1, currentTime.Day, "Day should match initialization");
            Assert.AreEqual(8, currentTime.Hour, "Hour should match initialization");
            Assert.AreEqual(0, currentTime.Minute, "Minute should match initialization");
        }

        [Test]
        public void GetEnergyLevel_AfterInitialization_Returns100()
        {
            float energy = _system.GetEnergyLevel();
            Assert.AreEqual(100f, energy, 0.001f, "Energy should initialize at 100");
        }

        [Test]
        public void AdvanceTime_WithPositiveMinutes_AdvancesTimeCorrectly()
        {
            DateTime start = _system.GetCurrentTime();
            _system.AdvanceTime(30f);
            DateTime end = _system.GetCurrentTime();
            Assert.AreEqual(start.AddMinutes(30), end, "Time should advance by 30 minutes");
        }

        [Test]
        public void AdvanceTime_WithMultipleAdvances_AccumulatesCorrectly()
        {
            DateTime start = _system.GetCurrentTime();
            _system.AdvanceTime(15f);
            _system.AdvanceTime(45f);
            DateTime end = _system.GetCurrentTime();
            Assert.AreEqual(start.AddMinutes(60), end, "Time should accumulate multiple advances");
        }

        [Test]
        public void AdvanceTime_WithZero_DoesNotChangeTime()
        {
            DateTime start = _system.GetCurrentTime();
            _system.AdvanceTime(0f);
            DateTime end = _system.GetCurrentTime();
            Assert.AreEqual(start, end, "Zero advance should not change time");
        }

        [Test]
        public void Update_With60RealSeconds_AdvancesOneHour()
        {
            DateTime start = _system.GetCurrentTime();
            SetPrivateField("realTimeAccumulator", 60f);
            InvokePrivateMethod("Update");
            DateTime end = _system.GetCurrentTime();
            Assert.AreEqual(start.AddHours(1), end, "60 real seconds should advance one game hour");
        }

        [Test]
        public void SetTimeScale_WithDifferentValues_ChangesStoredScale()
        {
            _system.SetTimeScale(2f);
            float scale = GetPrivateField<float>("timeScale");
            Assert.AreEqual(2f, scale, 0.001f, "Time scale should be updated");
        }

        [Test]
        public void SetTimeScale_WithZero_StopsTime()
        {
            _system.SetTimeScale(0f);
            float scale = GetPrivateField<float>("timeScale");
            Assert.AreEqual(0f, scale, 0.001f, "Time scale should be set to zero");
        }

        [Test]
        public void SetTimeScale_WithNegative_ClampsToZero()
        {
            _system.SetTimeScale(-2f);
            float scale = GetPrivateField<float>("timeScale");
            Assert.AreEqual(0f, scale, 0.001f, "Negative scale should clamp to zero");
        }

        [Test]
        public void ModifyEnergy_WithPositiveDelta_IncreasesEnergy()
        {
            _system.ModifyEnergy(-50f, "drain");
            _system.ModifyEnergy(20f, "restore");
            float energy = _system.GetEnergyLevel();
            Assert.AreEqual(70f, energy, 0.001f, "Energy should increase by delta");
        }

        [Test]
        public void ModifyEnergy_WithNegativeDelta_DecreasesEnergy()
        {
            _system.ModifyEnergy(-15f, "drain");
            float energy = _system.GetEnergyLevel();
            Assert.AreEqual(85f, energy, 0.001f, "Energy should decrease by delta");
        }

        [Test]
        public void ModifyEnergy_ExceedingMax_ClampsAt100()
        {
            _system.ModifyEnergy(20f, "restore");
            float energy = _system.GetEnergyLevel();
            Assert.AreEqual(100f, energy, 0.001f, "Energy should clamp at 100");
        }

        [Test]
        public void ModifyEnergy_GoingBelowZero_ClampsAt0()
        {
            _system.ModifyEnergy(-200f, "drain");
            float energy = _system.GetEnergyLevel();
            Assert.AreEqual(0f, energy, 0.001f, "Energy should clamp at 0");
        }

        [Test]
        public void ModifyEnergy_ReachingZero_FiresDepletionEvent()
        {
            bool fired = false;
            _system.OnEnergyDepleted += () => fired = true;
            _system.ModifyEnergy(-200f, "drain");
            Assert.IsTrue(fired, "OnEnergyDepleted should fire when energy hits zero");
        }

        [Test]
        public void OnEnergyChanged_WhenEnergyModified_FiresWithCorrectValue()
        {
            float captured = -1f;
            _system.OnEnergyChanged += value => captured = value;
            _system.ModifyEnergy(-10f, "drain");
            Assert.AreEqual(90f, captured, 0.001f, "OnEnergyChanged should provide updated energy");
        }

        [Test]
        public void PassiveDrain_Over1Hour_Drains2Energy()
        {
            _system.AdvanceTime(60f);
            float energy = _system.GetEnergyLevel();
            Assert.AreEqual(98f, energy, 0.01f, "Passive drain should reduce energy by 2 per hour");
        }

        [Test]
        public void ScheduleEvent_WithValidTime_ReturnsEventId()
        {
            DateTime futureTime = _system.GetCurrentTime().AddMinutes(10);
            string id = _system.ScheduleEvent(futureTime, null, "test");
            Assert.IsFalse(string.IsNullOrEmpty(id), "ScheduleEvent should return an event id");
        }

        [Test]
        public void ScheduleEvent_InPast_DoesNotSchedule()
        {
            DateTime pastTime = _system.GetCurrentTime().AddMinutes(-1);
            string id = _system.ScheduleEvent(pastTime, () => { }, "past");
            Assert.IsTrue(string.IsNullOrEmpty(id), "Scheduling in the past should return null or empty");
        }

        [Test]
        public void ScheduleEvent_WhenTimeReached_FiresCallback()
        {
            bool fired = false;
            DateTime futureTime = _system.GetCurrentTime().AddMinutes(30);
            _system.ScheduleEvent(futureTime, () => fired = true, "test_event");
            _system.AdvanceTime(30f);
            Assert.IsTrue(fired, "Scheduled event should fire when time is reached");
        }

        [Test]
        public void ScheduleEvent_MultipleEvents_FiresInChronologicalOrder()
        {
            List<int> order = new List<int>();
            DateTime now = _system.GetCurrentTime();
            _system.ScheduleEvent(now.AddMinutes(30), () => order.Add(2), "event_2");
            _system.ScheduleEvent(now.AddMinutes(10), () => order.Add(1), "event_1");
            _system.ScheduleEvent(now.AddMinutes(50), () => order.Add(3), "event_3");
            _system.AdvanceTime(60f);
            Assert.AreEqual(3, order.Count, "All events should fire");
            Assert.AreEqual(1, order[0], "First event should fire first");
            Assert.AreEqual(2, order[1], "Second event should fire second");
            Assert.AreEqual(3, order[2], "Third event should fire third");
        }

        [Test]
        public void AdvanceTime_PastMultipleEvents_ProcessesAllInOrder()
        {
            List<int> order = new List<int>();
            DateTime now = _system.GetCurrentTime();
            _system.ScheduleEvent(now.AddMinutes(5), () => order.Add(1), "event_1");
            _system.ScheduleEvent(now.AddMinutes(15), () => order.Add(2), "event_2");
            _system.ScheduleEvent(now.AddMinutes(25), () => order.Add(3), "event_3");
            _system.AdvanceTime(30f);
            Assert.AreEqual(3, order.Count, "All events should fire during time skip");
            Assert.AreEqual(1, order[0], "Events should be ordered by time");
            Assert.AreEqual(2, order[1], "Events should be ordered by time");
            Assert.AreEqual(3, order[2], "Events should be ordered by time");
        }

        [Test]
        public void CancelScheduledEvent_WithValidId_PreventsEventFromFiring()
        {
            bool fired = false;
            DateTime futureTime = _system.GetCurrentTime().AddMinutes(15);
            string id = _system.ScheduleEvent(futureTime, () => fired = true, "cancel_me");
            bool cancelled = _system.CancelScheduledEvent(id);
            _system.AdvanceTime(15f);
            Assert.IsTrue(cancelled, "CancelScheduledEvent should return true for valid id");
            Assert.IsFalse(fired, "Cancelled event should not fire");
        }

        [Test]
        public void CancelScheduledEvent_WithInvalidId_ReturnsFalse()
        {
            bool cancelled = _system.CancelScheduledEvent("does_not_exist");
            Assert.IsFalse(cancelled, "CancelScheduledEvent should return false for invalid id");
        }

        [Test]
        public void CancelScheduledEvent_AfterEventFired_ReturnsFalse()
        {
            DateTime futureTime = _system.GetCurrentTime().AddMinutes(5);
            string id = _system.ScheduleEvent(futureTime, () => { }, "fired_event");
            _system.AdvanceTime(5f);
            bool cancelled = _system.CancelScheduledEvent(id);
            Assert.IsFalse(cancelled, "CancelScheduledEvent should return false after event fired");
        }

        [Test]
        public void OnTimeAdvanced_WhenTimeAdvances_FiresWithCorrectTime()
        {
            DateTime captured = DateTime.MinValue;
            _system.OnTimeAdvanced += time => captured = time;
            DateTime expected = _system.GetCurrentTime().AddMinutes(20);
            _system.AdvanceTime(20f);
            Assert.AreEqual(expected, captured, "OnTimeAdvanced should fire with updated time");
        }

        [Test]
        public void Sleep_With8Hours_RestoresEnergyToFull()
        {
            _system.ModifyEnergy(-50f, "drain");
            _system.Sleep(8f);
            float energy = _system.GetEnergyLevel();
            Assert.AreEqual(100f, energy, 0.001f, "8 hours should restore energy to full");
        }

        [Test]
        public void Sleep_With4Hours_RestoresEnergyPartially()
        {
            _system.ModifyEnergy(-60f, "drain");
            _system.Sleep(4f);
            float energy = _system.GetEnergyLevel();
            Assert.AreEqual(90f, energy, 0.001f, "4 hours should restore 50 energy");
        }

        [Test]
        public void Sleep_AdvancesTimeBySpecifiedHours()
        {
            DateTime start = _system.GetCurrentTime();
            _system.Sleep(3f);
            DateTime end = _system.GetCurrentTime();
            Assert.AreEqual(start.AddHours(3), end, "Sleep should advance time by hours");
        }

        [Test]
        public void Sleep_WhileEnergyFull_KeepsEnergyAt100()
        {
            _system.Sleep(2f);
            float energy = _system.GetEnergyLevel();
            Assert.AreEqual(100f, energy, 0.001f, "Energy should not exceed 100 during sleep");
        }

        [Test]
        public void Sleep_FiresOnSleepEvent()
        {
            float capturedHours = 0f;
            _system.OnSleep += hours => capturedHours = hours;
            _system.Sleep(5f);
            Assert.AreEqual(5f, capturedHours, 0.001f, "OnSleep should fire with hours slept");
        }

        [Test]
        public void Sleep_ProcessesScheduledEventsDuringSleep()
        {
            bool fired = false;
            DateTime futureTime = _system.GetCurrentTime().AddHours(2);
            _system.ScheduleEvent(futureTime, () => fired = true, "sleep_event");
            _system.Sleep(4f);
            Assert.IsTrue(fired, "Scheduled event should fire during sleep");
        }

        [Test]
        public void OnDayChanged_AtMidnight_Fires()
        {
            int firedCount = 0;
            DateTime captured = DateTime.MinValue;
            _system.OnDayChanged += time =>
            {
                firedCount++;
                captured = time;
            };

            DateTime now = _system.GetCurrentTime();
            int minutesUntilMidnight = (24 - now.Hour) * 60 - now.Minute;
            _system.AdvanceTime(minutesUntilMidnight);

            Assert.AreEqual(1, firedCount, "OnDayChanged should fire at midnight");
            Assert.AreEqual(0, captured.Hour, "OnDayChanged should pass midnight hour");
            Assert.AreEqual(0, captured.Minute, "OnDayChanged should pass midnight minute");
        }

        [Test]
        public void OnDayChanged_WithMultipleDaySkip_FiresMultipleTimes()
        {
            int firedCount = 0;
            _system.OnDayChanged += time => firedCount++;
            _system.AdvanceTime(48f * 60f);
            Assert.AreEqual(2, firedCount, "OnDayChanged should fire once per day crossed");
        }

        [Test]
        public void Singleton_OnlyOneInstanceExists()
        {
            TimeEnergySystem first = TimeEnergySystem.Instance;
            TimeEnergySystem second = TimeEnergySystem.Instance;
            Assert.AreSame(first, second, "Instance should return same reference");
        }

        [Test]
        public void DontDestroyOnLoad_PersistsAcrossScenes()
        {
            UnityEngine.Object.DontDestroyOnLoad(_system.gameObject);
            string sceneName = _system.gameObject.scene.name;
            Assert.AreEqual("DontDestroyOnLoad", sceneName, "System should persist across scene changes");
        }

        private T GetPrivateField<T>(string fieldName)
        {
            FieldInfo field = typeof(TimeEnergySystem).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field {fieldName} should exist");
            return (T)field.GetValue(_system);
        }

        private void SetPrivateField(string fieldName, object value)
        {
            FieldInfo field = typeof(TimeEnergySystem).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field {fieldName} should exist");
            field.SetValue(_system, value);
        }

        private void InvokePrivateMethod(string methodName)
        {
            MethodInfo method = typeof(TimeEnergySystem).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, $"Method {methodName} should exist");
            method.Invoke(_system, null);
        }
    }
}
