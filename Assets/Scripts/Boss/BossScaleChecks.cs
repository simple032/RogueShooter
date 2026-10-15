using System;
using RogueShooter.Balance;
using RogueShooter.Spawning;

namespace RogueShooter.Boss
{
    public static class BossScaleChecks
    {
        public static string Run()
        {
            string ae = BossScaleTable.VerifyAnchorsAe();
            if (ae != null)
                return ae;

            // N31 continuous PL from balance_time_scale_箭骸.csv
            if (Math.Abs(TimePressure.AttrMul(0f) - 1.00f) > 0.001f) return "attr@0";
            if (Math.Abs(TimePressure.AttrMul(2f) - 1.075f) > 0.001f) return "attr@2";
            if (Math.Abs(TimePressure.AttrMul(4f) - 1.15f) > 0.001f) return "attr@4 knot";
            if (Math.Abs(TimePressure.AttrMul(5.5f) - 1.25f) > 0.001f) return "attr@5.5";
            if (Math.Abs(TimePressure.AttrMul(7f) - 1.35f) > 0.001f) return "attr@7 knot";
            if (Math.Abs(TimePressure.AttrMul(8.5f) - 1.45f) > 0.001f) return "attr@8.5";
            if (Math.Abs(TimePressure.AttrMul(10f) - 1.55f) > 0.001f) return "attr@10 knot";
            if (Math.Abs(TimePressure.AttrMul(12f) - 1.61f) > 0.001f) return "attr@12";
            if (Math.Abs(TimePressure.IntervalMul(0f) - 1.10f) > 0.001f) return "int@0";
            if (Math.Abs(TimePressure.IntervalMul(2f) - 1.05f) > 0.001f) return "int@2";
            if (Math.Abs(TimePressure.IntervalMul(4f) - 1.00f) > 0.001f) return "int@4 knot";
            if (Math.Abs(TimePressure.IntervalMul(5.5f) - 0.925f) > 0.001f) return "int@5.5";
            if (Math.Abs(TimePressure.IntervalMul(7f) - 0.85f) > 0.001f) return "int@7 knot";
            if (Math.Abs(TimePressure.IntervalMul(8.5f) - 0.785f) > 0.001f) return "int@8.5";
            if (Math.Abs(TimePressure.IntervalMul(10f) - 0.72f) > 0.001f) return "int@10 knot";
            if (Math.Abs(TimePressure.IntervalMul(12f) - 0.648f) > 0.001f) return "int@12";
            if (Math.Abs(TimePressure.IntervalFloor - 0.45f) > 0.001f) return "int floor 0.45";
            string cont = TimePressure.ContinuityCheck();
            if (cont != null)
                return cont;

            foreach(int build in new[]{8,11,14,18})
            foreach(float minutes in new[]{0f,8f,11.5f,12f,20f})
            {
                var snap=BossScaleTable.Resolve(build,minutes,TimePressure.AttrMul(minutes),1f);
                if(Math.Abs(snap.MaxHp-1100f*TimePressure.AttrMul(minutes))>.01f) return "entry time HP";
                if(Math.Abs(snap.DmgMul-TimePressure.DamageMul(minutes))>.001f) return "entry attack";
                var brain=new BossBrain();brain.Configure(1100f);brain.LockHpOnEnter(snap);brain.NotifyTimeCross(25f);
                if(Math.Abs(brain.LiveDmgMul-TimePressure.DamageMul(25f))>.001f) return "live attack curve";
                if(Math.Abs(brain.MaxHp-snap.MaxHp)>.01f||Math.Abs(brain.Hp-snap.MaxHp)>.01f)return "HP changed during fight";
            }
            // GDD v0.61: fixed expected anchors, interpolation, and capped damage.
            float[] damageTimes = { -1f, 0f, 2f, 4f, 6f, 8f, 10f, 12f, 13.5f, 15f, 17.5f, 20f, 30f };
            float[] damageExpected = { 1f, 1f, 1.05f, 1.10f, 1.20f, 1.30f, 1.425f, 1.55f, 1.65f, 1.75f, 1.875f, 2f, 2f };
            for (int i = 0; i < damageTimes.Length; i++)
                if (Math.Abs(TimePressure.DamageMul(damageTimes[i]) - damageExpected[i]) > .001f)
                    return "attack curve@" + damageTimes[i];
            foreach (float knot in new[] { 4f, 8f, 12f, 15f, 20f })
                if (Math.Abs(TimePressure.DamageMul(knot - .001f) - TimePressure.DamageMul(knot + .001f)) > .001f)
                    return "attack discontinuity@" + knot;
            if(Math.Abs(TimePressure.AttrMul(30f)-1.75f)>.001f) return "HP cap";

            return null;
        }

        public static string FormatPass(BossBrain brain, BossScaleSnapshot snap)
        {
            return string.Format(
                "ACCEPTANCE PASS W3-04 bands OK cont={0} lock anchor={1} MaxHP={2:0} enterDmg={3:0.00} crossDmg={4:0.00} B={5} Tm={6:0.00} Bm={7:0.00} Power={8:0.00} AEΔ≤0.02",
                snap.UsedContinuous, snap.AnchorId, brain.MaxHp, brain.EnterDmgMul, brain.LiveDmgMul,
                snap.BuildCount, snap.Tm, snap.Bm, snap.Power);
        }
    }
}
