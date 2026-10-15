using System.Collections.Generic;
using UnityEngine;
using RogueShooter.Maze;
using RogueShooter.Demo;

namespace RogueShooter.Ai
{
    /// <summary>Room-local walking targets and cached detours. Forced motion still uses MazeCollision.</summary>
    public sealed class RoomMobNavigation
    {
        const float Radius=.35f, Cell=.9f;
        const float Inset=Stage1Coin.BodyRadius+Stage1Maze.WalkInset;
        readonly MazeNode _room;
        readonly List<Vector3> _route=new List<Vector3>();
        bool[] _free;
        float[] _cost;
        int[] _parent;
        byte[] _state;
        readonly List<int> _open=new List<int>();
        int _width,_height,_index,_escape=-1;
        Vector3 _origin,_lastGoal;
        float _replanAt;
        public bool Detouring { get; private set; }
        public RoomMobNavigation(MazeNode room){_room=room;}

        public bool Legal(Vector3 p)
        {
            if(_room==null || !_room.Contains(p.x,p.y,Inset))return false;
            for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)
                if(!_room.Walkable(p.x+x*Radius,p.y+y*Radius))return false;
            return true;
        }
        public bool ClearLine(Vector3 from,Vector3 to)
        {
            int n=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(from,to)/.18f));
            for(int i=0;i<=n;i++)if(!Legal(Vector3.Lerp(from,to,(float)i/n)))return false;
            return true;
        }
        // ClearLine without the start sample. A body caught in the rim between the .3
        // slide clearance and the .35 grid clearance does not stand on a legal point,
        // but walking out of that rim is still safe — MoveTo clamps via Slide.
        bool ClearLineAhead(Vector3 from,Vector3 to)
        {
            int n=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(from,to)/.18f));
            for(int i=1;i<=n;i++)if(!Legal(Vector3.Lerp(from,to,(float)i/n)))return false;
            return true;
        }
        // Direction to the nearest free grid cell; gets a rim-caught body moving again.
        Vector3 EscapeDirection(Vector3 from)
        {
            EnsureGrid();
            int cell=Nearest(from,false);
            if(cell<0)return Vector3.zero;
            Vector3 d=Point(cell)-from;d.z=0f;
            return d.sqrMagnitude>.0001f?d.normalized:Vector3.zero;
        }
        public Vector3 KiteDirection(Vector3 from,Vector3 desired,Vector3 toPlayer,bool retreat)
        {
            if(desired.sqrMagnitude<.0001f)return Vector3.zero;
            desired.Normalize();toPlayer.Normalize();
            // Keep one direction while it is safe; this avoids oscillation at a wall.
            // A rim-caught body is not on a legal point, so only the path ahead must be
            // clear; if nothing clears, walk to the nearest free cell instead of freezing.
            bool anchored=Legal(from);
            bool Clear(Vector3 a,Vector3 b)=>anchored?ClearLine(a,b):ClearLineAhead(a,b);
            if(Clear(from,from+desired*.9f))return desired;
            Vector3 best=Vector3.zero;float score=float.NegativeInfinity;
            for(int i=1;i<8;i++)
            {
                float a=i*45*Mathf.Deg2Rad;
                var d=new Vector3(desired.x*Mathf.Cos(a)-desired.y*Mathf.Sin(a),desired.x*Mathf.Sin(a)+desired.y*Mathf.Cos(a),0);
                if(!Clear(from,from+d*.9f))continue;
                float s=Vector3.Dot(d,desired)+(retreat?-Vector3.Dot(d,toPlayer)*1.5f:0f);
                if(s>score){score=s;best=d;}
            }
            if(best.sqrMagnitude>.0001f)return best;
            return anchored?Vector3.zero:EscapeDirection(from);
        }
        public Vector3 NextStep(Vector3 from,Vector3 wanted,float step)
        {
            if(step<=0)return from;
            Vector3 goal=Project(wanted);
            bool anchored=Legal(from);
            if(anchored?ClearLine(from,goal):ClearLineAhead(from,goal))
            {Detouring=false;_route.Clear();return Vector3.MoveTowards(from,goal,step);}
            Detouring=true;
            if(Time.time>=_replanAt || (goal-_lastGoal).sqrMagnitude>.64f || _index>=_route.Count)
            {
                _lastGoal=goal;_replanAt=Time.time+.35f;FindRoute(from,goal);
            }
            while(_index<_route.Count && Vector2.Distance(from,_route[_index])<.12f)_index++;
            if(_index>=_route.Count)
            {
                // Empty route (fragmented free space): a rim-caught body still walks
                // to the nearest free cell instead of freezing on the spot.
                if(!anchored && _route.Count==0 && _escape>=0)return Vector3.MoveTowards(from,Point(_escape),step);
                return from;
            }
            var next=_route[_index];
            // The first hop always leaves the rim/corner even without a clear line back
            // to the body; later hops must stay visible or the stale route is replanned.
            if(_index>0 && anchored && !ClearLine(from,next)){_replanAt=0;return from;}
            return Vector3.MoveTowards(from,next,step);
        }
        Vector3 Project(Vector3 wanted)
        {
            wanted.x=Mathf.Clamp(wanted.x,_room.Center.X-_room.Width*.5f+Inset,_room.Center.X+_room.Width*.5f-Inset);
            wanted.y=Mathf.Clamp(wanted.y,_room.Center.Y-_room.Height*.5f+Inset,_room.Center.Y+_room.Height*.5f-Inset);
            if(Legal(wanted))return wanted;
            EnsureGrid();int cell=Nearest(wanted,false);
            return cell>=0?Point(cell):wanted;
        }
        void EnsureGrid()
        {
            if(_free!=null)return;
            _width=Mathf.FloorToInt((_room.Width-2*Inset)/Cell)+1;
            _height=Mathf.FloorToInt((_room.Height-2*Inset)/Cell)+1;
            _origin=new Vector3(_room.Center.X-_room.Width*.5f+Inset,_room.Center.Y-_room.Height*.5f+Inset,0);
            int count=_width*_height;_free=new bool[count];_cost=new float[count];_parent=new int[count];_state=new byte[count];
            for(int i=0;i<count;i++)_free[i]=Legal(Point(i));
        }
        Vector3 Point(int i)=>_origin+new Vector3(i%_width*Cell,i/_width*Cell,0);
        int Nearest(Vector3 p,bool connected)
        {
            int best=-1;float d=float.PositiveInfinity;
            for(int i=0;i<_free.Length;i++)
            {
                float s=(Point(i)-p).sqrMagnitude;
                if(!_free[i] || s>=d || (connected && !ClearLine(p,Point(i))))continue;
                best=i;d=s;
            }
            return best;
        }
        void FindRoute(Vector3 from,Vector3 goal)
        {
            EnsureGrid();_route.Clear();_index=0;_open.Clear();
            int start=Nearest(from,true),end=Nearest(goal,true);
            // A rim-caught or cornered body has no clear line to any grid cell; the
            // nearest free cell still starts the route out of the dead spot.
            if(start<0)start=Nearest(from,false);
            if(end<0)end=Nearest(goal,false);
            _escape=start;
            if(start<0 || end<0)return;
            for(int i=0;i<_free.Length;i++){_cost[i]=float.PositiveInfinity;_parent[i]=-1;_state[i]=0;}
            _cost[start]=0;_open.Add(start);_state[start]=1;
            while(_open.Count>0)
            {
                int bi=0;float score=float.PositiveInfinity;
                for(int j=0;j<_open.Count;j++)
                {int k=_open[j];float f=_cost[k]+Vector2.Distance(Point(k),Point(end));if(f<score){bi=j;score=f;}}
                int cur=_open[bi];_open.RemoveAt(bi);_state[cur]=2;
                if(cur==end)
                {
                    for(int k=end;k>=0;k=_parent[k])_route.Add(Point(k));
                    _route.Reverse();if(ClearLine(_route[_route.Count-1],goal))_route.Add(goal);
                    return;
                }
                int x=cur%_width,y=cur/_width;
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                {
                    int xx=x+dx,yy=y+dy;
                    if((dx==0&&dy==0) || xx<0||yy<0||xx>=_width||yy>=_height)continue;
                    int n=yy*_width+xx;
                    if(!_free[n] || _state[n]==2)continue;
                    if(dx!=0&&dy!=0 && (!_free[y*_width+xx]||!_free[yy*_width+x]))continue;
                    if(!ClearLine(Point(cur),Point(n)))continue;
                    float cost=_cost[cur]+Cell*(dx!=0&&dy!=0?1.414214f:1f);
                    if(cost>=_cost[n])continue;
                    _cost[n]=cost;_parent[n]=cur;
                    if(_state[n]==0){_state[n]=1;_open.Add(n);}
                }
            }
        }
    }
}
