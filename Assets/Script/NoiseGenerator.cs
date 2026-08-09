using UnityEngine;
using JayFos.Foliage;

namespace JayFos.Terrain
{
    public class NoiseGenerator
    {
        private readonly JayFos.World.WorldSettings settings;
        
        public NoiseGenerator(JayFos.World.WorldSettings settings)
        {
            this.settings = settings;
        }

        public static float ComputeSeedOffset(float worldCoord, int seed)
        {
            return worldCoord + (float)seed * 0.1f;
        }

        public HeightMap GenerateHeightMap(Vector2Int chunkCoord, JayFos.Biomes.BiomeDefinition biomeOverride = null)
        {
            int vertsPerLine = settings.verticesPerLine;
            int chunkSize = settings.chunkSize;
            float vertexSpacing = (float)chunkSize / (vertsPerLine - 1);
            
            float effectiveNoiseScale = settings.noiseScale;
            float effectiveHeightMultiplier = settings.heightMultiplier;
            int effectiveOctaves = settings.octaves;
            float effectivePersistence = settings.persistence;
            float effectiveLacunarity = settings.lacunarity;

            if (biomeOverride != null)
            {
                var terrainParams = biomeOverride.terrainParams as JayFos.Biomes.BiomeTerrainParams;
                if (terrainParams != null && terrainParams.HasTerrainOverrides)
                {
                    if (terrainParams.noiseScale > 0f) effectiveNoiseScale = terrainParams.noiseScale;
                    if (terrainParams.heightMultiplier > 0f) effectiveHeightMultiplier = terrainParams.heightMultiplier;
                    if (terrainParams.octaves > 0) effectiveOctaves = terrainParams.octaves;
                    if (terrainParams.persistence > 0f) effectivePersistence = terrainParams.persistence;
                    if (terrainParams.lacunarity > 0f) effectiveLacunarity = terrainParams.lacunarity;
                }
            }
            
            float[,] heights = new float[vertsPerLine, vertsPerLine];
            
            float minHeight = float.MaxValue;
            float maxHeight = float.MinValue;

            for (int z = 0; z < vertsPerLine; z++)
            {
                for (int x = 0; x < vertsPerLine; x++)
                {
                    float worldX = chunkCoord.x * chunkSize + x * vertexSpacing;
                    float worldZ = chunkCoord.y * chunkSize + z * vertexSpacing;

                    float seededWorldX = ComputeSeedOffset(worldX, settings.seed);
                    float seededWorldZ = ComputeSeedOffset(worldZ, settings.seed);
                    
                    float height = SampleNoise(seededWorldX, seededWorldZ,
                        effectiveNoiseScale, effectiveHeightMultiplier,
                        effectiveOctaves, effectivePersistence, effectiveLacunarity);
                    heights[x, z] = height;

                    if (height < minHeight) minHeight = height;
                    if (height > maxHeight) maxHeight = height;
                }
            }

            Vector2Int chunkOrigin = new Vector2Int(
                (int)(chunkCoord.x * chunkSize / vertexSpacing),
                (int)(chunkCoord.y * chunkSize / vertexSpacing)
            );

            HeightMap map = new HeightMap(heights, chunkOrigin, minHeight, maxHeight);
            map.vertexSpacing = vertexSpacing;
            return map;
        }

        public float SampleWorldSpaceNoise(float worldX, float worldZ, JayFos.Biomes.BiomeDefinition biomeOverride = null)
        {
            return SampleWorldSpaceNoise(worldX, worldZ, biomeOverride, null);
        }

