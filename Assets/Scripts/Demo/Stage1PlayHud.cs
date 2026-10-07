using System.Collections.Generic;
using UnityEngine;
using RogueShooter.Balance;
using RogueShooter.Boss;
using RogueShooter.Build;
using RogueShooter.Layout;
using RogueShooter.Maze;
using RogueShooter.Player;

namespace RogueShooter.Demo
{
    /// <summary>
    /// Stage-1 play chrome. Numbers and copy come from the committed rules:
    /// TimePressure labels, EconomyGold inherit, GuaranteedCritActive.HudText.
    /// Top-left is player HP, the run clock, and gold. The charge bar is not drawn.
    /// Top-center is the pressure timeline, or one boss HP bar during the boss room / fight.
    /// </summary>
    public sealed class Stage1PlayHud : MonoBehaviour
    {
        static readonly Dictionary<string, string> IconByName = new Dictionary<string, string>
        {
            { "锋矢", "jh_ui_icon_sharp_arrow" },
            { "疾张", "jh_ui_icon_rapid_draw" },
            { "鸿运", "jh_ui_icon_luck" },
            { "骨甲", "jh_ui_icon_bone_armor" },
            { "残影", "jh_ui_icon_afterimage" },
            { "盗墓者", "jh_ui_icon_grave_robber" },
            { "瞬击预感", "jh_ui_icon_instant_foresight" },
            { "破甲猛击", "jh_ui_icon_armor_break" },
            { "隙矢追猎", "jh_ui_icon_gap_hunter" },
            { "止血", "jh_ui_icon_hemostasis" },
            { "满血额外伤害", "jh_ui_icon_full_health_strike" },
            { "进房间5s内加攻", "jh_ui_icon_room_attack" },
            { "攻击吸血", "jh_ui_icon_lifesteal" },
            { "穿透后排", "jh_ui_icon_pierce_backline" },
            { "震矢", "jh_ui_icon_shock_arrow" },
            { "回身箭", "jh_ui_icon_return_arrow" },
            { "守势蓄发", "jh_ui_icon_guarded_charge" },
            { "凝神余响", "jh_ui_icon_focus_echo" },
        };

        readonly HashSet<string> _visited = new HashSet<string>();
        sealed class CorridorPart { public MazeEdge Edge; public Vector2 A, B; public bool Seen; }
        readonly List<CorridorPart> _corridors = new List<CorridorPart>();
        Stage1Maze _trailMaze;
        public int ExploredCorridorCount { get { int n=0; foreach(var p in _corridors) if(p.Seen) n++; return n; } }
        readonly List<string> _iconNames = new List<string>();
        readonly Dictionary<string, Sprite> _icons = new Dictionary<string, Sprite>();
        int _loggedOwned = -1;

        Stage1MazeDemo _demo;
        PlayerVitals _vitals;
        PlayerCharge _charge;
        GuaranteedCritActive _focus;
        Sprite _focusIcon;
        GUIStyle _focusKeyStyle;
        GUIStyle _hudTextStyle;
        GUIStyle _hintTextStyle;
        GUIStyle _iconBadgeStyle;
        GUIStyle _iconTooltipStyle;
        Texture2D _focusCooldownRing;
        Color32[] _focusRingPixels;
        float[] _focusRingAngles;
        byte[] _focusRingAlpha;
        int _focusRingStep = -1;
        float _prevHp = -1f;
        float _flash;
        float _peakFlash;
        bool _dead;
        public bool MapOverview { get; set; }
        int _inherit;
        GUIStyle _clockStyle;

        public string PhaseText { get; private set; }
        public string CritText { get; private set; }
        public string HpText { get; private set; }
        public string GoldText { get; private set; }
        public string ChargeText { get; private set; }
        public float Vignette01 { get; private set; }
        public float Flash01 { get { return _flash; } }
        public float PeakFlash { get { return _peakFlash; } }
        public bool DeathOpen { get { return _dead; } }
        public int InheritGold { get { return _inherit; } }
        public int IconCount { get { return _iconNames.Count; } }
        public int VisitedCount { get { return _visited.Count; } }
        public float ShownDamageProduct { get; private set; }
        public float ShownChargeSeconds { get; private set; }
        public float ShownGreenEnter { get; private set; }
        public float ShownGreenExit { get; private set; }

        public string VisitedIds
        {
            get
            {
                var ids = new List<string>(_visited);
                ids.Sort(System.StringComparer.Ordinal);
                return string.Join(",", ids.ToArray());
            }
        }

        public void Bind(Stage1MazeDemo demo)
        {
            _demo = demo;
        }

        public void BeginRun()
        {
            _dead = false;
            _inherit = 0;
            _prevHp = -1f;
            _flash = 0f;
            _peakFlash = 0f;
            _visited.Clear(); _corridors.Clear(); _trailMaze=null; MapOverview = false;
            _iconNames.Clear();
        }

        public void ClearPeakFlash()
        {
            _peakFlash = 0f;
        }

