#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using RogueShooter.Demo;
using RogueShooter.Player;
using RogueShooter.Vision;

namespace RogueShooter.Boss
{
    public sealed class BossWardenNativeChecks : MonoBehaviour
    {
        public const string Output="D:/Game Provide/outputs/boss_v05_integration_20261006";
        [Serializable] class Result {public string version;public bool passed;public int tickSamples;public List<string> checks=new List<string>();}
        readonly Result _result=new Result();Camera _camera;BossFightDriver _fight;Vector3 _origin;
        IEnumerator Start()
        {
            Directory.CreateDirectory(Output+"/native");Time.captureFramerate=20;
            var routine=Run();
            while(true)
            {
                bool next;
                try {next=routine.MoveNext();}
                catch(Exception e){_result.checks.Add("FAIL "+e);Finish(false);yield break;}
                if(!next)break;yield return routine.Current;
            }
            Finish(true);
        }
        void Finish(bool passed)
        {
            _result.passed=passed;File.WriteAllText(Output+"/runtime_checks.json",JsonUtility.ToJson(_result,true));
            Debug.Log("[BossV04] NATIVE_RUNTIME_"+(passed?"PASS":"FAIL")+" samples="+_result.tickSamples);
            EditorApplication.Exit(passed?0:1);
        }
        void Check(bool ok,string text){if(!ok)throw new Exception(text);_result.checks.Add("PASS "+text);}
        static void Field(object obj,string name,object value){obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(obj,value);}
        void Snapshot(string name)
        {
            var rt=new RenderTexture(1200,900,24);var old=_camera.targetTexture;var active=RenderTexture.active;
            _camera.targetTexture=rt;_camera.Render();RenderTexture.active=rt;
            var tex=new Texture2D(1200,900,TextureFormat.RGBA32,false);tex.ReadPixels(new Rect(0,0,1200,900),0,0);tex.Apply();
            File.WriteAllBytes(Output+"/native/"+name+".png",tex.EncodeToPNG());
            _camera.targetTexture=old;RenderTexture.active=active;Destroy(tex);rt.Release();Destroy(rt);
        }
        void ResetBrain(bool p2=false)
        {
            RunPause.ResetAll();var b=_fight.Brain;b.Reset();b.NotifyEnter();b.Tick(0,false,true);b.Tick(0,false,true);
            if(p2)b.ApplyDamage(b.MaxHp*.51f);
            _fight.ClearProjectiles();_fight.transform.position=_origin;_fight.Combat.ResetPresentation();Field(_fight.Combat,"_face",Vector3.down);
            // This harness resets the brain between cases. Production fights never reuse move indices.
            ((HashSet<long>)typeof(BossFightDriver).GetField("_attackHits",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(_fight)).Clear();
        }
        IEnumerator Run()
        {
            yield return null;yield return null;
            _result.version=BossWardenArt.Data.version;
            Check(_result.version=="boss-redesign/5-side-rest-high-slam","v05 side-rest / overhead-slam resources loaded");
            var demo=FindObjectOfType<Stage1MazeDemo>();Check(demo!=null,"main Stage1Maze scene loaded");
            _fight=demo.FinalBoss;Check(_fight!=null&&_fight.Combat.Presentation!=null,"main game owns v04 BOSS presentation");
            _origin=_fight.transform.position;demo.enabled=false;
            foreach(var ai in FindObjectsOfType<RogueShooter.Ai.MobFourStateAi>())ai.enabled=false;
            foreach(var motor in FindObjectsOfType<PlayerMotor2D>())motor.enabled=false;
            foreach(var follow in FindObjectsOfType<CameraFollow2D>())follow.enabled=false;
            _camera=Camera.main;_camera.transform.position=_origin+new Vector3(0,-.5f,-10);_camera.transform.rotation=Quaternion.identity;_camera.orthographicSize=5.5f;
            _fight.PlayerBody.position=_origin+Vector3.down*6;_fight.PlayerBody.GetComponent<PlayerVitals>().Configure(10000);
            RunPause.ResetAll();_fight.BeginEnter();_fight.enabled=false;
            // Independent visual review, separated from AI/action execution.
            var preview=new GameObject("Independent BOSS v04 sample");preview.transform.position=_origin;
            var body=preview.AddComponent<SpriteRenderer>();body.sortingLayerName="Entity";
            var view=preview.AddComponent<BossWardenPresentation>();view.Bind(body);
            _fight.Combat.BodyRenderer.enabled=false;
            foreach(var clip in BossWardenArt.Data.clips)
            {
                foreach(var dir in clip.directions)
                {
                    for(int tick=0;tick<clip.ticks.Length;tick++)
                    {
                        int frame=clip.FrameAt(tick/20f);view.ShowFrame(clip.id,dir.id,frame);
                        if(body.sprite.name!=$"jh_boss_warden_{clip.id}_{dir.id}_{clip.ticks[tick]:00}"||body.flipX)
                            throw new Exception("Frame mapping/mirror mismatch "+clip.id+"/"+dir.id+"/"+tick);
                        _result.tickSamples++;
                    }
                    int moment=clip.id.Contains("death")?10:clip.id=="p2_leap"?31:clip.id=="p1_to_p2"?12:clip.ticks.Length/3;
                    view.ShowFrame(clip.id,dir.id,clip.FrameAt(moment/20f));
                    Snapshot("review_"+clip.id+"_"+dir.id);
                    yield return null;
                }
            }
            Check(_result.tickSamples==4136,"14 clips x 8 real directions evaluated through production sprite player");
            // Capture the revised rest, aim and overhead silhouettes in the production renderer.
            foreach(var sample in new[]{("p1_idle",0),("p2_idle",0),("p1_single",10),("p2_triple",8),("p2_bash",8),("p2_leap",23)})
            {
                var clip=BossWardenArt.Find(sample.Item1);
                foreach(var dir in clip.directions)
                {
                    view.ShowFrame(clip.id,dir.id,clip.FrameAt(sample.Item2/20f));
                    Snapshot("v05_pose_"+clip.id+"_"+dir.id);yield return null;
                }
            }
            Destroy(preview);_fight.Combat.BodyRenderer.enabled=true;yield return null;
            // Execute the actual six combat branches against the real main-scene actor and maze.
            // P1 ranged is always the burst now; StraightShot has no selection path to exercise here.
            foreach(var move in new[]{BossMoveId.BurstShot,BossMoveId.WarningCharge,BossMoveId.ShieldBash,BossMoveId.TripleShot,BossMoveId.LeapSlam,BossMoveId.CrossbowBash})
            {
                bool p2=move==BossMoveId.TripleShot||move==BossMoveId.LeapSlam||move==BossMoveId.CrossbowBash;ResetBrain(p2);
                _fight.PlayerBody.position=_origin+Vector3.down*(FinalBossRules.IsMelee(move)?1.8f:6f);
                Check(_fight.Brain.TryStart(move),"start actual move "+move);
                int fired=_fight.Combat.ArrowsFired,hits=_fight.VolleyHitCount;var skill=FinalBossRules.Skill(move);
                int steps=Mathf.CeilToInt((skill.Windup+skill.Active+skill.Recovery)*20f);
                for(int i=0;i<=steps;i++)
                {
                    _fight.Combat.Tick(.05f);
                    if(_fight.Brain.CurrentMove!=BossMoveId.None)
                    {
                        string expected=BossWardenArt.MoveClip(move);
                        if(_fight.Combat.Presentation.CurrentClip!=expected)throw new Exception("Combat animation consumer mismatch "+move);
                        if(Mathf.Abs(_fight.Brain.ActionElapsed-skill.Windup)<.026f)Snapshot("combat_"+expected+"_active");
                        if(move==BossMoveId.LeapSlam&&Mathf.Abs(_fight.Brain.ActionElapsed-1.55f)<.026f)Snapshot("combat_p2_leap_land");
                    }
                    yield return null;_fight.Brain.Tick(.05f,false,_fight.ProjectileCount==0);
                }
                int expectedShots=move==BossMoveId.BurstShot||move==BossMoveId.TripleShot?3:0;
                Check(_fight.Combat.ArrowsFired-fired==expectedShots,"correct shot count "+move+"="+expectedShots);
                if(FinalBossRules.IsMelee(move))Check(_fight.VolleyHitCount>hits,"actual melee contact "+move);
                if(move==BossMoveId.LeapSlam)Check((_fight.transform.position-_fight.Combat.LeapTarget).magnitude<.001f,"jump lands at gameplay target without doubled travel");
                _fight.ClearProjectiles();
            }
            ResetBrain();_fight.PlayerBody.position=_origin+Vector3.down*6;
            float hp=_fight.Brain.Hp;_fight.enabled=true;_fight.DealDamage(hp*.55f,Vector3.zero,true);
            Check(_fight.Transitioning&&Time.timeScale==0,"phase transition on unscaled clock");
            while(_fight.Transitioning&&_fight.TransitionElapsed<.6f)yield return null;
            Check(_fight.Combat.Presentation.CurrentClip=="p1_to_p2","transition plays supplied clip");
            Check(_fight.Combat.Presentation.DroppedShield!=null,"shield released as independent prop");
            Snapshot("game_transition_shield_drop");
            while(_fight.Transitioning)yield return null;
            Check(!_fight.Transitioning&&Time.timeScale==1&&_fight.Brain.Phase==BossPhase.P2,"transition restores gameplay and P2");
            var shield=_fight.Combat.Presentation.DroppedShield;Vector3 shieldAt=shield.transform.position;
            _fight.transform.position+=Vector3.right*.5f;yield return null;
            Check(shield.transform.position==shieldAt,"discarded shield stays at world anchor");
            Snapshot("game_phase2_single_hand");
            _fight.DealDamage(100000,Vector3.zero,true);
            while(!_fight.Combat.Presentation.DeathFinished)yield return null;
            Check(_fight.FightWon&&_fight.Combat.Presentation.DeathFinished&&_fight.Combat.Presentation.CurrentClip=="p2_death","P2 death completes despite settlement pause");
            Check(_fight.ProjectileCount==0&&_fight.Combat.WarningCount==0,"death clears combat projectiles and telegraphs");Snapshot("game_p2_death");
            RunPause.ResetAll();var direct=new GameObject("Direct lethal P1 runtime regression").AddComponent<BossFightDriver>();
            direct.transform.position=_origin;direct.BindArena(null,_fight.Maze,_fight.Arena,_fight.PlayerBody);direct.BeginEnter();direct.DealDamage(100000,Vector3.zero,true);
            while(!direct.Combat.Presentation.DeathFinished)yield return null;
            Check(direct.Combat.Presentation.DeathFinished&&direct.Combat.Presentation.CurrentClip=="p1_death"&&direct.Brain.Phase2Transitions==0,"direct P1 lethal uses shield death without P2 transition");
            _fight.Combat.BodyRenderer.enabled=false;Snapshot("game_p1_direct_death");
        }
    }
}
#endif
