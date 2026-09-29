using System.Collections.Generic;
using UnityEngine;
using RogueShooter.Balance;
using RogueShooter.Build;
using RogueShooter.Maze;
using RogueShooter.Player;

namespace RogueShooter.Demo
{
    /// <summary>
    /// Stage-1 play chrome. Numbers and copy come from the committed rules:
    /// TimePressure labels, EconomyGold inherit, GuaranteedCritActive.HudText.
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

        Stage1MazeDemo _demo;
        PlayerVitals _vitals;
        PlayerCharge _charge;
        GuaranteedCritActive _focus;
        float _prevHp = -1f;
        float _flash;
        float _peakFlash;
        bool _dead;
        int _inherit;

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

            float minutes = Time.timeSinceLevelLoad / 60f;
            PhaseText = TimePressure.PhaseLabel(minutes);
            CritText = _focus != null ? _focus.HudLine : GuaranteedCritActive.HudText(0f, 0f);

            float hp = _vitals != null ? _vitals.Hp : 0f;
            float max = _vitals != null && _vitals.MaxHp > 0.01f ? _vitals.MaxHp : 1f;
            HpText = "血量 " + Mathf.CeilToInt(hp) + "/" + Mathf.CeilToInt(max);
            int gold = _demo.RunBuild != null ? _demo.RunBuild.Gold : 0;
            GoldText = "金币 " + gold;
            float held = _charge != null ? _charge.HeldSeconds : 0f;
            float fill = ChargeShotRules.Progress(held);
            ChargeText = "蓄力 " + Mathf.RoundToInt(fill * 100f) + "%";
            Vignette01 = Mathf.Clamp01(1f - hp / max);
            if (_flash > 0f)
                _flash = Mathf.MoveTowards(_flash, 0f, Time.deltaTime * 2.5f);

            if (!_dead && _vitals != null && _vitals.Hp <= 0.001f)
            {
                _dead = true;
                _inherit = EconomyGold.DeathInherit(gold);
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
            const int w = 280;
            const int h = 96;
            GUI.Box(new Rect(8, 8, w, h), "");
            var style = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            GUI.Label(new Rect(16, 12, w - 16, 18), HpText + "    " + GoldText, style);
            GUI.Label(new Rect(16, 32, w - 16, 18), ChargeText, style);
            GUI.Label(new Rect(16, 52, w - 16, 18), CritText, style);
            GUI.Label(new Rect(16, 72, w - 16, 18), PhaseText, style);
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
#if UNITY_EDITOR
            string path = "Assets/Resources/JianHaiReward/" + file + ".png";
            sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
#endif
            if (sprite != null)
                _icons[rewardName] = sprite;
            return sprite;
        }
    }
}
