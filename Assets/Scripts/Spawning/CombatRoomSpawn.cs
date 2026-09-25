using System;
using System.Text;
using RogueShooter.Ai;
using RogueShooter.Combat;
using RogueShooter.Layout;
using RogueShooter.Maze;

namespace RogueShooter.Spawning
{
    public struct SpawnAvoid
    {
        public float X;
        public float Y;
        /// <summary>HOOKS NoSpawnCore radius (chest 2 / altar 2.5).</summary>
        public float Radius;
        /// <summary>Collider AABB half-x including mob body pad.</summary>
        public float Hx;
        /// <summary>Collider AABB half-y including mob body pad.</summary>
        public float Hy;
        public string Tag;

        public SpawnAvoid(float x, float y, float radius, string tag)
            : this(x, y, radius, radius, radius, tag)
        {
        }

        public SpawnAvoid(float x, float y, float radius, float hx, float hy, string tag)
        {
            X = x;
            Y = y;
            Radius = radius < 0.01f ? 0.01f : radius;
            Hx = hx < 0.01f ? 0.01f : hx;
            Hy = hy < 0.01f ? 0.01f : hy;
            Tag = tag ?? "";
        }
    }

    public struct SpawnSpot
    {
        public float X;
        public float Y;
        public float DistPlayer;
        public string KindId;
        public bool Ranged;
    }

    public struct CombatRoomSpawnTrial
    {
        public bool Ranged;
        public float X;
        public float Y;
        public float DistPlayer;
        public float DistCenter;
        public int AvoidHit;
        public int MinPlayerHit;
        public int EvenRingHit;
    }

    public struct CombatRoomSpawnStats
    {
        public int MeleeN;
        public int RangedN;
        public float MeleeMean;
        public float RangedMean;
        public float MeleeMin;
        public float RangedMin;
        public float MeleeP10;
        public float MeleeP50;
        public float MeleeP90;
        public float RangedP10;
        public float RangedP50;
        public float RangedP90;
        public float CenterSpread;
        public int AvoidHits;
        public int MinPlayerHits;
        public int EvenRingHits;
        public float MeanGap;
        public CombatRoomSpawnTrial[] Trials;
    }

    /// <summary>
    /// Combat-room 落点. Replaces SpawnCluster even-ring (r=0.85).
    /// Room-area random; melee closer / ranged farther vs player; min player dist;
    /// chest/altar collider AABB + HOOKS cores r≈2 / 2.5. Numbers reused, not a new balance table.
    /// </summary>
    public static class CombatRoomSpawn
    {
        public const int PoolSize = 48;
        public const float NearFrac = 0.40f;
        public const float FarFrac = 0.40f;
        public const int SampleTrials = 40;

        /// <summary>HOOKS chest no-spawn r≈2. Also min distance to player.</summary>
        public const float ChestClearance = 2f;

        /// <summary>LockSiteCatalog altar NoSpawnRadius.</summary>
        public const float AltarClearance = 2.5f;

        public static float RoomInset => CollisionRules.WallThickness + CollisionRules.MobHalfX;

        public static float MinPlayerDist => ChestClearance;

        public static float PackSep => CollisionRules.MobHalfX * 2f;

        public static float ClearanceForKind(SiteKind kind)
        {
            if (kind == SiteKind.Altar)
                return AltarClearance;
            if (kind == SiteKind.Chest)
                return ChestClearance;
            return 0f;
        }

        public static SpawnAvoid FromSite(SiteKind kind, float x, float y)
        {
            float clear = ClearanceForKind(kind);
            float pad = clear + CollisionRules.MobHalfX;
            return new SpawnAvoid(x, y, clear, pad, pad, kind.ToString());
        }

        public static bool IsRanged(string kindId)
        {
            return EnemyKindCatalog.ForKind(kindId).RangedOrb;
        }

