using System.Collections.Generic;
using UnityEngine;
using JayFos.World;

namespace JayFos.Terrain
{
    public class Chunk
    {
        private struct FoliageEntry
        {
            public GameObject Instance;
            public bool IsPooled;

            public FoliageEntry(GameObject instance, bool isPooled)
            {
                Instance = instance;
                IsPooled = isPooled;
            }
        }

        private GameObject gameObject;

        public Vector2Int Coord { get; private set; }

        public GameObject GameObject
        {
            get { return gameObject; }
        }

        private readonly MeshFilter meshFilter;
        private readonly MeshRenderer meshRenderer;
        private readonly MeshCollider meshCollider;

        private readonly GameObject waterObject;
        private readonly MeshFilter waterMeshFilter;
        private readonly MeshRenderer waterMeshRenderer;
        private Mesh currentWaterMesh;

        private TerrainGenerator terrainGenerator;
        private JayFos.Foliage.FoliageGenerator foliageGenerator;
        private JayFos.Biomes.BiomeMap biomeMap;

        private Mesh currentMesh;

        private readonly List<JayFos.Foliage.FoliageGenerator.PlacementPoint> _scratchPlacementPoints =
            new List<JayFos.Foliage.FoliageGenerator.PlacementPoint>();

        private readonly List<FoliageEntry> activeFoliage = new List<FoliageEntry>();

        public Chunk(
            Vector2Int coord,
            Transform parent,
            JayFos.World.WorldSettings settings,
            TerrainGenerator terrainGenerator)
        {
            if (settings == null)
                throw new System.ArgumentNullException(nameof(settings));

            Coord = coord;

            gameObject = new GameObject($"Chunk_{coord.x}_{coord.y}");
            gameObject.transform.SetParent(parent, false);

            meshFilter = gameObject.AddComponent<MeshFilter>();
            meshRenderer = gameObject.AddComponent<MeshRenderer>();

            if (settings.generateCollision)
                meshCollider = gameObject.AddComponent<MeshCollider>();

            waterObject = CreateWaterChild(settings);
            waterMeshFilter = waterObject != null ? waterObject.GetComponent<MeshFilter>() : null;
            waterMeshRenderer = waterObject != null ? waterObject.GetComponent<MeshRenderer>() : null;

            // Initialize generators before first update.
            this.terrainGenerator = terrainGenerator ?? new TerrainGenerator(settings);
            foliageGenerator = new JayFos.Foliage.FoliageGenerator(settings);

            UpdateForCoord(coord, settings);
        }

        public void SetBiomeMap(JayFos.Biomes.BiomeMap biomeMap)
        {
            this.biomeMap = biomeMap;
            ForceApplyBiomeMapToGenerators();
        }

        public void OnActivate(
            Vector2Int coord,
            JayFos.World.WorldSettings settings,
            TerrainGenerator terrainGenerator)
        {
            if (settings == null)
                throw new System.ArgumentNullException(nameof(settings));

            Coord = coord;
            gameObject.name = $"Chunk_{coord.x}_{coord.y}";

            EnsureGenerators(settings, terrainGenerator);
            ApplyBiomeMapToGeneratorsIfPresent();

            ClearFoliage();
            _scratchPlacementPoints.Clear();

            UpdateForCoord(coord, settings);

            gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            ClearFoliage();
            ClearWaterMesh();

            // Clear references before destroying the mesh.
            ClearMeshReferences();

            if (currentMesh != null)
            {
                UnityEngine.Object.Destroy(currentMesh);
                currentMesh = null;
            }

            if (gameObject != null)
                gameObject.SetActive(false);
        }

        public void SetVisible(bool visible)
        {
            if (gameObject != null)
                gameObject.SetActive(visible);
        }

        public bool IsVisible()
        {
            return gameObject != null && gameObject.activeSelf;
        }

        public void Destroy()
        {
            ClearFoliage();
            ClearWaterMesh();
            ClearMeshReferences();

            if (currentMesh != null)
            {
                UnityEngine.Object.Destroy(currentMesh);
                currentMesh = null;
            }

            if (gameObject != null)
            {
                UnityEngine.Object.Destroy(gameObject);
                gameObject = null;
            }
        }

