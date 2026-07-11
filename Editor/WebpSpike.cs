using UnityEngine;
using UnityEditor;
using System.IO;
using SkiaSharp;

public static class WebpSpike
{
    [MenuItem("Tools/GLB WebP Import/Spike Test Decode")]
    public static void TestDecode()
    {
        string path = EditorUtility.OpenFilePanel("Select a WebP file", "", "webp");
        if (string.IsNullOrEmpty(path)) return;

        byte[] fileBytes = File.ReadAllBytes(path);

        using (SKBitmap skBitmap = SKBitmap.Decode(fileBytes))
        {
            if (skBitmap == null)
            {
                Debug.LogError($"SkiaSharp failed to decode: {path}");
                return;
            }

            int width = skBitmap.Width;
            int height = skBitmap.Height;

            // SkiaSharp pixels are top-down (row 0 = top), Unity Texture2D is bottom-up
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                int unityY = height - 1 - y;
                for (int x = 0; x < width; x++)
                {
                    SKColor c = skBitmap.GetPixel(x, y);
                    pixels[unityY * width + x] = new Color32(c.Red, c.Green, c.Blue, c.Alpha);
                }
            }

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();

            // Save a PNG next to the source so you can eyeball it outside Unity too
            string outPath = Path.Combine(Path.GetDirectoryName(path),
                Path.GetFileNameWithoutExtension(path) + "_decoded.png");
            File.WriteAllBytes(outPath, tex.EncodeToPNG());

            // Preview on a quad in the scene
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "WebP_Spike_Preview";
            var mat = new Material(Shader.Find("Unlit/Transparent")) { mainTexture = tex };
            quad.GetComponent<MeshRenderer>().sharedMaterial = mat;

            Debug.Log($"Decoded {width}x{height} WebP via SkiaSharp. PNG saved to {outPath}.");
        }
    }
} 
