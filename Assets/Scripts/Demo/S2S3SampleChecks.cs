using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using RogueShooter.Maze;

namespace RogueShooter.Demo
{
    /// <summary>
    /// Data-only acceptance checks for the hand-authored S2/S3 sample layouts.
    /// Runs in the editor without Play: quotas and differences per the spec,
    /// 28x22 rooms on the 50/44 lattice with 22u edge gaps, width-8 orthogonal
    /// corridors, doorway and corridor clear widths past the 4.8 roll, the
    /// stated shortest altar runs, a chest room on every stage-3 altar route,
    /// the single mechanism/stronghold room with its fixture clear of door
    /// mouths and spawn courts, and corridors that never cut through rooms.
    /// </summary>
    public static class S2S3SampleChecks
    {
        public const float RollWidth = 4.8f;

        public static string RunAll()
        {
            var sb = new StringBuilder();
            int pass = 0;
            for (int i = 0; i < S2S3Layouts.All.Length; i++)
            {
                string id = S2S3Layouts.All[i];
                string err = Run(id);
                if (err == null)
                {
                    pass++;
                    sb.Append("[S2S3] ").Append(id).Append(" ACCEPTANCE PASS | ").Append(FormatPass(id)).Append('\n');
                }
                else
                {
                    sb.Append("[S2S3] ").Append(id).Append(" ACCEPTANCE FAIL ").Append(err).Append('\n');
                }
            }
            sb.Append("[S2S3] all=").Append(S2S3Layouts.All.Length).Append(" pass=").Append(pass);
            return sb.ToString();
        }

        public static string Run(string id)
        {
            return RunLayout(S2S3Layouts.Build(id));
        }

