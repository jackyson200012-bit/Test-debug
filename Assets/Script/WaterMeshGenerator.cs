using UnityEngine;

namespace JayFos.Terrain
{
    public static class WaterMeshGenerator
    {
        private static Vector3[] _vertexBuffer;
        private static Vector2[] _uvBuffer;
        private static int[] _triangleBuffer;

        private static void EnsureBuffers(int vertsPerLine)
        {
            int totalVerts = vertsPerLine * vertsPerLine;
            int expectedTriangles = (vertsPerLine - 1) * (vertsPerLine - 1) * 6;

            if (_vertexBuffer == null || totalVerts > _vertexBuffer.Length)
            {
                _vertexBuffer = new Vector3[totalVerts];
                _uvBuffer = new Vector2[totalVerts];
                _triangleBuffer = new int[expectedTriangles];
            }
            else if (expectedTriangles > _triangleBuffer.Length)
            {
                _triangleBuffer = new int[expectedTriangles];
            }
        }

        /// <summary>
        /// Builds a flat water surface mesh for a single chunk. Vertices sit on the
        /// exact same XZ grid as the terrain mesh (same chunkSize / verticesPerLine),
        /// so neighbouring chunks tile seamlessly and cells whose terrain is below
        /// the water level are the only ones triangulated — dry terrain left empty.
        /// </summary>
        /// <param name="heightMap">Terrain height map for this chunk.</param>
        /// <param name="chunkSize">World size of one chunk edge.</param>
        /// <param name="vertsPerLine">Number of vertices per row (matches terrain).</param>
        /// <param name="waterLevel">World Y of the water surface.</param>
        /// <param name="worldOrigin">World-space XZ of this chunk's origin corner.</param>
        /// <returns>New water mesh, or an empty mesh when no cell is underwater.</returns>
        public static Mesh Generate(
            HeightMap heightMap,
            int chunkSize,
            int vertsPerLine,
            float waterLevel,
            Vector3 worldOrigin)
        {
            int vpl = Mathf.Max(2, vertsPerLine);
            int chunk = Mathf.Max(1, chunkSize);

            EnsureBuffers(vpl);

            float vertexSpacing = (float)chunk / (vpl - 1);

            Vector3[] vertices = _vertexBuffer;
            Vector2[] uvs = _uvBuffer;
            int[] triangles = _triangleBuffer;

            int triangleCount = 0;
            int vertexIndex = 0;
            float waterLevelWorld = waterLevel;

            // The entire grid is written so that XZ positions align perfectly with
            // terrain vertices (unique index per grid point). Unused dry vertices
            // are simply not referenced by any triangle.
            for (int z = 0; z < vpl; z++)
            {
                for (int x = 0; x < vpl; x++)
                {
                    float localX = x * vertexSpacing;
                    float localZ = z * vertexSpacing;

                    float worldX = worldOrigin.x + localX;
                    float worldZ = worldOrigin.z + localZ;

                    vertices[vertexIndex] = new Vector3(localX, 0f, localZ);
                    uvs[vertexIndex] = new Vector2(worldX, worldZ);

                    if (x < vpl - 1 && z < vpl - 1)
                    {
                        float avgHeight = heightMap.heights[x, z]
                            + heightMap.heights[x + 1, z]
                            + heightMap.heights[x, z + 1]
                            + heightMap.heights[x + 1, z + 1];
                        avgHeight *= 0.25f;

                        // Use the minimum corner height: a cell counts as water when
                        // ANY corner dips below the water line. This guarantees no
                        // holes in water coverage even across shorelines.
                        float minHeight = Mathf.Min(
                            Mathf.Min(heightMap.heights[x, z], heightMap.heights[x + 1, z]),
                            Mathf.Min(heightMap.heights[x, z + 1], heightMap.heights[x + 1, z + 1]));

                        if (minHeight < waterLevelWorld)
                        {
                            int a = vertexIndex;
                            int b = vertexIndex + vpl;
                            int c = vertexIndex + vpl + 1;
                            int d = vertexIndex + 1;

                            triangles[triangleCount++] = a;
                            triangles[triangleCount++] = b;
                            triangles[triangleCount++] = c;

                            triangles[triangleCount++] = a;
                            triangles[triangleCount++] = c;
                            triangles[triangleCount++] = d;
                        }
                    }

                    vertexIndex++;
                }
            }

            Mesh mesh = new Mesh
            {
                name = "Water Chunk Mesh"
            };

            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);

            if (triangleCount > 0)
            {
                mesh.SetTriangles(triangles, 0, triangleCount, 0);
            }

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}