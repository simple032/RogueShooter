using UnityEngine;
namespace RogueShooter.Boss
{
    public static class BossFightChecks
    {
        static BossBrain Enter(){var b=new BossBrain();b.Configure(1100);b.NotifyEnter();b.Tick(0,false,true);b.Tick(0,false,true);return b;}
        static void Advance(BossBrain b,float seconds,bool clear=true){while(seconds>0){float dt=Mathf.Min(.05f,seconds);b.Tick(dt,false,clear);seconds-=dt;}}
        public static string Run()
        {
            var b=Enter();
            if(!b.DoorClosed||b.Phase!=BossPhase.P1||b.UsesMobAi)return "entry/independent brain";
            if(FinalBossRules.MoveSpeed(BossPhase.P1)!=2.5f||FinalBossRules.MoveSpeed(BossPhase.P2)!=4.5f||FinalBossRules.MoveSpeed(BossPhase.P2)>=6)return "locked movement";
            if(FinalBossRules.IncomingMultiplier(BossPhase.P1,Vector3.right,Vector3.left,false)!=.5f)return "front body";
            if(FinalBossRules.IncomingMultiplier(BossPhase.P1,Vector3.right,Vector3.down,false)!=.5f)return "front boundary";
            if(FinalBossRules.IncomingMultiplier(BossPhase.P1,Vector3.right,Vector3.right,false)!=1)return "back body";
            if(FinalBossRules.IncomingMultiplier(BossPhase.P1,Vector3.right,Vector3.left,true)!=1)return "weak bypass";
            if(FinalBossRules.IncomingMultiplier(BossPhase.P2,Vector3.right,Vector3.left,false)!=1)return "P2 no shield";
            if(FinalBossRules.InSector(Vector3.zero,Vector3.right,new Vector3(4,0),2.6f,120,.32f))return "finite melee";
            if(FinalBossRules.InSector(Vector3.zero,Vector3.right,new Vector3(-2,0),2.6f,120,.32f))return "sector rear";
            if(!FinalBossRules.InSector(Vector3.zero,Vector3.right,new Vector3(2.85f,0),2.6f,120,.32f))return "arc circle contact";
            if(b.NextP1Ranged!=BossMoveId.BurstShot||b.TryStart(BossMoveId.StraightShot))return "P1 ranged always burst/single retired";
            if(!b.TryStart(BossMoveId.BurstShot)||b.TryWeakSpot(0))return "first burst/windup cannot interrupt";
            b.NotifyRangedFired();b.NotifyRangedFired();
            if(b.P1RangedUses!=1)return "one ranged use per first fire";
            Advance(b,4,false);if(b.ReadyToSelect)return "quiet waits own projectiles";
            Advance(b,.05f);if(b.QuietLeft<1)return "quiet starts only after projectiles clear";
            Advance(b,1.2f);if(!b.TryStart(BossMoveId.BurstShot))return "second burst";
            b.NotifyRangedFired();Advance(b,4);Advance(b,1.2f);
            if(b.TryStart(BossMoveId.BurstShot))return "third consecutive burst blocked by rotation";
            if(!b.TryStart(BossMoveId.WarningCharge))return "heavy rotation after two bursts";
            Advance(b,5);Advance(b,1.2f);if(b.CanUse(BossMoveId.WarningCharge))return "no consecutive charge";
            if(!b.TryStart(BossMoveId.BurstShot))return "burst resumes after rotation";
            Advance(b,1.1f);b.NotifyRangedFired();Advance(b,1.1f);
            if(b.MoveStep!=BossMoveStep.Recovery||!b.TryWeakSpot(b.CombatSeconds)||b.TryWeakSpot(b.CombatSeconds+7))return "recovery extension once per move";
            float before=b.Hp;b.ApplyDamage(600);
            if(b.Phase!=BossPhase.P2||b.Hp!=before-600||b.Phase2Transitions!=1)return "threshold overflow retained";
            b.ApplyDamage(1);if(b.Phase2Transitions!=1)return "P2 once";
            if(b.CanUse(BossMoveId.RingBurst)||b.CanUse(BossMoveId.ShieldBash))return "retired/wrong phase skill";
            if(b.TryStart(BossMoveId.TripleBurst))return "P2 burst cannot skip cadence";
            if(!b.TryStart(BossMoveId.TripleShot))return "P2 triple";
            b.NotifyRangedFired();b.NotifyRangedFired();if(b.P2RangedUses!=1)return "P2 counts first fire once";
            Advance(b,5);Advance(b,.5f);if(!b.TryStart(BossMoveId.TripleShot))return "P2 second triple";
            b.NotifyRangedFired();Advance(b,5);Advance(b,.5f);
            if(b.NextP2Ranged!=BossMoveId.TripleBurst||b.TryStart(BossMoveId.TripleShot))return "P2 third variant";
            if(!b.TryStart(BossMoveId.CrossbowBash))return "P2 rotate after two ranged";Advance(b,4);Advance(b,.5f);
            if(!b.TryStart(BossMoveId.TripleBurst))return "P2 third burst";b.NotifyRangedFired();b.NotifyRangedFired();
            if(b.P2RangedUses!=3||b.NextP2Ranged!=BossMoveId.TripleShot)return "P2 burst consumes one use";
            b.ApplyDamage(10000);if(b.Phase!=BossPhase.Defeated||b.Hp!=0)return "death";
            var kill=Enter();kill.ApplyDamage(1200);if(kill.Phase2Transitions!=0)return "lethal skips phase transition";
            var p=FinalBossCombat.PredictPoint(Vector3.zero,Vector3.left*6,Vector2.right*6);
            if(p.x<=0||p.x>3.0001f||p.y!=0)return "prediction clamp";
            return null;
        }
        public static string FormatPass(BossBrain b)=>"PASS BOSS v0.54 phase/DR/finite melee/counters/cooldowns/quiet/weak/death";
    }
}
