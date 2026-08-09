# JayFos Runtime — First-Person Controller (remaster)

Modular FPS controller that replaces the legacy `SimplePlayerMovement.cs` + `CameraFollow.cs`
mono-behaviours. Built in `JayFos.Runtime`; all class files live in this folder.

## Component map

| Component | Purpose | Placed on |
|-----------|---------|-----------|
| `PlayerInput` | Single source of input truth; reads the shared Input Actions asset each `Update`, caches state for `FixedUpdate` consumers | Player (root) |
| `GroundDetector` | Sphere-cast ground probe; slope angle, ceiling check, `Landed` signal | Player |
| `PlayerMotor` | Movement, sprint/walk/crouch, jump, landing snap, steep-slope slide, platform carry | Player |
| `StairStepper` | Step-up probe + down-cast landing verify before raising the player | Player |
| `FirstPersonCamera` | Body-yaw / pivot-pitch rig, eye height, head-bob, landing-bob, FOV kick, shake | Main Camera |
| `AnimatorDriver` | Feeds motor state into the character Animator, damped, missing-param safe | Player (root) |

The legacy `SimplePlayerMovement` / `CameraFollow` components are **preserved unmounted** (not
referenced by the scene) for rollback. Do not delete until you are confident.

## Hierarchy

```
Player                        Root — Rigidbody (interp, cont. col, freeze Y-rot), CapsuleCollider (r=0.5, h=2)
├─ Body                       Yaw control; PlayerMotor.body (action base); movement dir → body-local
│  ├─ CameraPivot             Local (0,1.6,0) — pitch (aim)
│  │   └─ Main Camera         FirstPersonCamera
│  ├─ ItemSocket              (0.3,1.2,0.45) weapon/held-item mount
│  ├─ RightHandAnchor         (0.25,0.95,0.35)
│  └─ LeftHandAnchor          (-0.25,0.95,0.35)
├─ CharacterModel             Mesh + Animator (visual only, non-solid)
└─ Dummy                      Nested Dummy.fbx + Animator (driver target)
```

The root's Y-rotation is frozen and yaw lives on the `Body`; `PlayerMotor.body` is wired to `Body`
so movement direction follows the camera.

## Defaults (inspector)

| Setting | Default |
|---------|---------|
| CapsuleCollider | r=0.5, height 2.0 (auto: 1.2 when crouched) |
| Rigidbody | interpolation Interpolate, collisionDetection Continuous, freeze all rotation |
| Max walk speed | 6.0 |
| Sprint multiplier | 1.6 (→ 9.6) |
| Walk / press-to-walk | 0.67× |
| Crouch | hold-to-crouch + toggle |
| Jump force | gravity -24, jump impulse 7.5 (apex ≈ 1.28 m) |
| Camera eye height | standing 1.6 / crouch 0.8 |
| Max aim pitch | ±80° |
| Dampings | speed 5/u·s, body yaw slerp (non-instant) |

## Integration points

- **Aim / view:** `FirstPersonCamera.AimPitch`, `AimYaw`, `ViewDirection` for Animation-Rigging /
  Cinemachine / weapon aim lines.
- **Motor state:** `Speed`, `IsGrounded`, `IsFalling`, `IsSprinting`, `IsCrouching`,
  `MoveDirectionWorld`, events `Jumped`, `Landed(float impact)`. `AnimatorDriver` (and it is) wired
  to these.
- Input asset: `InputSystem_Actions`, map `Player` with `Move/Look/Jump/Sprint/Crouch/Walk`.

## Known limitations / notes

- Physics sim runs ≈12× faster than real time in-editor for this project; stair-climb and
  steep-slide transients are sub-frame. Steady-state platform carry and walkable-slope reads verify
  correctly.
- OS-keyboard synthesis can't sustain a held InputAction here (`PlayerInput` re-reads the
  asset every frame), so the animator driver was parameter-mapped and verified on baseline/state
  writes rather than a held-move stream.
- A controller named `Testing/PlayerTest.controller` was used as a verification instrument on the
  Dummy Animator; the real character animator (blend from `Assets/Characters/Animations/*.fbx`) is
  author work — wire an AnimatorController over the Dummy/CharacterModel Animator and the driver
  feeds the `Speed/Grounded/Sprint/Crouch/MoveDirectionX/MoveDirectionZ/IsFalling/Jump/Landed`
  parameters automatically.