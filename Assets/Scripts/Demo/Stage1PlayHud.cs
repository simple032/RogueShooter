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
            DrawTimeRing(new Rect(12f, 12f, 72f, 72f));
            float hp = _vitals != null ? _vitals.Hp : 0f;
            float max = _vitals != null && _vitals.MaxHp > 0.01f ? _vitals.MaxHp : 1f;
            DrawBar("jh_ui_bar_hp_back", "jh_ui_bar_hp_fill", new Rect(96f, 24f, 180f, 22f), hp / max);
            float held = _charge != null ? _charge.HeldSeconds : 0f;
            ShotRead read = _charge != null ? _charge.CurrentRead : ShotRead.From(null);
            DrawBar("jh_ui_bar_hp_back", "jh_ui_bar_hp_fill", new Rect(96f, 52f, 180f, 10f), read.Progress(held));
            int gold = _demo.RunBuild != null ? _demo.RunBuild.Gold : 0;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            Sprite coin = Stage1IsoArt.PickupCoin();
            if (coin != null && coin.texture != null)
                GUI.DrawTexture(new Rect(96f, 64f, 18f, 18f), coin.texture, ScaleMode.ScaleToFit);
            GUI.Label(new Rect(118f, 66f, 80f, 18f), gold.ToString(), style);
        }

        void DrawTimeRing(Rect rect)
        {
            Sprite ring = Ui("jh_ui_ring_time");
            if (ring == null || ring.texture == null)
                return;
            // Second hand of the wall clock. No lap length in minutes. Restarts at 0.
            float angle = _demo.RunSeconds * 6f;
            Matrix4x4 prev = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, rect.center);
            GUI.DrawTexture(rect, ring.texture);
            GUI.matrix = prev;
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

        public const float MinimapSize = 168f;
        public const float MinimapScale = 3.2f;

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
            const float map = MinimapSize;
            var box = new Rect(Screen.width - map - 8f, 8f, map, map);
            GUI.Box(box, "");
            Vector2 playerPos = _demo.PlayerBody != null ? _demo.PlayerBody.position : Vector3.zero;
            GUI.BeginGroup(box);

            Vector2 MapPos(float wx, float wy)
            {
                return MinimapLocal(wx, wy, playerPos.x, playerPos.y);
            }

            var line = Texture2D.whiteTexture;
            for (int e = 0; e < maze.Edges.Length; e++)
            {
                MazeEdge edge = maze.Edges[e];
                if (!_visited.Contains(edge.FromId) || !_visited.Contains(edge.ToId))
                    continue;
                MazeVec2[] pts = edge.Points;
                if (pts == null || pts.Length < 2)
                    pts = new[] { edge.From, edge.To };
                for (int i = 0; i < pts.Length - 1; i++)
                {
                    Vector2 a = MapPos(pts[i].X, pts[i].Y);
                    Vector2 b = MapPos(pts[i + 1].X, pts[i + 1].Y);
                    DrawMapLine(a, b, 3f, new Color(0.55f, 0.5f, 0.4f, 0.85f), line);
                }
            }

            for (int i = 0; i < maze.Nodes.Length; i++)
            {
                MazeNode n = maze.Nodes[i];
                if (!_visited.Contains(n.Id))
                    continue;
                Vector2 c = MapPos(n.Center.X, n.Center.Y);
                float w = n.Width * MinimapScale;
                float h = n.Height * MinimapScale;
                bool here = _demo.PlayerBody != null && n.Contains(playerPos.x, playerPos.y, 0.2f);
                GUI.color = RoomColor(n.Kind, here);
                GUI.DrawTexture(new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, h), Texture2D.whiteTexture, ScaleMode.StretchToFill);
                GUI.color = Color.white;
            }

            Vector2 pp = MapPos(playerPos.x, playerPos.y);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(pp.x - 3f, pp.y - 3f, 6f, 6f), Texture2D.whiteTexture, ScaleMode.StretchToFill);
            GUI.color = Color.white;
            GUI.EndGroup();
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
                case MazeNodeKind.Altar: c = new Color(0.5f, 0.34f, 0.6f); break;
                case MazeNodeKind.Chest:
                case MazeNodeKind.LargeChest: c = new Color(0.72f, 0.58f, 0.26f); break;
                case MazeNodeKind.Connector: c = new Color(0.3f, 0.55f, 0.55f); break;
                default: c = new Color(0.42f, 0.45f, 0.52f); break;
            }
            return current ? Color.Lerp(c, Color.white, 0.45f) : c;
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
            {
                // crit2 属性档（VIT_C/…）：按家族复用已有图标。
                if (RogueShooter.Build.RewardPresent.TryForId(rewardName, out RogueShooter.Build.RewardPresent.Copy copy))
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
