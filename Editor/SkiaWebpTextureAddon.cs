// SkiaSharp-based WebP texture support for glTFast imports.
using System;
using System.Threading;
using System.Threading.Tasks;
using GLTFast;
using GLTFast.Addons;
using GLTFast.Schema;
using SkiaSharp;
using Unity.Collections;
using UnityEngine;

namespace SudoSidd.GlbWebpImport
{
    // 1. The Blueprint Factory: Inheriting from ImportAddon<T> tells glTFast how to spawn our worker.
    // This allows SkiaWebpAddonRegistration.cs to successfully find and register this class.
    public class SkiaWebpTextureAddon : ImportAddon<SkiaWebpTextureAddonInstance> { }

    // 2. The Worker Instance: Inheriting from ImportAddonInstance embeds us into glTFast's import cycle.
    public class SkiaWebpTextureAddonInstance : ImportAddonInstance, ITextureImageLoader
    {
        // Hook into the core glTFast import process to register our image loader
        public override void Inject(GltfImportBase gltfImport)
        {
            Debug.Log("[SkiaWebpImport] Inject(GltfImportBase) called — addon instance is being wired into this import.");
            gltfImport.AddImportAddonInstance(this);
        }

        // Required by the base class contract. We don't need to do anything here
        // for textures, but the method must exist to satisfy the compiler.
        public override void Inject(IInstantiator instantiator)
        {
            // Pass
        }

        // Required by the base class contract to prevent memory leaks. We don't have
        // unmanaged lifecycle assets to clear here, but it must exist to satisfy the compiler.
        public override void Dispose()
        {
            // Pass
        }

        public override bool SupportsGltfExtension(string extensionName)
        {
            return extensionName == "EXT_texture_webp";
        }

        // Detection via extension declaration in the glTF JSON (non-binary case).
        // NOTE: intentionally always returns false — WebP has no relevant schema
        // extension exposed without Newtonsoft-based parsing, and our embedded-GLB
        // case is fully handled by the byte-sniffing check below instead.
        public bool IsAbleToLoad(TextureBase texture, out int imageIndex)
        {
            imageIndex = -1;
            return false;
        }

        // Detection via raw byte sniffing for embedded GLB images.
        public bool IsAbleToLoad(ReadOnlySpan<byte> data)
        {
            var result = ImageFormatDetection.IsWebP(data);
            Debug.Log($"[SkiaWebpImport] IsAbleToLoad byte-check called, IsWebP={result}, length={data.Length}");
            return result;
        }

        // The core translation engine: takes raw bytes and feeds them to our Skia decoder
        public async Task<ImageResult> LoadImage(
            NativeArray<byte>.ReadOnly data,
            bool linear,
            bool readable,
            bool generateMipMaps,
            CancellationToken cancellationToken
            )
        {
            Debug.Log($"[SkiaWebpImport] LoadImage called, data length={data.Length}");
            var texture = await SkiaWebpDecoder.Decode(data, linear, readable, cancellationToken);
            Debug.Log($"[SkiaWebpImport] LoadImage result: texture={(texture != null ? $"{texture.width}x{texture.height}" : "NULL")}");
            return new ImageResult(texture, true);
        }
    }
}
