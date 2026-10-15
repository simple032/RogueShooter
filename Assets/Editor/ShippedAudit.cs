using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using RogueShooter.Demo;

namespace RogueShooter.EditorTools
{
    /// <summary>Shipped-adoption audit: every texture under the packed-sprite catalog
    /// roots is counted as available; the catalog asset's own paths list plus the
    /// contract-driven Resources pages count as used. Prints the unused byte mass so
    /// the trim decision is based on numbers.</summary>
    public static class ShippedUsageAudit
    {
        static readonly string[] Roots =
        {
            "Assets/Art/JianHai/Iso/Tiles",
            "Assets/Art/JianHai/Iso/Wall",
            "Assets/Art/JianHai/Iso/Edge",
            "Assets/Art/JianHai/Iso/Decal",
            "Assets/Art/JianHai/Iso/Props",
            "Assets/Art/JianHai/Iso/FX",
            "Assets/Art/JianHai/Iso/Characters",
            "Assets/Art/JianHai/Iso/Enemies",
            "Assets/Art/JianHai/Iso/Projectiles",
            "Assets/Art/JianHai/Iso/UI",
            "Assets/ArtSamples/S1MapRoom/Refine",
        };

        public static void RunBatch()
        {
            try
            {
                var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var catalog = AssetDatabase.LoadAssetAtPath<Stage1PackedSprites>("Assets/Resources/Stage1PackedSprites.asset");
                if (catalog != null)
                    foreach (var p in catalog.paths)
                        used.Add(Norm(p));

                string[] dirs = { "n", "ne", "e", "se", "s", "sw", "w", "nw" };

                var heroText = Resources.Load<TextAsset>("HeroArcherMeshyV01/contract");
                if (heroText == null) throw new InvalidOperationException("hero contract missing");
                var heroContract = JsonUtility.FromJson<HContract>(heroText.text);
                foreach (var c in heroContract.clips)
                    foreach (var d in c.directions)
                    {
                        int pages = Mathf.CeilToInt(c.frames / 16.0f);
                        for (int pg = 0; pg < pages; pg++)
                            used.Add("assets/resources/heroarchermeshyv01/sheets/" + c.id + "_" + d.id + "_" + pg.ToString("00") + ".png");
                    }
                foreach (var f in dirs)
                    used.Add("assets/resources/heroarchermeshyv01/projectiles/arrow_" + f + ".png");

                var bossText = Resources.Load<TextAsset>("BossWardenV04/contract");
                if (bossText == null) throw new InvalidOperationException("boss contract missing");
                var bossContract = JsonUtility.FromJson<BContract>(bossText.text);
                foreach (var c in bossContract.clips)
                    foreach (var d in c.directions)
                    {
                        used.Add("assets/resources/bosswardenv04/sheets/" + c.id + "_" + d.id + ".png");
                        used.Add("assets/resources/bosswardenv04/sheets/" + c.id + "_" + d.id + "_e.png");
                    }
                foreach (var f in dirs)
                {
                    for (int i = 0; i < 3; i++)
                        used.Add("assets/resources/bosswardenv04/sheets/shield_drop_" + f + "_" + i + ".png");
                    // P1 death shield series consumed by BossWardenPresentation.LateUpdate.
                    for (int i = 0; i < 2; i++)
                        used.Add("assets/resources/bosswardenv04/sheets/p1_death_shield_" + f + "_" + i + ".png");
                }
                used.Add("assets/resources/bosswardenv04/sheets/bolt_p1.png");
                used.Add("assets/resources/bosswardenv04/sheets/bolt_p2.png");

                long unusedBytes = 0; int unusedCount = 0; long usedBytes = 0; int usedCount = 0;
                var tu = Type.GetType("UnityEditor.TextureUtil,UnityEditor");
                var m = tu != null ? tu.GetMethod("GetStorageMemorySizeLong", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) : null;
                foreach (var root in Roots)
                {
                    if (!AssetDatabase.IsValidFolder(root))
                        continue;
                    foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { root }))
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                        long bytes = m != null && tex != null ? Convert.ToInt64(m.Invoke(null, new object[] { tex })) : 0;
                        if (bytes == 0 && File.Exists(path))
                            bytes = new FileInfo(path).Length; // fallback: source bytes
                        bool isUsed = used.Contains(Norm(path));
                        if (isUsed) { usedCount++; usedBytes += bytes; }
                        else { unusedCount++; unusedBytes += bytes; Debug.Log("[ShipAudit] UNUSED " + bytes + "B " + path); }
                    }
                }
                Debug.Log("[ShipAudit] SUMMARY roots used=" + usedCount + " (" + (usedBytes / 1048576.0).ToString("F0") + "MB) unused=" + unusedCount + " (" + (unusedBytes / 1048576.0).ToString("F0") + "MB)");
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

        static string Norm(string p) { return p == null ? "" : p.ToLowerInvariant().Replace('\\', '/'); }

        [Serializable] class HDirection { public string id; }
        [Serializable] class HClip { public string id; public int frames; public HDirection[] directions; }
        [Serializable] class HContract { public string version; public HClip[] clips; }
        [Serializable] class BClip { public string id; public HDirection[] directions; }
        [Serializable] class BContract { public string version; public BClip[] clips; }
    }
}
