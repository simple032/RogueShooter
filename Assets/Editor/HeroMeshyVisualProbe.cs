using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RogueShooter.Iso;
namespace RogueShooter.EditorTools
{
    public static class HeroMeshyVisualProbe
    {
        public static void RunBatch()
        {
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var view=new GameObject("Actual atlas visual probe").AddComponent<HeroMeshyPresentation>();view.Bind(view.gameObject.AddComponent<SpriteRenderer>());view.ReviewFrame("roll","se",4);
                var cam=new GameObject("Camera").AddComponent<Camera>();cam.orthographic=true;cam.orthographicSize=1.5f;cam.transform.position=new Vector3(0,.75f,-10);cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.clear;
                Debug.Log("[HeroMeshy] actual texture "+view.RenderedBody.sprite.texture.format+" "+view.RenderedBody.sprite.texture.width+"x"+view.RenderedBody.sprite.texture.height+" rect "+view.RenderedBody.sprite.rect);
                var original=view.RenderedBody.sprite;Capture(cam,"isolated_roll");view.RenderedBody.sharedMaterial=new Material(Shader.Find("Sprites/Default"));var block=new MaterialPropertyBlock();block.SetTexture("_MainTex",original.texture);view.RenderedBody.SetPropertyBlock(block);Capture(cam,"isolated_roll_default_shader");
                var direct=new Texture2D(384,384,TextureFormat.RGBA32,false);direct.LoadImage(File.ReadAllBytes("D:/Game Provide/Art_Source/JianHai/HeroArcher_Meshy_v01/renders/Color/roll/roll_se_004.png"));view.RenderedBody.sprite=Sprite.Create(direct,new Rect(0,0,384,384),new Vector2(.5f,.25f),128);block.SetTexture("_MainTex",direct);view.RenderedBody.SetPropertyBlock(block);Capture(cam,"isolated_roll_direct_png");
                var importer=(TextureImporter)AssetImporter.GetAtPath("Assets/Resources/HeroArcherMeshyV01/Sheets/roll_se_00.png");importer.textureCompression=TextureImporterCompression.Compressed;importer.compressionQuality=100;importer.SaveAndReimport();view.RenderedBody.sprite=original;HeroMeshyArt.Apply(view.RenderedBody);Capture(cam,"isolated_roll_dxt5");Debug.Log("[HeroMeshy] alternate texture "+original.texture.format);Debug.Log("[HeroMeshy] VISUAL_PROBE_PASS");EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        static void Capture(Camera cam,string name){var rt=new RenderTexture(384,384,24);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var tex=new Texture2D(384,384,TextureFormat.RGBA32,false);tex.ReadPixels(new Rect(0,0,384,384),0,0);tex.Apply();File.WriteAllBytes(HeroMeshyRuntimeChecks.Output+"/native/"+name+".png",tex.EncodeToPNG());RenderTexture.active=null;cam.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);}
    }
}
