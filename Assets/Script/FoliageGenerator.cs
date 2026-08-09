using System.Collections.Generic;
using UnityEngine;
using JayFos.Biomes;
using JayFos.Terrain;
using JayFos.World;
using JayFos.Roads;

namespace JayFos.Foliage
{
    public class FoliageGenerator
    {
        public struct SpawnStatistics
        {
            public int totalAttempts;
            public int rejectedWaterLevel;
            public int rejectedHeightThreshold;
            public int rejectedNoiseThreshold;
            public int rejectedSlope;
            public int rejectedRuleMatch;
            public int rejectedPatternAvoidance;
            public int successfulSpawns;

            public void Reset()
            {
                totalAttempts = 0;
                rejectedWaterLevel = 0;
                rejectedHeightThreshold = 0;
                rejectedNoiseThreshold = 0;
                rejectedSlope = 0;
                rejectedRuleMatch = 0;
                rejectedPatternAvoidance = 0;
                successfulSpawns = 0;
            }

            public override string ToString()
            {
                return $"[FoliageSpawnStats] Attempts: {totalAttempts} | " +
                      $"Rejected - Water: {rejectedWaterLevel}, Height: {rejectedHeightThreshold}, Noise: {rejectedNoiseThreshold}, " +
                      $"Slope: {rejectedSlope}, RuleMatch: {rejectedRuleMatch}, Pattern: {rejectedPatternAvoidance} | " +
                      $"Spawned: {successfulSpawns}";
            }
        }

        private readonly WorldSettings settings;
        private readonly NoiseGenerator noiseGenerator;
        private BiomeMap biomeMap;
        private SpawnStatistics stats;

        // Reusable buffers to avoid per-chunk allocations
        private readonly float[] _secondaryNoiseBuffer = new float[16 * 16];
        private readonly float[] _tertiaryNoiseBuffer = new float[16 * 16];

        public FoliageGenerator(WorldSettings settings)
        {
            this.settings = settings;
            this.noiseGenerator = new NoiseGenerator(settings);
            stats = new SpawnStatistics();
        }

        public void SetBiomeMap(BiomeMap biomeMap)
        {
            this.biomeMap = biomeMap;
        }

        public SpawnStatistics GetStats() => stats;

        public void ResetStats() => stats.Reset();

        public List<PlacementPoint> Generate(Vector2Int chunkCoord, HeightMap heightMap, RoadFieldGrid roadGrid = null)
        {
            return GenerateInternal(chunkCoord, heightMap, null, roadGrid);
        }

        public List<PlacementPoint> Generate(Vector2Int chunkCoord, HeightMap heightMap, List<PlacementPoint> output, RoadFieldGrid roadGrid = null)
        {
            return GenerateInternal(chunkCoord, heightMap, output, roadGrid);
        }

