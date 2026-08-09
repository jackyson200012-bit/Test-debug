using UnityEngine;

namespace JayFos.Cloud
{
    [CreateAssetMenu(fileName = "CloudSettings", menuName = "World/Cloud Settings")]
    public class CloudSettings : ScriptableObject
    {
        [Header("Cloud Generation")]
        public bool cloudEnabled = true;
        [Range(0f, 1f)]
        public float cloudCoverage = 0.5f;
        [Range(0f, 1f)]
        public float cloudDensity = 0.6f;

        [Header("Cloud Dimensions")]
        [Range(50f, 500f)]
        public float cloudAltitude = 200f;
        [Range(10f, 100f)]
        public float cloudHeight = 30f;
        [Range(20f, 200f)]
        public float cloudScale = 80f;

        [Header("Cloud Appearance")]
        [Range(0f, 1f)]
        public float cloudSoftness = 0.5f;
        [Range(0f, 1f)]
        public float cloudOpacity = 0.85f;
        public Color cloudColor = new Color(0.95f, 0.97f, 1f, 1f);
        public Color cloudShadowColor = new Color(0.6f, 0.65f, 0.75f, 1f);
        public Material cloudMaterial;

        [Header("Cloud Movement")]
        [Range(0f, 50f)]
        public float cloudSpeed = 5f;
        public Vector2 cloudWindDirection = new Vector2(1f, 0.3f);

        [Header("Cloud Streaming")]
        [Range(200f, 2000f)]
        public float cloudRenderDistance = 800f;
        [Range(50f, 300f)]
        public float cloudCellSize = 150f;
        [Range(1, 10)]
        public int cloudsPerCell = 3;

        [Header("Noise")]
        public float noiseScale = 0.008f;
        [Range(1, 8)]
        public int octaves = 4;
        [Range(0f, 1f)]
        public float persistence = 0.5f;
        public float lacunarity = 2f;

        [Header("Seed")]
        public int cloudSeedOffset = 98765;

        public int DeriveCloudSeed(int worldSeed)
        {
            return worldSeed ^ cloudSeedOffset;
        }
    }
}
