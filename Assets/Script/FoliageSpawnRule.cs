using System;
using UnityEngine;

namespace JayFos.Foliage
{
    [System.Serializable]
    public class FoliageSpawnRule
    {
        [Header("Identity")]
        public string name = "Default";
        public GameObject prefab;

        [Header("Biome / Tag Filtering")]
        public string biomeTag = "";
        public string customTag = "";

        [Header("Spawn Priority")]
        public int spawnPriority = 0;

        [Header("Density Conditions")]
        [Range(0f, 1f)]
        public float minDensity = 0f;
        [Range(0f, 1f)]
        public float maxDensity = 1f;

        [Header("Height Conditions")]
        public float minHeight = -1f;
        public float maxHeight = -1f;

        [Header("Slope Conditions")]
        [Range(0f, 90f)]
        public float maxSlope = 90f;

        [Header("Noise Variation Conditions")]
        [Range(-1f, 1f)]
        public float noiseVariationMin = -1f;
        [Range(-1f, 1f)]
        public float noiseVariationMax = 1f;

        public bool Matches(float terrainHeight, float slopeDegrees, float densityWeight, float secondaryNoise)
        {
            return MatchesInternal(terrainHeight, slopeDegrees, densityWeight, secondaryNoise);
        }

        public bool Matches(float terrainHeight, float slopeDegrees, float densityWeight, float secondaryNoise, string biomeFilter)
        {
            if (!string.IsNullOrEmpty(biomeFilter) && !biomeTag.Equals(biomeFilter, StringComparison.OrdinalIgnoreCase))
                return false;

            return MatchesInternal(terrainHeight, slopeDegrees, densityWeight, secondaryNoise);
        }

        private bool MatchesInternal(float terrainHeight, float slopeDegrees, float densityWeight, float secondaryNoise)
        {
            if (minHeight >= 0f && terrainHeight < minHeight) return false;
            if (maxHeight >= 0f && terrainHeight > maxHeight) return false;
            if (slopeDegrees > maxSlope) return false;
            if (densityWeight < minDensity || densityWeight > maxDensity) return false;
            if (secondaryNoise < noiseVariationMin || secondaryNoise > noiseVariationMax) return false;

            return true;
        }

        public string GetDebugReason(float terrainHeight, float slopeDegrees, float densityWeight, float secondaryNoise)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            bool matched = true;

            if (minHeight >= 0f && terrainHeight < minHeight)
            {
                sb.Append($"height {terrainHeight:F1} < min {minHeight}; ");
                matched = false;
            }
            if (maxHeight >= 0f && terrainHeight > maxHeight)
            {
                sb.Append($"height {terrainHeight:F1} > max {maxHeight}; ");
                matched = false;
            }
            if (slopeDegrees > maxSlope)
            {
                sb.Append($"slope {slopeDegrees:F1} > max {maxSlope}; ");
                matched = false;
            }
            if (densityWeight < minDensity || densityWeight > maxDensity)
            {
                sb.Append($"density {densityWeight:F2} not in [{minDensity}, {maxDensity}]; ");
                matched = false;
            }
            if (secondaryNoise < noiseVariationMin || secondaryNoise > noiseVariationMax)
            {
                sb.Append($"variation {secondaryNoise:F2} not in [{noiseVariationMin}, {noiseVariationMax}]; ");
                matched = false;
            }

            return matched ? "Matched" : ("Rejected: " + sb.ToString());
        }
    }
}