        public static string RunLayout(S2S3Layout layout)
        {
            string id = layout.Id;
            Stage1Maze maze = layout.ToMaze();
            int normal = 0, chest = 0, mechanism = 0, stronghold = 0, altar = 0, first = 0;
            for (int i = 0; i < layout.Rooms.Length; i++)
            {
                S2S3Room r = layout.Rooms[i];
                switch (r.Type)
                {
                    case S2S3RoomType.Normal: normal++; break;
                    case S2S3RoomType.Chest: chest++; break;
                    case S2S3RoomType.Mechanism: mechanism++; break;
                    case S2S3RoomType.Stronghold: stronghold++; break;
                    case S2S3RoomType.Altar: altar++; break;
                }
                if (r.First)
                    first++;
                if (Mathf.Abs(r.Center.x - r.Col * MazeRules.PitchX) > 0.01f
                    || Mathf.Abs(r.Center.y - r.Row * MazeRules.PitchY) > 0.01f)
                    return id + " room " + r.Id + " off the 50/44 lattice";
            }
            if (first != 1 || layout.Find("A") == null || !layout.Find("A").First)
                return id + " needs exactly one first room A";
            if (layout.Find("A").Type != S2S3RoomType.Normal)
                return id + " first room must be a normal room";
            if (altar != 1)
                return id + " altar count " + altar;
            if (layout.Phase3)
            {
                if (layout.Rooms.Length != 8 || normal != 3 || chest != 3 || stronghold != 1 || mechanism != 0)
                    return id + " quota rooms=" + layout.Rooms.Length + " N=" + normal + " C=" + chest + " S=" + stronghold + " M=" + mechanism;
                if (normal - chest != 0)
                    return id + " stage-3 normal/chest diff must be 0";
            }
            else
            {
                if (layout.Rooms.Length != 7 || normal != 3 || chest != 2 || mechanism != 1 || stronghold != 0)
                    return id + " quota rooms=" + layout.Rooms.Length + " N=" + normal + " C=" + chest + " M=" + mechanism + " S=" + stronghold;
                if (normal - chest != 1)
                    return id + " stage-2 normal/chest diff must be 1";
            }

            for (int i = 0; i < layout.Rooms.Length; i++)
            {
                S2S3Room a = layout.Rooms[i];
                for (int j = i + 1; j < layout.Rooms.Length; j++)
                {
                    S2S3Room b = layout.Rooms[j];
                    if (a.Col == b.Col && a.Row == b.Row)
                        return id + " rooms " + a.Id + "/" + b.Id + " share a lattice cell";
                }
                MazeNode node = maze.Find(a.Id);
                if (node == null || Mathf.Abs(node.Width - MazeRules.CombatWidth) > 0.01f
                    || Mathf.Abs(node.Height - MazeRules.CombatHeight) > 0.01f)
                    return id + " room " + a.Id + " not 28x22";
            }

            // Corridors: orthogonal, width 8, rims, clear widths.
            if (MazeRules.CorridorWidth < RollWidth)
                return id + " corridor width " + MazeRules.CorridorWidth + " < roll " + RollWidth;
            if (MazeRules.CorridorWidth - 2f * Stage1Maze.WalkInset < RollWidth)
                return id + " corridor clear width below roll";
            if (Stage1IsoArt.DoorGap < RollWidth)
                return id + " doorway " + Stage1IsoArt.DoorGap + " < roll " + RollWidth;
            for (int e = 0; e < layout.Edges.Length; e++)
            {
                S2S3Edge edge = layout.Edges[e];
                S2S3Room from = layout.Find(edge.FromId), to = layout.Find(edge.ToId);
                if (from == null || to == null || from == to)
                    return id + " edge " + edge.FromId + "--" + edge.ToId + " bad endpoints";
                if (!OnRim(from, edge.From) || !OnRim(to, edge.To))
                    return id + " edge " + edge.FromId + "--" + edge.ToId + " door point off the rim";
                Vector2[] pts = edge.Points;
                for (int s = 0; s < pts.Length - 1; s++)
                {
                    if (Mathf.Abs(pts[s + 1].x - pts[s].x) > 0.01f && Mathf.Abs(pts[s + 1].y - pts[s].y) > 0.01f)
                        return id + " corridor not orthogonal at " + pts[s];
                }
                // No corridor body inside a room (the mouths at both rims excepted).
                for (int s = 0; s < pts.Length - 1; s++)
                {
                    Vector2 a = pts[s], b = pts[s + 1];
                    float len = Vector2.Distance(a, b);
                    for (float t = 0.15f; t < 0.99f; t += 0.4f / Mathf.Max(1f, len))
                    {
                        Vector2 p = Vector2.Lerp(a, b, t);
                        if ((p - edge.From).sqrMagnitude < 4.4f * 4.4f || (p - edge.To).sqrMagnitude < 4.4f * 4.4f)
                            continue;
                        for (int r = 0; r < layout.Rooms.Length; r++)
                        {
                            S2S3Room room = layout.Rooms[r];
                            Vector2 cc = room.Center;
                            if (Mathf.Abs(p.x - cc.x) <= 14f - 0.35f && Mathf.Abs(p.y - cc.y) <= 11f - 0.35f)
                                return id + " corridor " + edge.FromId + "--" + edge.ToId + " cuts room " + room.Id;
                        }
                    }
                }
            }

            // Segment topology: no unintended crossings, no clashing parallel runs.
            List<S2S3Seg> segs = S2S3Layouts.Segments(layout);
            for (int i = 0; i < segs.Count; i++)
            {
                for (int j = 0; j < segs.Count; j++)
                {
                    if (i == j)
                        continue;
                    S2S3Seg a = segs[i], b = segs[j];
                    bool overlapSpan = a.Vertical != b.Vertical
                        ? (b.Line > a.Lo - 0.02f && b.Line < a.Hi + 0.02f && a.Line > b.Lo - 0.02f && a.Line < b.Hi + 0.02f)
                        : Mathf.Abs(a.Line - b.Line) < 9.05f
                          && Mathf.Min(a.Hi, b.Hi) - Mathf.Max(a.Lo, b.Lo) > -0.02f;
                    if (!overlapSpan)
                        continue;
                    if (a.Vertical != b.Vertical)
                    {
                        // Perpendicular: the shared point must be an end of one of them.
                        var meet = a.Vertical ? new Vector2(a.Line, b.Line) : new Vector2(b.Line, a.Line);
                        bool aEnds = Mathf.Abs(a.Along(meet) - a.Lo) < 0.05f || Mathf.Abs(a.Along(meet) - a.Hi) < 0.05f;
                        bool bEnds = Mathf.Abs(b.Along(meet) - b.Lo) < 0.05f || Mathf.Abs(b.Along(meet) - b.Hi) < 0.05f;
                        if (!aEnds && !bEnds)
                            return id + " unintended corridor crossing at " + meet;
                    }
                    else if (Mathf.Abs(a.Line - b.Line) > 0.02f)
                        return id + " parallel corridors clash at line " + a.Line + "/" + b.Line;
                    else
                    {
                        // Same line: the gap between runs must clear a corridor width.
                        float gap = a.Lo > b.Hi ? a.Lo - b.Hi : b.Lo > a.Hi ? b.Lo - a.Hi : -1f;
                        if (gap >= 0f && gap < MazeRules.CorridorWidth - 0.05f)
                            return id + " stacked corridors on line " + a.Line + " gap " + gap.ToString("0.0");
                    }
                }
            }

            // Reachability and the stated shortest altar run.
            bool[] reach = ReachableFrom(layout, "A");
            for (int i = 0; i < layout.Rooms.Length; i++)
                if (!reach[i])
                    return id + " room " + layout.Rooms[i].Id + " unreachable from A";
            string altarId = null;
            for (int i = 0; i < layout.Rooms.Length; i++)
                if (layout.Rooms[i].Type == S2S3RoomType.Altar)
                    altarId = layout.Rooms[i].Id;
            List<string> path;
            int hops = Shortest(layout, "A", altarId, out path);
            int wantHops = layout.Phase3 ? 6 : 5;
            if (path == null || hops != wantHops)
                return id + " shortest A->" + altarId + " hops=" + (path == null ? -1 : hops) + " want " + wantHops;
            int combat = 0;
            for (int i = 0; i < path.Count; i++)
                if (layout.Find(path[i]).Type != S2S3RoomType.Altar)
                    combat++;
            if (combat != (layout.Phase3 ? 5 : 4))
                return id + " shortest combat rooms " + combat + " want " + (layout.Phase3 ? 5 : 4);

            if (layout.Phase3 && !EveryAltarRoutePassesChest(layout, "A", altarId))
                return id + " some altar route has no chest room";

            // Fixture clearances: door mouths and the spawn court stay uncovered.
            for (int i = 0; i < layout.Rooms.Length; i++)
            {
                S2S3Room room = layout.Rooms[i];
                Vector2 c = room.Center;
                if (room.Type == S2S3RoomType.Mechanism)
                {
                    float track = S2S3PhaseDress.GateTrackY;
                    float half = S2S3PhaseDress.GateTrackHalf;
                    if (track + 0.25f > 11f - 4.6f || track - 0.25f < 3f)
                        return id + " copper strip overlaps door mouth or spawn court";
                    if (half + 0.3f > 14f - 4.6f)
                        return id + " copper strip reaches the side door mouth";
                    float slabHi = S2S3PhaseDress.GateSlabX + 1.3f;
                    if (track < 5.4f && slabHi > 14f - 4.6f)
                        return id + " copper slab parks in the side door mouth";
                }
                if (room.Type == S2S3RoomType.Stronghold)
                {
                    Vector2 sc = c + new Vector2(S2S3PhaseDress.SectorOffsetX, S2S3PhaseDress.SectorOffsetY);
                    float r = S2S3PhaseDress.SectorRadius;
                    if (Mathf.Abs(S2S3PhaseDress.SectorOffsetX) + r + 0.5f > 14f
                        || Mathf.Abs(S2S3PhaseDress.SectorOffsetY) + r + 0.5f > 11f)
                        return id + " sector disc reaches the walls";
                    if (sc.sqrMagnitude < (r + 2f) * (r + 2f))
                        return id + " sector disc covers the spawn court";
                    var doors = Stage1IsoArt.CollectDoors(maze.Find(room.Id), maze);
                    for (int d = 0; d < doors.Count; d++)
                    {
                        Vector2 dp = doors[d].Point;
                        Vector2 mouth = dp + (new Vector2(c.x, c.y) - dp).normalized * 3.2f;
                        if ((mouth - sc).sqrMagnitude < (r + 1.2f) * (r + 1.2f))
                            return id + " sector disc covers door mouth " + doors[d].Rim;
                    }
                }
            }
            return null;
        }

