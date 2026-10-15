using UnityEngine;
using System.Collections.Generic;
using RogueShooter.Demo;
using RogueShooter.Maze;
using RogueShooter.Player;
namespace RogueShooter.Boss
{
    /// <summary>Spatial execution. Gameplay clock owns all events; v04 art follows that clock.</summary>
    public sealed class FinalBossCombat : MonoBehaviour
    {
        BossFightDriver _fight; SpriteRenderer _body; Transform _visual;
        public BossWardenPresentation Presentation {get;private set;}
        readonly List<LineRenderer> _warnings=new List<LineRenderer>();
        int _warningUsed,_moveIndex=-1,_shotCount; bool _locked,_began,_landed;
        int _approachChoiceIndex=-1;bool _approachChoice;
        SpriteRenderer _alert;static Sprite _alertSprite;
        public bool AlertVisible=>_alert!=null&&_alert.enabled;
        public int VolleyCount=>_shotCount;
        Vector3 _aim=Vector3.down,_face=Vector3.down,_start,_dashEnd,_target,_last;
        float _walkClock,_previousElapsed;
        System.Random _rng;
        public int WarningCount {get {int n=0;foreach(var w in _warnings)if(w!=null&&w.enabled)n++;return n;}}
        public int AttacksExecuted{get;private set;}
        public int ArrowsFired{get;private set;}
        public Vector3 LockedAim=>_aim;
        public Vector3 BodyFacing=>_face;
        public SpriteRenderer BodyRenderer=>_body;
        public Vector3 DashEnd=>_dashEnd;
        public Vector3 LeapTarget=>_target;
        public bool Airborne=>_fight!=null&&_fight.Brain.CurrentMove==BossMoveId.LeapSlam&&_fight.Brain.ActionElapsed>=.7f&&_fight.Brain.ActionElapsed<1.55f;
        public void Bind(BossFightDriver fight)
        {
            _fight=fight;_last=transform.position;_rng=new System.Random(fight.FightSeed);
            transform.localScale=Vector3.one*FinalBossRules.BodyScale;
            _visual=new GameObject("BOSS v04 refined right-hand animation").transform;_visual.SetParent(transform,false);
            // Source PPU already encodes the final world size. Preserve the gameplay root scale.
            _visual.localScale=Vector3.one/FinalBossRules.BodyScale;
            _body=_visual.gameObject.AddComponent<SpriteRenderer>();_body.sortingLayerName="Entity";_body.spriteSortPoint=SpriteSortPoint.Pivot;
            Presentation=_visual.gameObject.AddComponent<BossWardenPresentation>();Presentation.Bind(_body);
            var group=gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();group.sortingLayerName="Entity";
            var alert=new GameObject("Boss windup !");alert.transform.SetParent(transform,false);
            _alert=alert.AddComponent<SpriteRenderer>();_alert.sprite=AlertSprite();_alert.sortingLayerName="FX";_alert.sortingOrder=50;_alert.enabled=false;
            ShowBody();
        }
        bool Visible(Vector3 p,float pad=.25f)
        {
            var cam=Camera.main;if(cam==null)return false;
            var min=cam.WorldToViewportPoint(p-new Vector3(pad,pad,0));var max=cam.WorldToViewportPoint(p+new Vector3(pad,pad,0));
            return min.z>0&&min.x>.03f&&max.x<.97f&&min.y>.04f&&max.y<.94f;
        }
        public bool FullyVisibleAt(Vector3 foot)
        {return Visible(foot+Vector3.up*1.7f,1.8f);}
        bool ClearLine(Vector3 a,Vector3 b,float radius=0)=>MazeCollision.FirstBlockT(_fight.Maze,null,a,b,radius)>1f;
        Vector3 PlayerPoint=>_fight.PlayerBody!=null?_fight.PlayerBody.position:transform.position+Vector3.down;
        Vector3 DirectionToPlayer(){var d=PlayerPoint-transform.position;d.z=0;return d.sqrMagnitude>.001f?d.normalized:_face;}
        static Vector3 Dir(float degrees){float a=degrees*Mathf.Deg2Rad;return new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);}
        static string FacingFrom(Vector3 d){string[] dirs={"e","ne","n","nw","w","sw","s","se"};int n=Mathf.RoundToInt(Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg/45f);return dirs[(n%8+8)%8];}
        void Turn(Vector3 wanted,float dt)
        {if(wanted.sqrMagnitude<.001f)return;float from=Mathf.Atan2(_face.y,_face.x)*Mathf.Rad2Deg,to=Mathf.Atan2(wanted.y,wanted.x)*Mathf.Rad2Deg;_face=Dir(Mathf.MoveTowardsAngle(from,to,FinalBossRules.TurnSpeed(_fight.Brain.Phase)*dt));_aim=_face;}
        public static Vector3 PredictPoint(Vector3 player,Vector3 boss,Vector2 groundVelocity)
        {
            float lead=Mathf.Min(.75f,Vector3.Distance(player,boss)/8f+.35f);
            return player+(Vector3)Vector2.ClampMagnitude(groundVelocity*lead*.65f,3f);
        }
        Vector3 PredictedDirection()
        {var motor=_fight.PlayerBody!=null?_fight.PlayerBody.GetComponent<PlayerMotor2D>():null;var p=PredictPoint(PlayerPoint,transform.position,motor!=null?motor.RecentWalkVelocity:Vector2.zero);var d=p-transform.position;return d.sqrMagnitude>.001f?d.normalized:_face;}
        bool Legal(BossMoveId move)
        {
            var b=_fight.Brain;var s=FinalBossRules.Skill(move);float d=Vector3.Distance(transform.position,PlayerPoint);
            if(!b.CanUse(move)||d<s.Min||d>s.Max||!FullyVisibleAt(transform.position)||!ClearLine(transform.position,PlayerPoint))return false;
            if(FinalBossRules.IsMelee(move))return Visible(transform.position,s.Radius+.4f);
            if(move==BossMoveId.LeapSlam){Vector3 target;return FindLeapTarget(PlayerPoint,out target);}
            if(move==BossMoveId.WarningCharge)return Vector3.Distance(transform.position,ClippedChargeEnd(transform.position,DirectionToPlayer()))>=2f;
            return Vector3.Angle(_face,DirectionToPlayer())<=45f;
        }
        public void SelectOrMove(float dt)
        {
            var b=_fight.Brain;
            var melee=b.Phase==BossPhase.P1?BossMoveId.ShieldBash:BossMoveId.CrossbowBash;
            if(b.ReadyToSelect&&_fight.ProjectileCount==0)
            {
                var ranged=b.Phase==BossPhase.P1?b.NextP1Ranged:b.NextP2Ranged;
                var heavy=b.Phase==BossPhase.P1?BossMoveId.WarningCharge:BossMoveId.LeapSlam;
                BossMoveId pick=BossMoveId.None;
                if(Legal(melee))pick=melee;
                else
                {
                    // Roll once per decision, not once per frame: pursuing must be allowed to finish.
                    if(_approachChoiceIndex!=b.MoveIndex){_approachChoiceIndex=b.MoveIndex;_approachChoice=_rng.Next(100)<75;}
                    bool pursue=_approachChoice&&b.CanUse(melee)&&Vector3.Distance(PlayerPoint,transform.position)<=6.5f;
                    if(!pursue)
                    {
                        bool r=Legal(ranged),h=Legal(heavy);
                        if(r&&h)pick=_rng.Next(100)<40?ranged:heavy;
                        else if(r)pick=ranged;else if(h)pick=heavy;
                    }
                }
                if(pick!=BossMoveId.None&&b.TryStart(pick))
                {Debug.Log("[FinalBoss] seed="+_fight.FightSeed+" pick="+pick+" index="+b.MoveIndex+" time="+b.CombatSeconds);return;}
            }
            if(b.MoveStep!=BossMoveStep.Idle)return;
            // Default is close pursuit. A small step out is only needed to rotate after two melee uses.
            float desired=2.15f;
            if(!b.CanUse(melee)&&b.CooldownLeft(melee)<=.0001f)desired=3.8f;
            Vector3 away=transform.position-PlayerPoint;if(away.sqrMagnitude<.001f)away=-_face;
            Vector3 goal=PlayerPoint+away.normalized*desired;float best=float.PositiveInfinity;
            if(!_fight.InArena(goal,1)||!FullyVisibleAt(goal)||!ClearLine(goal,goal,.7f))
            {
                goal=transform.position;
                for(int i=0;i<32;i++)
                {var p=PlayerPoint+Dir(i*11.25f)*desired;if(!_fight.InArena(p,1)||!FullyVisibleAt(p)||!ClearLine(p,p,.7f))continue;
                    float score=(p-transform.position).sqrMagnitude;if(score<best){best=score;goal=p;}}
            }
            var to=MazeCollision.Slide(_fight.Maze,null,transform.position,Vector3.MoveTowards(transform.position,goal,FinalBossRules.MoveSpeed(b.Phase)*dt),.7f);
            Move(to);Turn(DirectionToPlayer(),dt);
        }
        public void Tick(float dt)
        {
            var b=_fight.Brain;var move=b.CurrentMove;
            if(move==BossMoveId.None){HideWarnings();_visual.localPosition=Vector3.zero;ShowBody();return;}
            if(_moveIndex!=b.MoveIndex)
            {
                ClearWarnings();_moveIndex=b.MoveIndex;_shotCount=0;_locked=_began=_landed=false;_previousElapsed=0;
                _start=transform.position;_target=PlayerPoint;_dashEnd=transform.position;
                RogueShooter.Audio.JianHaiAudio.Emit(AudioPrefix(move)+"_warn",this);
            }
            float t=b.ActionElapsed;var s=FinalBossRules.Skill(move);
            if(b.MoveStep==BossMoveStep.Windup)_start=transform.position;
            bool burst=move==BossMoveId.BurstShot||move==BossMoveId.TripleBurst;
            float nextFire=s.Windup+(burst?_shotCount*FinalBossRules.VolleyInterval:0);
            float nextLock=s.Lock+(burst?_shotCount*FinalBossRules.VolleyInterval:0);
            bool pending=burst?_shotCount<3:_shotCount==0;
            if(!_locked&&(t<s.Windup||(burst&&pending)||(move==BossMoveId.LeapSlam&&!_began)))
            {
                var wanted=FinalBossRules.IsRanged(move)?PredictedDirection():DirectionToPlayer();
                Turn(wanted,dt);
                if(FinalBossRules.IsRanged(move))_aim=wanted;
                if(move==BossMoveId.LeapSlam){Vector3 target;if(FindLeapTarget(PlayerPoint,out target))_target=target;}
                if(move==BossMoveId.WarningCharge)_dashEnd=ClippedChargeEnd(_start,_aim);
                if(t+.00001f>=nextLock){_locked=true;RogueShooter.Audio.JianHaiAudio.Emit(AudioPrefix(move)+"_lock",this);}
            }
            if(t+.00001f>=s.Windup&&!_began)
            {if(move==BossMoveId.LeapSlam)_start=transform.position;_began=true;AttacksExecuted++;RogueShooter.Audio.JianHaiAudio.StopThreat(this);RogueShooter.Audio.JianHaiAudio.Emit(FinalBossRules.IsMelee(move)?"jh_skel_swing":AudioPrefix(move)+(FinalBossRules.IsHeavy(move)?"_active":"_fire"),this);}
            if(move==BossMoveId.WarningCharge&&b.MoveStep==BossMoveStep.Windup)_dashEnd=ClippedChargeEnd(_start,_aim);
            if(FinalBossRules.IsRanged(move)&&pending&&t+.00001f>=nextFire)
            {
                b.NotifyRangedFired();int group=burst?_shotCount:0;
                if(FinalBossRules.IsP2Ranged(move)){float angle=Mathf.Atan2(_aim.y,_aim.x)*Mathf.Rad2Deg+(move==BossMoveId.TripleBurst?FinalBossRules.TripleSweep(_shotCount):0);for(int side=-1;side<=1;side++)Spawn(Dir(angle+side*FinalBossRules.TripleSpread),move,group);}
                else Spawn(_aim,move,group);
                if(_shotCount>0)RogueShooter.Audio.JianHaiAudio.Emit(AudioPrefix(move)+"_fire",this);
                _shotCount++;if(burst&&_shotCount<3)_locked=false;
            }
            if(move==BossMoveId.WarningCharge&&t>=s.Windup&&_previousElapsed<s.Windup+s.Active&&b.MoveStep!=BossMoveStep.Idle)
            {
                float slice=Mathf.Max(0,Mathf.Min(t,s.Windup+s.Active)-Mathf.Max(_previousElapsed,s.Windup));
                Vector3 from=transform.position,to=Vector3.MoveTowards(from,_dashEnd,s.Speed*slice);
                if(_fight.PlayerBody!=null&&MazeCollision.CircleEntryT(PlayerPoint,from,to,s.Radius+FinalBossRules.PlayerRadius)<=1)_fight.TryAttackHit(move,0);
                Move(to);if((transform.position-_dashEnd).sqrMagnitude<.0001f)b.EndChargeEarly();
            }
            if(FinalBossRules.IsMelee(move)&&t>=s.Windup&&_previousElapsed<s.Windup+s.Active)
            {
                if(FinalBossRules.InSector(transform.position,_aim,PlayerPoint,s.Radius,s.Angle,FinalBossRules.PlayerRadius)&&ClearLine(transform.position,PlayerPoint))
                    _fight.TryAttackHit(move,0);
            }
            if(move==BossMoveId.LeapSlam)
            {
                float air=t-s.Windup;
                if(air>=0&&air<FinalBossRules.LeapAir)
                {
                    Move(Vector3.Lerp(_start,_target,Mathf.Clamp01(air/FinalBossRules.LeapAir)));
                    float h=air<.3f?Mathf.Sin(air/.3f*Mathf.PI*.5f):air<.45f?1f:Mathf.Cos((air-.45f)/.4f*Mathf.PI*.5f);
                    _visual.localPosition=Vector3.up*(Mathf.Max(0,h)*FinalBossRules.LeapHeight/FinalBossRules.BodyScale);
                }
                else if(air>=FinalBossRules.LeapAir)
                {
                    if(!_landed){_landed=true;Move(_target);_visual.localPosition=Vector3.zero;}
                    if(t<1.7f&&Vector3.Distance(PlayerPoint,_target)<=s.Radius+FinalBossRules.PlayerRadius&&ClearLine(_target,PlayerPoint))_fight.TryAttackHit(move,0);
                }
            }
            ShowBody();DrawWarnings(move,t);_previousElapsed=t;
        }
        string AudioPrefix(BossMoveId m)=>FinalBossRules.IsP2Ranged(m)?"jh_boss_triple":FinalBossRules.IsP1Ranged(m)?"jh_boss_bolt":"jh_boss_dash";
        void Move(Vector3 to){if(_fight.InArena(to,.7f))transform.position=to;}
        Vector3 ClippedChargeEnd(Vector3 start,Vector3 direction)
        {
            var end=start+direction*7;float wall=MazeCollision.FirstBlockT(_fight.Maze,null,start,end,.75f);
            if(wall<=1)end=Vector3.Lerp(start,end,Mathf.Max(0,wall-.005f));
            // Stop before telegraph or full body leaves the camera/room.
            for(int i=0;i<40;i++){if(_fight.InArena(end,1)&&Visible(end,1.1f)&&FullyVisibleAt(end))break;end=Vector3.Lerp(end,start,.1f);}
            return end;
        }
        Vector3 TracerEnd(Vector3 d)
        {var end=transform.position+d*7;for(int i=0;i<32&&!Visible(end,.25f);i++)end=Vector3.Lerp(end,transform.position,.1f);float wall=MazeCollision.FirstBlockT(_fight.Maze,null,transform.position,end,.12f);return wall<=1?Vector3.Lerp(transform.position,end,Mathf.Max(0,wall-.005f)):end;}
        public bool FindLeapTarget(Vector3 wanted,out Vector3 target)
        {
            target=wanted;if(_fight.Arena==null)return false;
            var r=_fight.Arena;target.x=Mathf.Clamp(target.x,r.Center.X-r.Width*.5f+2.5f,r.Center.X+r.Width*.5f-2.5f);
            target.y=Mathf.Clamp(target.y,r.Center.Y-r.Height*.5f+2.5f,r.Center.Y+r.Height*.5f-2.5f);
            if(LegalLanding(target))return true;
            Vector3 best=target;float distance=float.PositiveInfinity;
            for(int ring=1;ring<=12;ring++)for(int i=0;i<32;i++)
            {var p=target+Dir(i*11.25f)*ring*.5f;if(!LegalLanding(p))continue;float d=(p-wanted).sqrMagnitude;if(d<distance){distance=d;best=p;}}
            target=best;return !float.IsPositiveInfinity(distance);
        }
        bool LegalLanding(Vector3 p)=>_fight.InArena(p,2.5f)&&ClearLine(p,p,2.5f)&&Visible(p,2.5f)&&FullyVisibleAt(p+Vector3.up*FinalBossRules.LeapHeight);
        void Spawn(Vector3 dir,BossMoveId move,int group)
        {
            var go=new GameObject("Boss projectile "+move);var shot=go.AddComponent<FinalBossProjectile>();_fight.RegisterProjectile(shot);
            string clip=BossWardenArt.MoveClip(move),facing=FacingFrom(_face);
            int frame=BossWardenArt.Find(clip).FrameAt(_fight.Brain.ActionElapsed);
            Vector3 muzzle=Presentation.Muzzle(clip,facing,frame);
            // Art has eight rendered directions; rotate only the residual angle within a sector.
            float angle=Mathf.Atan2(dir.y,dir.x)*Mathf.Rad2Deg;
            go.transform.rotation=Quaternion.Euler(0,0,Mathf.DeltaAngle(Mathf.Round(angle/45f)*45f,angle));
            // Separate visual muzzle from ground collision origin (same travel, distinct height).
            shot.Launch(_fight,transform.position+dir*.9f,dir,move,group);
            var visual=new GameObject("BOSS flying bolt");visual.transform.SetParent(go.transform,false);
            var renderer=visual.AddComponent<SpriteRenderer>();renderer.sprite=BossWardenArt.Bolt(FinalBossRules.IsP2Ranged(move),BossWardenArt.Facing(dir));renderer.sortingLayerName="Entity";renderer.sortingOrder=3;
            visual.transform.position=muzzle;shot.BindVisual(visual.transform);ArrowsFired++;
        }
        static Sprite AlertSprite()
        {
            if(_alertSprite!=null)return _alertSprite;
            var texture=new Texture2D(16,40,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};var pixels=new Color32[16*40];
            for(int y=0;y<40;y++)for(int x=0;x<16;x++)
            {bool outer=x>=3&&x<=12&&(y>=12&&y<=38||y>=1&&y<=10);bool inner=x>=5&&x<=10&&(y>=14&&y<=36||y>=3&&y<=8);pixels[y*16+x]=inner?new Color32(255,239,180,255):outer?new Color32(32,24,18,255):new Color32(0,0,0,0);}
            texture.SetPixels32(pixels);texture.Apply(false,true);_alertSprite=Sprite.Create(texture,new Rect(0,0,16,40),new Vector2(.5f,0),48);return _alertSprite;
        }
        void DrawWarnings(BossMoveId move,float t)
        {
            HideWarnings();_warningUsed=0;var s=FinalBossRules.Skill(move);
            var color=_locked?new Color(1,.35f,.15f,.95f):new Color(1,.78f,.30f,.85f);
            bool repeating=move==BossMoveId.BurstShot||move==BossMoveId.TripleBurst;
            bool windup=t<s.Windup||(repeating&&_shotCount<3);
            if(_alert!=null&&windup&&FinalBossRules.IsRanged(move))
            {_alert.enabled=true;_alert.transform.position=new Vector3(transform.position.x,_body.bounds.max.y+.12f,0);_alert.transform.localScale=Vector3.one*(1f+.08f*Mathf.Sin(t*18))/FinalBossRules.BodyScale;_alert.color=_locked?new Color(1,.45f,.3f):Color.white;}
            if(FinalBossRules.IsMelee(move)&&t<s.Windup+s.Active)
            {
                color=t<s.Windup?new Color(1f,.78f,.30f,.85f):new Color(1f,.30f,.15f,.95f);
                Circle(transform.position,s.Radius,color,s.Angle);
                float angle=Mathf.Atan2(_aim.y,_aim.x)*Mathf.Rad2Deg;
                Warning(transform.position,transform.position+Dir(angle-s.Angle*.5f)*s.Radius,color,.07f);
                Warning(transform.position,transform.position+Dir(angle+s.Angle*.5f)*s.Radius,color,.07f);
                return;
            }
            if(move==BossMoveId.LeapSlam&&t<1.55f){Circle(_target,s.Radius,color,360);return;}
            if(move==BossMoveId.WarningCharge&&t<s.Windup)Warning(_start,_dashEnd,color,s.Radius*2);
        }
        void Circle(Vector3 center,float radius,Color color,float angle)
        {
            float at=Mathf.Atan2(_aim.y,_aim.x)*Mathf.Rad2Deg-angle*.5f;var line=WarningLine(color,.08f,33);
            for(int i=0;i<=32;i++)line.SetPosition(i,center+Dir(at+angle*i/32f)*radius);
        }
        LineRenderer WarningLine(Color color,float width,int count)
        {
            if(_warningUsed==_warnings.Count){var go=new GameObject("Boss telegraph");go.transform.SetParent(transform.parent,false);var l=go.AddComponent<LineRenderer>();l.useWorldSpace=true;l.sortingLayerName="Decal";l.sortingOrder=100;_warnings.Add(l);}
            var result=_warnings[_warningUsed++];result.enabled=true;result.positionCount=count;result.startWidth=result.endWidth=width;result.sharedMaterial=Stage1IsoArt.BandMaterial(color);return result;
        }
        void Warning(Vector3 a,Vector3 b,Color c,float width){var l=WarningLine(c,width,2);l.SetPosition(0,a);l.SetPosition(1,b);}
        void HideWarnings(){if(_alert!=null)_alert.enabled=false;foreach(var l in _warnings)if(l!=null)l.enabled=false;}
        public void ClearWarnings(){if(_alert!=null)_alert.enabled=false;RogueShooter.Audio.JianHaiAudio.StopThreat(this);foreach(var l in _warnings)if(l!=null)Destroy(l.gameObject);_warnings.Clear();_warningUsed=0;}
        public void ResetPresentation(){_moveIndex=-1;_locked=_began=_landed=false;_visual.localPosition=Vector3.zero;ClearWarnings();ShowBody();}
        public void ShowBody()
        {
            if(_body==null||Presentation.Dying)return;var b=_fight.Brain;
            Vector3 delta=transform.position-_last;_last=transform.position;_walkClock+=delta.magnitude/1.44f;
            bool p2=b.Phase==BossPhase.P2;string facing=FacingFrom(_face);
            if(_fight.PresentingTransition){Presentation.Show("p1_to_p2",facing,_fight.TransitionElapsed);return;}
            if(b.CurrentMove!=BossMoveId.None&&b.MoveStep!=BossMoveStep.Idle)
            {
                string clip=BossWardenArt.MoveClip(b.CurrentMove);float seconds=b.ActionElapsed;
                // Re-arm the supplied triple-shot clip before each .5s volley, then play its recovery.
                if(b.CurrentMove==BossMoveId.TripleBurst&&seconds>=.95f)
                    seconds=seconds<1.45f?.45f+(seconds-.95f):seconds<1.95f?.45f+(seconds-1.45f):seconds-1f;
                // Clipped charge skips remaining dash poses and enters recovery immediately.
                if(b.CurrentMove==BossMoveId.WarningCharge&&b.MoveStep==BossMoveStep.Recovery)seconds=1.85f+b.StepElapsed;
                Presentation.Show(clip,facing,seconds);return;
            }
            bool moving=delta.sqrMagnitude>.000001f;string idle=(p2?"p2_":"p1_")+(moving?"walk":"idle");
            float clock=moving?_walkClock*BossWardenArt.Find(idle).ticks.Length/20f:Time.time;
            Presentation.Show(idle,facing,clock,true);
        }
        public void BeginDeath(bool phase2){ClearWarnings();_visual.localPosition=Vector3.zero;Presentation.BeginDeath(phase2,FacingFrom(_face));}
        public static LineRenderer Stroke(Transform parent,Vector3[] pts,Color color,float width,int order,bool world)
        {var line=new GameObject("Stroke").AddComponent<LineRenderer>();line.transform.SetParent(parent,false);line.useWorldSpace=world;line.positionCount=pts.Length;line.SetPositions(pts);line.startWidth=line.endWidth=width;line.sharedMaterial=Stage1IsoArt.BandMaterial(color);line.sortingOrder=order;return line;}
        void OnDisable(){ClearWarnings();}
    }
}
