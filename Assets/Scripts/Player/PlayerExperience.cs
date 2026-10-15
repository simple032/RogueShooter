using System.Collections.Generic;
using UnityEngine;
using RogueShooter.Balance;
using RogueShooter.Spawning;
using RogueShooter.Demo;

namespace RogueShooter.Player
{
    /// <summary>Single-run state. Only real map enemies registered by the run can pay XP.</summary>
    public sealed class PlayerExperience : MonoBehaviour
    {
        readonly Dictionary<StubEnemy,StageId> _eligible = new Dictionary<StubEnemy,StageId>();
        PlayerVitals _vitals;
        bool _bossCredited;
        float _firstKillUntil, _levelUpUntil;
        public int Level { get; private set; } = 1;
        public int TotalXp { get; private set; }
        public int Progress => Level == ProgressionRules.MaxLevel ? 0 : TotalXp - ProgressionRules.Threshold(Level);
        public int NextCost => Level == ProgressionRules.MaxLevel ? 0 : ProgressionRules.Cost(Level);
        public float Progress01 => NextCost == 0 ? 1f : Mathf.Clamp01((float)Progress / NextCost);
        public bool LevelFlash => Time.unscaledTime < _levelUpUntil;
        public string Hint => LevelFlash ? "升级 · 生命上限提高"
            : Time.unscaledTime < _firstKillUntil ? "击败怪物获得经验，升级提高生命上限\n当前血量比例保持不变" : "";
        public string PauseText => "Lv." + Level + (NextCost == 0 ? " · MAX" : " · 经验 " + Progress + " / " + NextCost)
            + "\n本局总经验 " + TotalXp + " · 生命上限 " + (_vitals != null ? _vitals.MaxHp.ToString("0.#") : "—");

        public void ResetRun(PlayerVitals vitals)
        {
            _vitals = vitals;
            Level = 1; TotalXp = 0; _eligible.Clear(); _bossCredited = false;
            _firstKillUntil = _levelUpUntil = -1f;
            _vitals.ApplyLevelBonus(ProgressionRules.HpBonus(Level));
        }
        public void RegisterEnemy(StubEnemy enemy, StageId spawnStage)
        {
            if (enemy != null && !enemy.IsDead) _eligible[enemy] = spawnStage;
        }
        public void DiscardEnemy(StubEnemy enemy)
        { if (enemy != null) _eligible.Remove(enemy); }
        public int CreditEnemy(StubEnemy enemy)
        {
            if (enemy == null || !enemy.IsDead || !_eligible.TryGetValue(enemy, out var stage)) return 0;
            // Consume eligibility even when killed by a hazard or after defeat.
            _eligible.Remove(enemy);
            if (enemy.NoKillReward || !CanGain || RunPause.IsPaused) return 0;
            int xp = ProgressionRules.EnemyXpFor(enemy.KindId, stage, enemy.Elite);
            if (xp > 0 && TotalXp == 0) _firstKillUntil = Time.unscaledTime + 8f;
            return Gain(xp, enemy.KindId);
        }
        public int CreditBossVictory()
        {
            if (_bossCredited || !CanGain) return 0;
            _bossCredited = true;
            return Gain(ProgressionRules.BossXp, "Boss胜利");
        }
        bool CanGain => _vitals != null && !_vitals.IsDown && !RunPause.RunSettled && !RunPause.CombatEnded;
        int Gain(int amount, string source)
        {
            if (amount <= 0 || !CanGain) return 0;
            int before = TotalXp;
            TotalXp = (int)System.Math.Min(int.MaxValue, (long)TotalXp + amount);
            int newLevel = ProgressionRules.LevelFor(TotalXp);
            if (newLevel != Level)
            {
                Level = newLevel;
                _vitals.ApplyLevelBonus(ProgressionRules.HpBonus(Level));
                _levelUpUntil = Time.unscaledTime + 2.4f;
            }
            GameTrace.Log("[Progression] " + source + " xp=" + (TotalXp-before) + " total=" + TotalXp
                + " level=" + Level + " hp=" + _vitals.Hp + "/" + _vitals.MaxHp);
            return TotalXp - before;
        }
    }
}
