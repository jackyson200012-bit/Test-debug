using UnityEngine;
using JayFos.Biomes;

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
        private float currentWindMultiplier;

        private CloudManager cloudManager;
        private BiomeMap biomeMap;
        private Light mainLight;

        public WeatherState CurrentState => currentState;
        public float CurrentCoverage => currentCoverage;
        public float CurrentFogDensity => currentFogDensity;
        public float CurrentRainIntensity => currentRainIntensity;
        public float CurrentWindMultiplier => currentWindMultiplier;

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

            RenderSettings.fogDensity = currentFogDensity;
            RenderSettings.fog = currentFogDensity > 0.001f;

            if (mainLight != null)
                mainLight.intensity = currentAmbientIntensity;
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
    }
}
