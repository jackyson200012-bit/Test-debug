using UnityEngine;

namespace JayFos.Environment
{
    /// <summary>
    /// Central configuration for all Phase 2.8 features (Day/Night Cycle, Cloud Shadows,
    /// Lightning & Thunder, Snow, Wind Foliage, Atmospheric Depth).
    /// Each feature can be independently toggled.
    /// </summary>
    [CreateAssetMenu(fileName = "EnvironmentSettings", menuName = "World/Environment Settings")]
    public class EnvironmentSettings : ScriptableObject
    {
        [Header("Day/Night Cycle")]
        public bool enableDayNightCycle = true;
        [Range(60f, 600f)]
        public float dayLength = 120f; // seconds for full day/night cycle
        public float sunElevationMin = -10f;
        public float sunElevationMax = 80f;

        [Header("Sky Colors (7-phase trilight)") ]
        public Color nightSkyColor = new Color(0.05f, 0.05f, 0.15f, 1f);
        public Color dawnHorizonColor = new Color(0.9f, 0.5f, 0.2f, 1f);
        public Color daySkyColor = new Color(0.3f, 0.6f, 0.95f, 1f);
        public Color sunsetHorizonColor = new Color(0.95f, 0.35f, 0.1f, 1f);
        public Color twilightSkyColor = new Color(0.15f, 0.15f, 0.4f, 1f);

        [Tooltip("Temperature curve evaluated at DayNightCycle.DayProgress (0-1). Returns 0-1 temperature.")]
        public AnimationCurve temperatureCurve = new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.25f, 0.3f), new Keyframe(0.5f, 1f), new Keyframe(0.75f, 0.5f), new Keyframe(1f, 0.2f));

        [Header("Cloud Shadows")]
        public bool enableCloudShadows = true;

        [Header("Lightning & Thunder")]
        public bool enableLightning = true;

        [Header("Snow")]
        public bool enableSnow = true;
        [Range(0f, 1f)]
        public float snowThreshold = 0.35f; // temperature ceiling; snow activates when effective temp < this

        [Header("Wind Foliage")]
        public bool enableWindFoliage = true;

        [Header("Atmospheric Depth")]
        public bool enableAtmosphericDepth = true;
        [Range(0f, 0.1f)]
        public float fogDensityBase = 0f;
        [Range(0f, 0.1f)]
        public float fogDensityMax = 0.03f;
    }
}
