using UnityEngine;
using System.Collections.Generic;

namespace JayFos.Cloud
{
    public class CloudRenderer : MonoBehaviour
    {
        private Material cloudMaterial;
        private Mesh cloudMesh;

        private readonly List<CloudObject> pool = new List<CloudObject>();
        private int activeCount;

        private const int INITIAL_POOL = 128;

        // Cached MaterialPropertyBlock for cloud shadow darkening (zero per-frame allocation).
        private MaterialPropertyBlock shadowBlock;
        private int shadowIntensityId;

        private struct CloudObject
        {
            public GameObject go;
            public MeshRenderer renderer;
        }

        public void Initialize(Material material, Mesh mesh)
        {
            cloudMaterial = material;
            cloudMesh = mesh;
            shadowBlock = new MaterialPropertyBlock();
            shadowIntensityId = Shader.PropertyToID("_ShadowIntensity");

            for (int i = 0; i < INITIAL_POOL; i++)
            {
                pool.Add(CreateCloudObject());
            }
        }

        /// <summary>
        /// Applies a uniform cloud-coverage shadow multiplier to every active cloud renderer
        /// via a single cached MaterialPropertyBlock. Does NOT instantiate materials.
        /// </summary>
        public void SetCloudShadow(float intensity)
        {
            if (shadowBlock == null || activeCount == 0)
                return;

            shadowBlock.SetFloat(shadowIntensityId, intensity);
            for (int i = 0; i < activeCount; i++)
            {
                pool[i].renderer.SetPropertyBlock(shadowBlock);
            }
        }

        private CloudObject CreateCloudObject()
        {
            GameObject go = new GameObject("Cloud");
            go.transform.SetParent(transform, false);
            go.layer = gameObject.layer;

            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.mesh = cloudMesh;

            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.material = cloudMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            go.SetActive(false);

            return new CloudObject { go = go, renderer = mr };
        }

        public void SetClouds(List<CloudData> clouds)
        {
            activeCount = clouds.Count;

            while (pool.Count < activeCount)
            {
                pool.Add(CreateCloudObject());
            }

            for (int i = 0; i < activeCount; i++)
            {
                CloudData cloud = clouds[i];
                CloudObject obj = pool[i];

                obj.go.transform.position = cloud.position;
                obj.go.transform.rotation = Quaternion.Euler(0, cloud.rotation, 0);
                obj.go.transform.localScale = new Vector3(cloud.scale, 1f, cloud.scale);
                obj.go.SetActive(true);
            }

            for (int i = activeCount; i < pool.Count; i++)
            {
                pool[i].go.SetActive(false);
            }
        }

        public void Cleanup()
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i].go != null)
                    pool[i].go.SetActive(false);
            }
            activeCount = 0;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i].go != null)
                    Destroy(pool[i].go);
            }
            pool.Clear();
        }
    }

    public struct CloudData
    {
        public Vector3 position;
        public float scale;
        public float rotation;
        public float opacity;
        public float softness;
    }
}
