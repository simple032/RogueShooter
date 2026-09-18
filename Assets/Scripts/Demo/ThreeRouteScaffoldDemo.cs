using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using RogueShooter.Balance;
using RogueShooter.Layout;
using RogueShooter.Player;
using RogueShooter.Spawning;
using RogueShooter.Vision;

namespace RogueShooter.Demo
{
    /// <summary>
    /// Playable α/β/γ → Pre → BOSS scaffold with v0.4.2-LOCK spawn bands.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class ThreeRouteScaffoldDemo : MonoBehaviour
    {
        [SerializeField] float orthographicSize = 2.5f;
        [SerializeField] float moveSpeed = 10f;
        [SerializeField] float demoClockScale = 20f;

        BalanceLockData _lock;
        SpawnBandClock _clock;
        SpawnBandDirector _director;
        CameraViewService _view;
        readonly Dictionary<string, GameObject> _markers = new Dictionary<string, GameObject>();
        SpawnAnchor[] _demoAnchors;
        bool _pass;
        string _status = "loading…";

        IEnumerator Start()
        {
            if (!BalanceLock.TryLoadFromResources(out _lock, out string error))
            {
                _status = "FAIL " + error;
                Debug.LogError("[ThreeRouteScaffold] " + _status);
                yield break;
            }

            LogLock();
            BuildWorld();
            yield return null;
            RunNamedDemoSpawns();
            RunAcceptance();
        }

        void Update()
        {
            if (_clock == null)
                return;
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
                _clock.JumpToBand("Z1");
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
                _clock.JumpToBand("Z2");
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
                _clock.JumpToBand("Z3");
            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
                _clock.JumpToBand("Pre");
            if (Input.GetKeyDown(KeyCode.Space))
                _clock.SetPaused(!_clock.Paused);
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.Plus) || Input.GetKeyDown(KeyCode.KeypadPlus))
                _clock.SetClockScale(_clock.ClockScale + 5f);
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
                _clock.SetClockScale(Mathf.Max(1f, _clock.ClockScale - 5f));
            if (Input.GetKeyDown(KeyCode.R))
                _clock.ResetClock();
        }

        void LogLock()
        {
            Debug.Log($"[BalanceLock] loaded {_lock.lockVersion} from {_lock.sourceOfTruth}");
            Debug.Log($"[BalanceLock] P_spawn={_lock.pSpawn:0.00} " +
                      $"chest={_lock.RarityWeight(_lock.chestRarity, "C"):0.00}/{_lock.RarityWeight(_lock.chestRarity, "R"):0.00}/{_lock.RarityWeight(_lock.chestRarity, "E"):0.00} " +
                      $"altar={_lock.RarityWeight(_lock.altarRarity, "C"):0.00}/{_lock.RarityWeight(_lock.altarRarity, "R"):0.00}/{_lock.RarityWeight(_lock.altarRarity, "E"):0.00}");

            string[] ids = { "Z1", "Z2", "Z3" };
            for (int i = 0; i < ids.Length; i++)
            {
                SegmentMul seg = _lock.GetSegment(ids[i]);
                TimeScaleMul time = _lock.GetTimeScale(seg.timeRef);
                float eff = BalanceMath.ComposeSpawnInterval(seg, time);
                WaveSpec wave = _lock.GetWave(ids[i]);
                ClockBand clock = _lock.GetClockBand(ids[i]);
                Debug.Log($"[BalanceLock] {ids[i]} clock {clock.startMin:0}–{clock.endMin:0}′ waves×{wave.count} t_wave={wave.tWaveMidSeconds:0} " +
                          $"HP×{seg.hpMul} DMG×{seg.dmgMul} interval×{seg.intervalMul} " +
                          $"eff={eff:0.00}s (base {seg.baseIntervalSeconds} × seg {seg.intervalMul} × {time.id} {time.spawnIntervalMul})");
            }

            for (int i = 0; i < _lock.timeScale.Length; i++)
            {
                TimeScaleMul t = _lock.timeScale[i];
                Debug.Log($"[BalanceLock] {t.id} {t.tStartMin:0}–{t.tEndMin:0}′ attr×{t.enemyAttrMul} interval×{t.spawnIntervalMul}");
            }
        }

        void BuildWorld()
        {
            Transform root = transform;
            Color floor = new Color(0.18f, 0.175f, 0.17f);
            for (int i = 0; i < LockSiteCatalog.Corridors.Length; i++)
            {
                CorridorDef c = LockSiteCatalog.Corridors[i];
                if (!LockSiteCatalog.TryGet(c.FromId, out SiteDef from) || !LockSiteCatalog.TryGet(c.ToId, out SiteDef to))
                    continue;
                DemoPrimitives.Corridor(
                    "Corridor_" + c.FromId + "_" + c.ToId,
                    from.Position, to.Position,
                    LockSiteCatalog.CorridorWidth, floor, 0, root);
            }

            for (int i = 0; i < LockSiteCatalog.Sites.Length; i++)
            {
                SiteDef site = LockSiteCatalog.Sites[i];
                Vector3 pos = new Vector3(site.Position.x, site.Position.y, 0f);
                if (site.Kind == SiteKind.Switch)
                    pos += VisualSwitchOffset(site.Id);
                else if (site.Id == "C04")
                    pos += new Vector3(0.55f, 0.55f, 0f);

                float size = site.Kind == SiteKind.Switch ? 0.42f : 0.7f;
                GameObject marker = DemoPrimitives.Quad(
                    site.Id, pos, new Vector2(size, size),
                    LockSiteCatalog.ColorFor(site.Kind), 4, root);
                _markers[site.Id] = marker;

                if (LockSiteCatalog.IsNoSpawnKind(site.Kind))
                    marker.AddComponent<NoSpawnCore>().Configure(site.Id, site.Kind.ToString(), _lock.noSpawnRadius);

                AddWorldLabel(marker, site.Id);
            }

            GameObject player = new GameObject("Player");
            player.transform.position = Vector3.zero;
            DemoPrimitives.AddSprite(player, new Color(0.95f, 0.84f, 0.28f), 8);
            player.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
            player.AddComponent<PlayerMotor2D>().Configure(moveSpeed);

            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                cam = camGo.AddComponent<Camera>();
                camGo.tag = "MainCamera";
                camGo.AddComponent<AudioListener>();
            }

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.045f, 0.06f);
            cam.orthographic = true;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;

            _view = cam.GetComponent<CameraViewService>();
            if (_view == null)
                _view = cam.gameObject.AddComponent<CameraViewService>();
            _view.Configure(orthographicSize);

            CameraFollow2D follow = cam.GetComponent<CameraFollow2D>();
            if (follow == null)
                follow = cam.gameObject.AddComponent<CameraFollow2D>();
            follow.SetTarget(player.transform);

            _clock = gameObject.AddComponent<SpawnBandClock>();

            GameObject stubPrefab = new GameObject("StubEnemyPrefab");
            stubPrefab.transform.SetParent(root, false);
            stubPrefab.SetActive(false);
            DemoPrimitives.AddSprite(stubPrefab, new Color(0.86f, 0.28f, 0.24f), 6);
            stubPrefab.transform.localScale = new Vector3(0.85f, 0.85f, 1f);
            stubPrefab.AddComponent<StubEnemy>();

            var slots = new List<SpawnBandDirector.Slot>();
            for (int i = 0; i < LockSiteCatalog.Corridors.Length; i++)
            {
                CorridorDef c = LockSiteCatalog.Corridors[i];
                if (c.BandId == "Pre")
                    continue;
                if (!LockSiteCatalog.TryGet(c.FromId, out SiteDef from) || !LockSiteCatalog.TryGet(c.ToId, out SiteDef to))
                    continue;
                Vector2 mid = (from.Position + to.Position) * 0.5f;
                slots.Add(new SpawnBandDirector.Slot
                {
                    Id = "BandStub_" + c.BandId + "_" + c.FromId + "_" + c.ToId,
                    Position = new Vector3(mid.x, mid.y, 0f),
                    BandId = c.BandId
                });
            }

            _director = gameObject.AddComponent<SpawnBandDirector>();
            _director.Bind(_lock, _clock, slots.ToArray(), stubPrefab);
            _clock.Bind(_lock, demoClockScale);

            _demoAnchors = new[]
            {
                MakeDemoAnchor("Demo_InView", new Vector3(0f, 1.2f, 0f), new Color(0.35f, 0.75f, 1f), root),
                MakeDemoAnchor("Demo_NearCore_C01", new Vector3(12f, 0f, 0f), new Color(1f, 0.45f, 0.2f), root),
                MakeDemoAnchor("Demo_Corridor_Z1", new Vector3(6.5f, 1.8f, 0f), new Color(1f, 0.82f, 0.25f), root),
            };

            Debug.Log("[ThreeRouteScaffold] IDs Shared: C01 C02 A_Shared C03@PreOnly");
            Debug.Log("[ThreeRouteScaffold] IDs α: A2 A3 C04 C06 C05 DE02 DE04");
            Debug.Log("[ThreeRouteScaffold] IDs β: A1 A5 C07 C08 C09 DE01 DE03");
            Debug.Log("[ThreeRouteScaffold] IDs γ: A4 A6 C10 C11 C12");
            Debug.Log("[ThreeRouteScaffold] IDs Pre: Shop_01  switches: Z1_End@HubA Z2_End@HubB Z2_End_beta@A5前 Z2_End_gamma@A6前 Z3_End@Pre");
        }

        static Vector3 VisualSwitchOffset(string id)
        {
            switch (id)
            {
                case "Z1_End": return new Vector3(-0.55f, -0.55f, 0f);
                case "Z2_End": return new Vector3(-0.55f, -0.55f, 0f);
                case "Z3_End": return new Vector3(-0.55f, -0.55f, 0f);
                default: return Vector3.zero;
            }
        }

        static void AddWorldLabel(GameObject marker, string text)
        {
            var label = new GameObject("Label_" + text);
            label.transform.SetParent(marker.transform, false);
            label.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            var tm = label.AddComponent<TextMesh>();
            tm.text = text;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.characterSize = 0.18f;
            tm.fontSize = 24;
            tm.color = Color.white;
            Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font != null)
                tm.font = font;
        }

        static SpawnAnchor MakeDemoAnchor(string id, Vector3 pos, Color color, Transform parent)
        {
            GameObject go = DemoPrimitives.Quad("Anchor_" + id, pos, new Vector2(0.5f, 0.5f), color, 5, parent);
            var anchor = go.AddComponent<SpawnAnchor>();
            anchor.Configure(id, pos);
            return anchor;
        }

        void RunNamedDemoSpawns()
        {
            if (_director == null || _demoAnchors == null)
                return;
            for (int i = 0; i < _demoAnchors.Length; i++)
            {
                SpawnAnchor a = _demoAnchors[i];
                _director.EvaluateNamed(a.AnchorId, a.WorldPosition, "start");
            }
        }

        void RunAcceptance()
        {
            var missing = new List<string>(LockSiteCatalog.MissingRequiredIds());
            foreach (string id in LockSiteCatalog.RequiredShared)
                if (!_markers.ContainsKey(id)) missing.Add("marker:" + id);
            foreach (string id in LockSiteCatalog.RequiredAlpha)
                if (!_markers.ContainsKey(id)) missing.Add("marker:" + id);
            foreach (string id in LockSiteCatalog.RequiredBeta)
                if (!_markers.ContainsKey(id)) missing.Add("marker:" + id);
            foreach (string id in LockSiteCatalog.RequiredGamma)
                if (!_markers.ContainsKey(id)) missing.Add("marker:" + id);
            foreach (string id in LockSiteCatalog.RequiredPre)
                if (!_markers.ContainsKey(id)) missing.Add("marker:" + id);
            foreach (string id in LockSiteCatalog.RequiredSwitches)
                if (!_markers.ContainsKey(id)) missing.Add("marker:" + id);
            bool idsOk = missing.Count == 0;
            bool configOk = _lock != null;
            bool viewOk = _view != null && Mathf.Abs(_view.OrthographicSize - orthographicSize) < 0.01f;

            bool inViewSkip = _demoAnchors != null
                && SpawnViewGate.ShouldSkipSpawn(_demoAnchors[0].WorldPosition)
                && !_director.WasSpawned("Demo_InView");
            bool coreSkip = _demoAnchors != null
                && SpawnCoreGate.IsInsideNoSpawnCore(_demoAnchors[1].WorldPosition)
                && !_director.WasSpawned("Demo_NearCore_C01");
            bool corridorSpawn = _demoAnchors != null && _director.WasSpawned("Demo_Corridor_Z1");

            SegmentMul z1 = _lock.GetSegment("Z1");
            TimeScaleMul t0 = _lock.GetTimeScale("T0");
            float z1Eff = BalanceMath.ComposeSpawnInterval(z1, t0);

            _pass = idsOk && configOk && viewOk && inViewSkip && coreSkip && corridorSpawn && z1Eff > 4.5f && z1Eff < 4.9f;
            var sb = new StringBuilder();
            sb.Append(_pass ? "ACCEPTANCE PASS" : "ACCEPTANCE FAIL");
            sb.Append($" idsOk={idsOk} view={viewOk} inViewSkip={inViewSkip} coreSkip={coreSkip} corridorSpawn={corridorSpawn} z1Eff={z1Eff:0.00}");
            if (!idsOk)
                sb.Append(" missing=" + string.Join(",", missing.ToArray()));
            _status = sb.ToString();
            if (_pass)
                Debug.Log("[ThreeRouteScaffold] " + _status);
            else
                Debug.LogError("[ThreeRouteScaffold] " + _status);
        }

        void OnGUI()
        {
            const int pad = 8;
            int w = 560;
            int h = 250;
            GUI.Box(new Rect(pad, pad, w, h), "");
            var style = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            var title = new GUIStyle(style) { fontSize = 15, fontStyle = FontStyle.Bold };
            var rich = new GUIStyle(style) { richText = true };

            GUI.Label(new Rect(pad + 8, pad + 4, w - 16, 20), "三路脚手架 / Spawn bands  v0.4.2-LOCK", title);

            string band = _clock != null ? _clock.BandId : "?";
            float mins = _clock != null ? _clock.WallMinutes : 0f;
            float interval = _director != null ? _director.CurrentInterval() : 0f;
            SegmentMul seg = _lock != null ? _lock.GetSegment(band) : null;
            TimeScaleMul time = _lock != null ? _lock.TimeScaleAtMinutes(mins) : null;
            WaveSpec wave = _lock != null ? _lock.GetWave(band) : null;

            GUI.Label(new Rect(pad + 8, pad + 26, w - 16, 70),
                $"WASD move · 1/2/3 band · 4=Pre · Space pause · +/- clock · R reset\n" +
                $"band={band}  wall={mins:0.00}′  demoClock×{(_clock != null ? _clock.ClockScale : 0f):0}  {(_clock != null && _clock.Paused ? "PAUSED" : "")}\n" +
                $"waves {(_clock != null ? _clock.WaveIndex : 0)}/{(_clock != null ? _clock.WaveCount : 0)}  t_wave mid={(wave != null ? wave.tWaveMidSeconds : 0f):0}s  " +
                $"eff interval={interval:0.00}s\n" +
                $"HP×{(seg != null ? seg.hpMul : 0f):0.000}  DMG×{(seg != null ? seg.dmgMul : 0f):0.000}  " +
                $"time { (time != null ? time.id : "-") } attr×{(time != null ? time.enemyAttrMul : 0f):0.00} int×{(time != null ? time.spawnIntervalMul : 0f):0.00}",
                style);

            if (_lock != null)
            {
                GUI.Label(new Rect(pad + 8, pad + 100, w - 16, 34),
                    $"P_spawn={_lock.pSpawn:0.00}  chest C/R/E=" +
                    $"{_lock.RarityWeight(_lock.chestRarity, "C"):0.00}/{_lock.RarityWeight(_lock.chestRarity, "R"):0.00}/{_lock.RarityWeight(_lock.chestRarity, "E"):0.00}" +
                    $"  altar={_lock.RarityWeight(_lock.altarRarity, "C"):0.00}/{_lock.RarityWeight(_lock.altarRarity, "R"):0.00}/{_lock.RarityWeight(_lock.altarRarity, "E"):0.00}\n" +
                    $"SoT {_lock.sourceOfTruth}  ortho={orthographicSize}",
                    style);
            }

            string status = _pass
                ? "<color=#88ff88>" + _status + "</color>"
                : "<color=#ffcc88>" + _status + "</color>";
            GUI.Label(new Rect(pad + 8, pad + 138, w - 16, 36), status, rich);

            if (_demoAnchors != null && _view != null)
            {
                float y = pad + 176;
                for (int i = 0; i < _demoAnchors.Length; i++)
                {
                    SpawnAnchor a = _demoAnchors[i];
                    bool inView = _view.IsInCameraView(a.WorldPosition);
                    bool spawned = _director != null && _director.WasSpawned(a.AnchorId);
                    GUI.Label(new Rect(pad + 8, y, w - 16, 16),
                        $"{a.AnchorId}  {(inView ? "IN VIEW" : "OUT")}  {(spawned ? "SPAWNED" : "not spawned")}",
                        style);
                    y += 16;
                }
            }

            DrawIdPanel();
            DrawViewBorder();
        }

        void DrawIdPanel()
        {
            const int pad = 8;
            int x = Screen.width - 328;
            if (x < 580)
                x = 580;
            int w = 320;
            int h = Mathf.Min(420, Screen.height - 16);
            GUI.Box(new Rect(x, pad, w, h), "");
            var style = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            var title = new GUIStyle(style) { fontStyle = FontStyle.Bold };
            GUI.Label(new Rect(x + 8, pad + 4, w - 16, 18), "Lock IDs (v0.4.1 topology)", title);
            GUI.Label(new Rect(x + 8, pad + 24, w - 16, h - 32),
                "Shared: C01 C02 A_Shared C03@PreOnly\n" +
                "α: A2 A3 C04 C06 C05 DE02 DE04\n" +
                "β: A1 A5 C07 C08 C09 DE01 DE03\n" +
                "γ: A4 A6 C10 C11 C12\n" +
                "Pre: Shop_01   BOSS stub\n" +
                "Switches:\n" +
                "  Z1_End@HubA\n" +
                "  Z2_End@HubB\n" +
                "  Z2_End_beta@A5前\n" +
                "  Z2_End_gamma@A6前\n" +
                "  Z3_End@Pre entrance\n\n" +
                "Cores: Hub / Altar / Chest / Shop / Pre\n" +
                "+ SpawnViewGate skip-in-view",
                style);
        }

        static void DrawViewBorder()
        {
            Color old = GUI.color;
            GUI.color = new Color(0.35f, 0.9f, 1f, 0.85f);
            const int t = 3;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, Screen.height - t, Screen.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, 0, t, Screen.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(Screen.width - t, 0, t, Screen.height), Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
