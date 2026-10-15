using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RogueShooter.EditorTools
{
    public static class BossWardenV04Build
    {
        public static void BuildBatch()
        {
            try
            {
                BossWardenV04Import.Validate();BossWardenV04Import.BuildReview();
                string root="D:/Game Provide/outputs/boss_v05_integration_20261006/Build";Directory.CreateDirectory(root);
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes=new[]{"Assets/Scenes/StartMenu.scene","Assets/Scenes/Stage1Maze.scene","Assets/ArtSamples/BossWardenV04/BossWardenV04Preview.scene"},
                    locationPathName=root+"/JianHai_Boss_v05.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development
                });
                File.WriteAllText(root+"/build_result.json",JsonUtility.ToJson(new Result{result=report.summary.result.ToString(),errors=report.summary.totalErrors,warnings=report.summary.totalWarnings,size=report.summary.totalSize},true));
                if(report.summary.result!=BuildResult.Succeeded)throw new Exception("BOSS game build failed "+report.summary.result);
                Debug.Log("[BossV04] WINDOWS_BUILD_PASS bytes="+report.summary.totalSize);EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        [Serializable] class Result {public string result;public int errors,warnings;public ulong size;}
    }
}
