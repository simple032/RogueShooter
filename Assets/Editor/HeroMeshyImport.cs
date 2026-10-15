using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using RogueShooter.Iso;
namespace RogueShooter.EditorTools
{
    public static class HeroMeshyImport
    {
        public const string Root="Assets/Resources/HeroArcherMeshyV01/";
        [Serializable] public class RectData{public string name;public int x,y,size;}
        [Serializable] public class Sheet{public string path;public Vector2 pivot;public RectData[] rects;public bool single;}
        [Serializable] public class Manifest{public Sheet[] sheets;}
        [Serializable] public class ChangedManifest{public string[] paths;}
        [MenuItem("JianHai/主角 Meshy/导入精修动作")]
        public static void Import()
        {
            var m=JsonUtility.FromJson<Manifest>(File.ReadAllText(Root+"import.json"));int count=0;
            foreach(var sheet in m.sheets)
            {
                AssetDatabase.ImportAsset(sheet.path,ImportAssetOptions.ForceSynchronousImport);var imp=(TextureImporter)AssetImporter.GetAtPath(sheet.path);
                imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=sheet.single?SpriteImportMode.Single:SpriteImportMode.Multiple;
                imp.spritePixelsPerUnit=128;imp.mipmapEnabled=false;imp.alphaIsTransparency=true;imp.npotScale=TextureImporterNPOTScale.None;imp.maxTextureSize=2048;imp.isReadable=false;
                SetCompression(imp);imp.filterMode=FilterMode.Bilinear;imp.wrapMode=TextureWrapMode.Clamp;
                var settings=new TextureImporterSettings();imp.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=sheet.pivot;imp.SetTextureSettings(settings);
                if(!sheet.single){var rects=new SpriteMetaData[sheet.rects.Length];for(int i=0;i<rects.Length;i++){var r=sheet.rects[i];rects[i]=new SpriteMetaData{name=r.name,rect=new Rect(r.x,r.y,r.size,r.size),alignment=(int)SpriteAlignment.Custom,pivot=sheet.pivot};}imp.spritesheet=rects;}
                imp.SaveAndReimport();count+=sheet.rects.Length;
            }
            var mat=AssetDatabase.LoadAssetAtPath<Material>(Root+"HeroMaterial.mat");if(mat==null){mat=new Material(Shader.Find("JianHai/IsoSpriteLit"));AssetDatabase.CreateAsset(mat,Root+"HeroMaterial.mat");}
            mat.SetColor("_Ambient",Color.white);mat.SetColor("_LightColor",Color.black);mat.SetFloat("_Form",0);EditorUtility.SetDirty(mat);AssetDatabase.SaveAssets();Validate();BuildReview();Debug.Log("[HeroMeshy] IMPORT_PASS sprites="+count);
        }
        public static void Validate()
        {
            if(HeroMeshyArt.Data.version!="hero-meshy/6")throw new Exception("Wrong hero version");int count=0;
            foreach(var c in HeroMeshyArt.Data.clips)foreach(var d in c.directions)
            {if(d.muzzles.Length!=c.frames||d.torchTips.Length!=c.frames)throw new Exception("Hero marker length");for(int f=0;f<c.frames;f++){var s=HeroMeshyArt.Frame(c.id,d.id,f);if(s==null||s.pixelsPerUnit!=128||s.rect.width!=384||(s.pivot/s.rect.size-new Vector2(.5f,.25f)).sqrMagnitude>.000001f)throw new Exception("Hero frame/pivot "+c.id+"/"+d.id+"/"+f);count++;}}
            if(count!=6256)throw new Exception("Hero frames expected 6256 got "+count);foreach(var d in new[]{"e","ne","n","nw","w","sw","s","se"})if(HeroMeshyArt.Projectile(d)==null)throw new Exception("Missing actual 8-dir hero arrow");
            Debug.Log("[HeroMeshy] ASSET_CHECK_PASS frames="+count);
        }
        public static void BuildReview()
        {
            const string previewPath="Assets/ArtSamples/HeroMeshyV01/HeroMeshyPreview.scene";
            // The existing viewer loads the current contract and sprites at runtime.
            // Reusing it also preserves any layout changes and the user's open scene.
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(previewPath)!=null)return;
            var previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            bool batch=Application.isBatchMode;
            Directory.CreateDirectory("Assets/ArtSamples/HeroMeshyV01");var scene=UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,batch?UnityEditor.SceneManagement.NewSceneMode.Single:UnityEditor.SceneManagement.NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            try
            {
            var cam=new GameObject("Main Camera").AddComponent<Camera>();cam.tag="MainCamera";cam.orthographic=true;cam.orthographicSize=2.2f;cam.transform.position=new Vector3(-.65f,.6f,-10);cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.07f,.08f,.08f);
            new GameObject("主角动作查看").AddComponent<HeroMeshyPreview>();UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene,"Assets/ArtSamples/HeroMeshyV01/HeroMeshyPreview.scene");
            }
            finally
            {
                if(!batch)
                {
                    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
                    if(previous.IsValid()&&previous.isLoaded)UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
                }
            }
        }
        public static void Preflight(){Debug.Log("[HeroMeshy] COMPILE_PASS");EditorApplication.Exit(0);}
        static void SetCompression(TextureImporter imp){imp.textureCompression=TextureImporterCompression.Compressed;imp.compressionQuality=100;var p=imp.GetPlatformTextureSettings("Standalone");p.overridden=true;p.format=TextureImporterFormat.DXT5;p.maxTextureSize=2048;p.compressionQuality=100;imp.SetPlatformTextureSettings(p);}
        public static void FixTransparentCompressionBatch()
        {
            try
            {
                var m=JsonUtility.FromJson<Manifest>(File.ReadAllText(Root+"import.json"));AssetDatabase.StartAssetEditing();
                try{foreach(var sheet in m.sheets){var imp=(TextureImporter)AssetImporter.GetAtPath(sheet.path);SetCompression(imp);imp.SaveAndReimport();}}finally{AssetDatabase.StopAssetEditing();}
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);Validate();Debug.Log("[HeroMeshy] TRANSPARENT_COMPRESSION_FIX_PASS");EditorApplication.Exit(0);
            }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        public static void ImportBatch(){try{Import();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
        public static void VerifyImportedBatch(){try{Validate();BuildReview();Debug.Log("[HeroMeshy] IMPORT_VERIFY_PASS");EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
        public static void ReimportChangedBatch()
        {
            try
            {
                var changed=JsonUtility.FromJson<ChangedManifest>(File.ReadAllText(Root+"changed_sheets.json"));
                foreach(var path in changed.paths)
                {
                    if(!path.StartsWith(Root+"Sheets/",StringComparison.Ordinal)||!File.Exists(path))throw new Exception("Invalid changed hero sheet: "+path);
                    AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                }
                Validate();BuildReview();Debug.Log("[HeroMeshy] CHANGED_IMPORT_PASS pages="+changed.paths.Length);if(Application.isBatchMode)EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
        }
    }
}
