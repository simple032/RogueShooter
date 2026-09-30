using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using RogueShooter.Ai;
using RogueShooter.Art;
using RogueShooter.Balance;
using RogueShooter.Iso;
using RogueShooter.Maze;
using RogueShooter.Player;
using RogueShooter.Spawning;
using RogueShooter.Vision;
using RogueShooter.Build;

namespace RogueShooter.Demo
{
    /// <summary>
    /// Playable Stage-1 maze skeleton (Spec v0.5). Seeded rooms + lock/clear/open.
    /// Placeholder art. Connector is a stub — S2/S3 mazes are not built.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class Stage1MazeDemo : MonoBehaviour
    {
        [SerializeField] float orthographicSize = CameraViewService.PlayOrthoSize;
        [Tooltip("Play default ~6. 0 = MoveSpeeds.Player (L=22 lock)")]
        [SerializeField] float moveSpeed = MazeRules.PlayMoveSpeed;
        [SerializeField] int seed = 42;

        void Awake()
        {
            Stage1IsoArt.HideHandPlaced(gameObject);
        }

        public const float IsoPitchDeg = 30f;
        public const float IsoYawDeg = 45f;

        Stage1Maze _maze;
        MazePacing _pace;
        Transform _player;
        CameraViewService _view;
        BalanceLockData _lock;
        GameObject _stubPrefab;
        readonly Dictionary<string, CombatRoomSession> _sessions = new Dictionary<string, CombatRoomSession>();
        readonly Dictionary<string, GameObject> _roomFloors = new Dictionary<string, GameObject>();
        readonly Dictionary<string, GameObject> _chests = new Dictionary<string, GameObject>();
        readonly List<GameObject> _doors = new List<GameObject>();
        readonly List<GameObject> _live = new List<GameObject>();
        readonly Dictionary<string, List<GameObject>> _prespawn = new Dictionary<string, List<GameObject>>();
        readonly List<GameObject> _portals = new List<GameObject>();
        readonly List<GameObject> _world = new List<GameObject>();
        CombatRoomSession _active;
        Vector3 _lastGood;
        bool _pass;
        bool _portalWaiting;
        bool _skipPortalWait;
        Coroutine _cadence;
        string _status = "loading…";
        string _flash = "";
        float _flashUntil;
        float _runEpoch;
        RunBuildState _build;
        RewardScreenView _rewardScreen;
        AltarPick[] _chestOffers = System.Array.Empty<AltarPick>();
        AltarPick[] _altarOffers = System.Array.Empty<AltarPick>();
        string _offerRoom;
        bool _offerIsAltar;
        readonly HashSet<string> _takenRewards = new HashSet<string>();
        readonly Dictionary<string, AltarPick[]> _chestRolls = new Dictionary<string, AltarPick[]>();
        readonly Dictionary<string, AltarPick[]> _altarRolls = new Dictionary<string, AltarPick[]>();
        bool _connSettle;
        bool _ignoreClear;
        SpawnBandClock _pressureClock;
        System.Random _offerRng;

        public const float LockEdge = 0.45f;
        /// <summary>Clear-wave key. Off in a normal run so the key cannot kill a room without clearing it.</summary>
        public const bool DevClearHotkey = false;

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
            if (PlayerDown() && RewardOpen())
                CloseReward();
            SyncPressureClock();
            HandleHotkeys();
            if (!PlayerDown())
            {
                ContainPlayer();
                TryEnterRoom();
            }

            SweepDead();
        }

        public float RunSeconds
        {
            get { return Mathf.Max(0f, Time.timeSinceLevelLoad - _runEpoch); }
        }

        public void RestartRun()
        {
            BuildRun(seed);
            RunAcceptance();
        }

