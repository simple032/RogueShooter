using UnityEngine;
using RogueShooter.Ai;
using RogueShooter.Demo;
namespace RogueShooter.Art
{
    /// <summary>Ground sector outline uses the exact kind-specific melee radius, angle and committed direction.</summary>
    public sealed class MeleeAttackTelegraph : MonoBehaviour
    {
        const int Segments=32;
        MobFourStateAi _ai;StubEnemy _enemy;LineRenderer _arc;Material _material;
        readonly Vector3[] _points=new Vector3[Segments+3];
        public bool Visible=>_arc!=null&&_arc.enabled;
        public float Radius=>_ai!=null?EnemyCombatRules.MeleeStrikeRangeFor(_ai.KindId):0;
        public float Angle=>_ai!=null?EnemyCombatRules.MeleeStrikeAngleFor(_ai.KindId):0;
        void Awake()
        {
            _ai=GetComponent<MobFourStateAi>();_enemy=GetComponent<StubEnemy>();
            _material=new Material(Shader.Find("Sprites/Default"));
            var go=new GameObject("Melee attack sector");go.transform.SetParent(transform,false);
            _arc=go.AddComponent<LineRenderer>();_arc.sharedMaterial=_material;_arc.useWorldSpace=true;
            _arc.positionCount=_points.Length;_arc.startWidth=_arc.endWidth=.065f;
            _arc.sortingLayerName="Decal";_arc.sortingOrder=100;_arc.enabled=false;
        }
        void LateUpdate()
        {
            if(_ai==null)_ai=GetComponent<MobFourStateAi>();
            bool windup=_ai!=null&&_ai.InMeleeWindup;
            float release=_ai!=null?Time.time-_ai.MeleeReleasedAt:100f;
            _arc.enabled=_ai!=null&&_ai.isActiveAndEnabled&&_ai.RoomCombatAllowed&&!_ai.IsStaggered
                &&(_enemy==null||!_enemy.IsDead)&&(windup||(release>=0&&release<.12f));
            if(!_arc.enabled)return;
            float direction=Mathf.Atan2(_ai.MeleeDirection.y,_ai.MeleeDirection.x)*Mathf.Rad2Deg;
            _points[0]=transform.position;
            for(int i=0;i<=Segments;i++)
            {float a=(direction-Angle*.5f+Angle*i/Segments)*Mathf.Deg2Rad;_points[i+1]=transform.position+new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*Radius;}
            _points[_points.Length-1]=transform.position;_arc.SetPositions(_points);
            Color c=windup?new Color(1f,.73f,.30f,.55f+.35f*_ai.WindupProgress):new Color(1f,.30f,.15f,.9f*(1f-release/.12f));
            _arc.startColor=_arc.endColor=c;
        }
        void OnDisable(){if(_arc!=null)_arc.enabled=false;}
        void OnDestroy(){if(_material!=null)Destroy(_material);}
    }
}
