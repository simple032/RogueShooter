using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RogueShooter.Demo;
using RogueShooter.Player;
using RogueShooter.Iso;
namespace RogueShooter.EditorTools
{
    [InitializeOnLoad] public static class HeroRepairCadenceTest
    {
        const string Key="HeroRepair.Cadence";
        static HeroRepairCadenceTest(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)){SessionState.SetBool(Key,false);new GameObject("主角返修步频检查").AddComponent<HeroRepairCadenceRunner>();}};}
        public static void RunBatch(){SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/Stage1Maze.scene");EditorApplication.isPlaying=true;}
        public static void RunCurrentScene(){if(EditorApplication.isPlaying||EditorApplication.isCompiling)throw new Exception("Editor is busy");if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!="Assets/Scenes/Stage1Maze.scene")throw new Exception("Keep current scene: expected Stage1Maze");SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;}
    }
    public class HeroRepairCadenceRunner:MonoBehaviour
    {
        [Serializable] class Row{public int fps;public int direction;public bool charging;public float cycles;public float expected;}
        [Serializable] class Report{public bool passed;public string scope="actual main-scene presentation cadence only; art and foot contact NOT accepted";public List<Row> samples=new List<Row>();}
        IEnumerator Start()
        {
            var report=new Report();var run=Run(report);
            while(true){bool next;try{next=run.MoveNext();}catch(Exception e){Debug.LogException(e);Finish(report,false);yield break;}if(!next)break;yield return run.Current;}
            Finish(report,true);
        }
        void Finish(Report r,bool ok){r.passed=ok;Directory.CreateDirectory(HeroMeshyRuntimeChecks.Output);File.WriteAllText(HeroMeshyRuntimeChecks.Output+"/native_cadence.json",JsonUtility.ToJson(r,true));Time.captureFramerate=0;RunPause.ResetAll();if(Application.isBatchMode)EditorApplication.Exit(ok?0:1);else EditorApplication.isPlaying=false;}
        IEnumerator Run(Report report)
        {
            yield return null;yield return null;
            var demo=FindObjectOfType<Stage1MazeDemo>();if(demo==null)throw new Exception("No actual Stage1Maze player");demo.enabled=false;
            foreach(var ai in FindObjectsOfType<RogueShooter.Ai.MobFourStateAi>())ai.enabled=false;
            foreach(var motor in FindObjectsOfType<PlayerMotor2D>())motor.enabled=false;
            var hero=demo.PlayerBody.GetComponent<HeroMeshyPresentation>();var charge=hero.GetComponent<PlayerCharge>();var roll=hero.GetComponent<PlayerRoll>();
            hero.GetComponent<Stage1IsoActor>().enabled=false;charge.enabled=false;roll.enabled=false;
            var field=typeof(PlayerCharge).GetField("_charging",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var held=typeof(PlayerCharge).GetField("_held",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            Vector3 origin=hero.transform.position;
            foreach(int rate in new[]{30,60,120})foreach(bool slow in new[]{false,true})foreach(int dir in new[]{0,1,2,3,4,5,6,7})
            {
                RunPause.ResetAll();Time.captureFramerate=rate;hero.GetComponent<PlayerVitals>().Configure(10000);hero.transform.position=origin;hero.Bind(hero.GetComponent<SpriteRenderer>());field.SetValue(charge,slow);held.SetValue(charge,.7f);yield return null;
                float cycles=0,previous=hero.Phase;Vector3 travel=new Vector3(Mathf.Cos(dir*Mathf.PI/4),Mathf.Sin(dir*Mathf.PI/4));
                int ticks=Mathf.RoundToInt(.8f*rate);
                for(int i=0;i<ticks;i++){hero.transform.position+=travel*(slow?3:6)*Time.deltaTime;hero.Step();cycles+=Mathf.Repeat(hero.Phase-previous,1);previous=hero.Phase;yield return null;}
                float expected=.8f/(slow?.85f:.65f);report.samples.Add(new Row{fps=rate,direction=dir,charging=slow,cycles=cycles,expected=expected});
                if(Mathf.Abs(cycles-expected)>.015f)throw new Exception("Cadence wrong "+rate+"/"+dir+"/"+slow+" "+cycles+" expected "+expected);
            }
            field.SetValue(charge,false);Debug.Log("[HeroRepair] CADENCE_PASS 48 actual scene cases");
        }
    }
}