        private List<PlacementPoint> GenerateInternal(Vector2Int chunkCoord, HeightMap heightMap, List<PlacementPoint> output, RoadFieldGrid roadGrid)
        {
            stats.Reset();

            var placementPoints = output ?? new List<PlacementPoint>();

            FoliageSpawnRule[] rules = settings.GetSpawnRules();
            if (rules == null || rules.Length == 0) return placementPoints;

            int chunkX = chunkCoord.x;
            int chunkZ = chunkCoord.y;

            float minX = (float)chunkX * settings.chunkSize;
            float minZ = (float)chunkZ * settings.chunkSize;

            int samplesPerChunk = Mathf.Max(1, settings.chunkSize / 4);

            BiomeDefinition chunkBiome = null;
            if (biomeMap != null && settings.enableBiomeSystem)
            {
                chunkBiome = biomeMap.GetBiomeAtChunkCenter(chunkX, chunkZ, settings.chunkSize);
            }

            FoliageConfig biomeFoliageConfig = null;
            if (chunkBiome != null)
            {
                biomeFoliageConfig = chunkBiome.foliageConfig as FoliageConfig;
            }

            float effectiveSecondaryOffsetX = settings.noiseOffset.x;
            float effectiveSecondaryOffsetY = settings.noiseOffset.y;
            int secondaryOctaves = settings.octaves;
            float secondaryPersistence = settings.persistence;
            float secondaryLacunarity = settings.lacunarity;

            if (biomeFoliageConfig != null && biomeFoliageConfig.HasAnyOverrides)
            {
                effectiveSecondaryOffsetX = biomeFoliageConfig.noiseOffset.x;
                effectiveSecondaryOffsetY = biomeFoliageConfig.noiseOffset.y;
                if (biomeFoliageConfig.octaves > 0) secondaryOctaves = biomeFoliageConfig.octaves;
                if (biomeFoliageConfig.persistence > 0f) secondaryPersistence = biomeFoliageConfig.persistence;
                if (biomeFoliageConfig.lacunarity > 0f) secondaryLacunarity = biomeFoliageConfig.lacunarity;
            }

            int sampleIndex = 0;
            for (int i = 0; i < samplesPerChunk; i++)
            {
                float worldX = minX + (float)i * settings.chunkSize / samplesPerChunk;

                for (int j = 0; j < samplesPerChunk; j++)
                {
                    stats.totalAttempts++;

                    float worldZ = minZ + (float)j * settings.chunkSize / samplesPerChunk;

                    float noiseValue = noiseGenerator.SampleWorldSpaceNoise(worldX, worldZ, chunkBiome, biomeFoliageConfig);

                    var hAndSlope = heightMap.GetHeightAndSlope(worldX, worldZ);
                    float terrainHeight = hAndSlope.height;

                    if (terrainHeight < settings.waterLevel)
                    {
                        stats.rejectedWaterLevel++;
                        continue;
                    }

                    if (settings.terrainHeightThreshold > 0f && terrainHeight < settings.terrainHeightThreshold)
                    {
                        stats.rejectedHeightThreshold++;
                        continue;
                    }

                    float effectiveDensityMultiplier = settings.foliageDensityMultiplier;
                    float effectiveSpawnThreshold = settings.spawnThreshold;
                    float effectiveHeightMultiplier = settings.heightMultiplier;

                    if (biomeFoliageConfig != null && biomeFoliageConfig.HasAnyOverrides)
                    {
                        if (biomeFoliageConfig.densityPerUnitArea > 0f)
                            effectiveDensityMultiplier = biomeFoliageConfig.densityPerUnitArea;
                        if (biomeFoliageConfig.noiseThreshold > 0f)
                            effectiveSpawnThreshold = biomeFoliageConfig.noiseThreshold;
                        if (biomeFoliageConfig.heightMultiplier > 0f)
                            effectiveHeightMultiplier = biomeFoliageConfig.heightMultiplier;
                    }

                    float adjustedNoise = noiseValue * effectiveDensityMultiplier + 0.5f;

                    if (adjustedNoise < effectiveSpawnThreshold)
                    {
                        stats.rejectedNoiseThreshold++;
                        continue;
                    }

                    if (roadGrid != null && settings.enableRoads && settings.roadSettings != null)
                    {
                        float roadInfluence = roadGrid.Sample(worldX, worldZ);
                        if (roadInfluence > settings.roadSettings.roadThreshold)
                        {
                            stats.rejectedNoiseThreshold++;
                            continue;
                        }
                    }

                    float slopeDegrees = hAndSlope.slopeDegrees;

                    bool slopeRejected = false;
                    for (int r = 0; r < rules.Length; r++)
                    {
                        if (rules[r].prefab == null) continue;
                        if (slopeDegrees > rules[r].maxSlope)
                        {
                            slopeRejected = true;
                            break;
                        }
                    }
                    if (slopeRejected)
                    {
                        stats.rejectedSlope++;
                        continue;
                    }

                    float secondaryNoise = noiseGenerator.SampleWorldSpaceNoiseWithParams(
                        worldX * 0.7f + effectiveSecondaryOffsetX,
                        worldZ * 0.7f + effectiveSecondaryOffsetY, chunkBiome,
                        secondaryOctaves, secondaryPersistence, secondaryLacunarity) * 2.0f - 1.0f;

                    float tertiaryNoise = noiseGenerator.SampleWorldSpaceNoiseWithParams(
                        worldX * 1.5f + effectiveSecondaryOffsetX * 2f,
                        worldZ * 1.5f - effectiveSecondaryOffsetY * 2f, chunkBiome,
                        secondaryOctaves, secondaryPersistence, secondaryLacunarity) * 2.0f - 1.0f;

                    int matchedRuleIndex = -1;

                    float typeAdjustedDensity = adjustedNoise + tertiaryNoise * 0.15f;

                    string biomeFilter = (chunkBiome != null) ? chunkBiome.biomeName : "";

                    for (int ruleIdx = 0; ruleIdx < rules.Length; ruleIdx++)
                    {
                        FoliageSpawnRule rule = rules[ruleIdx];

                        if (rule.prefab == null) continue;

                        if (rule.Matches(terrainHeight, slopeDegrees, typeAdjustedDensity, secondaryNoise, biomeFilter))
                        {
                            matchedRuleIndex = ruleIdx;
                            break;
                        }
                    }

                    if (matchedRuleIndex < 0)
                    {
                        stats.rejectedRuleMatch++;
                        continue;
                    }

                    int hashValue = noiseGenerator.Hash(worldX * 0.5f + worldZ * 0.3f);

                    if (Mathf.Abs(hashValue % 6) == 0)
                    {
                        stats.rejectedPatternAvoidance++;
                        continue;
                    }

                    stats.successfulSpawns++;

                    placementPoints.Add(new PlacementPoint
                    {
                        WorldPosition = new Vector3(worldX, terrainHeight + 1f, worldZ),
                        ChunkPosition = new Vector2Int(
                            (int)((worldX - minX) / settings.chunkSize * settings.verticesPerLine),
                            (int)((worldZ - minZ) / settings.chunkSize * settings.verticesPerLine)
                        ),
                        FoliageType = matchedRuleIndex,
                        DensityWeight = adjustedNoise,
                        RandomSeed = hashValue,
                        SlopeDegrees = slopeDegrees,
                        TerrainHeight = terrainHeight
                    });

                    sampleIndex++;
                }
            }

            return placementPoints;
        }

        public struct PlacementPoint
        {
            public Vector3 WorldPosition;
            public Vector2Int ChunkPosition;
            public int FoliageType;
            public float DensityWeight;
            public int RandomSeed;
            public float SlopeDegrees;
            public float TerrainHeight;

            public Vector3 ToWorldPosition() => WorldPosition;

            public bool IsValid()
            {
                if ((WorldPosition.x == 0f && WorldPosition.y == 0f && WorldPosition.z == 0f) ||
                    Vector3.Distance(WorldPosition, Vector3.zero) < 0.01f)
                {
                    return false;
                }
                return true;
            }
        }
    }
}
