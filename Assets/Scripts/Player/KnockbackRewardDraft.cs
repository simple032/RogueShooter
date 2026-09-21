using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RogueShooter.Balance;
using RogueShooter.Build;
using RogueShooter.Spawning;

namespace RogueShooter.Player
{
    /// <summary>
    /// DRAFT 震矢 (R15) knockback-distance upgrades. lock=0. Multiplicative:
    /// kb = species_full * Π(1+kb_dist_pct) [* 1.5 if weak-spot].
    /// </summary>
    public static class KnockbackRewardDraft
    {
        public const string FileName = "balance_reward_knockback_draft.csv";
        public const string EffectKey = "kb_dist_pct";
        public const string LockNote = "DRAFT_NOT_LOCKED";
        public const string IdLow = "R15_C";
        public const string IdMid = "R15_R";
        public const float FallbackLow = 0.20f;
        public const float FallbackMid = 0.40f;

        static readonly Dictionary<string, float> Pct = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        static bool _loaded;
        static string _source = "fallback";

        public static string Source => _source;
        public static bool Loaded => _loaded;

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
                _source = "fallback-zhenshi " + LockNote;
                _loaded = true;
                return false;
            }

            string path = Path.Combine(dir, FileName);
            if (!File.Exists(path))
            {
                _source = "fallback-zhenshi missing " + path + " " + LockNote;
                _loaded = true;
                return false;
            }

            CsvTable table = CsvTable.Parse(File.ReadAllText(path, Encoding.UTF8));
            foreach (string[] row in table.DataRows())
            {
                string id = table.Get(row, "id");
                if (string.IsNullOrEmpty(id) || id.StartsWith("META", StringComparison.OrdinalIgnoreCase))
                    continue;
                string key = table.Get(row, "effect_key");
                if (!string.IsNullOrEmpty(key)
                    && !string.Equals(key, EffectKey, StringComparison.OrdinalIgnoreCase))
                    continue;
                float v = CsvTable.ToFloat(table.Get(row, "value"), -1f);
                if (v < 0f)
                    continue;
                Pct[id] = v;
            }

            _loaded = true;
            _source = path + " (" + LockNote + ")";
            return true;
        }

        public static float PctForId(string id)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(id))
                return 0f;
            float v;
            if (Pct.TryGetValue(id, out v))
                return v;
            RewardRow row;
            if (RewardCatalog.TryGet(id, out row)
                && string.Equals(row.Stat, EffectKey, StringComparison.OrdinalIgnoreCase))
                return row.Value;
            return 0f;
        }

        /// <summary>Π (1 + kb_dist_pct) over owned 震矢. 1 if none.</summary>
        public static float DistPctProduct(IList<string> ownedIds)
        {
            EnsureLoaded();
            float p = 1f;
            if (ownedIds == null)
                return p;
            for (int i = 0; i < ownedIds.Count; i++)
                p *= 1f + PctForId(ownedIds[i]);
            return p;
        }

        public static bool CatalogHasHighTier()
        {
            for (int i = 0; i < RewardCatalog.All.Length; i++)
            {
                RewardRow row = RewardCatalog.All[i];
                if (row.Name == "震矢" && row.Tier == RewardTier.High)
                    return true;
                if (row.Id != null && row.Id.StartsWith("R15_", StringComparison.OrdinalIgnoreCase)
                    && row.Tier == RewardTier.High)
                    return true;
            }

            return false;
        }

        static void SeedFallback()
        {
            Pct.Clear();
            Pct[IdLow] = FallbackLow;
            Pct[IdMid] = FallbackMid;
        }
    }
}
