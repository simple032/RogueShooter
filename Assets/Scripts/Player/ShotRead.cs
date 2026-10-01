using System.Collections.Generic;
using UnityEngine;
using RogueShooter.Build;

namespace RogueShooter.Player
{
    /// <summary>
    /// One read of 锋矢, 疾张, and 鸿运. The shot and the charge bar both use it.
    /// Values come from RewardCatalog. No new reward numbers.
    /// </summary>
    public struct ShotRead
    {
        public float DamageProduct;
        public float ChargeSeconds;
        public float GreenEnter;
        public float GreenExit;

        public float WindowSeconds
        {
            get { return GreenExit - GreenEnter; }
        }

        public static ShotRead From(IList<string> owned)
        {
            float scale = RewardStatHooks.ProductMul(owned, "charge_time");
            if (scale < 0.05f)
                scale = 0.05f;
            float add = RewardStatHooks.SumAdd(owned, "crit_window");
            float charge = ChargeShotRules.RingFillSeconds * scale;
            float enter;
            float exit;
            bool plain = Mathf.Abs(scale - 1f) <= 0.00001f && add <= 0.00001f;
            if (plain)
            {
                charge = ChargeShotRules.RingFillSeconds;
                enter = ChargeShotRules.GreenEnterSeconds;
                exit = ChargeShotRules.GreenExitSeconds;
            }
            else
            {
                float baseHalf = (ChargeShotRules.GreenExitSeconds - ChargeShotRules.GreenEnterSeconds) * 0.5f;
                float half = baseHalf * scale + add * 0.5f;
                enter = charge - half;
                exit = charge + half;
                if (enter < 0f)
                    enter = 0f;
            }

            return new ShotRead
            {
                DamageProduct = RewardStatHooks.ProductMul(owned, "damage"),
                ChargeSeconds = charge,
                GreenEnter = enter,
                GreenExit = exit
            };
        }

        public float Progress(float heldSeconds)
        {
            if (heldSeconds <= 0f || ChargeSeconds <= 0.001f)
                return 0f;
            float p = heldSeconds / ChargeSeconds;
            if (p > 1f)
                return 1f;
            return p;
        }

        public ChargeShotKind Resolve(float heldSeconds)
        {
            if (heldSeconds < ChargeShotRules.MinChargeSeconds)
                return ChargeShotKind.None;
            if (heldSeconds >= GreenEnter && heldSeconds <= GreenExit)
                return ChargeShotKind.Crit;
            if (heldSeconds < ChargeShotRules.WeakMaxSeconds)
                return ChargeShotKind.Weak;
            return ChargeShotKind.Full;
        }
    }
}