        public float SampleWorldSpaceNoise(float worldX, float worldZ, JayFos.Biomes.BiomeDefinition biomeOverride, JayFos.Foliage.FoliageConfig foliageConfig)
        {
            float seededX = ComputeSeedOffset(worldX, settings.seed);
            float seededZ = ComputeSeedOffset(worldZ, settings.seed);

            float effectiveNoiseScale = settings.noiseScale;
            int effectiveOctaves = settings.octaves;
            float effectivePersistence = settings.persistence;
            float effectiveLacunarity = settings.lacunarity;
            float effectiveDensityMultiplier = settings.foliageDensityMultiplier;

            if (biomeOverride != null)
            {
                var terrainParams = biomeOverride.terrainParams as JayFos.Biomes.BiomeTerrainParams;
                if (terrainParams != null && terrainParams.HasFoliageOverrides)
                {
                    if (terrainParams.foliageDensityMultiplier > 0f)
                        effectiveDensityMultiplier = terrainParams.foliageDensityMultiplier;
                }
            }

            if (foliageConfig != null && foliageConfig.HasAnyOverrides)
            {
                if (foliageConfig.octaves > 0) effectiveOctaves = foliageConfig.octaves;
                if (foliageConfig.persistence > 0f) effectivePersistence = foliageConfig.persistence;
                if (foliageConfig.lacunarity > 0f) effectiveLacunarity = foliageConfig.lacunarity;
            }

            float amplitude = 1f;
            float frequency = 1f;
            float noiseHeight = 0f;

            float precomputedSampleXBase = seededX * effectiveNoiseScale;
            float precomputedSampleZBase = seededZ * effectiveNoiseScale;

            for (int i = 0; i < effectiveOctaves; i++)
            {
                float sampleX = precomputedSampleXBase * frequency;
                float sampleZ = precomputedSampleZBase * frequency;

                float perlin = Mathf.PerlinNoise(sampleX, sampleZ) * 2f - 1f;
                noiseHeight += perlin * amplitude;

                amplitude *= effectivePersistence;
                frequency *= effectiveLacunarity;
            }

            return noiseHeight * effectiveDensityMultiplier;
        }

        public float SampleWorldSpaceNoiseWithParams(float worldX, float worldZ, JayFos.Biomes.BiomeDefinition biomeOverride = null, int? octaves = null, float? persistence = null, float? lacunarity = null)
        {
            float seededX = ComputeSeedOffset(worldX, settings.seed);
            float seededZ = ComputeSeedOffset(worldZ, settings.seed);

            float effectiveNoiseScale = settings.noiseScale;
            int effectiveOctaves = octaves ?? settings.octaves;
            float effectivePersistence = persistence ?? settings.persistence;
            float effectiveLacunarity = lacunarity ?? settings.lacunarity;
            float effectiveDensityMultiplier = settings.foliageDensityMultiplier;

            float amplitude = 1f;
            float frequency = 1f;
            float noiseHeight = 0f;

            float precomputedSampleXBase = seededX * effectiveNoiseScale;
            float precomputedSampleZBase = seededZ * effectiveNoiseScale;

            for (int i = 0; i < effectiveOctaves; i++)
            {
                float sampleX = precomputedSampleXBase * frequency;
                float sampleZ = precomputedSampleZBase * frequency;

                float perlin = Mathf.PerlinNoise(sampleX, sampleZ) * 2f - 1f;
                noiseHeight += perlin * amplitude;

                amplitude *= effectivePersistence;
                frequency *= effectiveLacunarity;
            }

            return noiseHeight * effectiveDensityMultiplier;
        }

        private float SampleNoise(float seededWorldX, float seededWorldZ,
            float noiseScale, float heightMultiplier,
            int octaves, float persistence, float lacunarity)
        {
            float amplitude = 1f;
            float frequency = 1f;
            float noiseHeight = 0f;

            for (int i = 0; i < octaves; i++)
            {
                float sampleX = seededWorldX * noiseScale * frequency;
                float sampleZ = seededWorldZ * noiseScale * frequency;

                float perlin = Mathf.PerlinNoise(sampleX, sampleZ) * 2f - 1f;
                noiseHeight += perlin * amplitude;

                amplitude *= persistence;
                frequency *= lacunarity;
            }

            return noiseHeight * heightMultiplier;
        }

        internal int Hash(float value)
        {
            uint data = (uint)(value * 609347520.0f);
            uint s = (uint)settings.seed;
            uint result = data ^ s;
            result ^= (result >> 16);
            result *= 0x85ebca6bu;
            result ^= (result >> 13);
            result *= 0xc2b2ae35u;
            result ^= (result >> 28);
            return (int)(result & 0x3fffffff);
        }
    }
}
