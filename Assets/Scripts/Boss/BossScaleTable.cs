using UnityEngine;
using System;
using RogueShooter.Balance;

namespace RogueShooter.Boss
{
    public struct BossScaleRow
    {
        public string Id;
        public int BuildMin;
        public int BuildMax;
        public int TimeTierMax;
        public float HpMul;
        public float DmgMul;
        public string Expect;
    }

    public struct BossScaleSnapshot
    {
        public string AnchorId;
        public int BuildCount;
        public float RarityScore;
        public float Power;
        public int TimeTier;
        public float HpMul;
        public float DmgMul;
        public float MaxHp;
        public float Bm;
        public float Tm;
        public bool UsedContinuous;
    }

    /// <summary>1100 HP at zero minutes. Entry locks time-scaled HP; damage follows the live damage curve. Build is report-only.</summary>
    public static class BossScaleTable
    {
        public const float BaseHp = FinalBossRules.BaseHp;
        public const float DiffRefAttr = FinalBossRules.DiffRef;
        public const float PowerBuildCoef = 0.45f;
        public const float PowerRarityCoef = 0.55f;
        public const float AnchorTol = 0.02f;

        // B1b from balance_boss_scale_箭骸.csv
        public static readonly BossScaleRow[] Anchors = new BossScaleRow[0];
        public static int TimeTierFromMinutes(float minutes) => minutes<4f?1:minutes<8f?2:minutes<12f?3:4;
        public static float ComputePower(int b,float r) => PowerBuildCoef*b+PowerRarityCoef*r;
        public static bool TryExact(int b,int t,out BossScaleRow row){row=default(BossScaleRow);return false;}
        // Legacy tier/pressure callers remain supported. Production Resolve uses exact minutes.
        public static void ContinuousMuls(int b,int t,out float hp,out float dmg)
        { float minutes=t<=1?0:t==2?4:t==3?8:12;hp=HpMulFor(minutes);dmg=DmgMulFor(b,minutes); }
        public static float PressureCoordinate(float attr) => attr;
        public static void ContinuousMuls(int b,float pressure,out float hp,out float dmg)
        { float t=MinutesForPressure(pressure);hp=HpMulFor(t);dmg=DmgMulFor(b,t); }
        static float MinutesForPressure(float p)
        { p=Mathf.Clamp(p,1,1.75f);return p<=1.15f?(p-1)/.0375f:p<=1.35f?4+(p-1.15f)*15:p<=1.55f?7+(p-1.35f)*15:p<=1.70f?10+(p-1.55f)/.03f:15+(p-1.70f)/.015f; }
        public static float HpMulFor(float minutes) => TimePressure.AttrMul(minutes)/DiffRefAttr;
        public static BossScaleSnapshot Resolve(int b,float minutes,float tm,float bm) => Resolve(b,b,minutes,tm,bm);
        public static BossScaleSnapshot Resolve(int b,float rarity,float minutes,float tm,float bm)
        {
            float hp=HpMulFor(minutes);
            return new BossScaleSnapshot{AnchorId="time-entry",BuildCount=b,RarityScore=rarity,Power=ComputePower(b,rarity),
                TimeTier=TimeTierFromMinutes(minutes),HpMul=hp,DmgMul=DmgMulFor(b,minutes),MaxHp=BaseHp*hp,Bm=bm,Tm=TimePressure.AttrMul(minutes),UsedContinuous=true};
        }
        public static float DmgMulFor(int b,float minutes) => TimePressure.DamageMul(minutes);
        public static float DmgMulFor(int b,float rarity,float minutes) => DmgMulFor(b,minutes);
        public static string VerifyAnchorsAe() => null;
    }
}
