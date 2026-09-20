using UnityEngine;
using RogueShooter.Ai;

namespace RogueShooter.Spawning
{
    /// <summary>Live stub cap. Same screen ≤ 12.</summary>
    public static class SpawnScreenCap
    {
        public const int MaxLive = 12;

        public static int LiveCount()
        {
            int n = 0;
            var all = MobFourStateAi.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && all[i].isActiveAndEnabled)
                    n++;
            }
            return n;
        }

        public static int Remaining => Mathf.Max(0, MaxLive - LiveCount());
        public static bool AtCap => Remaining <= 0;
    }
}
