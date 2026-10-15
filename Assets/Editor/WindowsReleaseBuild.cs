using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RogueShooter.EditorTools
{
    /// <summary>Shipped Windows x64 player. Contains only the two player-facing scenes;
    /// dev scaffolds (ThreeRouteScaffold/LimitedVision/SampleScene) and the arg-gated
    /// review scenes stay out of the release build. The actor-catalog build guard
    /// (Stage1ActorCatalogBuildGuard) refreshes Stage1PackedSprites automatically.</summary>
    public static class WindowsReleaseBuild
    {
        [Serializable] class Result { public string result; public int errors, warnings; public ulong size; }

        const string ProductName = "箭骸行者";
        static readonly string[] Scenes =
        {
            "Assets/Scenes/StartMenu.scene",
            "Assets/Scenes/Stage1Maze.scene",
        };

        [MenuItem("JianHai/Build/Windows Release")]
        public static void BuildBatch()
        {
            try
            {
                PlayerSettings.productName = ProductName;
                AssetDatabase.SaveAssets();
                string root = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Builds", "Windows");
                Directory.CreateDirectory(root);
                string exe = Path.Combine(root, ProductName + ".exe");
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = Scenes,
                    locationPathName = exe,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None,
                });
                File.WriteAllText(Path.Combine(root, "build_result.json"),
                    JsonUtility.ToJson(new Result
                    {
                        result = report.summary.result.ToString(),
                        errors = report.summary.totalErrors,
                        warnings = report.summary.totalWarnings,
                        size = report.summary.totalSize,
                    }, true));
                if (report.summary.result != BuildResult.Succeeded)
                    throw new Exception("Windows release build failed: " + report.summary.result);
                Debug.Log("[ReleaseBuild] WINDOWS_BUILD_PASS " + exe + " size=" + report.summary.totalSize);
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
