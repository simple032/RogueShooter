using System;
using System.Collections.Generic;
using UnityEngine;
using RogueShooter.Maze;
using RogueShooter.Spawning;

namespace RogueShooter.Demo
{
    /// <summary>One persistent level, with two isolated, altar-owned links. Source templates stay unchanged.</summary>
    public sealed class StageLevelMap
    {
        public Stage1Maze Maze;
        public Stage1Maze BossWing;
        public MazeEdge FinalLink;
        public MazeNode BossRoom;
        public readonly List<Stage1Maze> Parts = new List<Stage1Maze>();
        public readonly List<S2S3Layout> Layouts = new List<S2S3Layout>();
        public readonly List<MazeEdge> Links = new List<MazeEdge>();
        public StageId RoomStage(string id) { return id == "BOSS" || id == "PRE_BOSS" || (id != null && id.StartsWith("S3_")) ? StageId.S3 : id != null && id.StartsWith("S2_") ? StageId.S2 : StageId.S1; }
        public S2S3Layout Layout(StageId stage) { return stage == StageId.S1 ? null : Layouts[(int)stage-2]; }
        public string AltarId(StageId stage) { return Array.Find(Parts[(int)stage-1].Nodes, n => n.Kind == MazeNodeKind.Altar).Id; }

        public static StageLevelMap Generate(int seed, bool trial = false, string s2 = null, string s3 = null)
        {
            var level = new StageLevelMap();
            var first = Stage1MazeGen.Generate(seed);
            if (!trial)
            {
                first.Nodes = Array.FindAll(first.Nodes, n => n.Kind != MazeNodeKind.Connector);
                first.Edges = Array.FindAll(first.Edges, e => e.FromId != "CONN" && e.ToId != "CONN");
            }
            level.Parts.Add(first);
            int large = 0;
            if (!trial)
            for (int phase = 2; phase <= 3; phase++)
            {
                StageId stage = (StageId)phase;
                string template = phase == 2 ? s2 : s3;
                var layout = template == null ? StageMapGen.Generate(stage, seed, 2-large) : StageMapGen.GenerateTemplate(template, seed, 2-large);
                var previous = level.Parts[phase-2];
                var altar = Array.Find(previous.Nodes, n => n.Kind == MazeNodeKind.Altar);
                var nodes = new List<MazeNode>(); var edges = new List<MazeEdge>();
                foreach (var part in level.Parts) { nodes.AddRange(part.Nodes); edges.AddRange(part.Edges); }
                Vector2[] path = DockLayout(altar,layout,nodes,edges);
                string prefix = "S"+phase+"_";
                foreach (var room in layout.Rooms) room.Id = prefix+room.Id;
                foreach (var edge in layout.Edges) { edge.FromId=prefix+edge.FromId;edge.ToId=prefix+edge.ToId; }
                var partMaze = layout.ToMaze();
                if(phase==2)
                {
                    var candidates=Array.FindAll(partMaze.Nodes,n=>n.Kind==MazeNodeKind.Normal && n.Id!="S2_A");
                    if(candidates.Length>0)
                    {
                        var healing=candidates[new System.Random(seed^0x2417).Next(candidates.Length)];
                        healing.Kind=MazeNodeKind.Heal;
                        layout.Find(healing.Id).Type=S2S3RoomType.Heal;
                    }
                }
                foreach(var room in partMaze.Nodes) RoomWalkShape.Configure(room, seed);
                var link = Edge(altar.Id, prefix+"A", path);
                var outgoing = new List<MazeEdge>(previous.Edges) { link };
                previous.Edges = outgoing.ToArray();
                level.Links.Add(link); level.Layouts.Add(layout); level.Parts.Add(partMaze);
                large += partMaze.CountKind(MazeNodeKind.LargeChest);
            }
            if(!trial)
            {
                var nodes=new List<MazeNode>();var edges=new List<MazeEdge>();foreach(var part in level.Parts){nodes.AddRange(part.Nodes);edges.AddRange(part.Edges);}
                var last=level.Parts[2];var altar=Array.Find(last.Nodes,n=>n.Kind==MazeNodeKind.Altar);
                var wing=new S2S3Layout{Id="FINAL_WING",Rooms=new[]{
                    new S2S3Room{Id="PRE_BOSS",Type=S2S3RoomType.Normal,Col=0,Row=0},
                    new S2S3Room{Id="BOSS",Type=S2S3RoomType.Normal,Col=0,Row=1}},
                    Edges=new[]{new S2S3Edge{FromId="PRE_BOSS",ToId="BOSS",From=new Vector2(0,11),To=new Vector2(0,33),Points=new[]{new Vector2(0,11),new Vector2(0,33)}}}};
                var path=DockLayout(altar,wing,nodes,edges);
                var wingMaze=wing.ToMaze();var safe=wingMaze.Find("PRE_BOSS");safe.Kind=MazeNodeKind.Safe;
                var boss=wingMaze.Find("BOSS");boss.Kind=MazeNodeKind.Boss;
                level.FinalLink=Edge(altar.Id,safe.Id,path);
                last.Edges=new List<MazeEdge>(last.Edges){level.FinalLink}.ToArray();
                var inner=wingMaze.Edges[0];
                level.BossRoom=boss;level.BossWing=new Stage1Maze{Seed=seed,TemplateId="FINAL_WING",Nodes=new[]{safe,boss},Edges=new[]{inner}};
            }
            var allNodes = new List<MazeNode>(); var allEdges = new List<MazeEdge>();
            foreach (var part in level.Parts) { allNodes.AddRange(part.Nodes); allEdges.AddRange(part.Edges); }
            if(level.BossWing!=null){allNodes.AddRange(level.BossWing.Nodes);allEdges.AddRange(level.BossWing.Edges);}
            foreach (var n in allNodes)
            {
                var neighbors = new List<string>();
                foreach (var e in allEdges) { if(e.FromId==n.Id)neighbors.Add(e.ToId);else if(e.ToId==n.Id)neighbors.Add(e.FromId); }
                n.NeighborIds = neighbors.ToArray();
            }
            level.Maze = new Stage1Maze { Seed=seed, TemplateId="FULL_"+first.TemplateId,
                Nodes=allNodes.ToArray(), Edges=allEdges.ToArray(), Signature=first.Signature+"|"+string.Join("|",level.Layouts.ConvertAll(l=>l.Id+":"+l.Seed)) };
            return level;
        }

