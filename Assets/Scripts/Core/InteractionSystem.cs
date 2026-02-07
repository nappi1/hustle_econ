using System;
using UnityEngine;
using UI;

namespace Core
{
    public class InteractionSystem : MonoBehaviour
    {
        public enum InteractionType
        {
            PickupItem,
            DropItem,
            StartJob,
            EndJob,
            TalkToNpc,
            OpenDoor,
            SitDown,
            StandUp,
            UseComputer,
            UsePhone,
            Examine,
            Custom
        }

        public enum InteractionResult
        {
            Success,
            Failed,
            Cancelled,
            Unavailable
        }

        [System.Serializable]
        public class InteractionData
        {
            public GameObject interactableObject;
            public Interactable interactableComponent;
            public InteractionType type;
            public string targetId;
            public string promptText;
            public float distance;
            public bool isAvailable;
            public string unavailableReason;
        }

        [System.Serializable]
        public class InteractionSystemState
        {
            public InteractionData currentTarget;
            public InteractionData lastInteraction;
            public bool isInteracting;
            public float lastInteractionTime;
        }

        private static InteractionSystem _instance;
        public static InteractionSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<InteractionSystem>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("InteractionSystem");
                        _instance = go.AddComponent<InteractionSystem>();
                    }
                }
                return _instance;
            }
        }

        public event Action<InteractionData> OnInteractableDetected;
        public event Action OnInteractableLost;
        public event Action<InteractionData> OnInteractionStarted;
        public event Action<InteractionData, InteractionResult> OnInteractionCompleted;
        public event Action<InteractionData, string> OnInteractionFailed;
        public event Action<InteractionData> OnCurrentTargetChanged;

        [Header("Settings")]
        [SerializeField] private float maxInteractionDistance = 3f;
        [SerializeField] private float interactionCooldown = 0.5f;
        [SerializeField] private LayerMask interactableLayer;

        private InteractionSystemState _state;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }

        public void Initialize()
        {
            _state = new InteractionSystemState
            {
                currentTarget = null,
                lastInteraction = null,
                isInteracting = false,
                lastInteractionTime = -interactionCooldown
            };
        }

        private void Update()
        {
            UpdateCurrentTarget();

            if (InputManager.Instance != null &&
                InputManager.Instance.GetActionDown(InputManager.InputAction.Interact))
            {
                TryInteract();
            }
        }

        public InteractionData DetectInteractable()
        {
            if (PlayerController.Instance == null)
            {
                return null;
            }

            RaycastHit hit = PlayerController.Instance.GetLookTarget(maxInteractionDistance);
            if (hit.collider == null)
            {
                return null;
            }

            Interactable interactable = hit.collider.GetComponent<Interactable>();
            if (interactable == null)
            {
                return null;
            }

            InteractionData data = new InteractionData
            {
                interactableObject = hit.collider.gameObject,
                interactableComponent = interactable,
                type = interactable.interactionType,
                targetId = interactable.targetId,
                promptText = interactable.promptText,
                distance = hit.distance
            };

            data.isAvailable = CanInteract(data, out string reason);
            data.unavailableReason = reason;

            return data;
        }

        public void UpdateCurrentTarget()
        {
            InteractionData previousTarget = _state.currentTarget;
            InteractionData newTarget = DetectInteractable();

            if (newTarget != null)
            {
                _state.currentTarget = newTarget;
                if (previousTarget == null ||
                    previousTarget.interactableObject != newTarget.interactableObject)
                {
                    OnInteractableDetected?.Invoke(newTarget);
                    OnCurrentTargetChanged?.Invoke(newTarget);
                }
                return;
            }

            if (previousTarget != null)
            {
                OnInteractableLost?.Invoke();
                OnCurrentTargetChanged?.Invoke(null);
            }

            _state.currentTarget = null;
        }

        public InteractionResult TryInteract()
        {
            if (_state.currentTarget == null)
            {
                return InteractionResult.Unavailable;
            }

            if (Time.time - _state.lastInteractionTime < interactionCooldown)
            {
                return InteractionResult.Cancelled;
            }

            if (!_state.currentTarget.isAvailable)
            {
                OnInteractionFailed?.Invoke(_state.currentTarget, _state.currentTarget.unavailableReason);
                return InteractionResult.Failed;
            }

            InteractionResult result = Interact(_state.currentTarget);
            _state.lastInteractionTime = Time.time;
            _state.lastInteraction = _state.currentTarget;
            return result;
        }

        public InteractionResult Interact(InteractionData data)
        {
            _state.isInteracting = true;
            OnInteractionStarted?.Invoke(data);

            InteractionResult result = InteractionResult.Failed;

            try
            {
                switch (data.type)
                {
                    case InteractionType.PickupItem:
                        result = HandlePickupItem(data.targetId);
                        break;
                    case InteractionType.DropItem:
                        result = HandleDropItem(data.targetId);
                        break;
                    case InteractionType.StartJob:
                        result = HandleStartJob(data.targetId);
                        break;
                    case InteractionType.EndJob:
                        result = HandleEndJob(data.targetId);
                        break;
                    case InteractionType.TalkToNpc:
                        result = HandleTalkToNpc(data.targetId);
                        break;
                    case InteractionType.OpenDoor:
                        result = HandleOpenDoor(data.targetId);
                        break;
                    case InteractionType.SitDown:
                        result = HandleSitDown(data.interactableObject);
                        break;
                    case InteractionType.StandUp:
                        result = HandleStandUp();
                        break;
                    case InteractionType.UseComputer:
                        result = HandleUseComputer(data.targetId);
                        break;
                    case InteractionType.UsePhone:
                        result = HandleUsePhone();
                        break;
                    case InteractionType.Examine:
                        result = HandleExamine(data.targetId);
                        break;
                    default:
                        Debug.LogWarning($"Unhandled interaction type: {data.type}");
                        result = InteractionResult.Failed;
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Interaction failed: {e.Message}");
                result = InteractionResult.Failed;
            }

            _state.isInteracting = false;
            OnInteractionCompleted?.Invoke(data, result);
            return result;
        }

        public bool CanInteract(InteractionData data, out string reason)
        {
            if (data.distance > maxInteractionDistance)
            {
                reason = "Too far away";
                return false;
            }

            if (data.interactableComponent != null && data.interactableComponent.requiresLineOfSight)
            {
                if (PlayerController.Instance != null &&
                    !PlayerController.Instance.IsLookingAt(data.interactableObject, maxInteractionDistance))
                {
                    reason = "Cannot see target";
                    return false;
                }
            }

            if (data.interactableComponent != null && data.interactableComponent.requiredItems != null)
            {
                foreach (string item in data.interactableComponent.requiredItems)
                {
                    if (InventorySystem.Instance != null &&
                        !InventorySystem.Instance.HasItem("player", item))
                    {
                        reason = "Missing required item";
                        return false;
                    }
                }
            }

            switch (data.type)
            {
                case InteractionType.PickupItem:
                    return ValidatePickupItem(data.targetId, out reason);
                case InteractionType.StartJob:
                    return ValidateStartJob(data.targetId, out reason);
                case InteractionType.OpenDoor:
                    return ValidateTravelTo(data.targetId, out reason);
                default:
                    reason = string.Empty;
                    return true;
            }
        }

        private bool ValidatePickupItem(string itemId, out string reason)
        {
            if (EntitySystem.Instance == null)
            {
                reason = "Entity system unavailable";
                return false;
            }

            var entity = EntitySystem.Instance.GetEntity(itemId);
            if (entity == null)
            {
                reason = "Item not found";
                return false;
            }

            if (!string.IsNullOrEmpty(entity.owner) && entity.owner != "player")
            {
                reason = "Item belongs to someone else";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private bool ValidateStartJob(string jobId, out string reason)
        {
            if (JobSystem.Instance == null)
            {
                reason = "Job system unavailable";
                return false;
            }

            if (string.IsNullOrEmpty(jobId))
            {
                reason = "Job not found";
                return false;
            }

            JobSystem.Job job = JobSystem.Instance.GetJobById(jobId);
            if (job == null)
            {
                reason = "Job not found";
                return false;
            }

            if (!JobSystem.Instance.HasJob("player", jobId))
            {
                reason = "Not employed";
                return false;
            }

            if (!job.isActive)
            {
                reason = "Job not active";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private bool ValidateTravelTo(string locationId, out string reason)
        {
            if (LocationSystem.Instance == null)
            {
                reason = "Location system unavailable";
                return false;
            }

            if (!LocationSystem.Instance.CanTravelTo("player", locationId))
            {
                reason = "Cannot travel";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public InteractionData GetCurrentTarget()
        {
            return _state.currentTarget;
        }

        public bool IsLookingAtInteractable()
        {
            return _state.currentTarget != null;
        }

        public bool IsInteracting()
        {
            return _state.isInteracting;
        }

        public void SetCurrentTargetForTesting(InteractionData data)
        {
            _state.currentTarget = data;
        }

        public InteractionSystemState GetStateForTesting()
        {
            return _state;
        }

        private InteractionResult HandlePickupItem(string itemId)
        {
            if (InventorySystem.Instance == null)
            {
                return InteractionResult.Failed;
            }

            InventorySystem.Instance.AddItem("player", itemId);
            return InteractionResult.Success;
        }

        private InteractionResult HandleDropItem(string itemId)
        {
            if (InventorySystem.Instance == null)
            {
                return InteractionResult.Failed;
            }

            InventorySystem.Instance.RemoveItem("player", itemId);
            return InteractionResult.Success;
        }

        private InteractionResult HandleStartJob(string jobId)
        {
            if (JobSystem.Instance == null)
            {
                return InteractionResult.Failed;
            }

            JobSystem.Instance.StartShift("player", jobId);
            return InteractionResult.Success;
        }

        private InteractionResult HandleEndJob(string jobId)
        {
            if (JobSystem.Instance == null)
            {
                return InteractionResult.Failed;
            }

            JobSystem.Instance.EndShift("player", jobId);
            return InteractionResult.Success;
        }

        private InteractionResult HandleTalkToNpc(string npcId)
        {
            Debug.Log($"Talking to NPC: {npcId}");
            return InteractionResult.Success;
        }

        private InteractionResult HandleOpenDoor(string locationId)
        {
            if (LocationSystem.Instance == null)
            {
                return InteractionResult.Failed;
            }

            LocationSystem.Instance.TravelToLocation("player", locationId);
            return InteractionResult.Success;
        }

        private InteractionResult HandleSitDown(GameObject chair)
        {
            if (PlayerController.Instance == null)
            {
                return InteractionResult.Failed;
            }

            PlayerController.Instance.SetPosture(PlayerController.PlayerPosture.Sitting);
            return InteractionResult.Success;
        }

        private InteractionResult HandleStandUp()
        {
            if (PlayerController.Instance == null)
            {
                return InteractionResult.Failed;
            }

            PlayerController.Instance.SetPosture(PlayerController.PlayerPosture.Standing);
            return InteractionResult.Success;
        }

        private InteractionResult HandleUseComputer(string computerId)
        {
            Debug.Log($"Using computer: {computerId}");
            return InteractionResult.Success;
        }

        private InteractionResult HandleUsePhone()
        {
            if (PhoneUI.Instance != null)
            {
                PhoneUI.Instance.TogglePhone();
                return InteractionResult.Success;
            }

            return InteractionResult.Failed;
        }

        private InteractionResult HandleExamine(string entityId)
        {
            if (EntitySystem.Instance == null)
            {
                return InteractionResult.Failed;
            }

            var entity = EntitySystem.Instance.GetEntity(entityId);
            if (entity == null)
            {
                return InteractionResult.Failed;
            }

            if (HUDController.Instance != null)
            {
                HUDController.Instance.ShowNotification(entity.id, "Nothing special.", HUDController.NotificationType.Info, 3f);
            }

            return InteractionResult.Success;
        }
    }
}
