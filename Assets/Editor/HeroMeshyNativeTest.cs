using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RogueShooter.Iso;
namespace RogueShooter.EditorTools
{
    [InitializeOnLoad] public static class HeroMeshyNativeTest
    {
        const string Key="HeroMeshyV01.NativeChecks";
        static HeroMeshyNativeTest(){EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)){SessionState.SetBool(Key,false);new GameObject("主角主场景回归检查").AddComponent<HeroMeshyRuntimeChecks>();}};}
        public static void RunCurrentScene()
        {
            if(EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)throw new System.InvalidOperationException("Editor is busy");
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!="Assets/Scenes/Stage1Maze.scene")throw new System.InvalidOperationException("Current scene must be Stage1Maze");
            HeroMeshyImport.Validate();SessionState.SetBool(Key,true);EditorApplication.isPaused=false;EditorApplication.isPlaying=true;
        }
        public static void RunBatch(){HeroMeshyImport.Validate();SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/Stage1Maze.scene");EditorApplication.isPlaying=true;}
    }
}
