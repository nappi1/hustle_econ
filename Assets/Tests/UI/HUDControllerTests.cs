using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Core;
using UI;

namespace Tests.UI
{
    public class HUDControllerTests
    {
        private GameObject hudGameObject;
        private GameObject timeGameObject;
        private GameObject economyGameObject;
        private GameObject activityGameObject;
        private GameObject phoneGameObject;
        private GameObject detectionGameObject;
        private HUDController hud;

        [SetUp]
        public void SetUp()
        {
            ResetSingleton(typeof(HUDController));
            ResetSingleton(typeof(TimeEnergySystem));
            ResetSingleton(typeof(EconomySystem));
            ResetSingleton(typeof(ActivitySystem));
            ResetSingleton(typeof(PhoneUI));
            ResetSingleton(typeof(DetectionSystem));

            timeGameObject = new GameObject("TimeEnergySystem");
            timeGameObject.AddComponent<TimeEnergySystem>();

            economyGameObject = new GameObject("EconomySystem");
            economyGameObject.AddComponent<EconomySystem>();

            activityGameObject = new GameObject("ActivitySystem");
            activityGameObject.AddComponent<ActivitySystem>();

            phoneGameObject = new GameObject("PhoneUI");
            phoneGameObject.AddComponent<PhoneUI>();

            detectionGameObject = new GameObject("DetectionSystem");
            detectionGameObject.AddComponent<DetectionSystem>();

            hudGameObject = new GameObject("HUDController");
            hud = hudGameObject.AddComponent<HUDController>();
        }

        [TearDown]
        public void TearDown()
        {
            if (hudGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(hudGameObject);
            }

            if (timeGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(timeGameObject);
            }

            if (economyGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(economyGameObject);
            }

            if (activityGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(activityGameObject);
            }

            if (phoneGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(phoneGameObject);
            }

            if (detectionGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(detectionGameObject);
            }
        }

        [Test]
        public void UpdateTimeDisplay_FormatsTime()
        {
            hud.UpdateTimeDisplay();
            Assert.AreEqual("08:00", hud.GetState().currentTime, "Time should format to 08:00 initially");
        }

        [Test]
        public void UpdateTimeDisplay_UpdatesAfterAdvance()
        {
            TimeEnergySystem.Instance.AdvanceTime(60f);
            hud.UpdateTimeDisplay();
            Assert.AreEqual("09:00", hud.GetState().currentTime, "Time should update after advancing");
        }

        [Test]
        public void UpdateEnergyDisplay_SetsEnergy()
        {
            hud.UpdateEnergyDisplay();
            Assert.AreEqual(100f, hud.GetState().currentEnergy, 0.01f, "Energy should default to 100");
        }

        [Test]
        public void UpdateMoneyDisplay_ReflectsBalance()
        {
            EconomySystem.Instance.AddIncome("player", 50f, EconomySystem.IncomeSource.Salary, "test");
            hud.UpdateMoneyDisplay();
            Assert.AreEqual(50f, hud.GetState().currentMoney, 0.01f, "Money should update");
        }

        [Test]
        public void UpdateActivityDisplay_ShowsActive()
        {
            ActivitySystem.Instance.CreateActivity(ActivitySystem.ActivityType.Physical, "work_mop", 1f);
            hud.UpdateActivityDisplay();
            Assert.IsFalse(string.IsNullOrEmpty(hud.GetState().currentActivity), "Activity should be shown");
        }

        [Test]
        public void UpdateActivityDisplay_NoActivityClears()
        {
            hud.UpdateActivityDisplay();
            Assert.AreEqual(string.Empty, hud.GetState().currentActivity, "No activity should clear display");
        }

        [Test]
        public void UpdateMessageCount_ReflectsPhoneUnread()
        {
            PhoneUI.Instance.ReceiveMessage("npc_1", "hi");
            hud.UpdateMessageCount();
            Assert.AreEqual(1, hud.GetState().unreadMessages, "Unread count should update");
        }

        [Test]
        public void ShowNotification_AddsNotification()
        {
            string id = hud.ShowNotification("Title", "Msg", HUDController.NotificationType.Info, 0f);
            Assert.IsNotNull(id, "Notification id should be returned");
            Assert.AreEqual(1, hud.GetState().activeNotifications.Count, "Notification should be added");
        }

        [Test]
        public void ShowNotification_FiresEvent()
        {
            bool fired = false;
            hud.OnNotificationShown += _ => fired = true;
            hud.ShowNotification("Title", "Msg", HUDController.NotificationType.Info, 0f);
            Assert.IsTrue(fired, "Notification shown event should fire");
        }

        [Test]
        public void DismissNotification_Removes()
        {
            string id = hud.ShowNotification("Title", "Msg", HUDController.NotificationType.Info, 0f);
            hud.DismissNotification(id);
            Assert.AreEqual(0, hud.GetState().activeNotifications.Count, "Notification should be removed");
        }

        [Test]
        public void DismissNotification_FiresEvent()
        {
            string id = hud.ShowNotification("Title", "Msg", HUDController.NotificationType.Info, 0f);
            bool fired = false;
            hud.OnNotificationDismissed += _ => fired = true;
            hud.DismissNotification(id);
            Assert.IsTrue(fired, "Dismiss event should fire");
        }

        [Test]
        public void DismissAllNotifications_Clears()
        {
            hud.ShowNotification("Title1", "Msg1", HUDController.NotificationType.Info, 0f);
            hud.ShowNotification("Title2", "Msg2", HUDController.NotificationType.Info, 0f);
            hud.DismissAllNotifications();
            Assert.AreEqual(0, hud.GetState().activeNotifications.Count, "All notifications should clear");
        }

        [Test]
        public void SetVisibility_ChangesStateAndFires()
        {
            bool fired = false;
            hud.OnVisibilityChanged += _ => fired = true;
            hud.SetVisibility(HUDController.HUDVisibility.Hidden);
            Assert.AreEqual(HUDController.HUDVisibility.Hidden, hud.GetVisibility(), "Visibility should change");
            Assert.IsTrue(fired, "Visibility event should fire");
        }

        [Test]
        public void Show_SetsVisible()
        {
            hud.Hide();
            hud.Show();
            Assert.AreEqual(HUDController.HUDVisibility.Visible, hud.GetVisibility(), "Show should set visible");
        }

        [Test]
        public void Hide_SetsHidden()
        {
            hud.Hide();
            Assert.AreEqual(HUDController.HUDVisibility.Hidden, hud.GetVisibility(), "Hide should set hidden");
        }

        [Test]
        public void ForceUpdateForTesting_RefreshesState()
        {
            EconomySystem.Instance.AddIncome("player", 25f, EconomySystem.IncomeSource.Salary, "seed");
            PhoneUI.Instance.ReceiveMessage("npc_1", "hi");
            hud.ForceUpdateForTesting();
            Assert.AreEqual(25f, hud.GetState().currentMoney, 0.01f, "Force update should refresh money");
            Assert.AreEqual(1, hud.GetState().unreadMessages, "Force update should refresh unread");
        }

        private static void ResetSingleton(Type type)
        {
            FieldInfo field = type.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(null, null);
            }
        }
    }
}
