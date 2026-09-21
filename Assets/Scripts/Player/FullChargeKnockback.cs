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
    /// </summary>
    public static class FullChargeKnockback
    {
        public const string FileName = "balance_knockback_fullcharge_draft.csv";
        public const string LockNote = "DRAFT_NOT_LOCKED";
        public const float SlideSpeedStub = 12f; // world u/s; duration = dist / this (not locked)
        public const float MinSlideSeconds = 0.08f;

        // Fallback mids if CSV missing (same as draft table).
        const float FallbackNormal = 1.4f;
        const float FallbackDog = 2.1f;
        const float FallbackMage = 1.6f;
        const float FallbackGrand = 1.0f;
        const float FallbackShieldOpen = 0.75f;
        const float FallbackShieldRaised = 0.30f;
        const float FallbackBoss = 0.30f;

        static readonly Dictionary<string, float> Mid = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        static float _shieldRaisedMid = FallbackShieldRaised;
        static float _bossMid = FallbackBoss;
        static bool _loaded;
        static string _source = "fallback";

        public static string Source => _source;
        public static bool Loaded => _loaded;

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
            if (distance < 0.01f)
                return 0f;
            float t = distance / SlideSpeedStub;
            if (t < MinSlideSeconds)
                t = MinSlideSeconds;
            return t;
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
                float mid = CsvTable.ToFloat(table.Get(row, "kb_dist_mid"), -1f);
                if (string.IsNullOrEmpty(enemy) || mid < 0f)
                    continue;
                if (enemy.StartsWith("META", StringComparison.OrdinalIgnoreCase))
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
        }
    }
}
