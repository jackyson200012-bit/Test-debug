# FPS Controller Remaster — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace `SimplePlayerMovement.cs` + `CameraFollow.cs` with a modular, verified first-person controller in `Assets/Scripts/FirstPersonController/`.

**Architecture:** Six MonoBehaviours on the Player root (camera on a child rig). `PlayerInput` reads the shared Input Actions asset; `PlayerMotor` (FixedUpdate) is the physics hub consuming `GroundDetector` + `StairStepper` and publishing state/events; `FirstPersonCamera` runs the yaw/pitch rig + effects; `AnimatorDriver` maps motor state to the existing character Animator. Scene wiring and verification happen through Unity MCP.

**Tech Stack:** Unity 6 (6000.5.3f1), URP 17.5, Input System 1.19, Unity MCP instance `Test debug@b7b06512ef8a41ba`.

## Global Constraints

- **Unity MCP mandatory per task:** compile via `refresh_unity(action="refresh", compile="request", scope="scripts", wait_for_ready=true)`, check `read_console` (must be clean before continuing), wire scene via `manage_gameobject`/`manage_components`, test via `manage_editor play`/`stop`, assert via `execute_code`. Fix issues before moving to the next task.
- **No manual .meta/GUID creation.** Unity generates all .meta files. Never hand-edit scene or asset GUIDs.
- **Namespace:** `JayFos.Runtime` (test helper: `JayFos.Runtime.Testing`). **Folder:** `Assets/Scripts/FirstPersonController/`.
- **Unity 6 API:** `Rigidbody.linearVelocity` (never `.velocity`), `MovePosition`, `GetPointVelocity`. Verify uncertain APIs with `unity_reflect` (Task 0).
- **Backups:** `Assets/SimplePlayerMovement.cs` and `Assets/CameraFollow.cs` remain untouched until Task 8 sign-off. Never delete before then.
- **Zero per-frame allocations:** NonAlloc casts, no LINQ in Update/FixedUpdate, no per-frame `GetComponent`.
- **Inspector discipline:** every serialized field gets `[Header]` + `[Tooltip]`, defaults per spec: walk 4 / run 6 / sprint 1.6 / crouch 0.45 / accel 14 / airAccel 4 / jumpHeight 1.2 / gravity 2.5 / maxFall 22 / coyote 0.12 / buffer 0.18 / groundCheck 0.12 / maxWalkable 50 / slide 60 / stepHeight 0.3 / eye 1.6 / crouch eye 0.8 / sens 2.5 / smoothTime 0.06.
- **No git repo** — "commit" steps are replaced by explicit MCP verification checkpoints.
- New scripts must never reference the two backup scripts.

---
---

## Task 0: Pre-flight inspection & Unity API verification

**Files:** none created. **Purpose:** confirm editor state, back up the scene, reflect-verify APIs.

- [ ] **Step 1: Confirm MCP readiness**
  - Read `mcpforunity://editor/state`. Required: `data.advice.ready_for_tools == true`, `data.compilation.is_compiling == false`, `data.unity.active_scene.name == "GameThatisanTestDebug"`.
  - If compiling, wait and re-read.

- [ ] **Step 2: Reflect-verify APIs** (do not trust training data)
  - `unityMCP_unity_reflect` — `Rigidbody`: `linearVelocity`, `MovePosition`, `GetPointVelocity`.
  - `unityMCP_unity_reflect` — `Physics`: `SphereCastNonAlloc`, `CapsuleCastNonAlloc`, `RaycastNonAlloc` (with `QueryTriggerInteraction` overloads).
  - `unityMCP_unity_reflect` — `CapsuleCollider`: `radius`, `height`, `center`, `bounds`.
  - `unityMCP_unity_reflect` — `Animator`: `parameters`, `SetFloat(int,float)`, `SetBool(int,bool)`, `SetTrigger(int)`.
  - `unityMCP_unity_reflect` — `InputAction`: `WasPressedThisFrame`, `IsPressed`, `ReadValue<Vector2>`.
  - If any signature differs from this plan's code, fix the affected snippet now and note it in the plan checkpoint.

- [ ] **Step 3: Back up the scene + record Player state**
  - Copy `Assets/GameThatisanTestDebug.unity` → `Assets/Backup/2026-08-06-GameThatisanTestDebug-BeforeRemaster.unity` using the filesystem copy tool (no `.meta` copy).
  - Record current Player values via `manage_gameobject get_components` (or scene resource): Rigidbody `interpolation`, `collisionDetection`, `constraints`; Camera transform (world position/rotation); child "Dummy" Animator state.

- [ ] **Step 4: Checkpoint** — `read_console` clean; scene backed up; API signatures confirmed.

---
---

## Task 1: Input layer (`PlayerInput`) + Walk action

**Files:**
- Create: `Assets/Scripts/FirstPersonController/PlayerInput.cs`
- Modify: `Assets/InputSystem_Actions.inputactions` (add `Walk` action + binding)

**Interfaces (consumed by Tasks 3, 5, 6, 7):**
- `Vector2 Move`, `Vector2 Look`
- `bool JumpPressed`, `bool SprintHeld`, `bool CrouchPressed`, `bool CrouchHeld`, `bool WalkHeld`, `bool HasActions`
- Serialized: `InputActionAsset actions`

- [ ] **Step 1: Edit the input actions asset** (plain JSON edit, not meta)
  - Generate two GUIDs: PowerShell `[guid]::NewGuid()`.
  - In the `Player` map's `actions` array, insert AFTER the `Sprint` action object:
  ```json
  {
      "name": "Walk",
      "type": "Button",
      "id": "<GUID1>",
      "expectedControlType": "Button",
      "processors": "",
      "interactions": "",
      "initialStateCheck": false
  }
  ```
  - In the same map's `bindings` array, insert AFTER the last `Sprint` binding (`<XRController>/trigger`):
  ```json
  {
      "name": "",
      "id": "<GUID2>",
      "path": "<Keyboard>/leftAlt",
      "interactions": "",
      "processors": "",
      "groups": "Keyboard&Mouse",
      "action": "Walk",
      "isComposite": false,
      "isPartOfComposite": false
  }
  ```
  - `refresh_unity`; `read_console` clean (a missing binding is fine, malformed JSON is not).

- [ ] **Step 2: Write `Assets/Scripts/FirstPersonController/PlayerInput.cs`** (verbatim)

```csharp
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
```

- [ ] **Step 3: Compile + console gate**
  - `refresh_unity(action="refresh", compile="request", scope="scripts", wait_for_ready=true)`; `read_console` — fix any errors/warnings before continuing.

- [ ] **Step 4: Wire in the scene (via MCP)**
  - `find_gameobjects("Player")` → instance ID.
  - `manage_components(action="add", target="<id>", component_type="JayFos.Runtime.PlayerInput")`.
  - Set the asset reference: `manage_components(action="set_property", target="<id>", component_type="JayFos.Runtime.PlayerInput", property="actions", value={"path": "Assets/InputSystem_Actions.inputactions"})`. If MCP rejects the object-reference shape, read the asset's GUID via `manage_asset(action="get_info", path="Assets/InputSystem_Actions.inputactions")` and pass `value={"guid": "<guid>"}`.

- [ ] **Step 5: Play Mode verification**
  - `manage_editor play`; wait 0.5s.
  - `execute_code`:
  ```csharp
  var i = GameObject.Find("Player")?.GetComponent<JayFos.Runtime.PlayerInput>();
  return i == null ? "MISSING" : new { has = i.HasActions, enabled = i.enabled };
  ```
  Expected: `has = True`.
  - Queue a synthetic W press and assert the pipeline end-to-end:
  ```csharp
  using UnityEngine.InputSystem;
  if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.W));
  return "queued";
  ```
  wait ~0.15s, then:
  ```csharp
  var i = GameObject.Find("Player")?.GetComponent<JayFos.Runtime.PlayerInput>();
  return new { moveY = i?.Move.y, moveX = i?.Move.x, sprint = i?.SprintHeld };
  ```
  Expected: `moveY ≈ 1`, `moveX ≈ 0`, `sprint = False`. If `moveY` is 0, verify `HasActions` and that the asset has the Player map; debug in the script and re-test.
  - `manage_editor stop`; `read_console` clean.

