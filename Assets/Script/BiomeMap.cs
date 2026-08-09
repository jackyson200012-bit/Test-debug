using UnityEngine;
using JayFos.Terrain;

namespace JayFos.Biomes
{
    public class BiomeMap
    {
        private readonly int seed;
        private readonly float biomeNoiseScale;
        private readonly float biomeDetailNoiseScale;
        private readonly BiomeDefinition[] biomes;
        private readonly BiomeDefinition defaultBiome;

        public BiomeMap(int worldSeed, float biomeNoiseScale, float biomeDetailNoiseScale,
                        BiomeDefinition[] biomes, BiomeDefinition defaultBiome)
        {
            seed = worldSeed;
            this.biomeNoiseScale = biomeNoiseScale;
            this.biomeDetailNoiseScale = biomeDetailNoiseScale;
            this.biomes = biomes ?? new BiomeDefinition[0];
            this.defaultBiome = defaultBiome;
        }

        private float ComputeBiomeNoise(float worldX, float worldZ)
        {
            float seedOffsetX = seed * 0.1f;
            float seedOffsetZ = seed * 0.17f;

            float seededX = NoiseGenerator.ComputeSeedOffset(worldX + seedOffsetX, seed);
            float seededZ = NoiseGenerator.ComputeSeedOffset(worldZ + seedOffsetZ, seed);

            float primary = SampleBiomeNoise(seededX, seededZ, biomeNoiseScale);
            float detail = SampleBiomeNoise(seededX, seededZ, biomeDetailNoiseScale) * 0.5f;

            return primary + detail;
        }

        private static float SampleBiomeNoise(float worldX, float worldZ, float scale)
        {
            float seededX = NoiseGenerator.ComputeSeedOffset(worldX, 0);
            float seededZ = NoiseGenerator.ComputeSeedOffset(worldZ, 0);

            float sampleX = seededX * scale;
            float sampleZ = seededZ * scale;

            float perlin = Mathf.PerlinNoise(sampleX, sampleZ) * 2f - 1f;
            return perlin;
        }

        public BiomeDefinition GetBiome(float worldX, float worldZ)
        {
            return GetBiomeSample(worldX, worldZ).primary;
        }

        public BiomeSample GetBiomeSample(float worldX, float worldZ)
        {
            float biomeValue = ComputeBiomeNoise(worldX, worldZ);
            BiomeDefinition primary = SelectBiome(biomeValue);

            return new BiomeSample
            {
                primary = primary,
                primaryWeight = 1.0f,
                secondary = null,
                secondaryWeight = 0.0f,
                temperature = primary != null ? primary.temperature : 0.5f,
                moisture = primary != null ? primary.moisture : 0.5f,
                noiseValue = biomeValue
            };
        }

        public BiomeDefinition GetBiomeAtChunkCenter(int chunkX, int chunkZ, float chunkSize)
        {
            float centerX = (chunkX + 0.5f) * chunkSize;
            float centerZ = (chunkZ + 0.5f) * chunkSize;
            return GetBiome(centerX, centerZ);
        }

        public BiomeDefinition GetBiomeAtChunkCorner(int chunkX, int chunkZ, float chunkSize, int cornerX, int cornerZ)
        {
            float offsetX = chunkSize * cornerX;
            float offsetZ = chunkSize * cornerZ;
            float worldX = chunkX * chunkSize + offsetX;
            float worldZ = chunkZ * chunkSize + offsetZ;
            return GetBiome(worldX, worldZ);
        }

        public BiomeSample GetBiomeSampleAtChunkCorner(int chunkX, int chunkZ, float chunkSize, int cornerX, int cornerZ)
        {
            float offsetX = chunkSize * cornerX;
            float offsetZ = chunkSize * cornerZ;
            float worldX = chunkX * chunkSize + offsetX;
            float worldZ = chunkZ * chunkSize + offsetZ;
            return GetBiomeSample(worldX, worldZ);
        }

        public float[] GetBiomeSamplesAtChunkCorners(int chunkX, int chunkZ, float chunkSize, BiomeDefinition[] biomes, float defaultBiomeTemp, float defaultBiomeMoisture)
        {
            float[] cornerBiomes = new float[4];
            
            BiomeSample nw = GetBiomeSampleAtChunkCorner(chunkX, chunkZ, chunkSize, 0, 0);
            BiomeSample ne = GetBiomeSampleAtChunkCorner(chunkX, chunkZ, chunkSize, 1, 0);
            BiomeSample sw = GetBiomeSampleAtChunkCorner(chunkX, chunkZ, chunkSize, 0, 1);
            BiomeSample se = GetBiomeSampleAtChunkCorner(chunkX, chunkZ, chunkSize, 1, 1);
            
            cornerBiomes[0] = nw.primary != null ? nw.primary.GetHashCode() : -1;
            cornerBiomes[1] = ne.primary != null ? ne.primary.GetHashCode() : -1;
            cornerBiomes[2] = sw.primary != null ? sw.primary.GetHashCode() : -1;
            cornerBiomes[3] = se.primary != null ? se.primary.GetHashCode() : -1;
            
            return cornerBiomes;
        }

        private BiomeDefinition SelectBiome(float noiseValue)
        {
            int hash = ComputeBiomeHash(noiseValue);
            int biomeIndex = Mathf.Abs(hash) % (biomes.Length > 0 ? biomes.Length : 1);

            return biomes.Length > 0 ? biomes[biomeIndex] : defaultBiome;
        }

        private static int ComputeBiomeHash(float noiseValue)
        {
            uint data = (uint)(noiseValue * 609347520.0f);
            uint result = data;
            result ^= (result >> 16);
            result *= 0x85ebca6bu;
            result ^= (result >> 13);
            result *= 0xc2b2ae35u;
            result ^= (result >> 28);
            return (int)(result & 0x3fffffff);
        }

        public float GetBiomeNoiseValue(float worldX, float worldZ)
        {
            return ComputeBiomeNoise(worldX, worldZ);
        }

        public float GetTemperature(float worldX, float worldZ)
        {
            return GetBiomeSample(worldX, worldZ).temperature;
        }

        public float GetMoisture(float worldX, float worldZ)
        {
            return GetBiomeSample(worldX, worldZ).moisture;
        }

        public BiomeDefinition[] GetAllBiomes()
        {
            return biomes;
        }

        public BiomeDefinition GetDefaultBiome()
        {
            return defaultBiome;
        }
    }
}