        private void EnsureGenerators(
            JayFos.World.WorldSettings settings,
            TerrainGenerator providedTerrainGenerator)
        {
            if (providedTerrainGenerator != null)
            {
                terrainGenerator = providedTerrainGenerator;
            }
            else if (terrainGenerator == null)
            {
                terrainGenerator = new TerrainGenerator(settings);
            }

            if (foliageGenerator == null)
                foliageGenerator = new JayFos.Foliage.FoliageGenerator(settings);
        }

        private void ApplyBiomeMapToGeneratorsIfPresent()
        {
            if (biomeMap == null)
                return;

            ForceApplyBiomeMapToGenerators();
        }

        private void ForceApplyBiomeMapToGenerators()
        {
            if (terrainGenerator != null)
                terrainGenerator.SetBiomeMap(biomeMap);

            if (foliageGenerator != null)
                foliageGenerator.SetBiomeMap(biomeMap);
        }

        private void ClearMeshReferences()
        {
            if (meshFilter != null)
                meshFilter.sharedMesh = null;

            if (meshCollider != null)
                meshCollider.sharedMesh = null;
        }

        private void UpdateForCoord(Vector2Int coord, JayFos.World.WorldSettings settings)
        {
            if (terrainGenerator == null)
                terrainGenerator = new TerrainGenerator(settings);

            gameObject.transform.position = new Vector3(
                coord.x * settings.chunkSize,
                0f,
                coord.y * settings.chunkSize);

            using (var meshScope = TerrainProfiler.ScopedMesh())
            {
                HeightMap heightMap = terrainGenerator.GenerateHeightMap(coord);

                if (heightMap == null)
                    return;

                try
                {
                    JayFos.Biomes.BiomeDefinition chunkBiome = null;

                    if (biomeMap != null && settings.enableBiomeSystem)
                    {
                        chunkBiome = biomeMap.GetBiomeAtChunkCenter(coord.x, coord.y, settings.chunkSize);
                    }

                    Mesh mesh = MeshGenerator.Generate(heightMap, settings, chunkBiome);
                    AssignMesh(mesh);

                    if (meshRenderer != null)
                        meshRenderer.sharedMaterial = settings.terrainMaterial;

                    UpdateWater(coord, heightMap, settings);

                    using (var foliageScope = TerrainProfiler.ScopedFoliage())
                    {
                        GenerateFoliage(coord, heightMap, settings);
                    }
                }
                finally
                {
                    HeightMapPool.Return(heightMap);
                }
            }
        }

        private static GameObject CreateWaterChild(JayFos.World.WorldSettings settings)
        {
            if (settings == null || settings.waterMaterial == null)
                return null;

            var water = new GameObject("Water");
            water.transform.SetParent(null, false);
            water.AddComponent<MeshFilter>();
            water.AddComponent<MeshRenderer>();

            var renderer = water.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = settings.waterMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return water;
        }

        private void UpdateWater(Vector2Int coord, HeightMap heightMap, JayFos.World.WorldSettings settings)
        {
            if (waterObject == null || waterMeshFilter == null || settings == null)
                return;

            bool chunkHasWater = heightMap != null && heightMap.minHeight < settings.waterLevel;

            if (!chunkHasWater)
            {
                ClearWaterMesh();
                if (waterObject.activeSelf)
                    waterObject.SetActive(false);
                return;
            }

            if (heightMap == null)
                return;

            Vector3 worldOrigin = new Vector3(
                coord.x * settings.chunkSize,
                0f,
                coord.y * settings.chunkSize);

            var waterMesh = WaterMeshGenerator.Generate(
                heightMap,
                settings.chunkSize,
                settings.verticesPerLine,
                settings.waterLevel,
                worldOrigin);

            if (currentWaterMesh != null && currentWaterMesh != waterMesh)
            {
                UnityEngine.Object.Destroy(currentWaterMesh);
            }

            currentWaterMesh = waterMesh;
            waterMeshFilter.sharedMesh = currentWaterMesh;

            waterObject.transform.SetParent(gameObject.transform, false);
            waterObject.transform.localPosition = new Vector3(0f, settings.waterLevel, 0f);

            if (!waterObject.activeSelf)
                waterObject.SetActive(true);
        }

