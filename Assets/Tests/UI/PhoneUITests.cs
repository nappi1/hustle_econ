using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Core;
using UI;

namespace Tests.UI
{
    public class PhoneUITests
    {
        private GameObject phoneGameObject;
        private GameObject inputGameObject;
        private GameObject cameraGameObject;
        private GameObject activityGameObject;
        private GameObject economyGameObject;
        private GameObject timeGameObject;
        private PhoneUI phone;

        [SetUp]
        public void SetUp()
        {
            ResetSingleton(typeof(PhoneUI));
            ResetSingleton(typeof(InputManager));
            ResetSingleton(typeof(CameraController));
            ResetSingleton(typeof(ActivitySystem));
            ResetSingleton(typeof(EconomySystem));
            ResetSingleton(typeof(TimeEnergySystem));

            inputGameObject = new GameObject("InputManager");
            inputGameObject.AddComponent<InputManager>();

            cameraGameObject = new GameObject("CameraController");
            cameraGameObject.AddComponent<CameraController>();

            activityGameObject = new GameObject("ActivitySystem");
            activityGameObject.AddComponent<ActivitySystem>();

            economyGameObject = new GameObject("EconomySystem");
            economyGameObject.AddComponent<EconomySystem>();

            timeGameObject = new GameObject("TimeEnergySystem");
            timeGameObject.AddComponent<TimeEnergySystem>();

            phoneGameObject = new GameObject("PhoneUI");
            phone = phoneGameObject.AddComponent<PhoneUI>();
        }

        [TearDown]
        public void TearDown()
        {
            if (phoneGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(phoneGameObject);
            }

            if (inputGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(inputGameObject);
            }

            if (cameraGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(cameraGameObject);
            }

            if (activityGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(activityGameObject);
            }

            if (economyGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(economyGameObject);
            }

            if (timeGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(timeGameObject);
            }
        }

        [Test]
        public void OpenPhone_FromClosed_Opens()
        {
            phone.OpenPhone();
            Assert.IsTrue(phone.IsPhoneOpen(), "Phone should be open");
            Assert.AreEqual(PhoneUI.PhoneState.Open, phone.GetState(), "State should be Open");
        }

        [Test]
        public void ClosePhone_FromOpen_Closes()
        {
            phone.OpenPhone();
            phone.ClosePhone();
            Assert.IsFalse(phone.IsPhoneOpen(), "Phone should be closed");
            Assert.AreEqual(PhoneUI.PhoneState.Closed, phone.GetState(), "State should be Closed");
        }

        [Test]
        public void TogglePhone_OpensAndCloses()
        {
            phone.TogglePhone();
            Assert.IsTrue(phone.IsPhoneOpen(), "Toggle should open");
            phone.TogglePhone();
            Assert.IsFalse(phone.IsPhoneOpen(), "Toggle should close");
        }

        [Test]
        public void OpenPhone_AlreadyOpen_NoChange()
        {
            phone.OpenPhone();
            phone.OpenPhone();
            Assert.AreEqual(PhoneUI.PhoneState.Open, phone.GetState(), "State should remain Open");
        }

        [Test]
        public void ClosePhone_AlreadyClosed_NoChange()
        {
            phone.ClosePhone();
            Assert.AreEqual(PhoneUI.PhoneState.Closed, phone.GetState(), "State should remain Closed");
        }

        [Test]
        public void OpenPhone_FiresEvent()
        {
            bool fired = false;
            phone.OnPhoneOpened += () => fired = true;
            phone.OpenPhone();
            Assert.IsTrue(fired, "OnPhoneOpened should fire");
        }

        [Test]
        public void ClosePhone_FiresEvent()
        {
            bool fired = false;
            phone.OpenPhone();
            phone.OnPhoneClosed += () => fired = true;
            phone.ClosePhone();
            Assert.IsTrue(fired, "OnPhoneClosed should fire");
        }

        [Test]
        public void OpenPhone_PushesInputContext()
        {
            phone.OpenPhone();
            Assert.AreEqual(InputManager.InputContext.Phone, InputManager.Instance.GetContext(), "Input context should be Phone");
        }

        [Test]
        public void ClosePhone_PopsInputContext()
        {
            phone.OpenPhone();
            phone.ClosePhone();
            Assert.AreEqual(InputManager.InputContext.Gameplay, InputManager.Instance.GetContext(), "Input context should return to Gameplay");
        }

        [Test]
        public void OpenApp_WhenClosed_WarnsAndDoesNotChange()
        {
            phone.OpenApp(PhoneUI.PhoneApp.Messages);
            Assert.AreEqual(PhoneUI.PhoneApp.Home, phone.GetCurrentApp(), "App should remain Home when phone closed");
        }

        [Test]
        public void OpenApp_SetsCurrentAndPrevious()
        {
            phone.OpenPhone();
            phone.OpenApp(PhoneUI.PhoneApp.Messages);
            Assert.AreEqual(PhoneUI.PhoneApp.Messages, phone.GetCurrentApp(), "Current app should update");

            phone.OpenApp(PhoneUI.PhoneApp.Banking);
            PhoneUI.PhoneUIState state = phone.GetStateForTesting();
            Assert.AreEqual(PhoneUI.PhoneApp.Messages, state.previousApp, "Previous app should be tracked");
        }

