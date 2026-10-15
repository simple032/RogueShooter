using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using RogueShooter.Maze;

namespace RogueShooter.Demo
{
    /// <summary>Room flavours the S2/S3 sample round adds on top of the S1 kinds.
    /// Mechanism and stronghold rooms reuse the Normal art shell; their fixtures
    /// (copper gate / sector disc) and labels come from the sample dressing pass.
    /// Neither type counts toward the normal/chest quotas.</summary>
    public enum S2S3RoomType { Normal, Chest, Mechanism, Stronghold, Altar, Heal }

    public sealed class S2S3Room
    {
        public string Id;
        public int Col, Row;
        public S2S3RoomType Type;
        public bool First;
        public int DecorVariant;
        public bool LargeChest;
        public Vector2 WorldOffset;

        public Vector2 Center
        {
            get { return new Vector2(Col * MazeRules.PitchX, Row * MazeRules.PitchY) + WorldOffset; }
        }

        public MazeNodeKind ArtKind
        {
            get
            {
                switch (Type)
                {
                    case S2S3RoomType.Chest: return LargeChest ? MazeNodeKind.LargeChest : MazeNodeKind.Chest;
                    case S2S3RoomType.Mechanism: return MazeNodeKind.Mechanism;
                    case S2S3RoomType.Stronghold: return MazeNodeKind.Stronghold;
                    case S2S3RoomType.Altar: return MazeNodeKind.Altar;
                    case S2S3RoomType.Heal: return MazeNodeKind.Heal;
                    default: return MazeNodeKind.Normal;
                }
            }
        }
    }

    /// <summary>Door-to-door orthogonal corridor. The polyline's first point sits
    /// on the FromId room rim, the last on the ToId rim; middle points are the
    /// right-angle turns. Several edges may share trunk pieces (fork arms); the
    /// floor painter dedups cells and the segment merger below unifies walls.</summary>
    public sealed class S2S3Edge
    {
        public string FromId, ToId;
        public Vector2 From, To;
        public Vector2[] Points;
    }

    public sealed class S2S3Layout
    {
        public string Id;
        public string Title;
        public bool Phase3;
        public S2S3Room[] Rooms;
        public S2S3Edge[] Edges;
        public string PickNote;
        public int Seed = S2S3Layouts.Seed;
        public bool Production;

        public S2S3Room Find(string id)
        {
            for (int i = 0; i < Rooms.Length; i++)
                if (Rooms[i].Id == id)
                    return Rooms[i];
            return null;
        }

        public Stage1Maze ToMaze()
        {
            var nodes = new MazeNode[Rooms.Length];
            for (int i = 0; i < Rooms.Length; i++)
            {
                S2S3Room r = Rooms[i];
                nodes[i] = new MazeNode
                {
                    Id = r.Id,
                    Kind = r.ArtKind,
                    Center = new MazeVec2(r.Center.x, r.Center.y),
                    Width = MazeRules.CombatWidth,
                    Height = MazeRules.CombatHeight,
                };
            }
            var edges = new MazeEdge[Edges.Length];
            for (int i = 0; i < Edges.Length; i++)
            {
                S2S3Edge e = Edges[i];
                float len = 0f, maxSeg = 0f;
                for (int s = 0; s < e.Points.Length - 1; s++)
                {
                    float d = Vector2.Distance(e.Points[s], e.Points[s + 1]);
                    len += d;
                    if (d > maxSeg)
                        maxSeg = d;
                }
                var pts = new MazeVec2[e.Points.Length];
                for (int s = 0; s < pts.Length; s++)
                    pts[s] = new MazeVec2(e.Points[s].x, e.Points[s].y);
                edges[i] = new MazeEdge
                {
                    FromId = e.FromId,
                    ToId = e.ToId,
                    From = new MazeVec2(e.From.x, e.From.y),
                    To = new MazeVec2(e.To.x, e.To.y),
                    Width = MazeRules.CorridorWidth,
                    Length = len,
                    MaxSegment = maxSeg,
                    FoldCount = e.Points.Length - 1,
                    Points = pts,
                };
            }
            FillNeighbors(nodes, edges);
            return new Stage1Maze
            {
                Seed = Seed,
                TemplateId = Id,
                LargeChestUpgraded = Array.Exists(Rooms, r => r.LargeChest),
                Signature = Id + ":" + Seed + ":" + string.Join(",", Array.ConvertAll(Rooms, r => r.Id + "=" + r.Type + (r.LargeChest ? "+" : ""))),
                Nodes = nodes,
                Edges = edges,
            };
        }

