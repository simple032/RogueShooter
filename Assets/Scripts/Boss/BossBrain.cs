using System;
using System.Collections.Generic;
namespace RogueShooter.Boss
{
    public enum BossPhase{IdleOutside,Entering,DoorSealed,P1,P2,Defeated}
    // Preserve old values. RingBurst retired with no selection or execution path;
    // StraightShot retired from selection — P1 ranged is always the 3-shot burst.
    public enum BossMoveId{None=0,StraightShot=1,WarningCharge=2,TripleShot=3,RingBurst=4,BurstShot=5,ShieldBash=6,LeapSlam=7,CrossbowBash=8,TripleBurst=9}
    public enum BossMoveStep{Idle,Windup,Active,Recovery}
    public sealed class BossBrain
    {
        public const float DefaultMaxHp=FinalBossRules.BaseHp,Phase2HpFrac=.5f;
        public float MaxHp{get;private set;} public float Hp{get;private set;}
        public bool HpLocked{get;private set;} public string EnterAnchorId{get;private set;}
        public int EnterBuild{get;private set;} public int EnterTimeTier{get;private set;}
        public float EnterDmgMul{get;private set;} public float LiveDmgMul{get;private set;}
        public BossPhase Phase{get;private set;} public bool DoorClosed{get;private set;}
        public BossMoveId CurrentMove{get;private set;} public BossMoveStep MoveStep{get;private set;}
        public int MoveIndex{get;private set;} public int MovesCompleted{get;private set;}
        public int Phase2Transitions{get;private set;} public bool UsesMobAi=>false;
        public float ActionElapsed{get;private set;} public float CombatSeconds{get;private set;}
        public float QuietLeft=>Math.Max(0,_quietUntil-CombatSeconds);
        public int P1RangedUses{get;private set;}
        public int P2RangedUses{get;private set;}
        public BossMoveId NextP2Ranged=>P2RangedUses%3==2?BossMoveId.TripleBurst:BossMoveId.TripleShot;
        public BossMoveId NextP1Ranged=>BossMoveId.BurstShot;
        public float StepElapsed=>MoveStep==BossMoveStep.Active?ActionElapsed-_windup:MoveStep==BossMoveStep.Recovery?ActionElapsed-_windup-_active:ActionElapsed;
        public bool ReadyToSelect=>MoveStep==BossMoveStep.Idle&&QuietLeft<=.00001f&&(Phase==BossPhase.P1||Phase==BossPhase.P2);
        float _windup,_active,_recovery,_quietUntil,_weakUntil;
        int _weakMoveIndex=-1,_rangedConsumedIndex=-1,_sameCount;BossMoveId _lastMove;
        readonly Dictionary<BossMoveId,float> _cooldowns=new Dictionary<BossMoveId,float>();
        public void Configure(float max){MaxHp=max>1?max:DefaultMaxHp;Reset();}
        public void Reset()
        {
            if(MaxHp<1)MaxHp=DefaultMaxHp;Hp=MaxHp;HpLocked=false;EnterAnchorId="";EnterBuild=EnterTimeTier=0;
            EnterDmgMul=LiveDmgMul=1;Phase=BossPhase.IdleOutside;DoorClosed=false;ClearMove();
            MoveIndex=MovesCompleted=Phase2Transitions=P1RangedUses=P2RangedUses=0;CombatSeconds=0;_quietUntil=_weakUntil=0;
            _weakMoveIndex=_rangedConsumedIndex=-1;_sameCount=0;_lastMove=BossMoveId.None;_cooldowns.Clear();
        }
        public void LockHpOnEnter(BossScaleSnapshot s)
        {if(HpLocked)return;MaxHp=s.MaxHp>1?s.MaxHp:DefaultMaxHp;Hp=MaxHp;HpLocked=true;EnterAnchorId=s.AnchorId??"";EnterBuild=s.BuildCount;EnterTimeTier=s.TimeTier;EnterDmgMul=s.DmgMul>0?s.DmgMul:1;LiveDmgMul=EnterDmgMul;}
        public void NotifyTimeCross(float minutes){if(HpLocked)LiveDmgMul=BossScaleTable.DmgMulFor(EnterBuild,minutes);}
        public void NotifyEnter(){if(Phase==BossPhase.IdleOutside)Phase=BossPhase.Entering;}
        public void ApplyDamage(float amount)
        {
            if(Phase==BossPhase.IdleOutside||Phase==BossPhase.Defeated||amount<=0)return;Hp=Math.Max(0,Hp-amount);
            if(Hp<=0){Phase=BossPhase.Defeated;ClearMove();}
            else if(Phase==BossPhase.P1&&Hp<=MaxHp*.5f)EnterPhase2();
        }
        void EnterPhase2(){Phase=BossPhase.P2;Phase2Transitions++;ClearMove();_quietUntil=CombatSeconds;_lastMove=BossMoveId.None;_sameCount=0;}
        void ClearMove(){CurrentMove=BossMoveId.None;MoveStep=BossMoveStep.Idle;ActionElapsed=0;}
        static BossMoveId Key(BossMoveId m)=>FinalBossRules.IsP1Ranged(m)?BossMoveId.StraightShot:FinalBossRules.IsP2Ranged(m)?BossMoveId.TripleShot:m;
        public float CooldownLeft(BossMoveId m){float until;return _cooldowns.TryGetValue(Key(m),out until)?Math.Max(0,until-CombatSeconds):0;}
        public bool CanUse(BossMoveId m)
        {
            bool valid=Phase==BossPhase.P1?(FinalBossRules.IsP1Ranged(m)||m==BossMoveId.WarningCharge||m==BossMoveId.ShieldBash)
                :Phase==BossPhase.P2&&(FinalBossRules.IsP2Ranged(m)||m==BossMoveId.LeapSlam||m==BossMoveId.CrossbowBash);
            float until;bool cooled=!_cooldowns.TryGetValue(Key(m),out until)||CombatSeconds+.00001f>=until;
            bool same=Key(m)==Key(_lastMove);return valid&&cooled&&(!same||(!FinalBossRules.IsHeavy(m)&&_sameCount<2));
        }
        public bool TryStart(BossMoveId m)
        {
            if(!ReadyToSelect||!CanUse(m))return false;
            if(FinalBossRules.IsP1Ranged(m)&&m!=NextP1Ranged)return false;
            if(FinalBossRules.IsP2Ranged(m)&&m!=NextP2Ranged)return false;
            var s=FinalBossRules.Skill(m);_windup=s.Windup;_active=s.Active;_recovery=s.Recovery;
            _sameCount=Key(m)==Key(_lastMove)?_sameCount+1:1;_lastMove=m;
            CurrentMove=m;MoveIndex++;ActionElapsed=0;MoveStep=BossMoveStep.Windup;_cooldowns[Key(m)]=CombatSeconds+s.Cooldown;return true;
        }
        public void NotifyRangedFired()
        {if(_rangedConsumedIndex==MoveIndex)return;
            if(Phase==BossPhase.P1&&FinalBossRules.IsP1Ranged(CurrentMove)){_rangedConsumedIndex=MoveIndex;P1RangedUses++;}
            else if(Phase==BossPhase.P2&&FinalBossRules.IsP2Ranged(CurrentMove)){_rangedConsumedIndex=MoveIndex;P2RangedUses++;}}
        public bool WeakAvailable(float now)=>now>=_weakUntil&&_weakMoveIndex!=MoveIndex;
        public bool TryWeakSpot(float now)
        {
            if(!WeakAvailable(now)||MoveStep!=BossMoveStep.Recovery||(Phase!=BossPhase.P1&&Phase!=BossPhase.P2))return false;
            _recovery+=FinalBossRules.RecoveryExtension;_weakUntil=now+FinalBossRules.WeakCooldown;_weakMoveIndex=MoveIndex;return true;
        }
        public void InterruptCurrentMove(){TryWeakSpot(CombatSeconds);}
        public void EndChargeEarly()
        {if(CurrentMove==BossMoveId.WarningCharge&&MoveStep==BossMoveStep.Active){_active=Math.Max(0,ActionElapsed-_windup);MoveStep=BossMoveStep.Recovery;}}
        public BossPhase Tick(float dt)=>Tick(dt,true,true);
        public BossPhase Tick(float dt,bool mayStart,bool projectilesClear)
        {
            dt=Math.Max(0,dt);
            if(Phase==BossPhase.Entering){DoorClosed=true;Phase=BossPhase.DoorSealed;return Phase;}
            if(Phase==BossPhase.DoorSealed){Phase=BossPhase.P1;if(Hp<=MaxHp*.5f)EnterPhase2();}
            if(Phase!=BossPhase.P1&&Phase!=BossPhase.P2)return Phase;CombatSeconds+=dt;
            if(MoveStep!=BossMoveStep.Idle)
            {
                ActionElapsed+=dt;
                if(ActionElapsed>=_windup+_active+_recovery){MovesCompleted++;ClearMove();_quietUntil=float.PositiveInfinity;}
                else MoveStep=ActionElapsed>=_windup+_active?BossMoveStep.Recovery:ActionElapsed>=_windup?BossMoveStep.Active:BossMoveStep.Windup;
            }
            if(MoveStep==BossMoveStep.Idle)
            {
                if(projectilesClear&&float.IsPositiveInfinity(_quietUntil))_quietUntil=CombatSeconds+FinalBossRules.Quiet(Phase);
                if(mayStart&&projectilesClear&&ReadyToSelect)TryStart(Phase==BossPhase.P1?NextP1Ranged:NextP2Ranged);
            }
            return Phase;
        }
        public string Snapshot()=>"phase="+Phase+" hp="+Hp+"/"+MaxHp+" move="+CurrentMove+"/"+MoveStep+" elapsed="+ActionElapsed+" quiet="+QuietLeft+" ranged="+P1RangedUses;
    }
}
