using UnityEngine;
using JayFos.Biomes;
using JayFos.Environment;

namespace JayFos.Cloud
{
    public class WeatherSystem : MonoBehaviour
    {
        [SerializeField] private WeatherSettings weatherSettings;
        [SerializeField] private CloudSettings cloudSettings;
        [SerializeField] private float autoChangeInterval = 120f;

        private WeatherState currentState = WeatherState.Clear;
        private WeatherState targetState = WeatherState.Clear;
        private float transitionProgress = 1f;
        private float autoChangeTimer;

        private float currentCoverage;
        private float currentFogDensity;
        private float currentAmbientIntensity;
        private float currentRainIntensity;
        private float currentSnowIntensity;
        private float currentWindMultiplier;
        private float currentTemperature;
        private float currentDaylightFactor = 1f;

        private CloudManager cloudManager;
        private BiomeMap biomeMap;
        private Light mainLight;
        private SnowSystem snowSystem;

        // Phase 2.8 references (optional; null-safe when not assigned)
        public EnvironmentSettings environmentSettings;
        public DayNightCycle dayNightCycle;

        public WeatherState CurrentState => currentState;
        public float CurrentCoverage => currentCoverage;
        public float CurrentFogDensity => currentFogDensity;
        public float CurrentRainIntensity => currentRainIntensity;
        public float CurrentSnowIntensity => currentSnowIntensity;
        public float CurrentWindMultiplier => currentWindMultiplier;

        /// <summary>
        /// Normalized 0-1 wind force derived from the wind multiplier.
        /// 0 = calm (multiplier 1x), 1 = max wind (storm multiplier).
        /// </summary>
        public float CurrentWindForce
        {
            get
            {
                float maxMultiplier = weatherSettings != null ? weatherSettings.stormWindMultiplier : 2f;
                return Mathf.InverseLerp(1f, Mathf.Max(maxMultiplier, 1.001f), currentWindMultiplier);
            }
        }
        public float CurrentTemperature => currentTemperature;
        public float CurrentDaylightFactor => currentDaylightFactor;

        public void Initialize(CloudManager cloudManager, BiomeMap biomeMap, WeatherSettings weatherSettings)
        {
            this.cloudManager = cloudManager;
            this.biomeMap = biomeMap;
            this.weatherSettings = weatherSettings;
            mainLight = FindMainLight();

            ApplyStateInstant(currentState);
        }

        private void Update()
        {
            if (weatherSettings == null)
                return;

            UpdateTransition();
            UpdateAutoChange();
            ApplyToSystems();
        }

        public void SetWeather(WeatherState newState)
        {
            if (newState == targetState && transitionProgress >= 1f)
                return;

            targetState = newState;
            transitionProgress = 0f;
        }

        public void SetWeatherFromBiome(Vector3 worldPosition)
        {
            if (biomeMap == null)
                return;

            BiomeDefinition biome = biomeMap.GetBiome(worldPosition.x, worldPosition.z);
            if (biome == null)
                return;

            BiomeWeatherConfig weatherConfig = biome.weatherEnvironment as BiomeWeatherConfig;
            if (weatherConfig == null)
                return;

            float roll = Random.value;
            WeatherState newState = WeatherState.Clear;

            if (roll < weatherConfig.stormChance)
                newState = WeatherState.Storm;
            else if (roll < weatherConfig.stormChance + weatherConfig.rainChance)
                newState = WeatherState.Rain;
            else if (roll < weatherConfig.stormChance + weatherConfig.rainChance + weatherConfig.fogChance)
                newState = WeatherState.Fog;
            else
                newState = WeatherState.Cloudy;

            SetWeather(newState);
        }

        private void UpdateTransition()
        {
            if (transitionProgress >= 1f)
                return;

            transitionProgress += Time.deltaTime / weatherSettings.transitionDuration;
            transitionProgress = Mathf.Clamp01(transitionProgress);

            float t = transitionProgress * transitionProgress * (3f - 2f * transitionProgress);

            WeatherState fromState = currentState;
            WeatherState toState = targetState;

            currentCoverage = Mathf.Lerp(
                weatherSettings.GetTargetCoverage(fromState),
                weatherSettings.GetTargetCoverage(toState),
                t);

            currentFogDensity = Mathf.Lerp(
                weatherSettings.GetTargetFogDensity(fromState),
                weatherSettings.GetTargetFogDensity(toState),
                t);

            currentAmbientIntensity = Mathf.Lerp(
                weatherSettings.GetTargetAmbientIntensity(fromState),
                weatherSettings.GetTargetAmbientIntensity(toState),
                t);

            float fromRain = weatherSettings.GetRainIntensity(fromState);
            float toRain = weatherSettings.GetRainIntensity(toState);
            currentRainIntensity = Mathf.Lerp(fromRain, toRain, t);

            float fromWind = weatherSettings.GetWindMultiplier(fromState);
            float toWind = weatherSettings.GetWindMultiplier(toState);
            currentWindMultiplier = Mathf.Lerp(fromWind, toWind, t);

            if (transitionProgress >= 1f)
            {
                currentState = targetState;
            }
        }

        private void UpdateAutoChange()
        {
            autoChangeTimer += Time.deltaTime;
            if (autoChangeTimer >= autoChangeInterval)
            {
                autoChangeTimer = 0f;
                RandomWeatherChange();
            }
        }