        void Update()
        {
            if (_demo == null)
                _demo = GetComponent<Stage1MazeDemo>();
            if (_demo == null || _demo.PlayerBody == null)
                return;

            _vitals = _demo.PlayerBody.GetComponent<PlayerVitals>();
            _charge = _demo.PlayerBody.GetComponent<PlayerCharge>();
            _focus = _demo.PlayerBody.GetComponent<GuaranteedCritActive>();
            if (Input.GetKeyDown(KeyCode.M)) MapOverview = !MapOverview;
            NoteRoom();
            NoteCorridor();
            NoteHit();
            NoteIcons();

            float minutes = _demo.RunSeconds / 60f;
            PhaseText = "第" + (int)_demo.CurrentStage + "阶段 · " + TimePressure.PhaseLabel(minutes);
            CritText = _focus != null ? _focus.HudLine : GuaranteedCritActive.HudText(0f, 0f);

            float hp = _vitals != null ? _vitals.Hp : 0f;
            float max = _vitals != null && _vitals.MaxHp > 0.01f ? _vitals.MaxHp : 1f;
            HpText = "血量 " + Mathf.CeilToInt(hp) + "/" + Mathf.CeilToInt(max);
            int gold = _demo.RunBuild != null ? _demo.RunBuild.Gold : 0;
            GoldText = "金币 " + gold;
            float held = _charge != null ? _charge.HeldSeconds : 0f;
            ShotRead read = _charge != null ? _charge.CurrentRead : ShotRead.From(null);
            float fill = read.Progress(held);
            ChargeText = "蓄力 " + Mathf.RoundToInt(fill * 100f) + "%";
            ShownDamageProduct = read.DamageProduct;
            ShownChargeSeconds = read.ChargeSeconds;
            ShownGreenEnter = read.GreenEnter;
            ShownGreenExit = read.GreenExit;
            int ownedN = _demo.RunBuild != null && _demo.RunBuild.OwnedRewardIds != null
                ? _demo.RunBuild.OwnedRewardIds.Count : 0;
            if (ownedN != _loggedOwned)
            {
                _loggedOwned = ownedN;
                Debug.Log("[Stage1] hud-read dmg×=" + read.DamageProduct.ToString("0.00")
                          + " charge=" + read.ChargeSeconds.ToString("0.000")
                          + " window=" + read.GreenEnter.ToString("0.000")
                          + "-" + read.GreenExit.ToString("0.000"));
            }
            Vignette01 = VignetteFor(hp / max);
            if (_flash > 0f)
                _flash = Mathf.MoveTowards(_flash, 0f, Time.deltaTime * 2.5f);

            if (!_dead && _vitals != null && _vitals.IsDown)
            {
                _dead = true;
                _inherit = EconomyGold.DeathInherit(gold);
                Debug.Log("[Stage1] death lock move attack interact inherit=" + _inherit);
            }
        }

        void NoteRoom()
        {
            Stage1Maze maze = _demo.BuiltMaze;
            if (maze == null || maze.Nodes == null)
                return;
            Vector3 p = _demo.PlayerBody.position;
            for (int i = 0; i < maze.Nodes.Length; i++)
            {
                MazeNode n = maze.Nodes[i];
                if (n.Contains(p.x, p.y, 0.2f))
                    _visited.Add(n.Id);
            }
        }

