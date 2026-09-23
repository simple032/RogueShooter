using System.Text;
using RogueShooter.RoomCombat;

namespace RogueShooter.StageMap
{
    public static class StageMapChecks
    {
        /// <summary>Returns null on pass, else failure reason.</summary>
        public static string Run()
        {
            if (StageMapDeck.Faces.Length != StageMapDeck.SlotCount)
                return "deck length must be 4";
            int altar = 0, chest = 0, normal = 0;
            for (int i = 0; i < StageMapDeck.Faces.Length; i++)
            {
                switch (StageMapDeck.Faces[i])
                {
                    case StageSlotFace.AltarConn: altar++; break;
                    case StageSlotFace.Chest: chest++; break;
                    case StageSlotFace.Normal: normal++; break;
                    default: return "unknown deck face";
                }
            }

            if (altar != 1 || chest != 2 || normal != 1)
                return "deck must stay Altar×1 Chest×2 Normal×1";

            const int seed = 424242;
            StageSlotFace[] a = StageMapDeck.Shuffle(seed);
            StageSlotFace[] b = StageMapDeck.Shuffle(seed);
            if (a.Length != b.Length)
                return "shuffle length mismatch";
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                    return "same seed must reproduce 4-slot shuffle";
            }

            // Different seed should usually differ (not a hard fail if collision, but check not identical to fixed Faces order only)
            StageMapGraph g1 = StageMapGenerator.Generate(seed, 0);
            StageMapGraph g1b = StageMapGenerator.Generate(seed, 0);
            if (g1.SlotOrder.Length != 4 || g1b.SlotOrder.Length != 4)
                return "graph must have 4 slots";
            for (int i = 0; i < 4; i++)
            {
                if (g1.SlotOrder[i] != g1b.SlotOrder[i])
                    return "Generate same seed must match slot order";
            }

            string err = ValidateStage(g1);
            if (err != null)
                return err;

            // START is not a combat hub — FirstNormal is combat
            StageMapNode start = g1.Find(g1.StartId);
            StageMapNode first = g1.Find(g1.FirstNormalId);
            if (start == null || start.IsCombatRoom || start.Role != StageNodeRole.Start)
                return "START must be non-combat spawn (not empty combat hub)";
            if (first == null || !first.IsCombatRoom || first.CombatKind != RoomCombatKind.Normal)
                return "First room after START must be Normal combat";
            if (first.HasNextStageConn)
                return "FirstNormal must not carry CONN";

            // Direct edge START → FirstNormal
            bool linked = false;
            for (int i = 0; i < g1.Edges.Length; i++)
            {
                StageMapEdge e = g1.Edges[i];
                if ((e.FromId == g1.StartId && e.ToId == g1.FirstNormalId)
                    || (e.FromId == g1.FirstNormalId && e.ToId == g1.StartId))
                {
                    linked = true;
                    break;
                }
            }

            if (!linked)
                return "START must connect directly to FirstNormal";

            // CONN only on altar; no separate CONN room node
            if (string.IsNullOrEmpty(g1.AltarConnId) || g1.NextStageExitId != g1.AltarConnId)
                return "next-stage exit must be on altar module";
            StageMapNode altarNode = g1.Find(g1.AltarConnId);
            if (altarNode == null || altarNode.CombatKind != RoomCombatKind.Altar || !altarNode.HasNextStageConn)
                return "altar slot must own CONN";
            for (int i = 0; i < g1.Nodes.Length; i++)
            {
                StageMapNode n = g1.Nodes[i];
                if (n == null)
                    continue;
                if (n.Id.IndexOf("CONN", System.StringComparison.OrdinalIgnoreCase) >= 0
                    && n.Role != StageNodeRole.Slot)
                    return "CONN must not be a separate room node";
                if (n.HasNextStageConn && n.CombatKind != RoomCombatKind.Altar)
                    return "only altar may have next-stage CONN";
                if (n.Role == StageNodeRole.Corridor)
                    return "connectivity must not be separate corridor rooms";
            }

            // RoomCombat wave rules unchanged
            if (RoomCombatRules.ExpectedWaveCount(RoomCombatKind.Normal) != 1
                || RoomCombatRules.ExpectedWaveCount(RoomCombatKind.SmallChest) != 2
                || RoomCombatRules.ExpectedWaveCount(RoomCombatKind.Altar) != 2)
                return "RoomCombat wave table must stay §4.5";

            // Chain: stage→stage only altar CONN → next START
            StageChain chain = StageChainBuilder.Build(seed, 3);
            if (chain.Stages == null || chain.Stages.Length != 3)
                return "need 3 stages";
            if (chain.InterStageLinks == null || chain.InterStageLinks.Length != 2)
                return "need 2 inter-stage links";
            for (int s = 0; s < 3; s++)
            {
                string ve = ValidateStage(chain.Stages[s]);
                if (ve != null)
                    return "stage" + s + ": " + ve;
            }

            for (int i = 0; i < chain.InterStageLinks.Length; i++)
            {
                StageMapEdge link = chain.InterStageLinks[i];
                StageMapGraph prev = chain.Stages[i];
                StageMapGraph next = chain.Stages[i + 1];
                if (link.FromId != prev.NextStageExitId || link.FromId != prev.AltarConnId)
                    return "inter-stage from must be previous altar CONN";
                if (link.ToId != next.StartId)
                    return "inter-stage to must be next START";
                StageMapNode from = prev.Find(link.FromId);
                if (from == null || from.CombatKind != RoomCombatKind.Altar)
                    return "inter-stage source must be altar";
            }

            // Stage seeds differ → each stage validates via chain above
            return null;
        }

        static string ValidateStage(StageMapGraph g)
        {
            if (g == null)
                return "null graph";
            if (g.CountCombat(RoomCombatKind.Normal) != StageMapDeck.ExpectedNormal)
                return "Normal count must be 2";
            if (g.CountCombat(RoomCombatKind.SmallChest) != StageMapDeck.ExpectedChest)
                return "Chest count must be 2";
            if (g.CountCombat(RoomCombatKind.Altar) != StageMapDeck.ExpectedAltar)
                return "Altar count must be 1";

            int combat = 0;
            for (int i = 0; i < g.Nodes.Length; i++)
            {
                if (g.Nodes[i] != null && g.Nodes[i].IsCombatRoom)
                    combat++;
            }

            if (combat != StageMapDeck.CombatRoomCount)
                return "combat rooms must be 5";

            // Recount deck faces in slots only
            int a = 0, c = 0, n = 0;
            for (int i = 0; i < g.SlotOrder.Length; i++)
            {
                switch (g.SlotOrder[i])
                {
                    case StageSlotFace.AltarConn: a++; break;
                    case StageSlotFace.Chest: c++; break;
                    case StageSlotFace.Normal: n++; break;
                }
            }

            if (a != 1 || c != 2 || n != 1)
                return "slot order must preserve deck counts";

            return null;
        }

        public static string FormatPass(StageMapGraph sample)
        {
            var sb = new StringBuilder();
            sb.Append("ACCEPTANCE PASS StageMap §6.8 ");
            sb.Append("seed=").Append(sample != null ? sample.Seed : 0);
            sb.Append(" slots=");
            if (sample != null && sample.SlotOrder != null)
            {
                for (int i = 0; i < sample.SlotOrder.Length; i++)
                {
                    if (i > 0)
                        sb.Append(',');
                    sb.Append(sample.SlotOrder[i]);
                }
            }

            sb.Append(" N2/CH2/AL1 CONN@altar START≠hub");
            return sb.ToString();
        }
    }
}
