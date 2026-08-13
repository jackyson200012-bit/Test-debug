using UnityEngine;
using System.Collections.Generic;

namespace JayFos.Cloud
{
    public class CloudManager
    {
        private readonly CloudSettings settings;
        private readonly CloudNoiseGenerator noiseGenerator;
        private readonly CloudRenderer renderer;
        private readonly GameObject rendererObject;

        private readonly List<CloudData> activeClouds;
        private readonly Dictionary<Vector2Int, List<CloudData>> cloudCells;

        private Transform cameraTransform;
        private Vector2Int lastCameraCell;
        private float windOffsetX;
        private float windOffsetZ;

        public CloudManager(CloudSettings settings, int worldSeed, Transform parent)
        {
            this.settings = settings;
            noiseGenerator = new CloudNoiseGenerator(worldSeed, settings.cloudSeedOffset);

            Mesh cloudMesh = CloudMeshGenerator.GenerateCloudChunk(
                1f, settings.cloudHeight / settings.cloudScale, 4);

            rendererObject = new GameObject("CloudRenderer");
            if (parent != null)
                rendererObject.transform.SetParent(parent);
            rendererObject.transform.position = Vector3.zero;
            renderer = rendererObject.AddComponent<CloudRenderer>();
            renderer.Initialize(settings.cloudMaterial, cloudMesh);

            activeClouds = new List<CloudData>();
            cloudCells = new Dictionary<Vector2Int, List<CloudData>>();
        }

        public void SetCamera(Transform camera)
        {
            cameraTransform = camera;
            if (cameraTransform != null)
            {
                lastCameraCell = GetCameraCell();
                UpdateActiveClouds();
            }
        }

        public void Update(float deltaTime)
        {
            if (cameraTransform == null)
                return;

            if (!settings.cloudEnabled)
                return;

            windOffsetX += settings.cloudWindDirection.x * settings.cloudSpeed * deltaTime;
            windOffsetZ += settings.cloudWindDirection.y * settings.cloudSpeed * deltaTime;

            Vector2Int currentCell = GetCameraCell();

            if (currentCell != lastCameraCell)
            {
                lastCameraCell = currentCell;
                UpdateActiveClouds();
            }

            ApplyWindOffset();
            renderer.SetClouds(activeClouds);
        }

        public void SetCoverage(float coverage)
        {
            settings.cloudCoverage = coverage;
        }

        public float CurrentCoverage => settings.cloudCoverage;

        public CloudRenderer Renderer => renderer;

        private Vector2Int GetCameraCell()
        {
            Vector3 pos = cameraTransform.position;
            int cellSize = (int)settings.cloudCellSize;
            int cx = Mathf.FloorToInt(pos.x / cellSize);
            int cz = Mathf.FloorToInt(pos.z / cellSize);
            return new Vector2Int(cx, cz);
        }

        private void UpdateActiveClouds()
        {
            activeClouds.Clear();

            int cellRadius = Mathf.CeilToInt(settings.cloudRenderDistance / settings.cloudCellSize);
            float cellSize = settings.cloudCellSize;

            for (int dx = -cellRadius; dx <= cellRadius; dx++)
            {
                for (int dz = -cellRadius; dz <= cellRadius; dz++)
                {
                    int cellX = lastCameraCell.x + dx;
                    int cellZ = lastCameraCell.y + dz;
                    Vector2Int cellKey = new Vector2Int(cellX, cellZ);

                    float cellWorldX = cellX * cellSize;
                    float cellWorldZ = cellZ * cellSize;

                    float dist = Vector2.Distance(
                        new Vector2(cellWorldX + cellSize * 0.5f, cellWorldZ + cellSize * 0.5f),
                        new Vector2(cameraTransform.position.x, cameraTransform.position.z));

                    if (dist > settings.cloudRenderDistance)
                        continue;

                    GenerateCloudsForCell(cellKey, cellWorldX, cellWorldZ, cellSize);
                }
            }

        }

        private void GenerateCloudsForCell(Vector2Int cellKey, float cellOriginX, float cellOriginZ, float cellSize)
        {
            if (cloudCells.ContainsKey(cellKey))
            {
                activeClouds.AddRange(cloudCells[cellKey]);
                return;
            }

            List<CloudData> cellClouds = new List<CloudData>();

            for (int i = 0; i < settings.cloudsPerCell; i++)
            {
                float sampleX = cellOriginX + noiseGenerator.Hash01(cellKey.x * 17 + i * 31, cellKey.y * 13 + i * 47) * cellSize;
                float sampleZ = cellOriginZ + noiseGenerator.Hash01(cellKey.x * 23 + i * 53, cellKey.y * 19 + i * 61) * cellSize;

                float presence = noiseGenerator.GetCloudPresence(
                    sampleX, sampleZ,
                    settings.cloudCoverage,
                    settings.noiseScale,
                    settings.octaves,
                    settings.persistence,
                    settings.lacunarity);

                if (presence < 0.1f)
                    continue;

                float scaleVariation = noiseGenerator.Hash01(cellKey.x + i * 7, cellKey.y + i * 11);
                float cloudScale = settings.cloudScale * (0.6f + scaleVariation * 0.8f);

                float rotation = noiseGenerator.Hash01(cellKey.x + i * 3, cellKey.y + i * 5) * 360f;

                float heightVariation = noiseGenerator.Hash01(cellKey.x + i * 13, cellKey.y + i * 17);
                float cloudY = settings.cloudAltitude + (heightVariation - 0.5f) * settings.cloudHeight;

                CloudData cloud = new CloudData
                {
                    position = new Vector3(sampleX, cloudY, sampleZ),
                    scale = cloudScale,
                    rotation = rotation,
                    opacity = presence * settings.cloudOpacity,
                    softness = settings.cloudSoftness
                };

                cellClouds.Add(cloud);
                activeClouds.Add(cloud);
            }

            cloudCells[cellKey] = cellClouds;
        }

        private void ApplyWindOffset()
        {
            for (int i = 0; i < activeClouds.Count; i++)
            {
                CloudData cloud = activeClouds[i];
                cloud.position.x += windOffsetX;
                cloud.position.z += windOffsetZ;
                activeClouds[i] = cloud;
            }
        }

        public void Cleanup()
        {
            renderer.Cleanup();
            if (rendererObject != null)
                Object.Destroy(rendererObject);
            activeClouds.Clear();
            cloudCells.Clear();
        }
    }
}
