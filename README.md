# Unity WebP GLB Importer

A seamless integration for Unity that allows you to import GLTF/GLB files containing WebP texture compression. This package extends Unity's importing capabilities to handle web-optimized 3D assets without manual conversion.

## Features

*   **Seamless Import:** Simply drag and drop `.glb` files with WebP textures into your Project window.
*   **Automatic Transcoding:** Automatically handles the conversion or assignment of WebP data to Unity-compatible texture formats.
*   **Fast Workflow:** No need to pre-convert textures to PNG or Jpeg before exporting from Blender or other DCC tools.
*   **Reduced Build Size:** Leverage the superior compression of WebP for your source assets.

## Installation

### Via Unity Package Manager (Git URL)

1.  Open the Unity Package Manager (`Window` > `Package Manager`).
2.  Click the `+` button and select `Add package from git URL...`.
3.  Enter 'https://github.com/sudo-sidd/gltfast-webp-support-unity'.

### Manual Installation

1.  Download the latest release or clone the repository.
2.  Copy the contents into your project's `Packages/` folder.

## Requirements

*   Unity 2021.3 LTS or newer (recommended).
*   A GLTF import library (like [UnityGLTF](https://github.com/KhronosGroup/UnityGLTF) or [glTFast](https://github.com/atteneder/glTFast)) if not already present in your project.

## How It Works

Two separate problems have to be solved for a WebP-only GLB to import correctly in glTFast, and this package fixes both, automatically, before and during import:

1. **Making the WebP image discoverable.** When a texture uses `EXT_texture_webp` with no fallback image, the glTF JSON typically has no plain `source` field on that texture — only `extensions.EXT_texture_webp.source`. glTFast's image-index resolution doesn't know how to read that extension, so the texture is silently skipped before it's ever inspected. An `AssetPostprocessor` intercepts each `.glb` right before glTFast imports it, parses the GLB's JSON chunk, and copies the extension's image index into a plain `source` field on any texture that's missing one — pointing at the exact same image, nothing else changes. This patch is written back to the source `.glb` file on disk the first time it's needed; it's a no-op on every import after that, since the file no longer needs patching.

2. **Decoding the WebP bytes.** With the image now discoverable, glTFast reaches its raw byte-sniffing fallback for unrecognized image formats. A registered glTFast import addon detects WebP's magic bytes, decodes them via SkiaSharp into a `Texture2D`, and hands it back into glTFast's normal texture pipeline — from there it's treated exactly like a PNG or JPEG would be, including Unity's own per-platform texture compression on build.

**Note:** step 1 modifies your source `.glb` file in place on disk the first time it's imported. If you version these assets, expect one binary diff per file the first time each is imported after being added or re-exported.


## Dependencies

*   **[glTFast](https://github.com/atteneder/glTFast):** Required, not bundled — install it separately via the Package Manager. This package hooks into glTFast's import addon system and its Editor-time GLB importer.
*   **SkiaSharp:** Bundled as a precompiled native library (`SkiaSharp.dll`), used for WebP decoding. Editor-only — not included in player builds. Licensed under MIT; see [license/attribution file] for the full notice.

## License

This project is licensed under the MIT License.
