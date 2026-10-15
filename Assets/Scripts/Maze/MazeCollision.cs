using System.Collections.Generic;
using UnityEngine;
using RogueShooter.Demo;
namespace RogueShooter.Maze
{
    /// <summary>Floor-space projectile sweeps against the authoritative maze and live combat gates.
    /// Returns 2 for a clear segment; otherwise the first blocking fraction in [0,1].</summary>
    public static class MazeCollision
    {
        public static float FirstBlockT(Stage1Maze maze, IReadOnlyList<GameObject> doors, Vector3 a, Vector3 b, float radius = 0f)
        {
            float hit = 2f;
            if (maze != null)
            {
                if (!ClearAt(maze, a, radius)) return 0f;
                int count = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / .1f));
                for (int i = 1; i <= count; i++)
                {
                    float t = (float)i / count;
                    if (ClearAt(maze, Vector3.Lerp(a, b, t), radius)) continue;
                    float lo = (float)(i - 1) / count, hi = t;
                    for (int k = 0; k < 10; k++)
                    {
                        float mid = (lo + hi) * .5f;
                        if (ClearAt(maze, Vector3.Lerp(a, b, mid), radius)) lo = mid; else hi = mid;
                    }
                    hit = hi; break;
                }
            }
            if (doors == null) return hit;
            for (int i = 0; i < doors.Count; i++)
            {
                var go = doors[i];
                if (go == null || !go.activeInHierarchy) continue;
                var gate = go.GetComponent<Stage1DoorGate>();
                if (gate == null) continue;
                Vector2 center, half;
                if (gate.SlidingView != null)
                {
                    var blocker = gate.SlidingView.ClosedBlocker;
                    if (blocker == null || !blocker.enabled) continue;
                    center = (Vector2)blocker.transform.position + blocker.offset;
                    half = blocker.size * .5f;
                }
                else if (gate.HingedView != null && (gate.IsLocked || gate.HingedView.Open01 < .98f))
                {
                    var view = gate.HingedView;
                    center = view.transform.position;
                    bool side = view.Rim == 'E' || view.Rim == 'W';
                    half = side ? new Vector2(.15f, view.ClearWidth * .5f) : new Vector2(view.ClearWidth * .5f, .15f);
                }
                else continue;
                float t = BoxEntryT(a, b, center, half + Vector2.one * radius);
                if (t < hit) hit = t;
            }
            return hit;
        }
        public static Vector3 Slide(Stage1Maze maze,IReadOnlyList<GameObject> doors,Vector3 from,Vector3 to,float radius)
        {
            if(maze==null)return to;
            Vector3 p=from;
            int steps=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(from,to)/.1f));
            Vector3 step=(to-from)/steps;
            for(int i=0;i<steps;i++)
            {
                Vector3 q=p+step;
                if(FirstBlockT(maze,doors,p,q,radius)>1f){p=q;continue;}
                q=p+new Vector3(step.x,0,0);if(FirstBlockT(maze,doors,p,q,radius)>1f)p=q;
                q=p+new Vector3(0,step.y,0);if(FirstBlockT(maze,doors,p,q,radius)>1f)p=q;
            }return p;
        }
        static bool ClearAt(Stage1Maze maze, Vector3 p, float r)
        {
            if (!maze.OpenAt(p.x, p.y)) return false;
            if (r <= 0f) return true;
            return maze.OpenAt(p.x-r,p.y) && maze.OpenAt(p.x+r,p.y)
                && maze.OpenAt(p.x,p.y-r) && maze.OpenAt(p.x,p.y+r)
                && maze.OpenAt(p.x-r*.7071f,p.y-r*.7071f) && maze.OpenAt(p.x+r*.7071f,p.y-r*.7071f)
                && maze.OpenAt(p.x-r*.7071f,p.y+r*.7071f) && maze.OpenAt(p.x+r*.7071f,p.y+r*.7071f);
        }
        static float BoxEntryT(Vector2 a, Vector2 b, Vector2 c, Vector2 half)
        {
            Vector2 d = b - a; float lo = 0f, hi = 1f;
            for (int axis = 0; axis < 2; axis++)
            {
                if (Mathf.Abs(d[axis]) < .000001f)
                { if (a[axis] < c[axis]-half[axis] || a[axis] > c[axis]+half[axis]) return 2f; continue; }
                float t0 = (c[axis]-half[axis]-a[axis])/d[axis], t1 = (c[axis]+half[axis]-a[axis])/d[axis];
                lo = Mathf.Max(lo, Mathf.Min(t0,t1)); hi = Mathf.Min(hi, Mathf.Max(t0,t1));
                if (lo > hi) return 2f;
            }
            return lo;
        }
        public static float CircleEntryT(Vector3 point, Vector3 a, Vector3 b, float radius)
        {
            Vector2 delta = b-a, offset = a-point;
            float c = offset.sqrMagnitude-radius*radius;
            if (c <= 0f) return 0f;
            float len = delta.sqrMagnitude;
            if (len < .000001f) return 2f;
            float dot = Vector2.Dot(offset,delta);
            float disc = dot*dot-len*c;
            if (disc < 0f) return 2f;
            float t = (-dot-Mathf.Sqrt(disc))/len;
            return t >= 0f && t <= 1f ? t : 2f;
        }
    }
}
