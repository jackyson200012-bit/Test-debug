using System;
using UnityEngine;

namespace JayFos.Biomes
{
    [CreateAssetMenu(fileName = "BiomeDefinition", menuName = "World/Biome Definition")]
    public class BiomeDefinition : ScriptableObject
    {
        [Header("Identity")]
        public BiomeType biomeType = BiomeType.None;
        public string biomeName = "Unnamed Biome";

        [Header("Height Range")]
        public float heightMin = float.NegativeInfinity;
        public float heightMax = float.PositiveInfinity;

        [Header("Environmental Parameters")]
        [Range(0f, 1f)]
        public float temperature = 0.5f;
        [Range(0f, 1f)]
        public float moisture = 0.5f;

        [Header("Visual")]
        public Color color = Color.gray;

        [Header("Custom Attributes")]
        public BiomeAttribute[] customAttributes;

        [Header("Extension Points")]
        public ScriptableObject terrainParams;
        public ScriptableObject foliageConfig;
        public ScriptableObject resourceSpawning;
        public ScriptableObject weatherEnvironment;

        public bool HeightMatches(float terrainHeight)
        {
            if (heightMin > float.NegativeInfinity && terrainHeight < heightMin)
                return false;
            if (heightMax < float.PositiveInfinity && terrainHeight > heightMax)
                return false;
            return true;
        }

        public float GetAttribute(string key, float defaultValue = 0f)
        {
            if (customAttributes == null)
                return defaultValue;

            for (int i = 0; i < customAttributes.Length; i++)
            {
                if (string.Equals(customAttributes[i].key, key, StringComparison.OrdinalIgnoreCase))
                    return customAttributes[i].value;
            }

            return defaultValue;
        }

        public string ToDebugString()
        {
            return $"[{biomeType}] {biomeName} | Height:{heightMin:F1}~{heightMax:F1} | Temp:{temperature:F2} | Moist:{moisture:F2}";
        }
    }
}