        public static S2S3Layout CorridorLayout(Stage1Maze part)
        {
            return new S2S3Layout { Id=part.TemplateId+"_corridors", Rooms=new S2S3Room[0],
                Edges=Array.ConvertAll(part.Edges,e=>new S2S3Edge { FromId=e.FromId,ToId=e.ToId,
                    From=new Vector2(e.From.X,e.From.Y),To=new Vector2(e.To.X,e.To.Y),
                    Points=Array.ConvertAll(e.Points,p=>new Vector2(p.X,p.Y)) }) };
        }
        static MazeEdge Edge(string from, string to, Vector2[] path)
        {
            float length=0f,max=0f;
            for(int i=1;i<path.Length;i++){float d=Vector2.Distance(path[i-1],path[i]);length+=d;max=Mathf.Max(max,d);}
            return new MazeEdge { FromId=from,ToId=to,From=new MazeVec2(path[0].x,path[0].y),To=new MazeVec2(path[path.Length-1].x,path[path.Length-1].y),
                Width=MazeRules.CorridorWidth,Length=length,MaxSegment=max,FoldCount=path.Length-1,Points=Array.ConvertAll(path,p=>new MazeVec2(p.x,p.y)) };
        }
        static Vector2[] Simplify(List<Vector2> points)
        {
            var result=new List<Vector2>();
            foreach(var p in points)
            {
                if(result.Count>0 && (p-result[result.Count-1]).sqrMagnitude<.001f)continue;
                if(result.Count>1 && Mathf.Abs(Vector3.Cross(result[result.Count-1]-result[result.Count-2],p-result[result.Count-1]).z)<.001f)result.RemoveAt(result.Count-1);
                result.Add(p);
            }
            return result.ToArray();
        }
        public const float StageDoorGap=22f;
        static Rect RoomRect(MazeNode n)=>new Rect(n.Center.X-n.Width*.5f,n.Center.Y-n.Height*.5f,n.Width,n.Height);
        static Rect SegmentRect(Vector2 a,Vector2 b,float half)=>Rect.MinMaxRect(Mathf.Min(a.x,b.x)-half,Mathf.Min(a.y,b.y)-half,Mathf.Max(a.x,b.x)+half,Mathf.Max(a.y,b.y)+half);
        static List<Rect> CorridorRects(IEnumerable<MazeEdge> edges)
        {
            var result=new List<Rect>();foreach(var e in edges)for(int i=1;i<e.Points.Length;i++)
                result.Add(SegmentRect(new Vector2(e.Points[i-1].X,e.Points[i-1].Y),new Vector2(e.Points[i].X,e.Points[i].Y),e.Width*.5f+1f));
            return result;
        }
        static bool Overlap(Rect a,Rect b,float gap=2f)=>a.xMin<b.xMax+gap&&a.xMax>b.xMin-gap&&a.yMin<b.yMax+gap&&a.yMax>b.yMin-gap;
        static Vector2[] DockLayout(MazeNode altar,S2S3Layout layout,List<MazeNode> oldNodes,List<MazeEdge> oldEdges)
        {
            Vector2 origin=layout.Rooms[0].Center;var oldCorridors=CorridorRects(oldEdges);
            foreach(var direction in new[]{Vector2.up,Vector2.right,Vector2.left,Vector2.down})
            foreach(int mx in new[]{1,-1})foreach(int my in new[]{1,-1})
            {
                float half=direction.x!=0?altar.Width*.5f:altar.Height*.5f;
                float nextHalf=direction.x!=0?MazeRules.CombatWidth*.5f:MazeRules.CombatHeight*.5f;
                Vector2 mouth=new Vector2(altar.Center.X,altar.Center.Y)+direction*half;
                Vector2 center=mouth+direction*(StageDoorGap+nextHalf);
                Vector2 offset=center-new Vector2(origin.x*mx,origin.y*my);
                var candidate=new Stage1Maze{Nodes=Array.ConvertAll(layout.Rooms,r=>{
                    Vector2 c=new Vector2(r.Center.x*mx,r.Center.y*my)+offset;
                    return new MazeNode{Id=r.Id,Center=new MazeVec2(c.x,c.y),Width=MazeRules.CombatWidth,Height=MazeRules.CombatHeight};}),
                    Edges=Array.ConvertAll(layout.Edges,e=>Edge(e.FromId,e.ToId,Array.ConvertAll(e.Points,p=>new Vector2(p.x*mx,p.y*my)+offset)))};
                Vector2 target=center-direction*nextHalf;
                Rect link=SegmentRect(mouth,target,MazeRules.CorridorWidth*.5f+1f);
                var nextCorridors=CorridorRects(candidate.Edges);bool bad=false;
                foreach(var n in candidate.Nodes){Rect r=RoomRect(n);foreach(var old in oldNodes)if(Overlap(r,RoomRect(old)))bad=true;foreach(var old in oldCorridors)if(Overlap(r,old))bad=true;}
                foreach(var r in nextCorridors){foreach(var old in oldNodes)if(Overlap(r,RoomRect(old)))bad=true;foreach(var old in oldCorridors)if(Overlap(r,old))bad=true;}
                foreach(var old in oldNodes)if(old!=altar&&Overlap(link,RoomRect(old)))bad=true;
                foreach(var old in oldCorridors)if(Overlap(link,old))bad=true;
                for(int i=1;i<candidate.Nodes.Length;i++)if(Overlap(link,RoomRect(candidate.Nodes[i])))bad=true;
                foreach(var r in nextCorridors)if(Overlap(link,r))bad=true;
                // Do not consume the first room's existing doorway with the incoming link.
                foreach(var e in candidate.Edges)if(e.FromId==candidate.Nodes[0].Id && Vector2.Distance(new Vector2(e.From.X,e.From.Y),target)<1f)bad=true;
                if(bad)continue;
                foreach(var r in layout.Rooms){r.Col*=mx;r.Row*=my;r.WorldOffset=new Vector2(r.WorldOffset.x*mx,r.WorldOffset.y*my)+offset;}
                foreach(var e in layout.Edges){e.From=new Vector2(e.From.x*mx,e.From.y*my)+offset;e.To=new Vector2(e.To.x*mx,e.To.y*my)+offset;for(int i=0;i<e.Points.Length;i++)e.Points[i]=new Vector2(e.Points[i].x*mx,e.Points[i].y*my)+offset;}
                return new[]{mouth,target};
            }
            throw new InvalidOperationException("No adjacent stage placement at "+altar.Id+" for "+layout.Id);
        }
    }
}
