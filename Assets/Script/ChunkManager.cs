using System.Collections.Generic;
using UnityEngine;
using JayFos.World;
using JayFos.Foliage;
using JayFos.Biomes;

namespace JayFos.Terrain
{
    public class ChunkManager
    {
        private readonly WorldSettings settings;
        private readonly TerrainGenerator terrainGenerator;
        private readonly ChunkPool chunkPool;
        private readonly FoliagePool foliagePool;
        private BiomeMap biomeMap;

        private readonly Dictionary<Vector2Int, Chunk> activeChunks =
            new Dictionary<Vector2Int, Chunk>();

        private readonly HashSet<Vector2Int> _scratchCoords = new HashSet<Vector2Int>();
        private readonly List<Vector2Int> _toRemove = new List<Vector2Int>();
        private readonly List<Vector2Int> _coordsBuffer = new List<Vector2Int>();

        public ChunkManager(
            Transform parent,
            WorldSettings settings,
            TerrainGenerator terrainGenerator)
        {
            this.settings = settings;
            this.terrainGenerator = terrainGenerator;
            chunkPool = new ChunkPool(parent, settings);
            foliagePool = new FoliagePool();
            foliagePool.SetWorldSettings(settings);
        }

        public void SetBiomeMap(BiomeMap biomeMap)
        {
            this.biomeMap = biomeMap;
            terrainGenerator.SetBiomeMap(biomeMap);

            for (int i = 0; i < activeChunks.Count; i++)
            {
                var kvp = GetKeyValue(activeChunks, i);
                kvp.Value.SetBiomeMap(biomeMap);
            }
        }

        public void UpdateChunks(Vector2Int centerCoord)
        {
            GetCoordsInRange(centerCoord, settings.viewDistance, _scratchCoords);

            _toRemove.Clear();
            var keys = new List<Vector2Int>(activeChunks.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                if (!_scratchCoords.Contains(keys[i]))
                {
                    _toRemove.Add(keys[i]);
                }
            }

            for (int i = 0; i < _toRemove.Count; i++)
            {
                var coord = _toRemove[i];
                chunkPool.Release(activeChunks[coord]);
                activeChunks.Remove(coord);
            }

            var coordsList = _coordsBuffer;
            coordsList.Clear();
            foreach (var coord in _scratchCoords)
            {
                coordsList.Add(coord);
            }

            for (int i = 0; i < coordsList.Count; i++)
            {
                var coord = coordsList[i];
                if (!activeChunks.ContainsKey(coord))
                {
                    Chunk chunk = chunkPool.Get(coord, terrainGenerator);
                    if (biomeMap != null)
                        chunk.SetBiomeMap(biomeMap);
                    activeChunks[coord] = chunk;
                }
            }
        }

        private static KeyValuePair<Vector2Int, Chunk> GetKeyValue(Dictionary<Vector2Int, Chunk> dict, int index)
        {
            var enumerator = dict.GetEnumerator();
            for (int i = 0; i <= index; i++)
            {
                enumerator.MoveNext();
            }
            return enumerator.Current;
        }

        private void GetCoordsInRange(Vector2Int center, int radius, HashSet<Vector2Int> result)
        {
            result.Clear();

            int startX = center.x - radius;
            int endX = center.x + radius;
            int startZ = center.y - radius;
            int endZ = center.y + radius;

            for (int x = startX; x <= endX; x++)
            {
                for (int z = startZ; z <= endZ; z++)
                {
                    result.Add(new Vector2Int(x, z));
                }
            }
        }
    }
}
