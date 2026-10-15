using UnityEngine;
using RogueShooter.Ai;
using RogueShooter.Demo;
using RogueShooter.Player;

namespace RogueShooter.Art
{
    /// <summary>Animated boundary and sweep driven by the actual room hazard clock.</summary>
    public sealed class RoomHazardVisual : MonoBehaviour
    {
        RoomHazard _hazard;Material _material;LineRenderer _rim,_sweep,_core;float _clock;
        readonly Vector3[] _rimPoints=new Vector3[51];readonly Vector3[] _sweepPoints=new Vector3[49];
        public void Bind(RoomHazard hazard)
        {
            _hazard=hazard;_material=new Material(Shader.Find("Sprites/Default"));
            _rim=Line("Danger rim",.10f);_sweep=Line("Danger sweep",.20f);_core=Line("Danger source",.12f);
        }
        LineRenderer Line(string name,float width){var go=new GameObject(name);go.transform.SetParent(transform,false);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=_material;line.useWorldSpace=false;line.sortingOrder=4;line.startWidth=line.endWidth=width;line.enabled=false;return line;}
        static Vector3 Dir(float a){a*=Mathf.Deg2Rad;return new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);}
        void LateUpdate()
        {
            if(_hazard==null||_rim==null)return;if(!RunPause.IsPaused)_clock+=Time.deltaTime;
            var phase=_hazard.Phase;bool warning=phase==RoomHazard.State.Warning, active=phase==RoomHazard.State.Active;
            float duration=_hazard.IsSector?(warning?RoomHazardRules.SectorWarning:RoomHazardRules.SectorActive):(warning?CopperGate.WarnSeconds:CopperGate.ActiveSeconds);
            float progress=Mathf.Clamp01(1f-_hazard.Left/Mathf.Max(.001f,duration));
            _rim.enabled=_sweep.enabled=_core.enabled=warning||active;if(!warning&&!active)return;
            float pulse=.65f+.35f*Mathf.Sin(_clock*14f);
            Color rim=warning?new Color(1f,.72f,.24f,.65f+.30f*pulse):new Color(1f,.30f,.13f,1f);
            Color sweep=active?new Color(1f,.80f,.46f,.95f):new Color(1f,.72f,.24f,.7f);
            _rim.startColor=_rim.endColor=rim;_sweep.startColor=_sweep.endColor=sweep;_core.startColor=_core.endColor=sweep;
            if(_hazard.IsSector)
            {
                float start=_hazard.GapAngle+45f,r=S2S3PhaseDress.SectorRadius;_rimPoints[0]=Vector3.zero;
                for(int i=0;i<49;i++){float a=start+i*270f/48f;_rimPoints[i+1]=Dir(a)*r;_sweepPoints[i]=Dir(a)*r*(active?(.15f+.85f*progress):(.12f+.88f*progress));}
                _rimPoints[50]=Vector3.zero;_rim.positionCount=51;_rim.SetPositions(_rimPoints);_sweep.positionCount=49;_sweep.SetPositions(_sweepPoints);
                _core.positionCount=2;_core.SetPosition(0,Dir(start+135f)*-.12f);_core.SetPosition(1,Dir(start+135f)*.35f);
            }
            else
            {
                float half=S2S3PhaseDress.GateTrackHalf,w=CopperGate.Width*.5f;_rim.positionCount=5;_rim.SetPosition(0,new Vector3(-half,-w));_rim.SetPosition(1,new Vector3(half,-w));_rim.SetPosition(2,new Vector3(half,w));_rim.SetPosition(3,new Vector3(-half,w));_rim.SetPosition(4,new Vector3(-half,-w));
                // The active strip sweeps across its footprint, without moving the hit geometry.
                float y=Mathf.Lerp(-w,w,active?progress:Mathf.PingPong(progress*2f,1f));_sweep.positionCount=2;_sweep.SetPosition(0,new Vector3(-half,y));_sweep.SetPosition(1,new Vector3(half,y));
                _core.positionCount=2;float inset=warning?(1f-progress)*half:0f;_core.SetPosition(0,new Vector3(-half+inset,0));_core.SetPosition(1,new Vector3(half-inset,0));
            }
        }
        void OnDisable(){if(_rim!=null){_rim.enabled=_sweep.enabled=_core.enabled=false;}}
        void OnDestroy(){if(_material!=null)Destroy(_material);}
    }
}
