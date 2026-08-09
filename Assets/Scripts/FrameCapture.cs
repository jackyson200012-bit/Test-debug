using System.Collections;
using System.IO;
using UnityEngine;

public class FrameCapture : MonoBehaviour
{
    public string clipStateName = "Play";
    public int step = 30;
    public int totalFrames = 1277;
    public int renderWidth = 1024;
    public int renderHeight = 1024;
    public string outputFolder;
    public string doneMarker;
    public Camera captureCamera;

    IEnumerator Start()
    {
        if (string.IsNullOrEmpty(outputFolder))
            outputFolder = Path.Combine(Application.dataPath, "..", "Temp", "FrameCapture");
        if (string.IsNullOrEmpty(doneMarker))
            doneMarker = Path.Combine(outputFolder, "done.txt");

        if (File.Exists(doneMarker)) File.Delete(doneMarker);
        Directory.CreateDirectory(outputFolder);

        var anim = GetComponent<Animator>();
        if (anim == null)
        {
            Debug.LogError("FrameCapture: no Animator found");
            yield break;
        }
        anim.applyRootMotion = false;

        if (captureCamera == null) captureCamera = Camera.main;
        var rt = new RenderTexture(renderWidth, renderHeight, 24, RenderTextureFormat.ARGB32);
        rt.name = "FCC_RT";
        RenderTexture oldTarget = captureCamera.targetTexture;
        captureCamera.targetTexture = rt;

        int count = 0;
        for (int f = 0; f < totalFrames; f += step)
        {
            float norm = Mathf.Clamp01((float)f / totalFrames);
            anim.Play(clipStateName, 0, norm);
            anim.Update(0f);

            RenderTexture.active = rt;
            captureCamera.Render();
            var tex = new Texture2D(renderWidth, renderHeight, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, renderWidth, renderHeight), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            string path = Path.Combine(outputFolder, string.Format("frame_{0:D4}.png", f));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            DestroyImmediate(tex);
            count++;
        }

        captureCamera.targetTexture = oldTarget;
        DestroyImmediate(rt);
        File.WriteAllText(doneMarker, count.ToString());
        Debug.Log("FrameCapture: captured " + count + " frames -> " + outputFolder);
    }
}