        /// <summary>
        /// True if a mob origin would overlap the site BoxCollider2D or sit inside the
        /// HOOKS NoSpawnCore (both padded by mob body half).
        /// </summary>
        public static bool HitsVolume(float x, float y, SpawnAvoid[] avoids)
        {
            if (avoids == null)
                return false;
            float body = CollisionRules.MobHalfX;
            for (int i = 0; i < avoids.Length; i++)
            {
                float dx = x - avoids[i].X;
                float dy = y - avoids[i].Y;
                if (dx < 0f) dx = -dx;
                if (dy < 0f) dy = -dy;
                if (dx < avoids[i].Hx && dy < avoids[i].Hy)
                    return true;
                float core = avoids[i].Radius + body;
                if (dx * dx + dy * dy < core * core)
                    return true;
            }

            return false;
        }

        /// <summary>Static probes on chest/altar collider AABBs. Null = pass.</summary>
        public static string ProbeAvoidVolumes()
        {
            SpawnAvoid chest = FromSite(SiteKind.Chest, 0f, 0f);
            SpawnAvoid altar = FromSite(SiteKind.Altar, 12f, 0f);
            var arr = new[] { chest, altar };
            if (!HitsVolume(0f, 0f, arr))
                return "chest origin must block";
            if (!HitsVolume(ChestClearance, 0f, arr))
                return "chest AABB+body must block at clearance";
            if (HitsVolume(ChestClearance + CollisionRules.MobHalfX + 0.05f, 0f, arr))
                return "chest avoid leaked past collider+body";
            if (!HitsVolume(12f, 0f, arr))
                return "altar origin must block";
            if (!HitsVolume(12f + AltarClearance, 0f, arr))
                return "altar AABB+body must block at clearance";
            if (HitsVolume(12f + AltarClearance + CollisionRules.MobHalfX + 0.05f, 0f, arr))
                return "altar avoid leaked past collider+body";
            if (HitsVolume(6f, 0f, arr))
                return "gap between chest and altar must stay spawnable";
            return null;
        }

        public static SpawnSpot[] PlaceWave(
            MazeNode room,
            float playerX,
            float playerY,
            string[] kindIds,
            SpawnAvoid[] avoids,
            Random rng)
        {
            int n = kindIds != null ? kindIds.Length : 0;
            var spots = new SpawnSpot[n];
            if (n == 0 || room == null)
                return spots;

            var occX = new float[n];
            var occY = new float[n];
            int occN = 0;
            for (int i = 0; i < n; i++)
            {
                float x, y;
                PlaceOne(
                    room.Center.X, room.Center.Y, room.Width, room.Height,
                    playerX, playerY, IsRanged(kindIds[i]),
                    avoids, occX, occY, occN, rng, out x, out y);
                float dx = x - playerX;
                float dy = y - playerY;
                spots[i] = new SpawnSpot
                {
                    X = x,
                    Y = y,
                    DistPlayer = (float)Math.Sqrt(dx * dx + dy * dy),
                    KindId = kindIds[i],
                    Ranged = IsRanged(kindIds[i])
                };
                occX[occN] = x;
                occY[occN] = y;
                occN++;
            }

            return spots;
        }

        public static bool PlaceOne(
            float cx, float cy, float w, float h,
            float playerX, float playerY, bool ranged,
            SpawnAvoid[] avoids,
            float[] occX, float[] occY, int occN,
            Random rng,
            out float x, out float y)
        {
            if (rng == null)
                rng = new Random(1);

            float[] px = new float[PoolSize];
            float[] py = new float[PoolSize];
            float[] dist = new float[PoolSize];
            int filled = FillPool(
                cx, cy, w, h, playerX, playerY, avoids, occX, occY, occN, rng,
                true, true, px, py, dist);

            if (filled < 1)
            {
                // Strict: never fall back to a spot closer than MinPlayerDist or on top of another mob.
                float sx, sy;
                ClampToRoom(cx, cy, w, h, playerX + MinPlayerDist, playerY, out sx, out sy);
                return FindFreeNear(cx, cy, w, h, sx, sy, playerX, playerY, avoids, occX, occY, occN, out x, out y);
            }

            SortByDist(px, py, dist, filled);
            int lo, hi;
            Band(filled, ranged, out lo, out hi);
            int pick = lo;
            if (hi > lo)
                pick = lo + rng.Next(hi - lo);
            x = px[pick];
            y = py[pick];
            return true;
        }

