using UnityEngine;
using RogueShooter.Player;

namespace RogueShooter.Boss
{
    public sealed class BossWardenPresentation : MonoBehaviour
    {
        SpriteRenderer _body,_shield; MaterialPropertyBlock _block;
        string _deathClip,_deathFacing,_shieldFacing; float _deathTime;
        Vector3 _shieldOrigin; Vector3 _shieldScale; bool _shieldAnchored;
        public string CurrentClip {get;private set;}
        public string CurrentFacing {get;private set;}
        public int CurrentFrame {get;private set;}
        public bool Dying=>_deathClip!=null;
        public bool DeathFinished=>Dying&&_deathTime>=.8f;
        public SpriteRenderer Body=>_body;
        public SpriteRenderer DroppedShield=>_shield;
        public void Bind(SpriteRenderer body)
        {
            _body=body;_body.sharedMaterial=Resources.Load<Material>(BossWardenArt.Root+"WardenMaterial");
            if(_body.sharedMaterial==null)throw new System.InvalidOperationException("BOSS v04 material missing");
            _block=new MaterialPropertyBlock();
        }
        public void Show(string clip,string facing,float seconds,bool loop=false)
        {
            if(Dying)return;
            ShowFrame(clip,facing,BossWardenArt.Find(clip).FrameAt(seconds,loop));
            if(clip=="p1_to_p2")ShowShield("shield_drop",facing,Mathf.Clamp(Mathf.FloorToInt(seconds*20f+.0001f),0,23),8);
        }
        public void ShowFrame(string clip,string facing,int frame)
        {
            CurrentClip=clip;CurrentFacing=facing;CurrentFrame=frame;
            _body.sprite=BossWardenArt.Frame(clip,facing,frame);_body.flipX=false;_body.color=Color.white;
            _block.SetTexture("_MainTex",_body.sprite.texture);
            _block.SetFloat("_HasNormal",0);_block.SetFloat("_Form",0);
            // Color is already lit by the source render. Avoid a second flat-normal lighting pass.
            _block.SetColor("_Ambient",Color.white);_block.SetColor("_LightColor",Color.black);
            var emission=BossWardenArt.Emission(clip,facing);
            _block.SetFloat("_HasEmission",emission!=null?1:0);_block.SetFloat("_EmissionGain",.65f);
            if(emission!=null)_block.SetTexture("_EmissionTex",emission);
            _body.SetPropertyBlock(_block);
        }
        void ShowShield(string prefix,string facing,int tick,int release)
        {
            if(tick<release)return;
            if(!_shieldAnchored)
            {
                _shieldOrigin=transform.position;_shieldScale=transform.lossyScale;_shieldFacing=facing;_shieldAnchored=true;
                var prop=new GameObject("BOSS discarded shield (visual only)");
                prop.transform.SetParent(transform.parent.parent,false);prop.transform.position=_shieldOrigin;prop.transform.localScale=_shieldScale;
                _shield=prop.AddComponent<SpriteRenderer>();_shield.sortingLayerName="Entity";
                _shield.sortingOrder=-1;_shield.spriteSortPoint=SpriteSortPoint.Pivot;
            }
            _shield.sprite=BossWardenArt.Shield(prefix,_shieldFacing,tick);
        }
        public void ShowReviewShield(string prefix,string facing,int tick,int release){ShowShield(prefix,facing,tick,release);}
        public Vector3 Muzzle(string clip,string facing,int frame)
        {return transform.TransformPoint(BossWardenArt.Find(clip).Muzzle(facing,frame));}
        public void BeginDeath(bool phase2,string facing)
        {
            if(Dying)return;_deathClip=phase2?"p2_death":"p1_death";_deathFacing=facing;_deathTime=0;
            transform.localPosition=Vector3.zero;ShowFrame(_deathClip,_deathFacing,0);
        }
        void LateUpdate()
        {
            if(!Dying)return;
            if(!RunPause.MenuOpen)_deathTime=Mathf.Min(.8f,_deathTime+Time.unscaledDeltaTime);
            ShowFrame(_deathClip,_deathFacing,BossWardenArt.Find(_deathClip).FrameAt(_deathTime));
            if(_deathClip=="p1_death")ShowShield("p1_death_shield",_deathFacing,Mathf.Min(15,Mathf.FloorToInt(_deathTime*20f)),2);
        }
        void OnDestroy(){if(_shield!=null)Destroy(_shield.gameObject);}
    }
}