        [Test]
        public void GoBack_ReturnsToPreviousApp()
        {
            phone.OpenPhone();
            phone.OpenApp(PhoneUI.PhoneApp.Messages);
            phone.OpenApp(PhoneUI.PhoneApp.Banking);
            phone.GoBack();
            Assert.AreEqual(PhoneUI.PhoneApp.Messages, phone.GetCurrentApp(), "GoBack should return to previous app");
        }

        [Test]
        public void GoHome_ReturnsHome()
        {
            phone.OpenPhone();
            phone.OpenApp(PhoneUI.PhoneApp.Messages);
            phone.GoHome();
            Assert.AreEqual(PhoneUI.PhoneApp.Home, phone.GetCurrentApp(), "GoHome should set Home app");
        }

        [Test]
        public void OpenApp_FiresEvent()
        {
            bool fired = false;
            phone.OpenPhone();
            phone.OnAppOpened += app =>
            {
                if (app == PhoneUI.PhoneApp.Messages)
                {
                    fired = true;
                }
            };
            phone.OpenApp(PhoneUI.PhoneApp.Messages);
            Assert.IsTrue(fired, "OnAppOpened should fire");
        }

        [Test]
        public void SendMessage_AddsToListAndFires()
        {
            bool fired = false;
            phone.OnMessageSent += _ => fired = true;
            phone.SendMessage("npc_1", "hello");
            Assert.IsTrue(fired, "OnMessageSent should fire");
            Assert.AreEqual(1, phone.GetStateForTesting().messages.Count, "Message should be added");
        }

        [Test]
        public void ReceiveMessage_AddsToListAndFires()
        {
            bool fired = false;
            phone.OnMessageReceived += _ => fired = true;
            phone.ReceiveMessage("npc_1", "hi");
            Assert.IsTrue(fired, "OnMessageReceived should fire");
            Assert.AreEqual(1, phone.GetStateForTesting().messages.Count, "Message should be added");
        }

        [Test]
        public void GetMessagesWithNPC_Filters()
        {
            phone.SendMessage("npc_1", "a");
            phone.SendMessage("npc_2", "b");
            List<PhoneUI.PhoneMessage> list = phone.GetMessagesWithNPC("npc_1");
            Assert.AreEqual(1, list.Count, "Should return only messages for npc_1");
        }

        [Test]
        public void MarkMessagesRead_MarksUnread()
        {
            phone.ReceiveMessage("npc_1", "a");
            Assert.AreEqual(1, phone.GetUnreadCount(), "Unread should be 1");
            phone.MarkMessagesRead("npc_1");
            Assert.AreEqual(0, phone.GetUnreadCount(), "Unread should be 0 after mark read");
        }

        [Test]
        public void GetUnreadCount_ReturnsCorrectCount()
        {
            phone.ReceiveMessage("npc_1", "a");
            phone.ReceiveMessage("npc_2", "b");
            phone.MarkMessagesRead("npc_2");
            Assert.AreEqual(1, phone.GetUnreadCount(), "Unread count should be 1");
        }

        [Test]
        public void ReceiveMessage_WhenClosed_FiresNotification()
        {
            bool fired = false;
            phone.OnNotificationShown += app =>
            {
                if (app == PhoneUI.PhoneApp.Messages)
                {
                    fired = true;
                }
            };
            phone.ReceiveMessage("npc_1", "hi");
            Assert.IsTrue(fired, "Notification should fire when phone closed");
        }

        [Test]
        public void OpenApp_DrugDealing_StartsActivity()
        {
            phone.OpenPhone();
            int before = ActivitySystem.Instance.GetActiveActivities("player").Count;
            phone.OpenApp(PhoneUI.PhoneApp.DrugDealing);
            int after = ActivitySystem.Instance.GetActiveActivities("player").Count;
            Assert.Greater(after, before, "DrugDealing should start an activity");
            Assert.IsTrue(phone.IsActivityRunning(), "Phone should track activity running");
        }

        [Test]
        public void ClosePhone_EndsPhoneActivity()
        {
            phone.OpenPhone();
            phone.OpenApp(PhoneUI.PhoneApp.DrugDealing);
            int before = ActivitySystem.Instance.GetActiveActivities("player").Count;
            phone.ClosePhone();
            int after = ActivitySystem.Instance.GetActiveActivities("player").Count;
            Assert.Less(after, before, "Closing phone should end phone activity");
            Assert.IsFalse(phone.IsActivityRunning(), "Phone should clear activity flag");
        }

        [Test]
        public void BankingApp_DoesNotStartActivity()
        {
            phone.OpenPhone();
            int before = ActivitySystem.Instance.GetActiveActivities("player").Count;
            phone.OpenApp(PhoneUI.PhoneApp.Banking);
            int after = ActivitySystem.Instance.GetActiveActivities("player").Count;
            Assert.AreEqual(before, after, "Banking should not start an activity");
        }

        [Test]
        public void SetStateForTesting_OverridesState()
        {
            phone.SetStateForTesting(PhoneUI.PhoneState.Open);
            Assert.AreEqual(PhoneUI.PhoneState.Open, phone.GetState(), "State should be overridden");
        }

        [Test]
        public void AddMessageForTesting_AddsMessage()
        {
            var message = new PhoneUI.PhoneMessage
            {
                npcId = "npc_1",
                npcName = "NPC",
                messageText = "test",
                timestamp = 1f,
                isRead = true,
                isPlayerMessage = true
            };
            phone.AddMessageForTesting(message);
            Assert.AreEqual(1, phone.GetStateForTesting().messages.Count, "Message should be added");
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
