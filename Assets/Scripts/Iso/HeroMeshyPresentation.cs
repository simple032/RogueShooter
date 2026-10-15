using UnityEngine;
using RogueShooter.Player;
using RogueShooter.Demo;
namespace RogueShooter.Iso
{
    /// <summary>Presentation-only clocks. Charge, roll displacement, shots and damage remain gameplay-owned.</summary>
    public sealed class HeroMeshyPresentation : MonoBehaviour
    {
        SpriteRenderer _body,_owner;Vector3 _last,_travel=Vector3.right;float _clock,_phase,_release=-1,_releaseProgress=1,_hurtAge=1,_death=-1;int _seenHurt;
        public string Facing{get;private set;}="s";
        public string CurrentClip{get;private set;}="idle";
        public int CurrentFrame{get;private set;}
        public bool Moving{get;private set;}
        public float Phase=>_phase;
        public bool DeathFinished=>_death>=1.1f;
        public SpriteRenderer RenderedBody=>_body;
        public Vector3 VisualOffset=>_body!=null?_body.transform.localPosition:Vector3.zero;
        public void ReviewFrame(string clip,string facing,int frame){Facing=facing;Show(clip,frame);}
        public void Bind(SpriteRenderer renderer){_owner=renderer;if(_body==null){var visual=new GameObject("Meshy hero visual");visual.transform.SetParent(transform,false);_body=visual.AddComponent<SpriteRenderer>();_body.sortingLayerID=renderer.sortingLayerID;_body.sortingOrder=renderer.sortingOrder;_body.spriteSortPoint=SpriteSortPoint.Pivot;}renderer.enabled=false;_last=transform.position;_clock=_phase=0;_release=_death=-1;_hurtAge=1;var v=GetComponent<PlayerVitals>();_seenHurt=v==null?0:v.DamageSerial;Show("idle",0);}
        void Ensure(){if(_body==null)Bind(GetComponent<SpriteRenderer>());}
        public void BeginRelease(Vector3 aim,float progress)
        {
            Ensure();var roll=GetComponent<PlayerRoll>();if(roll!=null&&roll.LocksActions)return;
            Facing=HeroMeshyArt.Facing(aim);_release=0;_releaseProgress=Mathf.Clamp01(progress);if(Moving)Show("moving_release",HeroMeshyArt.ReleaseGrid(HeroMeshyArt.RelativeDirection(Facing,_travel),progress,0,_phase));else Show(progress<.65f?"release_weak":"release",0);
        }
        public Vector3 LaunchOffset(Vector3 aim,float progress)
        {
            Ensure();string facing=HeroMeshyArt.Facing(aim);string clip=Moving?"moving_draw":"draw";
            int frame=Moving?HeroMeshyArt.DrawGrid(HeroMeshyArt.RelativeDirection(facing,_travel),progress,_phase):Mathf.Clamp(Mathf.RoundToInt(progress*14),0,14);
            return HeroMeshyArt.DirectionData(clip,facing).muzzles[frame];
        }
        public Vector3 TorchOffset=>VisualOffset+(Vector3)HeroMeshyArt.DirectionData("ritual",Facing).torchTips[Mathf.Clamp(CurrentFrame,0,36)];
        // World distance and screen-projected foot sweeps have different units.
        // Use one world-space cadence for every facing; avoid fast tiny steps.
        void AdvanceGait(float distance,float dt,bool walk)
        {
            float cycle=walk?.85f:.65f,referenceSpeed=walk?3f:6f;
            float cycles=Mathf.Min(Mathf.Max(0,distance)/(referenceSpeed*cycle),Mathf.Max(0,dt)/cycle);
            _phase=Mathf.Repeat(_phase+cycles,1);
        }
        public void Step()
        {
            Ensure();if(_body==null)return;var vitals=GetComponent<PlayerVitals>();var roll=GetComponent<PlayerRoll>();var charge=GetComponent<PlayerCharge>();var ritual=GetComponent<HeroTorchRitual>();
            if(vitals!=null&&vitals.IsDown)
            {
                if(_death<0){_death=0;_release=-1;}_body.transform.localPosition=Vector3.zero;Moving=false;_last=transform.position;_body.color=Color.white;
                if(!RunPause.MenuOpen)_death=Mathf.Min(1.1f,_death+Time.unscaledDeltaTime);
                Show("death",Mathf.Min(22,Mathf.FloorToInt(_death*20f+.0001f)));return;
            }
            _death=-1;
            if(vitals!=null&&vitals.DamageSerial!=_seenHurt){_seenHurt=vitals.DamageSerial;_hurtAge=0;}
            if(!RunPause.IsPaused)_hurtAge+=Time.deltaTime;
            _body.color=_hurtAge<.12f?new Color(1,.28f,.22f):Color.white;
            if(ritual!=null&&ritual.Busy)
            {
                Moving=false;_release=-1;_last=transform.position;Facing=HeroMeshyArt.Facing(ritual.Aim);float p=ritual.Progress;
                Vector3 contact=HeroMeshyArt.DirectionData("ritual",Facing).torchTips[22];Vector3 shift=ritual.ContactTarget-(transform.position+contact);shift.z=0;
                float approach=p<.2f?Mathf.SmoothStep(0,1,p/.2f):p>.8f?Mathf.SmoothStep(0,1,(1-p)/.2f):1;
                Vector3 previous=_body.transform.localPosition;_body.transform.localPosition=shift*approach;
                if(p<.2f||p>.8f){AdvanceGait((_body.transform.localPosition-previous).magnitude,Time.deltaTime,true);Show("walk",Mathf.Min(11,Mathf.FloorToInt(_phase*12)));}
                else{float u=p<=.6f?(p-.2f)*1.5f:.6f+(p-.6f)*2;Show("ritual",Mathf.Clamp(Mathf.RoundToInt(u*36),0,36));}return;
            }
            _body.transform.localPosition=Vector3.zero;
            if(RunPause.IsPaused||Time.deltaTime<=0){_last=transform.position;return;}
            Vector3 delta=transform.position-_last;delta.z=0;_last=transform.position;
            bool walking=delta.magnitude>.0001f&&delta.magnitude<Mathf.Max(.5f,Time.deltaTime*30);
            if(roll!=null&&roll.LocksActions)
            {_release=-1;Moving=roll.IsRolling;Facing=HeroMeshyArt.Facing(roll.RollDirection);Show("roll",Mathf.Clamp(Mathf.FloorToInt(roll.RollElapsed*20f+.0001f),0,12));return;}
            if(walking)
            {
                _travel=delta.normalized;bool slowGait=charge!=null&&charge.IsCharging||_release>=0;
                AdvanceGait(delta.magnitude,Time.deltaTime,slowGait);
            }
            Moving=walking;
            if(charge!=null&&charge.IsCharging)
            {
                _release=-1;Facing=HeroMeshyArt.Facing(charge.VisualAimDir);float p=Mathf.Clamp01(charge.CurrentRead.Progress(charge.HeldSeconds));
                if(walking)Show("moving_draw",HeroMeshyArt.DrawGrid(HeroMeshyArt.RelativeDirection(Facing,delta),p,_phase));
                else Show(p>=1?"full_hold":"draw",p>=1?Mathf.FloorToInt(_clock*20)%12:Mathf.Clamp(Mathf.RoundToInt(p*14),0,14));
                _clock+=Time.deltaTime;return;
            }
            if(_release>=0)
            {
                if(_release<.20f)
                {
                    if(walking)Show("moving_release",HeroMeshyArt.ReleaseGrid(HeroMeshyArt.RelativeDirection(Facing,delta),_releaseProgress,_release,_phase));
                    else Show(_releaseProgress<.65f?"release_weak":"release",Mathf.Clamp(Mathf.FloorToInt(_release*20+.0001f),0,4));
                    _release+=Time.deltaTime;return;
                }
                _release=-1;
            }
            if(walking)Facing=HeroMeshyArt.Facing(delta);
            if(_hurtAge<.25f){Show("hurt",Mathf.Min(5,Mathf.FloorToInt(_hurtAge*20)));return;}
            if(walking)Show("run",Mathf.Min(9,Mathf.FloorToInt(_phase*10)));
            else{_clock+=Time.deltaTime;Show("idle",Mathf.FloorToInt(_clock*20)%20);}
        }
        public void Show(string clip,int frame){EnsureRenderer();_body.sprite=HeroMeshyArt.Frame(clip,Facing,frame);_body.flipX=false;if(_owner!=null){_owner.sprite=_body.sprite;_owner.flipX=false;}CurrentClip=clip;CurrentFrame=frame;HeroMeshyArt.Apply(_body);}
        void EnsureRenderer(){if(_body==null)_body=GetComponent<SpriteRenderer>();}
    }
}
