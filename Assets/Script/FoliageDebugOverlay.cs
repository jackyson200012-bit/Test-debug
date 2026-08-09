using UnityEngine;
using JayFos.World;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace JayFos.Terrain
{
    /// <summary>
    /// Debug overlay for foliage generation and terrain visualization.
    /// Draws spawn points, noise contours, slope indicators, and statistics using Unity Gizmos.
    /// Only functional in the Unity Editor (uses UnityEditor types for label styling).
    /// </summary>
    public class FoliageDebugOverlay : MonoBehaviour
    {
        [Header("Debug Settings")]
        public bool enabled = false;
        public bool showSpawnPoints = true;
        public bool showSlopeVisualization = true;
        public bool showNoiseMap = true;
        public bool showChunkBounds = true;

        [Range(4, 32)]
        public int debugResolution = 16;

        private GUIStyle _whiteLabelStyle;
        private GUIStyle _grayLabelStyle;
        private GUIStyle _cyanLabelStyle;
        private GUIStyle _yellowLabelStyle;

        [Header("World Settings Reference")]
        public WorldSettings manualWorldSettings;

        private void OnEnable()
        {
#if UNITY_EDITOR
            InitStyles();
#endif
        }

        private void OnDrawGizmos()
        {
            if (!enabled) return;

            var settings = FindWorldSettings();
            if (settings == null)
            {
#if UNITY_EDITOR
                Handles.Label(transform.position + Vector3.up * 3f, "Foliage Debug: No WorldSettings found");
#endif
                return;
            }

            var noiseGen = new NoiseGenerator(settings);
            float spawnThreshold = settings.spawnThreshold;
            float waterLevel = settings.waterLevel;
            int chunkSize = settings.chunkSize;

            if (showChunkBounds)
            {
                DrawChunkBoundsGrid(settings, chunkSize);
            }

            if (showSpawnPoints || showNoiseMap)
            {
                DrawNoiseVisualization(transform.position, chunkSize, noiseGen, settings, spawnThreshold, waterLevel);
            }

            DrawStatistics(settings);

#if UNITY_EDITOR
            Handles.Label(transform.position + Vector3.up * 5f, "Foliage Debug: Active", GetWhiteLabelStyle());
#endif
        }

        private void OnDrawGizmosSelected()
        {
            if (!enabled) return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 5f);
        }

        private void DrawChunkBoundsGrid(WorldSettings settings, int chunkSize)
        {
            Vector3 origin = transform.position;
            float radius = 50f;

            int chunkX = Mathf.FloorToInt(origin.x / chunkSize);
            int chunkZ = Mathf.FloorToInt(origin.z / chunkSize);

            int chunksToDraw = Mathf.CeilToInt(radius / chunkSize) + 1;

            for (int dx = -chunksToDraw; dx <= chunksToDraw; dx++)
            {
                for (int dz = -chunksToDraw; dz <= chunksToDraw; dz++)
                {
                    int cx = chunkX + dx;
                    int cz = chunkZ + dz;

                    Vector3 minPos = new Vector3(cx * chunkSize, 0f, cz * chunkSize);
                    Vector3 maxPos = new Vector3((cx + 1) * chunkSize, 0f, (cz + 1) * chunkSize);

                    Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
                    Gizmos.DrawWireCube((minPos + maxPos) / 2, new Vector3(chunkSize, 0.1f, chunkSize));

                    Vector3 center = new Vector3(minPos.x + chunkSize * 0.5f, 0.5f, minPos.z + chunkSize * 0.5f);
#if UNITY_EDITOR
                    Handles.Label(center, $"[{cx},{cz}]", GetGrayLabelStyle());
#endif
                }
            }
        }

        private void DrawNoiseVisualization(Vector3 center, int chunkSize, NoiseGenerator noiseGen, WorldSettings settings, float spawnThreshold, float waterLevel)
        {
            float resolution = debugResolution;
            float step = (float)chunkSize / resolution;

            HeightMap heightMap = null;
            var chunkCoord = new Vector2Int(Mathf.FloorToInt(center.x / chunkSize), Mathf.FloorToInt(center.z / chunkSize));
            try
            {
                heightMap = CreateHeightMapFromNoise(noiseGen, settings, chunkCoord, chunkSize);
            }
            catch { }

            for (int i = 0; i < resolution; i++)
            {
                for (int j = 0; j < resolution; j++)
                {
                    float worldX = center.x - chunkSize * 0.5f + i * step + step * 0.5f;
                    float worldZ = center.z - chunkSize * 0.5f + j * step + step * 0.5f;

                    float noiseValue = noiseGen.SampleWorldSpaceNoise(worldX, worldZ);

                    float terrainHeight = 0f;
                    if (heightMap != null)
                    {
                        terrainHeight = heightMap.GetHeightAtWorldPosition(worldX, worldZ);
                    }

                    float normalizedNoise = (noiseValue + 1f) / 2f;

                    Color noiseColor;

                    if (terrainHeight < waterLevel)
                    {
                        noiseColor = new Color(0.2f, 0.4f, 0.8f, 0.4f);
                    }
                    else if (normalizedNoise >= spawnThreshold)
                    {
                        noiseColor = new Color(0.2f, 0.8f, 0.2f, 0.5f);
                    }
                    else
                    {
                        noiseColor = new Color(0.8f, 0.6f, 0.2f, 0.3f);
                    }

                    Gizmos.color = noiseColor;
                    Gizmos.DrawWireCube(new Vector3(worldX, 0.1f, worldZ), new Vector3(step * 0.95f, 0f, step * 0.95f));

                    if (showSpawnPoints && normalizedNoise >= spawnThreshold)
                    {
                        Gizmos.color = Color.white;
                        Gizmos.DrawWireSphere(new Vector3(worldX, terrainHeight + 0.5f, worldZ), 0.8f);
                    }
                }
            }

            if (showSlopeVisualization && heightMap != null)
            {
                int stepInterval = Mathf.Max(1, (int)(resolution / 4));
                for (int i = 0; i < resolution; i += stepInterval)
                {
                    for (int j = 0; j < resolution; j += stepInterval)
                    {
                        float worldX = center.x - chunkSize * 0.5f + i * step + step * 0.5f;
                        float worldZ = center.z - chunkSize * 0.5f + j * step + step * 0.5f;

                        float hEast = heightMap.GetHeightAtWorldPosition(worldX + 2f, worldZ);
                        float hWest = heightMap.GetHeightAtWorldPosition(worldX - 2f, worldZ);
                        float slopeAngle = Mathf.Atan(Mathf.Abs(hEast - hWest) / 4f) * Mathf.Rad2Deg;

                        Color lineColor;
                        if (slopeAngle < 20f) lineColor = Color.green;
                        else if (slopeAngle < 45f) lineColor = Color.yellow;
                        else lineColor = Color.red;

                        Gizmos.color = lineColor;
                        Gizmos.DrawLine(new Vector3(worldX, 0.3f, worldZ - 1f), new Vector3(worldX + 1f, 0.3f, worldZ - 1f));
                        Gizmos.DrawLine(new Vector3(worldX, 0.3f, worldZ - 1f), new Vector3(worldX, 0.3f, worldZ + 1f));

                        float th = heightMap.GetHeightAtWorldPosition(worldX, worldZ);
#if UNITY_EDITOR
                        string slopeText = $"S:{slopeAngle:F0}\u00B0";
                        Handles.Label(new Vector3(worldX, th + 2f, worldZ), slopeText, GetColoredLabelStyle(lineColor));
#endif
                    }
                }
            }
        }

        private HeightMap CreateHeightMapFromNoise(NoiseGenerator noiseGen, WorldSettings settings, Vector2Int chunkCoord, int chunkSize)
        {
            int vertsPerLine = settings.verticesPerLine;
            float vertexSpacing = (float)chunkSize / (vertsPerLine - 1);

            float[,] heights = new float[vertsPerLine, vertsPerLine];
            float minHeight = float.MaxValue;
            float maxHeight = float.MinValue;

            for (int z = 0; z < vertsPerLine; z++)
            {
                for (int x = 0; x < vertsPerLine; x++)
                {
                    float worldX = chunkCoord.x * chunkSize + x * vertexSpacing;
                    float worldZ = chunkCoord.y * chunkSize + z * vertexSpacing;

                    float seededWorldX = NoiseGenerator.ComputeSeedOffset(worldX, settings.seed);
                    float seededWorldZ = NoiseGenerator.ComputeSeedOffset(worldZ, settings.seed);

                    float height = SampleNoise(seededWorldX, seededWorldZ, settings);
                    heights[x, z] = height;

                    if (height < minHeight) minHeight = height;
                    if (height > maxHeight) maxHeight = height;
                }
            }

            var map = new HeightMap(heights, chunkCoord, minHeight, maxHeight);
            map.vertexSpacing = vertexSpacing;
            return map;
        }

        private float SampleNoise(float seededWorldX, float seededWorldZ, WorldSettings settings)
        {
            float amplitude = 1f;
            float frequency = 1f;
            float noiseHeight = 0f;

            for (int i = 0; i < settings.octaves; i++)
            {
                float sampleX = seededWorldX * settings.noiseScale * frequency;
                float sampleZ = seededWorldZ * settings.noiseScale * frequency;

                float perlin = Mathf.PerlinNoise(sampleX, sampleZ) * 2f - 1f;
                noiseHeight += perlin * amplitude;

                amplitude *= settings.persistence;
                frequency *= settings.lacunarity;
            }

            return noiseHeight * settings.heightMultiplier;
        }

        private void DrawStatistics(WorldSettings settings)
        {
            Vector3 labelPos = transform.position + Vector3.up * 10f;

            string infoText = $"Seed: {settings.seed} | ChunkSize: {settings.chunkSize} | Verts/Line: {settings.verticesPerLine} | NoiseScale: {settings.noiseScale} | Octaves: {settings.octaves} | Persistence: {settings.persistence:F2}";

#if UNITY_EDITOR
            Handles.Label(labelPos, infoText, GetWhiteLabelStyle());

            labelPos.y += 3f;
            string spawnInfo = $"Spawn Threshold: {settings.spawnThreshold:F2} | Water Level: {settings.waterLevel:F2} | Foliage Density: {settings.foliageDensityMultiplier:F2}";

            Handles.Label(labelPos, spawnInfo, GetCyanLabelStyle());

            labelPos.y += 3f;
            var spawnRules = settings.GetSpawnRules();
            if (spawnRules != null && spawnRules.Length > 0)
            {
                string rulesText = "Spawn Rules: ";
                for (int i = 0; i < spawnRules.Length; i++)
                {
                    var rule = spawnRules[i];
                    rulesText += $"[{i}:{(rule.prefab?.name ?? "null")} h<{rule.maxHeight:F1} s<{rule.maxSlope:F1}>] ";
                }
                Handles.Label(labelPos, rulesText, GetYellowLabelStyle());
            }
#endif
        }

        public string GetDebugInfo()
        {
            return $"[FoliageDebugOverlay] Enabled: {enabled} | Resolution: {debugResolution} " +
                   $"| SpawnPoints: {showSpawnPoints} | Slope: {showSlopeVisualization}";
        }

        private void InitStyles()
        {
            _whiteLabelStyle = CreateLabelStyle(Color.white, 16);
            _grayLabelStyle = CreateLabelStyle(Color.gray, 14);
            _cyanLabelStyle = CreateLabelStyle(Color.cyan, 16);
            _yellowLabelStyle = CreateLabelStyle(Color.yellow, 16);
        }

        private GUIStyle GetWhiteLabelStyle()
        {
            if (_whiteLabelStyle == null) InitStyles();
            return _whiteLabelStyle;
        }

        private GUIStyle GetGrayLabelStyle()
        {
            if (_grayLabelStyle == null) InitStyles();
            return _grayLabelStyle;
        }

        private GUIStyle GetCyanLabelStyle()
        {
            if (_cyanLabelStyle == null) InitStyles();
            return _cyanLabelStyle;
        }

        private GUIStyle GetYellowLabelStyle()
        {
            if (_yellowLabelStyle == null) InitStyles();
            return _yellowLabelStyle;
        }

        private GUIStyle GetColoredLabelStyle(Color color)
        {
#if UNITY_EDITOR
            var style = new GUIStyle(EditorStyles.label);
            style.fontSize = 12;
            style.normal.textColor = color;
            return style;
#else
            return null;
#endif
        }

        private WorldSettings FindWorldSettings()
        {
            if (manualWorldSettings != null) return manualWorldSettings;

            var allSettings = ScriptableObject.FindObjectsOfType<WorldSettings>();
            for (int i = 0; i < allSettings.Length; i++)
            {
                if (allSettings[i] != null) return allSettings[i];
            }

            return null;
        }

        private GUIStyle CreateLabelStyle(Color color, int fontSize)
        {
#if UNITY_EDITOR
            var s = new GUIStyle(EditorStyles.label);
            s.fontSize = fontSize;
            s.normal.textColor = color;
            s.fontStyle = FontStyle.Bold;
            s.alignment = TextAnchor.UpperLeft;
            return s;
#else
            return null;
#endif
        }
    }
}