        private void RandomWeatherChange()
        {
            float roll = Random.value;
            WeatherState newState;

            if (roll < 0.3f)
                newState = WeatherState.Clear;
            else if (roll < 0.6f)
                newState = WeatherState.Cloudy;
            else if (roll < 0.8f)
                newState = WeatherState.Rain;
            else if (roll < 0.9f)
                newState = WeatherState.Storm;
            else
                newState = WeatherState.Fog;

            SetWeather(newState);
        }

        private void ApplyToSystems()
        {
            if (cloudManager != null)
                cloudManager.SetCoverage(currentCoverage);

            // Phase 2.8: daylight factor from DayNightCycle (1.0 when not assigned -> preserves Phase 2.6 behavior)
            currentDaylightFactor = dayNightCycle != null ? dayNightCycle.DaylightFactor : 1f;

            // Phase 2.8: compute effective temperature (global curve -> biome override)
            float globalTemp = 0.5f;
            if (environmentSettings != null && dayNightCycle != null)
            {
                globalTemp = environmentSettings.temperatureCurve.Evaluate(dayNightCycle.DayProgress);
            }

            float effectiveTemp = globalTemp;
            if (biomeMap != null && Camera.main != null)
            {
                Vector3 camPos = Camera.main.transform.position;
                BiomeDefinition biome = biomeMap.GetBiome(camPos.x, camPos.z);
                if (biome != null)
                {
                    effectiveTemp = biome.temperature;
                }
            }

            currentTemperature = effectiveTemp;

            // Snow toggle: effective temp below threshold AND enabled
            bool shouldSnow = effectiveTemp < (environmentSettings != null ? environmentSettings.snowThreshold : 0.5f)
                && (environmentSettings != null && environmentSettings.enableSnow);

            if (snowSystem != null)
            {
                snowSystem.enabled = shouldSnow;
            }

            // Rain disabled during snow to prevent mixed precipitation artifacts.
            // When snow ends, restore the state-derived rain so suppression isn't permanent.
            if (shouldSnow)
            {
                currentRainIntensity = 0f;
            }
            else if (weatherSettings != null && transitionProgress >= 1f)
            {
                currentRainIntensity = weatherSettings.GetRainIntensity(targetState);
            }

            // Snow intensity: kept distinct from rain so SnowSystem can emit while rain is suppressed.
            // Proportional to the precip target of the current weather state.
            if (weatherSettings != null)
            {
                currentSnowIntensity = shouldSnow ? weatherSettings.GetRainIntensity(targetState) : 0f;
            }

            ApplyFog();

            if (mainLight != null)
                mainLight.intensity = currentAmbientIntensity * currentDaylightFactor;
        }

        // Phase 2.8: Atmospheric Depth — fog mode, weather-driven density, and time-of-day haze color.
        // Falls back to the Phase 2.6 behavior when enableAtmosphericDepth is disabled.
        private void ApplyFog()
        {
            if (environmentSettings != null && environmentSettings.enableAtmosphericDepth)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;

                // currentFogDensity is an absolute density (0..fogFogDensity). Normalize it
                // so Clear weather maps to fogDensityBase and Fog/Storm to fogDensityMax.
                float maxFog = weatherSettings != null ? weatherSettings.fogFogDensity : 0.03f;
                float t = Mathf.InverseLerp(0f, maxFog, currentFogDensity);
                RenderSettings.fogDensity = Mathf.Lerp(environmentSettings.fogDensityBase, environmentSettings.fogDensityMax, t);
                RenderSettings.fogColor = ComputeHazeColor(currentDaylightFactor);
            }
            else
            {
                // Phase 2.6 behavior (preserved): direct weather density, simple on/off toggle
                RenderSettings.fogDensity = currentFogDensity;
                RenderSettings.fog = currentFogDensity > 0.001f;
            }
        }

        private Color ComputeHazeColor(float daylightFactor)
        {
            Color hazeColor;
            if (daylightFactor > 0.5f)
            {
                // During day: neutral, slightly warm haze
                hazeColor = Color.Lerp(new Color(0.8f, 0.85f, 0.9f), new Color(0.9f, 0.92f, 0.88f), (daylightFactor - 0.5f) * 2f);
            }
            else
            {
                // During night/sunset: cool blue or warm orange haze
                hazeColor = Color.Lerp(new Color(0.1f, 0.12f, 0.2f), new Color(0.6f, 0.3f, 0.15f), daylightFactor * 2f);
            }
            return hazeColor;
        }

        private void ApplyStateInstant(WeatherState state)
        {
            currentCoverage = weatherSettings.GetTargetCoverage(state);
            currentFogDensity = weatherSettings.GetTargetFogDensity(state);
            currentAmbientIntensity = weatherSettings.GetTargetAmbientIntensity(state);
            currentRainIntensity = weatherSettings.GetRainIntensity(state);
            currentWindMultiplier = weatherSettings.GetWindMultiplier(state);
            transitionProgress = 1f;
        }

        private Light FindMainLight()
        {
            Light[] lights = FindObjectsOfType<Light>();
            Light best = null;
            float bestIntensity = float.MinValue;

            foreach (Light light in lights)
            {
                if (light.type == LightType.Directional && light.intensity > bestIntensity)
                {
                    best = light;
                    bestIntensity = light.intensity;
                }
            }

            return best;
        }

        /// <summary>
        /// Allows WorldManager to pin the exact Light shared with DayNightCycle,
        /// so rotation (DayNightCycle) and intensity (WeatherSystem) act on the same light.
        /// </summary>
        public void SetMainLight(Light light)
        {
            mainLight = light;
        }

        public void SetSnowSystem(SnowSystem snowSystem)
        {
            this.snowSystem = snowSystem;
        }
    }
}
