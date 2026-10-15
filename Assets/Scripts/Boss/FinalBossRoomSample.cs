using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RogueShooter.Build;
using RogueShooter.Demo;
using RogueShooter.Iso;
using RogueShooter.Maze;
using RogueShooter.Player;
using RogueShooter.Vision;

namespace RogueShooter.Boss
{
    /// <summary>Isolated playable room. Uses production combat, art, projectiles and collisions.</summary>
    public sealed class FinalBossRoomSample : MonoBehaviour
    {
        public const string ScenePath="Assets/ArtSamples/FinalBossRoom/FinalBossRoom.scene";
        public BossFightDriver Fight {get;private set;}
        public Transform PlayerBody {get;private set;}
        public Stage1Maze Maze {get;private set;}
        public bool Practice {get;private set;}
        public bool MixedBuild {get;private set;}
        public bool Resetting {get;private set;}
        public float EntryMinutes {get;private set;}=11f;
        Transform _room; Camera _camera; bool _menu; float _pauseScale=1;
        bool _practiceUsed,_mixedUsed; float _entryUsed,_sampleSeconds;
        readonly List<string> _owned=new List<string>();
        Sprite _icon; Texture2D _ring; Color32[] _pixels; float[] _angles; byte[] _alpha; int _step=-1;
        void Start(){StartCoroutine(Rebuild(false));}
        public void Restart(bool phase2=false){if(!Resetting)StartCoroutine(Rebuild(phase2));}
        IEnumerator Rebuild(bool phase2)
        {
            Resetting=true;_menu=false;RunPause.ResetAll();RewardStatHooks.ResetRoomEnter();
            foreach(var arrow in FindObjectsOfType<ArrowFly>())Destroy(arrow.gameObject);
            _practiceUsed=Practice;_mixedUsed=MixedBuild;_entryUsed=EntryMinutes;_sampleSeconds=0;
            if(_room!=null){Destroy(_room.gameObject);yield return null;}
            _room=new GameObject("独立 Boss 战样板房").transform;_room.SetParent(transform,false);
            var node=new MazeNode{Id="BOSS",Kind=MazeNodeKind.Boss,Center=new MazeVec2(0,0),Width=28,Height=22,NeighborIds=new string[0]};
            Maze=new Stage1Maze{Seed=42,TemplateId="BOSS_TEST",Nodes=new[]{node},Edges=new MazeEdge[0]};
            var pieces=new List<GameObject>();
            Stage1IsoArt.BuildFloors(Maze,_room,Maze);
            Stage1IsoArt.BuildRoomShell(node,Maze,_room,pieces,42,null,false,false);
            S2S3EnvironmentPolish.RingPaving(Vector2.zero,"BOSS",_room,pieces);
            var player=new GameObject("Player");player.transform.SetParent(_room,false);player.transform.position=new Vector3(0,-6,0);
            player.AddComponent<Stage1IsoActor>().Bind("archer");
            var motor=player.AddComponent<PlayerMotor2D>();motor.Configure(MazeRules.PlayMoveSpeed);motor.BindMaze(Maze,null);
            player.AddComponent<PlayerVitals>().Configure(Practice?10000:100);
            player.AddComponent<PlayerStrike>().Configure(1.85f);player.AddComponent<GuaranteedCritActive>();
            var charge=player.AddComponent<PlayerCharge>();player.AddComponent<PlayerRoll>();
            _owned.Clear();
            // A repeatable sample of 10 legal picks, not a claimed typical/natural run.
            if(MixedBuild)_owned.AddRange(new[]{"R1M","R1M","R2M","R2M","R7H","R8H","R9H","R13H","R16M","R17M"});
            charge.BindOwnedRewards(_owned);charge.BindMaze(Maze);
            PlayerBody=player.transform;
            _camera=Camera.main;
            if(_camera==null){_camera=new GameObject("Main Camera").AddComponent<Camera>();_camera.tag="MainCamera";_camera.gameObject.AddComponent<AudioListener>();}
            _camera.clearFlags=CameraClearFlags.SolidColor;_camera.backgroundColor=new Color(.04f,.045f,.06f);_camera.nearClipPlane=.3f;_camera.farClipPlane=100;
            var lens=_camera.GetComponent<CameraViewService>()??_camera.gameObject.AddComponent<CameraViewService>();lens.Configure(CameraViewService.PlayOrthoSize);
            var follow=_camera.GetComponent<CameraFollow2D>()??_camera.gameObject.AddComponent<CameraFollow2D>();follow.enabled=true;follow.SetFaceOn(16);follow.SetTarget(PlayerBody);
            if(_camera.GetComponent<IsoSortAxis>()==null)_camera.gameObject.AddComponent<IsoSortAxis>();
            var boss=new GameObject("骸冠典狱长 · 正式战斗");boss.transform.SetParent(_room,false);boss.transform.position=new Vector3(0,-3,0);
            Fight=boss.AddComponent<BossFightDriver>();Fight.BindArena(null,Maze,node,PlayerBody);Fight.Settle.enabled=true;
            var audio=GetComponent<RogueShooter.Audio.JianHaiAudio>()??gameObject.AddComponent<RogueShooter.Audio.JianHaiAudio>();audio.BindBossSample(PlayerBody,Fight);
            Resetting=false;
            if(phase2){BeginFight();yield return null;Fight.DealDamage(Fight.Brain.MaxHp*.51f,Vector3.zero,true);}
        }
        public void BeginFight()
        {
            if(Resetting||Fight==null||Fight.FightStarted)return;
            if(Practice!=_practiceUsed||MixedBuild!=_mixedUsed||EntryMinutes!=_entryUsed){StartCoroutine(StartConfiguredFight());return;}
            RewardStatHooks.NotifyRoomEntered(Time.time);Fight.BeginEnter(MixedBuild?10:0,EntryMinutes);
        }
        IEnumerator StartConfiguredFight(){yield return Rebuild(false);BeginFight();}
        public void ConfigureTest(bool practice,bool mixed,float minutes)
        {Practice=practice;MixedBuild=mixed;EntryMinutes=Mathf.Max(0,minutes);}
        void Update()
        {
            if(Resetting||Fight==null)return;
            if(Fight.FightStarted&&!Fight.FightSettled&&!RunPause.IsPaused){_sampleSeconds+=Time.deltaTime;Fight.ObserveWallMinutes(_entryUsed+_sampleSeconds/60f);}
            if(Input.GetKeyDown(KeyCode.R)){Restart();return;}
            if(Input.GetKeyDown(KeyCode.F2)){Restart(true);return;}
            if(Input.GetKeyDown(KeyCode.Return))BeginFight();
            if(Input.GetKeyDown(KeyCode.Escape)&&!Fight.Transitioning&&!Fight.FightSettled)
            {_menu=!_menu;RunPause.MenuOpen=_menu;if(_menu){_pauseScale=Time.timeScale;Time.timeScale=0;}else Time.timeScale=_pauseScale;}
        }
        void OnGUI()
        {
            if(Resetting||Fight==null||PlayerBody==null)return;
            float s=JianHaiUiTheme.Scale;var old=GUI.matrix;GUI.matrix=Matrix4x4.Scale(new Vector3(s,s,1));
            float width=Screen.width/s,height=Screen.height/s;
            var label=new GUIStyle(GUI.skin.label){fontSize=15,normal={textColor=JianHaiUiTheme.Bone}};
            var center=new GUIStyle(label){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold,fontSize=18};
            var v=PlayerBody.GetComponent<PlayerVitals>();
            JianHaiUiTheme.ShadowLabel(new Rect(20,15,280,25),"生命 "+v.Hp.ToString("0")+" / "+v.MaxHp.ToString("0"),label);
            Bar(new Rect(20,43,200,8),v.Hp/v.MaxHp,new Color(.67f,.27f,.24f));
            var charge=PlayerBody.GetComponent<PlayerCharge>();var read=charge.CurrentRead;
            Bar(new Rect(20,56,200,6),charge.IsCharging?read.Progress(charge.HeldSeconds):0,
                read.Resolve(charge.HeldSeconds)==ChargeShotKind.Crit?new Color(.25f,.85f,.76f):new Color(.79f,.63f,.34f));
            JianHaiUiTheme.ShadowLabel(new Rect(width/2-240,12,480,26),"骸冠典狱长 · "+(Fight.Brain.Phase==BossPhase.P2?"裂冠":"盾冠"),center);
            Bar(new Rect(width/2-200,43,400,12),Fight.Brain.Hp/Fight.Brain.MaxHp,new Color(.67f,.27f,.24f));
            if(Fight.FightStarted)JianHaiUiTheme.ShadowLabel(new Rect(width/2-200,58,400,24),Fight.Brain.Hp.ToString("0")+" / "+Fight.Brain.MaxHp.ToString("0"),center);
            DrawSkill(width,height);
            if(!Fight.FightStarted||_menu||Fight.FightSettled)
            {
                float y=Fight.FightSettled?height/2+116:height/2-110;
                if(Fight.FightSettled) {if(GUI.Button(new Rect(width/2-120,y,240,32),"重新测试 [R]"))Restart();}
                else
                {
                    GUI.Box(new Rect(width/2-225,y,450,236),"");
                    GUI.Label(new Rect(width/2-210,y+10,420,25),_menu?"已暂停 · ESC 继续":"Boss 战样板房 · 独立测试",center);
                    GUI.Label(new Rect(width/2-210,y+42,420,50),"WASD 移动 · 左键蓄力射击 · Shift 翻滚 · Q 技能\nR 重开 · F2 直测二阶段（测试扣血）",label);
                    Practice=GUI.Toggle(new Rect(width/2-210,y+100,420,24),Practice,"耐久练习：10000生命（重开生效，非平衡测试）");
                    MixedBuild=GUI.Toggle(new Rect(width/2-210,y+128,420,24),MixedBuild,"固定10项强化（重开生效，非自然构筑）");
                    if(GUI.Button(new Rect(width/2-210,y+158,205,28),"入场时间 "+EntryMinutes+" 分钟"))EntryMinutes=EntryMinutes<12?13:11;
                    if(GUI.Button(new Rect(width/2+5,y+158,205,28),"重建测试配置"))Restart();
                    if(GUI.Button(new Rect(width/2-210,y+195,420,30),_menu?"继续":"开始战斗 [Enter]"))
                    {if(_menu){_menu=false;RunPause.MenuOpen=false;Time.timeScale=_pauseScale;}else BeginFight();}
                }
            }
            else
            {
                JianHaiUiTheme.ShadowLabel(new Rect(20,height-35,540,25),"样板房 · "+(_practiceUsed?"耐久练习":"100生命")+" · "+(_mixedUsed?"固定强化":"基础弓")+" · ESC暂停 / R重开",label);
            }
            GUI.matrix=old;
        }
        static void Bar(Rect r,float fraction,Color c){var old=GUI.color;GUI.color=new Color(.08f,.1f,.12f,.8f);GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=c;GUI.DrawTexture(new Rect(r.x,r.y,r.width*Mathf.Clamp01(fraction),r.height),Texture2D.whiteTexture);GUI.color=old;}
        void DrawSkill(float width,float height)
        {
            var focus=PlayerBody.GetComponent<GuaranteedCritActive>();if(_icon==null)_icon=Resources.Load<Sprite>("JianHaiReward/jh_ui_skill_focus_v01");if(_icon==null)return;
            var r=new Rect(width-116,height-116,96,96);var old=GUI.color;float remaining=Mathf.Clamp01(focus.CooldownLeft/GuaranteedCritActive.CooldownSeconds);
            var p=_icon.textureRect;var t=_icon.texture;GUI.color=remaining>0?new Color(.32f,.32f,.32f):Color.white;
            GUI.DrawTextureWithTexCoords(r,t,new Rect(p.x/t.width,p.y/t.height,p.width/t.width,p.height/t.height));GUI.color=Color.white;
            if(remaining>0){UpdateRing(remaining);GUI.DrawTexture(new Rect(r.x-5,r.y-5,106,106),_ring);}
            GUI.Label(new Rect(r.xMax-18,r.yMax-20,20,20),"Q");GUI.color=old;
        }
        void UpdateRing(float remaining)
        {
            const int n=256;
            if(_ring==null){_ring=new Texture2D(n,n,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear};_pixels=new Color32[n*n];_angles=new float[n*n];_alpha=new byte[n*n];
                for(int y=0;y<n;y++)for(int x=0;x<n;x++){int i=y*n+x;float dx=x+.5f-128,dy=y+.5f-128,r=Mathf.Sqrt(dx*dx+dy*dy);_alpha[i]=(byte)Mathf.RoundToInt(255*Mathf.Clamp01(Mathf.Min(r-120,126-r)));float a=Mathf.Atan2(dx,dy);_angles[i]=a<0?a+Mathf.PI*2:a;}}
            int step=Mathf.CeilToInt(remaining*360);if(step==_step)return;_step=step;
            for(int i=0;i<_pixels.Length;i++)_pixels[i]=_angles[i]<=step*Mathf.Deg2Rad?new Color32(238,194,105,_alpha[i]):new Color32(30,47,60,_alpha[i]);_ring.SetPixels32(_pixels);_ring.Apply(false,false);
        }
        void OnDestroy(){if(_ring!=null)Destroy(_ring);}
    }
}
