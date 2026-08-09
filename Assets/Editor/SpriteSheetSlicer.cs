#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

public class SpriteSheetSlicer : EditorWindow
{
    [MenuItem("Tools/Slice Frame Contact Sheet")]
    public static void SliceSheet()
    {
        string path = "Assets/FrameContactSheet.png";
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError("FrameContactSheet.png not found at Assets/FrameContactSheet.png");
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100;

        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null)
        {
            Debug.LogError("Failed to load texture at " + path);
            return;
        }

        int cols = 8;
        int rows = 6;
        int sliceWidth = tex.width / cols;
        int sliceHeight = tex.height / rows;

        System.Collections.Generic.List<SpriteMetaData> metas = new System.Collections.Generic.List<SpriteMetaData>();

        int index = 0;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (index >= 48) break; // 43 frames + padding
                SpriteMetaData smd = new SpriteMetaData();
                smd.name = $"FrameContactSheet_{index}";
                // Unity rect origin is bottom-left
                smd.rect = new Rect(c * sliceWidth, (rows - 1 - r) * sliceHeight, sliceWidth, sliceHeight);
                smd.alignment = (int)SpriteAlignment.Center;
                metas.Add(smd);
                index++;
            }
        }

        importer.spritesheet = metas.ToArray();
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        Debug.Log("Successfully sliced FrameContactSheet into " + metas.Count + " sprites!");
    }
}
#endif
