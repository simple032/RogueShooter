using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RogueShooter.Balance;
using RogueShooter.Spawning;

namespace RogueShooter.Player
{
    /// <summary>
    /// Full-charge / weak-spot CC from balance_knockback_fullcharge_draft.csv (DRAFT).
    /// Body full-charge: kb ≈ move × 0.6. Weak-spot: that distance × 1.5 + stagger.
    /// Shield raised body hit: no knockback, 0.5s root. Weak charge: none.
    /// Elite uses the same species row.
    /// </summary>
    public static class FullChargeKnockback
    {
        public const string FileName = "balance_knockback_fullcharge_draft.csv";
        public const string LockNote = "DRAFT_NOT_LOCKED";
        public static string FormulaNote = "kb=move*0.6 *Π(1+kb_dist_pct) [*1.5 ws]";
        public const float BossSlideSpeedStub = 12f;
        public const float MinSlideSeconds = 0.08f;

        const float FallbackNormal = 2.7f;
        const float FallbackDog = 4.32f;
        const float FallbackMage = 2.16f;
        const float FallbackGrand = 1.98f;
        const float FallbackShieldOpen = 2.7f;
        const float FallbackBoss = 0.30f;
        const float FallbackReturnSeconds = 0.6f;
        const float FallbackWeakSpotMul = 1.5f;
        const float FallbackShieldRootSeconds = 0.5f;

        static readonly Dictionary<string, float> Mid = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, float> WeakMid = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        static float _bossMid = FallbackBoss;
        static float _bossWeakMid = FallbackBoss * FallbackWeakSpotMul;
        static float _shieldRaisedWeakMid = FallbackShieldOpen * FallbackWeakSpotMul;
        static float _returnSeconds = FallbackReturnSeconds;
        static float _weakSpotMul = FallbackWeakSpotMul;
        static float _shieldRootSeconds = FallbackShieldRootSeconds;
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

        public static float WeakSpotMul
        {
            get
            {
                EnsureLoaded();
                return _weakSpotMul;
            }
        }

        public static float ShieldRaisedRootSeconds
        {
            get
            {
                EnsureLoaded();
                return _shieldRootSeconds;
            }
        }

        /// <summary>
        /// Full-charge body (held ≥ current full charge; 0.70s at 0B) or weak-spot window (Crit).
        /// Weak / none: no CC.
        /// </summary>
        public static bool Applies(ChargeShotKind kind, float heldSeconds)
        {
            return Applies(kind, heldSeconds, ChargeShotRules.RingFillSeconds);
        }

        /// <summary>Same with the loadout's full charge (疾张 shortens it).</summary>
        public static bool Applies(ChargeShotKind kind, float heldSeconds, float fullChargeSeconds)
        {
            if (kind == ChargeShotKind.None || kind == ChargeShotKind.Weak)
                return false;
            if (kind == ChargeShotKind.Crit)
                return true;
            return heldSeconds + ChargeShotRules.EdgeEpsilon >= fullChargeSeconds;
        }

        public static bool RootsOnBodyHit(string kindId, bool shieldRaised, bool weakSpot)
        {
            EnsureLoaded();
            if (weakSpot || !shieldRaised)
                return false;
            return string.Equals(kindId, EnemyKindIds.Shield, StringComparison.OrdinalIgnoreCase);
        }

        public static float MidDistance(string kindId, bool shieldRaised)
        {
            return HitDistance(kindId, shieldRaised, false, false);
        }

        public static float MidDistance(string kindId, bool shieldRaised, bool boss)
        {
            return HitDistance(kindId, shieldRaised, boss, false);
        }

        /// <summary>Elite / E-wave uses the same species mid. Flag ignored on purpose.</summary>
        public static float MidDistanceEliteSame(string kindId, bool shieldRaised, bool elite)
        {
            return HitDistance(kindId, shieldRaised, false, false);
        }

        public static float HitDistance(string kindId, bool shieldRaised, bool boss, bool weakSpot)
        {
            return HitDistance(kindId, shieldRaised, boss, weakSpot, 1f);
        }

        /// <summary>
        /// kb = species_full * distPctProduct [* weakSpotMul].
        /// Shield-raised body stays 0 (震矢 does not convert root to knockback).
        /// </summary>
        public static float HitDistance(
            string kindId, bool shieldRaised, bool boss, bool weakSpot, float distPctProduct)
        {
            EnsureLoaded();
            if (string.Equals(kindId, EnemyKindIds.Shield, StringComparison.OrdinalIgnoreCase)
                && shieldRaised && !weakSpot)
                return 0f;
            if (distPctProduct < 0.01f)
                distPctProduct = 1f;

            float kb;
            if (boss)
                kb = _bossMid;
            else
                kb = BodyFull(kindId);
            kb *= distPctProduct;
            if (weakSpot)
                kb *= _weakSpotMul;
            return kb;
        }

        public static float HitDistance(
            string kindId, bool shieldRaised, bool boss, bool weakSpot, IList<string> ownedRewardIds)
        {
            return HitDistance(
                kindId, shieldRaised, boss, weakSpot,
                KnockbackRewardDraft.DistPctProduct(ownedRewardIds));
        }

        public static float SlideSeconds(float distance)
        {
            return SlideSeconds(distance, false);
        }

        /// <summary>Non-boss: CSV t_return_s (0.6). BOSS: short stub — no 0.6s formula.</summary>
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
                    ApplyMeta(table.Get(row, "phase_pool"), FullCell(table, row));
                    continue;
                }

                float full = FullCell(table, row);
                float weak = CsvTable.ToFloat(table.Get(row, "kb_weakspot_mid"), -1f);
                string action = table.Get(row, "on_fullcharge");

