using UnityEngine;
using RogueShooter.Maze;
using RogueShooter.Player;

namespace RogueShooter.Demo
{
    /// <summary>First-run lessons consume current-step gameplay events, not key presses.</summary>
    public sealed class StartRoomTutorial : MonoBehaviour
    {
        public const string CompletionKey = "JianHai.StartTutorial.Completed.v1";
        public enum Lesson { Move, Shoot, WeakHit, Skill, Complete }
        public Lesson Step { get; private set; }
        public bool Active => Step != Lesson.Complete;
        public bool AllowSkillActivation => !Active || Step == Lesson.Skill;
        public StartTrainingTarget Target { get; private set; }

        Stage1MazeDemo _demo;
        MazeNode _room;
        PlayerMotor2D _motor;
        PlayerCharge _charge;
        GuaranteedCritActive _focus;
        float _distance, _completeUntil;
        int _weakAfterShot, _activationAtEntry;
        bool _bound;
        string _shotFeedback;

        public void Bind(Stage1MazeDemo demo, MazeNode room)
        {
            Unbind();
            _demo = demo; _room = room;
            _motor = GetComponent<PlayerMotor2D>();
            _charge = GetComponent<PlayerCharge>();
            _focus = GetComponent<GuaranteedCritActive>();
            Step = PlayerPrefs.GetInt(CompletionKey, 0) == 1 ? Lesson.Complete : Lesson.Move;
            _distance = _completeUntil = 0f;
            _shotFeedback = null;
            _bound = true;
            if (_motor != null) _motor.Walked += OnWalked;
            if (_charge != null) _charge.ShotReleased += OnShot;
            if (_focus != null) _focus.Activated += OnSkill;
        }

        bool CanLearn => _bound && Active && !RunPause.IsPaused
            && _room != null && _room.Contains(transform.position.x, transform.position.y, 0f)
            && (GetComponent<PlayerVitals>() == null || !GetComponent<PlayerVitals>().IsDown);

        void OnWalked(Vector2 displacement)
        {
            if (!CanLearn || Step != Lesson.Move) return;
            _distance += displacement.magnitude;
            if (_distance < 1f) return;
            Step = Lesson.Shoot;
            RogueShooter.Audio.JianHaiAudio.Emit("jh_tutorial_step",this);
            Target = StartTrainingTarget.Create(this, _room, _demo.transform);
        }

        void OnShot(ChargeShotKind kind, float held, int serial)
        {
            if (!CanLearn) return;
            if (Step == Lesson.Shoot)
            {
                if (kind == ChargeShotKind.None)
                {
                    _shotFeedback = "蓄力太短，没有射出箭。按住左键再试。";
                    return;
                }
                Step = Lesson.WeakHit;
                _shotFeedback = null;
                RogueShooter.Audio.JianHaiAudio.Emit("jh_tutorial_step",this);
                // Lesson 2's arrow cannot complete lesson 3 when it lands later.
                _weakAfterShot = serial;
                return;
            }
            if (Step != Lesson.WeakHit || serial <= _weakAfterShot && kind != ChargeShotKind.None) return;
            if (kind == ChargeShotKind.None)
                _shotFeedback = "蓄力太短，没有射出箭。按住左键再试。";
            else if (kind == ChargeShotKind.Weak)
                _shotFeedback = "这次是弱蓄箭，伤害较低。再多蓄一会，等绿色窗口。";
            else if (kind == ChargeShotKind.Crit)
                _shotFeedback = "已射出弱点箭；命中训练靶才完成。射空请重新瞄准再试。";
            else if (_charge != null && held > _charge.CurrentRead.GreenExit)
                _shotFeedback = "松开太晚，错过了绿色窗口。这次是普通箭，重新蓄力再试。";
            else
                _shotFeedback = "松开太早，还未进入绿色窗口。这次是普通箭，再多蓄一会。";
        }

        public void OnTargetHit(ChargeShotKind kind, int serial)
        {
            if (!CanLearn || Step != Lesson.WeakHit || kind != ChargeShotKind.Crit
                || serial <= _weakAfterShot || (_focus != null && _focus.IsActive)) return;
            Step = Lesson.Skill;
            RogueShooter.Audio.JianHaiAudio.Emit("jh_tutorial_step",this);
            _activationAtEntry = _focus != null ? _focus.ActivationId : 0;
        }

        void OnSkill()
        {
            if (!CanLearn || Step != Lesson.Skill || _focus == null
                || _focus.ActivationId <= _activationAtEntry) return;
            Step = Lesson.Complete;
            RogueShooter.Audio.JianHaiAudio.Emit("jh_tutorial_complete",this);
            PlayerPrefs.SetInt(CompletionKey, 1);
            PlayerPrefs.Save();
            _focus.ResetAfterTraining();
            ClearTarget();
            _completeUntil = Time.unscaledTime + 3f;
        }

        void ClearTarget()
        {
            if (Target == null) return;
            Target.gameObject.SetActive(false);
            Destroy(Target.gameObject);
            Target = null;
        }

