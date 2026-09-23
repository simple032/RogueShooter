using RogueShooter.RoomCombat;

namespace RogueShooter.StageMap
{
    public sealed class StageMapNode
    {
        public string Id;
        public StageNodeRole Role;
        public RoomCombatKind CombatKind;
        public StageSlotFace? SlotFace;
        public int SlotIndex = -1;
        public bool HasNextStageConn;
        public bool IsCombatRoom;
        public int StageIndex;
    }

    public sealed class StageMapEdge
    {
        public string FromId;
        public string ToId;
        public bool IsCorridor;
    }

    /// <summary>One stage graph: START → FirstNormal → 4 slots (star via corridors). CONN only on altar.</summary>
    public sealed class StageMapGraph
    {
        public int Seed;
        public int StageIndex;
        public StageMapNode[] Nodes;
        public StageMapEdge[] Edges;
        public StageSlotFace[] SlotOrder;
        public string StartId;
        public string FirstNormalId;
        public string AltarConnId;
        public string NextStageExitId; // same as AltarConnId — exit is on altar module, not a room

        public StageMapNode Find(string id)
        {
            if (Nodes == null || string.IsNullOrEmpty(id))
                return null;
            for (int i = 0; i < Nodes.Length; i++)
            {
                if (Nodes[i] != null && Nodes[i].Id == id)
                    return Nodes[i];
            }

            return null;
        }

        public int CountCombat(RoomCombatKind kind)
        {
            int n = 0;
            if (Nodes == null)
                return 0;
            for (int i = 0; i < Nodes.Length; i++)
            {
                StageMapNode node = Nodes[i];
                if (node != null && node.IsCombatRoom && node.CombatKind == kind)
                    n++;
            }

            return n;
        }
    }
}
