using System;
using System.Collections.Generic;
using UnityEngine;
using RogueShooter.Maze;
using RogueShooter.Spawning;

namespace RogueShooter.Demo
{
    public struct RunEndInfo { public int Seconds; public int Rewards; public int Keep; public string Restart; }
    /// <summary>Every wave uses legal separated ground; no relaxed near-player fallback.</summary>
    public static class Stage1Wave2
    {
        public const float MeleeFromPlayer=4f, MeleeGap=2.6f, MinimumGap=2.2f;
        public static bool ValidFeet(MazeNode node, Vector3 p)
        {
            if(node==null || !node.Contains(p.x,p.y,1.6f))return false;
            for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)
                if(!node.Walkable(p.x+x*.45f,p.y+y*.45f))return false;
            return true;
        }
        public static bool IsSafe(MazeNode node,Vector3 player,Vector3[] spots)
        {
            if(spots==null)return false;
            for(int i=0;i<spots.Length;i++)
            {
                if(!ValidFeet(node,spots[i])||Vector2.Distance(spots[i],player)<MeleeFromPlayer-.001f)return false;
                for(int j=0;j<i;j++)if(Vector2.Distance(spots[i],spots[j])<MinimumGap-.001f)return false;
            }
            return true;
        }
        public static Vector3[] Spots(MazeNode node,Vector3 player,string[] kinds,Stage1Maze maze=null,bool opening=false,Vector3[] preferred=null)
        {
            var pool=new List<Vector3>();var used=new List<Vector3>();
            if(node==null)return new Vector3[0];
            var doors=maze!=null?Stage1IsoArt.CollectDoors(node,maze):new List<Stage1IsoArt.DoorMark>();
            // A dense grid also covers narrow cross-shaped floor layouts and avoids solid decorations.
            for(float y=node.Center.Y-node.Height*.5f+1.8f;y<=node.Center.Y+node.Height*.5f-1.8f;y+=1.5f)
            for(float x=node.Center.X-node.Width*.5f+1.8f;x<=node.Center.X+node.Width*.5f-1.8f;x+=1.5f)
            {
                var p=new Vector3(x,y,0);if(!ValidFeet(node,p))continue;
                bool nearDoor=false;foreach(var door in doors)if(Vector2.Distance(p,door.Point)<4.4f){nearDoor=true;break;}
                if(!nearDoor && (!opening || ClearOfEntrances(p,doors)))pool.Add(p);
            }
            int count=kinds==null?0:kinds.Length;
            for(int i=0;i<count;i++)
            {
                float gap=EnemyKindCatalog.ForKind(kinds[i]).Melee?MeleeGap:MinimumGap;
                // Preserve all already-safe portal locations before considering
                // alternatives, so entering one marker does not move the whole wave.
                if(preferred!=null && i<preferred.Length && ValidFeet(node,preferred[i])
                    && Vector2.Distance(preferred[i],player)>=MeleeFromPlayer)
                {
                    bool separated=true;foreach(var p in used)if(Vector2.Distance(p,preferred[i])<MinimumGap)separated=false;
                    for(int j=i+1;j<preferred.Length;j++)
                        if(ValidFeet(node,preferred[j]) && Vector2.Distance(preferred[j],player)>=MeleeFromPlayer
                            && Vector2.Distance(preferred[i],preferred[j])<MinimumGap)separated=false;
                    if(separated){used.Add(preferred[i]);continue;}
                }
                int best=-1;float bestScore=-1;
                for(int k=0;k<pool.Count;k++)
                {
                    float dp=Vector2.Distance(pool[k],player);if(!opening && dp<MeleeFromPlayer)continue;
                    bool nearReserved=false;
                    if(preferred!=null)for(int j=i+1;j<preferred.Length;j++)
                        if(ValidFeet(node,preferred[j]) && Vector2.Distance(preferred[j],player)>=MeleeFromPlayer
                            && Vector2.Distance(pool[k],preferred[j])<gap)nearReserved=true;
                    if(nearReserved)continue;
                    float near=float.MaxValue;foreach(var p in used)near=Mathf.Min(near,Vector2.Distance(p,pool[k]));
                    if(near<gap)continue;
                    float score=Mathf.Min(dp,12f)+Mathf.Min(near,8f);
                    if(score>bestScore){best=k;bestScore=score;}
                }
                if(best<0)throw new InvalidOperationException("No safe spawn capacity in "+node.Id+" for "+count+" enemies");
                used.Add(pool[best]);pool.RemoveAt(best);
            }
            return used.ToArray();
        }
        public static bool ClearOfEntrances(Vector3 p,List<Stage1IsoArt.DoorMark> doors)
        {
            foreach(var d in doors)
            {
                Vector2 inward=d.Rim=='N'?Vector2.down:d.Rim=='S'?Vector2.up:d.Rim=='E'?Vector2.left:Vector2.right;
                Vector2 rel=(Vector2)p-d.Point;
                float depth=Vector2.Dot(rel,inward);
                float across=Mathf.Abs(Vector2.Dot(rel,new Vector2(-inward.y,inward.x)));
                // TryEnterRoom fires at depth 1.15; reserve its full doorway width
                // plus a short step margin. Clearance is authored before visibility.
                float a=Mathf.Max(0f,across-Stage1IsoArt.DoorGap*.5f);
                float b=depth<1.15f?1.15f-depth:Mathf.Max(0f,depth-2f);
                if(a*a+b*b<MeleeFromPlayer*MeleeFromPlayer)return false;
            }
            return true;
        }
    }
}
