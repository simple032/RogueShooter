using UnityEngine;
namespace RogueShooter.Boss
{
    public struct BossSkill
    {
        public float Windup,Lock,Active,Recovery,Cooldown,Damage,Speed,Radius,Angle,Min,Max;
        public BossSkill(float w,float l,float a,float r,float cd,float dmg,float speed,float radius,float angle,float min,float max)
        {Windup=w;Lock=l;Active=a;Recovery=r;Cooldown=cd;Damage=dmg;Speed=speed;Radius=radius;Angle=angle;Min=min;Max=max;}
    }
    // Runtime authority matches v0.56. Gameplay seconds independent of animation frames.
    public static class FinalBossRules
    {
        public const float BaseHp=1100f,DiffRef=1f,DamageWeightRef=1.28f;
        public const float Intro=1f,Transition=1.2f,HitRadius=.7f,BodyScale=1.7f;
        /// <summary>Visible warden height at play scale (~293px at PPU 128). The arrow
        /// hit tube samples the whole standing body, not only the feet circle.</summary>
        public const float HitBodyHeight=2.3f;
        public const float ShotRange=12f,TripleSpread=15f,DashDistance=7f;
        public const float RecoveryExtension=.5f,WeakCooldown=6f,PlayerRadius=.32f;
        public const float LeapAir=.85f,LeapHeight=.9f;
        public const float BoltRadius=.18f,TripleBoltRadius=.16f,VolleyInterval=.5f;
        public static float ProjectileRadius(BossMoveId m)=>IsP2Ranged(m)?TripleBoltRadius:BoltRadius;
        public static float TripleSweep(int volley)=>Mathf.Clamp(volley-1,-1,1)*6f;
        public static float MoveSpeed(BossPhase p)=>p==BossPhase.P2?4.5f:2.5f;
        public static float TurnSpeed(BossPhase p)=>p==BossPhase.P2?180f:90f;
        public static float Quiet(BossPhase p)=>p==BossPhase.P2?.35f:1.1f;
        public static BossSkill Skill(BossMoveId m)
        {
            switch(m)
            {
                case BossMoveId.StraightShot:return new BossSkill(1,.5f,.05f,1.65f,4,18,8,0,0,3.5f,10);
                case BossMoveId.BurstShot:return new BossSkill(1,.65f,1.05f,1.85f,4,12,8,0,0,3.5f,10);
                case BossMoveId.WarningCharge:return new BossSkill(1.15f,.6f,.7f,1.8f,6,24,10,.75f,0,3.5f,7);
                case BossMoveId.ShieldBash:return new BossSkill(.85f,.3f,.15f,1.4f,4.8f,20,0,2.6f,120,0,2.8f);
                case BossMoveId.TripleShot:return new BossSkill(.9f,.4f,.05f,1.3f,3,16,8.5f,0,15,3.5f,10);
                case BossMoveId.TripleBurst:return new BossSkill(.9f,.75f,1.05f,1.3f,3,16,8.5f,0,15,3.5f,10);
                case BossMoveId.LeapSlam:return new BossSkill(.7f,.7f,1,1.65f,6,26,0,2,360,4,9);
                case BossMoveId.CrossbowBash:return new BossSkill(.7f,.15f,.1f,1,3,22,0,2.8f,100,0,3);
                default:return default(BossSkill);
            }
        }
        public static float Windup(BossMoveId m)=>Skill(m).Windup;
        public static float LockAt(BossMoveId m)=>Skill(m).Lock;
        public static float Recovery(BossMoveId m)=>Skill(m).Recovery;
        public static float Damage(BossMoveId m)=>Skill(m).Damage;
        public static float Speed(BossMoveId m)=>Skill(m).Speed;
        public static bool IsP1Ranged(BossMoveId m)=>m==BossMoveId.StraightShot||m==BossMoveId.BurstShot;
        public static bool IsP2Ranged(BossMoveId m)=>m==BossMoveId.TripleShot||m==BossMoveId.TripleBurst;
        public static bool IsRanged(BossMoveId m)=>IsP1Ranged(m)||IsP2Ranged(m);
        public static bool IsMelee(BossMoveId m)=>m==BossMoveId.ShieldBash||m==BossMoveId.CrossbowBash;
        public static bool IsHeavy(BossMoveId m)=>m==BossMoveId.WarningCharge||m==BossMoveId.LeapSlam;
        public static float IncomingMultiplier(BossPhase p,Vector3 facing,Vector3 incoming,bool weak)
        {return p==BossPhase.P1&&!weak&&incoming.sqrMagnitude>.000001f&&Vector3.Dot(facing,-incoming)>=-0.000001f?.5f:1f;}
        public static bool InSector(Vector3 center,Vector3 facing,Vector3 point,float radius,float angle,float bodyRadius)
        {
            Vector2 p=point-center;float d=p.magnitude;
            if(d<=bodyRadius)return true;
            if(Vector2.Angle(facing,p)<=angle*.5f)return d<=radius+bodyRadius;
            float at=Mathf.Atan2(facing.y,facing.x)*Mathf.Rad2Deg;
            for(int side=-1;side<=1;side+=2)
            {
                float a=(at+side*angle*.5f)*Mathf.Deg2Rad;Vector2 edge=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;
                var closest=edge*Mathf.Clamp01(Vector2.Dot(p,edge)/edge.sqrMagnitude);
                if((p-closest).sqrMagnitude<=bodyRadius*bodyRadius)return true;
            }
            return false;
        }
    }
}
