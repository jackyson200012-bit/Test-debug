using UnityEngine;
using JayFos.Biomes;

namespace JayFos.Terrain
{
    public class TerrainGenerator
    {
        private readonly NoiseGenerator noiseGenerator;
        private readonly JayFos.World.WorldSettings settings;
        private JayFos.Biomes.BiomeMap biomeMap;

        public TerrainGenerator(JayFos.World.WorldSettings settings)
        {
            this.settings = settings;
            noiseGenerator = new NoiseGenerator(settings);
        }

        /// <summary>
        /// Sets the biome map for biome-aware terrain generation.
        /// Can be null (no biome influence) for backward compatibility.
        /// </summary>
        public void SetBiomeMap(JayFos.Biomes.BiomeMap biomeMap)
        {
            this.biomeMap = biomeMap;
        }

        public HeightMap GenerateHeightMap(Vector2Int chunkCoord)
        {
            JayFos.Biomes.BiomeDefinition biomeOverride = null;
            if (biomeMap != null && settings.enableBiomeSystem)
            {
                biomeOverride = biomeMap.GetBiomeAtChunkCenter(chunkCoord.x, chunkCoord.y, settings.chunkSize);
            }

            return noiseGenerator.GenerateHeightMap(chunkCoord, biomeOverride);
        }
    }
}
