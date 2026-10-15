using System;
using System.Collections.Generic;
using UnityEngine;
using RogueShooter.Balance;
using RogueShooter.Spawning;

namespace RogueShooter.Player
{
    /// <summary>One authored Resources config; thresholds are derived, never saved across runs.</summary>
    public static class ProgressionRules
    {
        [Serializable] sealed class EnemyXp { public string kind; public int xp; }
        [Serializable] sealed class Config
        {
            public int version, maxLevel, costConstant, costLinear, costQuadratic, bossXp;
            public float hpPerLevel, eliteXpMultiplier;
            public float[] stageXpMultipliers;
            public EnemyXp[] enemies;
        }
        static readonly Config Data;
        static readonly int[] Thresholds;
        static readonly Dictionary<string,int> EnemyRewards = new Dictionary<string,int>();

        static ProgressionRules()
        {
            var asset = Resources.Load<TextAsset>("Progression/player_progression");
            if (asset == null) throw new InvalidOperationException("Missing Progression/player_progression.json");
            Data = JsonUtility.FromJson<Config>(asset.text);
            if (Data == null || Data.version != 1 || Data.maxLevel < 2 || Data.maxLevel > 100
                || !Positive(Data.hpPerLevel) || !Positive(Data.eliteXpMultiplier)
                || Data.costConstant <= 0 || Data.costLinear < 0 || Data.costQuadratic < 0 || Data.bossXp <= 0
                || Data.stageXpMultipliers == null || Data.stageXpMultipliers.Length != 3 || Data.enemies == null)
                throw new InvalidOperationException("Invalid player progression config");
            foreach (float m in Data.stageXpMultipliers)
                if (!Positive(m)) throw new InvalidOperationException("Invalid stage XP multiplier");
            foreach (var row in Data.enemies)
            {
                if (row == null || string.IsNullOrEmpty(row.kind) || row.xp <= 0 || EnemyRewards.ContainsKey(row.kind))
                    throw new InvalidOperationException("Invalid / duplicate enemy XP record");
                EnemyRewards.Add(row.kind, row.xp);
            }
            foreach (string id in new[] {EnemyKindIds.Normal, EnemyKindIds.Dog, EnemyKindIds.CultMage, EnemyKindIds.Shield, EnemyKindIds.GrandMage})
                if (!EnemyRewards.ContainsKey(id)) throw new InvalidOperationException("Missing enemy XP: " + id);
            Thresholds = new int[Data.maxLevel + 1];
            for (int level = 1; level < Data.maxLevel; level++)
                Thresholds[level + 1] = checked(Thresholds[level] + Cost(level));
        }

        static bool Positive(float value) => value > 0 && !float.IsInfinity(value) && !float.IsNaN(value);
        public static int MaxLevel => Data.maxLevel;
        public static int BossXp => Data.bossXp;
        public static float HpBonus(int level) => Data.hpPerLevel * (Mathf.Clamp(level, 1, MaxLevel) - 1);
        public static int Cost(int level)
        {
            int n = Mathf.Clamp(level, 1, Data.maxLevel - 1) - 1;
            return checked(Data.costConstant + Data.costLinear * n + Data.costQuadratic * n * n);
        }
        public static int Threshold(int level) => Thresholds[Mathf.Clamp(level, 1, MaxLevel)];
        public static int LevelFor(int totalXp)
        {
            int level = 1;
            while (level < MaxLevel && totalXp >= Thresholds[level + 1]) level++;
            return level;
        }
        public static int EnemyXpFor(string kind, StageId stage, bool elite)
        {
            if (!EnemyRewards.TryGetValue(kind ?? "", out int xp)) return 0;
            int index = (int)stage - 1;
            if (index < 0 || index >= Data.stageXpMultipliers.Length) return 0;
            // Mathf.RoundToInt uses ties-to-even: do not round 16.5 down to 16.
            double result = xp * (double)Data.stageXpMultipliers[index] * (elite ? Data.eliteXpMultiplier : 1f);
            return checked((int)Math.Floor(result + .5d));
        }
    }
}
