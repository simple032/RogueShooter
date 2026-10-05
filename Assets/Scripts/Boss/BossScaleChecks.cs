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

            // A@B14 pressure 1.25 → enter HP = 1200 * 1 * 1.25/1.55, damage = 1/1.28 * 1.25/1.55.
            float minsA = 5.5f;
            float tmA = TimePressure.AttrMul(minsA);
            float bmA = SpawnWaveCatalog.BuildMul(14);
            var snapA = BossScaleTable.Resolve(14, minsA, tmA, bmA);
            if (snapA.AnchorId != "A" || !snapA.UsedContinuous)
                return "A continuous exact";
            if (Math.Abs(BossScaleTable.BaseHp - 1200f) > 0.01f)
                return "BaseHp!=1200";
            float expectHp = FinalBossRules.EnterMaxHp(14, tmA);
            float expectDmg = FinalBossRules.OutgoingMultiplier(14, tmA);
            if (Math.Abs(snapA.MaxHp - expectHp) > 0.02f || Math.Abs(snapA.DmgMul - expectDmg) > 0.001f)
                return "A MaxHP/dmg";
            if (Math.Abs(expectHp - (1200f * 1.25f / 1.55f)) > 1f)
                return "A enter formula";
            if (Math.Abs(snapA.MaxHp - 3850f) < 1f || Math.Abs(snapA.MaxHp - 1100f) < 1f)
                return "A still on a retired hp";

            var brain = new BossBrain();
            brain.Configure(BossBrain.DefaultMaxHp);
            brain.LockHpOnEnter(snapA);
            brain.NotifyEnter();
            brain.Tick(0.02f);
            brain.Tick(0.02f);
            float hpAtEnter = brain.Hp;
            float crossPressure = TimePressure.AttrMul(11f);
            brain.RefreshDamage(crossPressure);
            if (Math.Abs(brain.LiveDmgMul - FinalBossRules.OutgoingMultiplier(14, crossPressure)) > 0.001f)
                return "cross dmg";
            if (Math.Abs(brain.Hp - hpAtEnter) > 0.01f || Math.Abs(brain.MaxHp - expectHp) > 0.05f)
                return "cross must not change HP";

            // Mid-point continuous (not discrete-only)
            float hpMid, dmgMid;
            BossScaleTable.ContinuousMuls(12, 2, out hpMid, out dmgMid);
            if (hpMid < 0.88f || hpMid > 1.15f)
                return "mid continuous hp in band";

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