        void BuildRun(int newSeed)
        {
            int kept = EconomyGold.StartGold;
            if (_build != null)
                kept = EconomyGold.StartGold + EconomyGold.DeathInherit(_build.Gold);
            ClearWorld();
            if (_build != null)
                _build.Reset(kept);
            seed = newSeed;
            _maze = Stage1MazeGen.Generate(seed);
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
                      + _pace.FirstHopSeconds.ToString("0.0") + "s (door gap 22u, not a lock)"
                      + " shortest=" + _pace.ShortestWalk.ToString("0.0") + "u/"
                      + _pace.ShortestWalkSeconds.ToString("0.0") + "s START→CONN"
                      + " maxSeg=" + _pace.MaxCorridorSeg.ToString("0.0") + "u/"
                      + _pace.MaxCorridorSegSeconds.ToString("0.00") + "s"
                      + " pitch=" + MazeRules.PitchX.ToString("0") + "/"
                      + MazeRules.PitchY.ToString("0"));
            _runEpoch = Time.timeSinceLevelLoad;
            BuildWorld();
            Debug.Log("[Stage1] restart gold=" + (_build != null ? _build.Gold : 0)
                      + " rewards=0 timer=0");
            Debug.Log(Stage1IsoArt.PackLine());
            Debug.Log(Stage1IsoArt.ProportionLine());
            LogDryRun();
            if (!Application.isEditor && Application.isBatchMode)
                Application.Quit();
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
            _world.Add(Stage1IsoArt.BuildFloors(_maze, root));

            for (int i = 0; i < _maze.Nodes.Length; i++)
            {
                MazeNode n = _maze.Nodes[i];
                _roomFloors[n.Id] = null;
                Stage1IsoArt.BuildRoomShell(n, _maze, root, _world, seed, _doors);
                GameObject prop = Stage1IsoArt.BuildProp(n, root);
                if (prop != null)
                {
                    _world.Add(prop);
                    if (n.Kind == MazeNodeKind.Chest || n.Kind == MazeNodeKind.LargeChest)
                        _chests[n.Id] = prop;
                }
                _sessions[n.Id] = new CombatRoomSession(n);
            }

            MazeNode start = _maze.Find("START");
            Vector3 startPos = start != null
                ? new Vector3(start.Center.X, start.Center.Y, 0f)
                : Vector3.zero;

            GameObject player = new GameObject("Player");
            player.transform.position = startPos;
            player.AddComponent<Stage1IsoActor>().Bind("archer");
            player.AddComponent<PlayerMotor2D>().Configure(PlaySpeed());
            player.AddComponent<PlayerVitals>().Configure(EnemyDamageCatalog.PlayerMaxHpRef);
            player.AddComponent<PlayerStrike>().Configure(_lock != null ? _lock.strikeRange : 1.85f);
            player.AddComponent<PlayerCharge>();
            player.AddComponent<PlayerRoll>();
            player.AddComponent<GuaranteedCritActive>();
            EnsureBuild();
            var charge = player.GetComponent<PlayerCharge>();
            if (charge != null)
            {
                charge.BindOwnedRewards(_build.OwnedRewardIds);
                charge.BindMaze(_maze);
            }
            _player = player.transform;
            _lastGood = startPos;
            var hud = GetComponent<Stage1PlayHud>();
            if (hud == null)
                hud = gameObject.AddComponent<Stage1PlayHud>();
            hud.Bind(this);
            hud.BeginRun();
            _world.Add(player);
            Debug.Log("[MoveSpeed] player=" + PlaySpeed().ToString("0.000")
                      + " ortho=" + orthographicSize.ToString("0")
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
            // Art is already isometric. Do not pitch the camera on top of it.
            follow.SetFaceOn(16f);
            follow.SetTarget(player.transform);
            if (cam.GetComponent<IsoSortAxis>() == null)
                cam.gameObject.AddComponent<IsoSortAxis>();

            _stubPrefab = new GameObject("StubEnemyPrefab");
            _stubPrefab.transform.SetParent(root, false);
            _stubPrefab.SetActive(false);
            JianHaiBind.ApplyTo(_stubPrefab, JianHaiArtCatalog.EnemyE1Idle);
            _stubPrefab.AddComponent<StubEnemy>();
            _world.Add(_stubPrefab);
            PreplaceOpeningWaves();
        }

        /// <summary>Wave 1 is already standing in each combat room. AI stays off until the door fight starts.</summary>
        void PreplaceOpeningWaves()
        {
            if (_maze == null || _stubPrefab == null)
                return;
            for (int i = 0; i < _maze.Nodes.Length; i++)
            {
                MazeNode node = _maze.Nodes[i];
                if (node == null || !node.SpawnsEnemies || node.WaveCount < 1)
                    continue;
                DrawnComposition drawn = StageEnemyPool.DrawComposition(
                    StageId.S1, node.PoolRoom, Stage1MazeGen.DrawRng(seed, node.Id, 1));
                Vector3 center = new Vector3(node.Center.X, node.Center.Y, 0f);
                int n = drawn.Units != null ? drawn.Units.Length : 0;
                var parked = new List<GameObject>(n);
                for (int u = 0; u < n; u++)
                {
                    GameObject go = PlaceEnemy(node.Id, 1, u, n, drawn.Units[u], center, false);
                    if (go != null)
                        parked.Add(go);
                }

                _prespawn[node.Id] = parked;
                Debug.Log("[Stage1] prespawn " + node.Id + " " + node.Kind + " n=" + parked.Count);
            }
        }