        void NoteCorridor()
        {
            var maze = _demo.BuiltMaze;
            if (maze == null || maze.Edges == null) return;
            if (_trailMaze != maze)
            {
                _corridors.Clear(); _trailMaze=maze;
                foreach (var edge in maze.Edges)
                {
                    var pts=edge.Points; if(pts==null || pts.Length<2) pts=new[]{edge.From,edge.To};
                    for(int i=0;i<pts.Length-1;i++)
                    {
                        var a=new Vector2(pts[i].X,pts[i].Y); var b=new Vector2(pts[i+1].X,pts[i+1].Y);
                        int count=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)/.5f));
                        for(int j=0;j<count;j++) _corridors.Add(new CorridorPart{Edge=edge,A=Vector2.Lerp(a,b,j/(float)count),B=Vector2.Lerp(a,b,(j+1)/(float)count)});
                    }
                }
            }
            Vector2 player=_demo.PlayerBody.position;
            foreach(var room in maze.Nodes) if(room.Contains(player.x,player.y,0f)) return;
            foreach(var part in _corridors)
            {
                if(part.Seen || !part.Edge.Contains(player.x,player.y,0f)) continue;
                Vector2 dir=(part.B-part.A).normalized, mid=(part.A+part.B)*.5f, offset=player-mid;
                if(Mathf.Abs(Vector2.Dot(offset,dir)) <= .55f && Mathf.Abs(dir.x*offset.y-dir.y*offset.x)<=part.Edge.Width*.5f+.15f) part.Seen=true;
            }
        }

        void NoteHit()
        {
            if (_vitals == null)
                return;
            if (_prevHp < 0f)
                _prevHp = _vitals.Hp;
            float dropped = _prevHp - _vitals.Hp;
            if (dropped > 0.01f && _vitals.MaxHp > 0.01f)
            {
                _flash = Mathf.Clamp01(dropped / _vitals.MaxHp);
                if (_flash > _peakFlash)
                    _peakFlash = _flash;
            }

            _prevHp = _vitals.Hp;
        }

        void NoteIcons()
        {
            _iconNames.Clear();
            RunBuildState build = _demo.RunBuild;
            if (build == null || build.OwnedRewardIds == null)
                return;
            var seen = new HashSet<string>();
            for (int i = 0; i < build.OwnedRewardIds.Count; i++)
            {
                string id = build.OwnedRewardIds[i];
                RewardRow row;
                string name;
                if (RewardCatalog.TryGet(id, out row))
                {
                    name = row.Name;
                    if (!IconByName.ContainsKey(name) || !seen.Add(name))
                        continue;
                }
                else if (RogueShooter.Build.RewardPresent.IsBalanceStatId(id))
                {
                    name = id;
                    if (!seen.Add(name))
                        continue;
                }
                else
                {
                    continue;
                }
                _iconNames.Add(name);
            }
        }

        void OnGUI()
        {
            if (_demo == null || RunPause.InteractOpen)
                return;
            DrawVignette();
            DrawFlash();
            DrawStatus();
            DrawSkillCooldown();
            DrawMinimap();
            DrawIcons();
            if (_dead)
                DrawDeath();
        }

        void DrawStatus()
        {
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            float scale = JianHaiUiTheme.Scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale,scale,1f));
            float width = Screen.width / scale;
            GUI.color = Color.white;
            // Same slot as the pressure timeline. Boss HP replaces it; it is not drawn on top.
            var center = new Rect(width*.5f-150f,20f,300f,24f);
            float bossHp;
            float bossMax;
            if (TryCenterBossHp(out bossHp, out bossMax))
                DrawBossHpBar(center, bossHp, bossMax);
            else
                DrawTimeLine(center);
            float hp = _vitals != null ? _vitals.Hp : 0f;
            float max = _vitals != null && _vitals.MaxHp>.01f ? _vitals.MaxHp : 1f;
            var hpRect = new Rect(30f,28f,232f,24f);
            DrawBar("jh_ui_bar_hp_back","jh_ui_bar_hp_fill",hpRect,hp/max);
            DrawCenteredReadout(hpRect,Mathf.CeilToInt(hp)+" / "+Mathf.CeilToInt(max));
            // Charge bar used to sit here. The run clock takes that corner and stays readable.
            DrawRunClock(new Rect(30f,54f,168f,24f));
            Sprite coin = Stage1IsoArt.PickupCoin();
            if(coin!=null && coin.texture!=null) GUI.DrawTexture(new Rect(30f,82f,18f,18f),coin.texture,ScaleMode.ScaleToFit);
            _hudTextStyle.alignment=TextAnchor.MiddleLeft;
            GUI.Label(new Rect(52f,80f,120f,22f),(_demo.RunBuild!=null?_demo.RunBuild.Gold:0).ToString(),_hudTextStyle);
            _hudTextStyle.alignment=TextAnchor.MiddleCenter;
            string hint = _demo.InteractionHint;
            if(!string.IsNullOrEmpty(hint)) {
                if(_hintTextStyle==null) _hintTextStyle=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontSize=15,normal={textColor=JianHaiUiTheme.Bone}};
                float hintWidth=Mathf.Min(440f,_hintTextStyle.CalcSize(new GUIContent(hint)).x+32f);
                float height=Screen.height/scale;
                GUI.color=Color.white;JianHaiUiTheme.ShadowLabel(new Rect((width-hintWidth)*.5f,height-92f,hintWidth,32f),hint,_hintTextStyle);
            }
            GUI.color=previousColor;GUI.matrix=previousMatrix;
        }

        void DrawSkillCooldown()
        {
            if (_focus == null) return;
            if (_focusIcon == null)
                _focusIcon = Resources.Load<Sprite>("JianHaiReward/jh_ui_skill_focus_v01");
            if (_focusIcon == null) return;
            float scale = JianHaiUiTheme.Scale;
            float size = 96f * scale;
            float inset = 5f * scale;
            float margin = 20f * scale;
            var frame = new Rect(Screen.width - margin - size - inset * 2f,
                Screen.height - margin - size - inset * 2f, size + inset * 2f, size + inset * 2f);
            var icon = new Rect(frame.x + inset, frame.y + inset, size, size);
            Texture2D texture = _focusIcon.texture;
            Rect pixels = _focusIcon.textureRect;
            var uv = new Rect(pixels.x / texture.width, pixels.y / texture.height,
                pixels.width / texture.width, pixels.height / texture.height);
            float remaining = Mathf.Clamp01(_focus.CooldownLeft / GuaranteedCritActive.CooldownSeconds);
            Color old = GUI.color;
            // The circular asset supplies its own frame; no rectangular skill panel.
            GUI.color = remaining > 0f ? new Color(.32f, .32f, .32f, 1f) : Color.white;
            GUI.DrawTextureWithTexCoords(icon, texture, uv, true);
            if (remaining > 0f)
            {
                UpdateFocusCooldownRing(remaining);
                GUI.color = Color.white;
                GUI.DrawTexture(frame, _focusCooldownRing, ScaleMode.StretchToFill, true);
            }
            float badgeSize = 22f * scale;
            var badge = new Rect(frame.xMax - badgeSize, frame.yMax - badgeSize, badgeSize, badgeSize);

            if (_focusKeyStyle == null)
                _focusKeyStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold, normal = { textColor = new Color(.94f, .88f, .72f) } };
            _focusKeyStyle.fontSize = Mathf.RoundToInt(15f * scale);
            GUI.color = Color.white;
            JianHaiUiTheme.ShadowLabel(badge, "Q", _focusKeyStyle);
            GUI.color = old;
        }

        void UpdateFocusCooldownRing(float remaining)
        {
            const int resolution = 256;
            if (_focusCooldownRing == null)
            {
                _focusCooldownRing = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
                _focusCooldownRing.name = "Focus remaining cooldown ring";
                _focusCooldownRing.hideFlags = HideFlags.HideAndDontSave;
                _focusCooldownRing.filterMode = FilterMode.Bilinear;
                _focusCooldownRing.wrapMode = TextureWrapMode.Clamp;
                _focusRingPixels = new Color32[resolution * resolution];
                _focusRingAngles = new float[_focusRingPixels.Length];
                _focusRingAlpha = new byte[_focusRingPixels.Length];
                for (int y = 0; y < resolution; y++)
                for (int x = 0; x < resolution; x++)
                {
                    int i = y * resolution + x;
                    float dx = x + .5f - resolution * .5f;
                    float dy = y + .5f - resolution * .5f;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    // 1px feathered edges keep the thin ring readable at HUD scale.
                    _focusRingAlpha[i] = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(Mathf.Min(radius - 120f, 126f - radius)));
                    float angle = Mathf.Atan2(dx, dy);
                    _focusRingAngles[i] = angle < 0f ? angle + Mathf.PI * 2f : angle;
                }
                _focusRingStep = -1;
            }
            int step = Mathf.CeilToInt(Mathf.Clamp01(remaining) * 360f);
            if (step == _focusRingStep) return;
            _focusRingStep = step;
            float end = step * Mathf.Deg2Rad;
            for (int i = 0; i < _focusRingPixels.Length; i++)
            {
                byte alpha = _focusRingAlpha[i];
                _focusRingPixels[i] = _focusRingAngles[i] <= end
                    ? new Color32(238, 194, 105, alpha)
                    : new Color32(30, 47, 60, alpha);
            }
            _focusCooldownRing.SetPixels32(_focusRingPixels);
            _focusCooldownRing.Apply(false, false);
        }

        void OnDestroy()
        {
            if (_focusCooldownRing != null) Destroy(_focusCooldownRing);
        }

        public static Color PressureColor(float minutes)
        {
            return minutes<4f?new Color(1f,.84f,.45f):minutes<8f?new Color(.86f,.50f,.20f):minutes<12f?new Color(.65f,.26f,.16f):new Color(.40f,.09f,.09f);
        }

        // Band fills use the committed pressure colours; the words ride the bar in
        // brightened variants of the same hues so all four stay readable on the
        // dark tray while still reading as gold / orange / red / crimson.
        static readonly Color[] PhaseWordColors =
        {
            new Color(1.00f, .86f, .46f),   // 简单
            new Color(.98f, .60f, .26f),    // 普通
            new Color(.95f, .40f, .26f),    // 困难
            new Color(.88f, .30f, .33f),    // 极难
        };

        /// <summary>Horizontal wall-clock timeline: the needle stays fixed and the
        /// four pressure bands scroll right-to-left past it as the run clock
        /// advances, so each difficulty word rides the bar in its own colour.</summary>
        void DrawTimeLine(Rect rect)
        {
            if (_demo == null)
                return;
            float minutes = Mathf.Max(0f, _demo.RunSeconds) / 60f;
            const float pxPerMin = 12f;
            float needleX = rect.x + rect.width * 0.3f;
            // X of a run minute on the bar: future minutes sit right of the
            // needle, past minutes left, so growing time slides every mark left.
            float MinuteX(float m) { return needleX + (m - minutes) * pxPerMin; }

            Color prev = GUI.color;
            // Dark tray the bands scroll across.
            // No opaque backing; coloured time bands remain the pressure indicator.

            float[] bandStart = { 0f, TimePressure.Band2Min, TimePressure.Band3Min, TimePressure.LateMin };
            float farRightMin = minutes + (rect.xMax - needleX) / pxPerMin;
            for (int i = 0; i < 4; i++)
            {
                float m0 = bandStart[i];
                float m1 = i < 3 ? bandStart[i + 1] : farRightMin;
                float xa = Mathf.Max(rect.x, MinuteX(m0));
                float xb = Mathf.Min(rect.xMax, MinuteX(m1));
                if (xb - xa < 0.5f)
                    continue;
                Color fill = PressureColor((m0 + m1) * 0.5f);
                fill.a = .42f;
                GUI.color = fill;
                GUI.DrawTexture(new Rect(xa, rect.y, xb - xa, rect.height), Texture2D.whiteTexture, ScaleMode.StretchToFill);
            }

            // Minute ticks scroll with the bands and make the motion read.
            for (int m = Mathf.CeilToInt(minutes - (needleX - rect.x) / pxPerMin); m <= Mathf.FloorToInt(farRightMin); m++)
            {
                float x = MinuteX(m);
                if (x < rect.x + 1f || x > rect.xMax - 1f)
                    continue;
                GUI.color = new Color(1f, 1f, 1f, m % 5 == 0 ? .38f : .16f);
                float h = m % 5 == 0 ? 7f : 4f;
                GUI.DrawTexture(new Rect(x - .5f, rect.yMax - h - 2f, 1f, h), Texture2D.whiteTexture);
            }

            // Knot lines between the bands (4 / 7 / 10 minutes).
            for (int i = 1; i < 4; i++)
            {
                float x = MinuteX(bandStart[i]);
                if (x < rect.x || x > rect.xMax)
                    continue;
                GUI.color = new Color(.95f, .93f, .88f, .55f);
                GUI.DrawTexture(new Rect(x - .5f, rect.y, 1f, rect.height), Texture2D.whiteTexture);
            }

            // Everything left of the needle is the past: dim it under a veil.
            GUI.color = new Color(0f, 0f, 0f, .45f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, needleX - rect.x, rect.height), Texture2D.whiteTexture);

            // Difficulty words on their bands; a band already past the needle fades.
            var word = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            for (int i = 0; i < 4; i++)
            {
                float m0 = bandStart[i];
                float m1 = i < 3 ? bandStart[i + 1] : farRightMin;
                float xa = Mathf.Max(rect.x, MinuteX(m0));
                float xb = Mathf.Min(rect.xMax, MinuteX(m1));
                if (xb - xa < 34f)
                    continue;
                string label = TimePressure.PhaseLabel((m0 + m1) * 0.5f);
                Color c = PhaseWordColors[i];
                c.a = (xa + xb) * 0.5f < needleX ? .85f : 1f;
                word.normal.textColor = c;
                var wr = new Rect(xa, rect.y, xb - xa, rect.height);
                GUI.color = new Color(0f, 0f, 0f, .6f);
                GUI.Label(new Rect(wr.x + 1f, wr.y + 1f, wr.width, wr.height), label, word);
                GUI.color = Color.white;
                GUI.Label(wr, label, word);
            }

            // Caps above/below the tray mark the minute without covering its word.
            GUI.color = new Color(1f, 1f, 1f, .95f);
            GUI.DrawTexture(new Rect(needleX - 1.5f, rect.yMax - 3f, 3f, 5f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(needleX - 5f, rect.y - 5f, 10f, 3f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(needleX - 1.5f, rect.y - 2f, 3f, 3f), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        /// <summary>MM:SS.s of <see cref="Stage1MazeDemo.RunSeconds"/>, the pressure wall clock.</summary>
        public static string FormatRunClock(float seconds)
        {
            if (float.IsNaN(seconds) || seconds < 0f)
                seconds = 0f;
            int whole = Mathf.FloorToInt(seconds);
            int tenth = Mathf.FloorToInt((seconds - whole) * 10f + 0.0001f);
            if (tenth >= 10)
            {
                tenth = 0;
                whole++;
            }

            int minutes = whole / 60;
            int secs = whole % 60;
            return minutes.ToString("00") + ":" + secs.ToString("00") + "." + tenth.ToString();
        }

        /// <summary>Top-left run clock (replaces the charge bar slot and the old centre MM:SS clock).</summary>
        void DrawRunClock(Rect rect)
        {
            if (_demo == null)
                return;
            if (_clockStyle == null)
            {
                _clockStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 20,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = new Color(0.92f, 0.94f, 0.97f) }
                };
            }

            string text = FormatRunClock(_demo.RunSeconds);
            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.Label(new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height), text, _clockStyle);
            GUI.color = Color.white;
            GUI.Label(rect, text, _clockStyle);
            GUI.color = prev;
        }

        void DrawCenteredReadout(Rect rect, string text)
        {
            if (_hudTextStyle == null)
                _hudTextStyle = new GUIStyle(GUI.skin.label){fontSize=13,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,normal={textColor=JianHaiUiTheme.Bone}};
            _hudTextStyle.alignment = TextAnchor.MiddleCenter;
            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, .8f);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, _hudTextStyle);
            GUI.color = Color.white;
            GUI.Label(rect, text, _hudTextStyle);
            GUI.color = prev;
        }

        /// <summary>
        /// Boss HP owns the top-center slot only in the boss room or while that fight is running.
        /// One stack: <see cref="BossBrain.Hp"/> / <see cref="BossBrain.MaxHp"/> (P1 and P2 share it).
        /// This HUD is the only top bar.
        /// </summary>
        bool TryCenterBossHp(out float hp, out float max)
        {
            hp = 0f;
            max = 1f;
            BossFightDriver fight = BossFightDriver.Live;
            bool fighting = fight != null && fight.FightStarted && !fight.FightSettled;
            if (!fighting && !PlayerInBossRoom(fight))
                return false;
            return TryReadBossHp(fight, out hp, out max);
        }

        // Local adaptation of PR #34: the local FinalBossCombat (MonoBehaviour) has no Hp/MaxHp,
        // so only BossBrain is read. BossBrain is the single HP stack on this branch.
        static bool TryReadBossHp(BossFightDriver fight, out float hp, out float max)
        {
            hp = 0f;
            max = 0f;
            if (fight == null)
                return false;
            if (fight.Brain != null && fight.Brain.MaxHp > 0.01f)
            {
                hp = Mathf.Max(0f, fight.Brain.Hp);
                max = fight.Brain.MaxHp;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Boss room: a maze node named BOSS (not PreBoss), the maze node that contains the
        /// live boss, or the HOOKS BOSS site radius when the player is not inside some other room.
        /// The combat leash is wider than that hall and overlaps PreBoss, so it is not the room test.
        /// </summary>
        bool PlayerInBossRoom(BossFightDriver fight)
        {
            if (_demo == null || _demo.PlayerBody == null)
                return false;
            Vector3 p = _demo.PlayerBody.position;
            MazeNode room = RoomAt(_demo.BuiltMaze, p.x, p.y);
            if (room != null)
            {
                if (IsBossRoomId(room.Id))
                    return true;
                if (fight != null && room.Contains(fight.transform.position.x, fight.transform.position.y, 0f))
                    return true;
                return false;
            }

            return InHooksBossHall(p.x, p.y);
        }

        static MazeNode RoomAt(Stage1Maze maze, float x, float y)
        {
            if (maze == null || maze.Nodes == null)
                return null;
            MazeNode best = null;
            float bestArea = float.MaxValue;
            for (int i = 0; i < maze.Nodes.Length; i++)
            {
                MazeNode n = maze.Nodes[i];
                if (n == null || !n.Contains(x, y, 0f))
                    continue;
                float area = n.Width * n.Height;
                if (area < bestArea)
                {
                    bestArea = area;
                    best = n;
                }
            }

            return best;
        }

        static bool IsBossRoomId(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;
            if (id.Equals("BOSS", System.StringComparison.OrdinalIgnoreCase))
                return true;
            if (id.StartsWith("BOSS", System.StringComparison.OrdinalIgnoreCase))
                return true;
            return id.EndsWith("_BOSS", System.StringComparison.OrdinalIgnoreCase);
        }

        static bool InHooksBossHall(float x, float y)
        {
            SiteDef site;
            if (!LockSiteCatalog.TryGet("BOSS", out site))
                return false;
            float r = site.NoSpawnRadius > 0.01f ? site.NoSpawnRadius : 4f;
            float dx = x - site.Position.x;
            float dy = y - site.Position.y;
            return dx * dx + dy * dy <= r * r;
        }

        void DrawBossHpBar(Rect rect, float hp, float max)
        {
            float shown = max > 0.01f ? Mathf.Clamp01(hp / max) : 0f;
            Color prev = GUI.color;
            GUI.color = new Color(0.10f, 0.10f, 0.12f, 0.92f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
            if (shown > 0.001f)
            {
                GUI.color = new Color(0.68f, 0.25f, 0.18f, 1f);
                GUI.DrawTexture(
                    new Rect(rect.x, rect.y, rect.width * shown, rect.height),
                    Texture2D.whiteTexture,
                    ScaleMode.StretchToFill);
            }

            GUI.color = prev;
            DrawCenteredReadout(rect, Mathf.CeilToInt(Mathf.Max(0f, hp)) + " / " + Mathf.CeilToInt(max));
        }

        void DrawBar(string backId, string fillId, Rect rect, float amount)
        {
            Sprite back = Ui(backId);
            Sprite fill = Ui(fillId);
            if (back != null && back.texture != null)
                GUI.DrawTexture(rect, back.texture, ScaleMode.StretchToFill);
            if (fill == null || fill.texture == null || amount <= 0.001f)
                return;
            float shown = Mathf.Clamp01(amount);
            var cut = new Rect(rect.x, rect.y, rect.width * shown, rect.height);
            GUI.DrawTextureWithTexCoords(cut, fill.texture, new Rect(0f, 0f, shown, 1f));
        }

        void DrawChargeBar(Rect rect, ShotRead read, float held)
        {
            Color previous = GUI.color;
            GUI.color = new Color(.64f, .67f, .72f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            var inner = new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f);
            GUI.color = new Color(.10f, .12f, .16f);
            GUI.DrawTexture(inner, Texture2D.whiteTexture);
            float fill = Mathf.Clamp01(read.Progress(held));
            GUI.color = held >= read.GreenEnter && held <= read.GreenExit
                ? new Color(.35f, 1f, .58f) : new Color(.90f, .69f, .36f);
            if (fill > 0f) GUI.DrawTexture(new Rect(inner.x, inner.y, inner.width * fill, inner.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        Sprite Ui(string file)
        {
            return Stage1IsoArt.LoadSprite("Assets/Art/JianHai/UI/" + file + ".png");
        }

        /// <summary>Red edges stay hidden above 30% hp, then ease in as hp drops.</summary>
        static float VignetteFor(float hpRatio)
        {
            return Mathf.Clamp01((0.3f - hpRatio) / 0.3f);
        }

        static Texture2D _vignetteTex;

        /// <summary>Soft radial falloff: clear centre, dark red only at the corners.</summary>
        static Texture2D VignetteTexture()
        {
            if (_vignetteTex != null)
                return _vignetteTex;
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(u * u + v * v) / 1.4142f;
                    float a = Mathf.Clamp01((d - 0.45f) / 0.55f);
                    a = a * a * (3f - 2f * a);
                    pixels[y * size + x] = new Color32(72, 6, 6, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            _vignetteTex = tex;
            return tex;
        }

        void DrawVignette()
        {
            if (Vignette01 <= 0.001f)
                return;
            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, Vignette01 * 0.85f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), VignetteTexture(), ScaleMode.StretchToFill);
            GUI.color = prev;
        }

        void DrawFlash()
        {
            if (_flash <= 0.001f)
                return;
            Color prev = GUI.color;
            GUI.color = new Color(1f, 0.85f, 0.75f, _flash);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        public const float MinimapSize = 190f;
        /// <summary>Rooms at 1.5 px per unit (was 3.2) so the window spans most of the
        /// maze: corridors draw short and the 28×22 room blocks read as small chips.</summary>
        public const float MinimapScale = 1.5f;

        /// <summary>Player world position lands on the middle of the map.</summary>
        public static Vector2 MinimapLocal(float wx, float wy, float playerX, float playerY)
        {
            return new Vector2(
                MinimapSize * 0.5f + (wx - playerX) * MinimapScale,
                MinimapSize * 0.5f - (wy - playerY) * MinimapScale);
        }

        void DrawMinimap()
        {
            Stage1Maze maze = _demo.BuiltMaze;
            if (maze == null || maze.Nodes == null)
                return;
            Matrix4x4 mapMatrix = GUI.matrix;
            float uiScale = MapOverview ? 1f : JianHaiUiTheme.Scale;
            if(!MapOverview) GUI.matrix=Matrix4x4.Scale(new Vector3(uiScale,uiScale,1f));
            float map = MapOverview ? Mathf.Min(Screen.height-100f,Screen.width-100f) : MinimapSize;
            var box = MapOverview ? new Rect((Screen.width-map)*.5f,(Screen.height-map)*.5f,map,map) : new Rect(Screen.width/uiScale-map-20f,20f,map,map);
            GUI.color=Color.white;
            Vector2 playerPos = _demo.PlayerBody != null ? _demo.PlayerBody.position : Vector3.zero;
            float loX=float.MaxValue,hiX=float.MinValue,loY=float.MaxValue,hiY=float.MinValue;
            foreach(var n in maze.Nodes){if(!_visited.Contains(n.Id))continue;loX=Mathf.Min(loX,n.Center.X-n.Width*.5f);hiX=Mathf.Max(hiX,n.Center.X+n.Width*.5f);loY=Mathf.Min(loY,n.Center.Y-n.Height*.5f);hiY=Mathf.Max(hiY,n.Center.Y+n.Height*.5f);}
            foreach(var part in _corridors){if(!part.Seen)continue;loX=Mathf.Min(loX,part.A.x,part.B.x);hiX=Mathf.Max(hiX,part.A.x,part.B.x);loY=Mathf.Min(loY,part.A.y,part.B.y);hiY=Mathf.Max(hiY,part.A.y,part.B.y);}
            if(loX==float.MaxValue){GUI.matrix=mapMatrix;return;}
            float scale=MapOverview?Mathf.Min((map-48f)/(hiX-loX),(map-64f)/(hiY-loY)):MinimapScale;
            Vector2 centre=MapOverview?new Vector2((loX+hiX)*.5f,(loY+hiY)*.5f):playerPos;
            GUI.BeginGroup(box);

            Vector2 MapPos(float wx, float wy)
            {
                return new Vector2(map*.5f+(wx-centre.x)*scale,map*.5f-(wy-centre.y)*scale);
            }

            var line = Texture2D.whiteTexture;
            foreach(var part in _corridors)
                if(part.Seen) DrawMapLine(MapPos(part.A.x,part.A.y),MapPos(part.B.x,part.B.y),2f,new Color(.55f,.50f,.40f,.85f),line);

            for (int i = 0; i < maze.Nodes.Length; i++)
            {
                MazeNode n = maze.Nodes[i];
                if (!_visited.Contains(n.Id))
                    continue;
                Vector2 c = MapPos(n.Center.X, n.Center.Y);
                float w = n.Width * scale;
                float h = n.Height * scale;
                bool here = _demo.PlayerBody != null && n.Contains(playerPos.x, playerPos.y, 0.2f);
                GUI.color = RoomColor(n.Kind, here);
                if (MapOverview && !_visited.Contains(n.Id)) GUI.color *= new Color(.65f,.65f,.65f,1f);
                GUI.DrawTexture(new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, h), Texture2D.whiteTexture, ScaleMode.StretchToFill);
                GUI.color = Color.white;
                if (MapOverview && n.Kind == MazeNodeKind.Altar) GUI.Label(new Rect(c.x+8f,c.y-10f,110f,22f),"第"+(int)_demo.StageForRoom(n.Id)+"阶段祭坛");
                if (MapOverview && (n.Kind == MazeNodeKind.Boss || n.Kind == MazeNodeKind.Safe))
                    GUI.Label(new Rect(Mathf.Clamp(c.x+8f,6f,map-120f),Mathf.Clamp(c.y-10f,34f,map-22f),114f,22f),n.Kind==MazeNodeKind.Boss?"骸冠典狱长":"终局安全段");
            }
            if (MapOverview) GUI.Label(new Rect(12f,8f,map-24f,26f),"已探索（M 收起）");

            Vector2 pp = MapPos(playerPos.x, playerPos.y);
            GUI.color = new Color(.03f, .04f, .06f);
            GUI.DrawTexture(new Rect(pp.x - 6f, pp.y - 6f, 12f, 12f), Texture2D.whiteTexture);
            GUI.color = new Color(1f, .90f, .52f);
            GUI.DrawTexture(new Rect(pp.x - 4f, pp.y - 4f, 8f, 8f), Texture2D.whiteTexture, ScaleMode.StretchToFill);
            GUI.color = Color.white;
            GUI.EndGroup();
            GUI.matrix=mapMatrix;
        }

        static void DrawMapLine(Vector2 a, Vector2 b, float width, Color color, Texture tex)
        {
            Vector2 d = b - a;
            if (d.sqrMagnitude < 0.01f)
                return;
            Color prev = GUI.color;
            Matrix4x4 prevMat = GUI.matrix;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, a);
            GUI.DrawTexture(new Rect(a.x, a.y - width * 0.5f, d.magnitude, width), tex, ScaleMode.StretchToFill);
            GUI.matrix = prevMat;
            GUI.color = prev;
        }

        static Color RoomColor(MazeNodeKind kind, bool current)
        {
            Color c;
            switch (kind)
            {
                case MazeNodeKind.Start: c = new Color(0.36f, 0.55f, 0.32f); break;
                case MazeNodeKind.Heal: c = new Color(.76f,.22f,.29f); break;
                case MazeNodeKind.Altar: c = new Color(0.5f, 0.34f, 0.6f); break;
                case MazeNodeKind.Chest:
                case MazeNodeKind.LargeChest: c = new Color(0.72f, 0.58f, 0.26f); break;
                case MazeNodeKind.Connector: c = new Color(0.3f, 0.55f, 0.55f); break;
                case MazeNodeKind.Boss: c = new Color(0.64f, 0.25f, 0.19f); break;
                case MazeNodeKind.Safe: c = new Color(0.30f, 0.48f, 0.44f); break;
                default: c = new Color(0.42f, 0.45f, 0.52f); break;
            }
            return current ? Color.Lerp(c, Color.white, 0.45f) : c;
        }

        void DrawIcons()
        {
            float scale=JianHaiUiTheme.Scale;
            Matrix4x4 previous=GUI.matrix;GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1f));
            float width=Screen.width/scale,height=Screen.height/scale;
            int columns=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(width-170f,width*.34f)/52f),1,8);
            if(_iconBadgeStyle==null) _iconBadgeStyle=new GUIStyle(GUI.skin.label){fontSize=12,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,normal={textColor=JianHaiUiTheme.Bone}};
            if(_iconTooltipStyle==null) _iconTooltipStyle=new GUIStyle(GUI.skin.label){fontSize=14,wordWrap=true,padding=new RectOffset(10,10,8,8),normal={textColor=JianHaiUiTheme.Bone}};
            for(int i=0;i<_iconNames.Count;i++) {
                var rect=new Rect(20f+(i%columns)*52f,height-64f-(i/columns)*52f,44f,44f);
                Sprite sprite=Icon(_iconNames[i]);
                GUI.color=Color.white;
                if(sprite!=null)GUI.DrawTexture(new Rect(rect.x+3f,rect.y+3f,38f,38f),sprite.texture,ScaleMode.ScaleToFit);
                int count=0;foreach(string id in _demo.RunBuild.OwnedRewardIds)if(RewardCatalog.FamilyKey(id)==_iconNames[i] || id==_iconNames[i])count++;
                if(count>1){var badge=new Rect(rect.xMax-17f,rect.yMax-16f,17f,16f);GUI.color=Color.white;JianHaiUiTheme.ShadowLabel(badge,count.ToString(),_iconBadgeStyle);}
                if(rect.Contains(Event.current.mousePosition)){
                    string desc=RewardPresent.TryForId(_iconNames[i],out RewardPresent.Copy copy)||RewardPresent.TryForCatalogName(_iconNames[i],out copy)?copy.Sentence:"";
                    var tip=new Rect(Mathf.Min(rect.x,width-270f),rect.y-84f,250f,76f);
                    GUI.color=Color.white;
                    JianHaiUiTheme.ShadowLabel(tip,_iconNames[i]+(count>1?" ×"+count:"")+"\n"+desc,_iconTooltipStyle);
                }
            }
            GUI.color=Color.white;GUI.matrix=previous;
        }

        void DrawDeath()
        {
            RunEndInfo end = _demo != null ? _demo.EndInfo() : new RunEndInfo();
            var box = new Rect(Screen.width * 0.5f - 180f, Screen.height * 0.5f - 110f, 360f, 220f);
            var frame = Resources.Load<Sprite>("JianHaiReward/jh_ui_panel_bone");
            if (frame != null && frame.texture != null)
                GUI.DrawTexture(box, frame.texture, ScaleMode.StretchToFill);
            else
            {
                GUI.color=new Color(.025f,.035f,.045f,.95f);
                GUI.DrawTexture(box,Texture2D.whiteTexture);
                GUI.color=Color.white;
            }
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.91f, 0.88f, 0.82f) }
            };
            GUI.Label(new Rect(box.x, box.y + 12f, box.width, 28f), _demo.ConnSettle ? _demo.EndTitle : "本局结束", style);
            GUI.Label(new Rect(box.x, box.y + 48f, box.width, 24f), "用时 " + end.Seconds + " 秒", style);
            GUI.Label(new Rect(box.x, box.y + 76f, box.width, 24f), "强化 " + end.Rewards, style);
            GUI.Label(new Rect(box.x, box.y + 104f, box.width, 24f), "留下 " + end.Keep, style);
            if (GUI.Button(new Rect(box.x + 40f, box.y + 152f, box.width - 80f, 40f), Stage1MazeDemo.RestartLabel, JianHaiUiTheme.MenuButton())
                && _demo != null)
                _demo.RestartRun();
        }

        Sprite Icon(string rewardName)
        {
            Sprite sprite;
            if (_icons.TryGetValue(rewardName, out sprite) && sprite != null)
                return sprite;
            string file;
            if (!IconByName.TryGetValue(rewardName, out file))
            {
                // crit2 属性档（VIT_C/…）：按家族复用已有图标。
                if (RogueShooter.Build.RewardPresent.TryForId(rewardName, out RogueShooter.Build.RewardPresent.Copy copy) || RogueShooter.Build.RewardPresent.TryForCatalogName(rewardName,out copy))
                    file = copy.Icon;
            }
            if (string.IsNullOrEmpty(file))
                return null;
            sprite = Resources.Load<Sprite>("JianHaiReward/" + file);
            if (sprite == null)
                sprite = Stage1IsoArt.LoadSprite("Assets/Resources/JianHaiReward/" + file + ".png");
            if (sprite != null)
                _icons[rewardName] = sprite;
            return sprite;
        }
    }
}
