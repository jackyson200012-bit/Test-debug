# Design: Procedural arm/head aim for the player model (all animation states)

## Goal
When the player pitches the camera up/down, the character's arms and head must
rotate up/down in sync with the camera, in **every** animation state (Idle,
Walk, Run, Sprint, Crouch, Jump, Fall...). This must match the behavior the user
wants to see from the existing FBX clips without editing them.

## Mechanism (chosen: procedural script on all states)
A new `MonoBehaviour` (`FirstPersonLookAim`) that reads the camera's smoothed
`AimPitch` and applies a per-frame *delta* local rotation to the head/neck/arm
bones of the humanoid rig. The delta-additive approach preserves the currently
playing animation's own Y/Z rotations and is frame-rate independent.

## Where it operates
- Attached to `Player/Body/Dummy` (the GameObject with the humanoid `Animator`
  and `Dummy.fbx` avatar).
- `LateUpdate` runs after the Animator so we override the pose after evaluation.
- Uses `HumanBodyBones` lookup maps so re-targeting works regardless of the
  exact bone node names.

## Bones & default weights (tunable in Inspector)
- Head: 1.0
- Neck: 0.7
- Chest / UpperChest: 0.3
- Shoulders: 0.5
- UpperArm L/R: 0.85
- LowerArm L/R: 0.3

## Pitch handling
- Read smoothed pitch from `FirstPersonCamera.AimPitch` (already public).
- Smooth the applied value with `Mathf.SmoothDamp` for responsive but non-jittery
  motion.
- Apply positive pitch (look up) as a rotation about each bone's local -X (so a
  positive AimPitch pitck corresponds to the camera rotation convention).
- Respect the camera's min/max pitch automatically since we consume `AimPitch`.

## Files
- New: `Assets/Scripts/FirstPersonController/FirstPersonLookAim.cs`
- The component references `FirstPersonCamera` (auto-found in parent rig) and the
  local `Animator`. No existing clip/controller changes.