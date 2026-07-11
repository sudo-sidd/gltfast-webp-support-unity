// SkiaSharp WebP decoder, producing a Texture2D compatible with glTFast's import pipeline.
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace SudoSidd.GlbWebpImport
{
    static class SkiaWebpDecoder
    {
        public static Task<Texture2D> Decode(
            NativeArray<byte>.ReadOnly data,
            bool linear,
            bool readable,
            CancellationToken cancellationToken
            )
        {
            // Copy NativeArray into a managed byte[] for SkiaSharp's API.
            var bytes = new byte[data.Length];
            unsafe
            {
                fixed (byte* dst = bytes)
                {
                    UnsafeUtility.MemCpy(dst, data.GetUnsafeReadOnlyPtr(), data.Length);
                }
            }

            using var skBitmap = SKBitmap.Decode(bytes);
            if (skBitmap == null)
            {
                Debug.LogError("SkiaWebpDecoder: SkiaSharp failed to decode WebP image.");
                return Task.FromResult<Texture2D>(null);
            }

            int width = skBitmap.Width;
            int height = skBitmap.Height;

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false, linear);
            var pixels = new Color32[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    SKColor c = skBitmap.GetPixel(x, y);
                    pixels[y * width + x] = new Color32(c.Red, c.Green, c.Blue, c.Alpha);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, !readable);

            return Task.FromResult(tex);
        }
    }
}
