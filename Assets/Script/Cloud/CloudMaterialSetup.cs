#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace JayFos.Cloud.Editor
{
    public static class CloudMaterialSetup
    {
        [MenuItem("World/Create Cloud Material")]
        public static void CreateCloudMaterial()
        {
            Shader shader = Shader.Find("JayFos/Clouds/ProceduralCloud");
            if (shader == null)
            {
                Debug.LogError("ProceduralCloud shader not found");
                return;
            }

            Material mat = new Material(shader);
            mat.name = "CloudMat";
            mat.SetColor("_CloudColor", new Color(0.95f, 0.97f, 1f, 1f));
            mat.SetColor("_CloudShadowColor", new Color(0.6f, 0.65f, 0.75f, 1f));
            mat.SetFloat("_Opacity", 0.85f);
            mat.SetFloat("_SoftEdge", 0.5f);
            mat.SetFloat("_NoiseScale", 0.3f);
            mat.SetFloat("_HeightFade", 0.3f);

            string path = "Assets/Script/Materials/Clouds";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder("Assets/Script/Materials", "Clouds");
            }

            AssetDatabase.CreateAsset(mat, $"{path}/CloudMat.mat");
            AssetDatabase.SaveAssets();
            Debug.Log("Cloud material created at " + path + "/CloudMat.mat");
        }
    }
}
#endif