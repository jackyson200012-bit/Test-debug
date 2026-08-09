using System.IO;
using UnityEditor;
using UnityEngine;

public static class ContactSheetBuilder
{
    [MenuItem("Tools/FrameCapture/Build Contact Sheet")]
    public static void Build()
    {
        string folder = Path.Combine(Application.dataPath, "..", "Temp", "FrameCapture");
        string outPath = Path.Combine(Application.dataPath, "..", "FrameContactSheet.png");
        BuildFromFolder(folder, outPath);
    }

    public static void BuildFromFolder(string folder, string outputPng)
    {
        if (!Directory.Exists(folder))
        {
            Debug.LogError("FrameCapture folder not found: " + folder);
            return;
        }

        string[] files = Directory.GetFiles(folder, "frame_*.png");
        if (files.Length == 0)
        {
            Debug.LogError("No frame_*.png found in " + folder);
            return;
        }
        System.Array.Sort(files);

        const int cols = 8;
        const int cellW = 300, cellH = 300, labelH = 22, pad = 4;
        int rows = (files.Length + cols - 1) / cols;
        int w = cols * (cellW + pad) + pad;
        int h = rows * (cellH + labelH + pad) + pad;

        var sheet = new Texture2D(w, h, TextureFormat.RGB24, false);
        var bg = new Color32(28, 28, 30, 255);
        var fill = new Color32[sheet.width * sheet.height];
        for (int i = 0; i < fill.Length; i++) fill[i] = bg;
        sheet.SetPixels32(fill);

        for (int i = 0; i < files.Length; i++)
        {
            byte[] bytes = File.ReadAllBytes(files[i]);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes))
            {
                Object.DestroyImmediate(tex);
                continue;
            }

            Rect crop = AutoCrop(tex);
            int dx = pad + (i % cols) * (cellW + pad);
            int dyBase = pad + (i / cols) * (cellH + labelH + pad);
            int dy = dyBase + labelH;

            Color[] src = tex.GetPixels((int)crop.x, (int)crop.y, (int)crop.width, (int)crop.height);
            int srcW = (int)crop.width;
            int srcH = (int)crop.height;

            float imgA = srcW / (float)srcH;
            float cellA = cellW / (float)cellH;
            int dw, dh;
            if (imgA > cellA) { dw = cellW; dh = Mathf.RoundToInt(cellW / imgA); }
            else { dh = cellH; dw = Mathf.RoundToInt(cellH * imgA); }
            int ox = dx + (cellW - dw) / 2;
            int oy = dy + (cellH - dh) / 2;

            for (int y = 0; y < dh; y++)
            {
                int sy = Mathf.Clamp((int)((y / (float)dh) * srcH), 0, srcH - 1);
                for (int x = 0; x < dw; x++)
                {
                    int sx = Mathf.Clamp((int)((x / (float)dw) * srcW), 0, srcW - 1);
                    sheet.SetPixel(ox + x, oy + y, (Color32)src[sy * srcW + sx]);
                }
            }
            Object.DestroyImmediate(tex);
        }
        sheet.Apply();

        Color32[] px = sheet.GetPixels32();
        var flippedPx = new Color32[px.Length];
        for (int y = 0; y < sheet.height; y++)
            System.Array.Copy(px, y * sheet.width, flippedPx, (sheet.height - 1 - y) * sheet.width, sheet.width);
        var flipped = new Texture2D(sheet.width, sheet.height, TextureFormat.RGB24, false);
        flipped.SetPixels32(flippedPx);
        flipped.Apply();

        File.WriteAllBytes(outputPng, flipped.EncodeToPNG());
        Object.DestroyImmediate(flipped);
        Object.DestroyImmediate(sheet);
        Debug.Log("Contact sheet written: " + outputPng + " (" + files.Length + " frames)");
    }

    static Rect AutoCrop(Texture2D tex)
    {
        Color32[] px = tex.GetPixels32();
        int w = tex.width, h = tex.height;
        int minX = w, maxX = 0, minY = h, maxY = 0;
        for (int y = 0; y < h; y += 2)
        {
            for (int x = 0; x < w; x += 2)
            {
                Color32 c = px[y * w + x];
                int d = Mathf.Abs((int)c.r - 38) + Mathf.Abs((int)c.g - 38) + Mathf.Abs((int)c.b - 43);
                if (d > 30)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }
        if (maxX <= minX || maxY <= minY) return new Rect(0, 0, w, h);

        int pw = (int)Mathf.Max(12, (maxX - minX) * 0.12f);
        int ph = (int)Mathf.Max(12, (maxY - minY) * 0.12f);
        int cx = Mathf.Max(0, minX - pw);
        int cy = Mathf.Max(0, minY - ph);
        int cw = Mathf.Min(w - cx, (maxX - minX) + pw * 2);
        int ch = Mathf.Min(h - cy, (maxY - minY) + ph * 2);
        return new Rect(cx, cy, cw, ch);
    }
}
