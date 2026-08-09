using UnityEngine;
using JayFos.Terrain;

namespace JayFos.Roads
{
    public class RoadFieldGrid
    {
        private float[,] influence;
        private float gridSpacing;
        private int gridResolution;
        private int gridBorderCells;
        private Vector2 worldOrigin;
        private float roadThreshold;
        private float roadBlend;
        private float ridgeThreshold;
        private float ridgeSampleDistance;
        private float minRoadWidth;
        private float maxRoadWidth;
        private float widthNoiseScale;
        private int worldSeed;
        private float roadNoiseScale;

        private const float GRADIENT_EPSILON = 0.5f;

        public int GridResolution => gridResolution;
        public float GridSpacing => gridSpacing;

        public void Compute(Vector2Int chunkCoord, RoadSettings settings, int seed)
        {
            if (settings == null || !settings.enableRoads)
                return;

            worldSeed = seed;
            roadNoiseScale = settings.roadNoiseScale;
            roadThreshold = settings.roadThreshold;
            roadBlend = settings.roadBlend;
            ridgeThreshold = settings.ridgeThreshold;
            ridgeSampleDistance = settings.ridgeSampleDistance;
            minRoadWidth = settings.minRoadWidth;
            maxRoadWidth = settings.maxRoadWidth;
            widthNoiseScale = settings.widthNoiseScale;
            gridSpacing = settings.gridResolution;
            gridBorderCells = settings.gridBorderCells;

            int chunkSize = 64; // Will be passed or read from settings
            gridResolution = Mathf.CeilToInt(chunkSize / gridSpacing) + 1 + 2 * gridBorderCells;

            worldOrigin = new Vector2(
                chunkCoord.x * chunkSize - gridBorderCells * gridSpacing,
                chunkCoord.y * chunkSize - gridBorderCells * gridSpacing);

            int totalCells = gridResolution * gridResolution;
            if (influence == null || influence.Length != totalCells)
            {
                influence = new float[gridResolution, gridResolution];
            }

            for (int gz = 0; gz < gridResolution; gz++)
            {
                for (int gx = 0; gx < gridResolution; gx++)
                {
                    float worldX = worldOrigin.x + gx * gridSpacing;
                    float worldZ = worldOrigin.y + gz * gridSpacing;

                    float roadNoise = SampleNoise(worldX, worldZ);
                    float roadPresence = ComputeRoadPresence(roadNoise);
                    float ridgeSignal = ComputeRidgeSignal(worldX, worldZ, roadNoise);
                    float centerlinePresence = Mathf.Clamp01(ridgeSignal / ridgeThreshold);

                    influence[gx, gz] = roadPresence * centerlinePresence;
                }
            }
        }

        public float Sample(float worldX, float worldZ)
        {
            if (influence == null || gridSpacing <= 0f)
                return 0f;

            float gx = (worldX - worldOrigin.x) / gridSpacing;
            float gz = (worldZ - worldOrigin.y) / gridSpacing;

            int x0 = Mathf.FloorToInt(gx);
            int z0 = Mathf.FloorToInt(gz);

            x0 = Mathf.Clamp(x0, 0, gridResolution - 2);
            z0 = Mathf.Clamp(z0, 0, gridResolution - 2);

            float fx = Mathf.Clamp01(gx - x0);
            float fz = Mathf.Clamp01(gz - z0);

            float v00 = influence[x0, z0];
            float v10 = influence[x0 + 1, z0];
            float v01 = influence[x0, z0 + 1];
            float v11 = influence[x0 + 1, z0 + 1];

            float bottom = Mathf.Lerp(v00, v10, fx);
            float top = Mathf.Lerp(v01, v11, fx);

            return Mathf.Lerp(bottom, top, fz);
        }

        public bool IsRoad(float worldX, float worldZ, float threshold = 0.5f)
        {
            return Sample(worldX, worldZ) > threshold;
        }

        public float GetRoadWidth(float worldX, float worldZ)
        {
            float widthNoise = SampleNoise(worldX + 1000f, worldZ + 1000f);
            float roadInfluence = Sample(worldX, worldZ);
            return Mathf.Lerp(minRoadWidth, maxRoadWidth, widthNoise) * roadInfluence;
        }

        private float SampleNoise(float worldX, float worldZ)
        {
            float seededX = worldX + (float)worldSeed * 0.1f;
            float seededZ = worldZ + (float)worldSeed * 0.17f;

            float sampleX = seededX * roadNoiseScale;
            float sampleZ = seededZ * roadNoiseScale;

            return Mathf.PerlinNoise(sampleX, sampleZ) * 2f - 1f;
        }

        private float ComputeRoadPresence(float roadNoise)
        {
            return Mathf.SmoothStep(roadThreshold - roadBlend, roadThreshold + roadBlend, roadNoise);
        }

        private float ComputeRidgeSignal(float worldX, float worldZ, float roadNoise)
        {
            float dx = SampleNoise(worldX + GRADIENT_EPSILON, worldZ) -
                       SampleNoise(worldX - GRADIENT_EPSILON, worldZ);
            float dz = SampleNoise(worldX, worldZ + GRADIENT_EPSILON) -
                       SampleNoise(worldX, worldZ - GRADIENT_EPSILON);

            float perpX = -dz;
            float perpZ = dx;
            float perpLen = Mathf.Sqrt(perpX * perpX + perpZ * perpZ);

            if (perpLen < 0.001f)
                return 0f;

            perpX /= perpLen;
            perpZ /= perpLen;

            float sampleDist = ridgeSampleDistance;
            float leftNoise = SampleNoise(worldX + perpX * sampleDist, worldZ + perpZ * sampleDist);
            float rightNoise = SampleNoise(worldX - perpX * sampleDist, worldZ - perpZ * sampleDist);

            return Mathf.Max(0f, roadNoise - Mathf.Max(leftNoise, rightNoise));
        }

        public void Clear()
        {
            if (influence != null)
            {
                System.Array.Clear(influence, 0, influence.Length);
            }
        }
    }
}
