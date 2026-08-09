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