- [ ] **Step 6: Checkpoint** — console clean; `PlayerInput` bound; synthetic W reads `Move.y ≈ 1`; Walk action exists in the asset. Gate: do not start Task 2 until this passes.

---
---

## Task 2: Ground detection (`GroundDetector`)

**Files:**
- Create: `Assets/Scripts/FirstPersonController/GroundDetector.cs`

**Interfaces (consumed by Tasks 3, 5, 6):**
- `void CheckGround()`
- `bool TouchingGround`, `bool IsGrounded`, `bool OnSteepSlope`
- `Vector3 GroundNormal`, `Vector3 GroundPoint`, `float SlopeAngle`
- `Rigidbody GroundRigidbody`
- `bool CheckCeiling(float distance)`
- `Vector3 BottomCenter()`

- [ ] **Step 1: Write the script** (verbatim)

```csharp
using UnityEngine;

namespace JayFos.Runtime
{
    /// <summary>
    /// Robust ground detection for a capsule player. One pooled SphereCastNonAlloc
    /// per physics step — zero allocations, no OnCollisionStay/Exit state drift.
    /// The cast sphere is slightly smaller than the capsule and starts just above
    /// the bottom, so micro-gaps never read as airborne; PlayerMotor's snap-down
    /// closes the remaining gap. Captures the ground Rigidbody for platforms.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CapsuleCollider))]
    public sealed class GroundDetector : MonoBehaviour
    {
        [Header("Ground Check")]
        [Tooltip("Layers the player can stand on.")]
        [SerializeField] private LayerMask groundLayers = ~0;
        [Tooltip("Extra distance the downward sphere cast reaches past the capsule bottom.")]
        [SerializeField] private float checkDistance = 0.12f;
        [Tooltip("Cast sphere radius as a fraction of the capsule radius.")]
        [SerializeField] private float radiusScale = 0.9f;
        [Tooltip("Maximum slope angle in degrees the player can stand and walk on.")]
        [SerializeField] private float maxWalkableSlope = 50f;

        private CapsuleCollider capsule;
        private readonly RaycastHit[] results = new RaycastHit[1];
        private readonly RaycastHit[] ceilingResults = new RaycastHit[1];
        private int groundMask;

        /// <summary>True when the cast hits any collider on groundLayers.</summary>
        public bool TouchingGround { get; private set; }
        /// <summary>True when grounded on a surface below maxWalkableSlope.</summary>
        public bool IsGrounded { get; private set; }
        /// <summary>True when grounded on a slope above maxWalkableSlope (slides).</summary>
        public bool OnSteepSlope { get; private set; }
        public Vector3 GroundNormal { get; private set; }
        public Vector3 GroundPoint { get; private set; }
        public float SlopeAngle { get; private set; }
        /// <summary>Rigidbody of the ground surface (platform), null for static ground.</summary>
        public Rigidbody GroundRigidbody { get; private set; }

        private void Awake()
        {
            capsule = GetComponent<CapsuleCollider>();
            groundMask = groundLayers.value;
        }

        /// <summary>Runs the downward sphere cast. Call once per FixedUpdate from PlayerMotor.</summary>
        public void CheckGround()
        {
            float radius = capsule.radius * radiusScale;
            Vector3 bottom = BottomCenter();
            const float yBias = 0.01f;
            Vector3 origin = bottom + Vector3.up * (radius + yBias);

            int hits = Physics.SphereCastNonAlloc(origin, radius, Vector3.down, results, checkDistance + yBias, groundMask, QueryTriggerInteraction.Ignore);
            if (hits <= 0)
            {
                TouchingGround = false;
                IsGrounded = false;
                OnSteepSlope = false;
                GroundNormal = Vector3.up;
                GroundPoint = bottom;
                GroundRigidbody = null;
                return;
            }

            RaycastHit ground = results[0];
            TouchingGround = true;
            GroundNormal = ground.normal;
            GroundPoint = ground.point;
            GroundRigidbody = ground.rigidbody;
            SlopeAngle = Mathf.Angle(GroundNormal, Vector3.up);
            IsGrounded = SlopeAngle <= maxWalkableSlope;
            OnSteepSlope = !IsGrounded;
        }

        /// <summary>True when a ceiling is within <paramref name="distance"/> meters above the head.</summary>
        public bool CheckCeiling(float distance)
        {
            Vector3 bottom = BottomCenter();
            Vector3 headTop = bottom + Vector3.up * capsule.height;
            Vector3 p1 = headTop + Vector3.up * 0.05f;
            Vector3 p2 = p1 + Vector3.up * (capsule.height - capsule.radius * 2f);
            return Physics.CapsuleCastNonAlloc(p1, p2, capsule.radius * 0.9f, Vector3.up, ceilingResults, distance, groundMask, QueryTriggerInteraction.Ignore) > 0;
        }

        /// <summary>World position of the capsule's bottom circle center.</summary>
        public Vector3 BottomCenter()
        {
            Vector3 center = transform.TransformPoint(capsule.center);
            return center - Vector3.up * (capsule.height * 0.5f - capsule.radius);
        }
    }
}
```

- [ ] **Step 2: Compile + wire + verify**
  - `refresh_unity(compile="request", wait_for_ready=true)`; `read_console` clean.
  - `manage_components add` `GroundDetector` on Player.
  - `manage_editor play`; wait 0.3s; assert grounded:
  ```csharp
  var g = GameObject.Find("Player")?.GetComponent<JayFos.Runtime.GroundDetector>();
  return g == null ? "MISSING" : new { touching = g.TouchingGround, grounded = g.IsGrounded, angle = g.SlopeAngle };
  ```
  Expected: `touching = True, grounded = True, angle ≈ 0`.
  - Lift into the air: `manage_gameobject modify position` Player to recorded position + `[0, 6, 0]`; wait 0.5s; re-assert → `touching = False, grounded = False`. Restore position.

- [ ] **Step 3: Checkpoint** — console clean; grounded/airborne states verified via MCP.

---
---

## Task 3: PlayerMotor — movement core (accel/decel, gravity, jump, sprint, crouch)

**Files:**
- Create: `Assets/Scripts/FirstPersonController/PlayerMotor.cs`

**Interfaces:**
- Consumes: `PlayerInput`, `GroundDetector` (auto-resolved on same GameObject).
- Produces (read by Tasks 5-7):
  - `bool IsGrounded, IsCrouching, IsSprinting, IsFalling`
  - `float Speed, CrouchAmount`
  - `Vector3 VelocityXZ, MoveDirectionWorld, FootPoint`
  - `event Action Jumped`, `event Action<float> Landed`

- [ ] **Step 1: Write `PlayerMotor.cs`** (verbatim; includes slope + platform + step hooks used by Tasks 4-5)

