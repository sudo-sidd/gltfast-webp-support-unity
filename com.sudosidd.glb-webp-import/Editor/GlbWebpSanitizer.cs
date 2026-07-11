// Pre-processes a .glb file so glTFast's default (non-Newtonsoft) importer can
// actually locate WebP-only images.
//
// Root cause: when a texture uses EXT_texture_webp and has no fallback image, the
// glTF JSON typically omits the top-level texture.source field entirely (the image
// index only exists at extensions.EXT_texture_webp.source). glTFast's
// TextureBase.GetImageIndex() only knows how to resolve a plain source or
// Extensions.KHR_texture_basisu.source — it has no path for EXT_texture_webp — so
// affected textures return -1 and are skipped before SkiaWebpTextureAddon's
// byte-sniffing IsAbleToLoad(ReadOnlySpan<byte>) ever runs.
//
// Fix: copy extensions.EXT_texture_webp.source into a plain texture.source field
// (same images[] index, so nothing else about the file changes). Once that's in
// place, glTFast's normal resolution finds the image, reaches the byte-sniffing
// fallback, and SkiaWebpTextureAddon decodes it as usual.
//
// SanitizeBytes() is the core, reusable byte[]->byte[] operation. It's used both by
// the manual "Sanitize GLB" menu command below and by GlbWebpAutoSanitizePostprocessor,
// which runs this automatically on import.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SudoSidd.GlbWebpImport
{
    public static class GlbWebpSanitizer
    {
        const uint GlbMagic = 0x46546C67;      // "glTF"
        const uint ChunkTypeJson = 0x4E4F534A; // "JSON"
        const uint ChunkTypeBin = 0x004E4942;  // "BIN\0"

        [MenuItem("Tools/GLB WebP Import/Sanitize GLB (Fix EXT_texture_webp source)")]
        public static void SanitizeSelectedFile()
        {
            string path = EditorUtility.OpenFilePanel("Select a GLB file to sanitize", "", "glb");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                string outPath = SanitizeFileToNewFile(path);
                EditorUtility.DisplayDialog("GLB WebP Sanitizer",
                    $"Done. Sanitized file written to:\n{outPath}", "OK");
                EditorUtility.RevealInFinder(outPath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[GlbWebpSanitizer] Failed to sanitize '{path}': {e}");
                EditorUtility.DisplayDialog("GLB WebP Sanitizer", $"Failed:\n{e.Message}", "OK");
            }
        }

        /// Manual-tool entry point: reads a GLB from disk, patches it, and writes
        /// "&lt;name&gt;_websanitized.glb" next to the original (leaving the
        /// original untouched). Returns the output path. Useful for files that
        /// live outside Assets/, or for inspecting the result before importing.
        public static string SanitizeFileToNewFile(string path)
        {
            byte[] fileBytes = File.ReadAllBytes(path);
            byte[] patched = SanitizeBytes(fileBytes, out int fixedCount);

            Debug.Log($"[GlbWebpSanitizer] '{Path.GetFileName(path)}': patched {fixedCount} texture(s) that only had EXT_texture_webp.source.");

            string outPath = Path.Combine(
                Path.GetDirectoryName(path) ?? "",
                Path.GetFileNameWithoutExtension(path) + "_websanitized.glb");
            File.WriteAllBytes(outPath, patched);
            return outPath;
        }

        /// Core operation: parses a GLB byte buffer, patches any EXT_texture_webp-only
        /// textures to also carry a plain "source" index, and returns the rebuilt GLB
        /// bytes. Everything outside the touched texture entries (including the BIN
        /// chunk) is preserved. fixedCount is 0, and the returned bytes are logically
        /// unchanged, if nothing needed patching — callers can use that to decide
        /// whether a rewrite is worth doing at all.
        public static byte[] SanitizeBytes(byte[] fileBytes, out int fixedCount)
        {
            using var ms = new MemoryStream(fileBytes);
            using var br = new BinaryReader(ms);

            uint magic = br.ReadUInt32();
            if (magic != GlbMagic)
                throw new InvalidDataException("Not a valid GLB file (bad magic).");

            br.ReadUInt32(); // version — unused, we always write out version 2
            br.ReadUInt32(); // declared total length — recomputed on write

            byte[] jsonBytes = null;
            byte[] binBytes = null;
            var extraChunks = new List<(uint type, byte[] data)>();

            while (ms.Position < ms.Length)
            {
                uint chunkLength = br.ReadUInt32();
                uint chunkType = br.ReadUInt32();
                byte[] chunkData = br.ReadBytes((int)chunkLength);

                if (chunkType == ChunkTypeJson && jsonBytes == null)
                    jsonBytes = chunkData;
                else if (chunkType == ChunkTypeBin && binBytes == null)
                    binBytes = chunkData;
                else
                    extraChunks.Add((chunkType, chunkData)); // preserved as-is, unmodified
            }

            if (jsonBytes == null)
                throw new InvalidDataException("GLB has no JSON chunk.");

            string jsonText = Encoding.UTF8.GetString(jsonBytes);
            object root = GlbJsonMini.Parse(jsonText);
            if (root is not Dictionary<string, object> rootObj)
                throw new InvalidDataException("GLB JSON root is not an object.");

            fixedCount = FixWebpTextures(rootObj);

            string newJsonText = GlbJsonMini.Serialize(root);
            byte[] newJsonBytes = PadChunk(Encoding.UTF8.GetBytes(newJsonText), (byte)' ');
            byte[] paddedBin = binBytes != null ? PadChunk(binBytes, 0x00) : null;

            using var outStream = new MemoryStream();
            using (var bw = new BinaryWriter(outStream, Encoding.UTF8, leaveOpen: true))
            {
                uint newTotalLength = (uint)(12
                    + 8 + newJsonBytes.Length
                    + (paddedBin != null ? 8 + paddedBin.Length : 0));
                foreach (var (_, data) in extraChunks)
                    newTotalLength += (uint)(8 + data.Length);

                bw.Write(GlbMagic);
                bw.Write((uint)2);
                bw.Write(newTotalLength);

                bw.Write((uint)newJsonBytes.Length);
                bw.Write(ChunkTypeJson);
                bw.Write(newJsonBytes);

                if (paddedBin != null)
                {
                    bw.Write((uint)paddedBin.Length);
                    bw.Write(ChunkTypeBin);
                    bw.Write(paddedBin);
                }

                foreach (var (type, data) in extraChunks)
                {
                    bw.Write((uint)data.Length);
                    bw.Write(type);
                    bw.Write(data);
                }
            }

            return outStream.ToArray();
        }

        static byte[] PadChunk(byte[] data, byte padByte)
        {
            int remainder = data.Length % 4;
            if (remainder == 0) return data;
            int padCount = 4 - remainder;
            byte[] padded = new byte[data.Length + padCount];
            Buffer.BlockCopy(data, 0, padded, 0, data.Length);
            for (int i = data.Length; i < padded.Length; i++)
                padded[i] = padByte;
            return padded;
        }

        static int FixWebpTextures(Dictionary<string, object> root)
        {
            if (!root.TryGetValue("textures", out var texturesObj) || texturesObj is not List<object> textures)
                return 0;

            int fixedCount = 0;
            foreach (var t in textures)
            {
                if (t is not Dictionary<string, object> tex) continue;

                // Already has a plain source (e.g. a non-WebP fallback image) — leave it alone.
                if (tex.TryGetValue("source", out var srcVal) && srcVal != null) continue;

                if (!tex.TryGetValue("extensions", out var extObj) ||
                    extObj is not Dictionary<string, object> extensions) continue;
                if (!extensions.TryGetValue("EXT_texture_webp", out var webpObj) ||
                    webpObj is not Dictionary<string, object> webp) continue;
                if (!webp.TryGetValue("source", out var webpSrc)) continue;

                tex["source"] = webpSrc; // same images[] index — just makes it reachable
                fixedCount++;
            }
            return fixedCount;
        }
    }
}
