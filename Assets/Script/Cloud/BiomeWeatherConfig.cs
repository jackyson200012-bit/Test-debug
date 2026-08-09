using UnityEngine;

namespace JayFos.Cloud
{
    [CreateAssetMenu(fileName = "BiomeWeatherConfig", menuName = "World/Biome Weather Config")]
    public class BiomeWeatherConfig : ScriptableObject
    {
        [Header("Cloud Influence")]
        [Range(-0.5f, 0.5f)]
        public float cloudCoverageModifier = 0f;
        [Range(-0.5f, 0.5f)]
        public float cloudDensityModifier = 0f;

        [Header("Weather Probability")]
        [Range(0f, 1f)]
        public float rainChance = 0.3f;
        [Range(0f, 1f)]
        public float stormChance = 0.1f;
        [Range(0f, 1f)]
        public float fogChance = 0.2f;

        [Header("Fog")]
        [Range(-0.01f, 0.05f)]
        public float fogDensityModifier = 0f;
    }
}
