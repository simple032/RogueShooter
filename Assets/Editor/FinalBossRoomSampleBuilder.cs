using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RogueShooter.Boss;

namespace RogueShooter.EditorTools
{
    public static class FinalBossRoomSampleBuilder
    {
        [MenuItem("JianHai/Art Samples/Build Final Boss Test Room")]
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Exit Play before building the sample.");
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            Directory.CreateDirectory("Assets/ArtSamples/FinalBossRoom");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.orthographic=true;camera.orthographicSize=8;camera.transform.position=new Vector3(0,-8,-16);camera.gameObject.AddComponent<AudioListener>();
            new GameObject("BOSS 独立战斗样板房").AddComponent<FinalBossRoomSample>();
            EditorSceneManager.SaveScene(scene,FinalBossRoomSample.ScenePath);AssetDatabase.Refresh();
        }
        [MenuItem("JianHai/Art Samples/Open Final Boss Test Room")]
        public static void Open()
        {if(EditorApplication.isPlaying)return;if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;if(!File.Exists(FinalBossRoomSample.ScenePath))Build();else EditorSceneManager.OpenScene(FinalBossRoomSample.ScenePath);}
    }
}
