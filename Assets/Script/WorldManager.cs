using UnityEngine;
using JayFos.Biomes;
using JayFos.Cloud;
using JayFos.Terrain;

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
