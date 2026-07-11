# Unity WebP GLB Importer

A seamless integration for Unity that allows you to import GLTF/GLB files containing WebP texture compression. This package extends Unity's importing capabilities to handle web-optimized 3D assets without manual conversion.

## Features

*   **Seamless Import:** Simply drag and drop `.glb` or `.gltf` files with WebP textures into your Project window.
*   **Automatic Transcoding:** Automatically handles the conversion or assignment of WebP data to Unity-compatible texture formats.
*   **Fast Workflow:** No need to pre-convert textures to PNG or Jpeg before exporting from Blender or other DCC tools.
*   **Reduced Build Size:** Leverage the superior compression of WebP for your source assets.

## Installation

### Via Unity Package Manager (Git URL)

1.  Open the Unity Package Manager (`Window` > `Package Manager`).
2.  Click the `+` button and select `Add package from git URL...`.
3.  Enter ''.

### Manual Installation

1.  Download the latest release or clone the repository.
2.  Copy the contents into your project's `Packages/` folder.

## Requirements

*   Unity 2021.3 LTS or newer (recommended).
*   A GLTF import library (like [UnityGLTF](https://github.com/KhronosGroup/UnityGLTF) or [glTFast](https://github.com/atteneder/glTFast)) if not already present in your project.

## How It Works

This package hooks into the Unity asset pipeline by leveraging the glTFast Unity addon. When a GLB file is detected, the importer checks for the `EXT_texture_webp` extension and utilizes SkiaSharp DLLs to decode the image data. This allows Unity to then natively compress the textures for your specific target platform (Android, iOS, PC, etc.).

## Dependencies

This package requires the following dependencies to function:

*   **[glTFast](https://github.com/atteneder/glTFast):** The primary framework used for glTF loading and extension handling.
*   **[SkiaSharp](https://github.com/mono/SkiaSharp):** Used for high-performance WebP decoding across different editor platforms.
*   **[Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json):** Required for parsing glTF extension metadata.

## License

This project is licensed under the MIT License.
