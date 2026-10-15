using UnityEngine;
using RogueShooter.Ai;

namespace RogueShooter.Art
{
    /// <summary>Dashed path, with exactly the collision-clipped preview endpoint.</summary>
    public sealed class ShieldLungeTelegraph : MonoBehaviour
    {
        MobFourStateAi _ai; Material _material;
        readonly LineRenderer[] _dashes=new LineRenderer[16];
        public bool Visible { get { foreach(var line in _dashes) if(line!=null&&line.enabled)return true;return false; } }
        void Awake()
        {
            _ai=GetComponent<MobFourStateAi>();_material=new Material(Shader.Find("Sprites/Default"));
            for(int i=0;i<_dashes.Length;i++)
            {
                var go=new GameObject("Shield lunge dash "+i);go.transform.SetParent(transform,false);
                var line=go.AddComponent<LineRenderer>();line.sharedMaterial=_material;line.useWorldSpace=true;line.sortingOrder=3;
                line.positionCount=2;line.startWidth=line.endWidth=.10f;line.numCapVertices=0;line.enabled=false;_dashes[i]=line;
            }
        }
        void LateUpdate()
        {
            if(_ai==null)_ai=GetComponent<MobFourStateAi>();
            bool show=_ai!=null&&_ai.isActiveAndEnabled&&_ai.RoomCombatAllowed&&!_ai.ShieldBroken&&!_ai.IsStaggered&&(_ai.LungePhase=="跟踪"||_ai.LungePhase=="锁定");
            Vector3 dir=show?_ai.LungeAim.normalized:Vector3.right;Vector3 start=transform.position+dir*.35f;
            float length=show?Mathf.Max(0f,Vector3.Dot(_ai.LungePreviewEnd-start,dir)):0f;
            Color color=_ai!=null&&_ai.LungePhase=="锁定"?new Color(1f,.34f,.16f,.95f):new Color(1f,.78f,.28f,.85f);
            for(int i=0;i<_dashes.Length;i++){float a=i*.48f,b=Mathf.Min(a+.28f,length);var line=_dashes[i];line.enabled=show&&b>a;if(!line.enabled)continue;line.SetPosition(0,start+dir*a);line.SetPosition(1,start+dir*b);line.startColor=line.endColor=color;}
        }
        void OnDisable(){foreach(var line in _dashes)if(line!=null)line.enabled=false;}
        void OnDestroy(){if(_material!=null)Destroy(_material);}
    }
}
