using UnityEngine;
using System.Collections.Generic;
using RogueShooter.Balance;
using RogueShooter.Player;
using RogueShooter.Spawning;
using RogueShooter.Maze;
using RogueShooter.Demo;
namespace RogueShooter.Boss
{
    [DefaultExecutionOrder(200)]
    public sealed class BossFightDriver : MonoBehaviour
    {
        public BossBrain Brain {get;private set;}
        public BossSettlePanel Settle {get;private set;}
        public bool FightStarted {get;private set;}
        public bool FightWon {get;private set;}
        public bool FightSettled {get;private set;}
        public BossScaleSnapshot LastScale {get;private set;}
        public BossSettleReport LastSettle {get;private set;}
        public static BossFightDriver Live {get;private set;}
        public Stage1Maze Maze {get;private set;}
        public MazeNode Arena {get;private set;}
        public Transform PlayerBody {get;private set;}
        public FinalBossCombat Combat {get;private set;}
        public int FightSeed=>_demo!=null?_demo.Seed:42;
        public int ProjectileCount=>_shots.Count;
        public int VolleyHitCount {get;private set;}
        public bool Transitioning=>_transitionLeft>0;
        public float TransitionElapsed=>FinalBossRules.Transition-_transitionLeft;
        public bool IsStaggered=>false;
        public bool AcceptsDamage=>FightStarted&&!FightSettled&&!Transitioning&&!RunPause.IsPaused&&(Combat==null||!Combat.Airborne);
        [SerializeField] float stubMaxHp=BossBrain.DefaultMaxHp;
        [SerializeField] Transform doorVisual;
        readonly List<FinalBossProjectile> _shots=new List<FinalBossProjectile>();
        Stage1MazeDemo _demo; float _lastWallMinutes,_transitionLeft,_oldScale=1,_introLeft; bool _ownsTransition;
        readonly HashSet<long> _attackHits=new HashSet<long>();
        int _phaseChanges; float _knockLeft; Vector3 _knockDir; float _knockSpeed;
        void Awake(){Brain=new BossBrain();Brain.Configure(stubMaxHp);Settle=GetComponent<BossSettlePanel>();if(Settle==null)Settle=gameObject.AddComponent<BossSettlePanel>();}
        void OnEnable(){Live=this;}
        void OnDisable(){ClearProjectiles();if(Combat!=null)Combat.ClearWarnings();EndTransition();if(Live==this)Live=null;}
        public void BindArena(Stage1MazeDemo demo,Stage1Maze maze,MazeNode arena,Transform player)
        { _demo=demo;Maze=maze;Arena=arena;PlayerBody=player;Settle.enabled=false;Combat=gameObject.AddComponent<FinalBossCombat>();Combat.Bind(this); }
        public bool InArena(Vector3 p,float inset)=>Arena==null||Arena.Contains(p.x,p.y,inset);
        public void BindDoor(Transform door){doorVisual=door;if(doorVisual!=null)doorVisual.gameObject.SetActive(Brain.DoorClosed);}
        public void BeginEnter(int buildCount,float wallMinutes)
        {
            if(FightStarted)return;float attr=TimePressure.AttrMul(wallMinutes);
            LastScale=BossScaleTable.Resolve(buildCount,wallMinutes,attr,SpawnWaveCatalog.BuildMul(buildCount));Brain.LockHpOnEnter(LastScale);
            FightStarted=true;RogueShooter.Audio.JianHaiAudio.Emit("jh_boss_enter",this);_lastWallMinutes=wallMinutes;_introLeft=FinalBossRules.Intro;Brain.NotifyEnter();Brain.Tick(0,false,true);Brain.Tick(0,false,true);
            if(doorVisual!=null)doorVisual.gameObject.SetActive(true);
            Debug.Log("[FinalBoss] ENTER B="+buildCount+" t="+wallMinutes.ToString("F3")+" diff="+attr.ToString("F3")+" hp="+Brain.MaxHp.ToString("F1"));
        }
        public void BeginEnter(){BeginEnter(0,0);}
        public void SetWallMinutes(float minutes){ObserveWallMinutes(minutes);}
        public void ObserveWallMinutes(float minutes){_lastWallMinutes=minutes;if(FightStarted&&!FightSettled)Brain.NotifyTimeCross(minutes);}
        public float DealDamage(float amount,Vector3 incoming=default(Vector3),bool weak=false)
        {
            if(!AcceptsDamage)return 0;
            float before=Brain.Hp;bool phase2=Brain.Phase==BossPhase.P2;
            float mul=FinalBossRules.IncomingMultiplier(Brain.Phase,Combat!=null?Combat.BodyFacing:Vector3.down,incoming,weak);
            Brain.ApplyDamage(amount*mul);
            if(Brain.Phase==BossPhase.Defeated&&Combat!=null)Combat.BeginDeath(phase2);
            if(Brain.Phase2Transitions>_phaseChanges && Brain.Phase!=BossPhase.Defeated){_phaseChanges=Brain.Phase2Transitions;StartTransition();}
            return before-Brain.Hp;
        }
        public void ApplyWeakSpotStagger(float unusedSeconds)
        {if(!AcceptsDamage)return;if(Brain.TryWeakSpot(Brain.CombatSeconds)){RogueShooter.Audio.JianHaiAudio.Emit("jh_boss_interrupt",this);Debug.Log("[FinalBoss] recovery extended move="+Brain.CurrentMove);}}
        public void ApplyKnockback(Vector3 away,float distance)
        {if(!AcceptsDamage || (FinalBossRules.IsHeavy(Brain.CurrentMove)&&Brain.MoveStep==BossMoveStep.Active)||distance<=0)return;
            away.z=0;if(away.sqrMagnitude<.001f)return;_knockDir=away.normalized;_knockLeft=FullChargeKnockback.SlideSeconds(distance,true);_knockSpeed=distance/Mathf.Max(.001f,_knockLeft);}
        public void NotifyPlayerDead(){if(FightStarted&&!FightSettled)Finish(BossSettleOutcome.Lose);}
        public float OutgoingDamage(float baseDamage)=>Brain==null?baseDamage:baseDamage*Brain.LiveDmgMul;
        public void RegisterProjectile(FinalBossProjectile shot){_shots.Add(shot);}
        public void ProjectileEnded(FinalBossProjectile shot){_shots.Remove(shot);}
        public void ClearProjectiles(){foreach(var s in _shots.ToArray())if(s!=null)s.Clear();_shots.Clear();}
        public bool TryAttackHit(BossMoveId move,int group,int moveIndex=-1)
        {
            if(RunPause.IsPaused||FightSettled||Transitioning||PlayerBody==null)return false;
            int index=moveIndex<0?Brain.MoveIndex:moveIndex;if(index!=Brain.MoveIndex)return false;
            var v=PlayerBody.GetComponent<PlayerVitals>();if(v==null||v.IsDown)return false;
            long key=((long)index<<32)+(uint)group;if(!_attackHits.Add(key))return false;
            VolleyHitCount++;v.ApplyHit(OutgoingDamage(FinalBossRules.Damage(move)),"BOSS_"+move);return true;
        }
        void StartTransition(){RogueShooter.Audio.JianHaiAudio.StopThreat(Combat);RogueShooter.Audio.JianHaiAudio.Emit("jh_boss_phase2",this);ClearProjectiles();if(Combat!=null)Combat.ClearWarnings();_knockLeft=0;_oldScale=Time.timeScale;_transitionLeft=FinalBossRules.Transition;_ownsTransition=true;RunPause.BossTransition=true;Time.timeScale=0;
            if(Combat!=null)Combat.ResetPresentation();
            if(_demo!=null)_demo.ClearCombatProjectiles();Debug.Log("[FinalBoss] P2 transition hp="+Brain.Hp+" no heal");}
        void EndTransition(){if(!_ownsTransition)return;_ownsTransition=false;_transitionLeft=0;RunPause.BossTransition=false;if(!RunPause.RunSettled&&!RunPause.InteractOpen)Time.timeScale=_oldScale;}
        void Update()
        {
            if(!FightStarted||FightSettled)return;
            if(Transitioning){_transitionLeft=Mathf.Max(0,_transitionLeft-Time.unscaledDeltaTime);if(Combat!=null)Combat.ShowBody();if(_transitionLeft<=0)EndTransition();return;}
            if(RunPause.IsPaused)return;
            if(_demo!=null)ObserveWallMinutes(_demo.RunSeconds/60f);
            if(_introLeft>0){_introLeft-=Time.deltaTime;if(Combat!=null)Combat.ShowBody();return;}
            float left=Time.deltaTime;
            // Subdivide gameplay time so low frame rate cannot skip lock/fire/melee windows or overrun a dash.
            while(left>.000001f&&!RunPause.IsPaused&&!FightSettled)
            {
                float dt=Mathf.Min(.05f,left);left-=dt;
                bool knocking=_knockLeft>0;
                if(knocking){float slice=Mathf.Min(_knockLeft,dt);var to=transform.position+_knockDir*_knockSpeed*slice;float wall=MazeCollision.FirstBlockT(Maze,null,transform.position,to,FinalBossRules.HitRadius);if(wall<=1)to=Vector3.Lerp(transform.position,to,Mathf.Max(0,wall-.005f));if(InArena(to,1))transform.position=to;_knockLeft-=slice;}
                Brain.Tick(dt,false,ProjectileCount==0);
                if(FinalBossRules.IsHeavy(Brain.CurrentMove)&&Brain.MoveStep==BossMoveStep.Active)_knockLeft=0;
                if(Brain.Phase2Transitions>_phaseChanges&&Brain.Phase!=BossPhase.Defeated){_phaseChanges=Brain.Phase2Transitions;StartTransition();return;}
                if(Combat!=null){if(!knocking)Combat.SelectOrMove(dt);Combat.Tick(dt);}
            }
        }
        void LateUpdate(){if(!FightStarted||FightSettled)return;var vitals=PlayerBody!=null?PlayerBody.GetComponent<PlayerVitals>():null;
            if(vitals!=null&&vitals.IsDown)Finish(BossSettleOutcome.Lose);else if(Brain.Phase==BossPhase.Defeated)Finish(BossSettleOutcome.Win);}
        void Finish(BossSettleOutcome outcome)
        {if(FightSettled)return;FightSettled=true;FightWon=outcome==BossSettleOutcome.Win;if(FightWon)RogueShooter.Audio.JianHaiAudio.Emit("jh_boss_death",this);ClearProjectiles();if(Combat!=null)Combat.ClearWarnings();EndTransition();LastSettle=BossSettleReport.From(outcome,Brain,_lastWallMinutes);
            if(_demo!=null)_demo.CompleteBossFight(FightWon);else {RogueShooter.Audio.JianHaiAudio.Settle(FightWon?"jh_run_victory":"jh_run_defeat");RunPause.EnterSettled();Settle.Show(LastSettle);}Debug.Log("[FinalBoss] SETTLE "+outcome);}
        // Boss HP is drawn once, by Stage1PlayHud (top-center slot). No second top bar here (PR #34).
    }
}
