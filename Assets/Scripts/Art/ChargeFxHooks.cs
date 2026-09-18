using System;

namespace RogueShooter.Art
{
    /// <summary>
    /// P0 charge-crit FX hooks (README_FX_P0). Visuals only — no progress bar.
    /// A = string/bow/tip/crit flash; B = weak reticle.
    /// </summary>
    public static class ChargeFxHooks
    {
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
