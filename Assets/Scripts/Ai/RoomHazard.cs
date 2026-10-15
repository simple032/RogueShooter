using UnityEngine;
using System.Collections.Generic;
using RogueShooter.Demo;
using RogueShooter.Maze;
using RogueShooter.Player;
using RogueShooter.Spawning;
namespace RogueShooter.Ai
{
    public static class RoomHazardRules
    {
        public const float CopperTrackY=0f,SectorX=2.5f,SectorY=-1.5f,
            SectorWarning=1.4f,SectorActive=.6f,SectorRest=5f,SectorDamage=15f,SectorSafeAngle=90f;
    }
    /// <summary>One room clock, then one hit per body per pulse. No loot from hazard kills.</summary>
    public sealed class RoomHazard : MonoBehaviour
    {
        public enum State { Dormant, Warning, Active, Rest, Stopped }
        public State Phase {get;private set;}
        public string RoomId {get;private set;}
        public bool IsSector {get;private set;}
        public int Pulses {get;private set;}
        public int Hits {get;private set;}
        public Vector3 LockedCenter {get;private set;}
        public float GapAngle {get;private set;}
        public float Left=>_left;
        Stage1MazeDemo _demo; MazeNode _room;float _left;bool _refreshTargets=true;
        MeshRenderer _fill;LineRenderer _gap;Material _warningMat,_activeMat;Mesh _footprint;
        readonly HashSet<int> _hit=new HashSet<int>();readonly List<StubEnemy> _targets=new List<StubEnemy>();
        readonly Dictionary<int,Vector3> _previous=new Dictionary<int,Vector3>();
        public static bool MayBind(StageId stage,MazeNodeKind kind)=>stage==StageId.S2&&kind==MazeNodeKind.Mechanism||stage==StageId.S3&&kind==MazeNodeKind.Stronghold;
        public void Bind(Stage1MazeDemo demo,MazeNode room)
        {
            _demo=demo;_room=room;RoomId=room.Id;IsSector=room.Kind==MazeNodeKind.Stronghold;
            _warningMat=new Material(Shader.Find("Sprites/Default")){color=IsSector?new Color(.40f,.68f,.80f,.28f):new Color(.85f,.60f,.20f,.30f)};
            _activeMat=new Material(Shader.Find("Sprites/Default")){color=IsSector?new Color(.90f,.25f,.30f,.48f):new Color(.95f,.35f,.10f,.50f)};
            var centre=new Vector3(room.Center.X,room.Center.Y,0);
            LockedCenter=centre+(IsSector?new Vector3(RoomHazardRules.SectorX,RoomHazardRules.SectorY):Vector3.up*RoomHazardRules.CopperTrackY);
            transform.position=LockedCenter;Vector3 inward=centre-LockedCenter;GapAngle=Mathf.Atan2(inward.y,inward.x)*Mathf.Rad2Deg;
            var go=new GameObject("Danger footprint");go.transform.SetParent(transform,false);_footprint=Footprint();go.AddComponent<MeshFilter>().sharedMesh=_footprint;_fill=go.AddComponent<MeshRenderer>();_fill.sortingOrder=-3;
            if(IsSector){var pts=new Vector3[17];for(int i=0;i<pts.Length;i++)pts[i]=LockedCenter+Direction(GapAngle-45f+i*90f/16f)*S2S3PhaseDress.SectorRadius;_gap=RogueShooter.Boss.FinalBossCombat.Stroke(transform,pts,new Color(.80f,.85f,.66f,.85f),.10f,-2,true);}
            gameObject.AddComponent<RogueShooter.Art.RoomHazardVisual>().Bind(this);
            SetPhase(State.Dormant);
        }
        static Vector3 Direction(float angle){float a=angle*Mathf.Deg2Rad;return new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);}
        Mesh Footprint()
        {
            if(!IsSector){var m=new Mesh();float half=S2S3PhaseDress.GateTrackHalf,w=CopperGate.Width*.5f;m.vertices=new[]{new Vector3(-half,-w),new Vector3(half,-w),new Vector3(half,w),new Vector3(-half,w)};m.triangles=new[]{0,1,2,0,2,3};m.RecalculateBounds();return m;}
            const int count=48;var mesh=new Mesh();var v=new Vector3[count+2];var t=new int[count*3];for(int i=0;i<=count;i++)v[i+1]=Direction(GapAngle+45f+i*270f/count)*S2S3PhaseDress.SectorRadius;for(int i=0;i<count;i++){t[i*3]=0;t[i*3+1]=i+1;t[i*3+2]=i+2;}mesh.vertices=v;mesh.triangles=t;mesh.RecalculateBounds();return mesh;
        }
        public bool Covers(Vector3 point)
        {
            var d=point-LockedCenter;d.z=0;
            if(!IsSector)return Mathf.Abs(d.x)<=S2S3PhaseDress.GateTrackHalf&&Mathf.Abs(d.y)<=CopperGate.Width*.5f;
            if(d.sqrMagnitude>S2S3PhaseDress.SectorRadius*S2S3PhaseDress.SectorRadius)return false;
            if(d.sqrMagnitude<.0001f)return true;
            return Mathf.Abs(Mathf.DeltaAngle(Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg,GapAngle))>RoomHazardRules.SectorSafeAngle*.5f;
        }
        bool Crosses(int id,Vector3 point)
        {
            if(Covers(point))return true;if(!_previous.TryGetValue(id,out var previous))return false;
            int samples=Mathf.CeilToInt(Vector3.Distance(previous,point)/.2f);samples=Mathf.Clamp(samples,1,80);
            for(int i=0;i<=samples;i++)if(Covers(Vector3.Lerp(previous,point,i/(float)samples)))return true;return false;
        }
        void CacheTargets()
        {
            _targets.Clear();_previous.Clear();foreach(var body in Object.FindObjectsOfType<StubEnemy>())if(!body.IsDead&&_room.Contains(body.transform.position.x,body.transform.position.y,0))_targets.Add(body);
        }
        void Remember()
        {
            var p=_demo.PlayerBody;if(p!=null)_previous[p.GetInstanceID()]=p.position;
            foreach(var body in _targets)if(body!=null&&!body.IsDead)_previous[body.GetInstanceID()]=body.transform.position;
        }
        void Contacts()
        {
            var p=_demo.PlayerBody;float damage=IsSector?RoomHazardRules.SectorDamage:CopperGate.Damage;
            if(p!=null){int id=p.GetInstanceID();var vitals=p.GetComponent<PlayerVitals>();if(vitals!=null&&!vitals.IsDown&&!_hit.Contains(id)&&Crosses(id,p.position)){_hit.Add(id);Hits++;vitals.ApplyHit(damage,IsSector?"SECTOR":"COPPER",(p.position-LockedCenter).sqrMagnitude>.0001f?p.position-LockedCenter:Vector3.up);}}
            if(RunPause.IsPaused)return;
            foreach(var body in _targets){if(body==null||body.IsDead)continue;int id=body.GetInstanceID();if(!_hit.Contains(id)&&Crosses(id,body.transform.position)){_hit.Add(id);Hits++;body.TakeHazardDamage(Mathf.RoundToInt(damage));}}
        }
        public void StopForClear(){SetPhase(State.Stopped);_hit.Clear();}
        void SetPhase(State state)
        {
            if(state!=Phase){RogueShooter.Audio.JianHaiAudio.StopThreat(this);if(state==State.Warning)RogueShooter.Audio.JianHaiAudio.Emit(IsSector?"jh_sector_warn":"jh_gate_warn",this);else if(state==State.Active)RogueShooter.Audio.JianHaiAudio.Emit(IsSector?"jh_sector_active":"jh_gate_active",this);}
            Phase=state;
            if(state==State.Warning){Pulses++;_left=IsSector?RoomHazardRules.SectorWarning:CopperGate.WarnSeconds;}
            else if(state==State.Active){_hit.Clear();_left=IsSector?RoomHazardRules.SectorActive:CopperGate.ActiveSeconds;}
            else if(state==State.Rest)_left=IsSector?RoomHazardRules.SectorRest:CopperGate.RestSeconds;
            else _left=0;
            if(_fill!=null){_fill.enabled=state==State.Warning||state==State.Active;_fill.sharedMaterial=state==State.Active?_activeMat:_warningMat;}
            if(_gap!=null)_gap.enabled=state==State.Warning||state==State.Active;
        }
        void OnDestroy(){RogueShooter.Audio.JianHaiAudio.StopOwner(this);if(_warningMat!=null)Destroy(_warningMat);if(_activeMat!=null)Destroy(_activeMat);if(_footprint!=null)Destroy(_footprint);}
        void Update()
        {
            if(_demo==null||RunPause.IsPaused)return;
            var roomState=_demo.RoomPhase(RoomId);if(roomState==CombatRoomPhase.Cleared){if(Phase!=State.Stopped)StopForClear();return;}
            if(Phase==State.Stopped)return;if(roomState!=CombatRoomPhase.Fighting){_refreshTargets=true;return;}
            if(_refreshTargets){CacheTargets();Remember();_refreshTargets=false;}
            if(Phase==State.Dormant)SetPhase(State.Warning);
            float budget=Time.deltaTime;int guard=0;while(budget>0&&guard++<8&&!RunPause.IsPaused){if(Phase==State.Active)Contacts();if(_demo.RoomPhase(RoomId)==CombatRoomPhase.Cleared){StopForClear();break;}float slice=Mathf.Min(budget,_left);budget-=slice;_left-=slice;if(_left>.0001f)break;SetPhase(Phase==State.Warning?State.Active:Phase==State.Active?State.Rest:State.Warning);}
            Remember();transform.position=LockedCenter;
            float progress=1f-_left/(IsSector?RoomHazardRules.SectorWarning:CopperGate.WarnSeconds);
            if(Phase==State.Warning) _warningMat.color=IsSector?new Color(.40f,.68f,.80f,.18f+.20f*Mathf.Abs(Mathf.Sin(progress*18f))):new Color(.85f,.60f,.20f,.18f+.22f*Mathf.Abs(Mathf.Sin(progress*18f)));
            else if(Phase==State.Active) _activeMat.color=IsSector?new Color(.90f,.25f,.30f,.60f):new Color(.95f,.35f,.10f,.65f);
        }
    }
}
