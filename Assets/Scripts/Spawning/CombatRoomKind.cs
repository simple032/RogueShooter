using System;

namespace RogueShooter.Spawning
{
    /// <summary>Combat room class for Spec v0.5 pool routing. Not a maze generator.</summary>
    public enum CombatRoomKind
    {
        Normal = 0,
        Altar = 1,
        Chest = 2,
        LargeChest = 3
    }

    public static class CombatRoomKindUtil
    {
        public static string Label(CombatRoomKind kind)
        {
            switch (kind)
            {
                case CombatRoomKind.Altar: return "Altar";
                case CombatRoomKind.Chest: return "Chest";
                case CombatRoomKind.LargeChest: return "LargeChest";
                default: return "Normal";
            }
        }

        /// <summary>
        /// Altar and large/high chest only. Ordinary Chest and Normal combat rooms
        /// draw the normal composition table.
        /// </summary>
        public static bool PrefersEnhanced(CombatRoomKind kind)
        {
            return kind == CombatRoomKind.Altar
                || kind == CombatRoomKind.LargeChest;
        }

        /// <summary>Big/high chest: after enhanced, +1～2 same-pool units.</summary>
        public static bool AddsBigChestExtra(CombatRoomKind kind)
        {
            return kind == CombatRoomKind.LargeChest;
        }

        public static CombatRoomKind Parse(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return CombatRoomKind.Normal;
            string s = raw.Trim();
            if (s.Equals("Altar", StringComparison.OrdinalIgnoreCase)
                || s.Equals("祭坛", StringComparison.OrdinalIgnoreCase))
                return CombatRoomKind.Altar;
            if (s.Equals("LargeChest", StringComparison.OrdinalIgnoreCase)
                || s.Equals("HighChest", StringComparison.OrdinalIgnoreCase)
                || s.Equals("BigChest", StringComparison.OrdinalIgnoreCase)
                || s.Equals("大宝箱", StringComparison.OrdinalIgnoreCase))
                return CombatRoomKind.LargeChest;
            if (s.Equals("Chest", StringComparison.OrdinalIgnoreCase)
                || s.Equals("宝箱", StringComparison.OrdinalIgnoreCase))
                return CombatRoomKind.Chest;
            return CombatRoomKind.Normal;
        }

        public static CombatRoomKind Next(CombatRoomKind kind)
        {
            int n = ((int)kind + 1) % 4;
            return (CombatRoomKind)n;
        }
    }
}