        /// <summary>Spot keeps MinPlayerDist from the player and PackSep from every occupied spot.</summary>
        public static bool SpotOk(float x, float y, float playerX, float playerY, float[] occX, float[] occY, int occN)
        {
            return Dist(x, y, playerX, playerY) >= MinPlayerDist && !TooClose(x, y, occX, occY, occN);
        }

        /// <summary>
        /// Spiral search (0.25u rings, 16 angles, up to the room size) around (sx,sy) for a spot inside
        /// the room inset that passes <see cref="SpotOk"/> and the site volumes. False = none found
        /// (x,y = best effort farthest-from-player candidate).
        /// </summary>
        public static bool FindFreeNear(
            float cx, float cy, float w, float h, float sx, float sy,
            float playerX, float playerY, SpawnAvoid[] avoids,
            float[] occX, float[] occY, int occN, out float x, out float y)
        {
            float maxR = (w > h ? w : h);
            float bestD = -1f;
            x = sx;
            y = sy;
            for (float r = 0f; r <= maxR; r += 0.25f)
            {
                int steps = r < 0.01f ? 1 : 16;
                for (int k = 0; k < steps; k++)
                {
                    double a = k * (Math.PI * 2.0 / steps);
                    float tx, ty;
                    ClampToRoom(cx, cy, w, h, sx + (float)Math.Cos(a) * r, sy + (float)Math.Sin(a) * r, out tx, out ty);
                    if (HitsVolume(tx, ty, avoids))
                        continue;
                    if (SpotOk(tx, ty, playerX, playerY, occX, occY, occN))
                    {
                        x = tx;
                        y = ty;
                        return true;
                    }

                    float d = Dist(tx, ty, playerX, playerY);
                    if (!TooClose(tx, ty, occX, occY, occN) && d > bestD)
                    {
                        bestD = d;
                        x = tx;
                        y = ty;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Re-checks wave spots against the player's position at spawn time (the player moves during
        /// the portal hold) and against each other. Bad spots are re-rolled via PlaceOne, then spiral.
        /// Returns how many spots were moved.
        /// </summary>
        public static int EnforceAtSpawn(
            MazeNode room, float playerX, float playerY, SpawnSpot[] spots, SpawnAvoid[] avoids, Random rng)
        {
            if (room == null || spots == null)
                return 0;
            int n = spots.Length;
            var occX = new float[n];
            var occY = new float[n];
            int moved = 0;
            for (int i = 0; i < n; i++)
            {
                float x = spots[i].X;
                float y = spots[i].Y;
                if (!SpotOk(x, y, playerX, playerY, occX, occY, i) || HitsVolume(x, y, avoids))
                {
                    if (!PlaceOne(room.Center.X, room.Center.Y, room.Width, room.Height,
                            playerX, playerY, spots[i].Ranged, avoids, occX, occY, i, rng, out x, out y)
                        || !SpotOk(x, y, playerX, playerY, occX, occY, i))
                    {
                        FindFreeNear(room.Center.X, room.Center.Y, room.Width, room.Height,
                            spots[i].X, spots[i].Y, playerX, playerY, avoids, occX, occY, i, out x, out y);
                    }

                    moved++;
                }

                spots[i].X = x;
                spots[i].Y = y;
                spots[i].DistPlayer = Dist(x, y, playerX, playerY);
                occX[i] = x;
                occY[i] = y;
            }

            return moved;
        }

        public static CombatRoomSpawnStats SampleSeed42()
        {
            Stage1Maze maze = Stage1MazeGen.Generate(42);
            MazeNode chest = maze != null ? maze.Find("CHEST") : null;
            MazeNode altar = maze != null ? maze.Find("ALTAR") : null;
            MazeNode room = chest ?? altar;
            var stats = new CombatRoomSpawnStats
            {
                MeleeMin = 999f,
                RangedMin = 999f
            };
            if (room == null)
                return stats;

            float px = room.Center.X;
            float py = room.Center.Y - room.Height * 0.5f + RoomInset + 1.2f;
            var avoids = new[]
            {
                FromSite(SiteKind.Chest, room.Center.X, room.Center.Y + 0.4f)
            };

            int trials = SampleTrials;
            var meleeD = new float[trials];
            var rangedD = new float[trials];
            var rows = new CombatRoomSpawnTrial[trials * 2];
            float meleeSum = 0f;
            float rangedSum = 0f;
            float spreadSum = 0f;
            var rngM = new Random(42001);
            var rngR = new Random(42002);
            for (int i = 0; i < trials; i++)
            {
                float x, y;
                PlaceOne(room.Center.X, room.Center.Y, room.Width, room.Height,
                    px, py, false, avoids, null, null, 0, rngM, out x, out y);
                RecordTrial(ref stats, rows, i * 2, false, x, y, px, py,
                    room.Center.X, room.Center.Y, avoids, meleeD, i, ref meleeSum, ref spreadSum);

                PlaceOne(room.Center.X, room.Center.Y, room.Width, room.Height,
                    px, py, true, avoids, null, null, 0, rngR, out x, out y);
                RecordTrial(ref stats, rows, i * 2 + 1, true, x, y, px, py,
                    room.Center.X, room.Center.Y, avoids, rangedD, i, ref rangedSum, ref spreadSum);
            }

            stats.MeleeN = trials;
            stats.RangedN = trials;
            stats.MeleeMean = meleeSum / trials;
            stats.RangedMean = rangedSum / trials;
            stats.MeanGap = stats.RangedMean - stats.MeleeMean;
            stats.CenterSpread = spreadSum / (trials * 2f);
            stats.MeleeP10 = Percentile(meleeD, 0.10f);
            stats.MeleeP50 = Percentile(meleeD, 0.50f);
            stats.MeleeP90 = Percentile(meleeD, 0.90f);
            stats.RangedP10 = Percentile(rangedD, 0.10f);
            stats.RangedP50 = Percentile(rangedD, 0.50f);
            stats.RangedP90 = Percentile(rangedD, 0.90f);
            stats.Trials = rows;
            return stats;
        }

        public static string DumpSeed42Csv()
        {
            CombatRoomSpawnStats s = SampleSeed42();
            var sb = new StringBuilder();
            sb.AppendLine("# Stage-1 spawn land (CombatRoomSpawn) maze seed=42 CHEST 52x40");
            sb.AppendLine("# bypass SpawnCluster.Offset even-ring r=0.85");
            sb.AppendLine("# room-AABB uniform inset=wall+body; melee nearest 40%; ranged farthest 40%");
            sb.AppendLine("# minPlayer=2 (HOOKS chest r); avoid chest/altar BoxCollider2D + NoSpawnCore");
            sb.AppendLine("# player=south inset; fake chest at center+(0,0.4)");
            sb.AppendLine("trial,role,x,y,dist_player,dist_center,avoid_hit,min_player_hit,even_ring");
            if (s.Trials != null)
            {
                int meleeI = 0;
                int rangedI = 0;
                for (int i = 0; i < s.Trials.Length; i++)
                {
                    CombatRoomSpawnTrial t = s.Trials[i];
                    int idx = t.Ranged ? rangedI++ : meleeI++;
                    sb.Append(idx).Append(',').Append(t.Ranged ? "ranged" : "melee");
                    sb.Append(',').Append(t.X.ToString("0.000"));
                    sb.Append(',').Append(t.Y.ToString("0.000"));
                    sb.Append(',').Append(t.DistPlayer.ToString("0.000"));
                    sb.Append(',').Append(t.DistCenter.ToString("0.000"));
                    sb.Append(',').Append(t.AvoidHit);
                    sb.Append(',').Append(t.MinPlayerHit);
                    sb.Append(',').Append(t.EvenRingHit);
                    sb.AppendLine();
                }
            }

            sb.AppendLine("# summary");
            sb.AppendLine("metric,value,note");
            Row(sb, "melee_n", s.MeleeN.ToString(), "trials");
            Row(sb, "ranged_n", s.RangedN.ToString(), "trials");
            Row(sb, "melee_mean", s.MeleeMean.ToString("0.000"), "dist to player");
            Row(sb, "ranged_mean", s.RangedMean.ToString("0.000"), "dist to player");
            Row(sb, "mean_gap", s.MeanGap.ToString("0.000"), "ranged-melee (need >1.5)");
            Row(sb, "melee_min", s.MeleeMin.ToString("0.000"), "min dist player");
            Row(sb, "ranged_min", s.RangedMin.ToString("0.000"), "min dist player");
            Row(sb, "melee_p10", s.MeleeP10.ToString("0.000"), "dist_player");
            Row(sb, "melee_p50", s.MeleeP50.ToString("0.000"), "dist_player");
            Row(sb, "melee_p90", s.MeleeP90.ToString("0.000"), "dist_player");
            Row(sb, "ranged_p10", s.RangedP10.ToString("0.000"), "dist_player");
            Row(sb, "ranged_p50", s.RangedP50.ToString("0.000"), "dist_player");
            Row(sb, "ranged_p90", s.RangedP90.ToString("0.000"), "dist_player");
            Row(sb, "center_spread", s.CenterSpread.ToString("0.000"), "mean |p-center| (old ring=0.85)");
            Row(sb, "avoid_hits", s.AvoidHits.ToString(), "placed inside chest/altar volume");
            Row(sb, "min_player_hits", s.MinPlayerHits.ToString(), "placed closer than 2");
            Row(sb, "even_ring_hits", s.EvenRingHits.ToString(), "|r-0.85|<0.08");
            string probe = ProbeAvoidVolumes();
            Row(sb, "avoid_probe", probe == null ? "PASS" : "FAIL", probe ?? "chest+altar AABB spot-check");
            AppendHist(sb, s);
            bool pass = s.MeleeN >= 8 && s.AvoidHits == 0 && s.MinPlayerHits == 0
                && s.EvenRingHits <= s.MeleeN / 5 && s.CenterSpread >= 3f
                && s.MeleeMean + 1.5f <= s.RangedMean
                && s.MeleeP50 + 1.0f <= s.RangedP50 && probe == null;
            sb.Append("# ").Append(pass ? "ACCEPTANCE PASS spawn land" : "ACCEPTANCE FAIL spawn land");
            sb.AppendLine();
            return sb.ToString();
        }

        static void RecordTrial(
            ref CombatRoomSpawnStats stats,
            CombatRoomSpawnTrial[] rows,
            int row,
            bool ranged,
            float x, float y, float px, float py, float cx, float cy,
            SpawnAvoid[] avoids,
            float[] distArr, int distI,
            ref float sum, ref float spreadSum)
        {
            float d = Dist(x, y, px, py);
            float dc = Dist(x, y, cx, cy);
            int avoid = HitsVolume(x, y, avoids) ? 1 : 0;
            int minP = d < MinPlayerDist - 0.001f ? 1 : 0;
            int ring = IsEvenRing(x, y, cx, cy) ? 1 : 0;
            distArr[distI] = d;
            sum += d;
            spreadSum += dc;
            if (!ranged && d < stats.MeleeMin) stats.MeleeMin = d;
            if (ranged && d < stats.RangedMin) stats.RangedMin = d;
            stats.MinPlayerHits += minP;
            stats.AvoidHits += avoid;
            stats.EvenRingHits += ring;
            rows[row] = new CombatRoomSpawnTrial
            {
                Ranged = ranged,
                X = x,
                Y = y,
                DistPlayer = d,
                DistCenter = dc,
                AvoidHit = avoid,
                MinPlayerHit = minP,
                EvenRingHit = ring
            };
        }

        static void Row(StringBuilder sb, string metric, string value, string note)
        {
            sb.Append(metric).Append(',').Append(value).Append(',').Append(note).AppendLine();
        }

        static void AppendHist(StringBuilder sb, CombatRoomSpawnStats s)
        {
            sb.AppendLine("# dist_player histogram bin_width=4");
            sb.AppendLine("bin,melee,ranged");
            if (s.Trials == null)
                return;
            const int bins = 12;
            var melee = new int[bins];
            var ranged = new int[bins];
            for (int i = 0; i < s.Trials.Length; i++)
            {
                int b = (int)(s.Trials[i].DistPlayer / 4f);
                if (b < 0) b = 0;
                if (b >= bins) b = bins - 1;
                if (s.Trials[i].Ranged)
                    ranged[b]++;
                else
                    melee[b]++;
            }

            for (int i = 0; i < bins; i++)
            {
                sb.Append(i * 4).Append('-').Append((i + 1) * 4);
                sb.Append(',').Append(melee[i]).Append(',').Append(ranged[i]).AppendLine();
            }
        }

        static int FillPool(
            float cx, float cy, float w, float h,
            float playerX, float playerY,
            SpawnAvoid[] avoids,
            float[] occX, float[] occY, int occN,
            Random rng,
            bool enforceMinPlayer, bool enforcePack,
            float[] ox, float[] oy, float[] od)
        {
            float inset = RoomInset;
            float x0 = cx - w * 0.5f + inset;
            float x1 = cx + w * 0.5f - inset;
            float y0 = cy - h * 0.5f + inset;
            float y1 = cy + h * 0.5f - inset;
            if (x1 <= x0 || y1 <= y0)
                return 0;

            int n = 0;
            int guard = PoolSize * 8;
            for (int k = 0; k < guard && n < PoolSize; k++)
            {
                float x = x0 + (float)rng.NextDouble() * (x1 - x0);
                float y = y0 + (float)rng.NextDouble() * (y1 - y0);
                float d = Dist(x, y, playerX, playerY);
                if (enforceMinPlayer && d < MinPlayerDist)
                    continue;
                if (HitsVolume(x, y, avoids))
                    continue;
                if (enforcePack && TooClose(x, y, occX, occY, occN))
                    continue;
                ox[n] = x;
                oy[n] = y;
                od[n] = d;
                n++;
            }

            return n;
        }

        static void Band(int filled, bool ranged, out int lo, out int hi)
        {
            if (filled <= 1)
            {
                lo = 0;
                hi = filled;
                return;
            }

            int nearN = (int)(filled * NearFrac);
            if (nearN < 1) nearN = 1;
            int farN = (int)(filled * FarFrac);
            if (farN < 1) farN = 1;
            if (ranged)
            {
                lo = filled - farN;
                if (lo < 0) lo = 0;
                hi = filled;
            }
            else
            {
                lo = 0;
                hi = nearN;
            }
        }

        static void SortByDist(float[] px, float[] py, float[] dist, int n)
        {
            for (int i = 1; i < n; i++)
            {
                float dx = dist[i];
                float x = px[i];
                float y = py[i];
                int j = i - 1;
                while (j >= 0 && dist[j] > dx)
                {
                    dist[j + 1] = dist[j];
                    px[j + 1] = px[j];
                    py[j + 1] = py[j];
                    j--;
                }

                dist[j + 1] = dx;
                px[j + 1] = x;
                py[j + 1] = y;
            }
        }

        static bool TooClose(float x, float y, float[] occX, float[] occY, int occN)
        {
            if (occX == null || occN <= 0)
                return false;
            float min = PackSep;
            for (int i = 0; i < occN; i++)
            {
                if (Dist(x, y, occX[i], occY[i]) < min)
                    return true;
            }

            return false;
        }

        static bool IsEvenRing(float x, float y, float cx, float cy)
        {
            float r = Dist(x, y, cx, cy);
            return Math.Abs(r - SpawnCluster.Radius) < 0.08f;
        }

        static void ClampToRoom(float cx, float cy, float w, float h, float x, float y, out float ox, out float oy)
        {
            float inset = RoomInset;
            float x0 = cx - w * 0.5f + inset;
            float x1 = cx + w * 0.5f - inset;
            float y0 = cy - h * 0.5f + inset;
            float y1 = cy + h * 0.5f - inset;
            ox = x < x0 ? x0 : (x > x1 ? x1 : x);
            oy = y < y0 ? y0 : (y > y1 ? y1 : y);
        }

        static float Percentile(float[] v, float p)
        {
            if (v == null || v.Length == 0)
                return 0f;
            var copy = new float[v.Length];
            Array.Copy(v, copy, v.Length);
            Array.Sort(copy);
            int i = (int)((copy.Length - 1) * p);
            if (i < 0) i = 0;
            if (i >= copy.Length) i = copy.Length - 1;
            return copy[i];
        }

        static float Dist(float ax, float ay, float bx, float by)
        {
            float dx = ax - bx;
            float dy = ay - by;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
