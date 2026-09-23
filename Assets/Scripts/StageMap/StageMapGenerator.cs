using System.Collections.Generic;
using RogueShooter.RoomCombat;

namespace RogueShooter.StageMap
{
    /// <summary>
    /// GDD §6.8 generator: START (no combat) → fixed FirstNormal → 4-slot shuffle.
    /// Altar slot carries next-stage CONN (not a separate room). Topology: star from FirstNormal.
    /// </summary>
    public static class StageMapGenerator
    {
        public static StageMapGraph Generate(int seed, int stageIndex)
        {
            StageSlotFace[] order = StageMapDeck.Shuffle(seed);
            string prefix = "S" + stageIndex + "_";

            var start = new StageMapNode
            {
                Id = prefix + "START",
                Role = StageNodeRole.Start,
                CombatKind = RoomCombatKind.Corridor,
                IsCombatRoom = false,
                HasNextStageConn = false,
                StageIndex = stageIndex
            };

            var first = new StageMapNode
            {
                Id = prefix + "FirstNormal",
                Role = StageNodeRole.FirstNormal,
                CombatKind = RoomCombatKind.Normal,
                IsCombatRoom = true,
                HasNextStageConn = false,
                StageIndex = stageIndex
            };

            var nodes = new List<StageMapNode> { start, first };
            var edges = new List<StageMapEdge>
            {
                // START ↔ FirstNormal (direct). Corridor is the edge, not a room node.
                new StageMapEdge { FromId = start.Id, ToId = first.Id, IsCorridor = true }
            };

            string altarId = null;
            for (int i = 0; i < order.Length; i++)
            {
                StageSlotFace face = order[i];
                var slot = new StageMapNode
                {
                    Id = prefix + "Slot" + i + "_" + face,
                    Role = StageNodeRole.Slot,
                    SlotFace = face,
                    SlotIndex = i,
                    CombatKind = StageMapDeck.ToCombatKind(face),
                    IsCombatRoom = true,
                    HasNextStageConn = StageMapDeck.HasNextStageConn(face),
                    StageIndex = stageIndex
                };
                nodes.Add(slot);
                edges.Add(new StageMapEdge { FromId = first.Id, ToId = slot.Id, IsCorridor = true });
                if (slot.HasNextStageConn)
                    altarId = slot.Id;
            }

            return new StageMapGraph
            {
                Seed = seed,
                StageIndex = stageIndex,
                Nodes = nodes.ToArray(),
                Edges = edges.ToArray(),
                SlotOrder = order,
                StartId = start.Id,
                FirstNormalId = first.Id,
                AltarConnId = altarId,
                NextStageExitId = altarId
            };
        }

        /// <summary>Mix run seed with stage index so each stage reshuffles independently but reproducibly.</summary>
        public static int StageSeed(int runSeed, int stageIndex)
        {
            unchecked
            {
                return runSeed * 397 ^ (stageIndex + 1) * 7919;
            }
        }
    }
}
