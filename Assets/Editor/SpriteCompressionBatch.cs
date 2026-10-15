using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RogueShooter.EditorTools
{
    /// <summary>One-shot: enable Standalone texture compression for every texture the
    /// shipped build pulls in (sprites pull their sheets in via the packed-sprites
    /// catalog and Resources pages). Editor previews keep the lossless default; only
    /// the player build encodes BC7/DXT. Roll back with RollbackBatch.</summary>
    public static class SpriteCompressionBatch
    {
        static readonly string[] Roots =
        {
            "Assets/Resources",
            "Assets/Art/JianHai/Iso/Tiles",
            "Assets/Art/JianHai/Iso/Props",
            "Assets/Art/JianHai/Iso/FX",
            "Assets/Art/JianHai/Iso/Characters",
            "Assets/Art/JianHai/Iso/Enemies",
            "Assets/Art/JianHai/Iso/Projectiles",
            "Assets/Art/JianHai/Iso/UI",
            "Assets/ArtSamples/S1MapRoom/Refine",
        };

        [MenuItem("JianHai/Build/Set Sprite Compression (Standalone)")]
        public static void ApplyMenu() { Apply(true); }

        public static void RunBatch() { Apply(true); }
        public static void RollbackBatch() { Apply(false); }

        static void Apply(bool enable)
        {
            try
            {
                var paths = new HashSet<string>();
                foreach (var root in Roots)
                {
                    if (!AssetDatabase.IsValidFolder(root))
                        continue;
                    foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { root }))
                        paths.Add(AssetDatabase.GUIDToAssetPath(guid));
                }

                int changed = 0, skipped = 0;
                foreach (var path in paths)
                {
                    if (path.EndsWith("_n.png", StringComparison.Ordinal))
                        continue; // normal maps stay lossless: BC7 banding shows up as lighting errors
                    if (enable && path.StartsWith("Assets/Art/JianHai/Iso/Enemies/BossWarden/", StringComparison.Ordinal))
                        continue; // retired source frames are excluded from the catalog and never ship
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) { skipped++; continue; }
                    var ps = importer.GetPlatformTextureSettings("Standalone");
                    // Boss sheets (3612x3096 class) lose 4-divisibility when Unity downscales them to
                    // the 2048 cap, which silently disables block compression. Keep them at full res.
                    int max = path.StartsWith("Assets/Resources/BossWardenV04/", StringComparison.Ordinal) ? 4096 : 2048;
                    if (enable && ps.overridden && ps.textureCompression == TextureImporterCompression.Compressed && ps.maxTextureSize == max)
                    { skipped++; continue; } // already compressed: keeps repeat runs incremental
                    if (!enable && !ps.overridden)
                    { skipped++; continue; }
                    ps.overridden = enable;
                    ps.textureCompression = enable ? TextureImporterCompression.Compressed : TextureImporterCompression.Uncompressed;
                    ps.maxTextureSize = max;
                    importer.SetPlatformTextureSettings(ps);
                    importer.SaveAndReimport();
                    changed++;
                    if (changed % 500 == 0)
                        Debug.Log("[SpriteCompression] " + changed + "/" + paths.Count + " reimported, last=" + path);
                }
                AssetDatabase.SaveAssets();
                Debug.Log("[SpriteCompression] DONE enable=" + enable + " changed=" + changed + " skipped=" + skipped + " total=" + paths.Count);
                if (Application.isBatchMode)
                    EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
                else
                    throw;
            }
        }
    }
}
