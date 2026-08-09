using UnityEngine;

namespace JayFos.Biomes
{
    [CreateAssetMenu(fileName = "BiomeTerrainParams", menuName = "World/Biome Terrain Parameters")]
    public class BiomeTerrainParams : ScriptableObject
    {
        [Header("Noise Overrides")]
        public float noiseScale = 0f;
        public float heightMultiplier = 0f;
        public int octaves = 0;
        [Range(0f, 1f)]
        public float persistence = 0f;
        public float lacunarity = 0f;

        [Header("Foliage Overrides")]
        [Range(0f, 5f)]
        public float foliageDensityMultiplier = 0f;
        [Range(0f, 1f)]
        public float spawnThreshold = 0f;

        [Header("Water Level Override")]
        [Range(-50f, 10f)]
        public float waterLevel = -5f;
        public bool overrideWaterLevel = false;

        public bool HasTerrainOverrides =>
            noiseScale > 0f ||
            heightMultiplier > 0f ||
            octaves > 0 ||
            persistence > 0f ||
            lacunarity > 0f;

        public bool HasFoliageOverrides =>
            foliageDensityMultiplier > 0f ||
            spawnThreshold > 0f;
    }
}
