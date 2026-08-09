using System.Collections.Generic;
using UnityEngine;
using JayFos.Roads;

namespace JayFos.Terrain
{
    public static class MeshGenerator
    {
        private static int _bufferVertsPerLine;
        private static Vector3[] _vertexBuffer;
        private static Vector2[] _uvBuffer;
        private static int[] _triangleBuffer;
        private static int _triangleCount;

        private static void EnsureBuffers(int vertsPerLine)
        {
            int totalVerts = vertsPerLine * vertsPerLine;
            int expectedTriangles = (vertsPerLine - 1) * (vertsPerLine - 1) * 6;

            if (_vertexBuffer == null || totalVerts > _vertexBuffer.Length)
            {
                _vertexBuffer = new Vector3[totalVerts];
                _uvBuffer = new Vector2[totalVerts];
                _triangleBuffer = new int[expectedTriangles];
                _bufferVertsPerLine = vertsPerLine;
            }
            else if (expectedTriangles > _triangleBuffer.Length)
            {
                _triangleBuffer = new int[expectedTriangles];
                _bufferVertsPerLine = vertsPerLine;
            }
        }

        private static void ResetTriangleCount()
        {
            _triangleCount = 0;
        }

        public static Mesh Generate(HeightMap heightMap, JayFos.World.WorldSettings settings, JayFos.Biomes.BiomeDefinition biome = null, RoadFieldGrid roadGrid = null, Vector2Int chunkCoord = default)
        {
            int vertsPerLine = settings.verticesPerLine;
            int chunkSize = settings.chunkSize;

            EnsureBuffers(vertsPerLine);
            Vector3[] vertices = _vertexBuffer;
            Vector2[] uvs = _uvBuffer;
            ResetTriangleCount();

            float vertexSpacing = (float)chunkSize / (vertsPerLine - 1);
            bool hasRoads = roadGrid != null && settings.enableRoads && settings.roadSettings != null;
            Color roadColor = hasRoads ? settings.roadSettings.roadColor : Color.clear;
            float roadHeightOffset = hasRoads ? settings.roadSettings.roadHeightOffset : 0f;

            int vertexIndex = 0;

            for (int z = 0; z < vertsPerLine; z++)
            {
                for (int x = 0; x < vertsPerLine; x++)
                {
                    float height = heightMap.heights[x, z];
                    float finalHeight = height;
                    Color vertexColor = biome != null ? biome.color : Color.white;

                    if (hasRoads)
                    {
                        float worldX = chunkCoord.x * chunkSize + x * vertexSpacing;
                        float worldZ = chunkCoord.y * chunkSize + z * vertexSpacing;

                        float roadInfluence = roadGrid.Sample(worldX, worldZ);

                        if (roadInfluence > 0.01f)
                        {
                            float terrainBlend = 1f - roadInfluence;
                            float roadHeight = height + roadHeightOffset;
                            finalHeight = Mathf.Lerp(roadHeight, height, terrainBlend);
                            vertexColor = Color.Lerp(roadColor, vertexColor, terrainBlend);
                        }
                    }

                    vertices[vertexIndex] = new Vector3(
                        x * vertexSpacing,
                        finalHeight,
                        z * vertexSpacing
                    );

                    uvs[vertexIndex] = new Vector2(
                        (float)x / (vertsPerLine - 1),
                        (float)z / (vertsPerLine - 1)
                    );

                    if (x < vertsPerLine - 1 && z < vertsPerLine - 1)
                    {
                        int a = vertexIndex;
                        int b = vertexIndex + vertsPerLine;
                        int c = vertexIndex + vertsPerLine + 1;
                        int d = vertexIndex + 1;

                        int t0 = _triangleCount;
                        int t1 = _triangleCount + 1;
                        int t2 = _triangleCount + 2;

                        _triangleBuffer[t0] = a;
                        _triangleBuffer[t1] = b;
                        _triangleBuffer[t2] = c;
                        _triangleCount += 3;

                        t0 = _triangleCount;
                        t1 = _triangleCount + 1;
                        t2 = _triangleCount + 2;

                        _triangleBuffer[t0] = a;
                        _triangleBuffer[t1] = c;
                        _triangleBuffer[t2] = d;
                        _triangleCount += 3;
                    }

                    vertexIndex++;
                }
            }

            Mesh mesh = new Mesh
            {
                name = "Terrain Chunk Mesh"
            };

            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(_triangleBuffer, 0, _triangleCount, 0);
            mesh.SetUVs(0, uvs);

            Color[] vertexColors = null;
            if (hasRoads)
            {
                vertexColors = new Color[vertsPerLine * vertsPerLine];
                for (int i = 0; i < vertexColors.Length; i++)
                {
                    float worldX = chunkCoord.x * chunkSize + (i % vertsPerLine) * vertexSpacing;
                    float worldZ = chunkCoord.y * chunkSize + (i / vertsPerLine) * vertexSpacing;

                    Color baseColor = biome != null ? biome.color : Color.white;
                    float roadInfluence = roadGrid.Sample(worldX, worldZ);

                    if (roadInfluence > 0.01f)
                    {
                        float terrainBlend = 1f - roadInfluence;
                        vertexColors[i] = Color.Lerp(roadColor, baseColor, terrainBlend);
                    }
                    else
                    {
                        vertexColors[i] = baseColor;
                    }
                }
            }
            else if (biome != null)
            {
                int vertexCount = vertsPerLine * vertsPerLine;
                vertexColors = new Color[vertexCount];
                Color biomeColor = biome.color;
                for (int i = 0; i < vertexCount; i++)
                {
                    vertexColors[i] = biomeColor;
                }
            }

            if (vertexColors != null)
            {
                mesh.SetColors(vertexColors);
            }

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}