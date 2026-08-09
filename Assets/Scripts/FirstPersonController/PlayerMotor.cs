using System;
using UnityEngine;

namespace JayFos.Runtime
{
    /// <summary>
    /// Physics hub. FixedUpdate: reads PlayerInput, integrates horizontal velocity
    /// with acceleration curves, applies gravity + jump (coyote time & jump buffer),
    /// crouch resizing, slope projection, stair step-up and moving-platform
    /// velocity inheritance. Publishes state and events for camera/animator.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [Header("References (auto-resolved on this GameObject)")]
        [Tooltip("Yaw transform the camera rotates; movement is relative to it. Falls back to this transform.")]
        [SerializeField] private Transform body;
        [Tooltip("Player input. Auto-resolved on this GameObject.")]
        [SerializeField] private PlayerInput input;
        [Tooltip("Ground detector. Auto-resolved on this GameObject.")]
        [SerializeField] private GroundDetector ground;
        [Tooltip("Stair stepper. Auto-resolved on this GameObject.")]
        [SerializeField] private StairStepper stairs;

        [Header("Speed")]
        [SerializeField, Min(0f), Tooltip("Walk speed while holding the Walk action.")]
        private float walkSpeed = 4f;
        [SerializeField, Min(0f), Tooltip("Default run speed.")]
        private float runSpeed = 6f;
        [SerializeField, Min(0f), Tooltip("Sprint multiplier applied to runSpeed.")]
        private float sprintMultiplier = 1.6f;
        [SerializeField, Min(0f), Tooltip("Crouch multiplier applied to runSpeed.")]
        private float crouchMultiplier = 0.45f;
        [SerializeField, Tooltip("Require forward input to sprint.")]
        private bool sprintRequiresForward = true;

        [Header("Acceleration")]
        [SerializeField, Min(0f), Tooltip("Horizontal acceleration while grounded with input.")]
        private float groundAccel = 14f;
        [SerializeField, Min(0f), Tooltip("Horizontal deceleration while grounded without input.")]
        private float groundDecel = 18f;
        [SerializeField, Min(0f), Tooltip("Horizontal acceleration while airborne (air control).")]
        private float airAccel = 4f;

        [Header("Jump & Gravity")]
        [SerializeField, Min(0.1f), Tooltip("Maximum jump height in meters.")]
        private float jumpHeight = 1.2f;
        [SerializeField, Min(0f), Tooltip("Coyote time in seconds after leaving ground.")]
        private float coyoteTime = 0.12f;
        [SerializeField, Min(0f), Tooltip("Jump input buffer in seconds.")]
        private float jumpBufferTime = 0.18f;
        [SerializeField, Min(0f), Tooltip("Multiplier applied to Physics.gravity.")]
        private float gravityMultiplier = 2.5f;
        [SerializeField, Min(0f), Tooltip("Maximum fall speed (m/s).")]
        private float maxFallSpeed = 22f;
        [SerializeField, Tooltip("Downward velocity held while grounded to prevent micro-bounce.")]
        private float snapDownSpeed = -2f;

        [Header("Crouch")]
        [SerializeField, Tooltip("Toggle crouch (true) or hold to crouch (false).")]
        private bool crouchToggle = true;
        [SerializeField, Min(0.2f), Tooltip("Capsule height while crouched.")]
        private float crouchHeight = 1.2f;
        [SerializeField, Min(0.2f), Tooltip("Capsule height standing.")]
        private float standingHeight = 2f;
        [SerializeField, Min(0f), Tooltip("Capsule height lerp speed.")]
        private float crouchLerpSpeed = 12f;
        [SerializeField, Min(0f), Tooltip("Extra clearance required above the head to uncrouch.")]
        private float standClearance = 0.05f;

        [Header("Slopes")]
        [SerializeField, Tooltip("Slide down steep slopes above the walkable limit.")]
        private bool slideEnabled = true;
        [SerializeField, Range(0f, 1f), Tooltip("Minimum GroundNormal.y for a steep face to slide. Near-vertical risers/side faces (normal.y below this) are treated as steps/slope bases, not slides, so the player climbs instead of being pushed back.")]
        private float slideMinNormalY = 0.2f;
        [SerializeField, Min(0f), Tooltip("Acceleration along steep slope faces while sliding.")]
        private float slideAccel = 8f;
        [SerializeField, Min(0f), Tooltip("Maximum slide speed.")]
        private float slideMaxSpeed = 8f;

