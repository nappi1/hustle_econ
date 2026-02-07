using System;
using System.Collections;
using UnityEngine;

namespace Core
{
    public class CameraController : MonoBehaviour
    {
        public enum CameraMode
        {
            ThirdPerson,
            FirstPerson
        }

        public enum CameraTransitionSpeed
        {
            Instant,
            Fast,
            Smooth
        }

        [System.Serializable]
        public class CameraSettings
        {
            public Vector3 thirdPersonOffset = new Vector3(0.5f, 1.5f, -3f);
            public float thirdPersonFOV = 60f;
            public float thirdPersonRotationSpeed = 720f;
            public Vector3 firstPersonOffset = new Vector3(0f, 1.6f, 0f);
            public float firstPersonFOV = 90f;
            public float smoothTransitionTime = 0.5f;
            public float fastTransitionTime = 0.2f;
        }

        [System.Serializable]
        public class CameraState
        {
            public CameraMode currentMode;
            public CameraMode targetMode;
            public bool isLocked;
            public float lockDuration;
            public bool isTransitioning;
            public float transitionProgress;
        }

        private static CameraController instance;
        public static CameraController Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<CameraController>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("CameraController");
                        instance = go.AddComponent<CameraController>();
                    }
                }
                return instance;
            }
        }

        public event Action<CameraMode> OnModeChanged;
        public event Action<CameraMode> OnModeChangeStarted;
        public event Action<CameraMode> OnModeChangeCompleted;
        public event Action OnCameraLocked;
        public event Action OnCameraUnlocked;
        public event Action OnTransitionStarted;
        public event Action OnTransitionCompleted;

        [SerializeField] private CameraSettings settings = new CameraSettings();

        private Camera mainCamera;
        private Transform playerTransform;
        private CameraState state = new CameraState();
        private Vector3 currentOffset;
        private float currentFOV;
        private float lockEndTime;

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
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                mainCamera = gameObject.GetComponent<Camera>();
                if (mainCamera == null)
                {
                    mainCamera = gameObject.AddComponent<Camera>();
                }
            }

            state.currentMode = CameraMode.ThirdPerson;
            state.targetMode = CameraMode.ThirdPerson;
            state.isLocked = false;
            state.lockDuration = 0f;
            state.isTransitioning = false;
            state.transitionProgress = 1f;

            currentOffset = settings.thirdPersonOffset;
            currentFOV = settings.thirdPersonFOV;

            ApplyCameraSettings(CameraMode.ThirdPerson, true);
        }

        public void SetTarget(Transform target)
        {
            playerTransform = target;
        }

        public void SetMode(CameraMode mode, bool locked = false, float lockDuration = 0f)
        {
            if (state.isLocked && !locked)
            {
                Debug.LogWarning($"Cannot switch camera mode - locked for {lockEndTime - Time.time:F1}s");
                return;
            }

            if (locked)
            {
                state.isLocked = true;
                state.lockDuration = lockDuration;
                lockEndTime = Time.time + lockDuration;
                OnCameraLocked?.Invoke();
            }

            state.targetMode = mode;

            if (!state.isTransitioning && state.currentMode != state.targetMode)
            {
                StartTransition(CameraTransitionSpeed.Smooth);
            }

            OnModeChangeStarted?.Invoke(mode);
        }

        public void ToggleMode()
        {
            if (state.isLocked)
            {
                Debug.LogWarning("Cannot toggle camera - locked");
                return;
            }

            CameraMode newMode = state.currentMode == CameraMode.ThirdPerson
                ? CameraMode.FirstPerson
                : CameraMode.ThirdPerson;

            state.targetMode = newMode;
            StartTransition(CameraTransitionSpeed.Fast);
        }

        public void ForceMode(CameraMode mode)
        {
            state.targetMode = mode;
            state.currentMode = mode;
            state.isTransitioning = false;
            state.transitionProgress = 1f;
            ApplyCameraSettings(mode, true);
            OnModeChanged?.Invoke(mode);
        }

        public void OnJobStarted()
        {
            SetMode(CameraMode.FirstPerson, false);
        }

        public void OnJobEnded()
        {
            SetMode(CameraMode.ThirdPerson, false);
        }

        public void OnPhoneOpened()
        {
            SetMode(CameraMode.FirstPerson, false);
        }

        public void OnPhoneClosed()
        {
            if (!IsPlayerBusy() && !IsPlayerAtDesk())
            {
                SetMode(CameraMode.ThirdPerson, false);
            }
        }

        public void OnDeskSatDown()
        {
            SetMode(CameraMode.FirstPerson, false);
        }

        public void OnDeskStoodUp()
        {
            if (!IsPlayerBusy())
            {
                SetMode(CameraMode.ThirdPerson, false);
            }
        }

        public void OnIntimacyStarted()
        {
            SetMode(CameraMode.FirstPerson, true, 2f);
        }

        public void OnIntimacyEnded()
        {
            SetMode(CameraMode.ThirdPerson, false);
        }

        public CameraMode GetCurrentMode()
        {
            return state.currentMode;
        }

        public bool IsLocked()
        {
            return state.isLocked;
        }

        public bool IsTransitioning()
        {
            return state.isTransitioning;
        }

        public float GetCurrentFOV()
        {
            return currentFOV;
        }

        public void SetFOV(float fov)
        {
            currentFOV = Mathf.Max(1f, fov);
            ApplyCurrentSettings();
        }

        public void SetPosition(Vector3 offset)
        {
            currentOffset = offset;
        }

        public void SetModeForTesting(CameraMode mode)
        {
            state.currentMode = mode;
            state.targetMode = mode;
            state.isTransitioning = false;
            state.transitionProgress = 1f;
            ApplyCameraSettings(mode, true);
        }

        public void SetLockedForTesting(bool locked)
        {
            state.isLocked = locked;
        }

        public CameraState GetStateForTesting()
        {
            return state;
        }

        private void LateUpdate()
        {
            if (playerTransform == null)
            {
                UpdateLockTimer();
                return;
            }

            UpdateLockTimer();
            UpdateCameraPosition();
        }

        private void UpdateCameraPosition()
        {
            if (mainCamera == null)
            {
                return;
            }

            Vector3 targetPosition = playerTransform.position + playerTransform.TransformDirection(currentOffset);
            mainCamera.transform.position = targetPosition;

            if (state.currentMode == CameraMode.ThirdPerson)
            {
                Vector3 lookTarget = playerTransform.position + Vector3.up * 1.6f;
                mainCamera.transform.LookAt(lookTarget);
            }
            else
            {
                mainCamera.transform.rotation = playerTransform.rotation;
            }

            mainCamera.fieldOfView = currentFOV;
        }

        private void StartTransition(CameraTransitionSpeed speed)
        {
            state.isTransitioning = true;
            state.transitionProgress = 0f;
            OnTransitionStarted?.Invoke();

            float transitionTime = speed == CameraTransitionSpeed.Fast
                ? settings.fastTransitionTime
                : settings.smoothTransitionTime;

            if (speed == CameraTransitionSpeed.Instant)
            {
                ApplyCameraSettings(state.targetMode, true);
                CompleteTransition();
            }
            else
            {
                StartCoroutine(TransitionCoroutine(transitionTime));
            }
        }

        private IEnumerator TransitionCoroutine(float duration)
        {
            Vector3 startOffset = currentOffset;
            float startFOV = currentFOV;

            Vector3 targetOffset = state.targetMode == CameraMode.ThirdPerson
                ? settings.thirdPersonOffset
                : settings.firstPersonOffset;

            float targetFOV = state.targetMode == CameraMode.ThirdPerson
                ? settings.thirdPersonFOV
                : settings.firstPersonFOV;

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                state.transitionProgress = Mathf.Clamp01(elapsed / duration);

                currentOffset = Vector3.Lerp(startOffset, targetOffset, state.transitionProgress);
                currentFOV = Mathf.Lerp(startFOV, targetFOV, state.transitionProgress);

                ApplyCurrentSettings();

                yield return null;
            }

            currentOffset = targetOffset;
            currentFOV = targetFOV;
            ApplyCurrentSettings();

            CompleteTransition();
        }

        private void CompleteTransition()
        {
            state.currentMode = state.targetMode;
            state.isTransitioning = false;
            state.transitionProgress = 1f;

            OnModeChangeCompleted?.Invoke(state.currentMode);
            OnTransitionCompleted?.Invoke();
            OnModeChanged?.Invoke(state.currentMode);
        }

        private void ApplyCameraSettings(CameraMode mode, bool instant)
        {
            if (instant)
            {
                currentOffset = mode == CameraMode.ThirdPerson
                    ? settings.thirdPersonOffset
                    : settings.firstPersonOffset;

                currentFOV = mode == CameraMode.ThirdPerson
                    ? settings.thirdPersonFOV
                    : settings.firstPersonFOV;

                ApplyCurrentSettings();
            }
        }

        private void ApplyCurrentSettings()
        {
            if (mainCamera != null)
            {
                mainCamera.fieldOfView = currentFOV;
            }
        }

        private void UpdateLockTimer()
        {
            if (state.isLocked && Time.time >= lockEndTime)
            {
                state.isLocked = false;
                OnCameraUnlocked?.Invoke();
            }
        }

        private bool IsPlayerBusy()
        {
            if (ActivitySystem.Instance == null)
            {
                return false;
            }

            var activities = ActivitySystem.Instance.GetActiveActivities("player");
            return activities != null && activities.Count > 0;
        }

        private bool IsPlayerAtDesk()
        {
            if (PlayerController.Instance == null)
            {
                return false;
            }

            return PlayerController.Instance.GetPosture() == PlayerController.PlayerPosture.Sitting;
        }
    }
}
