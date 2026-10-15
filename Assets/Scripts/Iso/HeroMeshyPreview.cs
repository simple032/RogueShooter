using UnityEngine;
namespace RogueShooter.Iso
{
    public sealed class HeroMeshyPreview:MonoBehaviour
    {
        static readonly string[] Dirs={"e","ne","n","nw","w","sw","s","se"};
        static readonly string[] DirNames={"右","右上","上","左上","左","左下","下","右下"};
        static readonly string[] Names={"待机","行走参考","奔跑移动","举弓拉弦","满拉保持","松弦收招","弱蓄释放","翻滚起身","受击","死亡","点燃祭坛","移动拉弦","移动释放"};
        HeroMeshyPresentation _view;Font _font;Vector2 _scroll;int _clip,_dir=7,_relative;float _time,_phase,_draw=1;bool _playing=true,_half;
        void Start(){_font=Font.CreateDynamicFontFromOSFont("Microsoft YaHei UI",18);var go=new GameObject("主角实际动作资源");_view=go.AddComponent<HeroMeshyPresentation>();_view.Bind(go.AddComponent<SpriteRenderer>());}
        void Update()
        {
            if(_view==null)return;var c=HeroMeshyArt.Data.clips[_clip];if(_playing){float dt=Time.unscaledDeltaTime*(_half?.5f:1);_time+=dt;_phase=Mathf.Repeat(_phase+dt/.7f,1);}
            int frame;
            if(c.id=="moving_draw")frame=HeroMeshyArt.DrawGrid(_relative,_draw,_phase);
            else if(c.id=="moving_release")frame=HeroMeshyArt.ReleaseGrid(_relative,_draw,Mathf.Repeat(_time,.2f),_phase);
            else{float seconds=Mathf.Max(.05f,c.seconds);float u=c.loop?Mathf.Repeat(_time/seconds,1):Mathf.Clamp01(_time/seconds);frame=Mathf.Clamp(Mathf.FloorToInt(u*(c.loop?c.frames:c.frames-1)+.0001f),0,c.frames-1);}
            _view.ReviewFrame(c.id,Dirs[_dir],frame);
        }
        void Step(int direction){_playing=false;var c=HeroMeshyArt.Data.clips[_clip];_time=Mathf.Clamp(_time+direction*.05f,0,Mathf.Max(.2f,c.seconds));_phase=Mathf.Repeat(_phase+direction/6f,1);}
        void OnGUI()
        {
            if(_view==null)return;if(_font!=null)GUI.skin.font=_font;GUI.Box(new Rect(12,12,310,Mathf.Min(650,Screen.height-24)),"主角动作查看 · "+HeroMeshyArt.Data.version.Replace("hero-meshy/","v"));GUILayout.BeginArea(new Rect(24,42,284,Mathf.Min(596,Screen.height-72)));_scroll=GUILayout.BeginScrollView(_scroll);
            for(int i=0;i<Names.Length;i++)if(GUILayout.Button(Names[i]+(_clip==i?" ✓":""))){_clip=i;_time=_phase=0;}
            GUILayout.BeginHorizontal();if(GUILayout.Button(_playing?"暂停":"播放"))_playing=!_playing;if(GUILayout.Button(_half?"正常速度":"半速"))_half=!_half;if(GUILayout.Button("重播"))_time=_phase=0;GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();if(GUILayout.Button("前一步"))Step(-1);if(GUILayout.Button("后一步"))Step(1);GUILayout.EndHorizontal();
            GUILayout.Label("方向");_dir=GUILayout.SelectionGrid(_dir,DirNames,4);
            if(_clip>=11){GUILayout.Label("相对移动方向");_relative=GUILayout.SelectionGrid(_relative,new[]{"前进","左前","左移","左后","后退","右后","右移","右前"},4);GUILayout.Label("拉弦进度 / 释放强度");_draw=GUILayout.HorizontalSlider(_draw,0,1);}
            GUILayout.Label(Names[_clip]+" · "+DirNames[_dir]+" · 第 "+(_view.CurrentFrame+1)+" 帧");GUILayout.EndScrollView();GUILayout.EndArea();
        }
    }
}
