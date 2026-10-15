using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueShooter.Demo.Editor
{
    public static class DoorArtSampleBuild
    {
        public const string ScenePath = "Assets/Scenes/DoorArtSampleV01.unity";
        public const string TexturePath = "Assets/Art/JianHai/Iso/Props/DoorSampleV01/door_oak_iron_leaf_v01.png";
        public static string EvidenceDir => Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../02_design/美术设计/门洞样板_v01"));

        [MenuItem("JianHai/Art Samples/Build Door V01")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop sample play before rebuilding.");
            var current = SceneManager.GetActiveScene();
            if (current.isDirty) throw new System.InvalidOperationException("Preserve current unsaved scene before opening the sample.");
            var importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Door Art Sample V01");
            var sample = root.AddComponent<DoorArtSample>();
            sample.LeafTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            sample.Rebuild();
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = 6f;
            cam.transform.position = new Vector3(0f, 3f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.04f, .05f, .06f);
            cam.transparencySortMode = TransparencySortMode.Orthographic;
            camGo.AddComponent<AudioListener>();
            Directory.CreateDirectory(EvidenceDir);
            for (int i = 0; i < 3; i++)
            {
                sample.SetPoseImmediate(i * .5f);
                Capture(cam, Path.Combine(EvidenceDir, new[] { "01_closed.png", "02_half_open.png", "03_open.png" }[i]));
            }
            sample.SetPoseImmediate(1f);
            EditorUtility.SetDirty(sample);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath))
                throw new System.InvalidOperationException("Sample scene could not be saved.");
            File.WriteAllText(Path.Combine(EvidenceDir, "build_evidence.txt"),
                "Scene=" + ScenePath + "\nClearWidth=" + DoorArtSample.ClearWidth +
                "\nLeafHeight=" + DoorArtSample.LeafHeight + "\nOpenDegrees=" + DoorArtSample.OpenDegrees +
                "\nOpenBlockerEnabled=" + sample.ClosedBlocker.enabled + "\nMainSceneEdited=false\n");
            Debug.Log("DOOR_SAMPLE_BUILD_PASS " + ScenePath + " clearWidth=8 hinges=(-4,3)/(4,3)");
        }

        public static void BuildAndExit()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (System.Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
        }

        [MenuItem("JianHai/Art Samples/Open Door V01")]
        public static void OpenSample()
        {
            if (SceneManager.GetActiveScene().isDirty)
                throw new System.InvalidOperationException("Preserve current unsaved scene before opening the sample.");
            EditorSceneManager.OpenScene(ScenePath);
        }

        public static void Capture(Camera cam, string path)
        {
            var old = cam.targetTexture;
            var active = RenderTexture.active;
            var rt = new RenderTexture(1280, 720, 24);
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                cam.targetTexture = old; RenderTexture.active = active;
                Object.DestroyImmediate(image); rt.Release(); Object.DestroyImmediate(rt);
            }
        }
    }
}
