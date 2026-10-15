using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using RogueShooter.Demo;

namespace JianHai.ArtSamples
{
    /// <summary>
    /// Builds the six S2/S3 sample-house scenes plus the one-at-a-time verify
    /// scene. Each scene stays tiny — one camera and one rig — because every
    /// wall, floor, door and label renders at runtime from the hand-authored
    /// layout data, the same pattern the stage-1 maze scene uses.
    /// </summary>
    public static class S2S3SampleSceneBuilder
    {
        const string Folder = "Assets/ArtSamples/S2S3MapRoom";

        static readonly string[] SceneNames =
        {
            "S2_M01_ForkLoop",
            "S2_M02_DeepForkLongLoop",
            "S2_M03_ZigzagBranchStore",
            "S3_M01_DoubleRingInnerHall",
            "S3_M02_SingleRingSideHall",
            "S3_M03_DeepFoldCorridor",
        };

        [MenuItem("JianHai/Art Samples/Build S2S3 Sample Scenes")]
        public static void MenuBuildAll()
        {
            Debug.Log(BuildAll());
        }

        public static string BuildAll()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Exit Play before building the S2/S3 sample scenes.");
            Scene previous = SceneManager.GetActiveScene();
            if (previous.isDirty)
                throw new InvalidOperationException("The current scene has unsaved changes. Save it first.");
            EnsureFolder(Folder);

            for (int i = 0; i < S2S3Layouts.All.Length; i++)
                BuildSampleScene(i);
            BuildVerifyScene();

            AssetDatabase.SaveAssets();
            string summary = "[S2S3] built " + S2S3Layouts.All.Length + " sample scenes + verify rig under " + Folder;
            foreach (string name in SceneNames)
                summary += "\n  " + Folder + "/" + name + ".scene";
            summary += "\n  " + Folder + "/S2S3Verify.scene";
            return summary;
        }

        static void BuildSampleScene(int index)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var rigGo = new GameObject("S2S3 Sample Rig");
            var rig = rigGo.AddComponent<S2S3SampleRig>();
            rig.TemplateId = S2S3Layouts.All[index];
            rig.ConfigureCamera = true;
            CreateCamera();
            if (!EditorSceneManager.SaveScene(scene, Folder + "/" + SceneNames[index] + ".scene"))
                throw new Exception("Could not save S2S3 sample scene " + SceneNames[index]);
        }

        static void BuildVerifyScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var driverGo = new GameObject("S2S3 Verify Driver");
            driverGo.AddComponent<S2S3VerifyDriver>();
            CreateCamera();
            if (!EditorSceneManager.SaveScene(scene, Folder + "/S2S3Verify.scene"))
                throw new Exception("Could not save the S2S3 verify scene");
        }

        static void CreateCamera()
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 20f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.045f, 0.06f);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;
            camGo.AddComponent<AudioListener>();
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;
            string parent = folder.Substring(0, folder.LastIndexOf('/'));
            string leaf = folder.Substring(folder.LastIndexOf('/') + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
