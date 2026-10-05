using System;
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
        };

        readonly HashSet<string> _visited = new HashSet<string>();
        readonly List<string> _iconNames = new List<string>();
        readonly Dictionary<string, Sprite> _icons = new Dictionary<string, Sprite>();
        int _loggedOwned = -1;

        Stage1MazeDemo _demo;
        PlayerVitals _vitals;
        PlayerCharge _charge;
        GuaranteedCritActive _focus;
        float _prevHp = -1f;
        float _flash;
        float _peakFlash;
        bool _dead;
        int _inherit;
        GUIStyle _hpStyle;
        GUIStyle _clockStyle;
        GUIStyle _goldStyle;
        GUIStyle _phaseStyle;

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
            _visited.Clear();
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
            NoteRoom();
            NoteHit();
            NoteIcons();

            float minutes = _demo.RunSeconds / 60f;
            PhaseText = TimePressure.PhaseLabel(minutes);
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
            Vignette01 = Mathf.Clamp01(1f - hp / max);
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
                RewardRow row;
                if (!RewardCatalog.TryGet(build.OwnedRewardIds[i], out row))
                    continue;
                if (!seen.Add(row.Name))
                    continue;
                if (!IconByName.ContainsKey(row.Name))
                    continue;
                _iconNames.Add(row.Name);
            }
        }

        void OnGUI()
        {
            if (_demo == null)
                return;
            DrawStatus();
            DrawVignette();
            DrawFlash();
            DrawMinimap();
            DrawIcons();
            if (_dead)
                DrawDeath();
        }

        void DrawStatus()
        {
            // Same slot as the pressure timeline. Boss HP replaces it; it is not drawn on top.
            var center = new Rect(Screen.width * 0.5f - 150f, 20f, 300f, 24f);
            float bossHp;
            float bossMax;
            if (TryCenterBossHp(out bossHp, out bossMax))
                DrawBossHpBar(center, bossHp, bossMax);
            else
                DrawTimeLine(center);

            float hp = _vitals != null ? _vitals.Hp : 0f;
            float max = _vitals != null && _vitals.MaxHp > 0.01f ? _vitals.MaxHp : 1f;
            var hpRect = new Rect(30f, 28f, 232f, 24f);
            DrawBar("jh_ui_bar_hp_back", "jh_ui_bar_hp_fill", hpRect, hp / max);
            DrawCenteredReadout(hpRect, Mathf.CeilToInt(hp) + " / " + Mathf.CeilToInt(max));
            // Charge bar used to sit here. The run clock takes that corner and stays readable.
            DrawRunClock(new Rect(30f, 54f, 168f, 24f));

            int gold = _demo.RunBuild != null ? _demo.RunBuild.Gold : 0;
            if (_goldStyle == null)
            {
                _goldStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = new Color(0.91f, 0.88f, 0.82f) }
                };
            }

            Sprite coin = Stage1IsoArt.PickupCoin();
            if (coin != null && coin.texture != null)
                GUI.DrawTexture(new Rect(30f, 82f, 18f, 18f), coin.texture, ScaleMode.ScaleToFit);
            GUI.Label(new Rect(52f, 80f, 120f, 22f), gold.ToString(), _goldStyle);
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
            if (_hpStyle == null)
            {
                _hpStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.91f, 0.88f, 0.82f) }
                };
            }

            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, _hpStyle);
            GUI.color = Color.white;
            GUI.Label(rect, text, _hpStyle);
            GUI.color = prev;
        }

        /// <summary>
        /// Boss HP owns the top-center slot only in the boss room or while that fight is running.
        /// One stack: <see cref="BossBrain.Hp"/> / <see cref="BossBrain.MaxHp"/> (P1 and P2 share it;
        /// <see cref="FinalBossCombat"/> reads the same fields). This HUD is the only top bar.
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

            if (fight.Combat != null && fight.Combat.MaxHp > 0.01f)
            {
                hp = Mathf.Max(0f, fight.Combat.Hp);
                max = fight.Combat.MaxHp;
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
            if (id.Equals("BOSS", StringComparison.OrdinalIgnoreCase))
                return true;
            if (id.StartsWith("BOSS", StringComparison.OrdinalIgnoreCase))
                return true;
            return id.EndsWith("_BOSS", StringComparison.OrdinalIgnoreCase);
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

        static readonly Color[] PhaseWordColors =
        {
            new Color(1.00f, 0.86f, 0.46f),
            new Color(0.98f, 0.60f, 0.26f),
            new Color(0.95f, 0.40f, 0.26f),
            new Color(0.88f, 0.30f, 0.33f),
        };

        static Color PressureColor(float minutes)
        {
            if (minutes < TimePressure.Band2Min)
                return new Color(1f, 0.84f, 0.45f);
            if (minutes < TimePressure.Band3Min)
                return new Color(0.86f, 0.50f, 0.20f);
            if (minutes < TimePressure.LateMin)
                return new Color(0.65f, 0.26f, 0.16f);
            return new Color(0.40f, 0.09f, 0.09f);
        }

        /// <summary>
        /// Horizontal wall-clock timeline. The needle stays fixed and the four pressure
        /// bands scroll right-to-left as <see cref="Stage1MazeDemo.RunSeconds"/> advances.
        /// </summary>
        void DrawTimeLine(Rect rect)
        {
            if (_demo == null)
                return;
            float minutes = Mathf.Max(0f, _demo.RunSeconds) / 60f;
            const float pxPerMin = 12f;
            float needleX = rect.x + rect.width * 0.3f;
            float farRightMin = minutes + (rect.xMax - needleX) / pxPerMin;
            float[] bandStart = { 0f, TimePressure.Band2Min, TimePressure.Band3Min, TimePressure.LateMin };

            Color prev = GUI.color;
            for (int i = 0; i < 4; i++)
            {
                float m0 = bandStart[i];
                float m1 = i < 3 ? bandStart[i + 1] : farRightMin;
                float xa = Mathf.Max(rect.x, MinuteX(m0, minutes, needleX, pxPerMin));
                float xb = Mathf.Min(rect.xMax, MinuteX(m1, minutes, needleX, pxPerMin));
                if (xb - xa < 0.5f)
                    continue;
                Color fill = PressureColor((m0 + m1) * 0.5f);
                fill.a = 0.42f;
                GUI.color = fill;
                GUI.DrawTexture(new Rect(xa, rect.y, xb - xa, rect.height), Texture2D.whiteTexture, ScaleMode.StretchToFill);
            }

            int firstTick = Mathf.CeilToInt(minutes - (needleX - rect.x) / pxPerMin);
            int lastTick = Mathf.FloorToInt(farRightMin);
            for (int m = firstTick; m <= lastTick; m++)
            {
                float x = MinuteX(m, minutes, needleX, pxPerMin);
                if (x < rect.x + 1f || x > rect.xMax - 1f)
                    continue;
                GUI.color = new Color(1f, 1f, 1f, m % 5 == 0 ? 0.38f : 0.16f);
                float h = m % 5 == 0 ? 7f : 4f;
                GUI.DrawTexture(new Rect(x - 0.5f, rect.yMax - h - 2f, 1f, h), Texture2D.whiteTexture);
            }

            for (int i = 1; i < 4; i++)
            {
                float x = MinuteX(bandStart[i], minutes, needleX, pxPerMin);
                if (x < rect.x || x > rect.xMax)
                    continue;
                GUI.color = new Color(0.95f, 0.93f, 0.88f, 0.55f);
                GUI.DrawTexture(new Rect(x - 0.5f, rect.y, 1f, rect.height), Texture2D.whiteTexture);
            }

            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, needleX - rect.x, rect.height), Texture2D.whiteTexture);

            if (_phaseStyle == null)
            {
                _phaseStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }

            for (int i = 0; i < 4; i++)
            {
                float m0 = bandStart[i];
                float m1 = i < 3 ? bandStart[i + 1] : farRightMin;
                float xa = Mathf.Max(rect.x, MinuteX(m0, minutes, needleX, pxPerMin));
                float xb = Mathf.Min(rect.xMax, MinuteX(m1, minutes, needleX, pxPerMin));
                if (xb - xa < 34f)
                    continue;
                string label = TimePressure.PhaseLabel((m0 + m1) * 0.5f);
                Color c = PhaseWordColors[i];
                c.a = (xa + xb) * 0.5f < needleX ? 0.85f : 1f;
                _phaseStyle.normal.textColor = c;
                var word = new Rect(xa, rect.y, xb - xa, rect.height);
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.Label(new Rect(word.x + 1f, word.y + 1f, word.width, word.height), label, _phaseStyle);
                GUI.color = Color.white;
                GUI.Label(word, label, _phaseStyle);
            }

            GUI.color = new Color(1f, 1f, 1f, 0.95f);
            GUI.DrawTexture(new Rect(needleX - 1.5f, rect.yMax - 3f, 3f, 5f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(needleX - 5f, rect.y - 5f, 10f, 3f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(needleX - 1.5f, rect.y - 2f, 3f, 3f), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        static float MinuteX(float minute, float nowMinutes, float needleX, float pxPerMin)
        {
            return needleX + (minute - nowMinutes) * pxPerMin;
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

        Sprite Ui(string file)
        {
            return Stage1IsoArt.LoadSprite("Assets/Art/JianHai/UI/" + file + ".png");
        }

        void DrawVignette()
        {
            if (Vignette01 <= 0.001f)
                return;
            Color prev = GUI.color;
            GUI.color = new Color(0.45f, 0.02f, 0.02f, Vignette01);
            const float band = 72f;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, band), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, Screen.height - band, Screen.width, band), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, 0, band, Screen.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(Screen.width - band, 0, band, Screen.height), Texture2D.whiteTexture);
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

        void DrawMinimap()
        {
            Stage1Maze maze = _demo.BuiltMaze;
            if (maze == null || maze.Nodes == null)
                return;
            const float map = 168f;
            var box = new Rect(Screen.width - map - 8f, 8f, map, map);
            GUI.Box(box, "");
            var style = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter };
            int shown = 0;
            for (int i = 0; i < maze.Nodes.Length; i++)
            {
                MazeNode n = maze.Nodes[i];
                if (!_visited.Contains(n.Id))
                    continue;
                float x = box.x + 8f;
                float y = box.y + 8f + shown * 22f;
                GUI.Label(new Rect(x, y, map - 16f, 20f), n.Id + " " + MazeRules.Label(n.Kind), style);
                shown++;
            }
        }

        void DrawIcons()
        {
            float x = 12f;
            float y = Screen.height - 52f;
            for (int i = 0; i < _iconNames.Count; i++)
            {
                Sprite sprite = Icon(_iconNames[i]);
                var rect = new Rect(x, y, 36f, 36f);
                if (sprite != null && sprite.texture != null)
                    GUI.DrawTexture(rect, sprite.texture);
                x += 42f;
            }
        }

        void DrawDeath()
        {
            var box = new Rect(Screen.width * 0.5f - 180f, Screen.height * 0.5f - 60f, 360f, 120f);
            GUI.Box(box, "");
            var style = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(box.x, box.y + 16f, box.width, 28f), "本局结束", style);
            int pct = Mathf.RoundToInt(EconomyGold.DeathInheritRate * 100f);
            GUI.Label(new Rect(box.x, box.y + 52f, box.width, 28f),
                "继承金币 " + _inherit + "（" + pct + "%，上限 " + EconomyGold.DeathInheritCap + "）", style);
        }

        Sprite Icon(string rewardName)
        {
            Sprite sprite;
            if (_icons.TryGetValue(rewardName, out sprite) && sprite != null)
                return sprite;
            string file;
            if (!IconByName.TryGetValue(rewardName, out file))
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
