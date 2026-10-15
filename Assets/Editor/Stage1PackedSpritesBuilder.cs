using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Build;
using UnityEngine;
using RogueShooter.Demo;

namespace RogueShooter.EditorTools
{
    public static class Stage1PackedSpritesBuilder
    {
        const string AssetPath = "Assets/Resources/Stage1PackedSprites.asset";

        static readonly string[] Roots =
        {
            "Assets/Art/JianHai/Iso/Tiles/S1/Floor",
            "Assets/Art/JianHai/Iso/Tiles/S1/Wall",
            "Assets/Art/JianHai/Iso/Tiles/S1/Edge",
            "Assets/Art/JianHai/Iso/Tiles/S1/Decal",
            "Assets/Art/JianHai/Iso/Props",
            "Assets/Art/JianHai/Iso/FX",
            "Assets/Art/JianHai/Iso/Characters",
            "Assets/Art/JianHai/Iso/Enemies",
            "Assets/Art/JianHai/Iso/Projectiles",
            "Assets/Art/JianHai/UI",
            "Assets/Resources/JianHaiReward",
            "Assets/ArtSamples/S1MapRoom/Refine",
        };

        public static void BuildCatalog()
        {
            var paths = new List<string>();
            var sprites = new List<Sprite>();
            for (int r = 0; r < Roots.Length; r++)
            {
                if (!AssetDatabase.IsValidFolder(Roots[r]))
                    continue;
                string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { Roots[r] });
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    // v04 uses its own Resources pages. Keep the retired source frames in the
                    // project, but do not load/export thousands of unused legacy BOSS textures.
                    if (path.StartsWith("Assets/Art/JianHai/Iso/Enemies/BossWarden/", System.StringComparison.Ordinal))
                        continue;
                    Sprite sprite = FirstSprite(path);
                    if (sprite == null)
                        continue;
                    paths.Add(path);
                    sprites.Add(sprite);
                }
            }

            Stage1PackedSprites asset = AssetDatabase.LoadAssetAtPath<Stage1PackedSprites>(AssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<Stage1PackedSprites>();
                AssetDatabase.CreateAsset(asset, AssetPath);
            }

            asset.paths = paths.ToArray();
            asset.sprites = sprites.ToArray();
            ValidateRequiredActors(asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Stage1IsoArt.InvalidateActorCache();
            Debug.Log("[Stage1Pack] catalog n=" + paths.Count);
        }

        public static void ValidateRequiredActors(Stage1PackedSprites asset)
        {
            var available = new HashSet<string>();
            for (int i=0; i<asset.paths.Length && i<asset.sprites.Length; i++)
                if (asset.sprites[i] != null) available.Add(asset.paths[i]);
            foreach (var direction in new[]{"n","ne","e","se","s"})
                foreach (var action in new[]{"idle","walk","atk"})
                    for (int i=0; i<(action=="walk"?6:4); i++)
                        Require(available,$"Assets/Art/JianHai/Iso/Enemies/Shield/jh_shield_unshielded_{action}_{direction}_{i:00}.png");
            foreach (var direction in new[]{"n","ne","e","se","s","sw","w","nw"})
                foreach (var action in new[]{"hurt","die"})
                    for (int i=0; i<(action=="hurt"?3:6); i++)
                        Require(available,$"Assets/Art/JianHai/Iso/Characters/Archer/jh_archer_{action}_{direction}_{i:00}.png");
        }
        static void Require(HashSet<string> available,string path)
        { if (!available.Contains(path)) throw new BuildFailedException("Missing runtime actor sprite: "+path); }

        public static void ExportStage1()
        {
            BuildCatalog();
            string dir = @"D:\Game Provide\Stage1Export";
            Directory.CreateDirectory(dir);
            string exe = Path.Combine(dir, "RogueShooter.exe");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Stage1Maze.scene" },
                locationPathName = exe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("[Stage1Pack] EXPORT FAIL " + report.summary.result);
                return;
            }

            Debug.Log("[Stage1Pack] EXPORT " + exe);
        }

        static Sprite FirstSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
                return sprite;
            Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < all.Length; i++)
            {
                sprite = all[i] as Sprite;
                if (sprite != null)
                    return sprite;
            }

            return null;
        }
    }

    // Normal Build Settings exports also rebuild the catalog; editor fallback cannot hide missing build art.
    public sealed class Stage1ActorCatalogBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report) { Stage1PackedSpritesBuilder.BuildCatalog(); }
    }
}
