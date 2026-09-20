using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using RogueShooter.Ai;
using RogueShooter.Art;
using RogueShooter.Balance;
using RogueShooter.Boss;
using RogueShooter.Build;
using RogueShooter.DeadEnd;
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
        [SerializeField] float orthographicSize = 2.5f; // HOOKS lock; occupancy via JianHaiBind scale, not 2.7–3.5
        [Tooltip("0 = use MoveSpeeds.Player (L=22 lock)")]
        [SerializeField] float moveSpeed = 0f;
        [Tooltip("0 = use demo_spawnband_defaults.csv demo_clock_scale")]
        [SerializeField] float demoClockScaleOverride = 0f;

        BalanceLockData _lock;
        SpawnBandClock _clock;
        SpawnBandDirector _director;
        ChestAltarDirector _buildDir;
        DeadEndRunner _deadEnd;
        Transform _player;
        CameraViewService _view;
        readonly Dictionary<string, GameObject> _markers = new Dictionary<string, GameObject>();
        SpawnAnchor[] _demoAnchors;
        BossFightDriver _boss;
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
            TryBossEnter();
            if (_clock == null)
                return;
            if (_buildDir != null && _buildDir.Offering)
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
            if (Input.GetKeyDown(KeyCode.F1))
                TeleportTo("Chest_01");
            if (Input.GetKeyDown(KeyCode.F2))
                TeleportTo("A_Shared");
            if (Input.GetKeyDown(KeyCode.F3))
                TeleportTo("Shop_01");
            if (Input.GetKeyDown(KeyCode.F4))
                TeleportToNearestMob();
            if (Input.GetKeyDown(KeyCode.F5))
                TeleportTo("DE01");
            if (Input.GetKeyDown(KeyCode.F6))
                TeleportTo("DE02");
            if (Input.GetKeyDown(KeyCode.F7))
                TeleportTo("DE03");
            if (Input.GetKeyDown(KeyCode.F8))
                TeleportTo("DE04");
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
            Debug.Log($"[BalanceLock] Power {_lock.powerBuildCoef:0.00}*B+{_lock.powerRarityCoef:0.00}*RS formula={_lock.powerFormula} shop_in_power={_lock.shopInPower} " +
                      $"pool={(_lock.rewardPool != null ? _lock.rewardPool.Length : 0)} detect={_lock.mobDetectRadius:0.0} disengage×{_lock.mobDisengageMul:0.0}");
            if (_lock.gaps != null)
            {
                for (int i = 0; i < _lock.gaps.Length; i++)
                    Debug.Log("[BalanceLock][GAP] " + _lock.gaps[i]);
            }
        }

        void BuildWorld()
        {
            Transform root = transform;
            var siteRuntimes = new List<SiteRuntime>();
            Color room = new Color(0.102f, 0.114f, 0.141f);
            for (int i = 0; i < LockSiteCatalog.Rooms.Length; i++)
            {
                RoomDef r = LockSiteCatalog.Rooms[i];
                Vector3 c = new Vector3(r.Center.x, r.Center.y, 1.1f);
                GameObject floorGo = DemoPrimitives.Quad("Room_" + r.Id, c, r.Size, room, 0, root);
                JianHaiBind.SetLayer(floorGo, JianHaiArtCatalog.LayerGround, 0);
            }

            Color floor = new Color(0.165f, 0.188f, 0.220f);
            for (int i = 0; i < LockSiteCatalog.Corridors.Length; i++)
            {
                CorridorDef c = LockSiteCatalog.Corridors[i];
                if (!LockSiteCatalog.TryGet(c.FromId, out SiteDef from) || !LockSiteCatalog.TryGet(c.ToId, out SiteDef to))
                    continue;
                GameObject cor = DemoPrimitives.Corridor(
                    "Corridor_" + c.FromId + "_" + c.ToId,
                    from.Position, to.Position,
                    LockSiteCatalog.CorridorWidth, floor, 1, root);
                JianHaiBind.SetLayer(cor, JianHaiArtCatalog.LayerGround, 1);
            }

            for (int i = 0; i < LockSiteCatalog.Sites.Length; i++)
            {
                SiteDef site = LockSiteCatalog.Sites[i];
                Vector3 pos = new Vector3(site.Position.x, site.Position.y, 0f);
                GameObject marker = SpawnSiteMarker(site, pos, root);
                _markers[site.Id] = marker;

                if (site.IsNoSpawnCore)
                    marker.AddComponent<NoSpawnCore>().Configure(site.Id, site.Kind.ToString(), site.NoSpawnRadius);

                if (site.Kind == SiteKind.Chest || site.Kind == SiteKind.Altar || site.Kind == SiteKind.Shop)
                {
                    var runtime = marker.AddComponent<SiteRuntime>();
                    runtime.Configure(site, site.Kind != SiteKind.Chest);
                    siteRuntimes.Add(runtime);
                }

                AddWorldLabel(marker, site.Id, LabelOffset(site));
            }

            Vector3 startPos = Vector3.zero;
            if (LockSiteCatalog.TryGet("START", out SiteDef startSite))
                startPos = new Vector3(startSite.Position.x, startSite.Position.y, 0f);

            GameObject player = new GameObject("Player");
            player.transform.position = startPos;
            JianHaiBind.ApplyTo(player, JianHaiArtCatalog.PlayerIdle);
            float playerSpeed = moveSpeed > 0.0001f ? moveSpeed : MoveSpeeds.Player;
            player.AddComponent<PlayerMotor2D>().Configure(playerSpeed);
            player.AddComponent<PlayerVitals>().Configure(EnemyDamageCatalog.PlayerMaxHpRef);
            player.AddComponent<PlayerStrike>().Configure(_lock != null ? _lock.strikeRange : 1.85f);
            player.AddComponent<PlayerCharge>();
            player.AddComponent<GuaranteedCritActive>();
            _player = player.transform;
            Debug.Log($"[MoveSpeed] player={playerSpeed:0.00000} charge×{MoveSpeeds.ChargeMul:0.0} " +
                      $"E1={MoveSpeeds.E1:0.00000} E2={MoveSpeeds.E2:0.00000} E3={MoveSpeeds.E3:0.00000} E4={MoveSpeeds.E4:0.00000}");

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
            JianHaiBind.ApplyTo(stubPrefab, JianHaiArtCatalog.EnemyE1Idle);
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
            float clockScale = demoClockScaleOverride > 0.01f ? demoClockScaleOverride : _lock.demoClockScale;
            _clock.Bind(_lock, clockScale);
            var pressureTracker = gameObject.AddComponent<TimePressureTracker>();
            pressureTracker.Bind(_clock);

            _buildDir = gameObject.AddComponent<ChestAltarDirector>();
            _buildDir.Bind(_lock, _player, siteRuntimes, Environment.TickCount, _clock);
            _director.Bind(_lock, _clock, slots.ToArray(), stubPrefab, _player, OnStubKilled, _buildDir.Build);

            _deadEnd = gameObject.AddComponent<DeadEndRunner>();
            _deadEnd.Bind(_player, _buildDir, _director, root, _markers);

            _demoAnchors = new[]
            {
                MakeDemoAnchor("Demo_InView", new Vector3(5.2f, 7.8f, 0f), new Color(0.35f, 0.75f, 1f), root),
                MakeDemoAnchor("Demo_NearCore_Chest_01", new Vector3(6f, 11f, 0f), new Color(1f, 0.45f, 0.2f), root),
                MakeDemoAnchor("Demo_Corridor_Z1", new Vector3(24f, 0f, 0f), new Color(1f, 0.82f, 0.25f), root),
                MakeDemoAnchor("Demo_EdgeBuffer", new Vector3(10.2f, 7.5f, 0f), new Color(0.9f, 0.4f, 0.9f), root),
            };

            Debug.Log("[ThreeRouteScaffold] HOOKS Shared: START Chest_01 Chest_02 A_Shared Chest_03 HubA Anchor_S1_End");
            Debug.Log("[ThreeRouteScaffold] HOOKS α: A2 Chest_04 HubB Chest_06 DE02 A3 DE04 Chest_05 Anchor_S2_End_α");
            Debug.Log("[ThreeRouteScaffold] HOOKS β: A1 DE01 Chest_07 Chest_08 A5 DE03 Chest_09 Anchor_S2_End_β");
            Debug.Log("[ThreeRouteScaffold] HOOKS γ: A4 Chest_10 Room_γCombat Chest_11 A6 Chest_12 Anchor_S2_End_γ");
            Debug.Log("[ThreeRouteScaffold] HOOKS Pre: PreBoss Shop_01 Chest_03 Anchor_S3_End BOSS");
            Debug.Log("[JianHaiArt] PPU=" + JianHaiArtCatalog.Ppu + " filter=" + JianHaiArtCatalog.Filter
                      + " Chest_*→" + JianHaiArtCatalog.ChestRoot + "_* A_*→" + JianHaiArtCatalog.AltarRoot
                      + "_* Shop_01→" + JianHaiArtCatalog.ShopRoot
                      + " entityScale=" + JianHaiArtCatalog.EntityStubWorldScale
                      + " propScale=" + JianHaiArtCatalog.PropStubWorldScale
                      + " orthoLock=" + orthographicSize);
            BindBossDoor();
        }

        void BindBossDoor()
        {
            if (!_markers.TryGetValue("BOSS", out GameObject bossGo))
                return;
            _boss = bossGo.GetComponent<BossFightDriver>();
            if (_boss == null || !LockSiteCatalog.TryGet("BOSS", out SiteDef site))
                return;
            float r = site.NoSpawnRadius > 0.01f ? site.NoSpawnRadius : 4f;
            var doorPos = new Vector3(site.Position.x, site.Position.y - r, 0f);
            GameObject door = DemoPrimitives.Quad(
                "BossDoor", doorPos, new Vector2(3.2f, 0.28f),
                new Color(0.72f, 0.16f, 0.16f), 6, transform);
            door.SetActive(false);
            _boss.BindDoor(door.transform);
        }

        void TryBossEnter()
        {
            if (_boss == null || _player == null)
                return;
            if (_boss.FightStarted)
            {
                if (!_boss.FightSettled && _clock != null)
                    _boss.SetWallMinutes(_clock.WallMinutes);
                return;
            }

            if (!LockSiteCatalog.TryGet("BOSS", out SiteDef site))
                return;
            float r = site.NoSpawnRadius > 0.01f ? site.NoSpawnRadius : 4f;
            var p = new Vector2(_player.position.x, _player.position.y);
            if ((p - site.Position).sqrMagnitude > r * r)
                return;
            int build = _buildDir != null && _buildDir.Build != null ? _buildDir.Build.BuildCount : 0;
            float mins = _clock != null ? _clock.WallMinutes : 0f;
            _boss.BeginEnter(build, mins);
        }

        void OnStubKilled(StubEnemy enemy)
        {
            if (_buildDir != null)
                _buildDir.OnEnemyKilled(enemy);
            if (enemy != null)
                Destroy(enemy.gameObject);
        }

        static GameObject SpawnSiteMarker(SiteDef site, Vector3 pos, Transform parent)
        {
            string root = JianHaiArtCatalog.ArtRootForHook(site.Id);
            if (site.Kind == SiteKind.Chest || site.Kind == SiteKind.Altar || site.Kind == SiteKind.Shop
                || site.Kind == SiteKind.Boss)
            {
                string artId = JianHaiArtCatalog.SpriteName(root, JianHaiArtCatalog.DefaultState(site.Kind));
                if (site.Kind == SiteKind.Boss)
                    artId = JianHaiArtCatalog.BossIdle;
                GameObject go = JianHaiBind.Spawn(site.Id, pos, artId, parent);
                if (site.Kind == SiteKind.Boss && go.GetComponent<BossFightDriver>() == null)
                    go.AddComponent<BossFightDriver>();
                if (!string.IsNullOrEmpty(root) && site.Kind != SiteKind.Boss)
                    JianHaiSpriteSlot.Add(go, site);
                return go;
            }

            float size = site.Kind == SiteKind.Switch ? 0.42f : 0.7f;
            GameObject marker = DemoPrimitives.Quad(
                site.Id, pos, new Vector2(size, size),
                LockSiteCatalog.ColorFor(site.Kind), 4, parent);
            string layer = site.Kind == SiteKind.DeadEnd || site.Kind == SiteKind.Switch
                ? JianHaiArtCatalog.LayerDecal
                : JianHaiArtCatalog.LayerProp;
            JianHaiBind.SetLayer(marker, layer, 4);
            return marker;
        }

        static Vector3 LabelOffset(SiteDef site)
        {
            if (site.Id == "Chest_02" || site.Id == "Chest_06" || site.Id == "Chest_03")
                return new Vector3(0.9f, -1.15f, 0f);
            if (site.Kind == SiteKind.Chest || site.Kind == SiteKind.Altar || site.Kind == SiteKind.Shop
                || site.Kind == SiteKind.Boss)
                return new Vector3(0f, 1.25f, 0f);
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
            BuiltinUiFont.Apply(tm);
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
            Vector3 park = new Vector3(5f, 7.5f, 0f);
            if (_player != null)
                _player.position = park;
            Camera cam = Camera.main;
            if (cam != null)
                cam.transform.position = new Vector3(park.x, park.y, cam.transform.position.z);
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
            bool viewOk = _view != null
                && Mathf.Abs(orthographicSize - 2.5f) < 0.01f
                && Mathf.Abs(_view.OrthographicSize - 2.5f) < 0.01f;

            bool inViewSkip = _demoAnchors != null
                && SpawnViewGate.ShouldSkipSpawn(_demoAnchors[0].WorldPosition)
                && !_director.WasSpawned("Demo_InView");
            bool coreSkip = _demoAnchors != null
                && SpawnCoreGate.IsInsideNoSpawnCore(_demoAnchors[1].WorldPosition)
                && !_director.WasSpawned("Demo_NearCore_Chest_01");
            bool offViewSpawn = _demoAnchors != null
                && !SpawnViewGate.ShouldSkipSpawn(_demoAnchors[2].WorldPosition)
                && _director.WasSpawned("Demo_Corridor_Z1");
            bool edgeSkip = _demoAnchors != null
                && _demoAnchors.Length >= 4
                && !SpawnViewGate.IsInMainCameraView(_demoAnchors[3].WorldPosition)
                && SpawnViewGate.ShouldSkipSpawn(_demoAnchors[3].WorldPosition)
                && !_director.WasSpawned("Demo_EdgeBuffer");
            bool capOk = SpawnScreenCap.MaxLive == 12;

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

            string buildErr = BuildSliceChecks.Run(_lock);
            string aiErr = MobAiChecks.Run(_lock);
            bool buildOk = buildErr == null;
            bool aiOk = aiErr == null;
            bool altarsOn = _buildDir != null && _buildDir.AltarCount >= 3;
            bool powerOk = _lock != null
                && Mathf.Abs(_lock.powerBuildCoef - 0.45f) < 0.001f
                && Mathf.Abs(_lock.powerRarityCoef - 0.55f) < 0.001f;

            string artErr = JianHaiArtChecks.Run();
            bool artOk = artErr == null;
            string deErr = DeadEndChecks.Run();
            bool deOk = deErr == null;
            _pass = idsOk && configOk && csvOk && viewOk && inViewSkip && coreSkip && offViewSpawn
                    && edgeSkip && capOk
                    && z1Eff > 4.5f && z1Eff < 4.9f && buildOk && aiOk && altarsOn && powerOk && artOk && deOk;
            var sb = new StringBuilder();
            sb.Append(_pass ? "ACCEPTANCE PASS" : "ACCEPTANCE FAIL");
            sb.Append($" idsOk={idsOk} csvOk={csvOk} view={viewOk} inViewSkip={inViewSkip} edgeSkip={edgeSkip} coreSkip={coreSkip} offViewSpawn={offViewSpawn} capOk={capOk} pad={SpawnViewGate.EffectivePad:0.00} z1Eff={z1Eff:0.00}");
            sb.Append($" buildOk={buildOk} aiOk={aiOk} altarsOn={altarsOn} powerOk={powerOk} artOk={artOk} deOk={deOk}");
            if (!idsOk)
                sb.Append(" missing=" + string.Join(",", missing.ToArray()));
            if (!buildOk)
                sb.Append(" buildErr=" + buildErr);
            if (!aiOk)
                sb.Append(" aiErr=" + aiErr);
            if (!artOk)
                sb.Append(" artErr=" + artErr);
            if (!deOk)
                sb.Append(" deErr=" + deErr);
            _status = sb.ToString();
            if (_pass)
                Debug.Log("[ThreeRouteScaffold] " + _status);
            else
                Debug.LogError("[ThreeRouteScaffold] " + _status);
        }

        void TeleportTo(string id)
        {
            if (_player == null || !LockSiteCatalog.TryGet(id, out SiteDef site))
                return;
            _player.position = new Vector3(site.Position.x, site.Position.y, 0f);
            Debug.Log("[Teleport] " + id + " " + site.Position);
        }

        void TeleportToNearestMob()
        {
            if (_player == null)
                return;
            MobFourStateAi best = null;
            float bestD = float.MaxValue;
            IReadOnlyList<MobFourStateAi> all = MobFourStateAi.All;
            for (int i = 0; i < all.Count; i++)
            {
                MobFourStateAi m = all[i];
                if (m == null)
                    continue;
                float d = Vector2.Distance(_player.position, m.transform.position);
                if (d < bestD)
                {
                    bestD = d;
                    best = m;
                }
            }

            if (best == null)
            {
                Debug.Log("[Teleport] no stub mob yet — wait for SpawnBand or walk corridor");
                return;
            }

            Vector3 p = best.transform.position;
            _player.position = p + new Vector3(0.8f, 0f, 0f);
            Debug.Log("[Teleport] near " + best.name + " state=" + best.State);
        }

        void OnGUI()
        {
            const int pad = 8;
            int w = 580;
            int h = 380;
            GUI.Box(new Rect(pad, pad, w, h), "");
            var style = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            var title = new GUIStyle(style) { fontSize = 15, fontStyle = FontStyle.Bold };
            var rich = new GUIStyle(style) { richText = true };

            GUI.Label(new Rect(pad + 8, pad + 4, w - 16, 20), "三路脚手架 · Build + 四态AI  v0.4.2-LOCK", title);

            string band = _clock != null ? _clock.BandId : "?";
            float mins = _clock != null ? _clock.WallMinutes : 0f;
            float interval = _director != null ? _director.CurrentInterval() : 0f;
            SegmentMul seg = _lock != null ? _lock.GetSegment(band) : null;
            string pressurePhase = TimePressure.PhaseId(mins);
            string pressureLabel = TimePressure.PhaseLabel(mins);
            float attr = TimePressure.AttrMul(mins);
            float intMul = TimePressure.IntervalMul(mins);
            WaveSpec wave = _lock != null ? _lock.GetWave(band) : null;
            string hp = seg != null ? seg.hpMul.ToString("0.000") : "-";
            string dmg = seg != null ? seg.dmgMul.ToString("0.000") : "-";
            int waveN = wave != null ? wave.count : 0;

            GUI.Label(new Rect(pad + 8, pad + 24, w - 16, 22),
                $"时间段 HUD：{pressureLabel} ({pressurePhase})  wall={mins:0.00}′  M_attr={attr:0.00}  M_int={intMul:0.00}  spawnEff={interval:0.00}s",
                title);
            string classHud = _director != null
                ? SpawnWaveCatalog.ClassLabel(_director.LastClass) + " " + (_director.LastGroupLine ?? "")
                : "";
            if (!string.IsNullOrEmpty(classHud))
            {
                GUI.Label(new Rect(pad + 8, pad + 46, w - 16, 18),
                    "刷怪类 HUD：" + classHud, style);
            }

            GUI.Label(new Rect(pad + 8, pad + 48, w - 16, 54),
                $"WASD · E interact · F strike · N new run · F1 Chest_01 · F2 A_Shared · F3 Shop · F4 mob\n" +
                $"F5 DE01 ChestReveal · F6 DE02 MobWave · F7 DE03 EmptySoft · F8 DE04 StaticRoom\n" +
                $"1/2/3 band · 4=Pre · Space pause · +/- clock · R reset · hold LMB/C 蓄力射(≥0.15s)  " +
                $"band={band} ×{(_clock != null ? _clock.ClockScale : 0f):0} {(_clock != null && _clock.Paused ? "PAUSED" : "")}  " +
                $"waves {(_clock != null ? _clock.WaveIndex : 0)}/{waveN}  HP×{hp} DMG×{dmg}",
                style);

            if (_lock != null)
            {
                GUI.Label(new Rect(pad + 8, pad + 80, w - 16, 36),
                    $"P_spawn={_lock.pSpawn:0.00}  chest C/R/E=" +
                    $"{_lock.RarityWeight(_lock.chestRarity, "C"):0.00}/{_lock.RarityWeight(_lock.chestRarity, "R"):0.00}/{_lock.RarityWeight(_lock.chestRarity, "E"):0.00}" +
                    $"  altar={_lock.RarityWeight(_lock.altarRarity, "C"):0.00}/{_lock.RarityWeight(_lock.altarRarity, "R"):0.00}/{_lock.RarityWeight(_lock.altarRarity, "E"):0.00}\n" +
                    $"Power {_lock.powerBuildCoef:0.00}*B+{_lock.powerRarityCoef:0.00}*RS  detect={_lock.mobDetectRadius:0.0} disengage×{_lock.mobDisengageMul:0.0}  shop price={_lock.shopStubPrice} gold0={_lock.shopStartGold}",
                    style);
            }

            if (_buildDir != null && _buildDir.Build != null)
            {
                GUI.Label(new Rect(pad + 8, pad + 118, w - 16, 50),
                    _buildDir.SummaryLine() + "\n" +
                    $"chests {_buildDir.ChestPresentCount}/{_buildDir.ChestSlotCount} seed={_buildDir.Seed} empty={_buildDir.EmptyLine()}\n" +
                    $"altars {_buildDir.AltarCount}/{_buildDir.AltarCount} always present  " +
                    (_buildDir.Nearest != null ? _buildDir.Nearest.Prompt() : "walk to Chest/Altar/Shop then E"),
                    style);
            }

            string flash = _buildDir != null ? _buildDir.FlashMessage() : "";
            string status = _pass
                ? "<color=#88ff88>" + _status + "</color>"
                : "<color=#ffcc88>" + _status + "</color>";
            GUI.Label(new Rect(pad + 8, pad + 170, w - 16, 40), status, rich);
            if (_deadEnd != null)
                GUI.Label(new Rect(pad + 8, pad + 206, w - 16, 16), _deadEnd.HudLine, style);
            if (!string.IsNullOrEmpty(flash))
                GUI.Label(new Rect(pad + 8, pad + 222, w - 16, 18), flash, style);

            if (_boss != null && _boss.Brain != null && _boss.FightStarted)
            {
                var brain = _boss.Brain;
                GUI.Label(new Rect(pad + 8, pad + 226, w - 16, 18),
                    "BOSS " + brain.Phase
                    + " door=" + (brain.DoorClosed ? "CLOSED" : "OPEN")
                    + " lockHP=" + (brain.HpLocked ? "YES" : "NO")
                    + " " + brain.Hp.ToString("0") + "/" + brain.MaxHp.ToString("0"),
                    style);
            }

            DrawMobStates(pad + 8, pad + 246, w - 16, style);

            DrawIdPanel();
            DrawViewBorder();
            if (_buildDir != null)
                _buildDir.DrawOfferGui();
        }

        static void DrawMobStates(int x, int y, int w, GUIStyle style)
        {
            IReadOnlyList<MobFourStateAi> all = MobFourStateAi.All;
            int shown = 0;
            for (int i = 0; i < all.Count && shown < 5; i++)
            {
                MobFourStateAi m = all[i];
                if (m == null)
                    continue;
                GUI.Label(new Rect(x, y, w, 16),
                    m.DisplayName + "  " + m.State + "  dist=" + m.DistToPlayer.ToString("0.0"),
                    style);
                y += 16;
                shown++;
            }

            if (shown == 0)
                GUI.Label(new Rect(x, y, w, 16), "mobs: (none yet — SpawnBand stubs get Patrol/Alert/Chase/Disengage)", style);
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
                "+ SpawnViewGate spawn-in-view\n" +
                "Chests P_spawn=0.90 · Altars 100%\n" +
                "Shop never increments B/RS\n" +
                "DeadEnd once: DE01 Reveal / DE02 Wave\n" +
                "  DE03 EmptySoft 0–0 / DE04 Static\n" +
                "AI: Patrol→Alert→Chase→Disengage\n" +
                "Art PPU32 Point: Chest_*→jh_prop_chest_*",
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
