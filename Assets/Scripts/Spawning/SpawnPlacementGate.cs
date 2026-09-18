using UnityEngine;

namespace RogueShooter.Spawning
{
    /// <summary>
    /// Combines limited-vision skip-in-view with LOCK no-spawn cores.
    /// </summary>
    public static class SpawnPlacementGate
    {
        public static bool ShouldSkipSpawn(Vector3 worldPos, out string reason)
        {
            if (SpawnViewGate.ShouldSkipSpawn(worldPos))
            {
                reason = "SKIP in-view";
                return true;
            }

            if (SpawnCoreGate.TryGetBlockingCore(worldPos, out NoSpawnCore core))
            {
                reason = "SKIP no-spawn-core " + core.Kind + " " + core.CoreId;
                return true;
            }

            reason = "SPAWN out-of-view";
            return false;
        }
    }
}
