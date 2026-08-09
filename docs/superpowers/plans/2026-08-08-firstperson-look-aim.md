# FirstPersonLookAim Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the player's Dummy character head/neck/arms pitch up and down with the camera in every animation state, via a procedural component.

**Architecture:** A `FirstPersonLookAim` MonoBehaviour on `Player/Body/Dummy` reads `FirstPersonCamera.AimPitch`, smooths it, and applies weighted per-frame *delta* local rotations to the humanoid chest/neck/head/shoulder/arm bones in `LateUpdate`, preserving the underlying baked FBX pose every frame.

**Tech Stack:** Unity 6000.5.3f1 (URP), C#, Humanoid rig (`Dummy.fbx` avatar), existing `JayFos.Runtime` scripts.

## Global Constraints

- Namespace must be `JayFos.Runtime` (matches existing FirstPersonController scripts).
- Do NOT modify `PlayerTest.controller` or any clip in `Assets/Characters/Animations/`. Clips keep delivering pose; this component adds intent on top.
- Component must work in every Animator state (Idle, Walk, Run, Jump, Fall, CrouchIdle, CrouchWalk) with zero controller changes.
- Use `Animator.GetBoneTransform(HumanBodyBones)` — never hard-code bone GameObject names (rig is retargetable). Verified present in this engine.
- Preserve the animated pose: apply rotations as `bone.localRotation = bone.localRotation * pitchRot` (post-multiply, bone-local space) using only the *frame delta* of the smoothed pitch.
- Files must compile cleanly; verify via Unity console (no new errors).

---

### Task 1: Create FirstPersonLookAim component

**Files:**
- Create: `Assets/Scripts/FirstPersonController/FirstPersonLookAim.cs`

**Interfaces:**
- Consumes: `FirstPersonCamera.AimPitch` (`float` property, `double` `currentPitch` in `JayFos.Runtime`).
- Produces: `FirstPersonLookAim` MonoBehaviour. Auto-resolves `Animator` (same GO) and `FirstPersonCamera` (via `Object.FindAnyObjectByType<FirstPersonCamera>()`; the camera is in a sibling branch `Player/Body/CameraPivot/Main Camera`, NOT in the Dummy's parent chain). Serialized tuning: `smoothTime`, `pitchSign`, and one weight per bone group. Exposes no public API beyond Unity lifecycle.

> **API note (verified in engine):** `Object.FindFirstObjectByType` is deprecated here; use the non-deprecated `Object.FindAnyObjectByType<T>()`. `FirstPersonCamera.AimPitch` is a readonly `float` property.

- [x] **Step 1: Create the component**

Created: `Assets/Scripts/FirstPersonController/FirstPersonLookAim.cs` (`JayFos.Runtime`, the plan's `Step 1` code).

> **Implementation note (corrected from original draft):** the draft's per-frame *delta* accumulation is discarded by the Animator each frame because it fully rewrites bone transforms on every evaluation. The shipped component instead re-applies the **full smoothed offset** every frame via `bone.localRotation = bone.localRotation * Quaternion.Euler(offset, 0, 0)` in `LateUpdate` (post-multiply, bone-local). Tried `OnAnimatorIK` + `Animator.SetBoneLocalRotation` first, but the controller has no IK pass (layer IK Pass off) so it never fired and the pose did not compose.

- [x] **Step 2: Verify compile** — `read_console` after scripts refresh: no errors for `FirstPersonLookAim`.
- [x] **Step 3: Commit** — no git repo in this workspace; commit skipped.

---

### Task 2 — Wire component onto the rig + runtime verification

**Files:**
- Modify: `Player/Body/Dummy` (add `FirstPersonLookAim` component in scene)

**Interfaces:**
- Consumes: `FirstPersonLookAim` attached to same GameObject that has the humanoid `Animator`.

- [x] **Step 1: Add component on Dummy** — `manage_components(action=add, target=Dummy(-1654), component_type=FirstPersonLookAim)`. Auto-resolve confirmed at runtime: `animator`→Dummy's `Animator`, `cameraRig`→`Main Camera` (`Object.FindAnyObjectByType`; the camera is in the sibling `CameraPivot` branch, not the Dummy parent chain).

- [x] **Step 2: Compile check** — no errors after domain reload.

- [x] **Step 3: Play-mode smoke test** — entered play, drove camera pitch via reflection (`FirstPersonCamera.targetPitch/currentPitch`):
  - Look up (`pitch=-40`): head `X≈315.1` (~`-41°`), neck `≈336.9°` (~`-24°`, weight `0.7`), chest `≈350°` (`-10°`, weight `0.25`). ✓
  - Look down (`pitch=+40`): head `X≈35.1°` (`+39°`), chest `≈10°` (`+9°`). ✓
  - No sign flip needed (`pitchSign=1`).
  - Crouch state confirmed tracking (`headX≈315°, chestX≈350°` at `pitch=-40`). Walk state confirmed earlier at `pitch=-40` (`headX≈315.6°`).
  - Console clear of errors/warnings during live play for the component.

- [x] **Step 4: Scene clean** — `PlayerTest.controller` and `Assets/Characters/Animations/` untouched; only `FirstPersonLookAim` added to `Dummy`. (Pre-existing console error `The referenced script (Unknown) on this Behaviour is missing!` belongs to `GameWorld`'s null component, unrelated.)

---

### Task 3 — Finalize

- [x] **Step 1: Final console check** — no errors/warnings introduced by `FirstPersonLookAim`; only the pre-existing `GameWorld` missing-script message remains.
- [ ] **Step 2: Report to user** — done in session summary (below).