        void Unbind()
        {
            if (_motor != null) _motor.Walked -= OnWalked;
            if (_charge != null) _charge.ShotReleased -= OnShot;
            if (_focus != null) _focus.Activated -= OnSkill;
            ClearTarget();
            _bound = false;
        }
        void OnDisable() { Unbind(); }

        public string Instruction
        {
            get
            {
                switch (Step)
                {
                    case Lesson.Move: return "WASD / 方向键移动，走动后继续。\n按 Shift 沿移动方向翻滚；不按方向时沿角色朝向翻滚。";
                    case Lesson.Shoot: return "把鼠标移向训练靶，按住左键蓄力，松开发射。\n蓄力太短不会出箭；翻滚会取消当前蓄力。";
                    case Lesson.WeakHit: return "按住左键蓄力并瞄准训练靶，蓄力条变绿时松开并命中。\n弱点看松开时机，不需要瞄准头部。\n弱蓄：伤害较低；过早松开会不出箭。\n未到弱点窗：普通箭，不算弱点。\n位于弱点窗（绿色）：弱点箭，命中可打断、碎盾。\n超过弱点窗：普通箭；松开后重新蓄力。";
                    case Lesson.Skill: return "按 Q 开启凝神窥机：短时间内免蓄力，箭箭必弱点。\n生效时按住左键连续射击。\n右下角图标变暗表示冷却，冷却环消退后恢复就绪。";
                    default: return "教程完成 · 向上出发";
                }
            }
        }

        // Read the same thresholds as arrow resolution, including owned rewards.
        public string Status
        {
            get
            {
                if (Step == Lesson.WeakHit && _charge != null && _charge.IsCharging)
                {
                    var read = _charge.CurrentRead;
                    float held = _charge.HeldSeconds;
                    switch (read.Resolve(held))
                    {
                        case ChargeShotKind.None: return "蓄力刚开始 · 继续按住左键。";
                        case ChargeShotKind.Weak: return "当前是弱蓄 · 继续蓄力，等待绿色窗口。";
                        case ChargeShotKind.Crit: return "绿色弱点窗 · 现在松开左键！";
                        default: return held > read.GreenExit
                            ? "已经错过窗口 · 松开后重新蓄力；继续按住不会再变绿。"
                            : "还未到弱点窗 · 继续按住左键，变绿就松开。";
                    }
                }
                if (Step == Lesson.Shoot || Step == Lesson.WeakHit)
                    return _shotFeedback ?? (Step == Lesson.WeakHit
                        ? "看蓄力条的绿色提示，窗口一出现就松开；射空可以反复练习。"
                        : "先练习射出箭，下一步再练习绿色弱点窗。" );
                return string.Empty;
            }
        }

        void OnGUI()
        {
            var vitals = GetComponent<PlayerVitals>();
            if (RunPause.MenuOpen || RunPause.InteractOpen || !_bound || (vitals != null && vitals.IsDown)
                || (!Active && Time.unscaledTime >= _completeUntil)) return;
            float scale = Mathf.Clamp(Screen.height / 720f, .75f, 1.3f);
            float width = Mathf.Min((Step == Lesson.WeakHit ? 760f : 680f) * scale, Screen.width - 24f);
            var title = new GUIStyle(GUI.skin.label) { alignment=TextAnchor.MiddleCenter,
                fontSize=Mathf.RoundToInt(13f*scale), normal={textColor=new Color(.87f,.75f,.52f)} };
            var text = new GUIStyle(GUI.skin.label) { alignment=TextAnchor.MiddleCenter,
                fontSize=Mathf.RoundToInt(17f*scale), fontStyle=FontStyle.Bold, wordWrap=true,
                normal={textColor=new Color(.94f,.91f,.83f)} };
            var hint = new GUIStyle(text) { normal={textColor=new Color(.87f,.75f,.52f)} };
            if (Step == Lesson.WeakHit && _charge != null && _charge.IsCharging
                && _charge.CurrentRead.Resolve(_charge.HeldSeconds) == ChargeShotKind.Crit)
                hint.normal.textColor = new Color(.4f,1f,.72f);
            string instruction = Instruction;
            string status = Status;
            float bodyWidth = width - 24f;
            float bodyHeight = text.CalcHeight(new GUIContent(instruction),bodyWidth) + 8f*scale;
            float hintHeight = string.IsNullOrEmpty(status) ? 0f
                : hint.CalcHeight(new GUIContent(status),bodyWidth) + 8f*scale;
            float height = 32f*scale + bodyHeight + hintHeight;
            var r = new Rect((Screen.width-width)*.5f, Screen.height-height-16f, width, height);
            // Preserve the transparent tutorial presentation; text expands instead of clipping.
            JianHaiUiTheme.ShadowLabel(new Rect(r.x+12f,r.y+2f,r.width-24f,24f*scale),
                Active ? "训练 "+((int)Step+1)+" / 4" : "准备出发", title);
            JianHaiUiTheme.ShadowLabel(new Rect(r.x+12f,r.y+28f*scale,bodyWidth,bodyHeight), instruction, text);
            if (hintHeight > 0f)
                JianHaiUiTheme.ShadowLabel(new Rect(r.x+12f,r.y+28f*scale+bodyHeight,bodyWidth,hintHeight),status,hint);
        }
    }
}
