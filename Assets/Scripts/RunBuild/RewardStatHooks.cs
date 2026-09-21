using System;
using System.Collections.Generic;

namespace RogueShooter.Build
{
    /// <summary>
    /// R11–R14 stat hooks. Multiplicative unless noted (R14 pierce_back is additive).
    /// </summary>
    public static class RewardStatHooks
    {
        public const string DmgVsFullHp = "dmg_vs_fullhp";
        public const string AtkEnterRoom5s = "atk_enter_room_5s";
        public const string Lifesteal = "lifesteal";
        public const string PierceBack = "pierce_back";
        public const float EnterRoomWindowSeconds = 5f;
        public const float PierceBackCap = 1f;

        static float _enteredAt = float.NegativeInfinity;
        static bool _pendingEnter;

        public static void NotifyRoomEntered()
        {
            _pendingEnter = true;
        }

        public static void NotifyRoomEntered(float now)
        {
            _pendingEnter = false;
            _enteredAt = now;
        }

        public static void SyncClock(float now)
        {
            if (!_pendingEnter)
                return;
            _enteredAt = now;
            _pendingEnter = false;
        }

        public static void ResetRoomEnter()
        {
            _pendingEnter = false;
            _enteredAt = float.NegativeInfinity;
        }

        public static float ProductMul(IList<string> owned, string stat)
        {
            float p = 1f;
            if (owned == null || string.IsNullOrEmpty(stat))
                return p;
            for (int i = 0; i < owned.Count; i++)
            {
                if (!RewardCatalog.TryGet(owned[i], out RewardRow row))
                    continue;
                if (!string.Equals(row.Stat, stat, StringComparison.OrdinalIgnoreCase))
                    continue;
                p *= (1f + row.Value);
            }

            return p;
        }

        public static float SumAdd(IList<string> owned, string stat)
        {
            float s = 0f;
            if (owned == null || string.IsNullOrEmpty(stat))
                return s;
            for (int i = 0; i < owned.Count; i++)
            {
                if (!RewardCatalog.TryGet(owned[i], out RewardRow row))
                    continue;
                if (!string.Equals(row.Stat, stat, StringComparison.OrdinalIgnoreCase))
                    continue;
                s += row.Value;
            }

            return s;
        }

        public static bool PierceBackSaturated(IList<string> owned)
        {
            return SumAdd(owned, PierceBack) + 1e-4f >= PierceBackCap;
        }

        public static bool IsPoolExcluded(RewardRow row, IList<string> owned)
        {
            if (string.Equals(row.Stat, PierceBack, StringComparison.OrdinalIgnoreCase)
                && PierceBackSaturated(owned))
                return true;
            return false;
        }

        public static float DmgVsFullHpMul(IList<string> owned, bool targetFullHp)
        {
            if (!targetFullHp)
                return 1f;
            return ProductMul(owned, DmgVsFullHp);
        }

        public static float EnterRoomAtkMul(IList<string> owned, float now)
        {
            if (float.IsNegativeInfinity(_enteredAt) || now < _enteredAt)
                return 1f;
            if (now - _enteredAt > EnterRoomWindowSeconds)
                return 1f;
            return ProductMul(owned, AtkEnterRoom5s);
        }

        /// <summary>Heal fraction of damage dealt. One R13H → 0.15 (Π(1+v)−1).</summary>
        public static float LifestealFraction(IList<string> owned)
        {
            float p = ProductMul(owned, Lifesteal);
            return p > 1f ? p - 1f : 0f;
        }

        public static float PierceBackAdd(IList<string> owned)
        {
            return SumAdd(owned, PierceBack);
        }

        public static float ModifyOutgoing(IList<string> owned, float baseDamage, bool targetFullHp, float now)
        {
            float d = baseDamage;
            if (d < 0f)
                d = 0f;
            d *= DmgVsFullHpMul(owned, targetFullHp);
            d *= EnterRoomAtkMul(owned, now);
            return d;
        }

        public static float LifestealHeal(IList<string> owned, float damageDealt)
        {
            if (damageDealt <= 0f)
                return 0f;
            return damageDealt * LifestealFraction(owned);
        }
    }
}
