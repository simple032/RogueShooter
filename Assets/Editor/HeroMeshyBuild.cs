using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace RogueShooter.EditorTools
{
    public static class HeroMeshyBuild
    {
        [Serializable] class Result{public string result;public int errors,warnings;public ulong size;}
        public static void BuildBatch()
        {
            try{HeroMeshyImport.Validate();BossWardenV04Import.Validate();HeroMeshyImport.BuildReview();string root="D:/Game Provide/outputs/hero_meshy_v06_integration_20261007/Build";Directory.CreateDirectory(root);
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/StartMenu.scene","Assets/Scenes/Stage1Maze.scene","Assets/ArtSamples/BossWardenV04/BossWardenV04Preview.scene","Assets/ArtSamples/HeroMeshyV01/HeroMeshyPreview.scene"},locationPathName=root+"/JianHai_Hero_Meshy_v06.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
                File.WriteAllText(root+"/build_result.json",JsonUtility.ToJson(new Result{result=report.summary.result.ToString(),errors=report.summary.totalErrors,warnings=report.summary.totalWarnings,size=report.summary.totalSize},true));if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Hero combined build failed");Debug.Log("[HeroMeshy] WINDOWS_BUILD_PASS");if(Application.isBatchMode)EditorApplication.Exit(0);
            }catch(Exception e){Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
        }
    }
}
