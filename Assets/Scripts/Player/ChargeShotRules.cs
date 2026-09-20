namespace RogueShooter.Player
{
    public enum ChargeShotKind
    {
        None,
        Weak,
        Full,
        Crit // crit2: talent weak-spot window (命中弱点), not lucky-crit naming
    }

    /// <summary>
    /// crit2 charge bow: full 1.0s, weak-spot window 76–84%, min 0.3s, normal 0.6s.
    /// Muls: weak ×0.50 / full ×1.00 / weak-spot ×2.00 / crit ×2.00.
    /// </summary>
    public static class ChargeShotRules
    {
        public const float ChargeSeconds = 1.00f;
        public const float MinChargeSeconds = 0.30f;
        public const float NormalDamageMinSeconds = 0.60f;
        public const float GreenEnter = 0.76f;
        public const float GreenExit = 0.84f;
        public const float BaseDamage = 10f;
        public const float WeakMul = 0.50f;
        public const float FullMul = 1.00f;
        public const float CritMul = 2.00f; // weak-spot / normal crit
        public const float RecoverSeconds = 0.20f;

        public static float Progress(float heldSeconds)
        {
            if (heldSeconds <= 0f)
                return 0f;
            return heldSeconds / ChargeSeconds;
        }

        public static ChargeShotKind Resolve(float heldSeconds)
        {
            if (heldSeconds < MinChargeSeconds)
                return ChargeShotKind.None;

            float p = Progress(heldSeconds);
            if (p >= GreenEnter && p < GreenExit)
                return ChargeShotKind.Crit; // 命中弱点窗
            if (heldSeconds < NormalDamageMinSeconds)
                return ChargeShotKind.Weak;
            if (p >= GreenExit)
                return ChargeShotKind.Full;
            return ChargeShotKind.Weak;
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
