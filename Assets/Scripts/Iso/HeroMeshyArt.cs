using System;
using System.Collections.Generic;
using UnityEngine;
namespace RogueShooter.Iso
{
    public static class HeroMeshyArt
    {
        public const string Root="HeroArcherMeshyV01/";
        [Serializable] public class Direction { public string id;public Vector2[] muzzles,torchTips;public float stride; }
        [Serializable] public class Clip { public string id;public int frames;public float seconds;public bool loop;public Direction[] directions; }
        [Serializable] public class Contract { public string version;public int hz,width,height,ppu;public Vector2 pivot;public Clip[] clips; }
        static Contract _data;static Material _material;
        static readonly Dictionary<string,Sprite[]> Pages=new Dictionary<string,Sprite[]>();
        public static bool Available=>Resources.Load<TextAsset>(Root+"contract")!=null;
        public static Contract Data { get { if(_data==null)_data=JsonUtility.FromJson<Contract>(Resources.Load<TextAsset>(Root+"contract").text);return _data; } }
        public static Material Material { get {if(_material==null)_material=Resources.Load<Material>(Root+"HeroMaterial");return _material;} }
        public static Clip Find(string id){foreach(var c in Data.clips)if(c.id==id)return c;throw new ArgumentException("Hero clip missing: "+id);}
        public static Direction DirectionData(string clip,string facing){foreach(var d in Find(clip).directions)if(d.id==facing)return d;throw new ArgumentException("Hero facing missing: "+facing);}
        public static Sprite Frame(string clip,string facing,int frame)
        {
            frame=Mathf.Clamp(frame,0,Find(clip).frames-1);string key=clip+"_"+facing+"_"+(frame/16).ToString("00");
            if(!Pages.TryGetValue(key,out var page))
            {page=Resources.LoadAll<Sprite>(Root+"Sheets/"+key);Array.Sort(page,(a,b)=>string.CompareOrdinal(a.name,b.name));if(page.Length==0)throw new InvalidOperationException("Hero sprite page missing: "+key);Pages[key]=page;}
            return page[frame%16];
        }
        public static Sprite Projectile(string facing){return Resources.Load<Sprite>(Root+"Projectiles/arrow_"+facing);}
        public static int RelativeDirection(string aim,Vector3 movement)
        {return ((Index(Facing(movement))-Index(aim))%8+8)%8;}
        public static int Index(string direction){string[] dirs={"e","ne","n","nw","w","sw","s","se"};return Array.IndexOf(dirs,direction);}
        public static string Facing(Vector3 v){string[] dirs={"e","ne","n","nw","w","sw","s","se"};int i=Mathf.RoundToInt(Mathf.Atan2(v.y,v.x)*Mathf.Rad2Deg/45f);return dirs[(i%8+8)%8];}
        public static int DrawGrid(int relative,float progress,float phase){return Mathf.Clamp(relative,0,7)*30+Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(progress)*4),0,4)*6+Mathf.Min(5,Mathf.FloorToInt(Mathf.Repeat(phase,1)*6));}
        public static int ReleaseGrid(int relative,float progress,float seconds,float phase){return Mathf.Clamp(relative,0,7)*48+(progress<.65f?0:24)+Mathf.Clamp(Mathf.FloorToInt(seconds/.05f+.0001f),0,3)*6+Mathf.Min(5,Mathf.FloorToInt(Mathf.Repeat(phase,1)*6));}
        public static void Apply(SpriteRenderer body)
        {
            if(body.sharedMaterial!=Material)body.sharedMaterial=Material;
            var block=new MaterialPropertyBlock();body.GetPropertyBlock(block);block.SetTexture("_MainTex",body.sprite.texture);block.SetColor("_Ambient",Color.white);block.SetColor("_LightColor",Color.black);block.SetFloat("_HasNormal",0);block.SetFloat("_HasEmission",0);block.SetFloat("_Form",0);body.SetPropertyBlock(block);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset(){_data=null;_material=null;Pages.Clear();}
    }
}
