using System;
using System.Collections.Generic;

namespace RogueShooter.Build
{
    /// <summary>
    /// Independent P_spawn rolls for Chest_* sites. Altars are always present (no roll).
    /// </summary>
    public static class ChestPresenceRoller
    {
        public static Dictionary<string, bool> RollChests(IList<string> chestIds, float pSpawn, Random rng)
        {
            var map = new Dictionary<string, bool>();
            if (chestIds == null)
                return map;
            if (rng == null)
                rng = new Random();
            for (int i = 0; i < chestIds.Count; i++)
            {
                string id = chestIds[i];
                if (string.IsNullOrEmpty(id))
                    continue;
                map[id] = rng.NextDouble() < pSpawn;
            }

            return map;
        }

        public static int CountPresent(Dictionary<string, bool> map)
        {
            if (map == null)
                return 0;
            int n = 0;
            foreach (var kv in map)
            {
                if (kv.Value)
                    n++;
            }

            return n;
        }
    }
}
