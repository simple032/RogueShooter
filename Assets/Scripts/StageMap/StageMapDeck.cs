using RogueShooter.RoomCombat;

namespace RogueShooter.StageMap
{
    /// <summary>GDD §6.8 fixed deck: Altar+CONN ×1, Chest ×2, Normal ×1. Never invent faces.</summary>
    public static class StageMapDeck
    {
        public static readonly StageSlotFace[] Faces =
        {
            StageSlotFace.AltarConn,
            StageSlotFace.Chest,
            StageSlotFace.Chest,
            StageSlotFace.Normal
        };

        public const int SlotCount = 4;
        public const int CombatRoomCount = 5; // FirstNormal + 4 slots
        public const int ExpectedNormal = 2;
        public const int ExpectedChest = 2;
        public const int ExpectedAltar = 1;

        public static RoomCombatKind ToCombatKind(StageSlotFace face)
        {
            switch (face)
            {
                case StageSlotFace.AltarConn: return RoomCombatKind.Altar;
                case StageSlotFace.Chest: return RoomCombatKind.SmallChest;
                case StageSlotFace.Normal: return RoomCombatKind.Normal;
                default: return RoomCombatKind.Corridor;
            }
        }

        public static bool HasNextStageConn(StageSlotFace face)
        {
            return face == StageSlotFace.AltarConn;
        }

        /// <summary>Fisher–Yates copy of Faces using seed. Same seed → same order.</summary>
        public static StageSlotFace[] Shuffle(int seed)
        {
            var arr = new StageSlotFace[Faces.Length];
            for (int i = 0; i < Faces.Length; i++)
                arr[i] = Faces[i];

            var rng = new System.Random(seed);
            for (int i = arr.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                StageSlotFace tmp = arr[i];
                arr[i] = arr[j];
                arr[j] = tmp;
            }

            return arr;
        }
    }
}