        public static string FormatPass(string id)
        {
            S2S3Layout layout = S2S3Layouts.Build(id);
            List<string> path;
            string altarId = null;
            for (int i = 0; i < layout.Rooms.Length; i++)
                if (layout.Rooms[i].Type == S2S3RoomType.Altar)
                    altarId = layout.Rooms[i].Id;
            int hops = Shortest(layout, "A", altarId, out path);
            var sb = new StringBuilder();
            sb.Append(layout.Phase3 ? "S3" : "S2").Append(" 28x22 pitch50/44 gap22 corridor8 rollOK");
            sb.Append(" rooms=").Append(layout.Rooms.Length);
            sb.Append(" shortest=").Append(hops).Append("房(");
            for (int i = 0; i < path.Count; i++)
            {
                if (i > 0) sb.Append('-');
                sb.Append(path[i]);
            }
            sb.Append(") ").Append(layout.PickNote);
            return sb.ToString();
        }

        static bool OnRim(S2S3Room room, Vector2 p)
        {
            float dx = Mathf.Abs(p.x - room.Center.x), dy = Mathf.Abs(p.y - room.Center.y);
            bool ew = Mathf.Abs(dx - 14f) < 0.02f && dy < 0.02f;
            bool ns = Mathf.Abs(dy - 11f) < 0.02f && dx < 0.02f;
            return ew || ns;
        }

