using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RogueShooter.Boss;

namespace RogueShooter.EditorTools
{
    [InitializeOnLoad]
    public static class BossWardenV04NativeTest
    {
        const string Key="BossWardenV04.NativeBatch";
        static BossWardenV04NativeTest()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false))
                {SessionState.SetBool(Key,false);new GameObject("BOSS v04 integration checks").AddComponent<BossWardenNativeChecks>();}
            };
        }
        public static void RunBatch()
        {
            BossWardenV04Import.Validate();BossWardenV04Import.BuildReview();SessionState.SetBool(Key,true);
            EditorSceneManager.OpenScene("Assets/Scenes/Stage1Maze.scene");EditorApplication.isPlaying=true;
        }
    }
}
