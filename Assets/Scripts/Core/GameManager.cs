using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UI;

namespace Core
{
    public class GameManager : MonoBehaviour
    {
        public enum GameState
        {
            Initializing,
            MainMenu,
            Playing,
            Paused,
            Loading,
            Saving,
            GameOver
        }

        public enum SceneType
        {
            Menu,
            Apartment,
            Office,
            Street,
            Store,
            Restaurant,
            Other
        }

        [System.Serializable]
        public class GameSaveData
        {
            public string version;
            public DateTime saveDate;
            public float playTime;
            public string currentScene;
        }

        [System.Serializable]
        public class GameManagerState
        {
            public GameState currentState;
            public string currentScene;
            public string previousScene;
            public float gameStartTime;
            public float totalPlayTime;
            public bool isInitialized;
            public bool isPaused;
            public List<string> loadedScenes;
        }

        private static GameManager instance;
        public static GameManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<GameManager>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("GameManager");
                        instance = go.AddComponent<GameManager>();
                    }
                }
                return instance;
            }
        }

        public event Action<GameState> OnGameStateChanged;
        public event Action OnGamePaused;
        public event Action OnGameResumed;
        public event Action<string> OnSceneLoadStarted;
        public event Action<string> OnSceneLoadCompleted;
        public event Action<string> OnSceneUnloaded;
        public event Action<string> OnSaveStarted;
        public event Action<string> OnSaveCompleted;
        public event Action<string> OnLoadStarted;
        public event Action<string> OnLoadCompleted;
        public event Action OnAllSystemsInitialized;

        private GameManagerState state;

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

        public void Initialize()
        {
            if (state != null && state.isInitialized)
            {
                Debug.LogWarning("GameManager already initialized");
                return;
            }

            state = new GameManagerState
            {
                currentState = GameState.Initializing,
                gameStartTime = Time.time,
                totalPlayTime = 0f,
                isInitialized = false,
                isPaused = false,
                loadedScenes = new List<string>()
            };

            InitializeAllSystems();
            InitializeUI();

            state.isInitialized = true;
            SetGameState(GameState.MainMenu);
            OnAllSystemsInitialized?.Invoke();
        }

        private void Update()
        {
            if (state == null)
            {
                return;
            }

            if (state.currentState == GameState.Playing && !state.isPaused)
            {
                state.totalPlayTime += Time.deltaTime;
            }
        }

        private void InitializeAllSystems()
        {
            EntitySystem.Instance.ToString();
            TimeEnergySystem.Instance.ToString();
            EconomySystem.Instance.ToString();

            ReputationSystem.Instance.ToString();
            RelationshipSystem.Instance.ToString();
            DetectionSystem.Instance.ToString();

            SkillSystem.Instance.ToString();
            JobSystem.Instance.ToString();
            ActivitySystem.Instance.ToString();
            MinigameSystem.Instance.ToString();

            LocationSystem.Instance.ToString();
            InventorySystem.Instance.ToString();
            EventSystem.Instance.ToString();
            HeatSystem.Instance.ToString();
            IntoxicationSystem.Instance.ToString();
            BodySystem.Instance.ToString();
            AdultContentSystem.Instance.ToString();

            InputManager.Instance.ToString();

            var player = FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                player.Initialize("player");
            }

            var camera = FindAnyObjectByType<CameraController>();
            if (camera != null)
            {
                camera.Initialize();
            }
        }

        private void InitializeUI()
        {
            var hud = FindAnyObjectByType<HUDController>();
            if (hud != null)
            {
                hud.Initialize();
            }

            var phone = FindAnyObjectByType<PhoneUI>();
            if (phone != null)
            {
                phone.Initialize();
            }

            var minigameUi = FindAnyObjectByType<MinigameUI>();
            if (minigameUi != null)
            {
                minigameUi.Initialize();
            }
        }

        public void SetGameState(GameState newState)
        {
            if (state == null)
            {
                return;
            }

            if (state.currentState == newState)
            {
                return;
            }

            state.currentState = newState;
            OnGameStateChanged?.Invoke(newState);
        }

        public GameState GetGameState()
        {
            return state != null ? state.currentState : GameState.Initializing;
        }

        public void PauseGame()
        {
            if (state == null || state.isPaused)
            {
                return;
            }

            state.isPaused = true;
            Time.timeScale = 0f;

            if (InputManager.Instance != null)
            {
                InputManager.Instance.PushContext(InputManager.InputContext.UI);
            }

            OnGamePaused?.Invoke();
            SetGameState(GameState.Paused);
        }

        public void ResumeGame()
        {
            if (state == null || !state.isPaused)
            {
                return;
            }

            state.isPaused = false;
            Time.timeScale = 1f;

            if (InputManager.Instance != null)
            {
                InputManager.Instance.PopContext();
            }

            OnGameResumed?.Invoke();
            SetGameState(GameState.Playing);
        }

        public void QuitGame()
        {
            Application.Quit();
        }

        public void LoadScene(string sceneName, bool unloadCurrent = true)
        {
            if (state == null || state.currentState == GameState.Loading)
            {
                return;
            }

            if (!Application.isPlaying)
            {
                state.previousScene = state.currentScene;
                state.currentScene = sceneName;
                if (!state.loadedScenes.Contains(sceneName))
                {
                    state.loadedScenes.Add(sceneName);
                }
                OnSceneLoadStarted?.Invoke(sceneName);
                OnSceneLoadCompleted?.Invoke(sceneName);
                SetGameState(GameState.Playing);
                return;
            }

            if (!IsSceneInBuild(sceneName))
            {
                Debug.LogWarning($"LoadScene: scene '{sceneName}' not in build settings");
                return;
            }

            StartCoroutine(LoadSceneCoroutine(sceneName, unloadCurrent));
        }

        public void UnloadScene(string sceneName)
        {
            if (state == null)
            {
                return;
            }

            if (!Application.isPlaying)
            {
                state.loadedScenes.Remove(sceneName);
                OnSceneUnloaded?.Invoke(sceneName);
                return;
            }

            StartCoroutine(UnloadSceneCoroutine(sceneName));
        }

        public string GetCurrentScene()
        {
            return state != null ? state.currentScene : string.Empty;
        }

        public bool IsSceneLoaded(string sceneName)
        {
            return state != null && state.loadedScenes.Contains(sceneName);
        }

        public void SaveGame(string saveName)
        {
            if (state == null)
            {
                return;
            }

            SetGameState(GameState.Saving);
            OnSaveStarted?.Invoke(saveName);

            GameSaveData saveData = new GameSaveData
            {
                version = "0.1",
                saveDate = DateTime.Now,
                playTime = GetPlayTime(),
                currentScene = state.currentScene
            };

            string json = JsonUtility.ToJson(saveData, true);
            string savePath = GetSavePath(saveName);
            File.WriteAllText(savePath, json);
            OnSaveCompleted?.Invoke(saveName);
            SetGameState(GameState.Playing);
        }

        public void LoadGame(string saveName)
        {
            if (state == null)
            {
                return;
            }

            SetGameState(GameState.Loading);
            OnLoadStarted?.Invoke(saveName);

            string savePath = GetSavePath(saveName);
            if (!File.Exists(savePath))
            {
                Debug.LogWarning($"LoadGame: save not found {savePath}");
                SetGameState(GameState.MainMenu);
                return;
            }

            string json = File.ReadAllText(savePath);
            GameSaveData saveData = JsonUtility.FromJson<GameSaveData>(json);

            state.totalPlayTime = saveData.playTime;
            state.currentScene = saveData.currentScene;
            if (!string.IsNullOrEmpty(saveData.currentScene))
            {
                LoadScene(saveData.currentScene, true);
            }

            OnLoadCompleted?.Invoke(saveName);
            SetGameState(GameState.Playing);
        }

        public bool SaveExists(string saveName)
        {
            return File.Exists(GetSavePath(saveName));
        }

        public List<string> GetSaveFiles()
        {
            string saveDir = GetSaveDirectory();
            if (!Directory.Exists(saveDir))
            {
                return new List<string>();
            }

            List<string> saves = new List<string>();
            string[] files = Directory.GetFiles(saveDir, "*.json");
            foreach (string file in files)
            {
                saves.Add(Path.GetFileNameWithoutExtension(file));
            }
            return saves;
        }

        public void DeleteSave(string saveName)
        {
            string path = GetSavePath(saveName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        public float GetPlayTime()
        {
            return state != null ? state.totalPlayTime : 0f;
        }

        public void ResetPlayTime()
        {
            if (state != null)
            {
                state.totalPlayTime = 0f;
            }
        }

        public bool IsInitialized()
        {
            return state != null && state.isInitialized;
        }

        public bool IsPaused()
        {
            return state != null && state.isPaused;
        }

        public void SetStateForTesting(GameManagerState newState)
        {
            state = newState;
        }

        public GameManagerState GetStateForTesting()
        {
            return state;
        }

        private IEnumerator LoadSceneCoroutine(string sceneName, bool unloadCurrent)
        {
            SetGameState(GameState.Loading);
            OnSceneLoadStarted?.Invoke(sceneName);

            if (unloadCurrent && !string.IsNullOrEmpty(state.currentScene))
            {
                yield return UnloadSceneCoroutine(state.currentScene);
            }

            AsyncOperation loadOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            while (!loadOp.isDone)
            {
                yield return null;
            }

            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (scene.isLoaded)
            {
                SceneManager.SetActiveScene(scene);
                state.previousScene = state.currentScene;
                state.currentScene = sceneName;
                if (!state.loadedScenes.Contains(sceneName))
                {
                    state.loadedScenes.Add(sceneName);
                }

                InitializeSceneComponents();
                OnSceneLoadCompleted?.Invoke(sceneName);
                SetGameState(GameState.Playing);
            }
        }

        private IEnumerator UnloadSceneCoroutine(string sceneName)
        {
            if (!state.loadedScenes.Contains(sceneName))
            {
                yield break;
            }

            AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(sceneName);
            while (!unloadOp.isDone)
            {
                yield return null;
            }

            state.loadedScenes.Remove(sceneName);
            OnSceneUnloaded?.Invoke(sceneName);
        }

        private void InitializeSceneComponents()
        {
            var playerController = FindAnyObjectByType<PlayerController>();
            if (playerController != null)
            {
                playerController.Initialize("player");
            }

            var cameraController = FindAnyObjectByType<CameraController>();
            if (cameraController != null && playerController != null)
            {
                cameraController.SetTarget(playerController.GetTransform());
            }
        }

        private string GetSaveDirectory()
        {
            return Path.Combine(Application.persistentDataPath, "Saves");
        }

        private string GetSavePath(string saveName)
        {
            string saveDir = GetSaveDirectory();
            if (!Directory.Exists(saveDir))
            {
                Directory.CreateDirectory(saveDir);
            }

            return Path.Combine(saveDir, $"{saveName}.json");
        }

        private static bool IsSceneInBuild(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                return false;
            }

            string directPath = sceneName.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)
                ? sceneName
                : $"Assets/Scenes/{sceneName}.unity";

            return SceneUtility.GetBuildIndexByScenePath(directPath) >= 0;
        }
    }
}
