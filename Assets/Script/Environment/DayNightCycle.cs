using UnityEngine;
using Unity.Profiling;

namespace JayFos.Environment
{
    /// <summary>
    /// Day/Night Cycle: 180° east → south → west sun trajectory with sinusoidal elevation.
    /// Sets RenderSettings sky colors (trilight), DaylightFactor, and SunDirection each frame.
    /// </summary>
    public class DayNightCycle : MonoBehaviour
    {
        [Header("References")]
        public EnvironmentSettings settings;
        public Light sunLight; // Optional: assign a directional light as the sun

        [Header("State")]
        public float DayProgress { get; private set; }
        public float DaylightFactor { get; private set; }
        public Vector3 SunDirection { get; private set; }

        private float elapsedTime;

        // ProfilerMarker for performance baseline measurement
        private static readonly ProfilerMarker s_ProfilerMarker = new ProfilerMarker("DayNightCycle.Update");

        private readonly int ambientSkyId = Shader.PropertyToID("_AmbientSkyColor");
        private readonly int ambientEqId = Shader.PropertyToID("_AmbientEquatorColor");
        private readonly int ambientGroundId = Shader.PropertyToID("_AmbientGroundColor");

        private void Update()
        {
            using (s_ProfilerMarker.Auto())
            {
                if (settings == null)
                    return;

                UpdateDayProgress();
                UpdateAzimuthAndElevation();
                UpdateSunDirection();
                UpdateSkyColors();
                UpdateDaylightFactor();
                BroadcastSkyColors();

                if (sunLight != null)
                {
                    sunLight.transform.rotation = Quaternion.LookRotation(SunDirection);
                    // Intensity is owned by WeatherSystem (applies ambient * DaylightFactor),
                    // so DayNightCycle only controls the light's orientation.
                }
            }
        }

        private void UpdateDayProgress()
        {
            elapsedTime += Time.deltaTime;
            DayProgress = (elapsedTime % settings.dayLength) / settings.dayLength;
        }

        private void UpdateAzimuthAndElevation()
        {
            // Azimuth: 90° (east, sunrise) → 180° (south/zenith) → 270° (west, sunset)
            float azimuth = Mathf.LerpUnclamped(90f, 270f, DayProgress);

            // Elevation: sinusoidal arc — min at sunrise/set, max at noon
            float elevation = Mathf.LerpUnclamped(
                settings.sunElevationMin,
                settings.sunElevationMax,
                Mathf.Sin(DayProgress * Mathf.PI));
        }

        private void UpdateSunDirection()
        {
            // Recompute from dayProgress using the same formula as azimuth/elevation
            float azimuth = Mathf.LerpUnclamped(90f, 270f, DayProgress);
            float elevation = Mathf.LerpUnclamped(
                settings.sunElevationMin,
                settings.sunElevationMax,
                Mathf.Sin(DayProgress * Mathf.PI));

            float azimuthRad = azimuth * Mathf.Deg2Rad;
            float elevRad = elevation * Mathf.Deg2Rad;

            // Standard spherical-to-Cartesian: X = cos(az)*cos(el), Y = sin(el), Z = sin(az)*cos(el)
            SunDirection = new Vector3(
                Mathf.Cos(azimuthRad) * Mathf.Cos(elevRad),
                Mathf.Sin(elevRad),
                Mathf.Sin(azimuthRad) * Mathf.Cos(elevRad)
            ).normalized;
        }

        private void UpdateSkyColors()
        {
            // 7-phase trilight: each phase uses t = (dayProgress - start) / (end - start)
            float t = 0f;
            Color skyColor;
            Color equatorColor;
            Color groundColor;

            if (DayProgress < 0.15f) // Night
            {
                skyColor = settings.nightSkyColor;
                equatorColor = settings.nightSkyColor;
                groundColor = settings.nightSkyColor;
            }
            else if (DayProgress < 0.25f) // Dawn
            {
                t = (DayProgress - 0.15f) / 0.1f;
                skyColor = Color.Lerp(settings.nightSkyColor, settings.dawnHorizonColor, t);
                equatorColor = settings.daySkyColor;
                groundColor = settings.nightSkyColor;
            }
            else if (DayProgress < 0.35f) // Sunrise
            {
                t = (DayProgress - 0.25f) / 0.1f;
                skyColor = Color.Lerp(settings.dawnHorizonColor, settings.daySkyColor, t);
                equatorColor = settings.daySkyColor;
                groundColor = settings.dawnHorizonColor;
            }
            else if (DayProgress < 0.65f) // Daytime
            {
                skyColor = settings.daySkyColor;
                equatorColor = settings.daySkyColor;
                groundColor = settings.daySkyColor;
            }
            else if (DayProgress < 0.75f) // Sunset
            {
                t = (DayProgress - 0.65f) / 0.1f;
                skyColor = Color.Lerp(settings.daySkyColor, settings.sunsetHorizonColor, t);
                equatorColor = settings.daySkyColor;
                groundColor = settings.sunsetHorizonColor;
            }
            else if (DayProgress < 0.85f) // Twilight
            {
                t = (DayProgress - 0.75f) / 0.1f;
                skyColor = Color.Lerp(settings.sunsetHorizonColor, settings.twilightSkyColor, t);
                equatorColor = settings.twilightSkyColor;
                groundColor = settings.twilightSkyColor;
            }
            else // Night
            {
                t = (DayProgress - 0.85f) / 0.15f;
                skyColor = Color.Lerp(settings.twilightSkyColor, settings.nightSkyColor, t);
                equatorColor = settings.twilightSkyColor;
                groundColor = settings.twilightSkyColor;
            }

            RenderSettings.ambientSkyColor = skyColor;
            RenderSettings.ambientEquatorColor = equatorColor;
            RenderSettings.ambientGroundColor = groundColor;
        }

        private void UpdateDaylightFactor()
        {
            // 7-phase trilight daylight factor (0 = dark night, 1 = full daylight)
            if (DayProgress < 0.15f) // Night
            {
                DaylightFactor = 0.05f;
            }
            else if (DayProgress < 0.25f) // Dawn
            {
                float t = (DayProgress - 0.15f) / 0.1f;
                DaylightFactor = Mathf.Lerp(0.05f, 0.3f, t);
            }
            else if (DayProgress < 0.35f) // Sunrise
            {
                float t = (DayProgress - 0.25f) / 0.1f;
                DaylightFactor = Mathf.Lerp(0.3f, 0.8f, t);
            }
            else if (DayProgress < 0.65f) // Daytime
            {
                DaylightFactor = 1f;
            }
            else if (DayProgress < 0.75f) // Sunset
            {
                float t = (DayProgress - 0.65f) / 0.1f;
                DaylightFactor = Mathf.Lerp(0.8f, 0.2f, t);
            }
            else if (DayProgress < 0.85f) // Twilight
            {
                float t = (DayProgress - 0.75f) / 0.1f;
                DaylightFactor = Mathf.Lerp(0.2f, 0.05f, t);
            }
            else // Night
            {
                DaylightFactor = 0.05f;
            }
        }

        private void BroadcastSkyColors()
        {
            // Set global shader properties for sky colors (available to any shader that samples them)
            Color sky = RenderSettings.ambientSkyColor;
            Color eq = RenderSettings.ambientEquatorColor;
            Color ground = RenderSettings.ambientGroundColor;

            Shader.SetGlobalFloat(ambientSkyId, sky.r);
            Shader.SetGlobalFloat(ambientEqId, eq.r);
            Shader.SetGlobalFloat(ambientGroundId, ground.r);
        }
    }
}
