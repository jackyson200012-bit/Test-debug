using UnityEngine;

namespace JayFos.Roads
{
    [CreateAssetMenu(fileName = "RoadSettings", menuName = "World/Road Settings")]
    public class RoadSettings : ScriptableObject
    {
        [Header("Enable")]
        public bool enableRoads = true;

        [Header("Noise Parameters")]
        [Range(0.001f, 0.1f)]
        public float roadNoiseScale = 0.01f;
        [Range(0f, 1f)]
        public float roadThreshold = 0.3f;
        [Range(0.01f, 0.5f)]
        public float roadBlend = 0.1f;

        [Header("Ridge Detection")]
        [Range(0.01f, 0.5f)]
        public float ridgeThreshold = 0.05f;
        [Range(0.5f, 5f)]
        public float ridgeSampleDistance = 2.0f;

        [Header("Road Width")]
        [Range(1f, 20f)]
        public float minRoadWidth = 3.0f;
        [Range(2f, 30f)]
        public float maxRoadWidth = 8.0f;
        [Range(0.001f, 0.1f)]
        public float widthNoiseScale = 0.02f;

        [Header("Grid")]
        [Range(0.5f, 5f)]
        public float gridResolution = 2.0f;
        [Range(1, 8)]
        public int gridBorderCells = 4;

        [Header("Terrain Blending")]
        [Range(-5f, 5f)]
        public float roadHeightOffset = -0.5f;
        [Range(0.5f, 5f)]
        public float blendDistance = 2.0f;

        [Header("Visual")]
        public Color roadColor = new Color(0.4f, 0.35f, 0.3f);
        [Range(0.5f, 5f)]
        public float roadColorBlendWidth = 1.0f;

        [Header("Navigation Data")]
        public bool generateNavigationData = true;
        [Range(2f, 10f)]
        public float waypointSpacing = 5.0f;
    }
}