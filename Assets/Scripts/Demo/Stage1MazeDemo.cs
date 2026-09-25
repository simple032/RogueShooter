using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using RogueShooter.Ai;
using RogueShooter.Art;
using RogueShooter.Balance;
using RogueShooter.Maze;
using RogueShooter.Player;
using RogueShooter.Spawning;
using RogueShooter.Vision;
using RogueShooter.Build;
using RogueShooter.Combat;
using RogueShooter.Layout;

namespace RogueShooter.Demo
{
    /// <summary>
    /// Playable Stage-1 maze skeleton (Spec v0.5). Seeded rooms + lock/clear/open.
    /// Floors/walls/doors bind Provide-sourced JianHai PNGs under Assets/Art/JianHai/.
    /// Connector is a stub — S2/S3 mazes are not built.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class Stage1MazeDemo : MonoBehaviour
    {
        [SerializeField] float orthographicSize = CameraViewService.OrthoPlaySize;
        [Tooltip("Play default ~6. 0 = MoveSpeeds.Player (L=22 lock)")]
        [SerializeField] float moveSpeed = MazeRules.PlayMoveSpeed;
        [SerializeField] int seed = 42;

        Stage1Maze _maze;
        MazePacing _pace;
        Transform _player;
        CameraViewService _view;
        BalanceLockData _lock;
        GameObject _stubPrefab;
        readonly Dictionary<string, CombatRoomSession> _sessions = new Dictionary<string, CombatRoomSession>();
        readonly Dictionary<string, GameObject> _roomFloors = new Dictionary<string, GameObject>();
        readonly List<GameObject> _doors = new List<GameObject>();
        readonly List<GameObject> _live = new List<GameObject>();
        readonly List<GameObject> _portals = new List<GameObject>();
        readonly List<GameObject> _world = new List<GameObject>();
        readonly List<PendingSpawn> _pending = new List<PendingSpawn>();

        /// <summary>L5 fallback unit waiting for its spawn warning.</summary>
        sealed class PendingSpawn
        {
            public Coroutine Co;
            public SpawnWarnFx Fx;
            public Action Spawn;
            public bool Done;
        }
        readonly Dictionary<string, GameObject> _covers = new Dictionary<string, GameObject>();
        readonly HashSet<string> _revealed = new HashSet<string>();
        static Sprite _coverSprite;
        CombatRoomSession _active;
        Vector3 _lastGood;
        bool _pass;
        bool _portalWaiting;
        bool _skipPortalWait;
        float _portalHold;
        Coroutine _cadence;
        string _status = "loading…";
        string _flash = "";
        float _flashUntil;
        RunBuildState _build;
        Stage1PaintedPlay _painted;
        Rigidbody2D _playerBody;

        /// <summary>True when the scene's painted tilemap drives geometry (Stage1PaintedPlay + Grid).</summary>
        bool Painted => _painted != null && _painted.HasPaintedGrid;
        ChestAltarDirector _buildDir;
        readonly List<SiteRuntime> _sites = new List<SiteRuntime>();

        IEnumerator Start()
        {
            BalanceLock.TryLoadFromStreamingAssets(out _lock, out _);
            LoadDraftPool();
            BuildRun(seed);
            yield return null;
            RunAcceptance();
        }

        void Update()
        {
            HandleHotkeys();
            ContainPlayer();
            RevealRoomAtPlayer();
            LiftPlayerInDoorway();
            TryEnterRoom();
            SweepDead();
        }

        void BuildRun(int newSeed)
        {
            ClearWorld();
            seed = newSeed;
            if (_painted == null)
                _painted = GetComponent<Stage1PaintedPlay>();
            _maze = Painted ? _painted.BuildMaze(seed) : Stage1MazeGen.Generate(seed);
            if (Painted)
                Debug.Log("[S1Maze] painted layout " + _maze.Signature + " (tilemap geometry, Demo runtime)");
            _pace = Stage1MazeGen.MeasurePacing(_maze, PlaySpeed());
            Debug.Log("[S1Maze] " + Stage1MazeGen.FormatGraph(_maze));
            Debug.Log("[S1Maze] " + Stage1MazeGen.FormatQuota(_maze));
            Debug.Log("[S1Maze] pacing shortestWalk=" + _pace.ShortestWalk.ToString("0.0")
                      + "u/" + _pace.ShortestWalkSeconds.ToString("0.0") + "s"
                      + " rooms=" + _pace.ShortestCombatRooms
                      + " waves=" + _pace.ShortestWaves
                      + " combatEst=" + _pace.ShortestCombatEstimate.ToString("0") + "s"
                      + " totalEst=" + _pace.ShortestTotalEstimate.ToString("0") + "s"
                      + " | fullWalk=" + _pace.FullWalk.ToString("0.0")
                      + "u/" + _pace.FullWalkSeconds.ToString("0.0") + "s"
                      + " waves=" + _pace.FullWaves
                      + " totalEst=" + _pace.FullTotalEstimate.ToString("0")
                      + "s (informational walk; no clock gate)");
            Debug.Log("[S1Maze] walkOnly move=" + MazeRules.PlayMoveSpeed.ToString("0")
                      + " firstHop=" + _pace.FirstHop.ToString("0.0") + "u/"
                      + _pace.FirstHopSeconds.ToString("0.0") + "s (feel ~3s, not a lock)"
                      + " shortest=" + _pace.ShortestWalk.ToString("0.0") + "u/"
                      + _pace.ShortestWalkSeconds.ToString("0.0") + "s START→CONN"
                      + " maxSeg=" + _pace.MaxCorridorSeg.ToString("0.0") + "u/"
                      + _pace.MaxCorridorSegSeconds.ToString("0.00") + "s"
                      + " pitch=" + MazeRules.PitchX.ToString("0") + "/"
                      + MazeRules.PitchY.ToString("0"));
            BuildWorld();
            LogDryRun();
        }

        void LoadDraftPool()
        {
            string dir = EnemyPoolDraft.ResolveDirectory();
            if (!string.IsNullOrEmpty(Application.streamingAssetsPath))
            {
                string sa = Path.Combine(Application.streamingAssetsPath, EnemyPoolDraft.FolderName);
                if (Directory.Exists(sa))
                    dir = sa;
            }

            if (!EnemyPoolDraft.TryLoadFromDirectory(dir, out string err))
                Debug.LogError("[S1Maze] draft load FAIL " + err);
            else
                Debug.Log("[StagePool] loaded DRAFT from " + EnemyPoolDraft.Source);
            FullChargeKnockback.TryLoadFromDirectory(dir);
            KnockbackRewardDraft.TryLoadFromDirectory(dir);
            Debug.Log("[Knockback] " + FullChargeKnockback.Source
                      + " full≥" + ChargeShotRules.RingFillSeconds.ToString("0.00")
                      + "s dog=" + FullChargeKnockback.MidDistance(EnemyKindIds.Dog, false).ToString("0.00")
                      + " mage=" + FullChargeKnockback.MidDistance(EnemyKindIds.CultMage, false).ToString("0.00")
                      + " normal=" + FullChargeKnockback.MidDistance(EnemyKindIds.Normal, false).ToString("0.00")
                      + " grand=" + FullChargeKnockback.MidDistance(EnemyKindIds.GrandMage, false).ToString("0.00")
                      + " shield=" + FullChargeKnockback.MidDistance(EnemyKindIds.Shield, false).ToString("0.00")
                      + "/root" + FullChargeKnockback.ShieldRaisedRootSeconds.ToString("0.0") + "s"
                      + " ws×" + FullChargeKnockback.WeakSpotMul.ToString("0.0")
                      + " boss=" + FullChargeKnockback.MidDistance(null, false, true).ToString("0.00")
                      + "/" + FullChargeKnockback.HitDistance(null, false, true, true).ToString("0.00")
                      + " return=" + FullChargeKnockback.ReturnSeconds.ToString("0.0") + "s"
                      + " " + FullChargeKnockback.FormulaNote
                      + " 震矢=" + KnockbackRewardDraft.Source);
        }

        void BuildWorld()
        {
            Transform root = transform;
            bool painted = Painted;
            if (painted)
            {
                int wallCells = _painted.BuildWallFootprints();
                Debug.Log("[S1Maze] wall footprints cells=" + wallCells
                          + " (1x1 per wall cell; overhang art not solid) playerR=" + Stage1PaintedPlay.PlayerRadius
                          + " footOffset=" + Stage1PaintedPlay.PlayerFootOffset);
            }
            for (int i = 0; !painted && i < _maze.Edges.Length; i++)
            {
                MazeEdge e = _maze.Edges[i];
                MazeVec2[] pts = e.Points;
                if (pts == null || pts.Length < 2)
                {
                    pts = new[] { e.From, e.To };
                }

                for (int s = 0; s < pts.Length - 1; s++)
                {
                    GameObject cor = JianHaiBind.SpawnCorridorTiled(
                        "Corridor_" + e.FromId + "_" + e.ToId + "_" + s,
                        new Vector3(pts[s].X, pts[s].Y, 0f),
                        new Vector3(pts[s + 1].X, pts[s + 1].Y, 0f),
                        e.Width, JianHaiArtCatalog.TileFloorCorridor, root, 1);
                    _world.Add(cor);
                }
            }

            for (int i = 0; i < _maze.Nodes.Length; i++)
            {
                MazeNode n = _maze.Nodes[i];
                if (!painted)
                {
                    GameObject go = JianHaiBind.SpawnTiled(
                        "Room_" + n.Id,
                        new Vector3(n.Center.X, n.Center.Y, 1.1f),
                        new Vector2(n.Width, n.Height),
                        FloorArt(n.Kind), root, 0, Quaternion.identity, FloorTint(n.Kind));
                    _roomFloors[n.Id] = go;
                    _world.Add(go);
                }
                AddWorldLabel(root, n.Id + " " + MazeRules.Label(n.Kind),
                    new Vector3(n.Center.X, n.Center.Y + n.Height * 0.42f, 0f));
                AddProp(n, root);
                _sessions[n.Id] = new CombatRoomSession(n);
            }

            SpawnCollisionSolids(root);
            BuildRoomCovers(root);

            MazeNode start = _maze.Find("START");
            Vector3 startPos = start != null
                ? new Vector3(start.Center.X, start.Center.Y, 0f)
                : Vector3.zero;

            GameObject player = new GameObject("Player");
            player.transform.position = startPos;
            JianHaiBind.ApplyTo(player, JianHaiArtCatalog.PlayerIdle);
            JianHaiBind.SetLayer(player, JianHaiArtCatalog.LayerEntity, 20);
            if (painted)
            {
                // Single player: physics lives on the Demo-spawned player (before the motor caches it).
                Stage1PaintedPlay.AttachPhysics(player);
                _playerBody = player.GetComponent<Rigidbody2D>();
            }
            else
            {
                _playerBody = null;
            }
            player.AddComponent<PlayerMotor2D>().Configure(PlaySpeed());
            player.AddComponent<PlayerVitals>().Configure(EnemyDamageCatalog.PlayerMaxHpRef);
            player.AddComponent<PlayerStrike>().Configure(_lock != null ? _lock.strikeRange : 1.85f);
            player.AddComponent<PlayerCharge>();
            player.AddComponent<PlayerDodge>();
            player.AddComponent<GuaranteedCritActive>();
            CollisionVolume.Add(player, CollisionLayer.Player, false, CollisionRules.PlayerHalfX, CollisionRules.PlayerHalfY);
            EntityAnimView.Add(player, true);
            BindDirector(player);
            _player = player.transform;
            _lastGood = startPos;
            _world.Add(player);
            Debug.Log("[MoveSpeed] player=" + PlaySpeed().ToString("0.000")
                      + " ortho=" + CameraViewService.PlayOrthoSize.ToString("0.##")
                      + " seed=" + seed);

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

            _stubPrefab = new GameObject("StubEnemyPrefab");
            _stubPrefab.transform.SetParent(root, false);
            _stubPrefab.SetActive(false);
            JianHaiBind.ApplyTo(_stubPrefab, JianHaiArtCatalog.EnemyE1Idle);
            _stubPrefab.AddComponent<StubEnemy>();
            CollisionVolume.Add(_stubPrefab, CollisionLayer.Mob, false, CollisionRules.MobHalfX, CollisionRules.MobHalfY);
            EntityAnimView.Add(_stubPrefab, false);
            _world.Add(_stubPrefab);
        }

        void SpawnCollisionSolids(Transform root)
        {
            bool painted = Painted;
            List<MazeSolid> solids = painted
                ? MazeCollisionBuilder.Build(_maze, Stage1PaintedPlay.CorridorWidth)
                : MazeCollisionBuilder.Build(_maze);
            for (int i = 0; i < solids.Count; i++)
            {
                MazeSolid s = solids[i];
                GameObject go;
                if (painted && !s.Door)
                {
                    // Painted walls: tilemap visual + Stage1PaintedPlay wall footprints for the rigidbody
                    // player. Keep an invisible AABB so arrows / mobs (CollisionWorld) stop too.
                    go = new GameObject(s.Name);
                    go.transform.SetParent(root, false);
                    go.transform.position = new Vector3(s.X, s.Y, 1f);
                }
                else
                {
                    string art = s.Door ? JianHaiArtCatalog.PropGateHub : JianHaiArtCatalog.WallStone;
                    Color tint = s.Door ? new Color(1f, 0.62f, 0.58f, 1f) : Color.white;
                    int order = s.Door ? 6 : 2;
                    go = JianHaiBind.SpawnTiled(
                        s.Name,
                        new Vector3(s.X, s.Y, s.Door ? 0f : 1f),
                        new Vector2(s.Width, s.Height),
                        art, root, order, Quaternion.identity, tint);
                    if (painted)
                        JianHaiBind.SetLayer(go, JianHaiArtCatalog.LayerProp, order);
                }

                CollisionVolume.Add(go, s.Layer, !s.Door, s.Width * 0.5f, s.Height * 0.5f);
                if (s.Door)
                {
                    if (painted)
                    {
                        // Rigidbody player: a locked door must block physics as well.
                        var box = go.AddComponent<BoxCollider2D>();
                        box.size = new Vector2(s.Width, s.Height);
                    }
                    go.SetActive(false);
                    _doors.Add(go);
                }

                _world.Add(go);
            }
        }

        void BindDirector(GameObject player)
        {
            EnsureBuild();
            if (_buildDir == null)
                _buildDir = gameObject.AddComponent<ChestAltarDirector>();
            _buildDir.AllowReseedHotkey = false;
            _buildDir.CanUseSites = SiteUnlocked;
            _buildDir.Bind(_lock, player.transform, _sites, seed);
            _build = _buildDir.Build;
            var charge = player.GetComponent<PlayerCharge>();
            if (charge != null && _build != null)
                charge.BindOwnedRewards(_build.OwnedRewardIds);
        }

        bool SiteUnlocked(Vector3 pos)
        {
            MazeNode n = RoomAt(pos.x, pos.y, 0.2f);
            if (n == null || !n.SpawnsEnemies)
                return true;
            CombatRoomSession s;
            if (!_sessions.TryGetValue(n.Id, out s) || s == null)
                return false;
            return s.Phase == CombatRoomPhase.Cleared;
        }

        void AddProp(MazeNode n, Transform root)
        {
            if (n.Kind != MazeNodeKind.Chest && n.Kind != MazeNodeKind.LargeChest
                && n.Kind != MazeNodeKind.Altar && n.Kind != MazeNodeKind.Connector)
                return;
            string hook;
            SiteKind siteKind = SiteKind.Chest;
            if (n.Kind == MazeNodeKind.Altar)
            {
                hook = "A_Shared";
                siteKind = SiteKind.Altar;
            }
            else if (n.Kind == MazeNodeKind.Connector)
            {
                hook = "CONN_STUB";
            }
            else if (n.Id == "CHEST2")
            {
                hook = "Chest_02";
            }
            else
            {
                hook = "Chest_01";
            }
            Vector3 pos = new Vector3(n.Center.X, n.Center.Y + 0.4f, 0f);
            GameObject go;
            if (n.Kind == MazeNodeKind.Connector)
            {
                go = DemoPrimitives.Quad(hook, pos, new Vector2(1.1f, 1.1f),
                    new Color(0.25f, 0.72f, 0.62f), 5, root);
            }
            else
            {
                string rootArt = n.Kind == MazeNodeKind.Altar
                    ? JianHaiArtCatalog.AltarRoot
                    : JianHaiArtCatalog.ChestArtRoot(n.Kind == MazeNodeKind.LargeChest);
                string state = n.Kind == MazeNodeKind.Altar ? "idle" : "closed";
                go = JianHaiBind.Spawn(hook, pos, JianHaiArtCatalog.SpriteName(rootArt, state), root);
            }

            _world.Add(go);
            if (n.Kind == MazeNodeKind.Chest || n.Kind == MazeNodeKind.Altar)
            {
                var def = new SiteDef(hook, new Vector2(pos.x, pos.y), siteKind, RouteId.Shared, n.Id);
                var runtime = go.GetComponent<SiteRuntime>();
                if (runtime == null)
                    runtime = go.AddComponent<SiteRuntime>();
                runtime.Configure(def, n.Kind != MazeNodeKind.Chest);
                _sites.Add(runtime);
                float clear = CombatRoomSpawn.ClearanceForKind(siteKind);
                go.AddComponent<NoSpawnCore>().Configure(hook, siteKind.ToString(), clear);
                var box = go.GetComponent<BoxCollider2D>();
                if (box == null)
                    box = go.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                float sx = Mathf.Abs(go.transform.localScale.x);
                if (sx < 0.001f)
                    sx = 1f;
                box.size = new Vector2(clear * 2f / sx, clear * 2f / sx);
            }
        }

        void LogDryRun()
        {
            string text = Stage1MazeSampler.RunText(seed);
            using (var reader = new StringReader(text))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Length == 0)
                        continue;
                    if (line.StartsWith("# ACCEPTANCE FAIL", StringComparison.Ordinal))
                        Debug.LogError(line);
                    else
                        Debug.Log(line);
                }
            }
        }

        void HandleHotkeys()
        {
            bool offering = _buildDir != null && _buildDir.Offering;
            if (Input.GetKeyDown(KeyCode.N))
            {
                BuildRun(Environment.TickCount);
                RunAcceptance();
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                BuildRun(seed);
                RunAcceptance();
            }

            if (Input.GetKeyDown(KeyCode.F1))
                Teleport("START");
            if (Input.GetKeyDown(KeyCode.F2))
                Teleport("CONN");
            if (Input.GetKeyDown(KeyCode.F3))
                TeleportFirst(MazeNodeKind.Altar);
            if (Input.GetKeyDown(KeyCode.F4))
                TeleportFirstChest();
            if (!offering && (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)))
                Teleport("N1");
            if (!offering && (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)))
                Teleport("N2");
            if (!offering && (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)))
                TeleportFirst(MazeNodeKind.Altar);
            if (!offering && (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)))
                TeleportFirstChest();
            if (Input.GetKeyDown(KeyCode.K))
                KillLiveWave();
            if (Input.GetKeyDown(KeyCode.F9))
                WriteEvidence();
            if (Input.GetKeyDown(KeyCode.E))
            {
                if (_buildDir != null && (_buildDir.Offering || _buildDir.Nearest != null))
                    return;
                TryInteract();
            }
            if (Input.GetKeyDown(KeyCode.F6))
                GrantZhenShi(KnockbackRewardDraft.IdLow);
            if (Input.GetKeyDown(KeyCode.F7))
                GrantZhenShi(KnockbackRewardDraft.IdMid);
            if (Input.GetKeyDown(KeyCode.F8))
                ClearZhenShi();
        }

        void EnsureBuild()
        {
            if (_build == null)
                _build = new RunBuildState(0.45f, 0.55f, 0);
        }

        void GrantZhenShi(string id)
        {
            EnsureBuild();
            RewardRow row;
            if (!RewardCatalog.TryGet(id, out row))
            {
                Flash("missing " + id);
                return;
            }

            string rarity = row.Tier == RewardTier.Mid ? "R" : "C";
            _build.GrantBuildPick(id, rarity, 0, row.BuildEquiv);
            float p = KnockbackRewardDraft.DistPctProduct(_build.OwnedRewardIds);
            float dog = FullChargeKnockback.HitDistance(EnemyKindIds.Dog, false, false, false, p);
            float dogWs = FullChargeKnockback.HitDistance(EnemyKindIds.Dog, false, false, true, p);
            float raised = FullChargeKnockback.HitDistance(EnemyKindIds.Shield, true, false, false, p);
            Debug.Log("[Knockback] 震矢 grant " + id
                      + " +" + (row.Value * 100f).ToString("0") + "%"
                      + " product=" + p.ToString("0.00")
                      + " dogFull=" + dog.ToString("0.00")
                      + " dogWs=" + dogWs.ToString("0.00")
                      + " shieldRaisedBody=" + raised.ToString("0.00")
                      + " B=" + _build.BuildCount
                      + " " + KnockbackRewardDraft.LockNote);
            Flash("震矢 " + id + " ×" + p.ToString("0.00") + " B=" + _build.BuildCount);
        }

        void ClearZhenShi()
        {
            EnsureBuild();
            _build.Reset(0);
            Debug.Log("[Knockback] 震矢 clear product=1.00 " + KnockbackRewardDraft.LockNote);
            Flash("震矢 clear");
        }

        void TryEnterRoom()
        {
            if (_player == null || _maze == null)
                return;
            if (_active != null && _active.DoorsLocked)
                return;
            if (_portalWaiting)
                return;
            MazeNode inside = RoomAt(_player.position.x, _player.position.y, LockTriggerInset);
            if (inside == null || !inside.SpawnsEnemies)
                return;
            CombatRoomSession session;
            if (!_sessions.TryGetValue(inside.Id, out session))
                return;
            if (session.Phase != CombatRoomPhase.Vacant)
                return;
            ApplySteps(session, session.Enter());
        }

        void ApplySteps(CombatRoomSession session, CombatStep[] steps)
        {
            if (steps == null || steps.Length == 0)
                return;
            _active = session;
            int cadenceWave = 0;
            for (int i = 0; i < steps.Length; i++)
            {
                CombatStep step = steps[i];
                if (step.Portal)
                {
                    cadenceWave = step.Wave;
                    continue;
                }

                if (step.ShouldSpawn)
                {
                    int wave = step.Wave > 0 ? step.Wave : cadenceWave;
                    _cadence = StartCoroutine(PortalThenSpawn(session, wave));
                    continue;
                }

                Debug.Log(step.Line);
                if (step.ShouldOpen)
                {
                    SetDoors(session.RoomId, false);
                    Flash("open " + session.RoomId);
                    if (_active == session)
                        _active = null;
                    StartCoroutine(AutoOpenAfterClear(session.RoomId));
                }
            }

            if (session.DoorsLocked)
                SetDoors(session.RoomId, true);
        }

        IEnumerator PortalThenSpawn(CombatRoomSession session, int wave)
        {
            MazeNode node = _maze != null ? _maze.Find(session.RoomId) : null;
            if (node == null || _stubPrefab == null)
                yield break;

            _portalWaiting = true;
            _skipPortalWait = false;
            DrawnComposition drawn = StageEnemyPool.DrawComposition(
                StageId.S1, node.PoolRoom, Stage1MazeGen.DrawRng(seed, node.Id, wave));
            int n = drawn.Units != null ? drawn.Units.Length : 0;
            if (n < 1)
                n = 1;
            Vector3[] spots = PlaceWaveSpots(node, drawn, wave);
            ClearPortals();
            for (int i = 0; i < n; i++)
            {
                Vector3 pos = spots != null && i < spots.Length ? spots[i] : new Vector3(node.Center.X, node.Center.Y, 0f);
                GameObject fx = PortalFxStub.SpawnAt(
                    "PortalFx_" + node.Id + "_w" + wave + "_" + i, pos, transform);
                _portals.Add(fx);
                _world.Add(fx);
            }

            string show = PortalFxHook.PlayShow(session.RoomId, wave);
            Debug.Log(show);
            // Clock starts when PortalFx is shown, not when the room was entered / wave 1 cleared.
            float hold = MazeRules.PortalHoldForWave(wave);
            _portalHold = hold;
            Flash("PORTAL " + session.RoomId + " w" + wave + " " + hold.ToString("0.0") + "s");

            float t = 0f;
            while (t < hold && !_skipPortalWait)
            {
                t += Time.deltaTime;
                yield return null;
            }

            _skipPortalWait = false;
            Debug.Log(PortalFxHook.PlaySpawn(session.RoomId, wave));
            ClearPortals();
            _portalWaiting = false;
            _cadence = null;
            SpawnDrawn(session, wave, drawn, spots);
        }

        void SpawnDrawn(CombatRoomSession session, int wave, DrawnComposition drawn, Vector3[] spots)
        {
            Debug.Log(drawn.LogLine());
            Debug.Log("[StagePool] extra=+" + drawn.ExtraAdded
                      + " elite=" + drawn.EliteCount
                      + " n=" + drawn.UnitCount
                      + " tier=" + drawn.Tier
                      + " DRAFT_NOT_LOCKED");
            ClearLive();
            int n = drawn.Units != null ? drawn.Units.Length : 0;
            MazeNode node = _maze.Find(session.RoomId);
            string roomId = node != null ? node.Id : session.RoomId;
            float[] warns;
            spots = EnforceSpawnSpots(node, drawn, wave, spots, out warns);
            for (int i = 0; i < n; i++)
            {
                DrawnUnit u = drawn.Units[i];
                Vector3 pos = spots != null && i < spots.Length
                    ? spots[i]
                    : new Vector3(node != null ? node.Center.X : 0f, node != null ? node.Center.Y : 0f, 0f);
                float warn = warns != null && i < warns.Length ? warns[i] : 0f;
                if (warn > 0f)
                {
                    // L5 fallback: visible warning, unit appears after 1.0s (1.5s when closer than SpawnMinU).
                    int idx = i;
                    DrawnUnit uu = u;
                    Vector3 pp = pos;
                    var ps = new PendingSpawn();
                    ps.Fx = SpawnWarnFx.SpawnAt("SpawnWarn_" + roomId + "_w" + wave + "_" + i, pos, warn, transform);
                    _world.Add(ps.Fx.gameObject);
                    ps.Spawn = () => SpawnUnit(roomId, wave, idx, uu, pp);
                    _pending.Add(ps);
                    ps.Co = StartCoroutine(WarnThenSpawn(ps, warn));
                    Debug.Log("[L5Spawn] warn " + ps.Fx.name + " " + warn.ToString("0.0") + "s at "
                              + pos.x.ToString("0.0") + "," + pos.y.ToString("0.0"));
                    continue;
                }

                SpawnUnit(roomId, wave, i, u, pos);
            }

            session.MarkSpawned(n);
            Flash("wave " + wave + " " + drawn.CompId + " n=" + n);
            if (n <= 0)
                ApplySteps(session, session.NotifyKilled());
        }

        IEnumerator WarnThenSpawn(PendingSpawn ps, float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                yield return null;
            }

            ps.Co = null;
            FinishPending(ps);
        }

        void FinishPending(PendingSpawn ps)
        {
            if (ps == null || ps.Done)
                return;
            ps.Done = true;
            if (ps.Co != null)
                StopCoroutine(ps.Co);
            ps.Co = null;
            if (ps.Fx != null)
                Destroy(ps.Fx.gameObject);
            _pending.Remove(ps);
            ps.Spawn?.Invoke();
        }

        void CancelPending()
        {
            for (int i = 0; i < _pending.Count; i++)
            {
                PendingSpawn ps = _pending[i];
                ps.Done = true;
                if (ps.Co != null)
                    StopCoroutine(ps.Co);
                if (ps.Fx != null)
                    Destroy(ps.Fx.gameObject);
            }

            _pending.Clear();
        }

        void SpawnUnit(string roomId, int wave, int i, DrawnUnit u, Vector3 pos)
        {
            GameObject go = Instantiate(_stubPrefab, pos, Quaternion.identity, transform);
            go.name = "S1_" + roomId + "_w" + wave + "_" + i + "_" + u.KindId + (u.Elite ? "_ELITE" : "");
            JianHaiBind.ApplyTo(go, EntityAnimCatalog.ResolveEnemyIdle(u.KindId));
            var enemy = go.GetComponent<StubEnemy>();
            if (enemy == null)
                enemy = go.AddComponent<StubEnemy>();
            enemy.ConfigureKind(u.KindId, u.Hp, u.Elite);
            var ai = go.GetComponent<MobFourStateAi>();
            if (ai == null)
                ai = go.AddComponent<MobFourStateAi>();
            ai.Configure(_lock, _player, StageId.S1, true, u.Atk, u.Elite);
            EntityAnimView.Add(go, false, u.KindId);
            string capturedId = roomId;
            enemy.Died += _ => OnEnemyDied(capturedId);
            go.SetActive(true);
            _live.Add(go);
        }

        /// <summary>
        /// Spots are rolled when the portal shows; the player keeps moving during the hold. Re-check
        /// MinPlayerDist against the player's position now and keep PackSep between mobs.
        /// </summary>
        Vector3[] EnforceSpawnSpots(MazeNode node, DrawnComposition drawn, int wave, Vector3[] spots, out float[] warns)
        {
            warns = null;
            if (node == null || spots == null || _player == null)
                return spots;
            if (L5Rules.SpawnRuleEnabled)
                return EnforceL5(node, wave, spots, out warns);
            var ss = new SpawnSpot[spots.Length];
            for (int i = 0; i < spots.Length; i++)
            {
                string kind = drawn.Units != null && i < drawn.Units.Length ? drawn.Units[i].KindId : EnemyKindIds.Normal;
                ss[i] = new SpawnSpot { X = spots[i].x, Y = spots[i].y, KindId = kind, Ranged = CombatRoomSpawn.IsRanged(kind) };
            }

            Vector3 p = _player.position;
            int moved = CombatRoomSpawn.EnforceAtSpawn(
                node, p.x, p.y, ss, CollectAvoids(node), new System.Random(seed * 31 + wave * 7 + node.Id.GetHashCode()));
            float minD = float.MaxValue;
            float minPair = float.MaxValue;
            var outSpots = new Vector3[ss.Length];
            for (int i = 0; i < ss.Length; i++)
            {
                outSpots[i] = new Vector3(ss[i].X, ss[i].Y, 0f);
                if (ss[i].DistPlayer < minD)
                    minD = ss[i].DistPlayer;
                for (int j = 0; j < i; j++)
                    minPair = Mathf.Min(minPair, Vector2.Distance(outSpots[i], outSpots[j]));
            }

            Debug.Log("[SpawnLand] enforce room=" + node.Id + " w" + wave + " moved=" + moved
                      + " minPlayerDist=" + (ss.Length > 0 ? minD.ToString("0.00") : "-")
                      + " (need>=" + CombatRoomSpawn.MinPlayerDist.ToString("0.00") + ")"
                      + " minPair=" + (ss.Length > 1 ? minPair.ToString("0.00") : "-")
                      + " (need>=" + CombatRoomSpawn.PackSep.ToString("0.00") + ")");
            return outSpots;
        }

        readonly Vector2[] _l5Quad = new Vector2[4];

        Vector2[] CurrentQuad()
        {
            return ViewSpace.TryGetViewQuad(_l5Quad) ? _l5Quad : null;
        }

        /// <summary>L5 spots when the portals show: 14–22u, ≥2u outside the view quad, walkable room cell.</summary>
        Vector3[] PlaceL5(MazeNode node, int n, int wave)
        {
            float px = _player != null ? _player.position.x : node.Center.X;
            float py = _player != null ? _player.position.y : node.Center.Y;
            L5Spot[] l5 = L5Spawn.PlaceWave(node, px, py, n, CollectAvoids(node), CurrentQuad(),
                new System.Random(seed * 17 + wave * 5 + node.Id.GetHashCode()));
            var vs = new Vector3[l5.Length];
            for (int i = 0; i < l5.Length; i++)
                vs[i] = new Vector3(l5[i].X, l5[i].Y, 0f);
            Debug.Log("[L5Spawn] place " + L5Line(node, wave, l5));
            return vs;
        }

        Vector3[] EnforceL5(MazeNode node, int wave, Vector3[] spots, out float[] warns)
        {
            var l5 = new L5Spot[spots.Length];
            for (int i = 0; i < spots.Length; i++)
                l5[i] = new L5Spot { X = spots[i].x, Y = spots[i].y };
            Vector3 p = _player.position;
            int moved = L5Spawn.EnforceAtSpawn(node, p.x, p.y, l5, CollectAvoids(node), CurrentQuad(),
                new System.Random(seed * 31 + wave * 7 + node.Id.GetHashCode()));
            warns = new float[l5.Length];
            var outSpots = new Vector3[l5.Length];
            for (int i = 0; i < l5.Length; i++)
            {
                outSpots[i] = new Vector3(l5[i].X, l5[i].Y, 0f);
                warns[i] = l5[i].WarnSeconds;
            }

            Debug.Log("[L5Spawn] enforce moved=" + moved + " " + L5Line(node, wave, l5));
            return outSpots;
        }

        static string L5Line(MazeNode node, int wave, L5Spot[] l5)
        {
            int fb = 0;
            float minD = float.MaxValue, maxD = 0f, minOut = float.MaxValue;
            for (int i = 0; i < l5.Length; i++)
            {
                if (l5[i].Fallback) fb++;
                minD = Mathf.Min(minD, l5[i].DistPlayer);
                maxD = Mathf.Max(maxD, l5[i].DistPlayer);
                minOut = Mathf.Min(minOut, l5[i].OutsideQuad);
            }

            return "room=" + node.Id + " w" + wave + " n=" + l5.Length + " fallback=" + fb
                   + " dist=" + (l5.Length > 0 ? minD.ToString("0.0") + ".." + maxD.ToString("0.0") : "-")
                   + " minOutsideQuad=" + (l5.Length > 0 ? minOut.ToString("0.0") : "-")
                   + " iso=" + (ViewSpace.IsoOn ? 1 : 0) + " " + L5Rules.Describe();
        }

        Vector3[] PlaceWaveSpots(MazeNode node, DrawnComposition drawn, int wave)
        {
            int n = drawn.Units != null ? drawn.Units.Length : 0;
            if (n < 1)
                n = 1;
            if (L5Rules.SpawnRuleEnabled)
                return PlaceL5(node, n, wave);
            var kinds = new string[n];
            for (int i = 0; i < n; i++)
                kinds[i] = drawn.Units != null && i < drawn.Units.Length
                    ? drawn.Units[i].KindId
                    : EnemyKindIds.Normal;
            float px = node.Center.X;
            float py = node.Center.Y;
            if (_player != null)
            {
                px = _player.position.x;
                py = _player.position.y;
            }

            SpawnAvoid[] avoids = CollectAvoids(node);
            SpawnSpot[] spots = CombatRoomSpawn.PlaceWave(
                node, px, py, kinds, avoids, Stage1MazeGen.DrawRng(seed, node.Id, wave));
            var vs = new Vector3[spots.Length];
            float meleeSum = 0f;
            int meleeN = 0;
            float rangeSum = 0f;
            int rangeN = 0;
            for (int i = 0; i < spots.Length; i++)
            {
                vs[i] = new Vector3(spots[i].X, spots[i].Y, 0f);
                if (spots[i].Ranged)
                {
                    rangeSum += spots[i].DistPlayer;
                    rangeN++;
                }
                else
                {
                    meleeSum += spots[i].DistPlayer;
                    meleeN++;
                }
            }

            Debug.Log("[SpawnLand] room=" + node.Id + " n=" + spots.Length
                      + " avoids=" + (avoids != null ? avoids.Length : 0)
                      + " minPlayer=" + CombatRoomSpawn.MinPlayerDist.ToString("0.00")
                      + " meleeMean=" + (meleeN > 0 ? (meleeSum / meleeN).ToString("0.00") : "-")
                      + " rangedMean=" + (rangeN > 0 ? (rangeSum / rangeN).ToString("0.00") : "-")
                      + " bypass=SpawnCluster.Offset");
            return vs;
        }

        SpawnAvoid[] CollectAvoids(MazeNode node)
        {
            if (node == null || _sites == null)
                return new SpawnAvoid[0];
            var list = new List<SpawnAvoid>();
            for (int i = 0; i < _sites.Count; i++)
            {
                SiteRuntime s = _sites[i];
                if (s == null)
                    continue;
                Vector3 p = s.WorldPosition;
                if (!node.Contains(p.x, p.y, 0f))
                    continue;
                if (CombatRoomSpawn.ClearanceForKind(s.Kind) < 0.01f)
                    continue;
                list.Add(CombatRoomSpawn.FromSite(s.Kind, p.x, p.y));
            }

            return list.ToArray();
        }

        void OnEnemyDied(string roomId)
        {
            CombatRoomSession session;
            if (!_sessions.TryGetValue(roomId, out session))
                return;
            ApplySteps(session, session.NotifyKilled());
        }

        void SweepDead()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                if (_live[i] == null)
                    _live.RemoveAt(i);
            }
        }

        void KillLiveWave()
        {
            if (_portalWaiting)
            {
                _skipPortalWait = true;
                Flash("skip portal wait");
                return;
            }

            // L5 fallback units still in their warning: spawn them now so the kill covers them.
            var pending = _pending.ToArray();
            for (int i = 0; i < pending.Length; i++)
                FinishPending(pending[i]);

            var snapshot = _live.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                if (snapshot[i] == null)
                    continue;
                var enemy = snapshot[i].GetComponent<StubEnemy>();
                if (enemy != null && !enemy.IsDead)
                    enemy.TakeDamage(999);
            }
        }

        void TryInteract()
        {
            if (_player == null)
                return;
            MazeNode n = RoomAt(_player.position.x, _player.position.y, 0.2f);
            if (n == null)
                return;
            CombatRoomSession s;
            _sessions.TryGetValue(n.Id, out s);
            if (n.SpawnsEnemies && (s == null || s.Phase != CombatRoomPhase.Cleared))
            {
                Flash("clear room first");
                return;
            }

            if (n.Kind == MazeNodeKind.Connector)
            {
                Debug.Log("[S1Maze] connector stub — S2 maze not built");
                Flash("CONN stub (S2 not built)");
                return;
            }

            if (n.Kind == MazeNodeKind.Chest || n.Kind == MazeNodeKind.Altar)
            {
                Flash("walk closer (E range ~1.7)");
                return;
            }

            Debug.Log("[S1Maze] interact room=" + n.Id + " kind=" + MazeRules.Label(n.Kind));
            Flash("interact " + n.Id);
        }

        /// <summary>
        /// Chest / altar (and shop, if a room ever has one) open their RewardScreenView as soon as
        /// the room is cleared — no E. One frame later so the last kill / door open settle first.
        /// Same ChestAltarDirector offer rules as E; E still reopens after Esc.
        /// </summary>
        IEnumerator AutoOpenAfterClear(string roomId)
        {
            yield return null;
            SiteRuntime site = SiteInRoom(roomId);
            if (site == null || _buildDir == null)
            {
                Debug.Log("[AutoOpen] room=" + roomId + " no chest/altar/shop site");
                yield break;
            }

            var vitals = _player != null ? _player.GetComponent<PlayerVitals>() : null;
            if (vitals != null && vitals.IsDead)
            {
                Debug.Log("[AutoOpen] room=" + roomId + " skipped: player dead");
                yield break;
            }

            bool opened = _buildDir.AutoOpen(site, "clear " + roomId);
            Debug.Log("[AutoOpen] room=" + roomId + " site=" + site.Id + " kind=" + site.Kind
                      + " opened=" + opened + (opened ? "" : " (empty chest / claimed altar / already offering)"));
        }

        SiteRuntime SiteInRoom(string roomId)
        {
            for (int i = 0; i < _sites.Count; i++)
            {
                SiteRuntime s = _sites[i];
                if (s == null)
                    continue;
                if (s.Kind != SiteKind.Chest && s.Kind != SiteKind.Altar && s.Kind != SiteKind.Shop)
                    continue;
                if (s.Def.Note == roomId)
                    return s;
            }

            return null;
        }

        /// <summary>Test hook: invoke the clear → auto-open path for a room.</summary>
        public void DebugAutoOpen(string roomId)
        {
            StartCoroutine(AutoOpenAfterClear(roomId));
        }

        // ---- §4.5-1/2 room visibility: covered until the player is inside -----------------

        /// <summary>Inset past the room edge (player centre) that counts as "through the door".</summary>
        public const float RevealInset = 0.3f;
        /// <summary>
        /// Lock / wave-start trigger: player centre this far inside the room (PM 2026-09-25, was 1.15).
        /// Order on entry: reveal at RevealInset (0.3u) → lock + PortalFx at 2.0u, so the room is
        /// always visible before the doors close and the player is clear of the door strip.
        /// </summary>
        public const float LockTriggerInset = 2.0f;
        /// <summary>Above entities (Entity 20) and props; walls/floor (Ground) sit below anyway.</summary>
        public const int CoverSortingOrder = 90;

        void BuildRoomCovers(Transform root)
        {
            _covers.Clear();
            _revealed.Clear();
            for (int i = 0; i < _maze.Nodes.Length; i++)
            {
                MazeNode n = _maze.Nodes[i];
                if (n.Kind == MazeNodeKind.Start)
                {
                    _revealed.Add(n.Id);
                    continue;
                }

                var go = new GameObject("RoomCover_" + n.Id);
                go.transform.SetParent(root, false);
                float cx, cy, cw, ch;
                CoverRect(n, out cx, out cy, out cw, out ch);
                go.transform.position = new Vector3(cx, cy, -0.5f);
                go.transform.localScale = new Vector3(cw, ch, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = CoverSprite();
                sr.color = CoverColor();
                sr.sortingLayerName = JianHaiArtCatalog.LayerEntity;
                sr.sortingOrder = CoverSortingOrder;
                _covers[n.Id] = go;
                _world.Add(go);
            }

            Debug.Log("[RoomCover] covered=" + _covers.Count + " visible=" + string.Join(",", new List<string>(_revealed).ToArray())
                      + " inset=" + RevealInset.ToString("0.00") + " layer=" + JianHaiArtCatalog.LayerEntity + "/" + CoverSortingOrder);
        }

        /// <summary>
        /// Cover rect = room rect. Its edge is the room wall line, i.e. the door strip centre plane
        /// (MazeCollisionBuilder), so the reveal boundary (edge + RevealInset) is measured from the door.
        /// </summary>
        public static void CoverRect(MazeNode n, out float cx, out float cy, out float w, out float h)
        {
            cx = n.Center.X;
            cy = n.Center.Y;
            w = n.Width;
            h = n.Height;
        }

        void RevealRoomAtPlayer()
        {
            if (_player == null || _maze == null || _covers.Count == 0)
                return;
            MazeNode n = RoomAt(_player.position.x, _player.position.y, RevealInset);
            if (n == null || _revealed.Contains(n.Id))
                return;
            RevealRoom(n.Id);
        }

        void RevealRoom(string roomId)
        {
            if (!_revealed.Add(roomId))
                return;
            GameObject cover;
            if (_covers.TryGetValue(roomId, out cover) && cover != null)
                cover.SetActive(false);
            Debug.Log("[RoomCover] reveal " + roomId + " at " + (_player != null ? _player.position.ToString() : "-"));
        }

        /// <summary>
        /// Player order while its sprite overlaps a still-covered room (the doorway band: from the
        /// sprite first poking through the door until the centre is RevealInset inside). Covers stay
        /// at CoverSortingOrder so mobs / props inside stay hidden; only the player draws above.
        /// </summary>
        public const int PlayerDoorwayOrder = CoverSortingOrder + 1;

        bool _playerLifted;

        /// <summary>True while the player is drawn above a covered room's mask (tests).</summary>
        public bool PlayerLiftedOverCover => _playerLifted;

        void LiftPlayerInDoorway()
        {
            if (_player == null)
                return;
            var view = _player.GetComponent<EntityAnimView>();
            var sr = _player.GetComponent<SpriteRenderer>();
            if (view == null || sr == null)
                return;
            bool lift = PlayerOverlapsCover(sr.bounds);
            if (lift != _playerLifted)
                Debug.Log("[RoomCover] player " + (lift ? "lifted above" : "back under") + " covers at " + _player.position);
            _playerLifted = lift;
            view.SortingOverride = lift ? PlayerDoorwayOrder : 0;
        }

        /// <summary>Any active cover rect intersecting these bounds (XY only).</summary>
        public bool PlayerOverlapsCover(Bounds b)
        {
            foreach (KeyValuePair<string, GameObject> kv in _covers)
            {
                GameObject c = kv.Value;
                if (c == null || !c.activeSelf)
                    continue;
                Vector3 cp = c.transform.position;
                Vector3 cs = c.transform.localScale;
                if (b.max.x > cp.x - cs.x * 0.5f && b.min.x < cp.x + cs.x * 0.5f
                    && b.max.y > cp.y - cs.y * 0.5f && b.min.y < cp.y + cs.y * 0.5f)
                    return true;
            }

            return false;
        }

        /// <summary>True while the room's interior is still hidden (tests / HUD).</summary>
        public bool IsRoomCovered(string roomId)
        {
            GameObject cover;
            return _covers.TryGetValue(roomId, out cover) && cover != null && cover.activeSelf;
        }

        /// <summary>Same as the camera clear colour set in BuildWorld, so a covered room reads as void.</summary>
        static Color CoverColor()
        {
            return new Color(0.04f, 0.045f, 0.06f, 1f);
        }

        static Sprite CoverSprite()
        {
            if (_coverSprite != null)
                return _coverSprite;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.filterMode = FilterMode.Point;
            tex.Apply(false, false);
            tex.name = "RoomCover";
            _coverSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            _coverSprite.name = "RoomCover";
            return _coverSprite;
        }

        void SetDoors(string roomId, bool locked)
        {
            for (int i = 0; i < _doors.Count; i++)
            {
                GameObject d = _doors[i];
                if (d == null)
                    continue;
                if (!d.name.StartsWith("Door_" + roomId + "_", StringComparison.Ordinal))
                    continue;
                // Doors are built non-solid (MazeCollisionBuilder: solid = !Door). Locking must make
                // them solid so CollisionWorld.Trace (arrows / orbs) and TryMove (mobs) stop too.
                var vol = d.GetComponent<CollisionVolume>();
                if (vol != null)
                    vol.SetSolid(locked);
                d.SetActive(locked);
            }

            GameObject floor;
            if (_roomFloors.TryGetValue(roomId, out floor) && floor != null)
            {
                var sr = floor.GetComponent<SpriteRenderer>();
                MazeNode room = _maze.Find(roomId);
                if (sr != null && room != null)
                {
                    Color baseC = FloorTint(room.Kind);
                    sr.color = locked
                        ? new Color(baseC.r * 0.55f, baseC.g * 0.35f, baseC.b * 0.35f, 1f)
                        : baseC;
                }
            }
        }

        void ContainPlayer()
        {
            if (_player == null || _maze == null)
                return;
            Vector3 p = _player.position;
            if (_playerBody != null)
            {
                // Physics player: walls and locked doors collide. No per-frame pull-back;
                // only recover when the player is clearly off the level (teleport / tunnelling).
                if (IsInsideLevel(p.x, p.y, SafetyMargin))
                    _lastGood = p;
                else
                {
                    Debug.LogWarning("[S1Maze] safety recover player off-level at " + p + " -> " + _lastGood);
                    MovePlayer(_lastGood);
                }
                return;
            }

            if (_active != null && _active.DoorsLocked)
            {
                MazeNode room = _maze.Find(_active.RoomId);
                if (room != null && !room.Contains(p.x, p.y, 0.35f))
                {
                    p.x = Mathf.Clamp(p.x, room.Center.X - room.Width * 0.5f + 0.45f, room.Center.X + room.Width * 0.5f - 0.45f);
                    p.y = Mathf.Clamp(p.y, room.Center.Y - room.Height * 0.5f + 0.45f, room.Center.Y + room.Height * 0.5f - 0.45f);
                    MovePlayer(p);
                }

                _lastGood = _player.position;
                return;
            }

            if (IsWalkable(p.x, p.y))
            {
                _lastGood = p;
                return;
            }

            MovePlayer(_lastGood);
        }

        void MovePlayer(Vector3 p)
        {
            if (_player == null)
                return;
            _player.position = p;
            if (_playerBody != null)
            {
                _playerBody.position = p;
                _playerBody.velocity = Vector2.zero;
            }
        }

        const float SafetyMargin = 1.0f;

        /// <summary>Rooms/corridors grown by <paramref name="margin"/> (safety fallback only).</summary>
        bool IsInsideLevel(float x, float y, float margin)
        {
            for (int i = 0; i < _maze.Nodes.Length; i++)
            {
                if (_maze.Nodes[i].Contains(x, y, -margin))
                    return true;
            }

            for (int i = 0; i < _maze.Edges.Length; i++)
            {
                if (_maze.Edges[i].Contains(x, y, margin))
                    return true;
            }

            return false;
        }

        bool IsWalkable(float x, float y)
        {
            for (int i = 0; i < _maze.Nodes.Length; i++)
            {
                if (_maze.Nodes[i].Contains(x, y, 0.15f))
                    return true;
            }

            for (int i = 0; i < _maze.Edges.Length; i++)
            {
                if (_maze.Edges[i].Contains(x, y, 0.15f))
                    return true;
            }

            return false;
        }

        MazeNode RoomAt(float x, float y, float inset)
        {
            MazeNode best = null;
            float bestArea = float.MaxValue;
            for (int i = 0; i < _maze.Nodes.Length; i++)
            {
                MazeNode n = _maze.Nodes[i];
                if (!n.Contains(x, y, inset))
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

        void Teleport(string id)
        {
            if (_active != null && _active.DoorsLocked)
            {
                Flash("doors locked");
                return;
            }

            MazeNode n = _maze != null ? _maze.Find(id) : null;
            if (n == null || _player == null)
                return;
            MovePlayer(new Vector3(n.Center.X, n.Center.Y, 0f));
            _lastGood = _player.position;
            Debug.Log("[Teleport] " + id + " " + n.Center);
        }

        void TeleportFirst(MazeNodeKind kind)
        {
            if (_maze == null)
                return;
            for (int i = 0; i < _maze.Nodes.Length; i++)
            {
                if (_maze.Nodes[i].Kind == kind)
                {
                    Teleport(_maze.Nodes[i].Id);
                    return;
                }
            }
        }

        void TeleportFirstChest()
        {
            MazeNode n = _maze != null ? _maze.Find("CHEST") : null;
            if (n == null)
                n = _maze != null ? _maze.Find("CHEST2") : null;
            if (n != null)
                Teleport(n.Id);
        }

        void WriteEvidence()
        {
            string path = Stage1MazeSampler.WriteTo(Stage1MazeSampler.DefaultPath(), seed);
            string pacePath = Stage1MazeSampler.WritePacingTo(Stage1MazeSampler.PacingPath());
            string layoutPath = Stage1MazeSampler.WriteLayoutTo(
                Path.Combine(Stage1MazeSampler.DefaultDirectory(), "stage1_maze_layout_seed42.txt"), 42);
            string playPath = Stage1PlayableSampler.WriteTo(Stage1PlayableSampler.DefaultPath());
            Stage1PlayableSampler.WriteTo(Stage1PlayableSampler.StreamingPath());
            string landPath = Stage1PlayableSampler.WriteSpawnLandTo(Stage1PlayableSampler.SpawnLandDefaultPath());
            Stage1PlayableSampler.WriteSpawnLandTo(Stage1PlayableSampler.SpawnLandStreamingPath());
            Debug.Log("[S1Maze] evidence " + path);
            Debug.Log("[S1Maze] pacing " + pacePath + "\n" + Stage1MazeSampler.PacingText());
            Debug.Log("[S1Maze] layout " + layoutPath);
            Debug.Log("[S1Maze] playable " + playPath);
            Debug.Log("[S1Maze] spawn_land " + landPath);
            Flash("wrote " + path);
        }

        void RunAcceptance()
        {
            bool viewOk = _view != null
                && Mathf.Abs(orthographicSize - CameraViewService.OrthoPlaySize) < 0.01f // serialized ortho authoring size; applied size below
                && Mathf.Abs(_view.OrthographicSize - CameraViewService.PlayOrthoSize) < 0.01f;
            bool speedOk = Mathf.Abs(PlaySpeed() - MazeRules.PlayMoveSpeed) < 0.01f;
            string mazeErr = Stage1MazeChecks.Run();
            string poolErr = StageEnemyPoolChecks.Run();
            string playErr = Stage1PlayableChecks.Run();
            _pass = viewOk && speedOk && mazeErr == null && poolErr == null && playErr == null;
            var sb = new StringBuilder();
            sb.Append(_pass ? "ACCEPTANCE PASS" : "ACCEPTANCE FAIL");
            sb.Append(" view=").Append(viewOk ? 1 : 0);
            sb.Append(" move=").Append(speedOk ? 1 : 0);
            if (mazeErr != null)
                sb.Append(" mazeErr=").Append(mazeErr);
            if (poolErr != null)
                sb.Append(" poolErr=").Append(poolErr);
            if (playErr != null)
                sb.Append(" playErr=").Append(playErr);
            if (_pass)
                sb.Append(" | ").Append(Stage1MazeChecks.FormatPass());
            _status = sb.ToString();
            if (_pass)
                Debug.Log("[S1Maze] " + _status);
            else
                Debug.LogError("[S1Maze] " + _status);
        }

        void ClearPortals()
        {
            for (int i = 0; i < _portals.Count; i++)
            {
                if (_portals[i] != null)
                    Destroy(_portals[i]);
            }

            _portals.Clear();
        }

        void ClearLive()
        {
            CancelPending();
            for (int i = 0; i < _live.Count; i++)
            {
                if (_live[i] != null)
                    Destroy(_live[i]);
            }

            _live.Clear();
        }

        void ClearWorld()
        {
            if (_cadence != null)
            {
                StopCoroutine(_cadence);
                _cadence = null;
            }
            _portalWaiting = false;
            _skipPortalWait = false;
            ClearPortals();
            ClearLive();
            _sessions.Clear();
            _roomFloors.Clear();
            _doors.Clear();
            _sites.Clear();
            _covers.Clear();
            _revealed.Clear();
            CollisionWorld.Clear();
            _active = null;
            if (_player != null)
            {
                Destroy(_player.gameObject);
                _player = null;
            }

            for (int i = 0; i < _world.Count; i++)
            {
                if (_world[i] != null)
                    Destroy(_world[i]);
            }

            _world.Clear();
        }

        void Flash(string msg)
        {
            _flash = msg;
            _flashUntil = Time.unscaledTime + 2.2f;
        }

        float PlaySpeed()
        {
            return moveSpeed > 0.0001f ? moveSpeed : MoveSpeeds.Player;
        }

        static string FloorArt(MazeNodeKind kind)
        {
            switch (kind)
            {
                case MazeNodeKind.Start: return JianHaiArtCatalog.TileFloorSpawn;
                case MazeNodeKind.Altar: return JianHaiArtCatalog.TileFloorAltar;
                case MazeNodeKind.Chest:
                case MazeNodeKind.LargeChest: return JianHaiArtCatalog.TileFloorHub;
                case MazeNodeKind.Connector: return JianHaiArtCatalog.TileFloorCorridor;
                default: return JianHaiArtCatalog.TileFloorCorridor;
            }
        }

        static Color FloorTint(MazeNodeKind kind)
        {
            switch (kind)
            {
                case MazeNodeKind.Start: return new Color(0.95f, 0.97f, 1f, 1f);
                case MazeNodeKind.Altar: return new Color(1f, 0.92f, 1f, 1f);
                case MazeNodeKind.Chest: return new Color(1f, 0.96f, 0.88f, 1f);
                case MazeNodeKind.LargeChest: return new Color(1f, 0.94f, 0.80f, 1f);
                case MazeNodeKind.Connector: return new Color(0.88f, 1f, 0.96f, 1f);
                default: return Color.white;
            }
        }

        void AddWorldLabel(Transform root, string text, Vector3 world)
        {
            var label = new GameObject("Label_" + text);
            label.transform.SetParent(root, false);
            label.transform.position = world;
            var tm = label.AddComponent<TextMesh>();
            tm.text = text;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.characterSize = 0.18f;
            tm.fontSize = 22;
            tm.color = Color.white;
            BuiltinUiFont.Apply(tm);
            _world.Add(label);
        }

        void OnGUI()
        {
            // Reward/shop panel open: hide the debug HUD so it never covers the left card.
            if (RewardScreenView.PanelOpen)
                return;
            const int pad = 8;
            int w = 640;
            int h = 340;
            GUI.Box(new Rect(pad, pad, w, h), "");
            var style = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            var title = new GUIStyle(style) { fontSize = 15, fontStyle = FontStyle.Bold };
            var rich = new GUIStyle(style) { richText = true };
            GUI.Label(new Rect(pad + 8, pad + 4, w - 16, 22), "Stage-1 maze · playable combat (v2e)", title);
            GUI.Label(new Rect(pad + 8, pad + 28, w - 16, 78),
                "WASD · hold LMB/C charge (flying arrow) · Space/LShift dodge i-frame · F strike · chest/altar reward auto-opens on clear (E reopens)\n" +
                "K skip-wave · N new seed · R same seed · F9 log · F1 START · F2 CONN · F3 ALTAR · F4 CHEST · 1/2 N1/N2\n" +
                "rooms hidden until entered · enter combat → lock → [PortalFx] 1.0s → wave1 → clear → open  |  Chest/Altar: clear w1 → [PortalFx] 2.5s (>2 ≤3) → wave2\n" +
                "full charge KB DRAFT · F6 震矢C +20% · F7 震矢R +40% · F8 clear 震矢 · Q 凝神窥机 (HUD bottom-left)\n" +
                "dodge DRAFT DodgeRules dur=0.40s iframe=0.04–0.28s (len=0.24 未锁) cd=1.00s dist=6u cancel charge/recover · JianHai PNG · layers Player/Mob/Wall/Door/Projectile",
                style);
            string graph = _maze != null ? Stage1MazeGen.FormatGraph(_maze) : "";
            GUI.Label(new Rect(pad + 8, pad + 108, w - 16, 36), graph, style);
            string pace = _maze != null
                ? "firstHop " + _pace.FirstHopSeconds.ToString("0.0") + "s (~3s feel) · START→CONN "
                  + _pace.ShortestWalkSeconds.ToString("0") + "s · maxSeg "
                  + _pace.MaxCorridorSeg.ToString("0") + "u (≤30) · move=" + PlaySpeed().ToString("0")
                  + " pitch=" + MazeRules.PitchX.ToString("0") + "/" + MazeRules.PitchY.ToString("0")
                  + " rooms=" + MazeRules.CombatWidth.ToString("0") + "x" + MazeRules.CombatHeight.ToString("0")
                  + " altar=" + MazeRules.AltarWidth.ToString("0") + "x" + MazeRules.AltarHeight.ToString("0")
                : "";
            GUI.Label(new Rect(pad + 8, pad + 144, w - 16, 18), pace, style);
            var dodge = _player != null ? _player.GetComponent<PlayerDodge>() : null;
            string dodgeLine = dodge == null
                ? ""
                : (dodge.IsRolling ? "DODGE" : dodge.IsInvulnerable ? "IFRAME" : dodge.OnCooldown && dodge.LastRollStart > 0f ? "dodge CD" : "dodge ready")
                  + ( _buildDir != null ? "  " + _buildDir.SummaryLine() : "");
            string room = _active != null
                ? "active " + _active.RoomId + " " + _active.Phase + " wave=" + _active.CurrentWave
                  + "/" + _active.WavesTotal + " doors=" + (_active.DoorsLocked ? "LOCKED" : "OPEN")
                  + " live=" + _live.Count
                  + (_portalWaiting ? " PORTAL " + _portalHold.ToString("0.0") + "s" : "")
                  + "  " + dodgeLine
                : "walk a combat room to lock + portal + spawn  " + dodgeLine;
            GUI.Label(new Rect(pad + 8, pad + 164, w - 16, 18), room, style);
            string status = _pass
                ? "<color=#88ff88>" + _status + "</color>"
                : "<color=#ffcc88>" + _status + "</color>";
            GUI.Label(new Rect(pad + 8, pad + 184, w - 16, 48), status, rich);
            if (!string.IsNullOrEmpty(_flash) && Time.unscaledTime < _flashUntil)
                GUI.Label(new Rect(pad + 8, pad + 234, w - 16, 18), _flash, style);
            else if (_buildDir != null && !string.IsNullOrEmpty(_buildDir.FlashMessage()))
                GUI.Label(new Rect(pad + 8, pad + 234, w - 16, 18), _buildDir.FlashMessage(), style);
            // Offers render only through RewardScreenView (#17 removed the IMGUI offer panel).
        }
    }
}
