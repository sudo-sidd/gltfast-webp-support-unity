// Registers the SkiaSharp WebP addon globally so it applies automatically
// to every glTFast import, including drag-and-drop design-time GLB imports.
using GLTFast.Addons;
using UnityEditor;

namespace SudoSidd.GlbWebpImport
{
    [InitializeOnLoad]
    static class SkiaWebpAddonRegistration
    {
        static SkiaWebpAddonRegistration()
        {
            ImportAddonRegistry.RegisterImportAddon(new SkiaWebpTextureAddon());
            UnityEngine.Debug.Log("[SkiaWebpImport] Addon registered via InitializeOnLoad.");
        }
    }
}
