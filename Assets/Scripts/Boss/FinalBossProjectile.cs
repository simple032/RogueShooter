using UnityEngine;
using RogueShooter.Maze;
using RogueShooter.Player;
namespace RogueShooter.Boss
{
    public sealed class FinalBossProjectile : MonoBehaviour
    {
        BossFightDriver _owner; Vector3 _dir; float _speed,_distance,_radius; BossMoveId _move; bool _dead;int _group,_moveIndex;
        Transform _visual;Vector3 _muzzleOffset;float _visualAge;LineRenderer _outline,_shaft;
        static Sprite _glow;
        public float HitRadius=>_radius;
        public Vector3 VisualPoint=>_visual!=null?_visual.position:transform.position;
        public int VolleyGroup=>_group;
        public Vector3 Direction=>_dir;
        public BossMoveId Move=>_move;
        public void Launch(BossFightDriver owner,Vector3 at,Vector3 dir,BossMoveId move,int group=0)
        { _owner=owner;_dir=dir.normalized;_move=move;_group=group;_moveIndex=owner.Brain.MoveIndex;_radius=FinalBossRules.ProjectileRadius(move);_speed=FinalBossRules.Speed(move);transform.position=at; }
        public void BindVisual(Transform visual)
        {
            _visual=visual;_muzzleOffset=visual.position-transform.position;
            _outline=Line("Bolt contrast edge",new Color(.035f,.025f,.02f,1),.14f,4);
            _shaft=Line("Bolt bright spine",FinalBossRules.IsP2Ranged(_move)?new Color(.7f,1f,.9f):new Color(1f,.88f,.5f),.065f,5);
            var light=new GameObject("Bolt collision core");light.transform.SetParent(visual,false);
            var glow=light.AddComponent<SpriteRenderer>();glow.sprite=GlowSprite();glow.sortingLayerName="Entity";glow.sortingOrder=4;
            glow.color=FinalBossRules.IsP2Ranged(_move)?new Color(.4f,1f,.85f,.75f):new Color(1f,.7f,.25f,.75f);
            light.transform.localScale=Vector3.one*(_radius*2f);UpdateVisual(0);
        }
        LineRenderer Line(string name,Color color,float width,int order)
        {var line=new GameObject(name).AddComponent<LineRenderer>();line.transform.SetParent(transform,false);line.useWorldSpace=true;line.positionCount=2;line.startWidth=line.endWidth=width;line.sharedMaterial=RogueShooter.Demo.Stage1IsoArt.BandMaterial(color);line.sortingLayerName="Entity";line.sortingOrder=order;return line;}
        static Sprite GlowSprite()
        {if(_glow!=null)return _glow;const int n=32;var tex=new Texture2D(n,n,TextureFormat.RGBA32,false);var pixels=new Color32[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++){float r=new Vector2((x+.5f)/n*2-1,(y+.5f)/n*2-1).magnitude;pixels[y*n+x]=new Color32(255,255,255,(byte)Mathf.RoundToInt(255*Mathf.Clamp01(1-r)));}
            tex.SetPixels32(pixels);tex.Apply(false,true);_glow=Sprite.Create(tex,new Rect(0,0,n,n),new Vector2(.5f,.5f),n);return _glow;}
        void UpdateVisual(float dt)
        {if(_visual==null)return;_visualAge+=dt;_visual.position=transform.position+Vector3.Lerp(_muzzleOffset,Vector3.up*.85f,Mathf.Clamp01(_visualAge/.12f));
            var a=_visual.position-_dir*.36f;var b=_visual.position+_dir*.20f;_outline.SetPosition(0,a);_outline.SetPosition(1,b);_shaft.SetPosition(0,a);_shaft.SetPosition(1,b);}
        void Update()
        {
            if(_dead)return;
            if(_owner==null||_owner.FightSettled){Clear();return;}
            if(RunPause.IsPaused)return;
            float step=Mathf.Min(_speed*Time.deltaTime,FinalBossRules.ShotRange-_distance);
            Vector3 from=transform.position,to=from+_dir*step;
            float wall=MazeCollision.FirstBlockT(_owner.Maze,null,from,to,_radius);
            float hit=_owner.PlayerBody!=null?MazeCollision.CircleEntryT(_owner.PlayerBody.position,from,to,FinalBossRules.PlayerRadius+_radius):2f;
            if(wall<=1f&&wall<=hit){Clear();return;}
            if(hit<=1f){_owner.TryAttackHit(_move,_group,_moveIndex,_dir);Clear();return;}
            transform.position=to;_distance+=step;UpdateVisual(Time.deltaTime);
            if(_distance>=FinalBossRules.ShotRange||!_owner.InArena(to,.12f))Clear();
        }
        public void Clear(){if(_dead)return;_dead=true;if(_owner!=null)_owner.ProjectileEnded(this);Destroy(gameObject);}
    }
}
