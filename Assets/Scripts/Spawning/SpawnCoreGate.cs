using System.Collections.Generic;
using UnityEngine;

namespace RogueShooter.Spawning
{
    public static class SpawnCoreGate
    {
        static readonly List<NoSpawnCore> Cores = new List<NoSpawnCore>();

        public static void Register(NoSpawnCore core)
        {
            if (core != null && !Cores.Contains(core))
                Cores.Add(core);
        }

        public static void Unregister(NoSpawnCore core)
        {
            Cores.Remove(core);
        }

        public static bool TryGetBlockingCore(Vector3 worldPos, out NoSpawnCore core)
        {
            for (int i = Cores.Count - 1; i >= 0; i--)
            {
                NoSpawnCore candidate = Cores[i];
                if (candidate == null)
                {
                    Cores.RemoveAt(i);
                    continue;
                }

                float r = candidate.Radius;
                if ((candidate.WorldPosition - worldPos).sqrMagnitude <= r * r)
                {
                    core = candidate;
                    return true;
                }
            }

            core = null;
            return false;
        }

        public static bool IsInsideNoSpawnCore(Vector3 worldPos)
        {
            return TryGetBlockingCore(worldPos, out _);
        }
    }
}
