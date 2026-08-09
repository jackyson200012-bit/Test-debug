using UnityEngine;

namespace JayFos.Runtime
{
    /// <summary>Pitches a humanoid rig's upper body (chest/neck/head/shoulders/arms)
    /// up and down with the first-person camera on top of whatever animation is
    /// playing. Works for every Animator state without touching clips/controller.</summary>
    [DisallowMultipleComponent]
    public sealed class FirstPersonLookAim : MonoBehaviour
    {
        [Header("References (auto-resolved)")]
        [SerializeField] private Animator animator;
        [SerializeField] private FirstPersonCamera cameraRig;

        [Header("Smoothing")]
        [SerializeField, Min(0f), Tooltip("Exponential pitch smoothing time in seconds.")]
        private float smoothTime = 0.1f;

        [Header("Orientation")]
        [SerializeField, Tooltip("-1 inverts the pitch direction if the rig pitches backwards.")]
        private float pitchSign = 1f;

        [Header("Bone Weights (0..1)")]
        [SerializeField, Range(0f, 1f)] private float chestWeight = 0.25f;
        [SerializeField, Range(0f, 1f)] private float neckWeight = 0.7f;
        [SerializeField, Range(0f, 1f)] private float headWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float shoulderWeight = 0.5f;
        [SerializeField, Range(0f, 1f)] private float upperArmWeight = 0.85f;
        [SerializeField, Range(0f, 1f)] private float lowerArmWeight = 0.3f;

        [Header("Limits")]
        [SerializeField, Min(0f), Tooltip("Max degrees any bone may pitch up/down (applies to head and both arms).")]
        private float maxPitchDegrees = 45f;

        private Transform pelvis;
        private Transform chest, neck, head;
        private Transform shoulderL, shoulderR, upperArmL, upperArmR, lowerArmL, lowerArmR;

        private float currentSmoothed;   // last smoothed pitch written to bones
        private float pitchVelocity;

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (cameraRig == null) cameraRig = Object.FindAnyObjectByType<FirstPersonCamera>();
            CacheBones();
            currentSmoothed = cameraRig != null ? cameraRig.AimPitch : 0f;
        }

        private void LateUpdate()
        {
            if (cameraRig == null || animator == null) return;

            float target = cameraRig.AimPitch;
            currentSmoothed = Mathf.SmoothDamp(currentSmoothed, target, ref pitchVelocity, smoothTime);
            float offset = currentSmoothed * pitchSign; // degrees of pitch to compose this frame

            if (Mathf.Approximately(offset, 0f)) return;

            ApplyBone(chest,     offset * chestWeight);
            ApplyBone(neck,      offset * neckWeight);
            ApplyBone(head,      offset * headWeight);
            ApplyBone(shoulderL, offset * shoulderWeight);
            ApplyBone(shoulderR, offset * shoulderWeight);
            ApplyBone(upperArmL, offset * upperArmWeight);
            ApplyBone(upperArmR, offset * upperArmWeight);
            ApplyBone(lowerArmL, offset * lowerArmWeight);
            ApplyBone(lowerArmR, offset * lowerArmWeight);
        }

        private void ApplyBone(Transform bone, float degrees)
        {
            if (bone == null || bone == pelvis) return;
            // Clamp each bone's pitch (head and both arms) to the configured limit.
            degrees = Mathf.Clamp(degrees, -maxPitchDegrees, maxPitchDegrees);
            if (Mathf.Approximately(degrees, 0f)) return;
            // Pitch = rotate about the rig root's right (world +X, left-to-right).
            // Shoulder/arm bones have arbitrary local axes, so Euler around their local
            // X would roll/yaw them; map the root right into the bone's current local
            // frame instead so both arms always pitch up/down symmetrically.
            Vector3 axis = bone.InverseTransformDirection(transform.right);
            bone.localRotation = bone.localRotation * Quaternion.AngleAxis(degrees, axis);
        }

        private void CacheBones()
        {
            if (animator == null) return;
            pelvis      = animator.GetBoneTransform(HumanBodyBones.Hips);
            chest       = animator.GetBoneTransform(HumanBodyBones.Chest);
            neck        = animator.GetBoneTransform(HumanBodyBones.Neck);
            head        = animator.GetBoneTransform(HumanBodyBones.Head);
            shoulderL   = animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
            shoulderR   = animator.GetBoneTransform(HumanBodyBones.RightShoulder);
            upperArmL   = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            upperArmR   = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            lowerArmL   = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            lowerArmR   = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        }
    }
}