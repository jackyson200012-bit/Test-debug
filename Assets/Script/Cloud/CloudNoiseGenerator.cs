using UnityEngine;

namespace JayFos.Cloud
{
    public class CloudNoiseGenerator
    {
        private readonly int cloudSeed;

        public CloudNoiseGenerator(int worldSeed, int cloudSeedOffset)
        {
            cloudSeed = worldSeed ^ cloudSeedOffset;
        }

        public static int DeriveCloudSeed(int worldSeed, int cloudSeedOffset)
        {
            return worldSeed ^ cloudSeedOffset;
        }

        private static float ComputeSeedOffset(float worldCoord, int seed)
        {
            return worldCoord + (float)seed * 0.1f;
        }

        public float SampleCloudNoise(float worldX, float worldZ, float scale, int octaves, float persistence, float lacunarity)
        {
            float seededX = ComputeSeedOffset(worldX, cloudSeed);
            float seededZ = ComputeSeedOffset(worldZ, cloudSeed);

            float amplitude = 1f;
            float frequency = 1f;
            float noiseHeight = 0f;

            float baseX = seededX * scale;
            float baseZ = seededZ * scale;

            for (int i = 0; i < octaves; i++)
            {
                float sampleX = baseX * frequency;
                float sampleZ = baseZ * frequency;

                float perlin = Mathf.PerlinNoise(sampleX, sampleZ) * 2f - 1f;
                noiseHeight += perlin * amplitude;

                amplitude *= persistence;
                frequency *= lacunarity;
            }

            return noiseHeight;
        }

        public float GetCloudPresence(float worldX, float worldZ, float coverage, float noiseScale, int octaves, float persistence, float lacunarity)
        {
            float noise = SampleCloudNoise(worldX, worldZ, noiseScale, octaves, persistence, lacunarity);
            float normalized = (noise + 1f) * 0.5f;
            float threshold = 1f - coverage;
            return Mathf.Clamp01((normalized - threshold) / Mathf.Max(coverage, 0.01f));
        }

        public int Hash(float x, float z)
        {
            unchecked
            {
                int h = cloudSeed;
                h ^= (int)(x * 73856093f);
                h ^= (int)(z * 19349663f);
                h ^= (h >> 16);
                h *= (int)0x85ebca6b;
                h ^= (h >> 13);
                h *= (int)0xc2b2ae35;
                h ^= (h >> 16);
                return h;
            }
        }

        public float Hash01(float x, float z)
        {
            return Mathf.Abs(Hash(x, z) % 10000) / 10000f;
        }
    }
}