        [Header("Steps & Stairs")]
        [SerializeField, Min(0f), Tooltip("Minimum forward speed re-applied right after a step-up so the body clears the riser lip instead of being shoved back.")]
        private float minSpeedForStepRestore = 0.5f;

        private Rigidbody rb;
        private CapsuleCollider capsule;
        private float coyoteTimer;
        private float jumpBufferTimer;
        private float currentHeight;
        private float crouchAmount;
        private float originalBottomLocalY;
        private Vector3 lastPlatformVelocity;
        private bool wasGrounded;
        private bool jumpedThisFrame;
        private bool crouchPressConsumed;
        private float lastVerticalVelocity;
        private float lastJumpLogCoyote = -1f;
        [SerializeField] private bool dbg;
        [SerializeField] private bool jdbg;

        public event Action Jumped;
        /// <summary>Fired on landing. Parameter: impact speed in m/s.</summary>
        public event Action<float> Landed;

        public bool IsGrounded { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsFalling { get; private set; }
        public float Speed { get; private set; }
        public float CrouchAmount => crouchAmount;
        public Vector3 VelocityXZ { get; private set; }
        public Vector3 MoveDirectionWorld { get; private set; }
        public Vector3 FootPoint => BottomWorld();

        private Transform YawBase => body != null ? body : transform;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            capsule = GetComponent<CapsuleCollider>();
            if (input == null) input = GetComponent<PlayerInput>();
            if (ground == null) ground = GetComponent<GroundDetector>();
            if (stairs == null) stairs = GetComponent<StairStepper>();

            currentHeight = capsule.height;
            originalBottomLocalY = capsule.center.y - (capsule.height * 0.5f - capsule.radius);
        }

        private void FixedUpdate()
        {
            jumpedThisFrame = false;

            if (ground != null) ground.CheckGround();
            IsGrounded = ground != null && ground.IsGrounded;

            HandleCrouch();
            MoveDirectionWorld = GetMoveDirection();

            float dt = Time.fixedDeltaTime;
            Vector3 velocity = rb.linearVelocity;
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            float vertical = velocity.y;

            ApplyPlatformDelta(ref horizontal, ref vertical);
            horizontal = UpdateHorizontal(horizontal, dt);
            vertical = UpdateVertical(vertical, dt);
            TryStepUp(dt, ref horizontal, ref vertical);

            rb.linearVelocity = new Vector3(horizontal.x, vertical, horizontal.z);

            if (Debug.unityLogger.logEnabled && dbg)
                Debug.Log($"[DIAG Motor] tms={Time.fixedTime} gnd={IsGrounded} oS={ground?.OnSteepSlope} gAng={ground?.SlopeAngle:F1} gNy={ground?.GroundNormal.y:F2} moveDir={MoveDirectionWorld} inb=@{Speed:F2} outV={(new Vector3(horizontal.x, vertical, horizontal.z))}");

            UpdateState(dt);
        }

        private void LateUpdate()
        {
            if (body == null) return;
            float yaw = body.eulerAngles.y;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            body.localRotation = Quaternion.identity;
        }

        private Vector3 UpdateHorizontal(Vector3 horizontal, float dt)
        {
            if (ground != null && ground.OnSteepSlope && slideEnabled && ground.GroundNormal.y >= slideMinNormalY)
            {
                Vector3 slideDir = Vector3.ProjectOnPlane(Vector3.down, ground.GroundNormal).normalized;
                horizontal += slideDir * (slideAccel * dt);
                if (horizontal.sqrMagnitude > slideMaxSpeed * slideMaxSpeed)
                    horizontal = horizontal.normalized * slideMaxSpeed;
                return horizontal;
            }

            float targetSpeed = GetTargetSpeed();
            Vector3 desired = MoveDirectionWorld * targetSpeed;

            if (IsGrounded)
            {
                Vector3 platformVelocity = GetPlatformVelocity();
                desired += new Vector3(platformVelocity.x, 0f, platformVelocity.z);

                if (ground != null && ground.SlopeAngle > 0.01f)
                {
                    desired = Vector3.ProjectOnPlane(desired, ground.GroundNormal);
                    if (desired.sqrMagnitude > 0.0001f)
                        desired = desired.normalized * targetSpeed;
                }
            }

            float accel = IsGrounded
                ? (MoveDirectionWorld.sqrMagnitude > 0.001f ? groundAccel : groundDecel)
                : airAccel;
            horizontal = Vector3.MoveTowards(horizontal, desired, accel * dt);

            if (IsGrounded && ground != null)
                horizontal = Vector3.ProjectOnPlane(horizontal, ground.GroundNormal);

            return horizontal;
        }

