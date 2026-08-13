using UnityEngine;
using JayFos.Biomes;
using JayFos.Cloud;
using JayFos.Terrain;
using JayFos.Environment;

namespace JayFos.World
{
    public class WorldManager : MonoBehaviour
    {
        [SerializeField] private WorldSettings settings;
        [SerializeField] private Transform player;
        [SerializeField] private float updateInterval = 0.2f;

        private ChunkManager chunkManager;
        private TerrainGenerator terrainGenerator;
        private BiomeMap biomeMap;
        private CloudManager cloudManager;
        private WeatherSystem weatherSystem;

        private DayNightCycle dayNightCycle;
        private CloudShadowController cloudShadowController;
        private LightningManager lightningManager;
        private WindFoliageController windFoliageController;

        private float timer;
        private Vector2Int lastCenterCoord;

        private void Awake()
        {
            if (player == null)
            {
                player = GameObject.FindGameObjectWithTag("Player")?.transform;
            }

            terrainGenerator = new TerrainGenerator(settings);

            if (settings.enableBiomeSystem)
            {
                biomeMap = settings.CreateBiomeMap();
                if (biomeMap != null)
                {
                    terrainGenerator.SetBiomeMap(biomeMap);
                }
            }

            chunkManager = new ChunkManager(transform, settings, terrainGenerator);

            if (biomeMap != null)
            {
                chunkManager.SetBiomeMap(biomeMap);
            }

            if (settings.enableClouds && settings.cloudSettings != null)
            {
                try
                {
                    cloudManager = new CloudManager(settings.cloudSettings, settings.seed, transform);
                    cloudManager.SetCamera(Camera.main?.transform);

                    if (settings.weatherSettings != null)
                    {
                        weatherSystem = gameObject.AddComponent<WeatherSystem>();
                        weatherSystem.Initialize(cloudManager, biomeMap, settings.weatherSettings);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError("[WorldManager] Cloud/Weather init failed: " + e.Message);
                    cloudManager = null;
                    weatherSystem = null;
                }
            }

            InitializeEnvironmentSystems();
        }

        private void InitializeEnvironmentSystems()
        {
            if (settings == null || settings.environmentSettings == null)
                return;

            EnvironmentSettings env = settings.environmentSettings;

            if (weatherSystem != null)
            {
                weatherSystem.environmentSettings = env;
            }

            // Resolve a single sun light shared by DayNightCycle (rotation) and WeatherSystem (intensity).
            Light sunLight = FindOrCreateSunLight();

            if (env.enableDayNightCycle)
            {
                dayNightCycle = gameObject.AddComponent<DayNightCycle>();
                dayNightCycle.settings = env;
                dayNightCycle.sunLight = sunLight;

                if (weatherSystem != null)
                {
                    weatherSystem.dayNightCycle = dayNightCycle;
                    weatherSystem.SetMainLight(sunLight);
                }
            }
            else if (sunLight != null)
            {
                if (weatherSystem != null)
                    weatherSystem.SetMainLight(sunLight);
            }

            if (env.enableCloudShadows && cloudManager != null)
            {
                cloudShadowController = gameObject.AddComponent<CloudShadowController>();
                cloudShadowController.Initialize(cloudManager, dayNightCycle);
            }

            if (env.enableLightning)
            {
                lightningManager = gameObject.AddComponent<LightningManager>();
                lightningManager.weatherSystem = weatherSystem;
                lightningManager.environmentSettings = env;
            }

            if (env.enableSnow)
            {
                GameObject snowGO = new GameObject("SnowSystem");
                snowGO.transform.SetParent(transform, false);
                snowGO.transform.localPosition = Vector3.zero;

                SnowSystem snowSystemObj = snowGO.AddComponent<SnowSystem>();
                snowSystemObj.weatherSystem = weatherSystem;
                snowSystemObj.biomeMap = biomeMap;
                snowSystemObj.snowThreshold = env.snowThreshold;

                if (weatherSystem != null)
                    weatherSystem.SetSnowSystem(snowSystemObj);
            }

            if (env.enableWindFoliage)
            {
                windFoliageController = gameObject.AddComponent<WindFoliageController>();
                windFoliageController.weatherSystem = weatherSystem;
                windFoliageController.environmentSettings = env;
                windFoliageController.dayNightCycle = dayNightCycle;
            }
        }

        // Reuses the brightest existing directional light; creates a "Sun" only if none exists.
        private Light FindOrCreateSunLight()
        {
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
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

            if (best != null)
                return best;

            var sunGO = new GameObject("Sun");
            sunGO.transform.position = Vector3.zero;
            var sunLight = sunGO.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.intensity = 1f;
            sunLight.color = Color.white;
            return sunLight;
        }

        private void Start()
        {
            lastCenterCoord = GetPlayerChunkCoord();
            chunkManager.UpdateChunks(lastCenterCoord);
        }

        private void Update()
        {
            if (cloudManager != null)
            {
                if (Camera.main != null)
                    cloudManager.SetCamera(Camera.main.transform);
                cloudManager.Update(Time.deltaTime);
            }

            timer += Time.deltaTime;

            if (timer < updateInterval)
                return;

            timer = 0f;

            Vector2Int currentCoord = GetPlayerChunkCoord();

            if (currentCoord != lastCenterCoord)
            {
                lastCenterCoord = currentCoord;
                chunkManager.UpdateChunks(currentCoord);
            }
        }

        private Vector2Int GetPlayerChunkCoord()
        {
            if (player == null)
                return Vector2Int.zero;

            int x = Mathf.FloorToInt(player.position.x / settings.chunkSize);
            int z = Mathf.FloorToInt(player.position.z / settings.chunkSize);
            return new Vector2Int(x, z);
        }
    }
}
