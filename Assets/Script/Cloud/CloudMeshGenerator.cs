using UnityEngine;

namespace JayFos.Cloud
{
    public static class CloudMeshGenerator
    {
        private static Mesh _sharedQuadMesh;

        public static Mesh GetQuadMesh()
        {
            if (_sharedQuadMesh != null)
                return _sharedQuadMesh;

            _sharedQuadMesh = new Mesh { name = "CloudQuad" };

            float s = 1f;
            _sharedQuadMesh.vertices = new Vector3[]
            {
                new Vector3(-s, 0, -s),
                new Vector3(-s, 0,  s),
                new Vector3( s, 0,  s),
                new Vector3( s, 0, -s)
            };

            _sharedQuadMesh.uv = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(0, 1),
                new Vector2(1, 1),
                new Vector2(1, 0)
            };

            _sharedQuadMesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
            _sharedQuadMesh.RecalculateNormals();
            _sharedQuadMesh.RecalculateBounds();

            return _sharedQuadMesh;
        }

        public static Mesh GenerateCloudChunk(float scale, float height, int subdivisions)
        {
            subdivisions = Mathf.Max(1, subdivisions);
            int vertCount = (subdivisions + 1) * (subdivisions + 1);
            int triCount = subdivisions * subdivisions * 6;

            Vector3[] vertices = new Vector3[vertCount];
            Vector2[] uvs = new Vector2[vertCount];
            int[] triangles = new int[triCount];

            float halfScale = scale * 0.5f;
            float step = scale / subdivisions;

            int vertIdx = 0;
            for (int z = 0; z <= subdivisions; z++)
            {
                for (int x = 0; x <= subdivisions; x++)
                {
                    float px = -halfScale + x * step;
                    float pz = -halfScale + z * step;
                    float py = 0f;

                    float edgeX = Mathf.Abs(x / (float)subdivisions - 0.5f) * 2f;
                    float edgeZ = Mathf.Abs(z / (float)subdivisions - 0.5f) * 2f;
                    float edgeFactor = Mathf.Max(edgeX, edgeZ);
                    py -= edgeFactor * edgeFactor * height * 0.5f;

                    vertices[vertIdx] = new Vector3(px, py, pz);
                    uvs[vertIdx] = new Vector2(x / (float)subdivisions, z / (float)subdivisions);
                    vertIdx++;
                }
            }

            int triIdx = 0;
            for (int z = 0; z < subdivisions; z++)
            {
                for (int x = 0; x < subdivisions; x++)
                {
                    int a = z * (subdivisions + 1) + x;
                    int b = a + 1;
                    int c = a + (subdivisions + 1);
                    int d = c + 1;

                    triangles[triIdx++] = a;
                    triangles[triIdx++] = c;
                    triangles[triIdx++] = b;

                    triangles[triIdx++] = b;
                    triangles[triIdx++] = c;
                    triangles[triIdx++] = d;
                }
            }

            Mesh mesh = new Mesh { name = "CloudChunk" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}