        private float UpdateVertical(float vertical, float dt)
        {
            if (input != null && input.JumpPressed) jumpBufferTimer = jumpBufferTime;
            else jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - dt);

            if (IsGrounded) coyoteTimer = coyoteTime;
            else coyoteTimer = Mathf.Max(0f, coyoteTimer - dt);

            if (jdbg && (input != null && input.JumpPressed))
                Debug.Log($"[DIAG Jump::press] tms={Time.fixedTime:F3} grounded={IsGrounded} coyote={coyoteTimer:F3} buf={jumpBufferTimer:F3}");

            if (jdbg && jumpBufferTimer > 0f && !(coyoteTimer > 0f))
                Debug.Log($"[DIAG Jump::GATE] tms={Time.fixedTime:F3} HAVE-BUFFER={jumpBufferTimer:F3} but coyote=0 grounded={IsGrounded} -> blocked (must be grounded/coyote)");

            if (jdbg && jumpBufferTimer > 0f && coyoteTimer > 0f)
                Debug.Log($"[DIAG Jump::GATE] tms={Time.fixedTime:F3} HAVE-BUFFER={jumpBufferTimer:F3} coyote={coyoteTimer:F3} grounded={IsGrounded} -> eligible, firing");

            if (jdbg && jumpBufferTimer <= 0f && coyoteTimer > 0f && lastJumpLogCoyote <= 0f)
                Debug.Log($"[DIAG Jump::awaitPress] tms={Time.fixedTime:F3} grounded={IsGrounded} coyote={coyoteTimer:F3}");
            lastJumpLogCoyote = coyoteTimer;

            if (coyoteTimer > 0f && jumpBufferTimer > 0f)
            {
                float g = Physics.gravity.magnitude * gravityMultiplier;
                vertical = Mathf.Sqrt(2f * g * jumpHeight);
                vertical += GetPlatformVelocity().y;
                coyoteTimer = 0f;
                jumpBufferTimer = 0f;
                jumpedThisFrame = true;
                lastPlatformVelocity = Vector3.zero;
                Jumped?.Invoke();
                if (jdbg) Debug.Log($"[DIAG Jump] >>> FIRED tms={Time.fixedTime:F3} vertical={vertical:F2} groundedAfterFire={IsGrounded}");
            }
            else if (IsGrounded)
            {
                if (!jumpedThisFrame && vertical <= snapDownSpeed)
                    vertical = snapDownSpeed + GetPlatformVelocity().y;
            }
            else
            {
                vertical -= Physics.gravity.magnitude * gravityMultiplier * dt;
                vertical = Mathf.Max(vertical, -maxFallSpeed);
            }

