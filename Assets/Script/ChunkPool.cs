using System.Collections.Generic;
using UnityEngine;

namespace JayFos.Terrain
{
    public class ChunkPool
    {
        private readonly Stack<Chunk> pool = new Stack<Chunk>();
        private readonly Transform parent;
        private readonly JayFos.World.WorldSettings settings;

        public ChunkPool(Transform parent, JayFos.World.WorldSettings settings)
        {
            this.parent = parent;
            this.settings = settings;
        }

        public Chunk Get(Vector2Int coord, TerrainGenerator terrainGenerator)
        {
            Chunk chunk;

            if (pool.Count > 0)
            {
                chunk = pool.Pop();
                chunk.OnActivate(coord, settings, terrainGenerator);
            }
            else
            {
                chunk = new Chunk(coord, parent, settings, terrainGenerator);
            }

            return chunk;
        }

        public void Release(Chunk chunk)
        {
            chunk.Deactivate();
            pool.Push(chunk);
        }
    }
}
