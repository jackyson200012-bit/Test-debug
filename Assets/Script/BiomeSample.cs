using UnityEngine;

namespace JayFos.Biomes
{
    public struct BiomeSample
    {
        public BiomeDefinition primary;
        public float primaryWeight;
        public BiomeDefinition secondary;
        public float secondaryWeight;
        public float temperature;
        public float moisture;
        public float noiseValue;

        public bool IsValid => primary != null;
        public BiomeType PrimaryType => primary != null ? primary.biomeType : BiomeType.None;

        public override string ToString()
        {
            if (primary == null)
                return "BiomeSample: [No Biome]";

            if (secondary != null && secondaryWeight > 0f)
                return $"BiomeSample: {primary.biomeName} ({primaryWeight:F2}) + {secondary.biomeName} ({secondaryWeight:F2}) | T:{temperature:F2} M:{moisture:F2}";

            return $"BiomeSample: {primary.biomeName} | T:{temperature:F2} M:{moisture:F2}";
        }
    }
}
