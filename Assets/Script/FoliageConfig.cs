using UnityEngine;

namespace JayFos.Foliage
{
    [CreateAssetMenu(fileName = "FoliageConfig", menuName = "World/Foliage Configuration")]
    public class FoliageConfig : ScriptableObject
    {
        [Header("Placement")]
        public float densityPerUnitArea = 0f;
        [Range(0f, 1f)]
        public float noiseThreshold = 0f;

        [Header("Noise Parameters")]
        public int octaves = 0;
        [Range(0f, 1f)]
        public float persistence = 0f;
        public float lacunarity = 0f;

        [Header("Scale & Offset")]
        public Vector2 noiseOffset;
        public float heightMultiplier = 0f;

        public bool HasAnyOverrides =>
            densityPerUnitArea > 0f ||
            noiseThreshold > 0f ||
            octaves > 0 ||
            persistence > 0f ||
            lacunarity > 0f ||
            heightMultiplier > 0f ||
            (noiseOffset.x != 0f || noiseOffset.y != 0f);
    }
}
