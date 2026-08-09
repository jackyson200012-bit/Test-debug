# FPS Controller Remaster — Design Spec

Date: 2026-08-06
Project: Test debug (Unity 6000.5.3f1, URP 17.5, Input System 1.19)
Status: Approved by user

## 1. Goal

Remaster the existing two-script controller (`SimplePlayerMovement.cs`, `CameraFollow.cs`)
into a modular, AAA-quality first-person controller for an FPS: smooth movement, robust
grounding, slopes, stairs, moving platforms, camera polish, Animator integration, and
Animation-Rigging-ready hooks — clean, documented, tunable, and verified in-editor via
Unity MCP.

## 2. Decisions (from brainstorming)

- **Perspective:** First-person (breaking change; third-person orbit camera is removed).
- **Input:** Reuse existing `Assets/InputSystem_Actions.inputactions`. Add one **Walk**
  action (LeftAlt). Wire via runtime asset reference (no generated-wrapper dependency).
- **Scope:** Core movement mechanics on by default (sprint, crouch, walk, coyote, buffer,
  slopes, stairs, platforms). Cosmetic effects implemented but default off (head bob,
  landing bob, FOV kick).
- **Body:** Visible animated character kept; `AnimatorDriver` drives existing params.
- **Character rotation:** Smooth configurable yaw toward camera (default on), instant option.
- **Unity MCP:** Use for every milestone — compile, console, scene wiring, Play Mode tests.
- **Backups:** `SimplePlayerMovement.cs` and `CameraFollow.cs` remain untouched until the
  new controller passes all milestones; removed only after user sign-off.
- **No manual .meta/GUID editing:** Unity generates all .meta files; scene wiring via MCP.
- **Attachment anchors:** `ItemSocket`, `RightHandAnchor`, `LeftHandAnchor` added to the rig
  for future item/weapon systems.

## 3. Architecture

Folder: `Assets/Scripts/FirstPersonController/`. Namespace stays `JayFos.Runtime`.

| File | Responsibility |
|---|---|
| `PlayerInput.cs` | Wraps the existing actions asset. Publishes Move/Look vectors and Jump/Sprint/Crouch/Walk state. No physics. |
| `PlayerMotor.cs` | Hub. FixedUpdate: accel/decel, gravity, jump (coyote+buffer), crouch, sprint, slope projection, platform velocity inheritance. State + events. |
| `GroundDetector.cs` | SphereCastNonAlloc down-check; ground layers; slope angle; snap-to-ground; ground Rigidbody capture. |
| `StairStepper.cs` | Forward probe + `MovePosition` step-up; step height cap; descending handled by gravity+snap. |
| `FirstPersonCamera.cs` | Yaw/pitch look, smoothing, cursor lock, head bob (off), landing bob (off), FOV kick (off), `AddShake`, aim hooks. |
| `AnimatorDriver.cs` | Damped Speed, Grounded, IsFalling, Jump/Landed triggers, Sprint, Crouch, MoveDirectionX/Z. Cached hashes; skips missing params. |

Rig hierarchy (built via MCP in scene):

```
Player (Rigidbody, CapsuleCollider, all motor components, Animator root target)
├── Body (yaw; model parent; smooth rotate toward camera)
│   ├── CameraPivot (pitch)
│   │   └── Main Camera (eyeHeight local position)
│   ├── ItemSocket (world-space attachment anchor)
│   ├── RightHandAnchor
│   └── LeftHandAnchor
└── (existing animated character child stays)
```

## 4. Subsystem specs

### 4.1 Input (PlayerInput)
- `[SerializeField] InputActionAsset actions` (drag `InputSystem_Actions`).
- Reads in `Update`, caches into public fields read by FixedUpdate consumers:
  `Move (Vector2)`, `Look (Vector2)`, `JumpPressed (edge)`, `SprintHeld`, `CrouchToggled`,
  `WalkHeld`.
- Adds `Walk` action (LeftAlt) to the actions asset. All existing bindings unchanged.

### 4.2 Ground detection (GroundDetector)
- `SphereCastNonAlloc` (1-elem reusable array) from capsule bottom + small offset, radius
  0.9× capsule radius, distance `groundCheckDistance` (0.12), `groundLayers` mask.
- Outputs to Motor: `IsGrounded` (walkable only), `GroundNormal`, `GroundPoint`,
  `SlopeAngle`, `GroundRigidbody` (platform).
- Snap-to-ground: while grounded hold vertical velocity at `snapDownSpeed` (-2) to prevent
  micro-bounce / false airborne.

### 4.3 Movement physics (PlayerMotor)
- Speeds: `walkSpeed` 4, `runSpeed` 6, `sprintMultiplier` 1.6, `crouchMultiplier` 0.45.
- Horizontal velocity `MoveTowards` desired (camera-relative input × target speed) with
  `groundAccel` 14 / `groundDecel` 18 / `airAccel` 4.
