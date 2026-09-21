using System;

namespace RogueShooter.Art
{
    /// <summary>
    /// Charge FX hooks. Ring fill 0.70s (full circle = weak-spot center).
    /// A string-glow is weak secondary. No progress-bar HUD.
    /// </summary>
    public static class ChargeFxHooks
    {
        public const float ChargeSeconds = 0.70f;
        public const float GreenEnter = 0.68f;
        public const float GreenExit = 0.72f;
        public const float MidAt = 0.15f;

        public static float GreenWindowSeconds => GreenExit - GreenEnter;

        public static event Action OnChargeMid;
        public static event Action OnChargeEnterGreen;
        public static event Action OnChargeExitGreen;
        public static event Action OnCritConfirm;

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

        public static void CritConfirm()
        {
            OnCritConfirm?.Invoke();
        }
    }
}
