using System;
using System.Text;

namespace RogueShooter.Balance
{
    public static class EnemyDamageChecks
    {
        /// <summary>Returns null on pass.</summary>
        public static string Run()
        {
            if (!Match(EnemyDamageCatalog.E1, 30f, 1.10f, 0.42f, 25f, 35f))
                return "E1 recommend 30/1.10/0.42 hit%25-35";
            if (!Match(EnemyDamageCatalog.E2, 20f, 0.80f, 0.42f, 15f, 25f))
                return "E2 recommend 20/0.80/0.42 hit%15-25";
            if (!Match(EnemyDamageCatalog.E3, 20f, 1.10f, 0.22f, 15f, 25f))
                return "E3 recommend 20/1.10/0.22 hit%15-25";
            if (!Match(EnemyDamageCatalog.E4, 40f, 1.80f, 0.22f, 35f, 45f))
                return "E4 recommend 40/1.80/0.22 hit%35-45";

            // Speed order: E2 faster (lower interval) than E1/E3; E4 slowest
            if (!(EnemyDamageCatalog.E2.AttackIntervalRecommend < EnemyDamageCatalog.E1.AttackIntervalRecommend))
                return "E2 faster than E1";
            if (!(EnemyDamageCatalog.E4.AttackIntervalRecommend > EnemyDamageCatalog.E1.AttackIntervalRecommend))
                return "E4 slower than E1";

            // Windup: E3/E4 low vs E1/E2 mid
            if (!(EnemyDamageCatalog.E3.WindupRecommend < EnemyDamageCatalog.E1.WindupRecommend))
                return "E3 windup lower than E1";
            if (!(EnemyDamageCatalog.E4.WindupRecommend < EnemyDamageCatalog.E2.WindupRecommend))
                return "E4 windup lower than E2";

            float pct = EnemyDamageCatalog.HitPct("E1");
            if (Math.Abs(pct - 30f) > 0.01f)
                return "E1 hit% expect 30 got " + pct;

            return null;
        }

        static bool Match(EnemyDamageDef d, float atk, float iv, float wu, float hitMin, float hitMax)
        {
            if (Math.Abs(d.AtkRecommend - atk) > 0.01f) return false;
            if (Math.Abs(d.AttackIntervalRecommend - iv) > 0.001f) return false;
            if (Math.Abs(d.WindupRecommend - wu) > 0.001f) return false;
            if (Math.Abs(d.HitPctMin - hitMin) > 0.01f) return false;
            if (Math.Abs(d.HitPctMax - hitMax) > 0.01f) return false;
            float pct = d.AtkRecommend / d.PlayerMaxHpRef * 100f;
            return pct >= hitMin - 0.01f && pct <= hitMax + 0.01f;
        }

        public static string FormatPass()
        {
            var sb = new StringBuilder();
            sb.Append("ACCEPTANCE PASS N20 ").Append(EnemyDamageCatalog.FormatAll());
            return sb.ToString();
        }
    }
}
