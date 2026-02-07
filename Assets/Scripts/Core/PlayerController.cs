using System;
using UnityEngine;

namespace Core
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public enum PlayerMovementState
        {
            Idle,
            Walking,
            Running,
            Sitting,
            Driving,
            Locked
        }

        public enum PlayerPosture
        {
            Standing,
            Sitting,
            Driving
        }

        [System.Serializable]
        public class MovementSettings
        {
            public float walkSpeed = 3.5f;
            public float runSpeed = 6.0f;
            public float rotationSpeed = 720f;
            public float runEnergyDrainRate = 5f;
            public float gravity = -9.81f;
            public float groundCheckDistance = 0.2f;
        }

        [System.Serializable]
        public class PlayerState
        {
            public string playerId;
            public PlayerMovementState movementState;
            public PlayerPosture posture;
            public Vector3 position;
            public Quaternion rotation;
            public bool isGrounded;
            public float currentSpeed;
        }

        private static PlayerController instance;
        public static PlayerController Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<PlayerController>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("PlayerController");
                        instance = go.AddComponent<PlayerController>();
                    }
                }
                return instance;
            }
        }

        public event Action<PlayerMovementState> OnMovementStateChanged;
        public event Action<PlayerPosture> OnPostureChanged;
        public event Action<Vector3> OnPositionChanged;
        public event Action<GameObject> OnInteractionTargetChanged;
        public event Action<RaycastHit> OnLookTargetChanged;
        public event Action<float> OnRunEnergyDrained;

        [SerializeField] private MovementSettings settings = new MovementSettings();

        private CharacterController characterController;
        private Animator animator;
        private PlayerState state = new PlayerState();
        private bool movementEnabled = true;
        private Vector3 velocity;
        private Vector3 lastPosition;
        private GameObject lastInteractionTarget;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);

            characterController = GetComponent<CharacterController>();
            animator = GetComponent<Animator>();

            Initialize("player");
        }

        public void Initialize(string playerId)
        {
            state.playerId = playerId;
            state.movementState = PlayerMovementState.Idle;
            state.posture = PlayerPosture.Standing;
            state.position = transform.position;
            state.rotation = transform.rotation;
            state.isGrounded = characterController != null && characterController.isGrounded;
            state.currentSpeed = 0f;
            movementEnabled = true;
            lastPosition = transform.position;
        }

        private void Update()
        {
            if (!movementEnabled || !CanMove())
            {
                UpdatePositionChange();
                UpdateLookTarget();
                return;
            }

            ProcessInput();
            ApplyGravity();
            UpdateAnimator();
            CheckGroundedState();
            DrainRunEnergy(Time.deltaTime);
            UpdatePositionChange();
            UpdateLookTarget();
        }

        public void SetMovementEnabled(bool enabled)
        {
            movementEnabled = enabled;
        }

        public void SetMovementState(PlayerMovementState newState)
        {
            if (state.movementState == newState)
            {
                return;
            }

            state.movementState = newState;
            OnMovementStateChanged?.Invoke(newState);
        }

        public void SetPosture(PlayerPosture newPosture)
        {
            if (state.posture == newPosture)
            {
                return;
            }

            state.posture = newPosture;

            switch (newPosture)
            {
                case PlayerPosture.Sitting:
                    SetMovementState(PlayerMovementState.Sitting);
                    movementEnabled = false;
                    if (animator != null)
                    {
                        animator.SetBool("Sitting", true);
                        animator.SetBool("Driving", false);
                    }
                    break;

                case PlayerPosture.Driving:
                    SetMovementState(PlayerMovementState.Driving);
                    movementEnabled = false;
                    if (animator != null)
                    {
                        animator.SetBool("Driving", true);
                        animator.SetBool("Sitting", false);
                    }
                    break;

                case PlayerPosture.Standing:
                    movementEnabled = true;
                    if (animator != null)
                    {
                        animator.SetBool("Sitting", false);
                        animator.SetBool("Driving", false);
                    }
                    if (state.movementState == PlayerMovementState.Sitting ||
                        state.movementState == PlayerMovementState.Driving ||
                        state.movementState == PlayerMovementState.Locked)
                    {
                        SetMovementState(PlayerMovementState.Idle);
                    }
                    break;
            }

            OnPostureChanged?.Invoke(newPosture);
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            bool wasEnabled = characterController != null && characterController.enabled;
            if (characterController != null)
            {
                characterController.enabled = false;
            }

            transform.SetPositionAndRotation(position, rotation);

            if (characterController != null)
            {
                characterController.enabled = wasEnabled;
            }

            state.position = position;
            state.rotation = rotation;
            state.currentSpeed = 0f;
            lastPosition = position;
            OnPositionChanged?.Invoke(position);
        }

        public Vector3 GetPosition()
        {
            return transform.position;
        }

        public Quaternion GetRotation()
        {
            return transform.rotation;
        }

        public Transform GetTransform()
        {
            return transform;
        }

        public RaycastHit GetLookTarget(float maxDistance = 3f)
        {
            Ray ray = new Ray(transform.position + Vector3.up * 1.6f, transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
            {
                return hit;
            }

            return default(RaycastHit);
        }

        public bool IsLookingAt(GameObject target, float maxDistance = 3f)
        {
            RaycastHit hit = GetLookTarget(maxDistance);
            return hit.collider != null && hit.collider.gameObject == target;
        }

        public PlayerMovementState GetMovementState()
        {
            return state.movementState;
        }

        public PlayerPosture GetPosture()
        {
            return state.posture;
        }

        public bool IsGrounded()
        {
            return state.isGrounded;
        }

        public bool CanMove()
        {
            return movementEnabled && state.movementState != PlayerMovementState.Sitting &&
                   state.movementState != PlayerMovementState.Driving &&
                   state.movementState != PlayerMovementState.Locked;
        }

        public float GetCurrentSpeed()
        {
            return state.currentSpeed;
        }

        public void SetPositionForTesting(Vector3 position)
        {
            transform.position = position;
            state.position = position;
            lastPosition = position;
        }

        public void SetMovementStateForTesting(PlayerMovementState movementState)
        {
            SetMovementState(movementState);
        }

        public PlayerState GetStateForTesting()
        {
            return state;
        }

        private void ProcessInput()
        {
            if (characterController == null || InputManager.Instance == null)
            {
                return;
            }

            Vector3 inputDirection = InputManager.Instance.GetMovementInput();
            bool isRunning = InputManager.Instance.IsRunning();

            if (TimeEnergySystem.Instance != null && TimeEnergySystem.Instance.GetEnergyLevel() <= 10f)
            {
                isRunning = false;
            }

            float targetSpeed = isRunning ? settings.runSpeed : settings.walkSpeed;
            Vector3 moveDirection = transform.TransformDirection(inputDirection);
            Vector3 move = moveDirection * targetSpeed;

            characterController.Move(move * Time.deltaTime);

            PlayerMovementState newState = PlayerMovementState.Idle;
            if (inputDirection.magnitude > 0.1f)
            {
                newState = isRunning ? PlayerMovementState.Running : PlayerMovementState.Walking;
            }

            if (newState != state.movementState)
            {
                SetMovementState(newState);
            }

            if (inputDirection.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    settings.rotationSpeed * Time.deltaTime
                );
            }

            state.currentSpeed = characterController.velocity.magnitude;
            state.position = transform.position;
            state.rotation = transform.rotation;
        }

        private void ApplyGravity()
        {
            if (characterController == null)
            {
                return;
            }

            if (!characterController.isGrounded)
            {
                velocity.y += settings.gravity * Time.deltaTime;
            }
            else
            {
                velocity.y = -2f;
            }

            characterController.Move(velocity * Time.deltaTime);
        }

        private void UpdateAnimator()
        {
            if (animator == null)
            {
                return;
            }

            animator.SetFloat("Speed", state.currentSpeed);
            animator.SetBool("Grounded", state.isGrounded);
        }

        private void CheckGroundedState()
        {
            bool grounded = characterController != null && characterController.isGrounded;

            if (!grounded && settings.groundCheckDistance > 0f)
            {
                grounded = Physics.Raycast(transform.position, Vector3.down, settings.groundCheckDistance);
            }

            state.isGrounded = grounded;
        }

        private void DrainRunEnergy(float deltaTime)
        {
            if (state.movementState != PlayerMovementState.Running)
            {
                return;
            }

            float drainAmount = settings.runEnergyDrainRate * (deltaTime / 60f);
            if (drainAmount <= 0f)
            {
                return;
            }

            if (TimeEnergySystem.Instance != null)
            {
                TimeEnergySystem.Instance.ModifyEnergy(-drainAmount, "running");
            }

            OnRunEnergyDrained?.Invoke(drainAmount);
        }

        private void UpdatePositionChange()
        {
            state.position = transform.position;
            state.rotation = transform.rotation;

            if (Vector3.Distance(lastPosition, transform.position) > 0.1f)
            {
                lastPosition = transform.position;
                OnPositionChanged?.Invoke(lastPosition);
            }
        }

        private void UpdateLookTarget()
        {
            RaycastHit hit = GetLookTarget();
            GameObject target = hit.collider != null ? hit.collider.gameObject : null;

            if (target != lastInteractionTarget)
            {
                lastInteractionTarget = target;
                OnInteractionTargetChanged?.Invoke(target);
                OnLookTargetChanged?.Invoke(hit);
            }
        }
    }
}