                if (enemy.IndexOf("BOSS", StringComparison.OrdinalIgnoreCase) >= 0
                    || enemy.IndexOf("领主", StringComparison.Ordinal) >= 0)
                {
                    if (full >= 0f)
                        _bossMid = full;
                    if (weak >= 0f)
                        _bossWeakMid = weak;
                    else
                        _bossWeakMid = _bossMid * _weakSpotMul;
                    continue;
                }

                if (enemy.IndexOf("不举盾", StringComparison.Ordinal) >= 0
                    || enemy.IndexOf("不举", StringComparison.Ordinal) >= 0)
                {
                    if (full >= 0f)
                        Mid[EnemyKindIds.Shield] = full;
                    if (weak >= 0f)
                        WeakMid[EnemyKindIds.Shield] = weak;
                    continue;
                }

                if (enemy.IndexOf("举盾", StringComparison.Ordinal) >= 0)
                {
                    if (weak >= 0f)
                        _shieldRaisedWeakMid = weak;
                    float root = ParseTaggedSeconds(action, -1f);
                    if (root > 0f)
                        _shieldRootSeconds = root;
                    continue;
                }

                if (full < 0f)
                    continue;
                string kind = EnemyPoolDraft.KindIdFromChinese(enemy);
                Mid[kind] = full;
                if (weak >= 0f)
                    WeakMid[kind] = weak;
            }

            _loaded = true;
            _source = path + " (" + LockNote + ")";
            return true;
        }

        static void ApplyMeta(string key, float value)
        {
            if (string.Equals(key, "t_return_s", StringComparison.OrdinalIgnoreCase) && value > 0f)
                _returnSeconds = value;
            else if (string.Equals(key, "kb_weakspot_mul", StringComparison.OrdinalIgnoreCase) && value > 0f)
                _weakSpotMul = value;
        }

        static float FullCell(CsvTable table, string[] row)
        {
            string raw = table.Get(row, "kb_full_mid");
            if (string.IsNullOrEmpty(raw))
                raw = table.Get(row, "kb_dist_mid");
            return CsvTable.ToFloat(raw, -1f);
        }

        static float ParseTaggedSeconds(string raw, float fallback)
        {
            if (string.IsNullOrEmpty(raw))
                return fallback;
            var sb = new StringBuilder();
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                if ((c >= '0' && c <= '9') || c == '.')
                    sb.Append(c);
            }

            if (sb.Length == 0)
                return fallback;
            return CsvTable.ToFloat(sb.ToString(), fallback);
        }

        static float BodyFull(string kindId)
        {
            if (string.Equals(kindId, EnemyKindIds.Shield, StringComparison.OrdinalIgnoreCase))
                return Get(Mid, EnemyKindIds.Shield, FallbackShieldOpen);
            if (string.Equals(kindId, EnemyKindIds.Dog, StringComparison.OrdinalIgnoreCase))
                return Get(Mid, EnemyKindIds.Dog, FallbackDog);
            if (string.Equals(kindId, EnemyKindIds.CultMage, StringComparison.OrdinalIgnoreCase))
                return Get(Mid, EnemyKindIds.CultMage, FallbackMage);
            if (string.Equals(kindId, EnemyKindIds.GrandMage, StringComparison.OrdinalIgnoreCase))
                return Get(Mid, EnemyKindIds.GrandMage, FallbackGrand);
            return Get(Mid, EnemyKindIds.Normal, FallbackNormal);
        }

        static string NormalizedKind(string kindId)
        {
            if (string.Equals(kindId, EnemyKindIds.Shield, StringComparison.OrdinalIgnoreCase))
                return EnemyKindIds.Shield;
            if (string.Equals(kindId, EnemyKindIds.Dog, StringComparison.OrdinalIgnoreCase))
                return EnemyKindIds.Dog;
            if (string.Equals(kindId, EnemyKindIds.CultMage, StringComparison.OrdinalIgnoreCase))
                return EnemyKindIds.CultMage;
            if (string.Equals(kindId, EnemyKindIds.GrandMage, StringComparison.OrdinalIgnoreCase))
                return EnemyKindIds.GrandMage;
            return EnemyKindIds.Normal;
        }

        static float Get(Dictionary<string, float> map, string kind, float fallback)
        {
            float v;
            if (map.TryGetValue(kind, out v))
                return v;
            return fallback;
        }

        static void SeedFallback()
        {
            Mid.Clear();
            WeakMid.Clear();
            Mid[EnemyKindIds.Normal] = FallbackNormal;
            Mid[EnemyKindIds.Dog] = FallbackDog;
            Mid[EnemyKindIds.CultMage] = FallbackMage;
            Mid[EnemyKindIds.GrandMage] = FallbackGrand;
            Mid[EnemyKindIds.Shield] = FallbackShieldOpen;
            WeakMid[EnemyKindIds.Normal] = FallbackNormal * FallbackWeakSpotMul;
            WeakMid[EnemyKindIds.Dog] = FallbackDog * FallbackWeakSpotMul;
            WeakMid[EnemyKindIds.CultMage] = FallbackMage * FallbackWeakSpotMul;
            WeakMid[EnemyKindIds.GrandMage] = FallbackGrand * FallbackWeakSpotMul;
            WeakMid[EnemyKindIds.Shield] = FallbackShieldOpen * FallbackWeakSpotMul;
            _bossMid = FallbackBoss;
            _bossWeakMid = FallbackBoss * FallbackWeakSpotMul;
            _shieldRaisedWeakMid = FallbackShieldOpen * FallbackWeakSpotMul;
            _returnSeconds = FallbackReturnSeconds;
            _weakSpotMul = FallbackWeakSpotMul;
            _shieldRootSeconds = FallbackShieldRootSeconds;
        }
    }
}
