using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using RogueShooter.Vision;
using RogueShooter.Iso;

namespace RogueShooter.Demo.Editor
{
    public static class DoorSlideArtSampleBuild
    {
        public const string ScenePath = "Assets/Scenes/DoorSlideArtSampleV02.unity";
        [MenuItem("JianHai/Art Samples/Build Sliding Door V02")]
        public static void Build()
        {
            if (EditorApplication.isPlaying || SceneManager.GetActiveScene().isDirty)
                throw new System.InvalidOperationException("Preserve current play session or unsaved scene before building sample.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraGo = new GameObject("Main Camera"); cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 6;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.04f, .05f, .06f);
            camera.transform.position = new Vector3(6, 8.5f, -16);
            cameraGo.AddComponent<AudioListener>(); cameraGo.AddComponent<IsoSortAxis>(); cameraGo.AddComponent<CameraFollow2D>();
            var root = new GameObject("Sliding Door Art Sample V02");
            var sample = root.AddComponent<DoorSlideArtSample>();
            sample.WoodTexture = Resources.Load<Texture2D>(Stage1HingedDoorView.TextureResource);
            if (sample.WoodTexture == null) throw new System.InvalidOperationException("Door wood resource missing.");
            sample.Rebuild(); EditorUtility.SetDirty(sample);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath))
                throw new System.InvalidOperationException("Could not save sliding-door sample scene.");
            Debug.Log("SLIDE_SAMPLE_BUILD_PASS " + ScenePath + " slideFixtures=4 wideComparison=1 ortho=6");
        }

        [MenuItem("JianHai/Art Samples/Open Sliding Door V02")]
        public static void Open()
        {
            if (EditorApplication.isPlaying || SceneManager.GetActiveScene().isDirty)
                throw new System.InvalidOperationException("Preserve current session before opening sample.");
            EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
