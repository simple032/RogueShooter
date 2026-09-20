using System;

namespace RogueShooter.Art
{
    /// <summary>
    /// P0 charge-crit FX hooks. Spec §7.5 numbers. Visuals only — no progress bar.
    /// A = string glow / bow edge / warm tip; B = weak reticle open/close.
    /// </summary>
    public static class ChargeFxHooks
    {
        public const float ChargeSeconds = 1.00f;
        public const float GreenEnter = 0.76f;
        public const float GreenExit = 0.84f;
        public const float MidAt = 0.15f;

        public static float GreenWindowSeconds => ChargeSeconds * (GreenExit - GreenEnter);

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
