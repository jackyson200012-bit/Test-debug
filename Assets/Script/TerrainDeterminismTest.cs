using UnityEngine;
using JayFos.World;

namespace JayFos.Terrain
{
    public static class TerrainDeterminismTest
    {
        public static bool TestHeightMapDeterminism(WorldSettings settings, TerrainGenerator gen)
        {
            Vector2Int coord = new Vector2Int(0, 0);

            HeightMap hm1 = gen.GenerateHeightMap(coord);
            HeightMap hm2 = gen.GenerateHeightMap(coord);

            bool match = (hm1.width == hm2.width && hm1.depth == hm2.depth) && CompareHeightData(hm1.heights, hm2.heights);

            Debug.Log($"[DeterminismTest] HeightMap same-seed test: {(match ? "PASS" : "FAIL")}");
            return match;
        }

        public static bool TestSeedVariation(WorldSettings settings, TerrainGenerator gen)
        {
            Vector2Int coord = new Vector2Int(0, 0);

            HeightMap hmA = gen.GenerateHeightMap(coord);

            settings.seed = settings.seed + 1;
            HeightMap hmB = gen.GenerateHeightMap(coord);

            bool differs = !CompareHeightData(hmA.heights, hmB.heights);

            Debug.Log($"[DeterminismTest] Seed variation test: {(differs ? "PASS" : "FAIL")}");
            return differs;
        }

        public static bool TestMeshDeterminism(WorldSettings settings, TerrainGenerator gen)
        {
            Vector2Int coord = new Vector2Int(0, 0);

            HeightMap hmA = gen.GenerateHeightMap(coord);
            Mesh meshA = MeshGenerator.Generate(hmA, settings);
            Vector3[] vertsA = meshA.vertices;

            settings.seed++;
            HeightMap hmB = gen.GenerateHeightMap(coord);
            Mesh meshB = MeshGenerator.Generate(hmB, settings);
            Vector3[] vertsB = meshB.vertices;

            bool sameSeed = CompareMeshes(meshA, meshB);

            settings.seed--;
            HeightMap hmAA = gen.GenerateHeightMap(coord);
            Mesh meshAA = MeshGenerator.Generate(hmAA, settings);
            Vector3[] vertsAA = meshAA.vertices;

            bool differentSeed = !CompareMeshes(meshA, meshAA);

            bool total = sameSeed && differentSeed;
            Debug.Log($"[DeterminismTest] Full mesh pipeline: {(total ? "PASS" : "FAIL")}");
            return total;
        }

        private static bool CompareHeightData(float[,] a, float[,] b)
        {
            if (a == null || b == null) return false;
            if (a.GetLength(0) != b.GetLength(0) || a.GetLength(1) != b.GetLength(1)) return false;
            for (int x = 0; x < a.GetLength(0); x++)
            {
                for (int z = 0; z < a.GetLength(1); z++)
                {
                    if (Mathf.Abs(a[x, z] - b[x, z]) > Mathf.Epsilon) return false;
                }
            }
            return true;
        }

        private static bool CompareMeshes(Mesh a, Mesh b)
        {
            if (a == null || b == null) return false;

            Vector3[] va = a.vertices;
            Vector3[] vb = b.vertices;

            if (va.Length != vb.Length) return false;
            for (int i = 0; i < va.Length; i++)
            {
                if ((va[i] - vb[i]).sqrMagnitude > Mathf.Epsilon * 100) return false;
            }
            return true;
        }

        public static void RunAllTests(WorldSettings settings, TerrainGenerator gen)
        {
            if (settings == null || gen == null)
            {
                Debug.LogWarning("[DeterminismTest] Settings or TerrainGenerator is null — tests skipped.");
                return;
            }

            bool heightMapOk = TestHeightMapDeterminism(settings, gen);
            bool seedVarOk = TestSeedVariation(settings, gen);
            bool meshOk = TestMeshDeterminism(settings, gen);

            Debug.Log($"Results: HeightMap={heightMapOk}, SeedVariation={seedVarOk}, MeshPipeline={meshOk}");
        }
    }
}