            return vertical;
        }

        private void ApplyPlatformDelta(ref Vector3 horizontal, ref float vertical)
        {
            if (!IsGrounded)
            {
                lastPlatformVelocity = Vector3.zero;
                return;
            }
            Vector3 platformVelocity = GetPlatformVelocity();
            Vector3 delta = platformVelocity - lastPlatformVelocity;
            horizontal += new Vector3(delta.x, 0f, delta.z);
            vertical += delta.y;
            lastPlatformVelocity = platformVelocity;
        }

        private Vector3 GetPlatformVelocity()
        {
            if (ground == null || ground.GroundRigidbody == null) return Vector3.zero;
            return ground.GroundRigidbody.GetPointVelocity(ground.GroundPoint);
        }

        private void TryStepUp(float dt, ref Vector3 horizontal, ref float vertical)
        {
            if (dbg)
                Debug.Log($"[DIAG StepEnter] tms={Time.fixedTime} stairsNull={stairs == null} grounded={IsGrounded} jumped={jumpedThisFrame} moveFlat={MoveDirectionWorld} gny={ground?.GroundNormal.y:F3} gAng={ground?.SlopeAngle:F1} speed={Speed:F2}");
            if (stairs == null || !IsGrounded || jumpedThisFrame) return;
            Vector3 flat = MoveDirectionWorld;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0001f) return;

            bool stepped = stairs.TryStepUp(transform, MoveDirectionWorld, Speed, out Vector3 stepDelta);
            if (dbg) Debug.Log($"[DIAG StepResult] tms={Time.fixedTime} stepped={stepped} stepDelta={stepDelta}");
            if (stepped)
            {
                rb.MovePosition(rb.position + stepDelta);

                Vector3 stepForward = MoveDirectionWorld;
                stepForward.y = 0f;
                if (stepForward.sqrMagnitude > 0.0001f)
                {
                    stepForward.Normalize();
                    float incomingForward = Mathf.Max(0f, Vector3.Dot(horizontal, stepForward));
                    float restoredSpeed = Mathf.Max(incomingForward, Mathf.Max(Speed, minSpeedForStepRestore));
                    horizontal = stepForward * restoredSpeed;
                }

                if (vertical > snapDownSpeed) vertical = snapDownSpeed;
            }
        }

        private void HandleCrouch()
        {
            if (input == null) return;

            bool wantCrouch;
            if (crouchToggle)
            {
                if (input.CrouchPressed && !crouchPressConsumed)
                {
                    IsCrouching = !IsCrouching;
                    crouchPressConsumed = true;
                }
                if (!input.CrouchPressed) crouchPressConsumed = false;
                wantCrouch = IsCrouching;
            }
            else
            {
                IsCrouching = input.CrouchHeld;
                wantCrouch = input.CrouchHeld;
            }

            if (!wantCrouch && crouchAmount > 0.1f && ground != null && ground.CheckCeiling(standingHeight - currentHeight + standClearance))
                wantCrouch = true;

            IsCrouching = wantCrouch;
            float targetHeight = wantCrouch ? crouchHeight : standingHeight;
            currentHeight = Mathf.MoveTowards(currentHeight, targetHeight, crouchLerpSpeed * Time.fixedDeltaTime);
            crouchAmount = Mathf.InverseLerp(standingHeight, crouchHeight, currentHeight);

            capsule.height = currentHeight;
            capsule.center = new Vector3(
                capsule.center.x,
                originalBottomLocalY + currentHeight * 0.5f - capsule.radius,
                capsule.center.z);
        }

        private Vector3 GetMoveDirection()
        {
            if (input == null) return Vector3.zero;

            Vector3 forward = YawBase.forward;
            Vector3 right = YawBase.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector2 m = input.Move;
            Vector3 dir = forward * m.y + right * m.x;
            return dir.sqrMagnitude > 1f ? dir.normalized : dir;
        }

        private float GetTargetSpeed()
        {
            IsSprinting = input != null && input.SprintHeld && !IsCrouching && IsGrounded
                && (!sprintRequiresForward || input.Move.y > 0.1f);

            if (IsCrouching) return runSpeed * crouchMultiplier;
            if (input != null && input.WalkHeld) return walkSpeed;
            return IsSprinting ? runSpeed * sprintMultiplier : runSpeed;
        }

        private void UpdateState(float dt)
        {
            if (IsGrounded && !wasGrounded)
                Landed?.Invoke(Mathf.Abs(lastVerticalVelocity));
            wasGrounded = IsGrounded;

            VelocityXZ = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            Speed = VelocityXZ.magnitude;
            IsFalling = !IsGrounded && rb.linearVelocity.y < -0.5f;
            lastVerticalVelocity = rb.linearVelocity.y;
        }

        private Vector3 BottomWorld()
        {
            if (ground != null) return ground.BottomCenter();
            Vector3 center = transform.TransformPoint(capsule.center);
            return center - Vector3.up * (capsule.height * 0.5f - capsule.radius);
        }
    }
}
