using System.Collections.Generic;
using UnityEngine;
using JayFos.World;

namespace JayFos.Foliage
{
    public class FoliagePool
    {
        private static FoliagePool _instance;

        public static FoliagePool Instance
        {
            get => _instance;
            set => _instance = value;
        }

        private readonly Stack<GameObject> pool = new Stack<GameObject>(64);
        private WorldSettings settings;
        private int activeCount;

        private readonly List<Transform> _transformBuffer = new List<Transform>(16);

        public void SetWorldSettings(WorldSettings s)
        {
            settings = s;
            _instance = this;
        }

        public GameObject GetPooledInstance(Vector3 worldPosition, int foliageType)
        {
            if (pool.Count > 0)
            {
                var existing = pool.Pop();

                _transformBuffer.Clear();
                for (int i = 0; i < existing.transform.childCount; i++)
                {
                    _transformBuffer.Add(existing.transform.GetChild(i));
                }
                for (int i = 0; i < _transformBuffer.Count; i++)
                {
                    _transformBuffer[i].gameObject.SetActive(false);
                }

                existing.transform.SetPositionAndRotation(worldPosition, Quaternion.identity);
                existing.SetActive(true);
                activeCount++;
                return existing;
            }

            GameObject instance = null;
            if (settings != null)
            {
                if (foliageType >= 0 && settings.spawnRules != null && foliageType < settings.spawnRules.Length)
                {
                    var rule = settings.spawnRules[foliageType];
                    if (rule.prefab != null)
                        instance = GameObject.Instantiate(rule.prefab, worldPosition, Quaternion.identity);
                }
            }

            if (instance == null) return null;
            instance.SetActive(true);
            activeCount++;
            return instance;
        }

        public void ReleaseInstance(GameObject instance)
        {
            if (instance == null) return;

            _transformBuffer.Clear();
            for (int i = 0; i < instance.transform.childCount; i++)
            {
                _transformBuffer.Add(instance.transform.GetChild(i));
            }
            for (int i = 0; i < _transformBuffer.Count; i++)
            {
                _transformBuffer[i].gameObject.SetActive(false);
            }

            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.SetActive(false);
            pool.Push(instance);
            activeCount--;
        }

        public int GetActiveCount() => activeCount;
        public int GetPoolSize() => pool.Count;
    }
}