        static void FillNeighbors(MazeNode[] nodes, MazeEdge[] edges)
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                var ids = new List<string>();
                for (int e = 0; e < edges.Length; e++)
                {
                    if (edges[e].FromId == nodes[i].Id)
                        ids.Add(edges[e].ToId);
                    else if (edges[e].ToId == nodes[i].Id)
                        ids.Add(edges[e].FromId);
                }
                nodes[i].NeighborIds = ids.ToArray();
            }
        }
    }

    public enum S2S3EndKind { Wall, Corner, JoinInto }

    /// <summary>How one corridor piece ends. Wall: butts a room rim. Corner: an
    /// L-turn, Dir says which way the partner corridor departs the shared point.
    /// JoinInto: this branch ends on another corridor's interior; Dir is the side
    /// the branch approaches from.</summary>
    public sealed class S2S3End
    {
        public S2S3EndKind Kind = S2S3EndKind.Wall;
        public char Dir;
    }

    /// <summary>A T-branch landing on a host segment's interior. At is the
    /// position along the host axis; From is the side the branch comes from.
    /// A horizontal branch into a vertical trunk also pushes its north masonry
    /// band through the trunk wall, so the trunk's cutaway gap grows by the
    /// masonry height on the north side.</summary>
    public sealed class S2S3Join
    {
        public float At;
        public char From;
        public bool BranchHorizontal;
    }

    /// <summary>One merged physical corridor strip after collinear dedup.</summary>
    public sealed class S2S3Seg
    {
        public bool Vertical;
        public float Line;
        public float Lo, Hi;
        public S2S3End LoEnd = new S2S3End();
        public S2S3End HiEnd = new S2S3End();
        public List<S2S3Join> Joins = new List<S2S3Join>();

        public float Along(Vector2 p) { return Vertical ? p.y : p.x; }
        public Vector2 PointAt(float at) { return Vertical ? new Vector2(Line, at) : new Vector2(at, Line); }
    }

    /// <summary>Hand-authored stage-2/3 sample-house layouts, wireframe positions
    /// with the written topology. Rooms sit on the 50/44 pitch lattice (28x22,
    /// edge gap 22 both axes); corridors are width-8 orthogonal polylines.
    /// Where the S3 wireframes sketch the chest G off the stronghold D, the
    /// written spec wins: M01 closes the F-G-H ring, M02 hangs G over F, so the
    /// shortest altar run keeps its stated 6 rooms / 5 combat rooms.</summary>
    public static class S2S3Layouts
    {
        public const int Seed = 42;

        public static readonly string[] All =
        {
            "S2_M01", "S2_M02", "S2_M03", "S3_M01", "S3_M02", "S3_M03",
        };

        static readonly Dictionary<string, S2S3Layout> Cache = new Dictionary<string, S2S3Layout>();

        public static S2S3Layout Build(string id)
        {
            S2S3Layout cached;
            if (Cache.TryGetValue(id, out cached))
                return cached;
            S2S3Layout layout = Author(id);
            if (layout == null)
                throw new ArgumentException("Unknown S2/S3 layout: " + id);
            Cache[id] = layout;
            return layout;
        }

        public static S2S3Layout Fresh(string id)
        {
            return Author(id) ?? throw new ArgumentException("Unknown S2/S3 layout: " + id);
        }

        // ---- authoring ----

        static S2S3Room R(string id, int col, int row, S2S3RoomType type)
        {
            return new S2S3Room { Id = id, Col = col, Row = row, Type = type, First = id == "A" };
        }

        static S2S3Edge E(string a, string b, params float[] xy)
        {
            var pts = new Vector2[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++)
                pts[i] = new Vector2(xy[2 * i], xy[2 * i + 1]);
            return new S2S3Edge { FromId = a, ToId = b, From = pts[0], To = pts[pts.Length - 1], Points = pts };
        }

        static S2S3Layout Author(string id)
        {
            switch (id)
            {
                case "S2_M01": return M01();
                case "S2_M02": return M02();
                case "S2_M03": return M03();
                case "S3_M01": return M04();
                case "S3_M02": return M05();
                case "S3_M03": return M06();
                default: return null;
            }
        }

        // S2_M01 分叉回环: A-B, B forks to C/D, both arms rejoin at F,
        // F feeds E and altar G off one trunk above.
        static S2S3Layout M01()
        {
            return new S2S3Layout
            {
                Id = "S2_M01",
                Title = "阶段二 M01 分叉回环",
                Phase3 = false,
                PickNote = "C=宝箱 D=铜闸机关（两臂可对调），E=宝箱；大宝箱候选=E 与 C/D 中抽成箱的那间",
                Rooms = new[]
                {
                    R("A", 0, 0, S2S3RoomType.Normal),
                    R("B", 0, 1, S2S3RoomType.Normal),
                    R("C", -1, 2, S2S3RoomType.Chest),
                    R("D", 1, 2, S2S3RoomType.Mechanism),
                    R("F", 0, 3, S2S3RoomType.Normal),
                    R("E", -1, 4, S2S3RoomType.Chest),
                    R("G", 1, 4, S2S3RoomType.Altar),
                },
                Edges = new[]
                {
                    E("A", "B", 0, 11, 0, 33),
                    E("B", "C", 0, 55, 0, 88, -36, 88),
                    E("B", "D", 0, 55, 0, 88, 36, 88),
                    E("C", "F", -50, 99, -50, 132, -14, 132),
                    E("D", "F", 50, 99, 50, 132, 14, 132),
                    E("F", "E", 0, 143, 0, 176, -36, 176),
                    E("F", "G", 0, 143, 0, 176, 36, 176),
                },
            };
        }

        // S2_M02 深叉长环: main chain A-B-C-D-G, long arm C-E-F-G closing the
        // loop through the D/F trunk with the G branch.
        static S2S3Layout M02()
        {
            return new S2S3Layout
            {
                Id = "S2_M02",
                Title = "阶段二 M02 深叉长环",
                Phase3 = false,
                PickNote = "五格抽签落位：E,F=宝箱，C=铜闸机关，B,D=普通",
                Rooms = new[]
                {
                    R("A", 0, 0, S2S3RoomType.Normal),
                    R("B", 1, 0, S2S3RoomType.Normal),
                    R("C", 2, 0, S2S3RoomType.Mechanism),
                    R("D", 3, 0, S2S3RoomType.Normal),
                    R("E", 2, 1, S2S3RoomType.Chest),
                    R("F", 3, 2, S2S3RoomType.Chest),
                    R("G", 4, 1, S2S3RoomType.Altar),
                },
                Edges = new[]
                {
                    E("A", "B", 14, 0, 36, 0),
                    E("B", "C", 64, 0, 86, 0),
                    E("C", "D", 114, 0, 136, 0),
                    E("C", "E", 100, 11, 100, 33),
                    E("D", "G", 150, 11, 150, 44, 186, 44),
                    E("E", "F", 100, 55, 100, 88, 136, 88),
                    E("F", "G", 150, 77, 150, 44, 186, 44),
                },
            };
        }

        // S2_M03 折线支库: main chain A-B-C-D-G with dead-end chests E over B
        // and F over D.
        static S2S3Layout M03()
        {
            return new S2S3Layout
            {
                Id = "S2_M03",
                Title = "阶段二 M03 折线支库",
                Phase3 = false,
                PickNote = "E,F=宝箱；D=铜闸机关；B,C=普通",
                Rooms = new[]
                {
                    R("A", 0, 0, S2S3RoomType.Normal),
                    R("B", 1, 0, S2S3RoomType.Normal),
                    R("C", 2, 0, S2S3RoomType.Normal),
                    R("D", 3, 0, S2S3RoomType.Mechanism),
                    R("E", 1, 1, S2S3RoomType.Chest),
                    R("F", 3, 1, S2S3RoomType.Chest),
                    R("G", 4, 0, S2S3RoomType.Altar),
                },
                Edges = new[]
                {
                    E("A", "B", 14, 0, 36, 0),
                    E("B", "C", 64, 0, 86, 0),
                    E("C", "D", 114, 0, 136, 0),
                    E("D", "G", 164, 0, 186, 0),
                    E("B", "E", 50, 11, 50, 33),
                    E("D", "F", 150, 11, 150, 33),
                },
            };
        }

        // S3_M01 双环内殿: wings C and E converge on stronghold D, then D-F
        // feeds altar H while F-G-H closes the second ring.
        static S2S3Layout M04()
        {
            return new S2S3Layout
            {
                Id = "S3_M01",
                Title = "阶段三 M01 双环内殿",
                Phase3 = true,
                PickNote = "C,E,G=宝箱（大宝箱候选），B,F=普通，D=阵地（断仪扇区），H=大祭坛",
                Rooms = new[]
                {
                    R("A", 0, 0, S2S3RoomType.Normal),
                    R("B", 1, 0, S2S3RoomType.Normal),
                    R("C", 0, 1, S2S3RoomType.Chest),
                    R("E", 2, 1, S2S3RoomType.Chest),
                    R("D", 1, 2, S2S3RoomType.Stronghold),
                    R("G", 3, 2, S2S3RoomType.Chest),
                    R("F", 2, 3, S2S3RoomType.Normal),
                    R("H", 3, 4, S2S3RoomType.Altar),
                },
                Edges = new[]
                {
                    E("A", "B", 14, 0, 36, 0),
                    E("B", "C", 50, 11, 50, 44, 14, 44),
                    E("B", "E", 50, 11, 50, 44, 86, 44),
                    E("C", "D", 0, 55, 0, 88, 36, 88),
                    E("E", "D", 100, 55, 100, 88, 64, 88),
                    E("D", "F", 50, 99, 50, 132, 86, 132),
                    E("F", "G", 114, 132, 150, 132, 150, 99),
                    E("G", "H", 150, 99, 150, 165),
                    E("F", "H", 100, 143, 100, 176, 136, 176),
                },
            };
        }

        // S3_M02 单环侧殿: same front ring as M01 into stronghold D, then
        // D-F-H with the dead-end chest G hanging over F.
        static S2S3Layout M05()
        {
            return new S2S3Layout
            {
                Id = "S3_M02",
                Title = "阶段三 M02 单环侧殿",
                Phase3 = true,
                PickNote = "四格抽签落位：E,F=宝箱，B,C=普通；G=宝箱（大宝箱候选），D=阵地（断仪扇区）",
                Rooms = new[]
                {
                    R("A", 0, 0, S2S3RoomType.Normal),
                    R("B", 1, 0, S2S3RoomType.Normal),
                    R("C", 0, 1, S2S3RoomType.Normal),
                    R("E", 2, 1, S2S3RoomType.Chest),
                    R("D", 1, 2, S2S3RoomType.Stronghold),
                    R("F", 2, 3, S2S3RoomType.Chest),
                    R("G", 2, 4, S2S3RoomType.Chest),
                    R("H", 3, 4, S2S3RoomType.Altar),
                },
                Edges = new[]
                {
                    E("A", "B", 14, 0, 36, 0),
                    E("B", "C", 50, 11, 50, 44, 14, 44),
                    E("B", "E", 50, 11, 50, 44, 86, 44),
                    E("C", "D", 0, 55, 0, 88, 36, 88),
                    E("E", "D", 100, 55, 100, 88, 64, 88),
                    E("D", "F", 50, 99, 50, 132, 86, 132),
                    E("F", "G", 100, 143, 100, 165),
                    E("F", "H", 114, 132, 150, 132, 150, 165),
                },
            };
        }

        // S3_M03 纵深折廊: deep chain A-B-C-D-F-H, dead-end chests E over B
        // and G over F; the third chest C sits on the chain itself.
        static S2S3Layout M06()
        {
            return new S2S3Layout
            {
                Id = "S3_M03",
                Title = "阶段三 M03 纵深折廊",
                Phase3 = true,
                PickNote = "E,G=宝箱，第三箱抽在 C；B,F=普通，D=阵地（断仪扇区）",
                Rooms = new[]
                {
                    R("A", 0, 0, S2S3RoomType.Normal),
                    R("B", 1, 0, S2S3RoomType.Normal),
                    R("C", 2, 0, S2S3RoomType.Chest),
                    R("D", 3, 0, S2S3RoomType.Stronghold),
                    R("F", 4, 0, S2S3RoomType.Normal),
                    R("H", 5, 0, S2S3RoomType.Altar),
                    R("E", 1, 1, S2S3RoomType.Chest),
                    R("G", 4, 1, S2S3RoomType.Chest),
                },
                Edges = new[]
                {
                    E("A", "B", 14, 0, 36, 0),
                    E("B", "C", 64, 0, 86, 0),
                    E("C", "D", 114, 0, 136, 0),
                    E("D", "F", 164, 0, 186, 0),
                    E("F", "H", 214, 0, 236, 0),
                    E("B", "E", 50, 11, 50, 33),
                    E("F", "G", 200, 11, 200, 33),
                },
            };
        }

        // ---- corridor segmentation: merge collinear pieces, derive corners and T-joins ----

        struct Piece
        {
            public bool Vertical;
            public float Line;
            public float Lo, Hi;
        }

        public static List<S2S3Seg> Segments(S2S3Layout layout)
        {
            var pieces = new List<Piece>();
            for (int e = 0; e < layout.Edges.Length; e++)
            {
                Vector2[] pts = layout.Edges[e].Points;
                for (int s = 0; s < pts.Length - 1; s++)
                {
                    Vector2 p = pts[s], q = pts[s + 1];
                    if (Mathf.Abs(q.x - p.x) < 0.01f)
                        pieces.Add(new Piece { Vertical = true, Line = p.x, Lo = Mathf.Min(p.y, q.y), Hi = Mathf.Max(p.y, q.y) });
                    else if (Mathf.Abs(q.y - p.y) < 0.01f)
                        pieces.Add(new Piece { Vertical = false, Line = p.y, Lo = Mathf.Min(p.x, q.x), Hi = Mathf.Max(p.x, q.x) });
                    else
                        throw new InvalidOperationException("S2S3 corridor must stay orthogonal: " + layout.Id + " " + p + "->" + q);
                }
            }

            var segs = new List<S2S3Seg>();
            MergeInto(segs, pieces, true);
            MergeInto(segs, pieces, false);

            MarkEndsAndJoins(layout, segs, pieces);
            return segs;
        }

        static void MergeInto(List<S2S3Seg> segs, List<Piece> pieces, bool vertical)
        {
            var lines = new Dictionary<int, List<Piece>>();
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i].Vertical != vertical)
                    continue;
                int key = Mathf.RoundToInt(pieces[i].Line * 100f);
                List<Piece> bucket;
                if (!lines.TryGetValue(key, out bucket))
                {
                    bucket = new List<Piece>();
                    lines[key] = bucket;
                }
                bucket.Add(pieces[i]);
            }
            foreach (KeyValuePair<int, List<Piece>> pair in lines)
            {
                List<Piece> bucket = pair.Value;
                bucket.Sort((a, b) => a.Lo.CompareTo(b.Lo));
                float lo = bucket[0].Lo, hi = bucket[0].Hi;
                for (int i = 1; i < bucket.Count; i++)
                {
                    if (bucket[i].Lo <= hi + 0.02f)
                    {
                        if (bucket[i].Hi > hi)
                            hi = bucket[i].Hi;
                    }
                    else
                    {
                        segs.Add(new S2S3Seg { Vertical = vertical, Line = bucket[0].Line, Lo = lo, Hi = hi });
                        lo = bucket[i].Lo;
                        hi = bucket[i].Hi;
                    }
                }
                segs.Add(new S2S3Seg { Vertical = vertical, Line = bucket[0].Line, Lo = lo, Hi = hi });
            }
        }

        static void MarkEndsAndJoins(S2S3Layout layout, List<S2S3Seg> segs, List<Piece> pieces)
        {
            // Every polyline point that lands strictly inside another merged
            // segment is a T-join on that host; the non-collinear piece ending
            // there is the branch.
            var points = new List<Vector2>();
            for (int e = 0; e < layout.Edges.Length; e++)
            {
                Vector2[] pts = layout.Edges[e].Points;
                for (int s = 0; s < pts.Length; s++)
                {
                    bool dup = false;
                    for (int i = 0; i < points.Count; i++)
                        if ((points[i] - pts[s]).sqrMagnitude < 0.0004f) { dup = true; break; }
                    if (!dup)
                        points.Add(pts[s]);
                }
            }

            for (int p = 0; p < points.Count; p++)
            {
                Vector2 pt = points[p];
                for (int s = 0; s < segs.Count; s++)
                {
                    S2S3Seg host = segs[s];
                    if (!OnLine(host, pt))
                        continue;
                    float at = host.Along(pt);
                    if (at <= host.Lo + 0.02f || at >= host.Hi - 0.02f)
                        continue;
                    // pt is interior to the host: find the branch piece(s) at pt.
                    for (int k = 0; k < pieces.Count; k++)
                    {
                        Piece piece = pieces[k];
                        if (piece.Vertical == host.Vertical)
                            continue; // collinear with the host, already merged in
                        if (!PieceTouches(piece, pt))
                            continue;
                        char from = BranchFrom(piece, pt);
                        bool horizontalBranch = !piece.Vertical;
                        AddJoin(host, at, from, horizontalBranch);
                        S2S3Seg branch = SegOfPiece(segs, piece);
                        MarkBranchEnd(branch, pt, from);
                    }
                }
            }

            // Remaining shared endpoints are plain L-corners.
            for (int s = 0; s < segs.Count; s++)
            {
                S2S3Seg a = segs[s];
                TryCorner(a, a.LoEnd, a.PointAt(a.Lo), segs, s);
                TryCorner(a, a.HiEnd, a.PointAt(a.Hi), segs, s);
            }
        }

        static void AddJoin(S2S3Seg host, float at, char from, bool horizontalBranch)
        {
            for (int i = 0; i < host.Joins.Count; i++)
                if (host.Joins[i].At > at - 0.02f && host.Joins[i].At < at + 0.02f && host.Joins[i].From == from)
                    return;
            host.Joins.Add(new S2S3Join { At = at, From = from, BranchHorizontal = horizontalBranch });
        }

        static void MarkBranchEnd(S2S3Seg branch, Vector2 pt, char from)
        {
            float at = branch.Along(pt);
            S2S3End end = at <= branch.Lo + 0.02f ? branch.LoEnd : branch.HiEnd;
            // Fork arms reuse the same trunk piece, so one doorway can be reached
            // from several edges: claiming it twice with the same side is fine.
            if (end.Kind == S2S3EndKind.JoinInto && end.Dir == from)
                return;
            if (end.Kind != S2S3EndKind.Wall)
                throw new InvalidOperationException("S2S3 branch end already claimed at " + pt);
            end.Kind = S2S3EndKind.JoinInto;
            end.Dir = from;
        }

        static void TryCorner(S2S3Seg a, S2S3End end, Vector2 pt, List<S2S3Seg> segs, int self)
        {
            if (end.Kind != S2S3EndKind.Wall)
                return;
            for (int i = 0; i < segs.Count; i++)
            {
                if (i == self)
                    continue;
                S2S3Seg b = segs[i];
                if (b.Vertical == a.Vertical)
                    continue;
                S2S3End otherEnd = null;
                if ((b.PointAt(b.Lo) - pt).sqrMagnitude < 0.0004f)
                    otherEnd = b.LoEnd;
                else if ((b.PointAt(b.Hi) - pt).sqrMagnitude < 0.0004f)
                    otherEnd = b.HiEnd;
                if (otherEnd == null || otherEnd.Kind == S2S3EndKind.JoinInto)
                    continue;
                end.Kind = S2S3EndKind.Corner;
                end.Dir = Departure(b, pt);
                if (otherEnd.Kind == S2S3EndKind.Wall)
                {
                    otherEnd.Kind = S2S3EndKind.Corner;
                    otherEnd.Dir = Departure(a, pt);
                }
                return;
            }
        }

        static char Departure(S2S3Seg seg, Vector2 pt)
        {
            // Direction from the shared point INTO the segment's span.
            if (seg.Vertical)
                return pt.y <= seg.Lo + 0.02f ? 'N' : 'S';
            return pt.x <= seg.Lo + 0.02f ? 'E' : 'W';
        }

        static bool OnLine(S2S3Seg seg, Vector2 pt)
        {
            return Mathf.Abs((seg.Vertical ? pt.x : pt.y) - seg.Line) < 0.02f;
        }

        static bool PieceTouches(Piece piece, Vector2 pt)
        {
            if (piece.Vertical)
                return Mathf.Abs(piece.Line - pt.x) < 0.02f && pt.y >= piece.Lo - 0.02f && pt.y <= piece.Hi + 0.02f;
            return Mathf.Abs(piece.Line - pt.y) < 0.02f && pt.x >= piece.Lo - 0.02f && pt.x <= piece.Hi + 0.02f;
        }

        static char BranchFrom(Piece piece, Vector2 pt)
        {
            // Which side of the host the branch interior lies on.
            if (piece.Vertical)
            {
                float mid = (piece.Lo + piece.Hi) * 0.5f;
                return mid < pt.y ? 'S' : 'N';
            }
            float midX = (piece.Lo + piece.Hi) * 0.5f;
            return midX < pt.x ? 'W' : 'E';
        }

        static S2S3Seg SegOfPiece(List<S2S3Seg> segs, Piece piece)
        {
            for (int i = 0; i < segs.Count; i++)
            {
                S2S3Seg s = segs[i];
                if (s.Vertical == piece.Vertical
                    && Mathf.Abs(s.Line - piece.Line) < 0.02f
                    && piece.Lo >= s.Lo - 0.02f && piece.Hi <= s.Hi + 0.02f)
                    return s;
            }
            throw new InvalidOperationException("Merged segment lost for piece " + piece.Line);
        }
    }
}