        static bool[] ReachableFrom(S2S3Layout layout, string fromId)
        {
            var reach = new bool[layout.Rooms.Length];
            var queue = new Queue<int>();
            for (int i = 0; i < layout.Rooms.Length; i++)
            {
                if (layout.Rooms[i].Id == fromId)
                {
                    reach[i] = true;
                    queue.Enqueue(i);
                }
            }
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                var ids = NeighborIds(layout, layout.Rooms[i].Id);
                for (int k = 0; k < ids.Count; k++)
                {
                    for (int j = 0; j < layout.Rooms.Length; j++)
                    {
                        if (!reach[j] && layout.Rooms[j].Id == ids[k])
                        {
                            reach[j] = true;
                            queue.Enqueue(j);
                        }
                    }
                }
            }
            return reach;
        }

        static List<string> NeighborIds(S2S3Layout layout, string id)
        {
            var ids = new List<string>();
            for (int e = 0; e < layout.Edges.Length; e++)
            {
                if (layout.Edges[e].FromId == id)
                    ids.Add(layout.Edges[e].ToId);
                else if (layout.Edges[e].ToId == id)
                    ids.Add(layout.Edges[e].FromId);
            }
            return ids;
        }

        static int Shortest(S2S3Layout layout, string fromId, string toId, out List<string> path)
        {
            path = null;
            int n = layout.Rooms.Length;
            var prev = new int[n];
            var seen = new bool[n];
            var queue = new Queue<int>();
            for (int i = 0; i < n; i++)
            {
                prev[i] = -1;
                if (layout.Rooms[i].Id == fromId)
                {
                    seen[i] = true;
                    queue.Enqueue(i);
                }
            }
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                if (layout.Rooms[i].Id == toId)
                {
                    path = new List<string>();
                    for (int cur = i; cur >= 0; cur = prev[cur])
                        path.Add(layout.Rooms[cur].Id);
                    path.Reverse();
                    return path.Count;
                }
                var ids = NeighborIds(layout, layout.Rooms[i].Id);
                for (int k = 0; k < ids.Count; k++)
                {
                    for (int j = 0; j < n; j++)
                    {
                        if (!seen[j] && layout.Rooms[j].Id == ids[k])
                        {
                            seen[j] = true;
                            prev[j] = i;
                            queue.Enqueue(j);
                        }
                    }
                }
            }
            return -1;
        }

        static bool EveryAltarRoutePassesChest(S2S3Layout layout, string fromId, string toId)
        {
            var visited = new HashSet<string> { fromId };
            var stack = new List<string> { fromId };
            return Walk(layout, fromId, toId, visited, stack);
        }

        static bool Walk(S2S3Layout layout, string cur, string toId, HashSet<string> visited, List<string> path)
        {
            if (cur == toId)
            {
                for (int i = 0; i < path.Count; i++)
                {
                    if (path[i] != toId && layout.Find(path[i]).Type == S2S3RoomType.Chest)
                        return true;
                }
                // The altar room itself is not a chest room.
                return false;
            }
            var ids = NeighborIds(layout, cur);
            for (int k = 0; k < ids.Count; k++)
            {
                string next = ids[k];
                if (visited.Contains(next))
                    continue;
                visited.Add(next);
                path.Add(next);
                bool ok = Walk(layout, next, toId, visited, path);
                path.RemoveAt(path.Count - 1);
                visited.Remove(next);
                if (!ok)
                    return false;
            }
            return true;
        }
    }
}
