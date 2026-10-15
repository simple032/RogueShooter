using System;
using System.Collections.Generic;
using UnityEngine;
using RogueShooter.Maze;
using RogueShooter.Spawning;

namespace RogueShooter.Demo
{
    /// <summary>Small/medium/large ritual altars. One shared ritual vocabulary (chipped
    /// court, lit rune ring, stepped dais, fire basin, guide fires), three silhouettes:
    /// S1 a wayside shrine with a rune stele, S2 twin copper obelisks with watch fires,
    /// S3 a grand bone monolith with gold-capped spires and court pillars.</summary>
    public sealed class StageAltarView : MonoBehaviour
    {
        public const float IgnitionSeconds = 1.8f;
        public const float InteractRadius = 2.4f;
        public bool IsLit { get; private set; }
        public bool IsIgniting { get; private set; }
        public float Ignition01 { get; private set; }
        public StageId Stage { get; private set; }
        public bool ExitReady { get; private set; }
        float _elapsed, _fireClock, _exitElapsed;
        Action _done; bool _audioContact;
        Color _warm;
        readonly List<Material> _owned = new List<Material>();
        readonly List<Mesh> _meshes = new List<Mesh>();
        readonly List<Material> _runes = new List<Material>();
        readonly List<Material> _gild = new List<Material>();
        readonly List<Material> _path = new List<Material>();
        readonly List<SpriteRenderer> _flames = new List<SpriteRenderer>();
        readonly List<Vector3> _flameScales = new List<Vector3>();
        Sprite[] _frames;
        Material _wash;

