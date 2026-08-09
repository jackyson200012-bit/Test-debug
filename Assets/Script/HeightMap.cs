using UnityEngine;

namespace JayFos.Terrain
{
    public class HeightMap
    {
        private Vector2Int vertexOrigin = Vector2Int.zero;

        public float[,] heights;
        public float minHeight;
        public float maxHeight;
        public int width;
        public int depth;
        public float vertexSpacing;

        public HeightMap(float[,] heights, Vector2Int vertexOrigin, float minHeight, float maxHeight)
        {
            this.vertexOrigin = vertexOrigin;
            this.heights = heights;
            this.minHeight = minHeight;
            this.maxHeight = maxHeight;
            width = heights.GetLength(0);
            depth = heights.GetLength(1);
        }

        public HeightMap(int verticesPerLine, float minHeight, float maxHeight, float vertexSpacing = 1.0f)
        {
            int size = verticesPerLine;
            heights = new float[size, size];
            this.width = size;
            this.depth = size;
            this.minHeight = minHeight;
            this.maxHeight = maxHeight;
            this.vertexSpacing = vertexSpacing;
            this.vertexOrigin = Vector2Int.zero;
        }

        public float GetHeightAtWorldPosition(float worldX, float worldZ)
        {
            return GetHeightOnly(worldX, worldZ);
        }

        internal struct HeightAndSlopeResult
        {
            public float height;
            public float slopeDegrees;
        }

        internal HeightAndSlopeResult GetHeightAndSlope(float worldX, float worldZ)
        {
            float hCenter = GetHeightOnly(worldX, worldZ);

            float sampleRadius = 2.0f;
            float hNorth = GetHeightOnly(worldX, worldZ + sampleRadius);
            float hSouth = GetHeightOnly(worldX, worldZ - sampleRadius);
            float hEast  = GetHeightOnly(worldX + sampleRadius, worldZ);
            float hWest  = GetHeightOnly(worldX - sampleRadius, worldZ);

            float dx = (hEast - hWest) / (2.0f * sampleRadius);
            float dz = (hNorth - hSouth) / (2.0f * sampleRadius);
            float gradientMagnitude = Mathf.Sqrt(dx * dx + dz * dz);
            float slopeDegrees = Mathf.Rad2Deg * Mathf.Atan(gradientMagnitude);

            return new HeightAndSlopeResult { height = hCenter, slopeDegrees = slopeDegrees };
        }

        private float GetHeightOnly(float worldX, float worldZ)
        {
            if (vertexSpacing <= 0f) return minHeight;

            float relativeX = worldX - vertexOrigin.x * vertexSpacing;
            float relativeZ = worldZ - vertexOrigin.y * vertexSpacing;

            int xIndex = Mathf.FloorToInt(relativeX / vertexSpacing);
            int zIndex = Mathf.FloorToInt(relativeZ / vertexSpacing);

            xIndex = Mathf.Clamp(xIndex, 0, width - 1);
            zIndex = Mathf.Clamp(zIndex, 0, depth - 1);

            float fractionX = Mathf.Clamp01(relativeX / vertexSpacing - (float)xIndex);
            float fractionZ = Mathf.Clamp01(relativeZ / vertexSpacing - (float)zIndex);

            int nextX = Mathf.Min(xIndex + 1, width - 1);
            int nextZ = Mathf.Min(zIndex + 1, depth - 1);

            float h00 = heights[xIndex, zIndex];
            float h10 = heights[nextX, zIndex];
            float h01 = heights[xIndex, nextZ];
            float h11 = heights[nextX, nextZ];

            return Mathf.Lerp(Mathf.Lerp(h00, h10, fractionX), Mathf.Lerp(h01, h11, fractionX), fractionZ);
        }
    }
}
