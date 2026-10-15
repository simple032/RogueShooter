using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using RogueShooter.Demo;
using RogueShooter.Player;
namespace RogueShooter.Iso
{
    public sealed class HeroMeshyRuntimeChecks:MonoBehaviour
    {
        public const string Output="D:/Game Provide/outputs/hero_meshy_v06_integration_20261007";
        [Serializable] class Result{public bool passed;public string version;public int frameSamples;public List<string> checks=new List<string>();}
        readonly Result _result=new Result();Stage1MazeDemo _demo;HeroMeshyPresentation _hero;Stage1IsoActor _actor;PlayerCharge _charge;PlayerRoll _roll;PlayerVitals _vitals;Camera _camera;Vector3 _origin;
        IEnumerator Start(){Directory.CreateDirectory(Output+"/native");var run=Run();while(true){bool next;try{next=run.MoveNext();}catch(Exception e){_result.checks.Add("FAIL "+e);Finish(false);yield break;}if(!next)break;yield return run.Current;}Finish(true);}
        void Finish(bool pass)
        {
            _result.passed=pass;File.WriteAllText(Output+(Application.isEditor?"/runtime_checks.json":"/build_smoke.json"),JsonUtility.ToJson(_result,true));Debug.Log("[HeroMeshy] NATIVE_"+(pass?"PASS":"FAIL")+" frames="+_result.frameSamples);
            RunPause.ResetAll();Time.captureFramerate=0;
#if UNITY_EDITOR
            if(Application.isEditor){if(Application.isBatchMode)UnityEditor.EditorApplication.Exit(pass?0:1);else UnityEditor.EditorApplication.isPlaying=false;return;}
#endif
            Application.Quit(pass?0:1);
        }
        void Check(bool ok,string text){if(!ok)throw new Exception(text);_result.checks.Add("PASS "+text);}
        static void Field(object o,string name,object value){var f=o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);if(f==null)throw new Exception("Field absent: "+name);f.SetValue(o,value);}
        void Charge(float held,Vector3 aim){Field(_charge,"_held",held);Field(_charge,"_charging",true);Field(_charge,"<AimDir>k__BackingField",aim);}
        void Reset()
        {
            RunPause.ResetAll();_vitals.Configure(10000);Field(_charge,"_held",0f);Field(_charge,"_charging",false);Field(_charge,"_recoverUntil",-1f);Field(_roll,"_t",float.MaxValue);
            foreach(var arrow in FindObjectsOfType<ArrowFly>())arrow.CancelFlight();_hero.transform.position=_origin;_hero.Bind(_hero.GetComponent<SpriteRenderer>());_hero.GetComponent<GuaranteedCritActive>().DebugSetBuff(0);
        }
        void Snapshot(string name)
        {
            var framing=_camera.transform.position;if(name=="main_ritual_contact")_camera.transform.position=_hero.transform.position+_hero.VisualOffset+new Vector3(-.7f,.9f,-10);
            var rt=new RenderTexture(1200,900,24);var old=_camera.targetTexture;var active=RenderTexture.active;_camera.targetTexture=rt;_camera.Render();RenderTexture.active=rt;
            var tex=new Texture2D(1200,900,TextureFormat.RGBA32,false);tex.ReadPixels(new Rect(0,0,1200,900),0,0);tex.Apply();File.WriteAllBytes(Output+"/native/"+(Application.isEditor?"":"build_")+name+".png",tex.EncodeToPNG());_camera.targetTexture=old;_camera.transform.position=framing;RenderTexture.active=active;Destroy(tex);rt.Release();Destroy(rt);
        }
        IEnumerator Run()
        {
            yield return null;yield return null;Time.captureFramerate=60;RunPause.ResetAll();
            _demo=FindObjectOfType<Stage1MazeDemo>();Check(_demo!=null,"actual Stage1Maze main scene loaded");_demo.enabled=false;
            foreach(var ai in FindObjectsOfType<RogueShooter.Ai.MobFourStateAi>())ai.enabled=false;foreach(var motor in FindObjectsOfType<PlayerMotor2D>())motor.enabled=false;foreach(var follow in FindObjectsOfType<RogueShooter.Vision.CameraFollow2D>())follow.enabled=false;
            // Close-up evidence uses the actual scene camera without its normal framing controller.
            foreach(var framing in FindObjectsOfType<RogueShooter.Vision.CameraViewService>())framing.enabled=false;
            _hero=_demo.PlayerBody.GetComponent<HeroMeshyPresentation>();Check(_hero!=null,"main player uses Meshy presentation");_origin=_hero.transform.position;_actor=_hero.GetComponent<Stage1IsoActor>();_charge=_hero.GetComponent<PlayerCharge>();_roll=_hero.GetComponent<PlayerRoll>();_vitals=_hero.GetComponent<PlayerVitals>();_charge.enabled=false;_roll.enabled=false;
            _camera=Camera.main;_camera.transform.position=_origin+new Vector3(-.7f,.5f,-10);_camera.transform.rotation=Quaternion.identity;_camera.orthographicSize=2.7f;
            _result.version=HeroMeshyArt.Data.version;Check(_result.version=="hero-meshy/6","correct refined hero resources");
            _actor.enabled=false;var review=new GameObject("Independent true resource review");review.transform.position=_origin;var view=review.AddComponent<HeroMeshyPresentation>();var reviewBody=review.AddComponent<SpriteRenderer>();reviewBody.sortingLayerName=RogueShooter.Art.JianHaiArtCatalog.LayerEntity;view.Bind(reviewBody);_hero.RenderedBody.enabled=false;
            foreach(var c in HeroMeshyArt.Data.clips)foreach(var d in c.directions)
            {
                for(int f=0;f<c.frames;f++){view.ReviewFrame(c.id,d.id,f);CheckFrame(view,c.id,d.id,f);_result.frameSamples++;}
                int selected=c.id=="moving_draw"?27:c.id=="moving_release"?24:c.id=="death"?22:c.id=="roll"?4:c.frames/2;
                view.ReviewFrame(c.id,d.id,selected);if(Application.isEditor||d.id=="se")Snapshot("review_"+c.id+"_"+d.id);yield return null;
            }
            Check(_result.frameSamples==6256,"13 clips and pose grids / 8 actual directions / no mirror");Destroy(review);_hero.RenderedBody.enabled=true;_actor.enabled=true;Reset();yield return null;
            int combinations=0;
            foreach(var d in HeroMeshyArt.Find("draw").directions)
            {
                int aimIndex=HeroMeshyArt.Index(d.id);Vector3 aim=new Vector3(Mathf.Cos(aimIndex*Mathf.PI/4),Mathf.Sin(aimIndex*Mathf.PI/4));
                for(int move=0;move<8;move++)
                {
                    Charge(.7f,aim);Vector3 delta=new Vector3(Mathf.Cos(move*Mathf.PI/4),Mathf.Sin(move*Mathf.PI/4))*.04f;_hero.transform.position+=delta;_hero.Step();
                    int rel=(move-aimIndex+8)%8;CheckFrame(_hero,"moving_draw",d.id,HeroMeshyArt.DrawGrid(rel,1,_hero.Phase));combinations++;
                }
            }
            Check(combinations==64,"all 8 aiming x 8 travel direction combinations use moving lower body");Snapshot("main_moving_full_draw");
            Reset();Charge(.35f,Vector3.right);_hero.Step();_vitals.ApplyHit(2,"animation_check");yield return null;Check(_hero.CurrentClip=="draw","hurt preserves charging pose and charging state");
            Field(_charge,"_charging",false);yield return null;yield return null;Check(_hero.CurrentClip=="hurt","idle hurt reaction uses actual damage event");Snapshot("main_hurt");
            Reset();bool debugStrike=false;Check(_actor.TryBeginAttack(()=>debugStrike=true),"existing F debug strike still starts");yield return new WaitForSeconds(.22f);Check(debugStrike,"existing debug strike hit callback still runs");yield return new WaitForSeconds(.25f);Check(!_actor.Attacking,"existing debug strike clock finishes");
            Reset();Charge(2f,Vector3.right);_hero.Step();Check(_hero.CurrentClip=="full_hold","overcharge holds final draw without automatic arrow");yield return null;Check(_charge.ShotSerial==0,"no projectile from holding full draw");
            Field(_charge,"_charging",false);Field(_charge,"_recoverUntil",-1f);int before=_charge.ShotSerial;Check(_charge.FireAtHeld(.29f,Vector3.right)==ChargeShotKind.None&&_charge.ShotSerial==before,"short draw cancels without projectile");yield return null;Check(_hero.CurrentClip=="idle","cancel clears nocked pose");
            Reset();before=_charge.ShotSerial;Check(_charge.FireAtHeld(.3f,Vector3.right)!=ChargeShotKind.None,"minimum valid .30s shot remains functional");Check(_charge.ShotSerial==before+1&&_hero.CurrentClip=="release_weak","weak shot starts immediate weak release");Snapshot("main_weak_release");
            Reset();Check(_charge.FireAtHeld(.7f,new Vector3(.7071f,-.7071f))!=ChargeShotKind.None,"full draw uses real projectile event");Check(_hero.CurrentClip=="release"&&FindObjectsOfType<ArrowFly>().Length==1,"one projectile and release pose without redrawing");
            var arrowObject=FindObjectOfType<ArrowFly>();Check(arrowObject.GetComponent<SpriteRenderer>().sprite==HeroMeshyArt.Projectile("se"),"real directional Meshy arrow used");Snapshot("main_full_release");
            yield return new WaitForSeconds(.25f);Check(_hero.CurrentClip=="idle","release finishes at .20s gameplay recovery");
            Reset();var focus=_hero.GetComponent<GuaranteedCritActive>();focus.DebugSetBuff(5);before=_charge.ShotSerial;Check(_charge.TryFocusShot(Vector3.right),"focus can fire immediately without draw windup");Check(_hero.CurrentClip=="release","focus starts full release directly");yield return new WaitForSeconds(.78f);Check(_charge.TryFocusShot(Vector3.right)&&_charge.ShotSerial==before+2,"focus repeats at existing .75s interval");Snapshot("main_focus_release");
            foreach(var d in HeroMeshyArt.Find("draw").directions)
            {
                Reset();int aimIndex=HeroMeshyArt.Index(d.id);Vector3 aim=new Vector3(Mathf.Cos(aimIndex*Mathf.PI/4),Mathf.Sin(aimIndex*Mathf.PI/4));
                Charge(.7f,aim);_hero.Step();Vector3 expected=_hero.transform.position+(Vector3)HeroMeshyArt.DirectionData("draw",d.id).muzzles[14];
                _charge.FireAtHeld(.7f,aim);var flight=FindObjectOfType<ArrowFly>();Check(flight!=null&&(flight.transform.position-expected).magnitude<.001f,"actual arrow leaves source-rendered bow tip in "+d.id);
                var sprite=flight.GetComponent<SpriteRenderer>();Check(sprite.sprite==HeroMeshyArt.Projectile(d.id)&&sprite.sortingLayerName==RogueShooter.Art.JianHaiArtCatalog.LayerFx&&!sprite.flipX,"actual arrow orientation and visible FX layer in "+d.id);
            }
            foreach(int rate in new[]{30,60,120})
            {
                Reset();Time.captureFramerate=rate;Charge(.55f,Vector3.right);before=_charge.ShotSerial;Check(_roll.TryStartRoll(Vector2.right),"roll starts at "+rate+" fps");
                typeof(PlayerCharge).GetMethod("ReleaseCharge",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(_charge,null);Check(!_charge.IsCharging&&_charge.ShotSerial==before,"roll cancels draw without arrow at "+rate+" fps");
                Vector3 start=_hero.transform.position;for(int i=0;i<Mathf.CeilToInt(.60f*rate);i++){float moveDt=_roll.AdvanceMotion(1f/rate);_hero.transform.position+=Vector3.right*moveDt*PlayerRoll.MoveSpeed;_hero.Step();if(i==Mathf.FloorToInt(.45f*rate))Check(_hero.CurrentClip=="roll"&&_hero.CurrentFrame>=8,"true standing recovery frames at "+rate+" fps");yield return null;}
                Check(Mathf.Abs((_hero.transform.position-start).x-4.8f)<.005f,"game owns one 4.8u roll displacement at "+rate+" fps");
            }
            Reset();Time.captureFramerate=60;Charge(.55f,Vector3.right);_hero.Step();int frozen=_hero.CurrentFrame;RunPause.MenuOpen=true;yield return null;yield return null;Check(_hero.CurrentFrame==frozen,"menu pause freezes action pose");RunPause.ResetAll();
            var altar=FindObjectOfType<StageAltarView>();Check(altar!=null,"actual altar exists");_hero.transform.position=altar.transform.position+Vector3.down*2;_origin=_hero.transform.position;_camera.transform.position=_origin+new Vector3(-.7f,.5f,-10);_camera.orthographicSize=3.2f;var ritual=_hero.GetComponent<HeroTorchRitual>();if(ritual==null)ritual=_hero.gameObject.AddComponent<HeroTorchRitual>();Check(altar.BeginIgnition(null),"altar ignition begins on actual 1.8s clock");ritual.Begin(altar);Vector3 root=_hero.transform.position;float until=Time.realtimeSinceStartup+10;
            while(altar.Ignition01<.60f){if(Time.realtimeSinceStartup>=until)throw new Exception("ritual progress timeout");yield return null;}
            _hero.Step();Check((_hero.transform.position-root).sqrMagnitude<.000001f,"ritual visual approach leaves gameplay root unchanged");Check((_hero.transform.position+_hero.TorchOffset-ritual.ContactTarget).magnitude<.08f,"actual torch contact aligns with actual altar basin");Snapshot("main_ritual_contact");while(ritual.Busy){if(Time.realtimeSinceStartup>=until)throw new Exception("ritual finish timeout");yield return null;}yield return null;yield return null;Check(_hero.VisualOffset.sqrMagnitude<.000001f,"ritual returns to root and cleans torch layer");
            Reset();_vitals.ApplyHit(100000,"animation_death");RunPause.EnterSettled();yield return null;yield return null;Check(_hero.CurrentClip=="death","death begins after actual lethal damage");RunPause.MenuOpen=true;frozen=_hero.CurrentFrame;yield return null;yield return null;Check(_hero.CurrentFrame==frozen,"menu pause also freezes death");RunPause.MenuOpen=false;until=Time.realtimeSinceStartup+10;while(!_hero.DeathFinished){if(Time.realtimeSinceStartup>=until)throw new Exception("death timeout");yield return null;}Check(_hero.CurrentFrame==22&&Time.timeScale==0,"death completes and holds on settlement pause");Snapshot("main_death_hold");
        }
        static void CheckFrame(HeroMeshyPresentation view,string clip,string direction,int frame){var body=view.RenderedBody;if(view.CurrentClip!=clip||view.Facing!=direction||view.CurrentFrame!=frame||body.flipX||body.sprite!=HeroMeshyArt.Frame(clip,direction,frame)||body.sprite.texture.format!=TextureFormat.DXT5)throw new Exception("Hero playback/format mismatch "+clip+"/"+direction+"/"+frame);}
    }
}
