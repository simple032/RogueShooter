using System;
using System.Collections.Generic;
using UnityEngine;

namespace RogueShooter.Boss
{
    /// <summary>v04 art contract. Game clocks own actions, movement and damage.</summary>
    public static class BossWardenArt
    {
        public const string Root = "BossWardenV04/";
        [Serializable] public class Event { public string id; public int tick; }
        [Serializable] public class Direction { public string id; public Vector2[] muzzles; }
        [Serializable] public class Clip
        {
            public string id; public int frames; public int[] ticks;
            public Direction[] directions; public Event[] events;
            public int FrameAt(float seconds, bool loop=false)
            {
                int t=Mathf.Max(0,Mathf.FloorToInt(seconds*20f+.0001f));
                return ticks[loop?t%ticks.Length:Mathf.Min(t,ticks.Length-1)];
            }
            public Vector2 Muzzle(string facing,int frame)
            { foreach(var d in directions)if(d.id==facing)return d.muzzles[Mathf.Clamp(frame,0,frames-1)];return Vector2.zero; }
        }
        [Serializable] public class Contract { public string version; public int hz; public Clip[] clips; }
        static Contract _contract;
        static readonly Dictionary<string,Sprite[]> Sheets=new Dictionary<string,Sprite[]>();
        static readonly Dictionary<string,Texture2D> Emissions=new Dictionary<string,Texture2D>();
        public static Contract Data
        {
            get
            {
                if(_contract==null)
                {
                    var file=Resources.Load<TextAsset>(Root+"contract");
                    if(file==null)throw new InvalidOperationException("BOSS v04 art contract missing");
                    _contract=JsonUtility.FromJson<Contract>(file.text);
                }
                return _contract;
            }
        }
        public static Clip Find(string id)
        { foreach(var c in Data.clips)if(c.id==id)return c;throw new ArgumentException("Unknown BOSS clip "+id); }
        static Sprite[] Sheet(string name)
        {
            if(!Sheets.TryGetValue(name,out var frames))
            {
                frames=Resources.LoadAll<Sprite>(Root+"Sheets/"+name);
                Array.Sort(frames,(a,b)=>string.CompareOrdinal(a.name,b.name));
                if(frames.Length==0)throw new InvalidOperationException("BOSS sheet missing: "+name);
                Sheets.Add(name,frames);
            }
            return frames;
        }
        public static Sprite Frame(string clip,string facing,int frame)
        { var frames=Sheet(clip+"_"+facing);return frames[Mathf.Clamp(frame,0,frames.Length-1)]; }
        public static Texture2D Emission(string clip,string facing)
        {
            string key=clip+"_"+facing;
            if(!Emissions.TryGetValue(key,out var texture))
            {texture=Resources.Load<Texture2D>(Root+"Sheets/"+key+"_e");Emissions[key]=texture;}
            return texture;
        }
        public static Sprite Shield(string prefix,string facing,int tick)
        {return Sheet(prefix+"_"+facing+"_"+(tick/9))[tick%9];}
        public static Sprite Bolt(bool p2,string facing)
        { foreach(var s in Sheet("bolt_p"+(p2?2:1)))if(s.name.EndsWith("_"+facing))return s;return null; }
        public static string Facing(Vector3 d)
        { string[] dirs={"e","ne","n","nw","w","sw","s","se"};int n=Mathf.RoundToInt(Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg/45f);return dirs[(n%8+8)%8]; }
        public static string MoveClip(BossMoveId move)
        {
            switch(move)
            {
                case BossMoveId.StraightShot:return "p1_single";
                case BossMoveId.BurstShot:return "p1_burst";
                case BossMoveId.WarningCharge:return "p1_charge";
                case BossMoveId.ShieldBash:return "p1_bash";
                case BossMoveId.TripleBurst:
                case BossMoveId.TripleShot:return "p2_triple";
                case BossMoveId.LeapSlam:return "p2_leap";
                case BossMoveId.CrossbowBash:return "p2_bash";
                default:throw new ArgumentException("No animation for "+move);
            }
        }
        // Reset static references on scene/restart domain changes; Resources pages load only when used.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){_contract=null;Sheets.Clear();Emissions.Clear();}
    }
}
