using System;
using UnityEngine;
using JayFos.World;

namespace JayFos.Terrain
{
    public static class HeightMapPool
    {
        private const int PoolCapacity = 128;

        [ThreadStatic]
        private static HeightMap[] _pool;

        [ThreadStatic]
        private static int _usageCount;

        [ThreadStatic]
        private static int _availableCount;

        public static int AvailableReturnCount => _availableCount;

        public static HeightMap Get()
        {
            if (_pool == null)
                _pool = new HeightMap[PoolCapacity];

            if (_availableCount > 0 && _usageCount >= PoolCapacity)
            {
                int start = _usageCount % PoolCapacity;
                for (int i = 0; i < PoolCapacity; i++)
                {
                    int idx = (start + i) % PoolCapacity;
                    var hm = _pool[idx];
                    if (hm != null)
                    {
                        _pool[idx] = null;
                        _availableCount--;
                        return hm;
                    }
                }
            }

            int slotIndex = _usageCount % PoolCapacity;
            var map = _pool[slotIndex];

            if (map == null)
            {
                map = new HeightMap(256, float.MinValue, 0);
                _pool[slotIndex] = map;
            }

            return map;
        }

        public static HeightMap Get(WorldSettings settings)
        {
            if (settings == null)
                return Get();

            int size = Mathf.Max(settings.verticesPerLine, 1);

            if (size > PoolCapacity * 2)
                throw new ArgumentOutOfRangeException(nameof(size),
                    $"Pool capacity is {PoolCapacity}, requested map size ({size}) exceeds maximum.");

            if (_availableCount > 0 && _usageCount >= PoolCapacity)
            {
                int start = _usageCount % PoolCapacity;
                for (int i = 0; i < PoolCapacity; i++)
                {
                    int idx = (start + i) % PoolCapacity;
                    var hm = _pool[idx];
                    if (hm != null)
                    {
                        _pool[idx] = null;
                        _availableCount--;
                        return hm;
                    }
                }
            }

            int slotIndex = _usageCount % PoolCapacity;
            var map = _pool[slotIndex];

            if (map == null)
            {
                float minHeight = settings.terrainHeightThreshold;
                float maxHeight = settings.waterLevel + 5f;
                map = new HeightMap(size, minHeight, maxHeight);
                _pool[slotIndex] = map;
            }

            return map;
        }

        public static void Return(HeightMap map)
        {
            if (map == null || _pool == null) return;

            for (int i = 0; i < PoolCapacity; i++)
            {
                if (_pool[i] == null)
                {
                    Array.Clear(map.heights, 0, map.heights.Length);
                    _pool[i] = map;
                    _availableCount++;
                    return;
                }
            }
        }

        public static void Init()
        {
        }

        public static int AvailableSlots => _pool != null ? PoolCapacity : 0;
    }
}
