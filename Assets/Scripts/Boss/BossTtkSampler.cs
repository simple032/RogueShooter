using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RogueShooter.Balance;
using RogueShooter.Build;
using RogueShooter.Spawning;

namespace RogueShooter.Boss
{
    /// <summary>
    /// Multi-run BOSS TTK sample hook for testing.
    /// Shop buys never change Build; boss_build is altar/chest Build only.
    /// Stub DPS is fixed — does not assume Build↑ = attack↑.
    /// </summary>
    public static class BossTtkSampler
    {
        public const float DefaultStubDps = 40f;

        public struct Row
        {
            public int BossBuild;
            public float ArriveT;
            public string Diff;
            public string BoughtNonHeal;
            public float TtkS;
            public string Outcome;
            public int ArriveGold;
            public int GoldLeft;
            public float BossMaxHp;
        }

        public static string CsvHeader()
        {
            return "boss_build,arrive_t,diff,bought_non_heal,ttk_s,outcome,arrive_gold,gold_left,boss_max_hp";
        }

        public static string FormatCsv(Row row)
        {
            return string.Format(
                "{0},{1:0.###},{2},{3},{4:0.###},{5},{6},{7},{8:0.#}",
                row.BossBuild,
                row.ArriveT,
                Escape(row.Diff),
                Escape(row.BoughtNonHeal),
                row.TtkS,
                row.Outcome,
                row.ArriveGold,
                row.GoldLeft,
                row.BossMaxHp);
        }

        /// <summary>
        /// One sample: random non-heal shop buys with arrive gold, then stub-DPS BOSS fight.
        /// </summary>
        public static Row RunOnce(int seed, int bossBuild, float arriveMinutes, int arriveGold, float stubDps)
        {
            if (stubDps < 0.01f)
                stubDps = DefaultStubDps;
            if (arriveGold < 0)
                arriveGold = 0;
            if (bossBuild < 0)
                bossBuild = 0;

            var rng = new Random(seed);
            var shelves = ShopStock.RollShelves(rng);
            var bought = new List<string>();
            int gold = arriveGold;

            // Random affordable non-heal purchases (no heal). Shuffle indices.
            var order = new List<int>();
            for (int i = 0; i < shelves.Length; i++)
                order.Add(i);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int tmp = order[i];
                order[i] = order[j];
                order[j] = tmp;
            }

            for (int n = 0; n < order.Count; n++)
            {
                int idx = order[n];
                ShopShelf s = shelves[idx];
                if (s.Sold || s.IsHeal || s.ContentRole == ShopSlotRole.Heal)
                    continue;
                if (s.Price > gold)
                    continue;
                // Random skip so builds vary
                if (rng.Next(100) < 35)
                    continue;
                gold -= s.Price;
                s.Sold = true;
                shelves[idx] = s;
                bought.Add(s.Id + "@" + s.Price + "(" + ShopStock.RoleLabel(s.ContentRole) + ")");
            }

            float tm = TimePressure.AttrMul(arriveMinutes);
            float bm = SpawnWaveCatalog.BuildMul(bossBuild);
            var snap = BossScaleTable.Resolve(bossBuild, arriveMinutes, tm, bm);
            var brain = new BossBrain();
            brain.Configure(BossScaleTable.BaseHp);
            brain.LockHpOnEnter(snap);
            brain.NotifyEnter();
            brain.Tick(0.02f);
            brain.Tick(0.02f);

            float hp = brain.Hp;
            float ttk = 0f;
            const float dt = 0.05f;
            float guard = 600f;
            while (hp > 0f && ttk < guard)
            {
                brain.ApplyDamage(stubDps * dt);
                hp = brain.Hp;
                ttk += dt;
                if (brain.Phase == BossPhase.Defeated)
                    break;
            }

            bool win = brain.Phase == BossPhase.Defeated || hp <= 0f;
            return new Row
            {
                BossBuild = bossBuild,
                ArriveT = arriveMinutes,
                Diff = snap.AnchorId + "|T" + snap.TimeTier + "|MaxHP=" + snap.MaxHp.ToString("0"),
                BoughtNonHeal = bought.Count == 0 ? "(none)" : string.Join(";", bought.ToArray()),
                TtkS = ttk,
                Outcome = win ? "win" : "timeout",
                ArriveGold = arriveGold,
                GoldLeft = gold,
                BossMaxHp = snap.MaxHp
            };
        }

        public static string RunBatchCsv(int runs, int seed0, float stubDps)
        {
            var sb = new StringBuilder();
            sb.AppendLine(CsvHeader());
            var builds = new[] { 6, 8, 10, 12 };
            var arrives = new[] { 6f, 7.5f, 9f };
            var golds = new[] { 60, 80, 100, 120 };
            int n = 0;
            for (int i = 0; i < runs; i++)
            {
                int seed = seed0 + i * 97;
                int build = builds[i % builds.Length];
                float arrive = arrives[i % arrives.Length];
                int gold = golds[i % golds.Length];
                Row row = RunOnce(seed, build, arrive, gold, stubDps);
                sb.AppendLine(FormatCsv(row));
                n++;
            }

            sb.Append("# samples=").Append(n)
                .Append(" stub_dps=").Append(stubDps.ToString("0.#"))
                .Append(" note=shop_buy_never_changes_build; dps_fixed_not_scaled_by_build");
            return sb.ToString();
        }

        static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "";
            if (s.IndexOf(',') >= 0 || s.IndexOf('"') >= 0)
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }
    }
}
