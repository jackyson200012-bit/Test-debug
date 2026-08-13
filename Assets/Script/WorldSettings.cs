using UnityEngine;
using System.Collections.Generic;
using JayFos.Biomes;
using JayFos.Foliage;
using JayFos.Cloud;
using JayFos.Roads;
using JayFos.Environment;

namespace JayFos.World
{
    [CreateAssetMenu(fileName = "WorldSettings", menuName = "World/World Settings")]
    public class WorldSettings : ScriptableObject
    {
        [Header("World")]

        public int seed = 12345;

        [Header("Biome System")]

        public bool enableBiomeSystem = false;

        public BiomeDefinition[] biomes;

        public BiomeDefinition defaultBiome;

        [Range(0.001f, 0.1f)]
        public float biomeNoiseScale = 0.02f;

        [Range(0.001f, 0.1f)]
        public float biomeDetailNoiseScale = 0.05f;

        [Min(16)]
        public int chunkSize = 64;
        public Material terrainMaterial;

        [Min(2)]
        public int verticesPerLine = 65;

        [Header("Noise")]

        public float noiseScale = 0.03f;

        public float heightMultiplier = 15f;

        public int octaves = 4;

        [Range(0f, 1f)]
        public float persistence = 0.5f;

        public float lacunarity = 2f;

        public Vector2 noiseOffset;

        [Header("Streaming")]

        [Range(1, 10)]
        public int viewDistance = 5;

        public bool generateCollision;

        [Header("Terrain Texture")]

        public TerrainLayer groundLayer;

        [Header("Grass (Legacy - for backward compat)")]

        public GameObject grass01;
        public GameObject grass02;

        [Range(0f, 100f)]
        public int grassChance = 50;

        [Min(0f)]
        public float terrainHeightThreshold = 0f;


        [Header("Trees (Legacy - for backward compat)")]

        public GameObject fruitTree;

        [Min(0)]
        public int treesPerChunk = 200;

        [Range(1, 5)]
        public float foliageDensityMultiplier = 1.5f;

        [Range(0.0f, 1.0f)]
        public float spawnThreshold = 0.35f;

        public float hashOffset = 12345.789f;

        public float hashMultiplier = 1f;

        [Range(-50, 10)]
        public float waterLevel = -5f;

        [Header("Water")]

        [Tooltip("Shared material used for every water surface. Leave empty to skip water generation.")]
        public Material waterMaterial;

        [Range(0f, 0.5f)]
        public float waterWaveStrength = 0.06f;

        [Range(0f, 3f)]
        public float waterWaveSpeed = 0.8f;

        [Range(0f, 10f)]
        public float waterWaveFrequency = 1.2f;

        [Range(0f, 40f)]
        public float waterDepthMax = 10f;

        [Range(0f, 30f)]
        public float waterFoamDistance = 2.5f;

        [Header("Clouds & Weather")]
        public CloudSettings cloudSettings;
        public WeatherSettings weatherSettings;
        public bool enableClouds = true;

        [Header("Environment (Phase 2.8)")]
        public EnvironmentSettings environmentSettings;

        [Header("Roads")]
        public bool enableRoads = false;
        public RoadSettings roadSettings;

        [Header("Foliage Spawn Rules")]

        public FoliageSpawnRule[] spawnRules;

        private FoliageSpawnRule[] _cachedRules = null;

        public FoliageSpawnRule[] GetSpawnRules()
        {
            if (spawnRules != null && spawnRules.Length > 0)
                return spawnRules;

            if (_cachedRules != null)
                return _cachedRules;

            _cachedRules = AutoGenerateSpawnRules();
            return _cachedRules;
        }

        public BiomeMap CreateBiomeMap()
        {
            if (!enableBiomeSystem)
                return null;

            return new BiomeMap(
                worldSeed: seed,
                biomeNoiseScale: biomeNoiseScale,
                biomeDetailNoiseScale: biomeDetailNoiseScale,
                biomes: biomes,
                defaultBiome: defaultBiome
            );
        }

        private FoliageSpawnRule[] AutoGenerateSpawnRules()
        {
            var list = new List<FoliageSpawnRule>();

            if (fruitTree != null)
            {
                var treeRule = new FoliageSpawnRule
                {
                    name = "Auto-Generated Tree",
                    prefab = fruitTree,
                    minDensity = 0.35f,
                    maxDensity = 1f,
                    minHeight = terrainHeightThreshold,
                    maxHeight = -1f,
                    maxSlope = 45f,
                    noiseVariationMin = -1f,
                    noiseVariationMax = 1f
                };
                list.Add(treeRule);
            }

            if (grass01 != null)
            {
                var grassRule = new FoliageSpawnRule
                {
                    name = "Auto-Generated Grass",
                    prefab = grass01,
                    minDensity = 0f,
                    maxDensity = 1f,
                    minHeight = terrainHeightThreshold,
                    maxHeight = -1f,
                    maxSlope = 90f,
                    noiseVariationMin = -1f,
                    noiseVariationMax = 1f
                };
                list.Add(grassRule);
            }

            return list.ToArray();
        }
    }
}