- Sprint: requires forward input (flag `sprintRequiresForward`), disabled airborne/crouched.
- Gravity: `AddForce(gravity * gravityMultiplier)` with `gravityMultiplier` 2.5, vertical
  clamp `maxFallSpeed` -22.
- Jump: `jumpVelocity = sqrt(2 * g * jumpHeight)`, `jumpHeight` 1.2; coyote 0.12, buffer 0.18.
- `Landed` event with impact speed; buffered jumps auto-fire on landing.
- Crouch: capsule height/center lerp (standing 2.0/1.0 → crouched 1.2/0.6 default), ceiling
  check before standing (capsule cast up).
- Public state: `IsGrounded, IsCrouching, IsSprinting, IsFalling, VelocityXZ,
  MoveDirectionWorld, Speed, Jumped/Landed events`.

### 4.4 Slopes
- `maxWalkableSlope` 50°. Wish dir projected onto slope plane and re-normalized (full speed
  uphill/downhill). Resulting velocity projected onto `GroundNormal` each step (no bounce).
- `slideThreshold` 60°; above it, non-walkable → controlled `slideAccel` along slope face.
  `slideEnabled` flag.

### 4.5 Stairs & platforms
- StairStepper: while grounded + moving, forward `SphereCast` at step height; if wall top
  within `maxStepHeight` 0.3 and reachable, `MovePosition` up by delta (no bounce, no ramps).
- Platforms: while grounded on a Rigidbody, `platform.GetPointVelocity(foot)`; player
  velocity `+= (current − last)` each FixedUpdate; platform velocity added to movement
  target. No parenting. Handles conveyors, elevators, rotating platforms.
- `lastPlatformVelocity` reset on ground change / off-platform.

### 4.6 Camera (FirstPersonCamera)
- Parented under `CameraPivot`; pitch local, yaw on `Body` (motor eases body yaw).
- Look: accumulate `Look × sensitivity` (2.5), exponential damping `smoothTime` 0.06,
  pitch clamp ±80.
- Cursor lock on start; `SetLookEnabled(bool)` for menus.
- Head bob: distance-traveled sine, amplitude/freq serialized, default off.
- Landing bob: `Landed` event, impact threshold, default off.
- FOV kick: `sprintFov` > 0 enables; lerp while sprinting, recover after; default 0.
- `AddShake(strength, duration)`: decaying random offset; always armed.
- Aim hooks: `AimPitch`, `AimYaw`, `ViewDirection` (world), `AimPoint` (serialized
  Transform or self).

### 4.7 Animator (AnimatorDriver)
- Update-time; reads motor state. Params: `Speed` (damped `speedDamping` 5), `Grounded`,
  `IsFalling`, `Jump` (trigger), `Landed` (trigger), `Sprint`, `Crouch`, `MoveDirectionX/Z`.
- Hashes cached in `Start` from `animator.parameters`; missing params skipped silently.

### 4.8 Animation Rigging hooks (not implemented this pass)
- Public: `Camera.AimPitch/AimYaw/ViewDirection`, `Motor.VelocityXZ/MoveDirectionWorld`.
- Documented integration points for spine aiming / weapon hold / upper-body rigs.

## 5. Inspector defaults & tooltips

Every serialized field gets `[Header]` grouping + `[Tooltip]`. Key defaults listed above
(walk 4 / run 6 / sprint ×1.6 / crouch ×0.45 / accel 14 / airAccel 4 / jumpHeight 1.2 /
gravity ×2.5 / maxFall −22 / coyote 0.12 / buffer 0.18 / groundCheck 0.12 / maxWalkable 50 /
slide 60 / stepHeight 0.3 / eye 1.6 / crouch eye 0.8 / sensitivity 2.5 / smoothTime 0.06).

## 6. Breaking changes

1. `CameraFollow.cs` and `SimplePlayerMovement.cs` removed after verification (kept as
   backups until sign-off).
2. Third-person orbit camera removed — camera becomes FPS child rig.
3. Input moves from polling to the actions asset (same default keys).
4. Scene Player requires re-wiring (new components, camera re-parented, rig built).

## 7. Verification workflow (Unity MCP per milestone)

Order: Input → Ground Detection → Movement → Slopes → Stairs/Platforms → Camera → Animator
→ polish/final. For each: create scripts → `refresh_unity(compile=request)` → `read_console`
(no errors) → wire scene via MCP → Play Mode short test → console check → fix → next.
Rigidbody settings set in editor: Interpolate, Continuous, freeze rotation (existing).
Final: promote Player to prefab, keep old scripts as backups until user sign-off.

## 8. Out of scope

- Full spine/weapon aiming, weapon viewmodel, Animation Rigging rigs, Cinemachine.
- Multiplayer replication logic (architecture is per-player, no statics — ready for it).
