using System;

namespace RogueShooter.Art
{
    /// <summary>
    /// Charge FX hooks. Ring fill = full charge (0.70s at 0B); the weak-spot band is drawn at
    /// ChargeShotRules.WeakSpotEnterPct–ExitPct of the ring. Values forward to ChargeShotRules.
    /// A string-glow is weak secondary. No progress-bar HUD.
    /// </summary>
    public static class ChargeFxHooks
    {
        public const float ChargeSeconds = RogueShooter.Player.ChargeShotRules.RingFillSeconds;
        public static float GreenEnter => RogueShooter.Player.ChargeShotRules.GreenEnterSeconds;
        public static float GreenExit => RogueShooter.Player.ChargeShotRules.GreenExitSeconds;
        public const float MidAt = 0.15f;

        public static float GreenWindowSeconds => GreenExit - GreenEnter;

        public static event Action OnChargeStart;
        public static event Action OnChargeMid;
        public static event Action OnChargeEnterGreen;
        public static event Action OnChargeExitGreen;
        public static event Action OnChargeFull;
        public static event Action OnCritConfirm;

        public static void ChargeStart()
        {
            OnChargeStart?.Invoke();
        }

        public static void ChargeMid()
        {
            OnChargeMid?.Invoke();
        }

        public static void ChargeEnterGreen()
        {
            OnChargeEnterGreen?.Invoke();
        }

        public static void ChargeExitGreen()
        {
            OnChargeExitGreen?.Invoke();
        }

        public static void ChargeFull()
        {
            OnChargeFull?.Invoke();
        }

        public static void CritConfirm()
        {
            OnCritConfirm?.Invoke();
        }
    }
}
