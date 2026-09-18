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
        [Tooltip("0 = use demo_spawnband_defaults.csv demo_clock_scale")]
        [SerializeField] float demoClockScaleOverride = 0f;

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
            if (!BalanceLock.TryLoadFromStreamingAssets(out _lock, out string error))
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
            Debug.Log($"[BalanceLock] files={string.Join(",", _lock.loadedFiles ?? new string[0])}");
            Debug.Log($"[BalanceLock] P_spawn={_lock.pSpawn:0.00} n_altar={_lock.nAltar} n_chest={_lock.nChestSlots} " +
                      $"E[B] α={_lock.eBuildAlpha:0.0} β={_lock.eBuildBeta:0.0} γ={_lock.eBuildGamma:0.0}");
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
                Debug.Log($"[BalanceLock] {ids[i]} clock {clock.startMin:0.##}–{clock.endMin:0.##}′ waves×{wave.count} t_wave={wave.tWaveMidSeconds:0} " +
                          $"HP×{seg.hpMul} DMG×{seg.dmgMul} interval×{seg.intervalMul} csvCombinedInt×{seg.csvCombinedIntervalMul} " +
                          $"eff={eff:0.00}s (base {seg.baseIntervalSeconds} × seg {seg.intervalMul} × {time.id} {time.spawnIntervalMul})");
            }

            Debug.Log($"[BalanceLock] shop gold LOADED inherit={_lock.shopInheritRate:0.00} cap={_lock.shopInheritCap:0} build_from_shop={_lock.shopBuildFromShop} " +
                      $"arrive_p50={_lock.ShopGoldValue("arrive_shop_p50")} shelf={_lock.ShopGoldValue("shelf_roll")} path Pre_ready={_lock.pathPreReadySeconds:0}s");
            if (_lock.gaps != null)
            {
                for (int i = 0; i < _lock.gaps.Length; i++)
                    Debug.Log("[BalanceLock][GAP] " + _lock.gaps[i]);
            }
        }

        void BuildWorld()
        {
            Transform root = transform;
            Color room = new Color(0.16f, 0.155f, 0.15f);
            for (int i = 0; i < LockSiteCatalog.Rooms.Length; i++)
            {
                RoomDef r = LockSiteCatalog.Rooms[i];
                Vector3 c = new Vector3(r.Center.x, r.Center.y, 1.1f);
                DemoPrimitives.Quad("Room_" + r.Id, c, r.Size, room, 0, root);
            }

            Color floor = new Color(0.18f, 0.175f, 0.17f);
            for (int i = 0; i < LockSiteCatalog.Corridors.Length; i++)
            {
                CorridorDef c = LockSiteCatalog.Corridors[i];
                if (!LockSiteCatalog.TryGet(c.FromId, out SiteDef from) || !LockSiteCatalog.TryGet(c.ToId, out SiteDef to))
                    continue;
                DemoPrimitives.Corridor(
                    "Corridor_" + c.FromId + "_" + c.ToId,
                    from.Position, to.Position,
                    LockSiteCatalog.CorridorWidth, floor, 1, root);
            }

            for (int i = 0; i < LockSiteCatalog.Sites.Length; i++)
            {
                SiteDef site = LockSiteCatalog.Sites[i];
                Vector3 pos = new Vector3(site.Position.x, site.Position.y, 0f);
                float size = site.Kind == SiteKind.Switch ? 0.42f : 0.7f;
                GameObject marker = DemoPrimitives.Quad(
                    site.Id, pos, new Vector2(size, size),
                    LockSiteCatalog.ColorFor(site.Kind), 4, root);
                _markers[site.Id] = marker;

                if (site.IsNoSpawnCore)
                    marker.AddComponent<NoSpawnCore>().Configure(site.Id, site.Kind.ToString(), site.NoSpawnRadius);

                AddWorldLabel(marker, site.Id, LabelOffset(site));
            }

            Vector3 startPos = Vector3.zero;
            if (LockSiteCatalog.TryGet("START", out SiteDef startSite))
                startPos = new Vector3(startSite.Position.x, startSite.Position.y, 0f);

            GameObject player = new GameObject("Player");
            player.transform.position = startPos;
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
            float clockScale = demoClockScaleOverride > 0.01f ? demoClockScaleOverride : _lock.demoClockScale;
            _clock.Bind(_lock, clockScale);

            _demoAnchors = new[]
            {
                MakeDemoAnchor("Demo_InView", startPos + new Vector3(0.2f, 0.5f, 0f), new Color(0.35f, 0.75f, 1f), root),
                MakeDemoAnchor("Demo_NearCore_Chest_01", new Vector3(6f, 11f, 0f), new Color(1f, 0.45f, 0.2f), root),
                MakeDemoAnchor("Demo_Corridor_Z1", new Vector3(5f, 7.5f, 0f), new Color(1f, 0.82f, 0.25f), root),
            };

            Debug.Log("[ThreeRouteScaffold] HOOKS Shared: START Chest_01 Chest_02 A_Shared Chest_03 HubA Anchor_S1_End");
            Debug.Log("[ThreeRouteScaffold] HOOKS α: A2 Chest_04 HubB Chest_06 DE02 A3 DE04 Chest_05 Anchor_S2_End_α");
            Debug.Log("[ThreeRouteScaffold] HOOKS β: A1 DE01 Chest_07 Chest_08 A5 DE03 Chest_09 Anchor_S2_End_β");
            Debug.Log("[ThreeRouteScaffold] HOOKS γ: A4 Chest_10 Room_γCombat Chest_11 A6 Chest_12 Anchor_S2_End_γ");
            Debug.Log("[ThreeRouteScaffold] HOOKS Pre: PreBoss Shop_01 Chest_03 Anchor_S3_End BOSS");
        }

        static Vector3 LabelOffset(SiteDef site)
        {
            if (site.Id == "Chest_02" || site.Id == "Chest_06" || site.Id == "Chest_03")
                return new Vector3(0.7f, -0.85f, 0f);
            return new Vector3(0f, 0.85f, 0f);
        }

        static void AddWorldLabel(GameObject marker, string text, Vector3 localOffset)
        {
            var label = new GameObject("Label_" + text);
            label.transform.SetParent(marker.transform, false);
            label.transform.localPosition = localOffset;
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
                && !_director.WasSpawned("Demo_NearCore_Chest_01");
            bool corridorSpawn = _demoAnchors != null && _director.WasSpawned("Demo_Corridor_Z1");

            bool csvOk = _lock != null && _lock.loadedFiles != null;
            if (csvOk)
            {
                for (int i = 0; i < BalanceLockLoader.RequiredCsvs.Length; i++)
                {
                    bool found = false;
                    for (int j = 0; j < _lock.loadedFiles.Length; j++)
                    {
                        if (_lock.loadedFiles[j] == BalanceLockLoader.RequiredCsvs[i])
                        {
                            found = true;
                            break;
                        }
                    }
                    csvOk &= found;
                }
            }

            SegmentMul z1 = _lock.GetSegment("Z1");
            TimeScaleMul t0 = _lock.GetTimeScale("T0");
            float z1Eff = BalanceMath.ComposeSpawnInterval(z1, t0);

            _pass = idsOk && configOk && csvOk && viewOk && inViewSkip && coreSkip && corridorSpawn && z1Eff > 4.5f && z1Eff < 4.9f;
            var sb = new StringBuilder();
            sb.Append(_pass ? "ACCEPTANCE PASS" : "ACCEPTANCE FAIL");
            sb.Append($" idsOk={idsOk} csvOk={csvOk} view={viewOk} inViewSkip={inViewSkip} coreSkip={coreSkip} corridorSpawn={corridorSpawn} z1Eff={z1Eff:0.00}");
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
                    $"SoT {_lock.sourceOfTruth}  shop={_lock.shopGoldStatus} inherit={_lock.shopInheritRate:0.00} build_from_shop={_lock.shopBuildFromShop}  ortho={orthographicSize}",
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
            GUI.Label(new Rect(x + 8, pad + 4, w - 16, 18), "HOOKS v0.4.1 IDs", title);
            GUI.Label(new Rect(x + 8, pad + 24, w - 16, h - 32),
                "Shared: START Chest_01/02 A_Shared HubA\n" +
                "α: A2 C04 HubB C06 DE02 A3 DE04 C05\n" +
                "β: A1 DE01 C07 C08 A5 DE03 C09\n" +
                "γ: A4 C10 γCombat C11 A6 C12\n" +
                "Pre: PreBoss C03 Shop_01 BOSS\n" +
                "Switches:\n" +
                "  Anchor_S1_End @ HubA north\n" +
                "  Anchor_S2_End_α @ HubB north\n" +
                "  Anchor_S2_End_β @ A5南口前\n" +
                "  Anchor_S2_End_γ @ A6南口前\n" +
                "  Anchor_S3_End @ Pre north\n\n" +
                "Cores: HOOKS radii (START4 Hub3.5 A2.5 C2 Shop3 Pre4)\n" +
                "+ SpawnViewGate skip-in-view\n" +
                "SoT CSVs + HOOKS_v041_LOCKED.md\n" +
                "DEMO_STUB: clock× only",
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
