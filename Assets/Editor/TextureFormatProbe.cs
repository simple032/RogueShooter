using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace RogueShooter.EditorTools
{
    /// <summary>Sample the real import format of the build's heavyweight textures
    /// so the size plan is based on data, not guesses.</summary>
    public static class TextureFormatProbe
    {
        public static void RunBatch()
        {
            try
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
                long rgbaBytes = 0, dxtBytes = 0, otherBytes = 0; int rgbaN = 0, dxtN = 0, otherN = 0;
                string[] roots =
                {
                    "Assets/Resources/BossWardenV04/Sheets",
                    "Assets/Resources/HeroArcherMeshyV01",
                    "Assets/Art/JianHai/Iso/Characters/Archer",
                    "Assets/Art/JianHai/Iso/Enemies/Shield",
                };
                var tu = Type.GetType("UnityEditor.TextureUtil,UnityEditor");
                var m = tu != null ? tu.GetMethod("GetStorageMemorySizeLong", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) : null;
                foreach (var root in roots)
                {
                    if (!AssetDatabase.IsValidFolder(root)) { Debug.Log("[TexProbe] MISSING ROOT " + root); continue; }
                    foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { root }))
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                        if (tex == null) continue;
                        long bytes = m != null ? Convert.ToInt64(m.Invoke(null, new object[] { tex })) : 0;
                        string bucket;
                        if (tex.format == TextureFormat.RGBA32) { rgbaBytes += bytes; rgbaN++; bucket = "RGBA32"; }
                        else if (tex.format.ToString().StartsWith("DXT") || tex.format.ToString().StartsWith("BC")) { dxtBytes += bytes; dxtN++; bucket = tex.format.ToString(); }
                        else { otherBytes += bytes; otherN++; bucket = tex.format.ToString(); }
                        if (bytes > 8L * 1024 * 1024 && bucket == "RGBA32")
                            Debug.Log("[TexProbe] BIG-UNCOMPRESSED " + path + " " + bytes + "B");
                    }
                    Debug.Log("[TexProbe] ROOT DONE " + root);
                }
                Debug.Log("[TexProbe] SUMMARY rgba=" + rgbaN + " (" + (rgbaBytes / 1048576.0).ToString("F0") + "MB) dxt=" + dxtN + " (" + (dxtBytes / 1048576.0).ToString("F0") + "MB) other=" + otherN + " (" + (otherBytes / 1048576.0).ToString("F0") + "MB)");
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
