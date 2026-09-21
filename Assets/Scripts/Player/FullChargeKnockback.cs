using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RogueShooter.Balance;
using RogueShooter.Spawning;

namespace RogueShooter.Player
{
    /// <summary>
    /// Full-charge knockback (held ≥ ring-full 0.70s). DRAFT mid distances from
    /// balance_knockback_fullcharge_draft.csv — not lock CSV. Elite uses the
    /// same species row. Weak charge: 0. Direction: shot_away.
    /// Non-boss: kb ≈ move × t_return (0.6s). BOSS mid is table-only (no formula).
    /// </summary>
    public static class FullChargeKnockback
    {
        public const string FileName = "balance_knockback_fullcharge_draft.csv";
        public const string LockNote = "DRAFT_NOT_LOCKED";
        public const string FormulaNote = "kb_dist=move_spd*t_return";
        public const float BossSlideSpeedStub = 12f; // BOSS only; not the 0.6s formula
        public const float MinSlideSeconds = 0.08f;

        // Fallback mids if CSV missing (same as draft table).
        const float FallbackNormal = 2.7f;
        const float FallbackDog = 4.32f;
        const float FallbackMage = 2.16f;
        const float FallbackGrand = 1.98f;
        const float FallbackShieldOpen = 2.7f;
        const float FallbackShieldRaised = 1.35f;
        const float FallbackBoss = 0.30f;
        const float FallbackReturnSeconds = 0.6f;

        static readonly Dictionary<string, float> Mid = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        static float _shieldRaisedMid = FallbackShieldRaised;
        static float _bossMid = FallbackBoss;
        static float _returnSeconds = FallbackReturnSeconds;
        static bool _loaded;
        static string _source = "fallback";

        public static string Source => _source;
        public static bool Loaded => _loaded;

        public static float ReturnSeconds
        {
            get
            {
                EnsureLoaded();
                return _returnSeconds;
            }
        }

        public static bool Applies(ChargeShotKind kind, float heldSeconds)
        {
            if (kind == ChargeShotKind.None || kind == ChargeShotKind.Weak)
                return false;
            return heldSeconds + 0.0001f >= ChargeShotRules.RingFillSeconds;
        }

        public static float MidDistance(string kindId, bool shieldRaised)
        {
            return MidDistance(kindId, shieldRaised, false);
        }

        public static float MidDistance(string kindId, bool shieldRaised, bool boss)
        {
            EnsureLoaded();
            if (boss)
                return _bossMid;
            if (string.Equals(kindId, EnemyKindIds.Shield, StringComparison.OrdinalIgnoreCase))
                return shieldRaised ? _shieldRaisedMid : Get(EnemyKindIds.Shield, FallbackShieldOpen);
            if (string.Equals(kindId, EnemyKindIds.Dog, StringComparison.OrdinalIgnoreCase))
                return Get(EnemyKindIds.Dog, FallbackDog);
            if (string.Equals(kindId, EnemyKindIds.CultMage, StringComparison.OrdinalIgnoreCase))
                return Get(EnemyKindIds.CultMage, FallbackMage);
            if (string.Equals(kindId, EnemyKindIds.GrandMage, StringComparison.OrdinalIgnoreCase))
                return Get(EnemyKindIds.GrandMage, FallbackGrand);
            return Get(EnemyKindIds.Normal, FallbackNormal);
        }

        /// <summary>Elite / E-wave uses the same species mid. Flag ignored on purpose.</summary>
        public static float MidDistanceEliteSame(string kindId, bool shieldRaised, bool elite)
        {
            return MidDistance(kindId, shieldRaised, false);
        }

        public static float SlideSeconds(float distance)
        {
            return SlideSeconds(distance, false);
        }

        /// <summary>
        /// Non-boss: CSV t_return_s (0.6). BOSS: short stub — table says no 0.6s formula.
        /// </summary>
        public static float SlideSeconds(float distance, bool boss)
        {
            EnsureLoaded();
            if (distance < 0.01f)
                return 0f;
            if (boss)
            {
                float t = distance / BossSlideSpeedStub;
                if (t < MinSlideSeconds)
                    t = MinSlideSeconds;
                return t;
            }

            return _returnSeconds > 0.01f ? _returnSeconds : FallbackReturnSeconds;
        }

        public static bool EnsureLoaded()
        {
            if (_loaded)
                return true;
            string dir = EnemyPoolDraft.ResolveDirectory();
            return TryLoadFromDirectory(dir);
        }

        public static bool TryLoadFromDirectory(string dir)
        {
            SeedFallback();
            if (string.IsNullOrEmpty(dir))
            {
                _source = "fallback-mids " + LockNote;
                _loaded = true;
                return false;
            }

            string path = Path.Combine(dir, FileName);
            if (!File.Exists(path))
            {
                _source = "fallback-mids missing " + path + " " + LockNote;
                _loaded = true;
                return false;
            }

            CsvTable table = CsvTable.Parse(File.ReadAllText(path, Encoding.UTF8));
            foreach (string[] row in table.DataRows())
            {
                string enemy = table.Get(row, "enemy");
                if (string.IsNullOrEmpty(enemy))
                    continue;
                if (enemy.StartsWith("META", StringComparison.OrdinalIgnoreCase))
                {
                    string key = table.Get(row, "phase_pool");
                    if (string.Equals(key, "t_return_s", StringComparison.OrdinalIgnoreCase))
                    {
                        float t = CsvTable.ToFloat(table.Get(row, "kb_dist_mid"), -1f);
                        if (t > 0f)
                            _returnSeconds = t;
                    }

                    continue;
                }

                float mid = CsvTable.ToFloat(table.Get(row, "kb_dist_mid"), -1f);
                if (mid < 0f)
                    continue;
                if (enemy.IndexOf("BOSS", StringComparison.OrdinalIgnoreCase) >= 0
                    || enemy.IndexOf("领主", StringComparison.Ordinal) >= 0)
                {
                    _bossMid = mid;
                    continue;
                }

                // "不举盾" contains "举盾" — match unshielded first.
                if (enemy.IndexOf("不举盾", StringComparison.Ordinal) >= 0
                    || enemy.IndexOf("不举", StringComparison.Ordinal) >= 0)
                {
                    Mid[EnemyKindIds.Shield] = mid;
                    continue;
                }

                if (enemy.IndexOf("举盾", StringComparison.Ordinal) >= 0)
                {
                    _shieldRaisedMid = mid;
                    continue;
                }

                string kind = EnemyPoolDraft.KindIdFromChinese(enemy);
                Mid[kind] = mid;
            }

            _loaded = true;
            _source = path + " (" + LockNote + ")";
            return true;
        }

        static float Get(string kind, float fallback)
        {
            float v;
            if (Mid.TryGetValue(kind, out v))
                return v;
            return fallback;
        }

        static void SeedFallback()
        {
            Mid.Clear();
            Mid[EnemyKindIds.Normal] = FallbackNormal;
            Mid[EnemyKindIds.Dog] = FallbackDog;
            Mid[EnemyKindIds.CultMage] = FallbackMage;
            Mid[EnemyKindIds.GrandMage] = FallbackGrand;
            Mid[EnemyKindIds.Shield] = FallbackShieldOpen;
            _shieldRaisedMid = FallbackShieldRaised;
            _bossMid = FallbackBoss;
            _returnSeconds = FallbackReturnSeconds;
        }
    }
}
