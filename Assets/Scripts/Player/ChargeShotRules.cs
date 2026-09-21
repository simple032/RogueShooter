namespace RogueShooter.Player
{
    public enum ChargeShotKind
    {
        None,
        Weak,
        Full,
        Crit // talent weak-spot window (命中弱点)
    }

    /// <summary>
    /// Charge bow (制作人 retune): ring full at 0.70s; recover 0.2s after fire.
    /// Fire if held over 0.2s; weak if held under 0.4s (x0.50); weak-spot 0.68-0.72s.
    /// </summary>
    public static class ChargeShotRules
    {
        public const float RingFillSeconds = 0.70f;
        public const float ChargeSeconds = RingFillSeconds;
        public const float MinChargeSeconds = 0.20f;
        public const float WeakMaxSeconds = 0.40f;
        public const float GreenEnterSeconds = 0.68f;
        public const float GreenExitSeconds = 0.72f;
        public const float GreenEnter = GreenEnterSeconds;
        public const float GreenExit = GreenExitSeconds;
        public const float BaseDamage = 10f;
        public const float WeakMul = 0.50f;
        public const float FullMul = 1.00f;
        public const float CritMul = 2.00f;
        public const float RecoverSeconds = 0.20f;
        public const float WeakSpotStaggerSeconds = 0.50f;

        public static float Progress(float heldSeconds)
        {
            if (heldSeconds <= 0f)
                return 0f;
            float p = heldSeconds / RingFillSeconds;
            if (p > 1f)
                return 1f;
            return p;
        }

        public static ChargeShotKind Resolve(float heldSeconds)
        {
            if (heldSeconds <= MinChargeSeconds)
                return ChargeShotKind.None;
            if (heldSeconds >= GreenEnterSeconds && heldSeconds <= GreenExitSeconds)
                return ChargeShotKind.Crit;
            if (heldSeconds < WeakMaxSeconds)
                return ChargeShotKind.Weak;
            return ChargeShotKind.Full;
        }

        public static float Damage(ChargeShotKind kind)
        {
            switch (kind)
            {
                case ChargeShotKind.Weak: return BaseDamage * WeakMul;
                case ChargeShotKind.Full: return BaseDamage * FullMul;
                case ChargeShotKind.Crit: return BaseDamage * CritMul;
                default: return 0f;
            }
        }

        /// <summary>Apply loadout damage product on top of shot mul.</summary>
        public static float DamageWithBuild(ChargeShotKind kind, float damageProduct)
        {
            float baseShot = Damage(kind);
            if (damageProduct < 0.01f)
                damageProduct = 1f;
            return baseShot * damageProduct;
        }
    }
}
