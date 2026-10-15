using System.Collections.Generic;
using UnityEngine;
using RogueShooter.Demo;
using RogueShooter.Player;
using RogueShooter.Boss;
namespace RogueShooter.Audio
{
    /// <summary>Run-owned voices. Rules resolve events; the DSP clock owns music phase.</summary>
    [DefaultExecutionOrder(500)]
    public sealed class JianHaiAudio : MonoBehaviour
    {
        sealed class Voice { public AudioSource source; public JianHaiAudioBank.Cue cue; public Component owner; public int ownerId; public Vector3 at; public bool paused,hadOwner,follow; public float fade=1; public double started; }
        sealed class MusicVoice { public AudioSource source; public string id; public float weight,target; }
        public static JianHaiAudio Live {get;private set;}
        public readonly Dictionary<string,int> Played=new Dictionary<string,int>(),Rejected=new Dictionary<string,int>();
        public string MusicState {get;private set;}
        public int ActiveSfx {get;private set;}
        public int ActiveMusic {get;private set;}
        public int PeakSfx {get;private set;}
        public int PeakMusic {get;private set;}
        public int MissingEvents {get;private set;}
        public int RunGeneration {get;private set;}
        readonly Dictionary<string,JianHaiAudioBank.Cue> _cues=new Dictionary<string,JianHaiAudioBank.Cue>();
        readonly System.Random _random=new System.Random();
        readonly Dictionary<string,int> _variants=new Dictionary<string,int>();
        readonly List<Voice> _voices=new List<Voice>(); readonly List<MusicVoice> _music=new List<MusicVoice>();
        Stage1MazeDemo _demo; Transform _samplePlayer; BossFightDriver _sampleBoss; PlayerMotor2D _motor; JianHaiAudioBank _bank; Transform[] _braziers; StageAltarView[] _altars;
        Voice _fire,_resonance; float _distance,_ambientPoll,_duck; double _origin,_musicPauseAt;
        bool _bound,_settled,_hardPaused,_focus=true;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ClearStatic(){Live=null;}
        public static void BindRun(Stage1MazeDemo demo){var a=demo.GetComponent<JianHaiAudio>();if(a==null)a=demo.gameObject.AddComponent<JianHaiAudio>();a.Bind(demo);}
        public static void Emit(string id,Component owner=null){if(Live!=null)Live.Play(id,owner!=null?owner.transform.position:Vector3.zero,owner,true);}
        public static void EmitAt(string id,Vector3 at,Component owner=null){if(Live!=null)Live.Play(id,at,owner,false);}
        public static void Stop(string id,Component owner=null){if(Live!=null)Live.StopCue(id,owner);}
        public static void StopThreat(Component owner){if(Live!=null)foreach(var v in Live._voices)if(v.cue!=null&&v.ownerId==(owner!=null?owner.GetInstanceID():0)&&v.cue.bus=="Threat")Live.Release(v);}
        public static void StopOwner(Component owner){if(Live!=null)foreach(var v in Live._voices)if(v.cue!=null&&v.ownerId==(owner!=null?owner.GetInstanceID():0)&&(v.cue.loop||v.cue.bus=="Threat"))Live.Release(v);}
        public static void BeforeRebuild(){if(Live!=null)Live.ResetRun();}
        public static void Settle(string id)
        {
            if(Live==null||Live._settled)return;Live._settled=true;
            foreach(var v in Live._voices)if(v.cue!=null&&v.cue.bus!="UI"&&v.cue.bus!="CriticalConfirm")Live.Release(v);
            Live.Play(id,Vector3.zero,null,false);
        }
        void Awake()
        {
            Live=this;_bank=Resources.Load<JianHaiAudioBank>("JianHaiAudio/Bank");
            if(_bank==null){Debug.LogWarning("[JianHaiAudio] Bank missing; sound disabled.");return;}
            foreach(var c in _bank.cues)_cues[c.id]=c;
            for(int i=0;i<27;i++)_voices.Add(new Voice{source=Source("SFX "+i)});
            foreach(var s in _bank.stems){var source=Source("Music "+s.id);source.clip=s.clip;source.loop=true;_music.Add(new MusicVoice{id=s.id,source=source});}
            if(FindObjectOfType<AudioListener>()==null&&Camera.main!=null)Camera.main.gameObject.AddComponent<AudioListener>();
        }
        AudioSource Source(string name){var go=new GameObject(name);go.transform.SetParent(transform,false);var s=go.AddComponent<AudioSource>();s.playOnAwake=false;s.spatialBlend=0;s.dopplerLevel=0;s.pitch=1;return s;}
        void Bind(Stage1MazeDemo demo)
        {
            ResetRun();_demo=demo;_bound=_bank!=null;RunGeneration++;_origin=AudioSettings.dspTime+.08;
            _motor=demo.PlayerBody!=null?demo.PlayerBody.GetComponent<PlayerMotor2D>():null;if(_motor!=null)_motor.Walked+=Walked;
            _altars=FindObjectsOfType<StageAltarView>();var props=new List<Transform>();
            foreach(var t in demo.GetComponentsInChildren<Transform>())if(t.name=="Brazier")props.Add(t);_braziers=props.ToArray();
            Play("jh_amb_dungeon_air",Vector3.zero,this,false);SetMusic("exploration");
        }
        /// <summary>Bind the isolated playable BOSS sample to the same bank, buses and DSP music.</summary>
        public void BindBossSample(Transform player,BossFightDriver boss)
        {
            ResetRun();_samplePlayer=player;_sampleBoss=boss;_bound=_bank!=null;RunGeneration++;_origin=AudioSettings.dspTime+.08;
            _motor=player!=null?player.GetComponent<PlayerMotor2D>():null;if(_motor!=null)_motor.Walked+=Walked;
            _altars=System.Array.Empty<StageAltarView>();_braziers=System.Array.Empty<Transform>();
            Play("jh_amb_dungeon_air",Vector3.zero,this,false);SetMusic("exploration");
        }
        public void ResetRun()
        {
            _bound=false;if(_motor!=null)_motor.Walked-=Walked;_motor=null;_demo=null;_samplePlayer=null;_sampleBoss=null;
            foreach(var v in _voices){v.source.Stop();v.source.clip=null;v.cue=null;v.owner=null;v.paused=false;}
            foreach(var m in _music){m.source.Stop();m.source.volume=0;m.weight=m.target=0;}
            _fire=_resonance=null;_distance=_ambientPoll=_duck=0;_settled=_hardPaused=false;MusicState=null;
            ActiveSfx=ActiveMusic=PeakSfx=PeakMusic=MissingEvents=0;Played.Clear();Rejected.Clear();_variants.Clear();
        }
        void Walked(Vector2 delta){_distance+=delta.magnitude;if(_distance<1.5f)return;_distance%=1.5f;Emit("jh_player_step",_motor);}
        static void Count(Dictionary<string,int> counts,string id){counts.TryGetValue(id,out var n);counts[id]=n+1;}
        public int CountPlayed(string id)=>Played.TryGetValue(id,out var n)?n:0;
        public int CountActive(string id){int n=0;foreach(var v in _voices)if(v.cue!=null&&v.cue.id==id)n++;return n;}
        bool World(JianHaiAudioBank.Cue c)=>c.bus=="Threat"||c.bus=="EnemyAction"||c.bus=="Impact"||c.bus=="PlayerMove";
        Voice Play(string id,Vector3 at,Component owner,bool follow)
        {
            if(!_bound)return null;if(!_cues.TryGetValue(id,out var cue)){MissingEvents++;Debug.LogWarning("[JianHaiAudio] Unknown event "+id);return null;}
            if((_settled&&World(cue))||(_hardPaused&&cue.bus!="UI")||(RunPause.InteractOpen&&World(cue))){Count(Rejected,id);return null;}
            int oid=owner!=null?owner.GetInstanceID():0;Voice replace=null,free=null,lowest=null;int busCount=0,sfxCount=0;
            foreach(var v in _voices)
            {
                if(v.cue==null){if(free==null)free=v;continue;}
                if(v.cue.id==id&&v.ownerId==oid){if(cue.loop&&v.fade>0)return v;replace=v;}
                if(v.cue.bus==cue.bus){busCount++;if(lowest==null||v.started<lowest.started)lowest=v;}if(v.cue.bus!="Ambience")sfxCount++;
            }
            if(cue.bus=="Ambience"&&busCount>=3&&replace==null){Count(Rejected,id);return null;}
            if(cue.bus!="Ambience"&&sfxCount>=24&&replace==null)
            {
                lowest=null;foreach(var v in _voices)if(v.cue!=null&&v.cue.bus!="Ambience"&&(lowest==null||v.cue.priority<lowest.cue.priority||v.cue.priority==lowest.cue.priority&&v.started<lowest.started))lowest=v;
                if(lowest!=null&&lowest.cue.priority>cue.priority){Count(Rejected,id);return null;}replace=lowest;
            }
            if(replace==null&&busCount>=cue.busLimit)replace=lowest;
            var voice=replace??free;if(voice==null||cue.clips==null||cue.clips.Length==0){Count(Rejected,id);return null;}
            voice.source.Stop();voice.cue=cue;voice.owner=owner;voice.ownerId=oid;voice.hadOwner=owner!=null;voice.follow=follow;voice.at=at;voice.paused=false;voice.fade=1;voice.started=AudioSettings.dspTime;
            _variants.TryGetValue(id,out var last);int index=cue.clips.Length>1?(last+_random.Next(1,cue.clips.Length))%cue.clips.Length:0;_variants[id]=index;
            voice.source.clip=cue.clips[index];voice.source.loop=cue.loop;voice.source.priority=128-cue.priority;SetVolume(voice);voice.source.Play();Count(Played,id);return voice;
        }
        void Release(Voice voice){if(voice.cue!=null)voice.fade=-Mathf.Max(.0001f,Mathf.Abs(voice.fade));}
        void StopCue(string id,Component owner){int oid=owner!=null?owner.GetInstanceID():0;foreach(var v in _voices)if(v.cue!=null&&v.cue.id==id&&v.ownerId==oid)Release(v);}
        void SetVolume(Voice v)
        {
            float level=1;var cue=v.cue;var cam=Camera.main;
            var player=_demo!=null?_demo.PlayerBody:_samplePlayer;
            if(cue.spatial!="L"&&player!=null)
            {
                float distance=Vector2.Distance(v.at,player.position);level=Mathf.Clamp01(1-distance/35f);
                v.source.panStereo=cam!=null?Mathf.Clamp((cam.WorldToViewportPoint(v.at).x-.5f)*1.4f,-.85f,.85f):0;
            }else v.source.panStereo=0;
            float duck=cue.bus=="Ambience"?-4*_duck:0;
            if(cue.id=="jh_bow_charge_loop"&&v.owner!=null){var charge=v.owner.GetComponent<PlayerCharge>();if(charge!=null)level*=Mathf.Lerp(.35f,.85f,charge.CurrentRead.Progress(charge.HeldSeconds));}
            v.source.volume=Mathf.Pow(10,(cue.gainDb+duck)/20f)*level*Mathf.Abs(v.fade);
        }
        void Update()
        {
            if(!_bound)return;bool hard=RunPause.MenuOpen||!_focus;
            if(hard!=_hardPaused){_hardPaused=hard;if(hard){_musicPauseAt=AudioSettings.dspTime;foreach(var m in _music)m.source.Pause();}else{_origin+=AudioSettings.dspTime-_musicPauseAt;foreach(var m in _music)if(m.weight>0)m.source.UnPause();}}
            float dt=Time.unscaledDeltaTime;bool threat=false;ActiveSfx=0;
            foreach(var v in _voices)
            {
                if(v.cue==null)continue;if(v.hadOwner&&v.owner==null&&(v.cue.loop||v.cue.bus=="Threat"))Release(v);
                if(v.follow&&v.owner!=null&&v.cue.spatial=="W")v.at=v.owner.transform.position;
                if(RunPause.IsPaused&&v.cue.id=="jh_bow_charge_loop")Release(v);
                bool pause=hard&&v.cue.bus!="UI"||RunPause.InteractOpen&&World(v.cue);
                if(v.fade<0){v.fade=Mathf.Min(0,v.fade+dt/Mathf.Max(.01f,v.cue.stopFade));pause=false;}
                if(pause&&!v.paused){v.source.Pause();v.paused=true;}else if(!pause&&v.paused){v.source.UnPause();v.paused=false;}
                if(v.fade==0||(!v.paused&&!v.source.isPlaying)){v.source.Stop();v.cue=null;v.owner=null;continue;}
                SetVolume(v);if(v.cue.bus=="Threat")threat=true;if(v.cue.bus!="Ambience")ActiveSfx++;
            }
            PeakSfx=Mathf.Max(PeakSfx,ActiveSfx);if(hard)return;_duck=Mathf.MoveTowards(_duck,threat?1:0,dt/(threat?.015f:.22f));
            string state="exploration";var boss=_demo!=null?_demo.FinalBoss:_sampleBoss;
            if(_settled)state="silent";
            else if(boss!=null&&boss.FightStarted&&!boss.FightSettled)state=boss.Brain.Phase==BossPhase.P2?"boss_p2":"boss_p1";
            else if(_demo!=null&&(_demo.AltarClockPaused||_demo.FinalShopOpen))state="shelter";
            else if(_demo!=null&&_demo.AudioCombatActive)state="combat_s"+(int)_demo.CurrentStage;
            SetMusic(state);UpdateMusic(dt);_ambientPoll-=dt;if(_ambientPoll<=0){_ambientPoll=.25f;Ambient();}
        }
        public void SetMusic(string id)
        {
            if(MusicState==id)return;MusicState=id;foreach(var m in _music)m.target=0;
            if(_bank!=null)foreach(var preset in _bank.presets)if(preset.id==id)foreach(var layer in preset.layers)foreach(var m in _music)if(m.id==layer.id)m.target=layer.weight;
        }
        void UpdateMusic(float dt)
        {
            ActiveMusic=0;foreach(var m in _music)
            {
                if(m.target>0&&m.weight<=0){double start=System.Math.Max(_origin,AudioSettings.dspTime+.06);var clip=m.source.clip;m.source.timeSamples=(int)(System.Math.Max(0,start-_origin)*clip.frequency%clip.samples);m.source.PlayScheduled(start);}
                m.weight=Mathf.MoveTowards(m.weight,m.target,dt/.65f);m.source.volume=m.weight*Mathf.Pow(10,(-10-3*_duck)/20f);
                if(m.weight<=0&&m.target<=0)m.source.Stop();else ActiveMusic++;
            }PeakMusic=Mathf.Max(PeakMusic,ActiveMusic);
        }
        void Ambient()
        {
            var player=_demo!=null?_demo.PlayerBody:_samplePlayer;
            if(_settled||player==null){if(_fire!=null)Release(_fire);if(_resonance!=null)Release(_resonance);return;}
            Vector3 p=player.position;Transform nearest=null;float best=625;
            foreach(var t in _braziers)if(t!=null&&(t.position-p).sqrMagnitude<best){nearest=t;best=(t.position-p).sqrMagnitude;}
            if(nearest==null){if(_fire!=null)Release(_fire);}else if(_fire==null||_fire.cue==null)_fire=Play("jh_amb_brazier",nearest.position,this,false);else _fire.at=nearest.position;
            StageAltarView altar=null;best=225;foreach(var a in _altars)if(a!=null&&a.IsLit&&(a.transform.position-p).sqrMagnitude<best){altar=a;best=(a.transform.position-p).sqrMagnitude;}
            if(altar==null){if(_resonance!=null)Release(_resonance);}else if(_resonance==null||_resonance.cue==null)_resonance=Play("jh_amb_altar_resonance",altar.transform.position,this,false);else _resonance.at=altar.transform.position;
        }
        void OnApplicationFocus(bool focus){_focus=focus;}
        void OnDestroy(){ResetRun();if(Live==this)Live=null;}
    }
}
