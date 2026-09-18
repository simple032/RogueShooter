using System;
using RogueShooter.Balance;

namespace RogueShooter.Ai
{
    public static class MobAiChecks
    {
        public static string Run(BalanceLockData data)
        {
            float detect = data != null && data.mobDetectRadius > 0f ? data.mobDetectRadius : 5.5f;
            float mul = data != null && data.mobDisengageMul > 0f ? data.mobDisengageMul : 1.6f;
            float alert = data != null ? data.mobAlertSeconds : 0.4f;
            var brain = new MobAiBrain();
            brain.Configure(detect, mul, alert);
            float disengage = detect * mul;

            if (brain.State != MobAiState.Patrol)
                return "AI start Patrol";

            brain.Tick(detect + 2f, false, 0.1f, false);
            if (brain.State != MobAiState.Patrol)
                return "outside detect must not chase";

            brain.Tick(detect * 0.4f, false, 0.05f, false);
            if (brain.State != MobAiState.Alert)
                return "player in detect → Alert";

            float t = 0f;
            while (t < alert + 0.05f)
            {
                brain.Tick(detect * 0.4f, false, 0.05f, false);
                t += 0.05f;
            }

            if (brain.State != MobAiState.Chase)
                return "Alert → Chase while in detect";

            brain.Tick(disengage + 0.8f, false, 0.05f, false);
            if (brain.State != MobAiState.Disengage)
                return "kite beyond disengage → Disengage";

            brain.Tick(disengage + 2f, false, 0.05f, true);
            if (brain.State != MobAiState.Patrol)
                return "Disengage at home → Patrol";

            brain.Reset();
            brain.Tick(detect * 0.5f, true, 0.02f, false);
            if (brain.State != MobAiState.Alert)
                return "hit → Alert";
            t = 0f;
            while (t < alert + 0.05f)
            {
                brain.Tick(detect * 0.5f, false, 0.05f, false);
                t += 0.05f;
            }

            if (brain.State != MobAiState.Chase)
                return "hit → Alert → Chase";

            return null;
        }
    }
}
