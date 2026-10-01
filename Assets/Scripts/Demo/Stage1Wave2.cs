using System.Collections.Generic;
using UnityEngine;
using RogueShooter.Maze;
using RogueShooter.Spawning;

namespace RogueShooter.Demo
{
    public struct RunEndInfo
    {
        public int Seconds;
        public int Rewards;
        public int Keep;
        public string Restart;
    }

    /// <summary>
    /// Wave 2 stands on room candidates, outside the 0.85 center cluster and off the player.
    /// Wave 1 still uses SpawnCluster. No portal wait is added here.
    /// </summary>
    public static class Stage1Wave2
    {
        public const float MeleeFromPlayer = 4f;
        public const float MeleeGap = 2.6f;

        public static Vector3[] Spots(MazeNode node, Vector3 player, string[] kinds)
        {
            int n = kinds != null ? kinds.Length : 0;
            var spots = new Vector3[n];
            if (node == null || n == 0)
                return spots;
            Vector3 center = new Vector3(node.Center.X, node.Center.Y, 0f);
            var pool = Candidates(node, center);
            var used = new List<Vector3>();
            for (int i = 0; i < n; i++)
            {
                bool melee = EnemyKindCatalog.ForKind(kinds[i]).Melee;
                Vector3 picked = Pick(pool, player, used, melee, i);
                spots[i] = picked;
                used.Add(picked);
            }

            return spots;
        }

        static List<Vector3> Candidates(MazeNode node, Vector3 center)
        {
            var list = new List<Vector3>();
            float[] radii = { 5f, 8f, 11f };
            for (int r = 0; r < radii.Length; r++)
            {
                int arcs = r == 0 ? 8 : 10;
                for (int a = 0; a < arcs; a++)
                {
                    float ang = (Mathf.PI * 2f * a) / arcs + r * 0.4f;
                    Vector3 p = center + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * radii[r];
                    if (!node.Contains(p.x, p.y, 1.6f))
                        continue;
                    if (Vector2.Distance(Flat(p), Flat(center)) <= SpawnCluster.Radius + 0.05f)
                        continue;
                    list.Add(p);
                }
            }

            return list;
        }

        static Vector3 Pick(List<Vector3> pool, Vector3 player, List<Vector3> used, bool melee, int index)
        {
            int best = -1;
            float bestScore = -1f;
            float fromPlayer = melee ? MeleeFromPlayer : 3f;
            float gap = melee ? MeleeGap : 2.2f;
            for (int i = 0; i < pool.Count; i++)
            {
                Vector3 p = pool[i];
                float dp = Vector2.Distance(Flat(p), Flat(player));
                if (dp < fromPlayer)
                    continue;
                if (TooClose(p, used, gap))
                    continue;
                float score = dp + (melee ? 0f : 2f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }

            if (best < 0)
            {
                float loose = -1f;
                for (int i = 0; i < pool.Count; i++)
                {
                    float dp = Vector2.Distance(Flat(pool[i]), Flat(player));
                    float near = Nearest(pool[i], used);
                    float score = Mathf.Min(dp, near);
                    if (score > loose)
                    {
                        loose = score;
                        best = i;
                    }
                }
            }

            if (best >= 0)
            {
                Vector3 chosen = pool[best];
                pool.RemoveAt(best);
                return chosen;
            }

            float ang = index * 0.9f + 0.3f;
            return player + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * (fromPlayer + 1.5f);
        }

        static float Nearest(Vector3 p, List<Vector3> used)
        {
            float best = 99f;
            for (int i = 0; i < used.Count; i++)
            {
                float d = Vector2.Distance(Flat(p), Flat(used[i]));
                if (d < best)
                    best = d;
            }

            return best;
        }

        static bool TooClose(Vector3 p, List<Vector3> used, float gap)
        {
            for (int i = 0; i < used.Count; i++)
            {
                if (Vector2.Distance(Flat(p), Flat(used[i])) < gap)
                    return true;
            }

            return false;
        }

        static Vector2 Flat(Vector3 p)
        {
            return new Vector2(p.x, p.y);
        }
    }
}
