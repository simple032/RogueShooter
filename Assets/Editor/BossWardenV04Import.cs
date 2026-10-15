using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using RogueShooter.Boss;

namespace RogueShooter.EditorTools
{
    public static class BossWardenV04Import
    {
        public const string Root="Assets/Resources/BossWardenV04/";
        [Serializable] public class RectData {public string name; public int x,y,size;}
        [Serializable] public class Sheet {public string path; public Vector2 pivot; public RectData[] rects; public bool emission;}
        [Serializable] public class Manifest {public Sheet[] sheets;}
        [MenuItem("JianHai/BOSS v05/Import Animation Pack")]
        public static void Import()
        {
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Root+"import.json"));
            int count=0;
            foreach(var sheet in manifest.sheets)
            {
                string glow=sheet.path.Replace(".png","_e.png");
                if(sheet.emission)
                {
                    AssetDatabase.ImportAsset(glow,ImportAssetOptions.ForceSynchronousImport);
                    var e=(TextureImporter)AssetImporter.GetAtPath(glow);
                    e.textureType=TextureImporterType.Default;e.sRGBTexture=true;e.mipmapEnabled=false;e.npotScale=TextureImporterNPOTScale.None;
                    e.maxTextureSize=4096;e.isReadable=false;e.textureCompression=TextureImporterCompression.CompressedHQ;e.filterMode=FilterMode.Bilinear;e.wrapMode=TextureWrapMode.Clamp;
                    e.SaveAndReimport();
                }
                AssetDatabase.ImportAsset(sheet.path,ImportAssetOptions.ForceSynchronousImport);
                var imp=(TextureImporter)AssetImporter.GetAtPath(sheet.path);
                imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Multiple;
                imp.spritePixelsPerUnit=128;imp.mipmapEnabled=false;imp.alphaIsTransparency=true;
                imp.npotScale=TextureImporterNPOTScale.None;imp.maxTextureSize=4096;imp.isReadable=false;
                imp.textureCompression=TextureImporterCompression.CompressedHQ;imp.filterMode=FilterMode.Bilinear;imp.wrapMode=TextureWrapMode.Clamp;
                var settings=new TextureImporterSettings();imp.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;imp.SetTextureSettings(settings);
                var rects=new SpriteMetaData[sheet.rects.Length];
                for(int i=0;i<rects.Length;i++)
                {var r=sheet.rects[i];rects[i]=new SpriteMetaData{name=r.name,rect=new Rect(r.x,r.y,r.size,r.size),alignment=(int)SpriteAlignment.Custom,pivot=sheet.pivot};}
                imp.spritesheet=rects;
                imp.secondarySpriteTextures=sheet.emission?new[]{new SecondarySpriteTexture{name="_EmissionTex",texture=AssetDatabase.LoadAssetAtPath<Texture2D>(glow)}}:Array.Empty<SecondarySpriteTexture>();
                imp.SaveAndReimport();count+=rects.Length;
            }
            var mat=AssetDatabase.LoadAssetAtPath<Material>(Root+"WardenMaterial.mat");
            if(mat==null){mat=new Material(Shader.Find("JianHai/IsoSpriteLit"));AssetDatabase.CreateAsset(mat,Root+"WardenMaterial.mat");}
            mat.SetColor("_Ambient",Color.white);mat.SetColor("_LightColor",Color.black);mat.SetFloat("_EmissionGain",.65f);
            EditorUtility.SetDirty(mat);AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("[BossV05] import sprites="+count+" source="+BossWardenArt.Data.version);
        }
        public static void Validate()
        {
            var m=JsonUtility.FromJson<Manifest>(File.ReadAllText(Root+"import.json"));int total=0;
            foreach(var sheet in m.sheets)
            {
                var assets=AssetDatabase.LoadAllAssetsAtPath(sheet.path);int n=0;
                foreach(var asset in assets)if(asset is Sprite s)
                {
                    n++;total++;
                    if(s.pixelsPerUnit!=128||s.rect.width!=sheet.rects[0].size||(s.pivot/s.rect.size-sheet.pivot).sqrMagnitude>.000001f)
                        throw new Exception("Invalid BOSS pivot/size: "+s.name);
                }
                if(n!=sheet.rects.Length)throw new Exception("Missing BOSS sprites: "+sheet.path+" got "+n);
            }
            string regression=BossFightChecks.Run();if(regression!=null)throw new Exception("BOSS regression: "+regression);
            Debug.Log("[BossV04] ASSET_CHECK_PASS sprites="+total+" gameplay rules regression=PASS");
        }
        [MenuItem("JianHai/BOSS v05/Open Animation Review")]
        public static void OpenReview()
        {
            const string path="Assets/ArtSamples/BossWardenV04/BossWardenV04Preview.scene";
            if(!File.Exists(path))BuildReview();
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path);
        }
        public static void BuildReview()
        {
            string folder="Assets/ArtSamples/BossWardenV04";Directory.CreateDirectory(folder);
            var scene=UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);
            var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";
            camera.orthographic=true;camera.orthographicSize=3.2f;camera.transform.position=new Vector3(-.7f,1.3f,-10);
            camera.backgroundColor=new Color(.055f,.061f,.067f);camera.clearFlags=CameraClearFlags.SolidColor;
            new GameObject("BOSS 动作验收").AddComponent<BossWardenPreview>();
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene,folder+"/BossWardenV04Preview.scene");
        }
        public static void ImportBatch()
        { try { Import();BuildReview();EditorApplication.Exit(0); }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);} }
    }
}