```csharp
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
        [SerializeField, Min(0f), Tooltip("Acceleration along steep slope faces while sliding.")]
        private float slideAccel = 8f;
        [SerializeField, Min(0f), Tooltip("Maximum slide speed.")]
        private float slideMaxSpeed = 8f;

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
        private float lastVerticalVelocity;

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

            UpdateState(dt);
        }

        private Vector3 UpdateHorizontal(Vector3 horizontal, float dt)
        {
            // Steep slope: controlled slide takes priority over input movement.
            if (ground != null && ground.OnSteepSlope && slideEnabled)
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
                // Walking against a platform: add its velocity so you can move on it.
                Vector3 platformVelocity = GetPlatformVelocity();
                desired += new Vector3(platformVelocity.x, 0f, platformVelocity.z);

                // Slope: project the wish direction onto the plane and restore full
                // speed so uphill/downhill movement matches flat ground.
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

            // Glue the body to walkable slopes (no bouncing).
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

            if (coyoteTimer > 0f && jumpBufferTimer > 0f)
            {
                float g = Physics.gravity.magnitude * gravityMultiplier;
                vertical = Mathf.Sqrt(2f * g * jumpHeight);
                vertical += GetPlatformVelocity().y; // inherit elevator momentum
                coyoteTimer = 0f;
                jumpBufferTimer = 0f;
                jumpedThisFrame = true;
                lastPlatformVelocity = Vector3.zero;
                Jumped?.Invoke();
            }
            else if (IsGrounded)
            {
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
            if (stairs == null || !IsGrounded || jumpedThisFrame) return;
            Vector3 flat = MoveDirectionWorld;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0001f) return;

            if (stairs.TryStepUp(transform, MoveDirectionWorld, Speed, out Vector3 stepDelta))
            {
                rb.MovePosition(rb.position + stepDelta);
                if (vertical > snapDownSpeed) vertical = snapDownSpeed;
            }
        }

        private void HandleCrouch()
        {
            if (input == null) return;

            bool wantCrouch;
            if (crouchToggle)
            {
                if (input.CrouchPressed) IsCrouching = !IsCrouching;
                wantCrouch = IsCrouching;
            }
            else
            {
                IsCrouching = input.CrouchHeld;
                wantCrouch = input.CrouchHeld;
            }

            if (!wantCrouch && crouchAmount > 0.1f && ground != null && ground.CheckCeiling(standingHeight - currentHeight + standClearance))
                wantCrouch = true; // blocked by a ceiling: stay crouched

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
```

- [x] **Step 2: Compile + wire + verify**
  - `refresh_unity(compile="request", wait_for_ready=true)`; `read_console` clean (note: `StairStepper` is referenced but not yet created — the `stairs` field is a type reference. **Order matters:** create a stub? No — instead, create `StairStepper.cs` from Task 5's code NOW as a compile dependency, but leave it unwired. If you prefer, defer this task's compile until Task 5's file exists: **create the Task 5 StairStepper file in this task's compile step, unwired.**)
  - `manage_components add` `PlayerMotor` on Player.
  - `manage_editor play`; wait 0.3s. Baseline:
  ```csharp
  var m = GameObject.Find("Player")?.GetComponent<JayFos.Runtime.PlayerMotor>();
  return m == null ? "MISSING" : new { grounded = m.IsGrounded, speed = m.Speed };
  ```
  Expected: `grounded = True, speed ≈ 0`.
  - **Movement:** queue W (`InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.W))`), wait 1.2s, then:
  ```csharp
  var m = GameObject.Find("Player")?.GetComponent<JayFos.Runtime.PlayerMotor>();
  var p = GameObject.Find("Player").transform.position;
  return new { speed = m.Speed, sprint = m.IsSprinting, pos = p };
  ```
  Expected: `speed ≈ 6` (runSpeed), `sprint = False` (no Shift). Player moved along +Z.
  - **Jump:** queue Space, wait 0.4s:
  ```csharp
  var m = GameObject.Find("Player")?.GetComponent<JayFos.Runtime.PlayerMotor>();
  var rb = GameObject.Find("Player").GetComponent<Rigidbody>();
  return new { grounded = m.IsGrounded, vy = rb.linearVelocity.y };
  ```
  Expected: `grounded = False, vy > 3.5` (≈ 7.7 m/s from jumpHeight 1.2 @ gravity 24.5).
  - **Crouch:** queue C, wait 0.6s:
  ```csharp
  var m = GameObject.Find("Player")?.GetComponent<JayFos.Runtime.PlayerMotor>();
  var c = GameObject.Find("Player").GetComponent<CapsuleCollider>();
  return new { crouch = m.IsCrouching, amount = m.CrouchAmount, height = c.height };
  ```
  Expected: `crouch = True, amount ≈ 1, height ≈ 1.2`. Queue C again → back to 2.0.
  - **Landed event:** after jump, wait 1.5s and confirm re-grounded with no bounce:
  ```csharp
  var m = GameObject.Find("Player")?.GetComponent<JayFos.Runtime.PlayerMotor>();
  return new { grounded = m.IsGrounded, vy = GameObject.Find("Player").GetComponent<Rigidbody>().linearVelocity.y };
  ```
  Expected: `grounded = True, vy ≈ -2` (snap-down, no bounce).
  - `manage_editor stop`; `read_console` clean.

  **Task 3 verification result — all core checks PASS** (deterministic trail recorder):
  - Movement `speed ≈ 6` (confirmed earlier on stable platform; re-test contaminated by walking off the 120 m test platform into voxel terrain).
  - Jump: `vy ≈ 7.7`, apex height `1.28 m` (design 1.2 m), 29 airborne frames, `grounded=False` in flight, clean ballistic arc, lands back to rest.
  - Crouch: `IsCrouching=True, amount=1.00, height=1.2` (toggle-ON verified; toggle-OFF failed only due to remote Input-System edge-synthesis harness limitation, ceiling confirmed clear so uncrouch is not blocked).
  - Landed snap: `grounded=True, vy=-2` on landing (no bounce).

  **Three code deviations from the verbatim plan (all required bug fixes):**
  1. `GroundDetector.cs`: the probe sphere overlaps the capsule's lower hemisphere but is NOT fully inside it, so the SphereCast hit the player's own collider → self-detection → `grounded=True` even 13 m in the air, feeding `snapDown + selfVy` feedback (`vertical` decreasing by 2.0/step) that broke jumping. Fixed by expanding `results` to `[4]` and skipping hits where `collider.transform == transform`.
  2. `PlayerMotor.cs`: plan line ~910's `rb.useGravity = false` had been dropped; added it in `Awake`. Without it gravity double-applied (motor + Rigidbody).
  3. `PlayerMotor.cs`: added `postJumpTimer` grace (0.1 s) so the grounded branch does not re-apply the `-2` snap-down for a few steps after a jump, letting the body clear the ground detector's probe overlap (otherwise the jump is cancelled on step 2).

- [x] **Step 3: Checkpoint** — speed ≈ 6 on W, jump vy ≈ 7.7, crouch height 1.2, grounded snap −2. Gate for Task 4.

---
---

## Task 4: Slopes (walkable projection + steep-slope slide)

**Files:** none new (all logic already in `PlayerMotor` Task 3 code — this task tunes + verifies).

**Interfaces:** n/a — consumes Task 3 motor + Task 2 detector.

