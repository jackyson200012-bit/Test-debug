using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using JayFos.Cloud;
using JayFos.Foliage;

namespace JayFos.Environment
{
    /// <summary>
    /// Applies wind-driven sway to registered foliage renderers via shader properties.
    /// Uses a single cached MaterialPropertyBlock (allocated in Awake) and reads live
    /// foliage from FoliageRendererRegistry — no per-frame hierarchy scanning.
    /// </summary>
    public class WindFoliageController : MonoBehaviour
    {
        [Header("References")]
        public WeatherSystem weatherSystem;
        public EnvironmentSettings environmentSettings;
        public DayNightCycle dayNightCycle;

        [Header("Wind Settings")]
        [Tooltip("Base wind speed (how fast the sway oscillates).")]
        [Range(0.5f, 5f)]
        public float windSpeed = 1.5f;
        [Tooltip("Maximum sway amplitude at wind strength 1.0.")]
        [Range(0f, 2f)]
        public float maxSway = 0.5f;
        [Tooltip("Height at which sway tapers to zero (base of foliage).")]
        [Range(0f, 5f)]
        public float taperHeight = 1f;
        [Tooltip("Wind direction as a horizontal XY vector.")]
        public Vector2 windDirection = new Vector2(1f, 0f);

        private static readonly ProfilerMarker s_ProfilerMarker = new ProfilerMarker("WindFoliageController.Update");

        private readonly int windStrengthId = Shader.PropertyToID("_WindStrength");
        private readonly int windSpeedId = Shader.PropertyToID("_WindSpeed");
        private readonly int windDirectionId = Shader.PropertyToID("_WindDirection");

        // Cached MaterialPropertyBlock — allocated once in Awake, reused every frame (zero GC alloc at runtime).
        private MaterialPropertyBlock mbCache;

        // Reusable buffer to avoid per-frame List allocations when copying from the registry.
        private readonly List<MeshRenderer> foliageRenderers = new List<MeshRenderer>(256);

        private void Awake()
        {
            mbCache = new MaterialPropertyBlock();
        }

        private void Update()
        {
            using (s_ProfilerMarker.Auto())
            {
                if (weatherSystem == null || environmentSettings == null)
                    return;

                float windForce = weatherSystem.CurrentWindForce;
                float strength = windForce * maxSway;
                Vector4 windDir = new Vector4(windDirection.x, windDirection.y, 0f, 0f);

                // Global shader properties — any shader sampling these reacts to wind.
                Shader.SetGlobalFloat(windStrengthId, strength);
                Shader.SetGlobalFloat(windSpeedId, windSpeed);
                Shader.SetGlobalVector(windDirectionId, windDir);

                // Per-renderer overrides via one cached MaterialPropertyBlock (reused, zero alloc).
                FoliageRendererRegistry.CopyTo(foliageRenderers);

                for (int i = 0; i < foliageRenderers.Count; i++)
                {
                    MeshRenderer renderer = foliageRenderers[i];
                    if (renderer == null)
                        continue;

                    mbCache.SetFloat(windStrengthId, strength);
                    mbCache.SetFloat(windSpeedId, windSpeed);
                    mbCache.SetVector(windDirectionId, windDir);
                    renderer.SetPropertyBlock(mbCache);
                }
            }
        }
    }
}