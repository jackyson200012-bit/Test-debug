using UnityEngine;
using UnityEngine.InputSystem;

namespace JayFos.Runtime
{
    /// <summary>
    /// Single source of input truth for the player. Reads the shared Input
    /// Actions asset in Update() and caches state for FixedUpdate consumers
    /// (PlayerMotor, FirstPersonCamera). Input is never polled from physics code.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInput : MonoBehaviour
    {
        [Header("Input")]
        [Tooltip("Shared Input Actions asset. Assign InputSystem_Actions.")]
        [SerializeField] private InputActionAsset actions;

        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction jumpAction;
        private InputAction sprintAction;
        private InputAction crouchAction;
        private InputAction walkAction;

        /// <summary>Movement input (WASD / left stick), camera-relative.</summary>
        public Vector2 Move { get; private set; }
        /// <summary>Look input (mouse delta / right stick).</summary>
        public Vector2 Look { get; private set; }
        /// <summary>True on the frame the Jump action was pressed.</summary>
        public bool JumpPressed { get; private set; }
        /// <summary>True while the Sprint action is held.</summary>
        public bool SprintHeld { get; private set; }
        /// <summary>True on the frame the Crouch action was pressed (edge for toggles).</summary>
        public bool CrouchPressed { get; private set; }
        /// <summary>True while the Crouch action is held (for hold-to-crouch).</summary>
        public bool CrouchHeld { get; private set; }
        /// <summary>True while the Walk action is held.</summary>
        public bool WalkHeld { get; private set; }
        /// <summary>False when the asset or the Player map could not be bound.</summary>
        public bool HasActions { get; private set; }

        private void Awake()
        {
            if (actions == null)
            {
                HasActions = false;
                Debug.LogWarning("[PlayerInput] No InputActionAsset assigned to 'actions'.", this);
                return;
            }

            InputActionMap playerMap = actions.FindActionMap("Player", false);
            if (playerMap == null)
            {
                HasActions = false;
                Debug.LogWarning("[PlayerInput] Input Actions asset has no 'Player' map.", this);
                return;
            }

            moveAction   = playerMap.FindAction("Move");
            lookAction   = playerMap.FindAction("Look");
            jumpAction   = playerMap.FindAction("Jump");
            sprintAction = playerMap.FindAction("Sprint");
            crouchAction = playerMap.FindAction("Crouch");
            walkAction   = playerMap.FindAction("Walk");
            HasActions   = moveAction != null && lookAction != null;
        }

        private void OnEnable()
        {
            if (actions != null) actions.Enable();
        }

        private void OnDisable()
        {
            if (actions != null) actions.Disable();
        }

        private void Update()
        {
            if (!HasActions) return;

            Move = moveAction.ReadValue<Vector2>();
            Look = lookAction.ReadValue<Vector2>();
            JumpPressed  = jumpAction   != null && jumpAction.WasPressedThisFrame();
            SprintHeld   = sprintAction != null && sprintAction.IsPressed();
            CrouchPressed = crouchAction != null && crouchAction.WasPressedThisFrame();
            CrouchHeld   = crouchAction != null && crouchAction.IsPressed();
            WalkHeld     = walkAction   != null && walkAction.IsPressed();
        }
    }
}