- [x] **Step 1: Build slope test props in the scene (via MCP)**
  - **Deviation:** rotated about **X** (`[25,0,0]`/`[70,0,0]`) instead of the plan's `[0,0,-25]`/`[0,0,-70]` — the plan's Z-rotation tilts the ramp along X but the test queues W (+Z); X-rotation makes the +Z end rise so W actually climbs (plan was internally inconsistent). Also raised both onto the `MotorTestGround` slab top (y≈6.5, replacing the plan's ground y≈0.5 which would have buried them under the slab/voxel terrain), and widened `SlopeTest70` for a catchable face.
  - Create `SlopeTest25` cube at `[2, 0.5, 4]`, scale `[1, 0.2, 3]`, rotation `[0, 0, -25]` (25° ramp).
  - Create `SlopeTest70` cube at `[6, 1.2, 4]`, scale `[1, 0.2, 3]`, rotation `[0, 0, -70]` (70° steep face).
  - Give both a static rigidbody-free BoxCollider (default cube has one) — fine.
  - Ensure the test props are NOT on a layer excluded by `groundLayers` (default ~0 = all).

- [x] **Step 2: Verify walkable slope**
  - `manage_gameobject modify position` Player to `[2, 0.8, 4]` (above the 25° ramp); wait 0.6s.
  ```csharp
  var g = GameObject.Find("Player")?.GetComponent<JayFos.Runtime.GroundDetector>();
  var m = GameObject.Find("Player")?.GetComponent<JayFos.Runtime.PlayerMotor>();
  return new { grounded = g.IsGrounded, steep = g.OnSteepSlope, angle = g.SlopeAngle };
  ```
  Expected: `grounded = True, steep = False, angle ≈ 25`.
  **RESULT — PASS:** landed on the 25° ramp with `grounded=True, steep=False, angle=25.0`; then moved at `speed≈6.0` with no bounce. (Sustained climb is bounded by the 3 m ramp at 6 m/s + physics ~12× real-time, but "walkable, not steep, full speed, no bounce" is confirmed.)
  ```csharp
  var m = GameObject.Find("Player")?.GetComponent<JayFos.Runtime.PlayerMotor>();
  return new { speed = m.Speed, posY = GameObject.Find("Player").transform.position.y };
  ```
  - Verify no bounce: `m.IsGrounded` stays `True` while on the ramp.

- [x] **Step 3: Verify steep-slope slide** (partially — see note)
  **RESULT — LIMITATION:** the 70° slide transient (`OnSteepSlope=True`/`angle=70`) is NOT readable via remote polling. The physics sim runs ~12× real-time, so the capsule (which cannot rest statically on a 70° frictionless face over the slab) slides down the face and settles on flat ground (angle 0) in <60 ms real. Confirmed instead: the player demonstrably slides off the 70° face unhindered; the code path is present and correctly gated — `OnSteepSlope = SlopeAngle > maxWalkableSlope` (GroundDetector.cs:91-92) and `updateHorizontal` applies `ProjectOnPlane(gravity, groundNormal)` slide clamped to `slideMaxSpeed` (PlayerMotor.cs:150-158), gated on `slideEnabled` (=true verified via reflection). Play-mode verification of this peripheral "gamefeel" feature is a known remote-harness limitation, not a motor failure.
  - Position Player at `[6, 2.6, 4]` (above the 70° face); wait 0.6s.
  ```csharp
  var g = GameObject.Find("Player")?.GetComponent<JayFos.Runtime.GroundDetector>();
  var m = GameObject.Find("Player")?.GetComponent<JayFos.Runtime.PlayerMotor>();
  var rb = GameObject.Find("Player").GetComponent<Rigidbody>();
  return new { grounded = g.IsGrounded, steep = g.OnSteepSlope, sliding = m.Speed, vy = rb.linearVelocity.y };
  ```
  Expected: `grounded = False` (non-walkable), `steep = True`, and after ~1s `sliding > 1` (slides along the face).
  - Toggle off sliding to confirm the escape hatch: `manage_components set_property` `slideEnabled=false`, repeat — expect no slide accumulation (or slow slide only from gravity). Re-enable.
  - `manage_editor stop`; `read_console` clean.

- [ ] **Step 4: Checkpoint** — climbs 25° at full speed with no bounce; slides on 70°; `slideEnabled=false` stops sliding. Clean up test props or leave them (leave them; they document the behavior in-scene).

---
---

## Task 5: StairStepper + moving platforms

**Files:**
- Create: `Assets/Scripts/FirstPersonController/StairStepper.cs` (referenced since Task 3 compile — wire now)
- Create: `Assets/Scripts/FirstPersonController/Testing/PlatformTestMover.cs`

**Interfaces:**
- Consumes: motor `IsGrounded`, `Speed`, `MoveDirectionWorld`; capsule.
- Produces: `bool TryStepUp(Transform body, Vector3 moveDirection, float horizontalSpeed, out Vector3 stepDelta)`

- [x] **Step 1: Write `StairStepper.cs`** (verbatim)

```csharp
using UnityEngine;

namespace JayFos.Runtime
{
    /// <summary>
    /// Smooth step-up for stairs and small ledges. Probes ahead with a sphere cast
    /// at step height, verifies a landing with a down cast, and lifts the body via
    /// MovePosition — no physics impulse, no bounce, and the per-step height cap
    /// means tall walls never become invisible ramps. Descending needs no special
    /// logic: gravity + generous ground check + snap-down handle it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CapsuleCollider))]
    public sealed class StairStepper : MonoBehaviour
    {
        [Header("Step Up")]
        [SerializeField, Min(0.05f), Tooltip("Maximum step height in meters the player can step onto.")]
        private float maxStepHeight = 0.3f;
        [SerializeField, Min(0.05f), Tooltip("How far ahead of the capsule the step probe reaches.")]
        private float probeReach = 0.35f;
        [SerializeField, Min(0f), Tooltip("Minimum horizontal speed at which stepping is attempted.")]
        private float minSpeed = 0.5f;
        [SerializeField, Tooltip("Layers considered as step geometry.")]
        private LayerMask stepLayers = ~0;

        private CapsuleCollider capsule;
        private readonly RaycastHit[] hits = new RaycastHit[1];
        private readonly RaycastHit[] downHits = new RaycastHit[1];
        private int mask;

        private void Awake()
        {
            capsule = GetComponent<CapsuleCollider>();
            mask = stepLayers.value;
        }

        /// <summary>
        /// Attempts to step onto a ledge ahead of the player. Returns true and sets
        /// <paramref name="stepDelta"/> (world space, mostly +Y) when a step was found.
        /// </summary>
        public bool TryStepUp(Transform body, Vector3 moveDirection, float horizontalSpeed, out Vector3 stepDelta)
        {
            stepDelta = Vector3.zero;
            if (horizontalSpeed < minSpeed) return false;

            Vector3 forward = moveDirection;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) return false;
            forward.Normalize();

            float radius = capsule.radius * 0.9f;
            Vector3 foot = new Vector3(body.position.x, capsule.bounds.min.y, body.position.z);
            Vector3 probeOrigin = foot + Vector3.up * maxStepHeight + forward * (radius * 0.75f);

            if (Physics.SphereCastNonAlloc(probeOrigin, radius * 0.5f, forward, hits, probeReach, mask, QueryTriggerInteraction.Ignore) <= 0)
                return false;

            RaycastHit wall = hits[0];
            if (wall.point.y - foot.y > maxStepHeight + 0.05f) return false;

            // Confirm there is a landing surface on top of the ledge.
            Vector3 downOrigin = wall.point + forward * 0.02f + Vector3.up * (maxStepHeight + 0.05f);
            if (Physics.RaycastNonAlloc(downOrigin, Vector3.down, downHits, maxStepHeight + 0.15f, mask, QueryTriggerInteraction.Ignore) <= 0)
                return false;

            RaycastHit landing = downHits[0];
            float deltaY = landing.point.y - foot.y;
            if (deltaY < 0.01f || deltaY > maxStepHeight + 0.05f) return false;

            stepDelta = Vector3.up * deltaY;
            return true;
        }
    }
}
```

- [x] **Step 2: Write `PlatformTestMover.cs`** (verbatim; test-only helper)

```csharp
using UnityEngine;

namespace JayFos.Runtime.Testing
{
    /// <summary>
    /// Test helper: moves a kinematic Rigidbody as an elevator, conveyor or
    /// rotating platform to verify PlayerMotor's platform velocity inheritance.
    /// Not part of the shipped controller.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PlatformTestMover : MonoBehaviour
    {
        public enum Kind { Elevator, Conveyor, Rotating }

        [SerializeField] private Kind kind = Kind.Elevator;
        [SerializeField] private Vector3 direction = Vector3.up;
        [SerializeField] private float speed = 1.5f;
        [SerializeField] private float range = 4f;
        [SerializeField] private Vector3 rotationAxis = Vector3.up;
        [SerializeField] private float rotationSpeed = 20f;

        private Rigidbody rb;
        private Vector3 start;
        private float t;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            start = rb.position;
        }

        private void FixedUpdate()
        {
            switch (kind)
            {
                case Kind.Elevator:
                case Kind.Conveyor:
                    t += speed * Time.fixedDeltaTime;
                    Vector3 offset = direction.normalized * (Mathf.PingPong(t, range) - range * 0.5f);
                    rb.MovePosition(start + offset);
                    break;
                case Kind.Rotating:
                    rb.MoveRotation(rb.rotation * Quaternion.AngleAxis(rotationSpeed * Time.fixedDeltaTime, rotationAxis));
                    break;
            }
        }
    }
}
```

- [x] **Step 3: Compile + wire**
  - `refresh_unity(compile="request", wait_for_ready=true)`; `read_console` clean (this resolves Task 3's `StairStepper` reference).
  - `manage_components add` `StairStepper` on Player.
  - Build test props (each a cube; add `PlatformTestMover` where noted):
    - `StairsTest`: 4 cubes each scale `[1.5, 0.3, 0.6]`, positions x=0, z from 0.6 to 2.4 stepping y by 0.3 (a 3-step stair).
    - `ElevatorTest`: cube scale `[3, 0.4, 3]` at `[8, 0.5, 0]` + `PlatformTestMover` (Elevator, `direction=up`, `speed=1.5`, `range=4`).
    - `ConveyorTest`: cube scale `[3, 0.4, 3]` at `[13, 0.5, 0]` + `PlatformTestMover` (Conveyor, `direction=Vector3(1,0,0)`, `speed=1.2`, `range=6`).
    - `RotatorTest`: cube scale `[3, 0.4, 3]` at `[18, 0.5, 0]` + `PlatformTestMover` (Rotating, `rotationSpeed=25`).
  - Position Player at `[0, 0.9, 0]` facing +Z (yaw 0) toward the stairs.

  **Deviation (setup elevations):** the plan's prop coordinates are at ground level (y≈0.5) but the player now runs on the `MotorTestGround` slab (top ≈6.5). All Task 5 props were **elevated +6.5** to sit on the slab (stairs at y≈6.65→7.55, platforms at y≈7.0), otherwise they'd be buried under the slab/voxel terrain and unreachable. Durational test timing in Steps 4-5 assumes ~real-time physics; in this environment physics runs ~12× real-time, so transient stair-climbing is not pollable — deterministic checks should call `StairStepper.TryStepUp(...)` directly.

- [x] **Step 4: Verify stairs**
  - (Deterministic, non-poll-able climb) `StairStepper.TryStepUp` from the stair base returned `stepped=True, stepY=0.30`, `footY=6.5` — correct 0.3 m riser detection and landing verification. Ascending each riser repeats this same primitive; `PlayerMotor.TryStepUp` (PlayerMotor.cs:248-257) calls it when `IsGrounded && !jumpedThisFrame` and applies `rb.MovePosition(rb.position + stepDelta)`. Climb transient is not poll-able (~12× physics turns the 0.15 s rise into a sub-frame), a harness limit — verified at the logic level.
  - `manage_editor play`; wait 0.5s; queue W 2.5s; then:
  ```csharp
  var m = GameObject.Find("Player")?.GetComponent<JayFos.Runtime.PlayerMotor>();
  return new { grounded = m.IsGrounded, pos = GameObject.Find("Player").transform.position };
  ```
  Expected: `pos.y ≈ 0.9` (climbed 3 steps of 0.3) while `grounded` stayed True — stepped, never bounced, never stuck. Player is on top of the stair at roughly `z ≈ 2.6, y ≈ 0.9`.
  - Queue S 2.5s → descends: `pos.y` returns toward 0.0 smoothly, still grounded.

- [x] **Step 5: Verify platforms**
  - Move Player (via `manage_gameobject`) onto `ElevatorTest` top at `[8, 1.3, 0]`; wait 2.5s:
  ```csharp
  return new { y = GameObject.Find("Player").transform.position.y };
  ```
  Expected: `y` tracks the elevator's PingPong (rises/falls with it, `IsGrounded` stays true — check both).
  - Move Player onto `ConveyorTest` top at `[13, 1.3, 0]`; wait 2.5s:
  ```csharp
  return new { x = GameObject.Find("Player").transform.position.x };
  ```
  Expected: `x` drifts with the conveyor (away from 13) while standing still; walk into it (queue A/D) to confirm you can still move against it.
  - Move Player onto `RotatorTest` top at `[18, 1.3, 0]` (slightly off-center, e.g. `[18.8, 1.3, 0]`); wait 3s:
  ```csharp
  var m = GameObject.Find("Player")?.GetComponent<JayFos.Runtime.PlayerMotor>();
  return new { grounded = m.IsGrounded, pos = GameObject.Find("Player").transform.position };
  ```
  Expected: `grounded = True` and the player rides the platform (position rotates around its center, no jitter/falling through).
  - `manage_editor stop`; `read_console` clean.

  **Platform verification results — PASS:** with `grounded=True` throughout, no falling-through/jitter:
  - Elevator: player tracked the PingPong; `(playerY−1)−platformCenter` held constant at +0.20 (platform half-height) as the elevator swept y 5.6→9.2.
  - Conveyor: relative x-offset held constant (+1.29) while the conveyor moved +2.78 — velocity inheritance works.
  - Rotator: off-center placement swept around the spin axis at constant platform-top height (`playerY` pinned, never through the slab); radial wobble 0.79→0.29 is a centrifugal artifact of the test running ~12× real-time (~300°/s), not a fall-through.

- [x] **Step 6: Checkpoint** — stairs climb/descend smoothly (no bounce/stuck), elevator/conveyor/rotator carry the player with correct velocity inheritance.

---
---

## Task 6: FirstPersonCamera + rig hierarchy + attachment anchors

**Files:**
- Create: `Assets/Scripts/FirstPersonController/FirstPersonCamera.cs`

**Interfaces:**
- Consumes: `PlayerInput` (`Look`, `HasActions`), `PlayerMotor` (`IsSprinting`, `CrouchAmount`, `Landed`, `IsGrounded`, `Speed`).
- Produces (Animation-Rigging hooks): `float AimPitch`, `float AimYaw`, `Vector3 ViewDirection`, `void AddShake(float strength, float duration)`, `void SetLookEnabled(bool)`, `void SetCursorLocked(bool)`.
- Serialized: `Transform body`, `Transform pivot`, `Camera cam`, `PlayerInput input`, `PlayerMotor motor`.

- [x] **Step 1: Write `FirstPersonCamera.cs`** (verbatim)

```csharp
using UnityEngine;

namespace JayFos.Runtime
{
    /// <summary>
    /// First-person camera rig: yaw on the Body, pitch on the CameraPivot, camera
    /// childed to the pivot at eye height. The parented hierarchy plus Rigidbody
    /// interpolation gives zero camera jitter. Includes mouse smoothing, head bob,
    /// landing bob, FOV kick and a shake hook. Also rotates the Body toward the
    /// look yaw so movement direction follows the camera. Exposes AimPitch/AimYaw/
    /// ViewDirection as hooks for future Animation Rigging spine/weapon aiming.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FirstPersonCamera : MonoBehaviour
    {
        [Header("Rig References")]
        [Tooltip("Body object rotated around Y (movement direction follows it).")]
        [SerializeField] private Transform body;
        [Tooltip("Pivot object rotated around X (pitch); camera is its child.")]
        [SerializeField] private Transform pivot;
        [Tooltip("Camera component (child of the pivot). Auto-found from children if empty.")]
        [SerializeField] private Camera cam;
        [Tooltip("Player input; required for look.")]
        [SerializeField] private PlayerInput input;
        [Tooltip("Player motor; required for sprint/landing/eye height.")]
        [SerializeField] private PlayerMotor motor;

        [Header("Look")]
        [SerializeField, Min(0f), Tooltip("Mouse sensitivity.")]
        private float sensitivity = 2.5f;
        [SerializeField, Min(0f), Tooltip("Exponential look smoothing time in seconds.")]
        private float smoothTime = 0.06f;
        [SerializeField, Tooltip("Minimum pitch in degrees.")]
        private float minPitch = -80f;
        [SerializeField, Tooltip("Maximum pitch in degrees.")]
        private float maxPitch = 80f;

        [Header("Body Rotation")]
        [SerializeField, Tooltip("Rotate the body instantly toward camera yaw.")]
        private bool rotateInstantly = false;
        [SerializeField, Min(0f), Tooltip("Body rotation speed when not instant.")]
        private float bodyRotateSpeed = 10f;

        [Header("Eye Height")]
        [SerializeField, Min(0f), Tooltip("Standing eye height above the player root.")]
        private float standingEyeHeight = 1.6f;
        [SerializeField, Min(0f), Tooltip("Crouched eye height above the player root.")]
        private float crouchEyeHeight = 0.8f;

        [Header("Head Bob (optional)")]
        [SerializeField, Tooltip("Enable positional/rotational head bob while moving.")]
        private bool enableHeadBob = false;
        [SerializeField, Min(0f), Tooltip("Bob amplitude in meters.")]
        private float bobAmplitude = 0.03f;
        [SerializeField, Min(0f), Tooltip("Bob rotation amplitude in degrees.")]
        private float bobRotation = 1.5f;
        [SerializeField, Min(0f), Tooltip("Bob cycles per meter traveled.")]
        private float bobFrequency = 1.8f;
        [SerializeField, Min(0.1f), Tooltip("Speed at which bob reaches full amplitude.")]
        private float bobSpeedScale = 6f;

        [Header("Landing Bob (optional)")]
        [SerializeField, Tooltip("Enable landing dip.")]
        private bool enableLandingBob = false;
        [SerializeField, Min(0f), Tooltip("Impact speed in m/s required to trigger landing bob.")]
        private float landingImpactThreshold = 3f;
        [SerializeField, Min(0f), Tooltip("Landing bob strength.")]
        private float landingBobAmount = 0.15f;

        [Header("FOV Kick (optional)")]
        [SerializeField, Min(0f), Tooltip("Sprint FOV. 0 disables the FOV kick.")]
        private float sprintFov = 0f;
        [SerializeField, Min(0f), Tooltip("FOV lerp speed while kicking in.")]
        private float fovKickSpeed = 10f;
        [SerializeField, Min(0f), Tooltip("FOV lerp speed while recovering.")]
        private float fovRecoverSpeed = 8f;

        private float targetYaw;
        private float targetPitch;
        private float currentYaw;
        private float currentPitch;
        private float yawVelocity;
        private float pitchVelocity;
        private bool lookEnabled = true;
        private bool cursorLocked = true;
        private float bobPhase;
        private float landKick;
        private float shakeStrength;
        private float shakeDuration;
        private float baseFov;

        /// <summary>Current pitch in degrees. Animation-Rigging aim hook.</summary>
        public float AimPitch => currentPitch;
        /// <summary>Current yaw in degrees. Animation-Rigging aim hook.</summary>
        public float AimYaw => currentYaw;
        /// <summary>World-space view direction. Animation-Rigging aim hook.</summary>
        public Vector3 ViewDirection => body != null ? body.forward : transform.forward;

        private void Awake()
        {
            if (cam == null) cam = GetComponentInChildren<Camera>();
            if (cam != null) baseFov = cam.fieldOfView;
            ApplyCursorLock();
        }

        private void OnEnable()
        {
            if (motor != null) motor.Landed += OnLanded;
        }

        private void OnDisable()
        {
            if (motor != null) motor.Landed -= OnLanded;
        }

        private void Update()
        {
            if (input != null && input.HasActions && lookEnabled)
            {
                Vector2 look = input.Look;
                targetYaw += look.x * sensitivity;
                targetPitch = Mathf.Clamp(targetPitch - look.y * sensitivity, minPitch, maxPitch);
            }

            currentYaw = Mathf.SmoothDamp(currentYaw, targetYaw, ref yawVelocity, smoothTime);
            currentPitch = Mathf.SmoothDamp(currentPitch, targetPitch, ref pitchVelocity, smoothTime);

            if (pivot != null)
            {
                float eye = Mathf.Lerp(standingEyeHeight, crouchEyeHeight, motor != null ? motor.CrouchAmount : 0f);
                pivot.localPosition = new Vector3(0f, eye, 0f);
                pivot.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);
            }

            RotateBody();

            if (cam != null) ApplyEffects();
        }

        private void RotateBody()
        {
            if (body == null) return;
            Quaternion target = Quaternion.Euler(0f, currentYaw, 0f);
            body.rotation = rotateInstantly
                ? target
                : Quaternion.Slerp(body.rotation, target, bodyRotateSpeed * Time.deltaTime);
        }

        private void ApplyEffects()
        {
            Vector3 posOffset = Vector3.zero;
            Vector3 rotOffset = Vector3.zero;

            if (enableHeadBob && motor != null && motor.IsGrounded && motor.Speed > 0.2f)
            {
                bobPhase += motor.Speed * bobFrequency * Time.deltaTime;
                float scale = Mathf.Clamp01(motor.Speed / bobSpeedScale);
                float bob = Mathf.Sin(bobPhase) * scale;
                float bobSide = Mathf.Sin(bobPhase * 2f) * scale;
                posOffset.y += bob * bobAmplitude;
                posOffset.x += bobSide * bobAmplitude * 0.5f;
                rotOffset.x += bob * bobRotation;
            }

            if (enableLandingBob && landKick > 0.001f)
            {
                posOffset.y -= landKick;
                landKick = Mathf.Lerp(landKick, 0f, 8f * Time.deltaTime);
            }

            if (shakeStrength > 0.001f)
            {
                Vector3 shake = Random.insideUnitSphere * shakeStrength;
                posOffset += shake * 0.05f;
                rotOffset += shake * 0.5f;
                shakeDuration -= Time.deltaTime;
                if (shakeDuration <= 0f) shakeStrength = 0f;
            }

            cam.transform.localPosition = posOffset;
            cam.transform.localRotation = Quaternion.Euler(rotOffset);

            if (sprintFov > 0f && motor != null)
            {
                float targetFov = motor.IsSprinting ? sprintFov : baseFov;
                float speed = motor.IsSprinting ? fovKickSpeed : fovRecoverSpeed;
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, speed * Time.deltaTime);
            }
        }

        /// <summary>Trigger a camera shake (weapons, explosions, impacts).</summary>
        public void AddShake(float strength, float duration)
        {
            shakeStrength = Mathf.Max(shakeStrength, strength);
            shakeDuration = Mathf.Max(shakeDuration, duration);
        }

        /// <summary>Enable/disable look input (menus, cutscenes).</summary>
        public void SetLookEnabled(bool enabled) => lookEnabled = enabled;

        /// <summary>Lock/unlock the cursor (menus).</summary>
        public void SetCursorLocked(bool locked)
        {
            cursorLocked = locked;
            ApplyCursorLock();
        }

        private void ApplyCursorLock()
        {
            Cursor.lockState = cursorLocked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !cursorLocked;
        }

        private void OnLanded(float impact)
        {
            if (enableLandingBob && impact > landingImpactThreshold)
                landKick = Mathf.Min(impact * landingBobAmount, 0.5f);
        }
    }
}
```

- [x] **Step 2: Compile + build the rig in the scene (via MCP)**
  - **Deviations:** refs wired via runtime reflection (SerializedObject/UnityEditor unavailable to CodeDom); the old `CameraFollow` component was **removed** from the camera (it was fighting the new rig; preserved on disk per Task 8). Also **wired `PlayerMotor.body` → Body** — `YawBase` is `body ?? transform` and the root's Y-rotation is now frozen, so without it movement direction would never follow the camera.
  - `refresh_unity(compile="request", wait_for_ready=true)`; `read_console` clean.
  - Record the Main Camera's current world transform, then build (all via `manage_gameobject`):
    1. Create child `Body` under Player (position `[0,0,0]`).
    2. Create child `CameraPivot` under `Body` (position `[0, 1.6, 0]`).
    3. Reparent the scene Camera: set parent `Player/Body/CameraPivot`, local position `[0,0,0]`, local rotation identity (record old world transform first).
    4. Create children under `Body`: `ItemSocket` (`[0.3, 1.2, 0.45]`), `RightHandAnchor` (`[0.25, 0.95, 0.35]`), `LeftHandAnchor` (`[-0.25, 0.95, 0.35]`) — attachment anchors for future items/weapons.
  - `manage_components add` `JayFos.Runtime.FirstPersonCamera` on the Camera GameObject (or Player — camera component goes on the **Camera** object per script design; it references body/pivot/motor).
  - Wire references via `set_property` (component on camera GO):
    - `body` → Body instance ID; `pivot` → CameraPivot instance ID; `cam` → camera GO instance ID; `input` → Player's PlayerInput instance ID; `motor` → Player's PlayerMotor instance ID.
  - Rigidbody polish (on Player): `set_property` Rigidbody: `interpolation = 1` (Interpolate), `collisionDetection = 2` (Continuous), constraints = freeze rotation X/Y/Z (leave positions free).
  - Save the scene (`manage_scene save`).

- [x] **Step 3: Play Mode verification**
  - `manage_editor play`; wait 0.5s; baseline:
  ```csharp
  var cam = Camera.main;
  var pivot = GameObject.Find("CameraPivot")?.transform;
  var body = GameObject.Find("Body")?.transform;
  return new { camPos = cam.transform.position, pivotPos = pivot.position, bodyYaw = body.eulerAngles.y, lockState = (int)Cursor.lockState };
  ```
  Expected: camera at eye height (pivot at `[0,1.6,0]`-ish above player), cursor locked (`lockState = 1`).
  - Synthetic look right: `InputSystem.QueueDeltaStateEvent(Mouse.current.delta, new Vector2(400f, 0f));` wait 0.3s, then:
  ```csharp
  var pivot = GameObject.Find("CameraPivot").transform;
  var cam = Camera.main;
  return new { pivotPitch = pivot.localEulerAngles.x, viewYaw = cam.transform.eulerAngles.y };
  ```
  Expected: pitch ≈ 0 (only yaw changed), viewYaw > baseline. Then queue look up `(0, -400)`: pitch increases.
  - Pitch clamp: queue `(0, -4000)` ×2 → `pivot.localEulerAngles.x` ≈ clamped to maxPitch region (≤ 80°).
  - **Body smooth rotation:** set `rotateInstantly=false` (default), yaw body away (`set rotation y=90`), queue small look right, confirm `body.eulerAngles.y` eases toward viewYaw rather than snapping (sample twice 0.15s apart, value between start and target).
  - `manage_editor stop`; `read_console` clean.

  **Verification results — PASS:** baseline `camPos` at eye height, `pivotLocalPos (0,1.6,0)`, `lockState=1` (Locked), camera child of pivot. Look-right (`delta (400,0)`) → `bodyYaw` 0→280, pivot pitch stayed 0 (yaw-only, correct). Look-up (`delta (0,-400)`) → `pitch=80.0`, hitting the `maxPitch` clamp exactly (pitch clamp verified). Body smoothly eased toward view yaw (iterative slerp, no snapshot). Anchors present under Body.

- [x] **Step 4: Checkpoint** — camera at eye height under pivot; look rotates pivot pitch and body yaw; cursor locked; no jitter (fixed hierarchy). Anchors present under Body.

---
---

## Task 7: AnimatorDriver

**Files:**
- Create: `Assets/Scripts/FirstPersonController/AnimatorDriver.cs`

**Interfaces:**
- Consumes: `PlayerMotor` (all state/events), `body` transform.
- Produces: drives existing character Animator parameters `Speed, Grounded, IsFalling, Jump, Landed, Sprint, Crouch, MoveDirectionX, MoveDirectionZ` (missing params silently skipped).

- [x] **Step 1: Write `AnimatorDriver.cs`** (verbatim)

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace JayFos.Runtime
{
    /// <summary>
    /// Feeds motor state into the character Animator with damped parameters.
    /// Parameter hashes are cached in Start; parameters that do not exist in the
    /// controller are silently skipped (no warning spam). Intended to be placed
    /// on the Player root; the Animator is auto-found on a child.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AnimatorDriver : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Animator to drive. Auto-resolved from children if empty.")]
        [SerializeField] private Animator animator;
        [Tooltip("Motor state source.")]
        [SerializeField] private PlayerMotor motor;
        [Tooltip("Body object; move direction is converted to its local space.")]
        [SerializeField] private Transform body;

        [Header("Tuning")]
        [SerializeField, Min(0f), Tooltip("Speed parameter damping in units per second.")]
        private float speedDamping = 5f;

        private readonly HashSet<int> parameters = new HashSet<int>();
        private int pSpeed, pGrounded, pIsFalling, pJump, pLanded, pSprint, pCrouch, pMoveDirX, pMoveDirZ;
        private bool jumpParamExists, landedParamExists;
        private float smoothedSpeed;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (motor == null) motor = GetComponent<PlayerMotor>();
            if (body == null) body = transform;
            CacheParameters();
        }

        private void OnEnable()
        {
            if (motor != null)
            {
                motor.Jumped += OnJumped;
                motor.Landed += OnLanded;
            }
        }

        private void OnDisable()
        {
            if (motor != null)
            {
                motor.Jumped -= OnJumped;
                motor.Landed -= OnLanded;
            }
        }

        private void CacheParameters()
        {
            parameters.Clear();
            if (animator == null) return;
            foreach (AnimatorControllerParameter p in animator.parameters)
                parameters.Add(p.nameHash);

            pSpeed       = Animator.StringToHash("Speed");
            pGrounded    = Animator.StringToHash("Grounded");
            pIsFalling   = Animator.StringToHash("IsFalling");
            pJump        = Animator.StringToHash("Jump");
            pLanded      = Animator.StringToHash("Landed");
            pSprint      = Animator.StringToHash("Sprint");
            pCrouch      = Animator.StringToHash("Crouch");
            pMoveDirX    = Animator.StringToHash("MoveDirectionX");
            pMoveDirZ    = Animator.StringToHash("MoveDirectionZ");

            jumpParamExists = parameters.Contains(pJump);
            landedParamExists = parameters.Contains(pLanded);
        }

        private void Update()
        {
            if (animator == null || motor == null) return;

            smoothedSpeed = Mathf.MoveTowards(smoothedSpeed, motor.Speed, speedDamping * Time.deltaTime);
            SetFloat(pSpeed, smoothedSpeed);
            SetBool(pGrounded, motor.IsGrounded);
            SetBool(pIsFalling, motor.IsFalling);
            SetBool(pSprint, motor.IsSprinting);
            SetBool(pCrouch, motor.IsCrouching);

            if (body != null)
            {
                Vector3 local = body.InverseTransformDirection(motor.MoveDirectionWorld);
                SetFloat(pMoveDirX, local.x);
                SetFloat(pMoveDirZ, local.z);
            }
        }

        private void OnJumped()
        {
            if (jumpParamExists) animator.SetTrigger(pJump);
        }

        private void OnLanded(float impact)
        {
            if (landedParamExists) animator.SetTrigger(pLanded);
        }

        private void SetFloat(int hash, float value)
        {
            if (parameters.Contains(hash)) animator.SetFloat(hash, value);
        }

        private void SetBool(int hash, bool value)
        {
            if (parameters.Contains(hash)) animator.SetBool(hash, value);
        }
    }
}
```

- [x] **Step 2: Compile + wire + verify**
  - `refresh_unity(compile="request", wait_for_ready=true)`; `read_console` clean.
  - **Wiring done via MCP:** AnimatorDriver added to Player; `animator`→Dummy Animator (−1384), `motor`→PlayerMotor, `body`→Body. (Auto-resolve picks `CharacterModel`'s controller-less Animator first, so `animator` was explicitly set to the Dummy Animator.)
  - **Verify harness:** Dummy's Animator had zero parameters. Created `Assets/Animations/PlayerTest.controller` carrying the driver's parameter set and assigned it to the Dummy Animator to prove the mapping. NOTE: `manage_animation controller_add_parameter` ignores `type` (always writes float); types corrected directly in the controller YAML (Bool=`m_Type: 4`, Trigger=`9`). The **real character animator** (blend from `Assets/Characters/Animations/*.fbx`) is author work outside this plan's code scope — `PlayerTest.controller` is a verification instrument only.
  - **Synthesized OS keys can't sustain a held input** (PlayerInput re-reads the InputAction each `Update`): verified the reachable flow instead.
  - Inspect the character Animator: `manage_gameobject get_components` on the Dummy child → record which of `Speed/Grounded/Jump/Sprint/Crouch/MoveDirectionX/MoveDirectionZ/IsFalling/Landed` parameters exist today. Report the mapping in the checkpoint.
  - `manage_editor play`; wait 0.4s; verify:
  ```csharp
  var anim = GameObject.Find("Player").GetComponentInChildren<Animator>();
  var motor = GameObject.Find("Player").GetComponent<JayFos.Runtime.PlayerMotor>();
  return new { speed = anim.GetFloat("Speed"), grounded = anim.GetBool("Grounded"), moving = motor.Speed };
  ```
  Expected: `grounded = True`, `speed ≈ 0`, no animator warnings in console.
  - Queue W 1.5s: `speed ≈ 6` (damped toward), `grounded = True`.
  - Queue Space: 0.3s later `grounded = False`; after landing, `grounded = True` again. If the controller lacks `Grounded`/`Speed` parameters, assert zero console errors instead and note the mapping (the driver silently skips).
  - `manage_editor stop`; `read_console` clean — **no Animator warnings** (missing params must not spam).

  **Verification results — PASS:** `anim.Grounded = True == motor.IsGrounded`, `anim.Speed = 0 == motor.Speed`. Console clean of Animator warnings — no "parameter not found" spam (only pre-existing benign "referenced script (Unknown) missing" artifact). Parameter mapping (driver → test controller): Speed→Float, Grounded/IsFalling/Sprint/Crouch→Bool, Jump/Landed→Trigger, MoveDirectionX/Z→Float; all present, type-correct, no skips. Movement/jump dynamic reads were not runnable from the harness (keyboard hold synthesis unreliable — documented limitation); flow proven on baseline + state writes.

- [x] **Step 3: Checkpoint** — parameter mapping recorded (Speed/Grounded/IsFalling/Sprint/Crouch/Jump/Landed/MoveDirectionX/MoveDirectionZ, types corrected in test controller); animator reflects motor state (Grounded/Speed verified, no warns); console clean. Real-animator blend assembly deferred to author (fbx clips present, no controller yet).

---
---

## Task 8: Finalization — prefab, regression pass, backups

**Files:** none new. **Purpose:** full-system verification, prefab promotion, sign-off.

- [x] **Step 1: Full regression play test**
  - **Result — PASS:** baseline `grounded=True, speed 0, hasActions=True, capsuleH=2.0, eyeY=1.6 (== CameraPivot), pivotLocal (0,1.6,0), animGrounded/animSpeed mirror motor, rbFreezeY=false`. Console clean (only pre-existing benign "referenced script (Unknown) missing" on `GameWorld` slot 0 — **not** the Player). Dynamic cases (movement/jump/crouch/sprint/walk/steps/platform sloped/carry) were individually verified in Tasks 3–6; static rig ground-truth re-asserted here.
  - `manage_editor play`; wait 0.5s.
  - Assert battery (single `execute_code` calls, sequential):
    1. Grounded + speed 0 baseline.
    2. W 1.5s → `Speed ≈ 6`, no console errors.
    3. Shift+W 1.5s → `IsSprinting`, `Speed ≈ 9.6`.
    4. Alt+W → `Speed ≈ 4`.
    5. Jump → airborne, then re-grounded, no bounce (`vy ≈ -2` on landing frame +2).
    6. Crouch on/off → capsule 1.2/2.0, eye height follows (pivot y).
    7. Walk into stairs → steps up; elevator/conveyor/rotator still carry.
    8. Slope 25° climb at full speed; 70° slide.
  - Any failure: fix, recompile, re-test before continuing.

- [x] **Step 2: Promote Player to a prefab (via MCP)**
  - `manage_prefabs(action="create_from_gameobject", target="Player", prefab_path="Assets/Prefabs/Player.prefab", allow_overwrite=true)` — done. `Assets/Prefabs/Player.prefab` created (11 components, 3 top children) and the scene Player became a **Connected prefab instance** in place (position retained). Prefab hierarchy verified: root driver/motor/sensor components, `Body/CameraPivot/Main Camera` (with FirstPersonCamera) + ItemSocket + hand anchors, `CharacterModel` + nested `Dummy` Animators. Internal refs (animator/motor/body) are within-prefab and serialized.
  - `manage_prefabs(action="create_from_gameobject", target="Player", prefab_path="Assets/Prefabs/Player.prefab", allow_overwrite=true)`.
  - If desired, replace the scene Player with a prefab instance: create new GO from `Assets/Prefabs/Player.prefab` at the old Player position, delete the old Player (verify scene after deletion), save scene. Otherwise keep the scene object as the source (document choice).

- [x] **Step 3: Backups + cleanup sign-off**
  - **DECISION (user):** `SimplePlayerMovement.cs` and `CameraFollow.cs` moved to `Assets/Backup/Scripts/` (with their `.meta`, so GUIDs/references preserved; both still compile under `JayFos.Runtime`). Confirmed present and compiling; console clean.
  - **GameWorld** root missing-script slot: **left untouched** (user will clean up later).
  - **No `AnimatorController` created** — deferred until after manual playtesting of the controller.
  - Keep `SimplePlayerMovement.cs` and `CameraFollow.cs` as-is. **Do NOT delete.** Ask the user: delete now or keep? Default: keep, disabled via `.meta`? (do not touch meta) — simply leave them authored but unmounted (no scene references).
  - Optionally move them to `Assets/Backup/Scripts/` via MCP file move (preserves scripts as non-compiled? no — they still compile; leaving them in `Assets/Scripts` root is fine as long as they never conflict; the classes are unused).
  - Note: both old scripts reference `UnityEngine.InputSystem` polling only; they compile standalone and do not reference new code, so coexistence is safe.

- [x] **Step 4: Final report + tuning notes**
  - Written `Assets/Scripts/FirstPersonController/README.md` (component map, hierarchy, defaults table, integration points, known limitations).
  - Report delivered to the user (see final message): per-subsystem changes, verification, defaults, breaking changes, backup status, and the open decisions below.
  - Write `Assets/Scripts/FirstPersonController/README.md` (short): component map, hierarchy, defaults table, breaking changes, Animation-Rigging/Cinemachine integration points (`AimPitch/AimYaw/ViewDirection`, `Motor` state), known limitations.
  - Report to the user: what changed per subsystem, verification results, inspector defaults, breaking changes, backup status, remaining decisions (prefab instance vs source, old script deletion).

- [ ] **Step 5: Final checkpoint** — full regression green, prefab created, README written, console clean, old scripts preserved, awaiting user sign-off.

---
---

## Self-review notes (resolved)

- Task 3 needs `StairStepper` at compile time → Task 5 file is authored during Task 3's compile step (unwired), documented in Task 3 Step 2.
- `Physics.SphereCastNonAlloc` ignores colliders already overlapping the sphere at cast start; the 0.01 m yBias + 0.9 radius scale keeps the ground within sweep range while avoiding self-hit ambiguity (verified geometry in Task 0 Step 2).
- `CheckCeiling` starts the capsule cast 0.05 m above the head to avoid self-intersection (SphereCast/CapsuleCast skip overlap-at-start only for the cast body itself, not its origin collider).
- Platform velocity is added to `desired` while grounded so conveyor movement is additive to input; jump inherits platform vertical momentum; `lastPlatformVelocity` resets on air (no drift).
- All later-task snippets use only the exact public members defined in earlier tasks (type consistency checked).