        public Stage1Maze BuiltMaze { get { return _maze; } }

        public RunBuildState RunBuild
        {
            get
            {
                EnsureBuild();
                return _build;
            }
        }
        public Transform PlayerBody { get { return _player; } }
        public bool DoorHoldActive { get { return _portalWaiting; } }

        public int LiveEnemyCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _live.Count; i++)
                {
                    if (_live[i] == null)
                        continue;
                    StubEnemy enemy = _live[i].GetComponent<StubEnemy>();
                    if (enemy != null && !enemy.IsDead)
                        n++;
                }

                return n;
            }
        }

        public string Prompt
        {
            get { return Time.unscaledTime <= _flashUntil ? _flash : ""; }
        }

        public bool ConnSettle { get { return _connSettle; } }

        public bool OfferChoices
        {
            get { return _rewardScreen != null && _rewardScreen.ChoicesVisible; }
        }

        public void Interact()
        {
            TryInteract();
        }

        public void CloseOffer()
        {
            CancelReward();
        }

        public string[] CurrentOfferIds()
        {
            if (_offerIsAltar)
                return IdsOfAltar(_altarOffers);
            return IdsOfAltar(_chestOffers);
        }

        public int ActiveDoorCount(string roomId)
        {
            int n = 0;
            if (string.IsNullOrEmpty(roomId))
                return 0;
            string prefix = "Door_" + roomId + "_";
            for (int i = 0; i < _doors.Count; i++)
            {
                GameObject d = _doors[i];
                if (d != null && d.activeSelf && d.name.StartsWith(prefix, StringComparison.Ordinal))
                    n++;
            }

            return n;
        }

        static string[] IdsOfAltar(AltarPick[] offers)
        {
            if (offers == null)
                return System.Array.Empty<string>();
            var ids = new string[offers.Length];
            for (int i = 0; i < offers.Length; i++)
                ids[i] = offers[i].Id;
            return ids;
        }

        public int PrespawnCount(string roomId)
        {
            List<GameObject> parked;
            if (string.IsNullOrEmpty(roomId) || !_prespawn.TryGetValue(roomId, out parked) || parked == null)
                return 0;
            return parked.Count;
        }

        public CombatRoomPhase RoomPhase(string roomId)
        {
            CombatRoomSession session;
            if (string.IsNullOrEmpty(roomId) || !_sessions.TryGetValue(roomId, out session) || session == null)
                return CombatRoomPhase.Vacant;
            return session.Phase;
        }

        public int RoomWave(string roomId)
        {
            CombatRoomSession session;
            if (string.IsNullOrEmpty(roomId) || !_sessions.TryGetValue(roomId, out session) || session == null)
                return 0;
            return session.CurrentWave;
        }

        public int RoomWaves(string roomId)
        {
            CombatRoomSession session;
            if (string.IsNullOrEmpty(roomId) || !_sessions.TryGetValue(roomId, out session) || session == null)
                return 0;
            return session.WavesTotal;
        }

        public void ProofClearWave()
        {
            KillLiveWave();
        }

        public void ProofTeleportChest()
        {
            TeleportFirstChest();
        }

        public void ProofTeleportAltar()
        {
            TeleportFirst(MazeNodeKind.Altar);
        }

        public string ProofApply(string id)
        {
            EnsureBuild();
            if (!RewardCatalog.TryGet(id, out RewardRow row))
                return "missing " + id;
            string rarity = row.Tier == RewardTier.High ? "高" : row.Tier == RewardTier.Mid ? "中" : "低";
            _build.GrantBuildPick(id, rarity, 0, row.BuildEquiv);
            ApplyOwnedStats(row);
            PlayerCharge charge = _player != null ? _player.GetComponent<PlayerCharge>() : null;
            if (charge != null)
                charge.BindOwnedRewards(_build.OwnedRewardIds);
            return null;
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

        bool PlayerDown()
        {
            if (_player == null)
                return false;
            PlayerVitals vitals = _player.GetComponent<PlayerVitals>();
            return vitals != null && vitals.IsDown;
        }

        void HandleHotkeys()
        {
            if (PlayerDown())
            {
                if (Input.GetKeyDown(KeyCode.R))
                    RestartRun();
                return;
            }

            if (RewardOpen())
            {
                HandleRewardKeys();
                return;
            }

            if (Input.GetKeyDown(KeyCode.N))
            {
                BuildRun(Environment.TickCount);
                RunAcceptance();
            }

            if (Input.GetKeyDown(KeyCode.R))
                RestartRun();

            if (Input.GetKeyDown(KeyCode.F1))
                Teleport("START");
            if (Input.GetKeyDown(KeyCode.F2))
                Teleport("CONN");
            if (Input.GetKeyDown(KeyCode.F3))
                TeleportFirst(MazeNodeKind.Altar);
            if (Input.GetKeyDown(KeyCode.F4))
                TeleportFirstChest();
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
                Teleport("N1");
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
                Teleport("N2");
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
                TeleportFirst(MazeNodeKind.Altar);
            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
                TeleportFirstChest();
            if (Input.GetKeyDown(KeyCode.K))
                TryDevClear();
            if (Input.GetKeyDown(KeyCode.F9))
                WriteEvidence();
            if (Input.GetKeyDown(KeyCode.E))
                TryInteract();
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
            MazeNode inside = RoomAt(_player.position.x, _player.position.y, 1.15f);
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
                    OpenRoomProps(session.RoomId);
                    Flash("open " + session.RoomId);
                    if (_active == session)
                        _active = null;
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

            List<GameObject> parked;
            if (wave <= 1 && _prespawn.TryGetValue(session.RoomId, out parked))
            {
                _prespawn.Remove(session.RoomId);
                ActivatePrespawn(session, parked);
                yield break;
            }

            _portalWaiting = true;
            _skipPortalWait = false;
            DrawnComposition drawn = StageEnemyPool.DrawComposition(
                StageId.S1, node.PoolRoom, Stage1MazeGen.DrawRng(seed, node.Id, wave));
            Vector3 center = new Vector3(node.Center.X, node.Center.Y, 0f);
            int n = drawn.Units != null ? drawn.Units.Length : 0;
            if (n < 1)
                n = 1;
            ClearPortals();
            for (int i = 0; i < n; i++)
            {
                Vector3 pos = SpawnCluster.Offset(center, i, n);
                GameObject fx = PortalFxStub.SpawnAt(
                    "PortalFx_" + node.Id + "_w" + wave + "_" + i, pos, transform);
                _portals.Add(fx);
                _world.Add(fx);
            }

            string show = PortalFxHook.PlayShow(session.RoomId, wave);
            Debug.Log(show);
            Flash("PORTAL " + session.RoomId + " w" + wave + " now");

            float hold = MazeRules.PlayPortalWaitSeconds;
            float t = 0f;
            while (t < hold && !_skipPortalWait)
            {
                t += Time.deltaTime;
                yield return null;
            }

            _skipPortalWait = false;
            Debug.Log(PortalFxHook.PlaySpawn(session.RoomId, wave, hold));
            ClearPortals();
            _portalWaiting = false;
            _cadence = null;
            SpawnDrawn(session, wave, drawn, center);
        }

        void SpawnDrawn(CombatRoomSession session, int wave, DrawnComposition drawn, Vector3 center)
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
            for (int i = 0; i < n; i++)
            {
                GameObject go = PlaceEnemy(roomId, wave, i, n, drawn.Units[i], center, true);
                if (go != null)
                    _live.Add(go);
            }

            session.MarkSpawned(n);
            Flash("wave " + wave + " " + drawn.CompId + " n=" + n);
            if (n <= 0)
                ApplySteps(session, session.NotifyKilled());
        }

        void ActivatePrespawn(CombatRoomSession session, List<GameObject> parked)
        {
            _portalWaiting = false;
            ClearPortals();
            if (_cadence != null)
            {
                _cadence = null;
            }

            int n = 0;
            if (parked != null)
            {
                for (int i = 0; i < parked.Count; i++)
                {
                    GameObject go = parked[i];
                    if (go == null)
                        continue;
                    MobFourStateAi ai = go.GetComponent<MobFourStateAi>();
                    if (ai != null)
                        ai.enabled = true;
                    _live.Add(go);
                    n++;
                }
            }

            session.MarkSpawned(n);
            Debug.Log("[Stage1] door " + session.RoomId + " prespawn n=" + n + " hold=0");
            if (n <= 0)
                ApplySteps(session, session.NotifyKilled());
        }

        GameObject PlaceEnemy(string roomId, int wave, int index, int n, DrawnUnit u, Vector3 center, bool aiOn)
        {
            if (_stubPrefab == null)
                return null;
            Vector3 pos = SpawnCluster.Offset(center, index, n);
            GameObject go = Instantiate(_stubPrefab, pos, Quaternion.identity, transform);
            go.name = "S1_" + roomId + "_w" + wave + "_" + index + "_" + u.KindId + (u.Elite ? "_ELITE" : "");
            string family = u.KindId == EnemyKindIds.Dog ? "dog"
                : u.KindId == EnemyKindIds.CultMage ? "mage"
                : "skel";
            go.AddComponent<Stage1IsoActor>().Bind(family);
            StubEnemy enemy = go.GetComponent<StubEnemy>();
            if (enemy == null)
                enemy = go.AddComponent<StubEnemy>();
            enemy.ConfigureKind(u.KindId, u.Hp, u.Elite);
            MobFourStateAi ai = go.GetComponent<MobFourStateAi>();
            if (ai == null)
                ai = go.AddComponent<MobFourStateAi>();
            ai.Configure(_lock, _player, StageId.S1, true, u.Atk, u.Elite);
            ai.enabled = aiOn;
            string capturedId = roomId;
            enemy.Died += dead => OnEnemyDied(capturedId, dead);
            BindPressure(go);
            go.SetActive(true);
            return go;
        }

        void SyncPressureClock()
        {
            if (_pressureClock == null)
            {
                var go = new GameObject("Stage1PressureClock");
                go.transform.SetParent(transform, false);
                _pressureClock = go.AddComponent<SpawnBandClock>();
                _pressureClock.SetPaused(true);
            }

            _pressureClock.JumpToMinutes(RunSeconds / 60f);
        }

        void BindPressure(GameObject go)
        {
            if (go == null)
                return;
            SyncPressureClock();
            EnemyPressureState pressure = go.GetComponent<EnemyPressureState>();
            if (pressure == null)
                pressure = go.AddComponent<EnemyPressureState>();
            pressure.Bind(_pressureClock, 1f, 1f);
        }

        public void ProofSetRunSeconds(float seconds)
        {
            _runEpoch = Time.timeSinceLevelLoad - Mathf.Max(0f, seconds);
            SyncPressureClock();
        }

        public void ProofHotkeyKill()
        {
            TryDevClear();
        }

        void OnEnemyDied(string roomId, StubEnemy enemy)
        {
            if (_ignoreClear)
                return;
            DropCoin(enemy);
            CombatRoomSession session;
            if (!_sessions.TryGetValue(roomId, out session))
                return;
            ApplySteps(session, session.NotifyKilled());
        }

        void DropCoin(StubEnemy enemy)
        {
            if (enemy == null)
                return;
            float minutes = RunSeconds / 60f;
            int amount = EconomyGold.KillGold(enemy.KindId, minutes);
            var go = new GameObject("Coin");
            go.transform.position = enemy.transform.position;
            var coin = go.AddComponent<Stage1Coin>();
            coin.Bind(_player, amount, gain =>
            {
                EnsureBuild();
                _build.AddGold(gain);
            });
            _world.Add(go);
        }

        void SweepDead()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                if (_live[i] == null)
                    _live.RemoveAt(i);
            }
        }

        void TryDevClear()
        {
            if (!DevClearHotkey)
            {
                Debug.Log("[Stage1] hotkey ignored dev=0");
                return;
            }

            HotkeyKillIgnoreClear();
        }

        void HotkeyKillIgnoreClear()
        {
            _ignoreClear = true;
            var snapshot = _live.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                if (snapshot[i] == null)
                    continue;
                StubEnemy enemy = snapshot[i].GetComponent<StubEnemy>();
                if (enemy != null && !enemy.IsDead)
                    enemy.TakeDamage(999);
            }

            _ignoreClear = false;
            Debug.Log("[Stage1] hotkey kill ignored clear");
        }

        void KillLiveWave()
        {
            if (_portalWaiting)
            {
                _skipPortalWait = true;
                Flash("skip portal wait");
                return;
            }

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
            if (_player == null || PlayerDown())
                return;
            MazeNode n = RoomAt(_player.position.x, _player.position.y, 0.2f);
            if (n == null)
                return;
            CombatRoomSession s;
            _sessions.TryGetValue(n.Id, out s);
            if (n.SpawnsEnemies && (s == null || s.Phase != CombatRoomPhase.Cleared))
            {
                Flash("先清完这间");
                return;
            }

            if (n.Kind == MazeNodeKind.Connector)
            {
                _connSettle = true;
                EnsureBuild();
                int rewards = _build.OwnedRewardIds != null ? _build.OwnedRewardIds.Count : 0;
                Debug.Log("[Stage1] conn settle gold=" + _build.Gold
                          + " build=" + _build.BuildCount
                          + " rewards=" + rewards
                          + " t=" + RunSeconds.ToString("0.0"));
                return;
            }

            if (n.Kind != MazeNodeKind.Chest && n.Kind != MazeNodeKind.LargeChest && n.Kind != MazeNodeKind.Altar)
            {
                Debug.Log("[S1Maze] interact room=" + n.Id + " kind=" + MazeRules.Label(n.Kind));
                Flash("这里不能开");
                return;
            }

            if (_takenRewards.Contains(n.Id))
            {
                Flash("已经拿过了");
                return;
            }

            OpenReward(n);
        }

        void OpenRoomProps(string roomId)
        {
        }

        bool RewardOpen()
        {
            return _rewardScreen != null && _rewardScreen.Session != null && _rewardScreen.Session.Open;
        }

        void OpenReward(MazeNode room)
        {
            EnsureBuild();
            if (_offerRng == null)
                _offerRng = new System.Random(seed);
            _offerRoom = room.Id;
            _offerIsAltar = room.Kind == MazeNodeKind.Altar;
            RewardScreenView view = RewardView();
            if (_offerIsAltar)
            {
                AltarPick[] saved;
                if (!_altarRolls.TryGetValue(room.Id, out saved))
                {
                    saved = AltarRewardRoll.RollThree(AltarSize.Small, _offerRng);
                    _altarRolls[room.Id] = saved;
                }

                _altarOffers = saved;
                _chestOffers = System.Array.Empty<AltarPick>();
                view.ShowAltar(CardsFromAltar(_altarOffers));
            }
            else
            {
                GameObject chest;
                if (_chests.TryGetValue(room.Id, out chest))
                    Stage1IsoArt.OpenChest(chest);
                AltarPick[] saved;
                if (!_chestRolls.TryGetValue(room.Id, out saved))
                {
                    bool large = room.Kind == MazeNodeKind.LargeChest;
                    saved = AltarRewardRoll.RollThreeChest(large, _offerRng);
                    _chestRolls[room.Id] = saved;
                }

                _chestOffers = saved;
                _altarOffers = System.Array.Empty<AltarPick>();
                view.ShowChest(CardsFromAltar(_chestOffers));
            }

            Debug.Log("[S1Maze] offer " + room.Id + " altar=" + (_offerIsAltar ? 1 : 0));
        }

        void HandleRewardKeys()
        {
            if (_rewardScreen == null || _rewardScreen.Session == null)
                return;
            if (!_rewardScreen.ChoicesVisible)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                    CancelReward();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E))
            {
                CancelReward();
                return;
            }

            int pick = -1;
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) pick = 0;
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) pick = 1;
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) pick = 2;
            if (pick < 0)
                return;
            ConfirmReward(pick);
        }

        void ConfirmReward(int pick)
        {
            if (!RewardOpen() || _rewardScreen == null || !_rewardScreen.ChoicesVisible)
                return;
            EnsureBuild();
            AltarPick[] offers = _offerIsAltar ? _altarOffers : _chestOffers;
            if (offers == null || pick < 0 || pick >= offers.Length)
                return;
            AltarPick chosen = offers[pick];
            if (!RewardCatalog.TryGet(chosen.Id, out RewardRow row))
                return;
            if (_offerIsAltar)
                _build.ConfirmAltarPick(AltarSize.Small, chosen.Id);
            else
            {
                string rarity = row.Tier == RewardTier.High ? "高" : row.Tier == RewardTier.Mid ? "中" : "低";
                _build.GrantBuildPick(chosen.Id, rarity, 0, row.BuildEquiv);
                _build.AddGold(EconomyGold.ChestGold(false, Time.timeSinceLevelLoad / 60f));
            }

            ApplyOwnedStats(row);
            if (!string.IsNullOrEmpty(_offerRoom))
                _takenRewards.Add(_offerRoom);
            var charge = _player != null ? _player.GetComponent<PlayerCharge>() : null;
            if (charge != null)
                charge.BindOwnedRewards(_build.OwnedRewardIds);
            CloseReward();
        }

        void ApplyOwnedStats(RewardRow picked)
        {
            if (_player == null || _build == null)
                return;
            var ids = _build.OwnedRewardIds;
            var vitals = _player.GetComponent<PlayerVitals>();
            if (vitals != null)
            {
                vitals.ApplyRewardMax(RewardStatHooks.ProductMul(ids, "max_hp"));
                if (picked.Stat == "heal" && picked.Value > 0f)
                    vitals.Heal(vitals.MaxHp * picked.Value);
            }

            var motor = _player.GetComponent<PlayerMotor2D>();
            if (motor != null)
                motor.ApplyRewardSpeed(RewardStatHooks.ProductMul(ids, "move_speed"));
        }

        void CancelReward()
        {
            CloseReward();
        }

        void CloseReward()
        {
            if (_rewardScreen != null)
                _rewardScreen.Hide();
            RunPause.InteractOpen = false;
            Time.timeScale = 1f;
            _offerRoom = null;
        }

        RewardScreenView RewardView()
        {
            if (_rewardScreen == null)
            {
                _rewardScreen = GetComponent<RewardScreenView>();
                if (_rewardScreen == null)
                    _rewardScreen = gameObject.AddComponent<RewardScreenView>();
                _rewardScreen.OnPick = ConfirmReward;
            }

            return _rewardScreen;
        }

        static RewardCardData[] CardsFromAltar(AltarPick[] picks)
        {
            if (picks == null)
                return System.Array.Empty<RewardCardData>();
            var cards = new RewardCardData[picks.Length];
            for (int i = 0; i < picks.Length; i++)
                cards[i] = RewardPresent.ToCard(picks[i].Id, picks[i].Tier, "", "", i, false);
            return cards;
        }

        void SetDoors(string roomId, bool locked)
        {
            // The arch pieces are the gates: locked = red tint, open = normal.
            for (int i = 0; i < _doors.Count; i++)
            {
                GameObject d = _doors[i];
                if (d == null)
                    continue;
                if (!d.name.StartsWith("Door_" + roomId + "_", StringComparison.Ordinal))
                    continue;
                var sr = d.GetComponent<SpriteRenderer>();
                if (sr != null)
                    sr.color = locked ? new Color(0.72f, 0.28f, 0.22f, 1f) : Color.white;
            }
        }

        void ContainPlayer()
        {
            if (_player == null || _maze == null)
                return;
            Vector3 p = _player.position;
            if (_active != null && _active.DoorsLocked)
            {
                MazeNode room = _maze.Find(_active.RoomId);
                if (room != null && !room.Contains(p.x, p.y, 0.35f))
                {
                    float hx = room.Width * 0.5f - LockEdge;
                    float hy = room.Height * 0.5f - LockEdge;
                    p.x = Mathf.Clamp(p.x, room.Center.X - hx, room.Center.X + hx);
                    p.y = Mathf.Clamp(p.y, room.Center.Y - hy, room.Center.Y + hy);
                    _player.position = p;
                }

                _lastGood = _player.position;
                return;
            }

            if (IsWalkable(p.x, p.y))
            {
                _lastGood = p;
                return;
            }

            _player.position = _lastGood;
        }

        bool IsWalkable(float x, float y)
        {
            return _maze != null && _maze.OpenAt(x, y);
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
            _player.position = new Vector3(n.Center.X, n.Center.Y, 0f);
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
            Debug.Log("[S1Maze] evidence " + path);
            Debug.Log("[S1Maze] pacing " + pacePath + "\n" + Stage1MazeSampler.PacingText());
            Debug.Log("[S1Maze] layout " + layoutPath);
            Flash("wrote " + path);
        }

        void RunAcceptance()
        {
            bool viewOk = _view != null
                && Mathf.Abs(orthographicSize - CameraViewService.PlayOrthoSize) < 0.01f
                && Mathf.Abs(_view.OrthographicSize - CameraViewService.PlayOrthoSize) < 0.01f;
            bool speedOk = Mathf.Abs(PlaySpeed() - MazeRules.PlayMoveSpeed) < 0.01f;
            string mazeErr = Stage1MazeChecks.Run();
            string poolErr = StageEnemyPoolChecks.Run();
            _pass = viewOk && speedOk && mazeErr == null && poolErr == null;
            var sb = new StringBuilder();
            sb.Append(_pass ? "ACCEPTANCE PASS" : "ACCEPTANCE FAIL");
            sb.Append(" view=").Append(viewOk ? 1 : 0);
            sb.Append(" move=").Append(speedOk ? 1 : 0);
            if (mazeErr != null)
                sb.Append(" mazeErr=").Append(mazeErr);
            if (poolErr != null)
                sb.Append(" poolErr=").Append(poolErr);
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

        void ClearPrespawn()
        {
            foreach (KeyValuePair<string, List<GameObject>> pair in _prespawn)
            {
                List<GameObject> parked = pair.Value;
                if (parked == null)
                    continue;
                for (int i = 0; i < parked.Count; i++)
                {
                    if (parked[i] != null)
                        Destroy(parked[i]);
                }
            }

            _prespawn.Clear();
        }

        void ClearShots()
        {
            ArrowFly[] arrows = UnityEngine.Object.FindObjectsOfType<ArrowFly>();
            for (int i = 0; i < arrows.Length; i++)
            {
                if (arrows[i] != null)
                    Destroy(arrows[i].gameObject);
            }

            MageOrbProjectile[] orbs = UnityEngine.Object.FindObjectsOfType<MageOrbProjectile>();
            for (int i = 0; i < orbs.Length; i++)
            {
                if (orbs[i] != null)
                    Destroy(orbs[i].gameObject);
            }
        }

        void ClearLive()
        {
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
            ClearPrespawn();
            ClearShots();
            ClearPortals();
            ClearLive();
            _sessions.Clear();
            _roomFloors.Clear();
            _chests.Clear();
            _doors.Clear();
            _takenRewards.Clear();
            _chestRolls.Clear();
            _altarRolls.Clear();
            _connSettle = false;
            CloseReward();
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

        static Color FloorColor(MazeNodeKind kind)
        {
            switch (kind)
            {
                case MazeNodeKind.Start: return new Color(0.16f, 0.22f, 0.28f);
                case MazeNodeKind.Altar: return new Color(0.22f, 0.14f, 0.26f);
                case MazeNodeKind.Chest: return new Color(0.26f, 0.18f, 0.10f);
                case MazeNodeKind.LargeChest: return new Color(0.32f, 0.24f, 0.08f);
                case MazeNodeKind.Connector: return new Color(0.10f, 0.24f, 0.22f);
                default: return new Color(0.102f, 0.114f, 0.141f);
            }
        }

        void OnGUI()
        {
            if (!string.IsNullOrEmpty(_flash) && Time.unscaledTime <= _flashUntil)
            {
                var prompt = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 22,
                    alignment = TextAnchor.MiddleCenter
                };
                GUI.Label(new Rect(0f, Screen.height * 0.72f, Screen.width, 40f), _flash, prompt);
            }

            if (!_connSettle)
                return;
            int w = 360;
            int h = 160;
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;
            GUI.Box(new Rect(x, y, w, h), "");
            var title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            var line = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            EnsureBuild();
            int rewards = _build.OwnedRewardIds != null ? _build.OwnedRewardIds.Count : 0;
            GUI.Label(new Rect(x + 16f, y + 16f, w - 32f, 28f), "连接口结算", title);
            GUI.Label(new Rect(x + 16f, y + 52f, w - 32f, 24f), "金币 " + _build.Gold, line);
            GUI.Label(new Rect(x + 16f, y + 80f, w - 32f, 24f), "强化 " + rewards, line);
            GUI.Label(new Rect(x + 16f, y + 108f, w - 32f, 24f), "用时 " + Mathf.FloorToInt(RunSeconds) + " 秒", line);
        }
    }
}
