using UnityEngine;

namespace JayFos.Cloud
{
    public enum WeatherState
    {
        Clear = 0,
        Cloudy = 1,
        Rain = 2,
        Storm = 3,
        Fog = 4
    }

    [CreateAssetMenu(fileName = "WeatherSettings", menuName = "World/Weather Settings")]
    public class WeatherSettings : ScriptableObject
    {
        [Header("Transition")]
        [Range(1f, 120f)]
        public float transitionDuration = 10f;

        [Header("Clear")]
        [Range(0f, 1f)]
        public float clearCloudCoverage = 0.15f;
        public float clearFogDensity = 0f;
        public float clearAmbientIntensity = 1f;

        [Header("Cloudy")]
        [Range(0f, 1f)]
        public float cloudyCloudCoverage = 0.65f;
        public float cloudyFogDensity = 0.002f;
        public float cloudyAmbientIntensity = 0.85f;

        [Header("Rain")]
        [Range(0f, 1f)]
        public float rainCloudCoverage = 0.8f;
        public float rainFogDensity = 0.005f;
        public float rainAmbientIntensity = 0.65f;
        [Range(0f, 1f)]
        public float rainIntensity = 0.7f;
        public float rainSpeed = 15f;

        [Header("Storm")]
        [Range(0f, 1f)]
        public float stormCloudCoverage = 0.95f;
        public float stormFogDensity = 0.01f;
        public float stormAmbientIntensity = 0.4f;
        [Range(0f, 1f)]
        public float stormRainIntensity = 1f;
        public float stormRainSpeed = 25f;
        public float stormWindMultiplier = 2f;

        [Header("Fog")]
        [Range(0f, 1f)]
        public float fogCloudCoverage = 0.3f;
        public float fogFogDensity = 0.03f;
        public float fogAmbientIntensity = 0.7f;

        public float GetTargetCoverage(WeatherState state)
        {
            switch (state)
            {
                case WeatherState.Clear: return clearCloudCoverage;
                case WeatherState.Cloudy: return cloudyCloudCoverage;
                case WeatherState.Rain: return rainCloudCoverage;
                case WeatherState.Storm: return stormCloudCoverage;
                case WeatherState.Fog: return fogCloudCoverage;
                default: return clearCloudCoverage;
            }
        }

        public float GetTargetFogDensity(WeatherState state)
        {
            switch (state)
            {
                case WeatherState.Clear: return clearFogDensity;
                case WeatherState.Cloudy: return cloudyFogDensity;
                case WeatherState.Rain: return rainFogDensity;
                case WeatherState.Storm: return stormFogDensity;
                case WeatherState.Fog: return fogFogDensity;
                default: return clearFogDensity;
            }
        }

        public float GetTargetAmbientIntensity(WeatherState state)
        {
            switch (state)
            {
                case WeatherState.Clear: return clearAmbientIntensity;
                case WeatherState.Cloudy: return cloudyAmbientIntensity;
                case WeatherState.Rain: return rainAmbientIntensity;
                case WeatherState.Storm: return stormAmbientIntensity;
                case WeatherState.Fog: return fogAmbientIntensity;
                default: return clearAmbientIntensity;
            }
        }

        public float GetRainIntensity(WeatherState state)
        {
            switch (state)
            {
                case WeatherState.Rain: return rainIntensity;
                case WeatherState.Storm: return stormRainIntensity;
                default: return 0f;
            }
        }

        public float GetWindMultiplier(WeatherState state)
        {
            if (state == WeatherState.Storm) return stormWindMultiplier;
            return 1f;
        }
    }
}
