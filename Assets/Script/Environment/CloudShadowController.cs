using UnityEngine;
using JayFos.Cloud;

namespace JayFos.Environment
{
    /// <summary>
    /// Uniform cloud-coverage darkening (NOT spatial shadow projection).
    /// A single _CloudShadowIntensity value derived from global cloud coverage is broadcast
    /// each frame so all receivers (water, terrain, clouds) darken uniformly.
    /// </summary>
    public class CloudShadowController : MonoBehaviour
    {
        [Header("References")]
        private CloudManager cloudManager;
        private DayNightCycle dayNightCycle;

        [Header("Shadow Settings")]
        [Tooltip("How much cloud coverage darkens surfaces (0-1).")]
        [Range(0f, 1f)]
        public float shadowIntensity = 0.4f;

        [Tooltip("Softness of the darkening (reserved; kept for shader parity).")]
        [Range(0f, 1f)]
        public float shadowSoftness = 0.3f;

        private readonly int cloudShadowId = Shader.PropertyToID("_CloudShadowIntensity");
        private readonly int shadowSoftnessId = Shader.PropertyToID("_ShadowSoftness");
        private readonly int daylightId = Shader.PropertyToID("_DaylightFactor");

        private float currentShadowIntensity;

        /// <summary>Current smoothed shadow intensity (0-1). Exposed for inspect/debug.</summary>
        public float CurrentShadowIntensity => currentShadowIntensity;

        /// <summary>
        /// Wired by WorldManager at startup. Uses a plain method (no scene reliance)
        /// so the controller has no serialized references.
        /// </summary>
        public void Initialize(CloudManager clouds, DayNightCycle cycle)
        {
            cloudManager = clouds;
            dayNightCycle = cycle;
        }

        private void Start()
        {
            currentShadowIntensity = 0f;
        }

        private void Update()
        {
            if (cloudManager == null)
                return;

            // Compute uniform coverage-based darkening, smoothed over time.
            float coverage = cloudManager.CurrentCoverage;
            float target = coverage * shadowIntensity;
            currentShadowIntensity = Mathf.Lerp(currentShadowIntensity, target, Time.deltaTime * 5f);
            if (currentShadowIntensity < 0.001f)
                currentShadowIntensity = 0f;

            // Broadcast uniform global properties (any shader reading them darkens uniformly).
            Shader.SetGlobalFloat(cloudShadowId, currentShadowIntensity);
            Shader.SetGlobalFloat(shadowSoftnessId, shadowSoftness);
            Shader.SetGlobalFloat(daylightId, dayNightCycle != null ? dayNightCycle.DaylightFactor : 1f);

            // Darken clouds uniformly via per-renderer MPB (no material instantiation, no allocation).
            if (cloudManager.Renderer != null)
                cloudManager.Renderer.SetCloudShadow(currentShadowIntensity);
        }
    }
}