        public static StageAltarView Create(MazeNode node, StageId stage, Vector2 outlet, Transform parent, List<GameObject> world)
        {
            var go=new GameObject("Altar_"+node.Id);go.transform.SetParent(parent,false);
            go.transform.position=new Vector3(node.Center.X,node.Center.Y,0f);
            var view=go.AddComponent<StageAltarView>();view.Stage=stage;view.Build(node,outlet);world.Add(go);return view;
        }
        public bool BeginIgnition(Action done)
        {
            if(IsLit||IsIgniting)return false;
            _done=done;_elapsed=0f;_audioContact=false;IsIgniting=true;RogueShooter.Audio.JianHaiAudio.Emit("jh_altar_ignite_start",this);return true;
        }
        public void SetExitReady(bool ready) { if(ExitReady!=ready)_exitElapsed=0f;ExitReady=ready;RefreshLight(); }
        void Update()
        {
            if(IsIgniting)
            {
                _elapsed+=Time.deltaTime;Ignition01=Mathf.Clamp01(_elapsed/IgnitionSeconds);
                if(!_audioContact&&Ignition01>=.60f){_audioContact=true;RogueShooter.Audio.JianHaiAudio.Emit((int)Stage==1?"jh_altar_ignite_contact_small":(int)Stage==2?"jh_altar_ignite_contact_medium":"jh_altar_ignite_contact_large",this);}
                if(Ignition01>=1f){RogueShooter.Audio.JianHaiAudio.Emit("jh_altar_shelter",this);IsIgniting=false;IsLit=true;var done=_done;_done=null;done?.Invoke();}
            }
            _fireClock+=Time.deltaTime;if(ExitReady)_exitElapsed+=Time.deltaTime;RefreshLight();
        }
        void RefreshLight()
        {
            float growth=IsLit?1f:Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.60f,.98f,Ignition01));
            for(int i=0;i<_flames.Count;i++)
            {
                var sr=_flames[i];float local=Mathf.Clamp01((growth-(i==0?0f:.28f))*1.4f);
                sr.enabled=local>.01f;
                if(_frames!=null)sr.sprite=_frames[(Mathf.FloorToInt(_fireClock*10f)+i*2)%_frames.Length];
                sr.transform.localScale=Vector3.Scale(_flameScales[i],new Vector3(.4f+.6f*local,.08f+.92f*local,1));
            }
            for(int i=0;i<_runes.Count;i++)
            {
                float light=IsLit?1f:Mathf.Clamp01((Ignition01-i*.012f)*2f);
                _runes[i].color=Color.Lerp(new Color(.24f,.26f,.29f,1),new Color(_warm.r*.75f,_warm.g*.75f,_warm.b*.75f,1),light);
            }
            for(int i=0;i<_gild.Count;i++)
            {
                float light=IsLit?1f:Mathf.Clamp01((Ignition01-i*.012f)*2f);
                _gild[i].color=Color.Lerp(new Color(.34f,.29f,.16f,1),new Color(.95f,.80f,.45f,1),light);
            }
            for(int i=0;i<_path.Count;i++)_path[i].color=Color.Lerp(new Color(.25f,.28f,.31f,1),new Color(_warm.r*.70f,_warm.g*.70f,_warm.b*.70f,1),ExitReady?Mathf.Clamp01(_exitElapsed*5f-i):0f);
            if(_wash!=null)_wash.color=new Color(_warm.r,_warm.g,_warm.b,.12f*growth);
        }
        void Build(MazeNode node,Vector2 outlet)
        {
            int tier=(int)Stage;
            // The three altars share one ritual vocabulary but read at a glance by
            // silhouette, accent metal and fire color, so a player never confuses them.
            Color top,left,right,bevel,accent,flame;
            if(Stage==StageId.S1)
            {
                _warm=new Color(1f,.63f,.22f);
                top=new Color(.385f,.375f,.355f);left=new Color(.20f,.195f,.175f);right=new Color(.27f,.26f,.235f);
                bevel=new Color(.50f,.485f,.45f);accent=new Color(.47f,.45f,.40f);flame=new Color(1f,.88f,.70f);
            }
            else if(Stage==StageId.S2)
            {
                _warm=new Color(1f,.44f,.15f);
                top=new Color(.34f,.38f,.42f);left=new Color(.18f,.21f,.24f);right=new Color(.245f,.275f,.30f);
                bevel=new Color(.48f,.50f,.50f);accent=new Color(.58f,.42f,.26f);flame=new Color(1f,.50f,.30f);
            }
            else
            {
                _warm=new Color(1f,.82f,.50f);
                top=new Color(.47f,.50f,.545f);left=new Color(.27f,.31f,.36f);right=new Color(.36f,.40f,.45f);
                bevel=new Color(.62f,.64f,.66f);accent=new Color(.68f,.58f,.36f);flame=new Color(1.3f,1.12f,.9f);
            }
            // --- layered court floor: apron veil, paved ring, sanctum slab, causeway ---
            float courtW=5.2f+(tier-1)*1.8f,courtH=3.2f+(tier-1)*1.15f;
            Shape("ritual apron",Diamond(courtW*1.26f,courtH*1.28f),new Color(.25f,.28f,.30f,.08f),-682);
            Shape("ritual court",Diamond(courtW,courtH),new Color(.25f,.28f,.30f,.10f),-680);
            var paving=Diamond(courtW*.68f,courtH*.68f);
            for(int i=0;i<4;i++)
                Line("court paving joint",paving[i],paving[(i+1)%4],.03f,new Color(.16f,.18f,.20f,.55f),-679);
            float plateW=2.2f+tier*.30f,plateH=1.35f+tier*.20f;
            Shape("sanctum plate",Diamond(plateW,plateH),new Color(.22f,.24f,.27f,.17f),-678);
            var plateRim=Diamond(plateW,plateH);
            for(int i=0;i<4;i++)
                Line("sanctum plate rim",Vector2.Lerp(plateRim[i],plateRim[(i+1)%4],.05f),Vector2.Lerp(plateRim[i],plateRim[(i+1)%4],.95f),.035f,new Color(.52f,.55f,.58f,.55f),-677);
            // The processional causeway tucks under the front step and runs out
            // past the rim, so the dais gains an approach instead of sitting on a rug.
            float walkW=.75f+tier*.15f,walkTop=-.60f,walkBottom=-(courtH*1.34f);
            Shape("processional strip",new[]{new Vector2(-walkW,walkTop),new Vector2(walkW,walkTop),new Vector2(walkW,walkBottom),new Vector2(-walkW,walkBottom)},new Color(.22f,.24f,.27f,.14f),-678);
            for(float yj=walkTop-.95f;yj>walkBottom+.35f;yj-=.95f)
                Line("processional joint",new Vector2(-walkW,yj),new Vector2(walkW,yj),.028f,new Color(.15f,.17f,.19f,.55f),-677);
            Line("processional edge L",new Vector2(-walkW,walkTop),new Vector2(-walkW,walkBottom),.025f,new Color(.42f,.44f,.47f,.45f),-677);
            Line("processional edge R",new Vector2(walkW,walkTop),new Vector2(walkW,walkBottom),.025f,new Color(.42f,.44f,.47f,.45f),-677);
            if(tier>=3)
            {
                var flags=Diamond(courtW*1.16f,courtH*1.18f);
                for(int i=0;i<4;i++)
                    Shape("court flagstone "+i,Shift(Diamond(.5f,.32f),flags[i]),new Color(.20f,.22f,.25f,.12f),-679);
            }
            var outline=Diamond(courtW,courtH);
            Color rimA=Stage==StageId.S1?new Color(.43f,.42f,.35f,.70f):Stage==StageId.S2?new Color(.44f,.40f,.33f,.70f):new Color(.47f,.44f,.34f,.70f);
            for(int i=0;i<4;i++)
            {
                Vector2 a0=outline[i],b0=outline[(i+1)%4];
                Line("ritual rim",Vector2.Lerp(a0,b0,.07f),Vector2.Lerp(a0,b0,.46f),.07f,rimA,-677);
                Line("ritual chipped rim",Vector2.Lerp(a0,b0,.54f),Vector2.Lerp(a0,b0,.93f),.06f,new Color(.33f,.36f,.38f,.75f),-677);
            }
            if(tier>=2)
            {
                // Kept stony when unlit so the glow belongs to the rune ring alone.
                Color innerRim=Stage==StageId.S2?new Color(.40f,.38f,.33f,.45f):new Color(.44f,.42f,.35f,.45f);
                var inner=Diamond(courtW*.5f,courtH*.5f);
                for(int i=0;i<4;i++)
                    Line("ritual inner rim",Vector2.Lerp(inner[i],inner[(i+1)%4],.12f),Vector2.Lerp(inner[i],inner[(i+1)%4],.88f),.05f,innerRim,-673);
            }
            int runeCount=12+(tier-1)*6;
            for(int i=0;i<runeCount;i++)
            {
                float t=i*Mathf.PI*2/runeCount;Vector2 p=new Vector2(Mathf.Cos(t)*courtW*.815f,Mathf.Sin(t)*courtH*.82f);
                if(tier==1)
                {
                    Vector2 tg=new Vector2(-Mathf.Sin(t),Mathf.Cos(t));
                    _runes.Add(Line("ritual rune",p-tg*.11f,p+tg*.11f,.05f,Color.gray,-674));
                }
                else if(tier==2)
                    _runes.Add(Shape("ritual rune",new[]{p+new Vector2(-.17f,-.05f),p+new Vector2(.17f,-.05f),p+new Vector2(.17f,.05f),p+new Vector2(-.17f,.05f)},Color.gray,-674));
                else
                    _runes.Add(Shape("ritual rune",Shift(Diamond(.15f,.15f),p),Color.gray,-674));
            }
            // --- stepped dais with an approach stair ---
            float foot=1.45f+tier*.30f;
            Stage1IsoArt.PlaceMesh("altar contact",transform.position+Vector3.down*.2f,new Vector3(foot*1.28f,foot*.62f,1),Stage1IsoArt.SoftDiscMesh(),Own(new Color(0,0,0,.30f)),-650,transform);
            Color edge=Color.Lerp(bevel,accent,.30f);
            for(int i=0;i<tier;i++)
                Tier(foot-i*.30f,i*.38f,i<tier-1?foot-(i+1)*.30f:0f,i==tier-1?Color.Lerp(top,bevel,.35f):top,left,right,bevel,edge,-8+i*5);
            float sw=foot*.52f,frontY=-(foot*.5f);
            for(int s=0;s<(tier>=3?2:1);s++)
            {
                float sh=sw*.5f;Vector2 at=new Vector2(0,frontY-sh*.42f);
                Shape("altar step top "+s,Shift(Diamond(sw,sh),at),new Color(top.r*.92f,top.g*.92f,top.b*.92f),s*2);
                Shape("altar step left "+s,new[]{at+new Vector2(-sw,0),at+new Vector2(0,-sh),at+new Vector2(0,-sh-.18f),at+new Vector2(-sw,-.18f)},left,s*2+1);
                Shape("altar step right "+s,new[]{at+new Vector2(0,-sh),at+new Vector2(sw,0),at+new Vector2(sw,-.18f),at+new Vector2(0,-sh-.18f)},right,s*2+1);
                frontY=at.y-sh-.18f;sw*=.66f;
            }
            // --- tier-specific verticals and fire identity ---
            _frames=new Sprite[6];for(int i=0;i<6;i++)_frames[i]=Stage1IsoArt.LoadSprite("Assets/Art/JianHai/Iso/FX/jh_iso_fx_brazier_fire_0"+i+".png");
            if(Stage==StageId.S1)
            {
                // Wayside shrine: one blunt rune stele behind the basin.
                Shape("stele plinth",new[]{new Vector2(-.30f,.40f),new Vector2(.30f,.40f),new Vector2(.24f,.62f),new Vector2(-.24f,.62f)},top,3);
                Shape("stele shaft",new[]{new Vector2(-.20f,.62f),new Vector2(.20f,.62f),new Vector2(.14f,1.70f),new Vector2(0,1.86f),new Vector2(-.16f,1.74f)},top,4);
                Shape("stele shade",new[]{new Vector2(.20f,.62f),new Vector2(.14f,1.70f),new Vector2(0,1.86f)},new Color(right.r*.75f,right.g*.75f,right.b*.75f),5);
                Line("stele chip",new Vector2(-.13f,1.62f),new Vector2(-.02f,1.80f),.03f,new Color(.22f,.21f,.19f),6);
                _runes.Add(Shape("stele rune",Shift(Diamond(.14f,.18f),new Vector2(0,1.56f)),Color.gray,6));
                Basin(.08f,.36f,accent,5);
                Flame(new Vector2(0,.30f),new Vector3(.75f,.95f,1),flame);
            }
            else if(Stage==StageId.S2)
            {
                // Twin obelisks planted on the dais shoulders, copper fittings and watch fires.
                foreach(float sign in new[]{-1f,1f})
                {
                    float x=sign*.92f;
                    Shape("obelisk plinth "+sign,new[]{new Vector2(x-.22f,.02f),new Vector2(x+.22f,.02f),new Vector2(x+.18f,.24f),new Vector2(x-.18f,.24f)},top,4);
                    Shape("obelisk shaft "+sign,new[]{new Vector2(x-.15f,.24f),new Vector2(x+.15f,.24f),new Vector2(x+.10f,2.00f),new Vector2(x-.10f,2.00f)},top,5);
                    Shape("obelisk shade "+sign,new[]{new Vector2(x+.06f,.28f),new Vector2(x+.12f,.28f),new Vector2(x+.08f,1.96f),new Vector2(x+.02f,1.96f)},new Color(right.r*.75f,right.g*.75f,right.b*.75f),6);
                    Line("obelisk patina "+sign,new Vector2(x-.13f,.98f),new Vector2(x+.13f,.98f),.06f,new Color(.22f,.34f,.30f),7);
                    Line("obelisk copper band "+sign,new Vector2(x-.13f,1.40f),new Vector2(x+.13f,1.40f),.05f,accent,7);
                    _runes.Add(Shape("obelisk rune "+sign,Shift(Diamond(.08f,.11f),new Vector2(x,1.18f)),Color.gray,7));
                    // Capital, lip slab and an open bowl: the mouth stays readable cold.
                    Shape("obelisk capital "+sign,new[]{new Vector2(x-.17f,2.00f),new Vector2(x+.17f,2.00f),new Vector2(x+.15f,2.14f),new Vector2(x-.15f,2.14f)},accent,8);
                    Shape("obelisk lip "+sign,new[]{new Vector2(x-.19f,2.14f),new Vector2(x+.19f,2.14f),new Vector2(x+.15f,2.26f),new Vector2(x-.15f,2.26f)},new Color(.66f,.48f,.30f),8);
                    Shape("obelisk bowl "+sign,Shift(Ellipse(.17f,.075f,0f),new Vector2(x,2.32f)),new Color(.46f,.34f,.24f),9);
                    Shape("obelisk bowl mouth "+sign,Shift(Ellipse(.11f,.04f,0f),new Vector2(x,2.33f)),new Color(.16f,.11f,.07f),9);
                    Flame(new Vector2(x,2.46f),new Vector3(.55f,.72f,1),flame);
                }
                Basin(.48f,.46f,accent,6);
                Flame(new Vector2(0,.62f),new Vector3(1.30f,1.95f,1),flame);
            }
            else
            {
                // Grand sanctum: broken gold-rune monolith, gold-capped spires, court pillars.
                foreach(float sign in new[]{-1f,1f})
                {
                    float x=sign*1.05f;
                    Shape("spire plinth "+sign,new[]{new Vector2(x-.20f,.02f),new Vector2(x+.20f,.02f),new Vector2(x+.16f,.22f),new Vector2(x-.16f,.22f)},top,4);
                    Shape("spire shaft "+sign,new[]{new Vector2(x-.13f,.22f),new Vector2(x+.13f,.22f),new Vector2(x+.09f,2.05f),new Vector2(x-.09f,2.05f)},top,5);
                    Shape("spire shade "+sign,new[]{new Vector2(x+.04f,.26f),new Vector2(x+.10f,.26f),new Vector2(x+.06f,2.00f),new Vector2(x,2.00f)},new Color(right.r*.75f,right.g*.75f,right.b*.75f),6);
                    Line("spire gold band "+sign,new Vector2(x-.11f,.95f),new Vector2(x+.11f,.95f),.05f,accent,7);
                    _runes.Add(Shape("spire rune "+sign,Shift(Diamond(.075f,.10f),new Vector2(x,1.45f)),Color.gray,7));
                    Shape("spire cap "+sign,new[]{new Vector2(x-.14f,2.05f),new Vector2(x+.14f,2.05f),new Vector2(x,2.44f)},new Color(.78f,.68f,.44f),8);
                }
                Shape("monolith plinth",new[]{new Vector2(-.40f,1.16f),new Vector2(.40f,1.16f),new Vector2(.33f,1.48f),new Vector2(-.33f,1.48f)},top,8);
                Shape("monolith shaft",new[]{new Vector2(-.34f,1.48f),new Vector2(.34f,1.48f),new Vector2(.22f,3.55f),new Vector2(.02f,3.86f),new Vector2(-.26f,3.66f)},top,9);
                Shape("monolith shade",new[]{new Vector2(.34f,1.48f),new Vector2(.22f,3.55f),new Vector2(.02f,3.86f)},new Color(right.r*.75f,right.g*.75f,right.b*.75f),10);
                Line("monolith crack A",new Vector2(-.18f,2.30f),new Vector2(0,2.52f),.03f,new Color(.24f,.27f,.30f),10);
                Line("monolith crack B",new Vector2(.05f,2.92f),new Vector2(.19f,3.12f),.028f,new Color(.24f,.27f,.30f),10);
                foreach(float y in new[]{1.85f,2.45f,3.05f})
                    _gild.Add(Shape("monolith rune",Shift(Diamond(.13f,.16f),new Vector2(0,y)),Color.gray,11));
                Basin(.86f,.55f,accent,7);
                Flame(new Vector2(0,.90f),new Vector3(1.62f,2.55f,1),flame);
                Vector2 c=new Vector2(node.Center.X,node.Center.Y);
                var pillars=new List<GameObject>();
                foreach(float sign in new[]{-1f,1f})
                    S2S3EnvironmentPolish.Pillar(c+new Vector2(sign*3.8f,2.6f),"ritual back "+node.Id,transform,pillars);
            }
            // --- flanking votive bowls and warm light pool ---
            // Votives sit on the paved precinct just outside the rim corners, so the
            // bare ring around the tiled island stays clear of loose props.
            Vector2 vp=new Vector2(courtW*.54f,courtH*.54f);
            foreach(float sign in new[]{-1f,1f})
            {
                Vector2 p=new Vector2(sign*vp.x,vp.y);
                Shape("votive basin "+sign,Shift(Ellipse(.40f+(tier-1)*.06f,.19f+(tier-1)*.02f,0f),p),Stage==StageId.S1?new Color(.35f,.32f,.27f):Stage==StageId.S2?new Color(.40f,.30f,.22f):new Color(.52f,.45f,.28f),-10);
                Flame(p,new Vector3(.62f+tier*.06f,.72f+tier*.08f,1),flame);
            }
            var wash=Stage1IsoArt.PlaceMesh("altar light pool",transform.position+Vector3.up,new Vector3(courtW*.73f,courtH*.66f,1),Stage1IsoArt.SoftDiscMesh(),Own(new Color(1,.5f,.2f,0)),-676,transform);
            _wash=wash.GetComponent<MeshRenderer>().sharedMaterial;
            // An outgoing axis, not a generic cross, makes the next-stage mouth identifiable.
            Vector2 cc=new Vector2(node.Center.X,node.Center.Y),dir=(outlet-cc).normalized;float distance=Vector2.Distance(cc,outlet);
            var into=new List<GameObject>();
            Vector2 guide=cc+dir*(distance-2.2f),side=new Vector2(-dir.y,dir.x)*3.2f;
            Stage1IsoArt.Brazier(guide-side,transform,into);
            Stage1IsoArt.Brazier(guide+side,transform,into);
            if(tier==1)
                foreach(float sign in new[]{-1f,1f})S2S3EnvironmentPolish.Pillar(cc+new Vector2(sign*10f,7f),"ritual "+node.Id,transform,into);
            else
            {
                // S2/S3 altar rooms carry the corner-cut air walls, so the guide
                // pillars flank the outgoing mouth on kept floor instead of
                // standing inside a cut corner.
                Vector2 gate=cc+dir*(distance-3.6f);
                foreach(float sign in new[]{-1f,1f})S2S3EnvironmentPolish.Pillar(gate+side*.6f*sign,"ritual "+node.Id,transform,into);
            }
            RefreshLight();
        }
        void Tier(float w,float y,float wNext,Color top,Color left,Color right,Color bevel,Color edge,int order)
        {
            float h=w*.5f;
            Shape("stone tier top",Shift(Diamond(w,h),new Vector2(0,y)),top,order);
            // Separate convex side quads avoid the triangulation of a concave strip.
            Shape("stone tier left face",new[]{new Vector2(-w,y),new Vector2(0,y-h),new Vector2(0,y-h-.25f),new Vector2(-w,y-.25f)},left,order+1);
            Shape("stone tier right face",new[]{new Vector2(0,y-h),new Vector2(w,y),new Vector2(w,y-.25f),new Vector2(0,y-h-.25f)},right,order+1);
            Line("tier left bevel",new Vector2(-w+.06f,y-.03f),new Vector2(0,y-h+.03f),.035f,bevel,order+2);
            Line("tier right bevel",new Vector2(0,y-h+.03f),new Vector2(w-.06f,y-.03f),.03f,new Color(bevel.r*.9f,bevel.g*.9f,bevel.b*.9f),order+2);
            // The two far edges complete the raised curb; the accent tint tracks the stage metal.
            Line("tier back curb L",new Vector2(-w+.05f,y+.02f),new Vector2(-.04f,y+h-.02f),.03f,edge,order+2);
            Line("tier back curb R",new Vector2(.04f,y+h-.02f),new Vector2(w-.05f,y+.02f),.03f,edge,order+2);
            for(int j=1;j<=3;j++)
            {
                float t=j/4f;
                Line("tier grout",Vector2.Lerp(new Vector2(-w,y),new Vector2(0,y+h),t),Vector2.Lerp(new Vector2(0,y-h),new Vector2(w,y),t),.025f,new Color(.19f,.22f,.25f),order+2);
                Vector2 p=Vector2.Lerp(new Vector2(-w,y),new Vector2(0,y-h),t);
                Line("tier front joint",p,p+Vector2.down*.22f,.023f,new Color(.10f,.12f,.14f),order+2);
            }
            Shape("stone chip",new[]{new Vector2(w*.6f,y-h*.4f),new Vector2(w*.6f+.14f,y-h*.4f-.13f),new Vector2(w*.6f+.04f,y-h*.4f-.18f)},new Color(.20f,.23f,.26f),order+3);
            // One broken corner per level, alternating sides so the wear is not mechanical.
            float side=((int)(y/.38f)&1)==0?-1f:1f;
            Vector2 v=new Vector2(side*w,y);
            Shape("tier corner chip",new[]{v,v+new Vector2(-side*.18f,.07f),v+new Vector2(-side*.22f,-.05f)},new Color(.17f,.20f,.23f),order+3);
            // Contact shade the level above casts on this one; the upper top and
            // faces occlude the back half, so only the front arc is drawn.
            if(wNext>0f)
            {
                float w2=wNext+.08f,h2=w2*.5f+.03f;
                Line("tier under shadow L",new Vector2(-w2,y),new Vector2(0,y-h2),.05f,new Color(.08f,.09f,.11f,.45f),order+2);
                Line("tier under shadow R",new Vector2(0,y-h2),new Vector2(w2,y),.05f,new Color(.08f,.09f,.11f,.45f),order+2);
            }
        }
        void Basin(float y,float rx,Color rim,int embers)
        {
            // The bowl sits on the dais face with a visible wall thickness and an
            // inner lip, so it reads as a raised fire basin instead of a hole.
            // An etched ring around the bowl ties it to the dais top; order 7 keeps
            // it under the monolith plinth (8) and the basin itself (9+).
            var fireRing=Shift(Diamond(rx*1.55f,rx*.85f),new Vector2(0,y-.05f));
            Color ring=new Color(rim.r*.55f,rim.g*.55f,rim.b*.55f);
            for(int i=0;i<4;i++)
                Line("basin fire ring",fireRing[i],fireRing[(i+1)%4],.03f,ring,7);
            Shape("basin shadow",Ellipse(rx*1.25f,rx*.50f,y-rx*.42f),new Color(.12f,.14f,.17f),9);
            Shape("basin wall",Ellipse(rx,rx*.40f,y-.11f),new Color(rim.r*.55f,rim.g*.55f,rim.b*.55f),10);
            Shape("basin stone rim",Ellipse(rx,rx*.40f,y),rim,11);
            Shape("basin inner lip",Ellipse(rx*.88f,rx*.35f,y+.01f),new Color(rim.r*.72f,rim.g*.72f,rim.b*.72f),12);
            Shape("coal bed",Ellipse(rx*.75f,rx*.27f,y+.02f),new Color(.13f,.11f,.09f),13);
            for(int i=0;i<embers;i++)
            {
                float a=i*Mathf.PI*2/embers+.7f;
                Vector2 p=new Vector2(Mathf.Cos(a)*rx*.52f,y+.03f+Mathf.Sin(a)*rx*.17f);
                _runes.Add(Shape("coal ember",Shift(Ellipse(.05f,.032f,0f),p),Color.gray,14));
            }
        }
        void Flame(Vector2 p,Vector3 scale,Color tint)
        {
            var go=new GameObject("altar flame");go.transform.SetParent(transform,false);go.transform.localPosition=p;
            var sr=go.AddComponent<SpriteRenderer>();sr.sortingOrder=18;sr.color=tint;sr.enabled=false;
            _flames.Add(sr);_flameScales.Add(scale);
        }
        Material Own(Color color){var mat=new Material(Shader.Find("Sprites/Default")){color=color};_owned.Add(mat);return mat;}
        Material Shape(string name,Vector2[] pts,Color color,int order)
        {
            var mesh=new Mesh{name=name};var v=new Vector3[pts.Length];var tris=new int[(pts.Length-2)*3];
            for(int i=0;i<pts.Length;i++)v[i]=pts[i];for(int i=0;i<pts.Length-2;i++){tris[i*3]=0;tris[i*3+1]=i+1;tris[i*3+2]=i+2;}
            mesh.vertices=v;mesh.triangles=tris;mesh.RecalculateBounds();_meshes.Add(mesh);var mat=Own(color);
            Stage1IsoArt.PlaceMesh(name,transform.position,Vector3.one,mesh,mat,order,transform);return mat;
        }
        Material Line(string name,Vector2 a,Vector2 b,float width,Color color,int order)
        {
            Vector2 delta=(b-a).normalized,side=new Vector2(-delta.y,delta.x)*width*.5f;
            return Shape(name,new[]{a-side,b-side,b+side,a+side},color,order);
        }
        static Vector2[] Diamond(float w,float h){return new[]{new Vector2(-w,0),new Vector2(0,h),new Vector2(w,0),new Vector2(0,-h)};}
        static Vector2[] Ellipse(float x,float y,float at){var p=new Vector2[32];for(int i=0;i<p.Length;i++){float t=i*Mathf.PI*2/p.Length;p[i]=new Vector2(Mathf.Cos(t)*x,Mathf.Sin(t)*y+at);}return p;}
        static Vector2[] Shift(Vector2[] p,Vector2 offset){for(int i=0;i<p.Length;i++)p[i]+=offset;return p;}
        void OnDestroy(){RogueShooter.Audio.JianHaiAudio.StopOwner(this);foreach(var mat in _owned)if(mat!=null)Destroy(mat);foreach(var mesh in _meshes)if(mesh!=null)Destroy(mesh);}
    }
}
