using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
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
            "Assets/Art/JianHai/Iso/Characters",
            "Assets/Art/JianHai/Iso/Enemies",
            "Assets/Art/JianHai/Iso/Projectiles",
            "Assets/Art/JianHai/UI",
            "Assets/Resources/JianHaiReward",
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
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log("[Stage1Pack] catalog n=" + paths.Count);
        }

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
}
