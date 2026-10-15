using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using RogueShooter.Demo;
using RogueShooter.Player;
using RogueShooter.Vision;

namespace RogueShooter.Boss
{
    /// <summary>Optional review/smoke launchers. Normal game launch keeps the existing start menu.</summary>
    public sealed class BossWardenLaunch : MonoBehaviour
    {
        static string Output
        {
            get
            {
                var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--boss-smoke-output");
                return index>=0&&index+1<args.Length?args[index+1]:"D:/Game Provide/outputs/boss_v05_integration_20261006";
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            var args=Environment.GetCommandLineArgs();
            if(Array.IndexOf(args,"--boss-v05-review")>=0||Array.IndexOf(args,"--boss-v04-review")>=0)SceneManager.LoadScene("BossWardenV04Preview");
            else if(Array.IndexOf(args,"--boss-v05-smoke")>=0||Array.IndexOf(args,"--boss-v04-smoke")>=0)
            {var go=new GameObject("BOSS build integration probe");DontDestroyOnLoad(go);go.AddComponent<BossWardenLaunch>();}
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(Output+"/native");
            SceneManager.LoadScene("Stage1Maze");yield return null;yield return null;
            var task=CheckBuild();
            while(true)
            {
                bool next;
                try {next=task.MoveNext();}
                catch(Exception e){File.WriteAllText(Output+"/build_smoke.json",JsonUtility.ToJson(new Smoke{passed=false,detail=e.ToString()},true));Application.Quit(1);yield break;}
                if(!next)break;yield return task.Current;
            }
            File.WriteAllText(Output+"/build_smoke.json",JsonUtility.ToJson(new Smoke{passed=true,detail="Standalone Windows: main-scene BOSS, 14x8 resource lookup, transition, world shield, settlement death.",version=BossWardenArt.Data.version},true));
            Application.Quit(0);
        }
        [Serializable] class Smoke {public bool passed;public string version;public string detail;}
        static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
        IEnumerator CheckBuild()
        {
            var demo=FindObjectOfType<Stage1MazeDemo>();Require(demo!=null,"main scene absent");
            var boss=demo.FinalBoss;Require(boss!=null&&boss.Combat.Presentation!=null,"BOSS runtime absent");demo.enabled=false;
            foreach(var motor in FindObjectsOfType<PlayerMotor2D>())motor.enabled=false;
            foreach(var ai in FindObjectsOfType<RogueShooter.Ai.MobFourStateAi>())ai.enabled=false;
            foreach(var follow in FindObjectsOfType<CameraFollow2D>())follow.enabled=false;
            var camera=Camera.main;camera.transform.position=boss.transform.position+new Vector3(0,1,-10);camera.transform.rotation=Quaternion.identity;camera.orthographicSize=3;
            boss.PlayerBody.position=boss.transform.position+Vector3.down*5;
            foreach(var clip in BossWardenArt.Data.clips)foreach(var d in clip.directions)
                Require(BossWardenArt.Frame(clip.id,d.id,0)!=null,"missing player-build clip "+clip.id+"/"+d.id);
            RunPause.ResetAll();boss.BeginEnter();yield return null;Capture(camera,"build_p1_native");
            boss.DealDamage(boss.Brain.Hp*.55f,Vector3.zero,true);
            Require(boss.Transitioning,"no phase transition");float deadline=Time.realtimeSinceStartup+12;
            while(boss.Transitioning){Require(Time.realtimeSinceStartup<deadline,"transition timeout");yield return null;}
            Require(boss.Brain.Phase==BossPhase.P2&&boss.Combat.Presentation.DroppedShield!=null,"P2/shield absent");
            Capture(camera,"build_p2_native");boss.DealDamage(100000,Vector3.zero,true);
            while(!boss.Combat.Presentation.DeathFinished){Require(Time.realtimeSinceStartup<deadline,"death timeout");yield return null;}
            Require(boss.FightWon&&boss.Combat.Presentation.CurrentClip=="p2_death","settlement death absent");Capture(camera,"build_death_native");
        }
        static void Capture(Camera camera,string name)
        {
            var rt=new RenderTexture(1200,900,24);var old=camera.targetTexture;var active=RenderTexture.active;
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var tex=new Texture2D(1200,900,TextureFormat.RGBA32,false);tex.ReadPixels(new Rect(0,0,1200,900),0,0);tex.Apply();
            File.WriteAllBytes(Output+"/native/"+name+".png",tex.EncodeToPNG());camera.targetTexture=old;RenderTexture.active=active;
            Destroy(tex);rt.Release();Destroy(rt);
        }
    }
}
