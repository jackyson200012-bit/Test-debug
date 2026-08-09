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

        [SerializeField] private bool dbg;
        private CapsuleCollider capsule;
        private readonly RaycastHit[] results = new RaycastHit[4];
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
            // Start the probe sphere overlapping the capsule: SphereCast ignores
            // colliders overlapped at the start, so the player's own capsule is
            // excluded from the sweep (a sphere starting just above the bottom
            // would sweep INTO the capsule and report it as ground).
            Vector3 origin = bottom + Vector3.up * (radius * 0.5f);

            int hits = Physics.SphereCastNonAlloc(origin, radius, Vector3.down, results, checkDistance + radius * 0.5f, groundMask, QueryTriggerInteraction.Ignore);

            // The probe sphere overlaps the capsule's lower hemisphere, so the cast
            // can report the player's own collider. Skip any hit on our own object;
            // otherwise the detector reads the player as ground forever (even mid-air),
            // which feeds PlayerMotor's snap-down a self velocity and breaks jumping.
            RaycastHit? found = null;
            for (int i = 0; i < hits; i++)
            {
                if (results[i].collider.transform == transform) continue;
                found = results[i];
                break;
            }

            if (found == null)
            {
                TouchingGround = false;
                IsGrounded = false;
                OnSteepSlope = false;
                GroundNormal = Vector3.up;
                GroundPoint = bottom;
                GroundRigidbody = null;
                return;
            }

            RaycastHit ground = found.Value;
            TouchingGround = true;
            GroundNormal = ground.normal;
            GroundPoint = ground.point;
            GroundRigidbody = ground.rigidbody;
            SlopeAngle = Vector3.Angle(GroundNormal, Vector3.up);
            IsGrounded = SlopeAngle <= maxWalkableSlope;
            OnSteepSlope = !IsGrounded;
            if (dbg)
                Debug.Log($"[DIAG Ground] tms={Time.fixedTime} hit={ground.collider?.name} touching={TouchingGround} grounded={IsGrounded} steep={OnSteepSlope} ang={SlopeAngle:F1} n={GroundNormal} pt={GroundPoint}");
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
