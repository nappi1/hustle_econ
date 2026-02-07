using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Core;
using UI;

namespace Tests.Core
{
    public class GameManagerTests
    {
        private GameObject gameManagerObject;
        private GameManager manager;

        [SetUp]
        public void SetUp()
        {
            ResetSingleton(typeof(GameManager));
            ResetSingleton(typeof(EntitySystem));
            ResetSingleton(typeof(TimeEnergySystem));
            ResetSingleton(typeof(EconomySystem));
            ResetSingleton(typeof(ReputationSystem));
            ResetSingleton(typeof(RelationshipSystem));
            ResetSingleton(typeof(DetectionSystem));
            ResetSingleton(typeof(SkillSystem));
            ResetSingleton(typeof(JobSystem));
            ResetSingleton(typeof(ActivitySystem));
            ResetSingleton(typeof(MinigameSystem));
            ResetSingleton(typeof(LocationSystem));
            ResetSingleton(typeof(InventorySystem));
            ResetSingleton(typeof(EventSystem));
            ResetSingleton(typeof(HeatSystem));
            ResetSingleton(typeof(IntoxicationSystem));
            ResetSingleton(typeof(BodySystem));
            ResetSingleton(typeof(AdultContentSystem));
            ResetSingleton(typeof(InputManager));
            ResetSingleton(typeof(HUDController));
            ResetSingleton(typeof(PhoneUI));
            ResetSingleton(typeof(MinigameUI));

            gameManagerObject = new GameObject("GameManager");
            manager = gameManagerObject.AddComponent<GameManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (gameManagerObject != null)
            {
                UnityEngine.Object.DestroyImmediate(gameManagerObject);
            }
        }

        [Test]
        public void Initialize_SetsInitializedAndMainMenu()
        {
            Assert.IsTrue(manager.IsInitialized(), "GameManager should initialize");
            Assert.AreEqual(GameManager.GameState.MainMenu, manager.GetGameState(), "Default state should be MainMenu");
        }

        [Test]
        public void SetGameState_FiresEvent()
        {
            bool fired = false;
            manager.OnGameStateChanged += _ => fired = true;
            manager.SetGameState(GameManager.GameState.Playing);
            Assert.IsTrue(fired, "State change event should fire");
        }

        [Test]
        public void PauseResume_UpdatesState()
        {
            manager.SetGameState(GameManager.GameState.Playing);
            manager.PauseGame();
            Assert.IsTrue(manager.IsPaused(), "Should be paused");
            Assert.AreEqual(GameManager.GameState.Paused, manager.GetGameState(), "State should be Paused");

            manager.ResumeGame();
            Assert.IsFalse(manager.IsPaused(), "Should resume");
            Assert.AreEqual(GameManager.GameState.Playing, manager.GetGameState(), "State should be Playing");
        }

        [Test]
        public void SaveGame_CreatesFile()
        {
            string saveName = "test_save";
            manager.SaveGame(saveName);
            Assert.IsTrue(manager.SaveExists(saveName), "Save should exist");
            manager.DeleteSave(saveName);
        }

        [Test]
        public void GetSaveFiles_ReturnsSave()
        {
            string saveName = "test_save_list";
            manager.SaveGame(saveName);
            var saves = manager.GetSaveFiles();
            Assert.Contains(saveName, saves, "Save list should include save");
            manager.DeleteSave(saveName);
        }

        [Test]
        public void DeleteSave_RemovesFile()
        {
            string saveName = "test_save_delete";
            manager.SaveGame(saveName);
            manager.DeleteSave(saveName);
            Assert.IsFalse(manager.SaveExists(saveName), "Save should be deleted");
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