        private void ClearWaterMesh()
        {
            if (waterMeshFilter != null)
                waterMeshFilter.sharedMesh = null;

            if (currentWaterMesh != null)
            {
                UnityEngine.Object.Destroy(currentWaterMesh);
                currentWaterMesh = null;
            }
        }

        private void AssignMesh(Mesh newMesh)
        {
            if (currentMesh != null && currentMesh != newMesh)
            {
                ClearMeshReferences();
                UnityEngine.Object.Destroy(currentMesh);
            }

            currentMesh = newMesh;

            if (meshFilter != null)
                meshFilter.sharedMesh = currentMesh;

            if (meshCollider != null)
                meshCollider.sharedMesh = currentMesh;
        }

        private void GenerateFoliage(
            Vector2Int coord,
            HeightMap heightMap,
            JayFos.World.WorldSettings settings)
        {
            if (foliageGenerator == null || heightMap == null || settings == null)
                return;

            _scratchPlacementPoints.Clear();

            var placementPoints = foliageGenerator.Generate(coord, heightMap, _scratchPlacementPoints);
            if (placementPoints == null)
                return;

            // Clear existing foliage before spawning new foliage.
            ClearFoliage();

            JayFos.Foliage.FoliageSpawnRule[] rules = settings.GetSpawnRules();

            for (int i = 0; i < placementPoints.Count; i++)
            {
                var point = placementPoints[i];

                if (!point.IsValid())
                    continue;

                bool isPooled;
                GameObject foliageGO = GetOrCreateFoliageInstance(point, rules, out isPooled);

                if (foliageGO == null)
                    continue;

                if (!foliageGO.activeSelf)
                    foliageGO.SetActive(true);

                Transform foliageTransform = foliageGO.transform;

                // Parent first, then set world position.
                foliageTransform.SetParent(gameObject.transform, false);
                foliageTransform.position = point.WorldPosition;

                ApplyFoliageMaterial(foliageGO, rules, point.FoliageType);

                activeFoliage.Add(new FoliageEntry(foliageGO, isPooled));
            }
        }

        private void ApplyFoliageMaterial(
            GameObject foliageGO,
            JayFos.Foliage.FoliageSpawnRule[] rules,
            int foliageType)
        {
            if (foliageGO == null || rules == null)
                return;

            if (foliageType < 0 || foliageType >= rules.Length)
                return;

            GameObject prefab = rules[foliageType].prefab;
            if (prefab == null)
                return;

            MeshRenderer renderer = foliageGO.GetComponent<MeshRenderer>();
            if (renderer == null)
                return;

            MeshRenderer prefabRenderer = prefab.GetComponent<MeshRenderer>();
            if (prefabRenderer != null)
                renderer.sharedMaterial = prefabRenderer.sharedMaterial;
        }

        private GameObject GetOrCreateFoliageInstance(
            JayFos.Foliage.FoliageGenerator.PlacementPoint point,
            JayFos.Foliage.FoliageSpawnRule[] rules,
            out bool isPooled)
        {
            isPooled = false;

            var pool = JayFos.Foliage.FoliagePool.Instance;

            if (pool != null)
            {
                GameObject pooledInstance = pool.GetPooledInstance(point.WorldPosition, point.FoliageType);

                if (pooledInstance != null)
                {
                    isPooled = true;
                    return pooledInstance;
                }
            }

            // Fallback creation if the pool did not provide an instance.
            // If your FoliagePool is intended to be the only creator, you can remove this fallback block.
            if (rules != null && point.FoliageType >= 0 && point.FoliageType < rules.Length)
            {
                GameObject prefab = rules[point.FoliageType].prefab;

                if (prefab != null)
                    return UnityEngine.Object.Instantiate(prefab);
            }

            return null;
        }

        private void ClearFoliage()
        {
            if (activeFoliage.Count == 0)
                return;

            var pool = JayFos.Foliage.FoliagePool.Instance;

            for (int i = 0; i < activeFoliage.Count; i++)
            {
                FoliageEntry entry = activeFoliage[i];

                if (entry.Instance == null)
                    continue;

                if (entry.IsPooled && pool != null)
                {
                    pool.ReleaseInstance(entry.Instance);
                }
                else
                {
                    UnityEngine.Object.Destroy(entry.Instance);
                }
            }

            activeFoliage.Clear();
        }
    }
}