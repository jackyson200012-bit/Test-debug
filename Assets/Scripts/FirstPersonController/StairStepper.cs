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

        [SerializeField] private bool dbg;
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
            if (dbg) Debug.Log($"[DIAG Stair::enter] tms={Time.fixedTime} speed={horizontalSpeed:F2} (min={minSpeed}) movesqr={new Vector3(moveDirection.x, 0, moveDirection.z).sqrMagnitude:F3}");
            if (horizontalSpeed < minSpeed) { if (dbg) Debug.Log($"[DIAG Stair] REJECT speed<min (speed={horizontalSpeed:F2})"); return false; }

            Vector3 forward = moveDirection;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) { if (dbg) Debug.Log("[DIAG Stair] REJECT no forward dir"); return false; }
            forward.Normalize();

            float radius = capsule.radius * 0.9f;
            float sphereRadius = radius * 0.5f;
            // Recess the probe inside the capsule silhouette. The old radius*0.75
            // forward offset put the sphere's leading edge past the collider surface,
            // so it spawned already overlapping a flush riser. SphereCast then returned
            // a degenerate dist=0 hit (pt=(0,0,0)) whose phantom landing-cast missed the
            // tread, so TryStepUp always failed exactly when the player reached a step.
            float probeOffset = Mathf.Max(0f, capsule.radius - sphereRadius - 0.05f);
            Vector3 foot = new Vector3(body.transform.position.x, capsule.bounds.min.y, body.transform.position.z);
            Vector3 probeOrigin = foot + Vector3.up * maxStepHeight + forward * probeOffset;
            if (dbg) Debug.Log($"[DIAG Stair] foot={foot} probeOrigin={probeOrigin} capRadius={capsule.radius} maxStepH={maxStepHeight} footY(from bounds)={capsule.bounds.min.y} capY={capsule.bounds.center.y}");

            int probeHits = Physics.SphereCastNonAlloc(probeOrigin, sphereRadius, forward, hits, probeReach, mask, QueryTriggerInteraction.Ignore);
            if (probeHits <= 0) { if (dbg) Debug.Log($"[DIAG Stair] REJECT no probe hit (probeHits={probeHits}) reach={probeReach}"); return false; }
            if (dbg) Debug.Log($"[DIAG Stair] probe hit: collider={hits[0].collider?.name} pt={hits[0].point} n={hits[0].normal} dist={hits[0].distance:F2}");

            RaycastHit wall = hits[0];
            if (wall.point.y - foot.y > maxStepHeight + 0.05f) { if (dbg) Debug.Log($"[DIAG Stair] REJECT wall too tall (dh={(wall.point.y - foot.y):F2}>{(maxStepHeight + 0.05f):F2})"); return false; }

            // Confirm there is a landing surface on top of the ledge.
            Vector3 downOrigin = wall.point + forward * 0.02f + Vector3.up * (maxStepHeight + 0.05f);
            if (Physics.RaycastNonAlloc(downOrigin, Vector3.down, downHits, maxStepHeight + 0.15f, mask, QueryTriggerInteraction.Ignore) <= 0)
            { if (dbg) Debug.Log($"[DIAG Stair] REJECT no down/lip landing at {downOrigin}"); return false; }

            RaycastHit landing = downHits[0];
            float deltaY = landing.point.y - foot.y;
            if (deltaY < 0.01f || deltaY > maxStepHeight + 0.05f) { if (dbg) Debug.Log($"[DIAG Stair] REJECT landing delta {deltaY:F2} ({landing.point.y:F2}->foot {foot.y:F2})"); return false; }

            stepDelta = Vector3.up * deltaY;
            if (dbg) Debug.Log($"[DIAG Stair] >>> STEP delta={stepDelta}");
            return true;
        }
    }
}
