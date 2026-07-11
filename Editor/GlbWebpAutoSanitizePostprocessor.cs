// Automatically runs GlbWebpSanitizer on every .glb asset before glTFast imports it.
//
// IMPORTANT: we do NOT write the sanitized bytes back to disk synchronously inside
// OnPreprocessAsset. Unity's asset pipeline checks the source file's version/hash
// during import; changing the file's content mid-import races that check and throws
// "Build asset version error", which can abort the import partway through and leave
// some textures generated from stale data — inconsistent per texture/model, not a
// clean failure.
//
// Instead: detect whether a file needs patching, let THIS import pass finish
// untouched (its WebP textures may fail this one time — harmless), then write the
// sanitized bytes after the pass settles and explicitly force one clean reimport.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SudoSidd.GlbWebpImport
{
    class GlbWebpAutoSanitizePostprocessor : AssetPostprocessor
    {
        // Paths already scheduled for a deferred write + reimport, so we don't
        // re-schedule while one is pending.
        static readonly HashSet<string> s_Pending = new HashSet<string>();

        // Unity calls this by name/signature convention — it isn't a virtual override.
        void OnPreprocessAsset()
        {
            if (!assetPath.EndsWith(".glb", StringComparison.OrdinalIgnoreCase))
                return;
            if (s_Pending.Contains(assetPath))
                return;

            string fullPath = Path.GetFullPath(assetPath);

            try
            {
                byte[] original = File.ReadAllBytes(fullPath);
                byte[] patched = GlbWebpSanitizer.SanitizeBytes(original, out int fixedCount);

                if (fixedCount > 0)
                {
                    string capturedPath = assetPath;
                    s_Pending.Add(capturedPath);

                    EditorApplication.delayCall += () =>
                    {
                        try
                        {
                            File.WriteAllBytes(fullPath, patched);
                            AssetDatabase.ImportAsset(capturedPath, ImportAssetOptions.ForceUpdate);
                        }
                        finally
                        {
                            s_Pending.Remove(capturedPath);
                        }
                    };

                    Debug.Log($"[GlbWebpAutoSanitizer] '{assetPath}': {fixedCount} texture(s) need patching. " +
                              "This import pass will run unsanitized; a clean reimport is scheduled right after.");
                }
            }
            catch (Exception e)
            {
                // Don't block the import over a sanitizer failure — just warn and let
                // glTFast proceed with the file as-is.
                Debug.LogWarning($"[GlbWebpAutoSanitizer] Could not auto-sanitize '{assetPath}': {e.Message}");
            }
        }
    }
}
