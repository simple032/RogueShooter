using UnityEngine;

namespace RogueShooter.Boss
{
    /// <summary>Independent native review stage; not included in the main gameplay scene.</summary>
    public sealed class BossWardenPreview : MonoBehaviour
    {
        readonly string[] _names={"持盾待机","持盾步行","单发","三连发","持盾冲锋","盾击","二阶段待机","二阶段步行","三联射","跳砸","弩砸","弃盾换装","二阶段死亡","一期死亡"};
        readonly string[] _directions={"n","ne","e","se","s","sw","w","nw"};
        readonly string[] _directionNames={"北","东北","东","东南","南","西南","西","西北"};
        BossWardenPresentation _view;int _clip,_direction=3;float _seconds;bool _play=true;float _speed=1;
        void Start(){ResetView();}
        void ResetView()
        {
            if(_view!=null)Destroy(_view.gameObject);
            var go=new GameObject("动作验收角色");go.transform.SetParent(transform,false);
            var renderer=go.AddComponent<SpriteRenderer>();renderer.sortingLayerName="Entity";
            _view=go.AddComponent<BossWardenPresentation>();_view.Bind(renderer);_seconds=0;
        }
        void Update()
        {
            var c=BossWardenArt.Data.clips[_clip];
            if(_play)_seconds+=Time.unscaledDeltaTime*_speed;
            bool loop=c.id.EndsWith("idle")||c.id.EndsWith("walk");
            if(!loop&&_seconds>=c.ticks.Length/20f)_seconds=c.ticks.Length/20f;
            if(c.id.EndsWith("death"))
            {
                // Review still uses the production sprite player; manual tick avoids a separate death clock.
                _view.ShowFrame(c.id,_directions[_direction],c.FrameAt(_seconds));
                if(c.id=="p1_death")_view.ShowReviewShield("p1_death_shield",_directions[_direction],Mathf.Min(15,(int)(_seconds*20)),2);
            }
            else _view.Show(c.id,_directions[_direction],_seconds,loop);
        }
        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12,12,220,Screen.height-24),GUI.skin.box);
            GUILayout.Label("骸冠典狱长 · v05");
            GUILayout.Label("身侧低持 · 射击抬弩 · 砸击高举");
            int clip=GUILayout.SelectionGrid(_clip,_names,2);
            if(clip!=_clip){_clip=clip;ResetView();}
            GUILayout.Space(10);int dir=GUILayout.SelectionGrid(_direction,_directionNames,4);
            if(dir!=_direction){_direction=dir;ResetView();}
            if(GUILayout.Button(_play?"暂停":"播放"))_play=!_play;
            if(GUILayout.Button("从头播放")){ResetView();_play=true;}
            if(GUILayout.Button(_speed==1?"切换半速":"切换正常速度"))_speed=_speed==1?.5f:1;
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("前一步"))Step(-1);
            if(GUILayout.Button("后一步"))Step(1);
            GUILayout.EndHorizontal();
            GUILayout.Label("时间 "+_seconds.ToString("0.00")+" 秒\n当前帧 "+_view.CurrentFrame+"\n八方向均为实际渲染");
            GUILayout.EndArea();
        }
        void Step(int direction)
        {
            _play=false;
            var clip=BossWardenArt.Data.clips[_clip];
            int tick=Mathf.RoundToInt(_seconds*20f)+direction;
            _seconds=Mathf.Clamp(tick,0,clip.ticks.Length-1)/20f;
        }
    }
}
