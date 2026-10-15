using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using RogueShooter.Ai;
using RogueShooter.Art;
using RogueShooter.Balance;
using RogueShooter.Iso;
using RogueShooter.Maze;
using RogueShooter.Player;
using RogueShooter.Spawning;
using RogueShooter.Vision;
using RogueShooter.Build;
using RogueShooter.Boss;

namespace RogueShooter.Demo
{
    /// <summary>
    /// Playable Stage-1 maze skeleton (Spec v0.5). Seeded rooms + lock/clear/open.
    /// Three-stage run reusing the approved sample shells, floors and dressing.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class Stage1MazeDemo : MonoBehaviour
    {
        [SerializeField] float orthographicSize = CameraViewService.PlayOrthoSize;
        [Tooltip("Play default ~6. 0 = MoveSpeeds.Player (L=22 lock)")]
        [SerializeField] float moveSpeed = MazeRules.PlayMoveSpeed;
        [SerializeField] int seed = 42;
        [SerializeField] bool randomizeSeedOnStart = true;

        void Awake()
        {
            Stage1IsoArt.HideHandPlaced(gameObject);
        }

        public const float IsoPitchDeg = 30f;
        public const float IsoYawDeg = 45f;

        Stage1Maze _maze;
        StageId _stage = StageId.S1;
        S2S3Layout _phaseLayout;
        StageLevelMap _level;
        readonly Dictionary<string, StageAltarView> _altars = new Dictionary<string, StageAltarView>();
        readonly List<Stage1DoorGate> _forwardGates = new List<Stage1DoorGate>();
        readonly List<Stage1DoorGate> _returnGates = new List<Stage1DoorGate>();
        StartRoomTutorial _tutorial;
        public StartRoomTutorial Tutorial => _tutorial;
        float _startPausedSeconds;
        float _startPauseAt = -1f;
        public bool StartClockPaused => _startPauseAt >= 0f;
        public bool IsInStartRoom => _player != null && _maze != null && _maze.Find("START") != null
            && _maze.Find("START").Contains(_player.position.x, _player.position.y, 0f);
        float _altarPausedSeconds;
        float _altarPauseAt = -1f;
        float _menuPausedSeconds;
        float _menuPauseAt = -1f;
        /// <summary>ESC 暂停菜单打开中：世界冻结、热键与房间逻辑挂起、run clock 停走。</summary>
        public bool MenuPaused { get { return _menuPauseAt >= 0f; } }
        public const string StartMenuSceneName = "StartMenu";
        float _stageBannerUntil;
        public bool AltarClockPaused { get { return _altarPauseAt >= 0f; } }
        public string StageBanner { get { return Time.unscaledTime < _stageBannerUntil ? "第"+(int)_stage+"阶段 · "+(_stage == StageId.S2 ? "锈铜遗库" : "寒骨内殿") : ""; } }
        public StageId StageForRoom(string id) { return _level != null ? _level.RoomStage(id) : StageId.S1; }
        public StageAltarView AltarView(string id) { return _altars.TryGetValue(id,out var v) ? v : null; }
        static AltarSize SizeForStage(StageId stage) { return stage == StageId.S3 ? AltarSize.Large : stage == StageId.S2 ? AltarSize.Mid : AltarSize.Small; }
        int _largeChestRooms;
        public StageId CurrentStage { get { return _stage; } }
        public S2S3Layout PhaseLayout { get { return _phaseLayout; } }
        AltarSize CurrentAltarSize { get { return _stage == StageId.S3 ? AltarSize.Large : _stage == StageId.S2 ? AltarSize.Mid : AltarSize.Small; } }
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
        bool _offerIsShop;
        FinalPrepShop _shop;
        public FinalPrepShop FinalShop => _shop;
        public bool FinalShopOpen => _offerIsShop && RewardOpen();
        public bool ReadyForFinalShop => !RunFlow.IsStage1Trial && _stage==StageId.S3 && AltarReady(StageId.S3) && !_connSettle && !PlayerDown() && _finalBoss!=null && !_finalBoss.FightStarted;
        public void ShowFinalShop(RewardCardData[] cards,string caption) { _offerIsShop=true;_offerIsAltar=false;_offerRoom=null;RewardView().ShowShop(cards,caption); }
        public void ApplyShopReward(RewardRow row) { ApplyOwnedStats(row);_player?.GetComponent<PlayerCharge>()?.BindOwnedRewards(_build.OwnedRewardIds); }
        readonly HashSet<string> _takenRewards = new HashSet<string>();
        readonly Dictionary<string, AltarPick[]> _chestRolls = new Dictionary<string, AltarPick[]>();
        readonly Dictionary<string, AltarPick[]> _altarRolls = new Dictionary<string, AltarPick[]>();
        bool _connSettle;
        bool _runWon;
        BossFightDriver _finalBoss;
        Stage1DoorGate _finalExit;
        public BossFightDriver FinalBoss => _finalBoss;
        public string EndTitle => _runWon ? (RunFlow.IsStage1Trial ? "第一阶段试玩完成" : "通关 · 骸冠已破") : "失败 · 结算";
        bool _ignoreClear;
        bool _suppressExperience;
        SpawnBandClock _pressureClock;
        System.Random _offerRng;

        public const float LockEdge = 0.45f;
        /// <summary>Clear-wave key. Off in a normal run so the key cannot kill a room without clearing it.</summary>
        public const bool DevClearHotkey = false;
        /// <summary>N, F1–F4, 1–4, F6–F9. Off for a normal play. K stays on DevClearHotkey.</summary>
        public const bool DevHotkeys = false;
        public const string RestartLabel = "重新开始（R）";

        IEnumerator Start()
        {
            BalanceLock.TryLoadFromStreamingAssets(out _lock, out _);
            LoadDraftPool();
            BuildRun(randomizeSeedOnStart ? PickRestartSeed() : seed);
            yield return null;
            RunAcceptance();
        }

        void Update()
        {
            if (PlayerDown())
                CatchDeath();
            if (PlayerDown() && RewardOpen())
                CloseReward();
            HandleHotkeys();
            if (!PlayerDown() && !MenuPaused)
            {
                ContainPlayer();
                TryEnterRoom();
                UpdateProgressionGates();
            }

            UpdateAltarClock();
            UpdateStartClock();
            GuaranteedCritActive.TimePressureHeld = StartClockPaused || AltarClockPaused || MenuPaused;
            SyncPressureClock();
            SweepDead();
        }

        float _settleSeconds = -1f;
        bool _deathFrozen;
        int _restartNonce;

        public float RunSeconds
        {
            get
            {
                if (_settleSeconds >= 0f)
                    return _settleSeconds;
                return Mathf.Max(0f, Time.timeSinceLevelLoad - _runEpoch - _startPausedSeconds
                    - (_startPauseAt >= 0f ? Time.timeSinceLevelLoad - _startPauseAt : 0f) - _altarPausedSeconds
                    - (_altarPauseAt >= 0f ? Time.timeSinceLevelLoad - _altarPauseAt : 0f) - _menuPausedSeconds
                    - (_menuPauseAt >= 0f ? Time.timeSinceLevelLoad - _menuPauseAt : 0f));
            }
        }

        public int Seed { get { return seed; } }

        public void RestartRun()
        {
            BuildRun(PickRestartSeed());
            RunAcceptance();
        }

        public int PickRestartSeed()
        {
            _restartNonce++;
            int next = unchecked(Environment.TickCount + _restartNonce * 104729);
            if (next == seed || next == 0)
                next = unchecked(seed + _restartNonce * 17 + 1);
            return next;
        }

        /// <summary>ESC menu can only open in normal play: no death, no card overlay,
        /// no settle panel and no scripted transition already owning the clock.</summary>
        public bool CanMenuPause()
        {
            return !MenuPaused && !PlayerDown() && !RewardOpen() && !_connSettle
                && !RunPause.BossTransition && !RunPause.RitualActing;
        }

        /// <summary>Open the ESC pause. Start/altar holds are folded first so the paused
        /// span is banked exactly once (RunSeconds subtracts it via the menu hold).</summary>
        public void OpenMenuPause()
        {
            if (MenuPaused || !CanMenuPause())
                return;
            float now = Time.timeSinceLevelLoad;
            if (_startPauseAt >= 0f)
            {
                _startPausedSeconds += now - _startPauseAt;
                _startPauseAt = -1f;
            }
            if (_altarPauseAt >= 0f)
            {
                _altarPausedSeconds += now - _altarPauseAt;
                _altarPauseAt = -1f;
            }
            _menuPauseAt = now;
            RunPause.MenuOpen = true;
            RogueShooter.Audio.JianHaiAudio.Emit("jh_ui_open",this);
            Time.timeScale = 0f;
            Debug.Log("[Stage1] menu pause open t=" + RunSeconds.ToString("0.0"));
        }

        public void CloseMenuPause()
        {
            if (!MenuPaused)
                return;
            _menuPausedSeconds += Time.timeSinceLevelLoad - _menuPauseAt;
            _menuPauseAt = -1f;
            RunPause.MenuOpen = false;
            RogueShooter.Audio.JianHaiAudio.Emit("jh_ui_close",this);
            if (!RunPause.RunSettled && !RunPause.InteractOpen && !RunPause.BossTransition && !RunPause.RitualActing)
                Time.timeScale = 1f;
            // Re-arm the room holds in the same frame so no sub-frame wall-clock
            // leaks between the menu closing and the next Update.
            UpdateStartClock();
            UpdateAltarClock();
            Debug.Log("[Stage1] menu pause close t=" + RunSeconds.ToString("0.0"));
        }

        public void RestartFromMenu()
        {
            CloseMenuPause();
            RestartRun();
        }

        /// <summary>Back to the start-menu scene. Cross-scene pause statics are cleared
        /// so neither the menu nor the next run inherits this scene's state.</summary>
        public void ReturnToStartMenu()
        {
            _menuPauseAt = -1f;
            RunPause.ResetAll();
            SceneManager.LoadScene(StartMenuSceneName);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void BuildRun(int newSeed)
        {
            RogueShooter.Audio.JianHaiAudio.BeforeRebuild();
            int kept = EconomyGold.StartGold;
            if (_build != null)
                kept = EconomyGold.StartGold + EconomyGold.DeathInherit(_build.Gold);
            ClearWorld();
            if (_build != null)
                _build.Reset(kept);
            seed = newSeed;
            _offerRng = new System.Random(newSeed);
            _stage = StageId.S1;
            _phaseLayout = null;
            _level = StageLevelMap.Generate(seed, RunFlow.IsStage1Trial);
            _maze = _level.Maze;
            _altarPausedSeconds = 0f; _altarPauseAt = -1f; _stageBannerUntil = 0f;
            _largeChestRooms = _maze.CountKind(MazeNodeKind.LargeChest);
            _pace = Stage1MazeGen.MeasurePacing(Stage1MazeGen.Generate(seed), PlaySpeed());
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
            _startPausedSeconds = 0f; _startPauseAt = _runEpoch;
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
            Transform prefabRoot = null;
            for (int p = 0; p < _level.Parts.Count; p++)
            {
                var part = _level.Parts[p]; var stage = (StageId)(p+1);
                var layout = _level.Layout(stage);
                Transform root = new GameObject("StageMap_" + stage).transform;
                root.SetParent(transform, false); _world.Add(root.gameObject);
                if (p == 0) prefabRoot = root;
                var floorRoot = Stage1IsoArt.BuildFloors(part, root, _maze, p == 0); _world.Add(floorRoot);
                foreach (var n in part.Nodes)
                {
                    _roomFloors[n.Id] = null;
                    RoomWalkShape.BuildWalls(n,root);
                    Stage1IsoArt.BuildRoomShell(n, _maze, root, _world, part.Seed, _doors, stage != StageId.S3);
                    if (n.Kind != MazeNodeKind.Altar)
                    {
                        GameObject prop = Stage1IsoArt.BuildProp(n, root);
                        if (prop != null) { _world.Add(prop); if (n.Kind == MazeNodeKind.Chest || n.Kind == MazeNodeKind.LargeChest) _chests[n.Id] = prop; }
                        if (layout != null && n.Kind!=MazeNodeKind.Heal) S2S3PhaseDress.DressRoom(layout, layout.Find(n.Id), _maze, root, _world);
                    }
                    if(n.Kind==MazeNodeKind.Heal) HealingRoomView.Create(n,root,_world);
                    RoomSolidProps.Bind(n,root);
                    _sessions[n.Id] = new CombatRoomSession(n);
                }
                S2S3CorridorShell.Build(StageLevelMap.CorridorLayout(part), root, _world);
                if (layout != null) S2S3PhaseDress.FinishPhase(layout, _maze, root, _world, floorRoot.GetComponentInChildren<UnityEngine.Tilemaps.Tilemap>());
                foreach (var n in part.Nodes)
                    if (n.Kind == MazeNodeKind.Altar)
                    {
                        var link = _level.Links.Find(e => e.FromId == n.Id) ?? (_level.FinalLink != null && _level.FinalLink.FromId==n.Id ? _level.FinalLink : null);
                        Vector2 outlet = link != null ? new Vector2(link.From.X,link.From.Y) : new Vector2(n.Center.X,n.Center.Y+11f);
                        _altars[n.Id] = StageAltarView.Create(n,stage,outlet,root,_world);
                        RoomSolidProps.Bind(n,root);
                    }
            }
            foreach (var link in _level.Links)
            {
                var n = _maze.Find(link.FromId); var dx = link.From.X-n.Center.X; var dy = link.From.Y-n.Center.Y;
                char rim = Mathf.Abs(dx)>Mathf.Abs(dy) ? (dx>0?'E':'W') : (dy>0?'N':'S');
                _forwardGates.Add(_doors.Find(g => g != null && g.name == "Door_"+n.Id+"_"+rim)?.GetComponent<Stage1DoorGate>());
                var target = _maze.Find(link.ToId);
                float tx=link.To.X-target.Center.X,ty=link.To.Y-target.Center.Y;
                char backRim=Mathf.Abs(tx)>Mathf.Abs(ty)?(tx>0?'E':'W'):(ty>0?'N':'S');
                _returnGates.Add(_doors.Find(g => g != null && g.name == "Door_"+link.ToId+"_"+backRim)?.GetComponent<Stage1DoorGate>());
            }
            UpdateProgressionGates();
            MazeNode start = _maze.Find("START");
            Vector3 startPos = start != null ? new Vector3(start.Center.X,start.Center.Y,0f) : Vector3.zero;

            GameObject player = _player != null ? _player.gameObject : new GameObject("Player");
            player.transform.position = startPos;
            if (_player == null)
            {
            player.AddComponent<Stage1IsoActor>().Bind("archer");
            player.AddComponent<PlayerMotor2D>().Configure(PlaySpeed());
            player.AddComponent<PlayerVitals>().Configure(EnemyDamageCatalog.PlayerMaxHpRef);
            player.AddComponent<PlayerStrike>().Configure(_lock != null ? _lock.strikeRange : 1.85f);
            player.AddComponent<PlayerCharge>();
            player.AddComponent<PlayerRoll>();
            player.AddComponent<GuaranteedCritActive>();
            }
            var experience = player.GetComponent<PlayerExperience>();
            if (experience == null) experience = player.AddComponent<PlayerExperience>();
            experience.ResetRun(player.GetComponent<PlayerVitals>());
            EnsureBuild();
            var charge = player.GetComponent<PlayerCharge>();
            if (charge != null)
            {
                charge.BindOwnedRewards(_build.OwnedRewardIds);
                charge.BindMaze(_maze, _doors);
            }
            player.GetComponent<PlayerMotor2D>().BindMaze(_maze,_doors);
            _player = player.transform;
            _lastGood = startPos;
            var hud = GetComponent<Stage1PlayHud>();
            if (hud == null)
                hud = gameObject.AddComponent<Stage1PlayHud>();
            hud.Bind(this);
            hud.BeginRun();
            var pauseMenu = GetComponent<Stage1PauseMenu>();
            if (pauseMenu == null)
                pauseMenu = gameObject.AddComponent<Stage1PauseMenu>();
            pauseMenu.Bind(this);
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
            _stubPrefab.transform.SetParent(prefabRoot, false);
            _stubPrefab.SetActive(false);
            JianHaiBind.ApplyTo(_stubPrefab, JianHaiArtCatalog.EnemyE1Idle);
            _stubPrefab.AddComponent<StubEnemy>();
            _world.Add(_stubPrefab);
            PreplaceOpeningWaves();
            BuildRoomHazards();
            BuildFinalBossWing();
            _tutorial = player.GetComponent<StartRoomTutorial>();
            if (_tutorial == null) _tutorial = player.AddComponent<StartRoomTutorial>();
            _tutorial.Bind(this, start);
            SetDoors("START", _tutorial.Active);
            RogueShooter.Audio.JianHaiAudio.BindRun(this);
        }

        void BuildRoomHazards()
        {
            foreach(var node in _maze.Nodes)if(RoomHazard.MayBind(StageForRoom(node.Id),node.Kind))
            {var go=new GameObject("RoomHazard_"+node.Id);go.transform.SetParent(transform,false);_world.Add(go);go.AddComponent<RoomHazard>().Bind(this,node);}
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
                DrawnComposition drawn = StageEnemyPool.DrawRoom(
                    StageForRoom(node.Id), node.Id, node.PoolRoom, 1, Stage1MazeGen.DrawRng(_level.Parts[(int)StageForRoom(node.Id)-1].Seed, node.Id, 1));
                Vector3 center = new Vector3(node.Center.X, node.Center.Y, 0f);
                int n = drawn.Units != null ? drawn.Units.Length : 0;
                var parked = new List<GameObject>(n);
                var kinds=Array.ConvertAll(drawn.Units, unit=>unit.KindId);
                var firstSpots=Stage1Wave2.Spots(node,center,kinds,_maze,opening:true);
                for (int u = 0; u < n; u++)
                {
                    GameObject go = PlaceEnemy(node.Id, 1, u, n, drawn.Units[u], firstSpots[u], false, true);
                    if (go != null)
                        parked.Add(go);
                }

                _prespawn[node.Id] = parked;
                Debug.Log("[Stage1] prespawn " + node.Id + " " + node.Kind + " n=" + parked.Count);
            }
        }

        void BuildFinalBossWing()
        {
            if(_level.BossWing==null)return;var root=new GameObject("Final boss wing").transform;root.SetParent(transform,false);_world.Add(root.gameObject);
            _world.Add(Stage1IsoArt.BuildFloors(_level.BossWing,root,_maze,false));
            foreach(var n in _level.BossWing.Nodes)Stage1IsoArt.BuildRoomShell(n,_maze,root,_world,seed,_doors,false,true);
            S2S3CorridorShell.Build(StageLevelMap.CorridorLayout(_level.BossWing),root,_world);
            var arena=_level.BossRoom;var center=new Vector2(arena.Center.X,arena.Center.Y);
            S2S3EnvironmentPolish.RingPaving(center,"BOSS",root,_world);
            var bossLink=_level.BossWing.Edges[0];var at=bossLink.To;
            char entranceRim=at.Y<center.y?'S':'N';
            var entrance=_doors.Find(g=>g!=null && g.name=="Door_BOSS_"+entranceRim)?.GetComponent<Stage1DoorGate>();
            var go=new GameObject("骸冠典狱长 · Meshy v04");go.transform.SetParent(root,false);go.transform.position=new Vector3(center.x,center.y-3,0);
            _finalBoss=go.AddComponent<BossFightDriver>();_finalBoss.BindArena(this,_maze,arena,_player);_finalBoss.BindEntranceGate(entrance);
            var outlet=_level.FinalLink;var source=_maze.Find(outlet.FromId);float dx=outlet.From.X-source.Center.X,dy=outlet.From.Y-source.Center.Y;
            char rim=Mathf.Abs(dx)>Mathf.Abs(dy)?(dx>0?'E':'W'):(dy>0?'N':'S');_finalExit=_doors.Find(g=>g!=null&&g.name=="Door_"+source.Id+"_"+rim)?.GetComponent<Stage1DoorGate>();
            var safe=_maze.Find("PRE_BOSS");var shopGo=new GameObject("Final safe room supplies");shopGo.transform.SetParent(root,false);_shop=shopGo.AddComponent<FinalPrepShop>();_shop.Bind(this,safe,seed);
            UpdateProgressionGates();
        }
        void BeginFinalBoss()
        {
            if(_finalBoss==null||_finalBoss.FightStarted)return;
            foreach(var ai in UnityEngine.Object.FindObjectsOfType<MobFourStateAi>(true))ai.enabled=false;
            ClearCombatProjectiles();_finalBoss.BeginEnter(_build.BuildCount,RunSeconds/60f);
            Flash("骸冠典狱长 · 入口已封闭");
        }
        public void ClearCombatProjectiles(){ClearShots();}
        public void CompleteBossFight(bool won)
        {if(_connSettle)return;if(won) Experience?.CreditBossVictory();_runWon=won;RogueShooter.Audio.JianHaiAudio.Settle(won?"jh_run_victory":"jh_run_defeat");_settleSeconds=RunSeconds;_connSettle=true;CloseReward();ClearCombatProjectiles();RunPause.EnterSettled();}

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
        public PlayerExperience Experience => _player != null ? _player.GetComponent<PlayerExperience>() : null;
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

        public bool AudioCombatActive => _active != null && _active.Phase == CombatRoomPhase.Fighting;
        public bool ConnSettle { get { return _connSettle; } }

        /// <summary>Victory keep uses the death-inherit rate and cap. No second ratio.</summary>
        public int VictoryKeep
        {
            get
            {
                if (!_connSettle || _build == null)
                    return 0;
                return EconomyGold.DeathInherit(_build.Gold);
            }
        }

        public RunEndInfo EndInfo()
        {
            EnsureBuild();
            int rewards = _build.OwnedRewardIds != null ? _build.OwnedRewardIds.Count : 0;
            return new RunEndInfo
            {
                Seconds = Mathf.FloorToInt(RunSeconds),
                Rewards = rewards,
                Keep = EconomyGold.DeathInherit(_build.Gold),
                Restart = RestartLabel
            };
        }

        public int HeldGold
        {
            get
            {
                EnsureBuild();
                return _build.Gold;
            }
        }

        public void ProofSetGold(int gold)
        {
            EnsureBuild();
            _build.Reset(gold < 0 ? 0 : gold);
        }

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

        public void ProofOpenConnector()
        {
            Teleport("CONN");
            TryInteract();
        }

        public string ProofApply(string id)
        {
            EnsureBuild();
            if (!RewardCatalog.TryGet(id, out RewardRow row))
                return "missing " + id;
            if(RewardCatalog.IsUniqueId(id)&&_build.OwnedRewardIds.Contains(id))return "already owned "+id;
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

        public void CatchDeath()
        {
            if (_deathFrozen || !PlayerDown())
                return;
            _deathFrozen = true;
            RogueShooter.Audio.JianHaiAudio.Settle("jh_run_defeat");
            if (_settleSeconds < 0f)
                _settleSeconds = RunSeconds;
            RunPause.EnterCombatEnd();
            ClearShots();
            Debug.Log("[Stage1] death end t=" + RunSeconds.ToString("0.0")
                      + " scale=" + Time.timeScale.ToString("0")
                      + " shots cleared");
        }

        void HandleHotkeys()
        {
            if (MenuPaused)
                return;
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

            if (DevHotkeys && Input.GetKeyDown(KeyCode.N))
            {
                BuildRun(Environment.TickCount);
                RunAcceptance();
            }

            if (Input.GetKeyDown(KeyCode.R))
                RestartRun();

            if (DevHotkeys && Input.GetKeyDown(KeyCode.F1))
                Teleport("START");
            if (DevHotkeys && Input.GetKeyDown(KeyCode.F2))
                Teleport("CONN");
            if (DevHotkeys && Input.GetKeyDown(KeyCode.F3))
                TeleportFirst(MazeNodeKind.Altar);
            if (DevHotkeys && Input.GetKeyDown(KeyCode.F4))
                TeleportFirstChest();
            if (DevHotkeys && (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)))
                Teleport("N1");
            if (DevHotkeys && (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)))
                Teleport("N2");
            if (DevHotkeys && (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)))
                TeleportFirst(MazeNodeKind.Altar);
            if (DevHotkeys && (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)))
                TeleportFirstChest();
            if (Input.GetKeyDown(KeyCode.K))
                TryDevClear();
            if (DevHotkeys && Input.GetKeyDown(KeyCode.F9))
                WriteEvidence();
            if (Input.GetKeyDown(KeyCode.E))
                TryInteract();
            if (DevHotkeys && Input.GetKeyDown(KeyCode.F6))
                GrantZhenShi(KnockbackRewardDraft.IdLow);
            if (DevHotkeys && Input.GetKeyDown(KeyCode.F7))
                GrantZhenShi(KnockbackRewardDraft.IdMid);
            if (DevHotkeys && Input.GetKeyDown(KeyCode.F8))
                ClearZhenShi();
        }

        public static bool DevKeyLive(KeyCode key)
        {
            return DevKeyAllowed(key, DevHotkeys, DevClearHotkey);
        }

        public static bool DevKeyAllowed(KeyCode key, bool devOn, bool clearOn)
        {
            if (key == KeyCode.K)
                return clearOn;
            if (key == KeyCode.R || key == KeyCode.E)
                return true;
            switch (key)
            {
                case KeyCode.N:
                case KeyCode.F1:
                case KeyCode.F2:
                case KeyCode.F3:
                case KeyCode.F4:
                case KeyCode.Alpha1:
                case KeyCode.Alpha2:
                case KeyCode.Alpha3:
                case KeyCode.Alpha4:
                case KeyCode.Keypad1:
                case KeyCode.Keypad2:
                case KeyCode.Keypad3:
                case KeyCode.Keypad4:
                case KeyCode.F6:
                case KeyCode.F7:
                case KeyCode.F8:
                case KeyCode.F9:
                    return devOn;
            }

            return true;
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
            if (inside == null) return;
            if(inside.Kind==MazeNodeKind.Safe||inside.Kind==MazeNodeKind.Boss)
            {
                if(_stage!=StageId.S3||!AltarReady(StageId.S3)){_player.position=_lastGood;return;}
                if(inside.Kind==MazeNodeKind.Boss)BeginFinalBoss();else _shop?.EnsureStock();
                return;
            }
            StageId roomStage = StageForRoom(inside.Id);
            if (roomStage > _stage)
            {
                if (inside.Id != "S"+(int)roomStage+"_A" || !AltarReady(_stage)) { _player.position=_lastGood; return; }
                EnterStage(roomStage);
            }
            if (!inside.SpawnsEnemies) return;
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
                    RogueShooter.Audio.JianHaiAudio.Emit(wave>1?"jh_room_wave_next":"jh_room_seal",this);
                    _cadence = StartCoroutine(PortalThenSpawn(session, wave));
                    continue;
                }

                Debug.Log(step.Line);
                if (step.ShouldOpen)
                {
                    SetDoors(session.RoomId, false);
                    RogueShooter.Audio.JianHaiAudio.Emit("jh_room_clear",this);
                    OpenRoomProps(session.RoomId);
                    Flash("房间已清理 · 通道开启");
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
            DrawnComposition drawn = StageEnemyPool.DrawRoom(
                StageForRoom(node.Id), node.Id, node.PoolRoom, wave, Stage1MazeGen.DrawRng(_level.Parts[(int)StageForRoom(node.Id)-1].Seed, node.Id, wave));
            Vector3 center = new Vector3(node.Center.X, node.Center.Y, 0f);
            int n = drawn.Units != null ? drawn.Units.Length : 0;
            if (n < 1)
                n = 1;
            Vector3 player = _player != null ? _player.position : center;
            string[] kinds = new string[n];
            for (int i = 0; i < n; i++)
                kinds[i] = drawn.Units != null && i < drawn.Units.Length ? drawn.Units[i].KindId : EnemyKindIds.Normal;
            Vector3[] spots = Stage1Wave2.Spots(node, player, kinds, _maze);
            ClearPortals();
            for (int i = 0; i < n; i++)
            {
                Vector3 pos = spots != null ? spots[i] : SpawnCluster.Offset(center, i, n);
                GameObject fx = PortalFxStub.SpawnAt(
                    "PortalFx_" + node.Id + "_w" + wave + "_" + i, pos, transform);
                _portals.Add(fx);
                _world.Add(fx);
            }

            string show = PortalFxHook.PlayShow(session.RoomId, wave);
            Debug.Log(show);
            Flash("下一波即将出现");

            float hold = MazeRules.PlayPortalWaitSeconds;
            float t = 0f;
            while (true)
            {
                // Player movement during the warning cannot cause a hidden spawn
                // away from its portal. Keep safe markers and re-preview changed ones.
                if (!Stage1Wave2.IsSafe(node, _player != null ? _player.position : center, spots))
                {
                    spots = Stage1Wave2.Spots(node, _player != null ? _player.position : center, kinds, _maze, preferred:spots);
                    for (int i=0;i<_portals.Count && i<spots.Length;i++)
                        if (_portals[i]!=null) _portals[i].transform.position=spots[i]+Vector3.forward*.05f;
                    t=0f;
                }
                if (t >= hold || _skipPortalWait) break;
                t += Time.deltaTime;
                yield return null;
            }

            _skipPortalWait = false;
            Debug.Log(PortalFxHook.PlaySpawn(session.RoomId, wave, hold));
            ClearPortals();
            _portalWaiting = false;
            _cadence = null;
            SpawnDrawn(session, wave, drawn, center, spots);
        }

        void SpawnDrawn(CombatRoomSession session, int wave, DrawnComposition drawn, Vector3 center, Vector3[] spots)
        {
            Debug.Log(drawn.LogLine());
            Debug.Log("[StagePool] extra=+" + drawn.ExtraAdded
                      + " elite=" + drawn.EliteCount
                      + " n=" + drawn.UnitCount
                      + " tier=" + drawn.Tier
                      + " DRAFT_NOT_LOCKED");
            ClearLive();
            int n = drawn.Units != null ? drawn.Units.Length : 0;
            // PortalThenSpawn validates the final marker positions in this same
            // frame. Never secretly redraw positions after the visible warning.
            MazeNode node = _maze.Find(session.RoomId);
            string roomId = node != null ? node.Id : session.RoomId;
            for (int i = 0; i < n; i++)
            {
                Vector3 at = spots != null && i < spots.Length ? spots[i] : center;
                GameObject go = PlaceEnemy(roomId, wave, i, n, drawn.Units[i], at, true, spots != null);
                if (go != null)
                    _live.Add(go);
            }

            session.MarkSpawned(n);
            Flash("第" + wave + "波敌人来袭");
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
            // These are already visible enemies, not a new spawn. Activate the
            // existing instances in place; entrance clearance is planned before play.
            if (parked != null)
            {
                for (int i = 0; i < parked.Count; i++)
                {
                    GameObject go = parked[i];
                    if (go == null)
                        continue;
                    var parkedBody = go.GetComponent<StubEnemy>();
                    if (parkedBody != null && parkedBody.IsDead)
                    {
                        // The room consumed its wave: track the corpse so ClearLive
                        // owns it on rebuild. LiveEnemyCount already filters dead.
                        _live.Add(go);
                        continue;
                    }
                    MobFourStateAi ai = go.GetComponent<MobFourStateAi>();
                    if (ai != null) { ai.ActivateRoom(); ai.enabled = true; }
                    _live.Add(go);
                    n++;
                }
            }

            session.MarkSpawned(n);
            Debug.Log("[Stage1] door " + session.RoomId + " prespawn n=" + n + " hold=0");
            if (n <= 0)
                ApplySteps(session, session.NotifyKilled());
        }

        GameObject PlaceEnemy(string roomId, int wave, int index, int n, DrawnUnit u, Vector3 center, bool aiOn, bool placed)
        {
            if (_stubPrefab == null)
                return null;
            Vector3 pos = placed ? center : SpawnCluster.Offset(center, index, n);
            var spawnRoom=_maze.Find(roomId);
            if(spawnRoom!=null&&!_maze.OpenAt(pos.x,pos.y))
                pos=RogueShooter.Maze.MazeCollision.Slide(_maze,null,new Vector3(spawnRoom.Center.X,spawnRoom.Center.Y,0),pos,.4f);
            GameObject go = Instantiate(_stubPrefab, pos, Quaternion.identity, transform);
            go.name = StageForRoom(roomId) + "_" + roomId + "_w" + wave + "_" + index + "_" + u.KindId + (u.Elite ? "_ELITE" : "");
            string family = u.KindId == EnemyKindIds.Dog ? "dog"
                : EnemyKindCatalog.ForKind(u.KindId).RangedOrb ? "mage"
                : u.KindId == EnemyKindIds.Shield ? "shield"
                : "skel";
            go.AddComponent<Stage1IsoActor>().Bind(family);
            StubEnemy enemy = go.GetComponent<StubEnemy>();
            if (enemy == null)
                enemy = go.AddComponent<StubEnemy>();
            enemy.ConfigureKind(u.KindId, u.Hp, u.Elite);
            MobFourStateAi ai = go.GetComponent<MobFourStateAi>();
            if (ai == null)
                ai = go.AddComponent<MobFourStateAi>();
            ai.Configure(_lock, _player, StageForRoom(roomId), true, u.Atk, u.Elite);
            ai.BindMaze(_maze, roomId, _doors);
            if (aiOn) ai.ActivateRoom();
            if (go.GetComponent<RogueShooter.Art.MeleeAttackTelegraph>() == null) go.AddComponent<RogueShooter.Art.MeleeAttackTelegraph>();
            ai.enabled = aiOn;
            string capturedId = roomId;
            Experience?.RegisterEnemy(enemy, StageForRoom(capturedId));
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
            _altarPausedSeconds = 0f; _altarPauseAt = -1f;
            _startPausedSeconds = 0f; _startPauseAt = IsInStartRoom ? Time.timeSinceLevelLoad : -1f;
            _menuPausedSeconds = 0f; _menuPauseAt = -1f;
            _runEpoch = Time.timeSinceLevelLoad - Mathf.Max(0f, seconds);
            SyncPressureClock();
        }

        public void ProofHotkeyKill()
        {
            TryDevClear();
        }

        void OnEnemyDied(string roomId, StubEnemy enemy)
        {
            if (_ignoreClear) { Experience?.DiscardEnemy(enemy); return; }
            if (_suppressExperience) Experience?.DiscardEnemy(enemy);
            else Experience?.CreditEnemy(enemy);
            if (enemy != null && !enemy.NoKillReward) DropCoin(enemy);
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
                _build.AddKillGold(gain);
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
            bool previous = _suppressExperience;
            _suppressExperience = true;
            try { KillLiveWaveForProof(); }
            finally { _suppressExperience = previous; }
        }

        void KillLiveWaveForProof()
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

        bool AltarReady(StageId stage)
        {
            if (_level == null) return false;
            string id = _level.AltarId(stage);
            return _altars.TryGetValue(id,out var view) && view.IsLit && _takenRewards.Contains(id);
        }
        public bool ArriveConnector()
        {
            if (_connSettle || RunPause.RunSettled || PlayerDown() || RewardOpen() || (_active != null && _active.DoorsLocked)) return false;
            if (!AltarReady(_stage)) { Flash("先点燃祭坛并领取强化"); return false; }
            if (RunFlow.IsStage1Trial) { OpenConnectorSettle(); return true; }
            Flash("沿点亮的出口前进 · 进入下一阶段后无法返回"); return true;
        }
        void EnterStage(StageId stage)
        {
            if (stage <= _stage || stage != (StageId)((int)_stage+1) || !AltarReady(_stage)) return;
            RogueShooter.Audio.JianHaiAudio.Emit("jh_stage_enter",this);
            _stage = stage; _phaseLayout = _level.Layout(stage); _stageBannerUntil = Time.unscaledTime+3f;
            UpdateProgressionGates();
            Flash("进入第"+(int)stage+"阶段 · 回程已封闭");
            Debug.Log("[StageLevel] entered "+stage+" player/map/build retained");
        }
        void UpdateProgressionGates()
        {
            SetDoors("START", _tutorial != null && _tutorial.Active);
            if(_finalExit!=null)_finalExit.SetLocked(!AltarReady(StageId.S3));
            for(int i=0;i<_forwardGates.Count;i++)
            {
                StageId source=(StageId)(i+1);
                var forward=_forwardGates[i];
                bool combat=_sessions.TryGetValue(_level.Links[i].FromId,out var session) && session.DoorsLocked;
                if(forward!=null)forward.SetLocked(combat || _stage>source || !AltarReady(source));
                var back=_returnGates[i];
                bool entryFight=_sessions.TryGetValue(_level.Links[i].ToId,out var entry) && entry.DoorsLocked;
                if(back!=null)back.SetLocked(entryFight || (int)_stage>=i+2);
                var altar=AltarView(_level.Links[i].FromId);if(altar!=null)altar.SetExitReady(_stage==source && AltarReady(source));
            }
        }
        void UpdateStartClock()
        {
            // Menu pause keeps the hold folded (OpenMenuPause banked it); the clock
            // re-arms by itself once the menu closes and the player is still here.
            bool hold = IsInStartRoom && !MenuPaused;
            if (hold && _startPauseAt < 0f) _startPauseAt = Time.timeSinceLevelLoad;
            else if (!hold && _startPauseAt >= 0f)
            {
                _startPausedSeconds += Time.timeSinceLevelLoad-_startPauseAt;
                _startPauseAt = -1f;
            }
        }
        void UpdateAltarClock()
        {
            bool hold=false;
            if(_player!=null && !PlayerDown() && !_connSettle)
            {
                var room=RoomAt(_player.position.x,_player.position.y,Stage1Maze.WalkInset);
                hold=room!=null && room.Kind==MazeNodeKind.Altar && _altars.TryGetValue(room.Id,out var altar) && altar.IsLit;
            }
            hold = hold && !MenuPaused;
            if(hold && _altarPauseAt<0f)_altarPauseAt=Time.timeSinceLevelLoad;
            else if(!hold && _altarPauseAt>=0f){_altarPausedSeconds+=Time.timeSinceLevelLoad-_altarPauseAt;_altarPauseAt=-1f;}
        }
        bool NearAltar(MazeNode room)
        {
            var altar=AltarView(room.Id);return altar!=null && Vector2.Distance(_player.position,altar.transform.position)<=StageAltarView.InteractRadius;
        }
        void IgniteAltar(MazeNode room)
        {
            var altar=AltarView(room.Id);if(altar==null || altar.IsIgniting)return;
            altar.BeginIgnition(()=>
            {
                _player.GetComponent<HeroTorchRitual>()?.Finish();
                UpdateAltarClock();
                if(!PlayerDown() && RoomAt(_player.position.x,_player.position.y,.15f)==room && NearAltar(room) && !RewardOpen())OpenReward(room);
                Flash("祭坛已点燃 · 房内计时暂停");
                Debug.Log("[StageAltar] lit "+room.Id+" size="+SizeForStage(StageForRoom(room.Id)));
            });
            var ritual=_player.GetComponent<HeroTorchRitual>();if(ritual==null)ritual=_player.gameObject.AddComponent<HeroTorchRitual>();ritual.Begin(altar);
        }
        public string InteractionHint
        {
            get
            {
                if (_player == null || _maze == null || PlayerDown() || _connSettle || RewardOpen()) return "";
                var room=RoomAt(_player.position.x,_player.position.y,.2f);if(room==null)return "";
                if(room.Kind==MazeNodeKind.Connector)return "E 完成第一阶段试玩";
                if(room.Kind==MazeNodeKind.Safe && ReadyForFinalShop)return _shop!=null&&_shop.Near?"E 补给":"";
                if(RoomPhase(room.Id)!=CombatRoomPhase.Cleared)return "";
                if(room.Kind==MazeNodeKind.Altar)
                {
                    var view=AltarView(room.Id);var size=AltarRewardRoll.SizeLabel(SizeForStage(StageForRoom(room.Id)));
                    if(view!=null && view.IsIgniting)return "";
                    if(view==null || !view.IsLit)return NearAltar(room)?"按 E 点燃祭坛":"靠近祭坛，按 E 点燃";
                    if(!_takenRewards.Contains(room.Id))return NearAltar(room)?"E 强化":"";
                    return "";
                }
                if((room.Kind==MazeNodeKind.Chest || room.Kind==MazeNodeKind.LargeChest) && !_takenRewards.Contains(room.Id))return "E 开启宝箱";
                return "";
            }
        }

        public void OpenConnectorSettle()
        {
            if (_connSettle || RunPause.RunSettled)
                return;
            _settleSeconds = RunSeconds;
            _connSettle = true;
            _runWon = true;
            RogueShooter.Audio.JianHaiAudio.Settle("jh_trial_complete");
            RunPause.EnterSettled();
            ClearShots();
            EnsureBuild();
            int rewards = _build.OwnedRewardIds != null ? _build.OwnedRewardIds.Count : 0;
            int keep = EconomyGold.DeathInherit(_build.Gold);
            Debug.Log("[Stage1] conn settle gold=" + _build.Gold
                      + " keep=" + keep
                      + " build=" + _build.BuildCount
                      + " rewards=" + rewards
                      + " t=" + RunSeconds.ToString("0.0")
                      + " scale=" + Time.timeScale.ToString("0"));
        }

        void TryInteract()
        {
            if (_connSettle || RunPause.RunSettled)
                return;
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
                ArriveConnector();
                return;
            }

            if(n.Kind==MazeNodeKind.Safe){if(_shop!=null&&_shop.CanOpen)_shop.Open();else Flash("请靠近补给台");return;}
            if (n.Kind != MazeNodeKind.Chest && n.Kind != MazeNodeKind.LargeChest && n.Kind != MazeNodeKind.Altar)
            {
                Debug.Log("[S1Maze] interact room=" + n.Id + " kind=" + MazeRules.Label(n.Kind));
                Flash("这里不能开");
                return;
            }

            if (n.Kind == MazeNodeKind.Altar)
            {
                if (!NearAltar(n)) { Flash("请靠近祭坛"); return; }
                var altar = AltarView(n.Id);
                if (altar == null || altar.IsIgniting) return;
                if (!altar.IsLit) { IgniteAltar(n); return; }
            }

            if (_takenRewards.Contains(n.Id))
            {
                if (n.Kind == MazeNodeKind.Altar && (StageForRoom(n.Id) == StageId.S3 || RunFlow.IsStage1Trial))
                {
                    ArriveConnector();
                    return;
                }
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
            RewardCatalog.BindPoolContext(StageForRoom(room.Id),_build.OwnedRewardIds);
            _offerRoom = room.Id;
            _offerIsAltar = room.Kind == MazeNodeKind.Altar;
            RewardScreenView view = RewardView();
            if (_offerIsAltar)
            {
                AltarPick[] saved;
                if (!_altarRolls.TryGetValue(room.Id, out saved))
                {
                    saved = AltarRewardRoll.RollThree(SizeForStage(StageForRoom(room.Id)), _offerRng,_build.Regression);
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
                    RogueShooter.Audio.JianHaiAudio.Emit("jh_chest_open",this);
                    bool large = room.Kind == MazeNodeKind.LargeChest;
                    saved = AltarRewardRoll.RollThreeChest(large, _offerRng,_build.Regression);
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
            if(_offerIsShop){
                if(Input.GetKeyDown(KeyCode.Alpha4)||Input.GetKeyDown(KeyCode.Keypad4))pick=3;
                if(Input.GetKeyDown(KeyCode.Alpha5)||Input.GetKeyDown(KeyCode.Keypad5))pick=4;
                if(Input.GetKeyDown(KeyCode.Alpha6)||Input.GetKeyDown(KeyCode.Keypad6))pick=5;
            }
            if (pick < 0) return;
            ConfirmReward(pick);
        }

        void ConfirmReward(int pick)
        {
            if (!RewardOpen() || _rewardScreen == null || !_rewardScreen.ChoicesVisible)
                return;
            if(_offerIsShop){_shop?.TryBuy(pick);return;}
            EnsureBuild();
            AltarPick[] offers = _offerIsAltar ? _altarOffers : _chestOffers;
            if (offers == null || pick < 0 || pick >= offers.Length)
                return;
            AltarPick chosen = offers[pick];
            if(!_build.CanReceive(chosen.Id)){Flash("该强化已拥有或达到上限，请选其他强化");return;}
            if (!RewardCatalog.TryGet(chosen.Id, out RewardRow row))
                return;
            if (_offerIsAltar)
                _build.ConfirmAltarPick(SizeForStage(StageForRoom(_offerRoom)), chosen.Id);
            else
            {
                string rarity = row.Tier == RewardTier.High ? "高" : row.Tier == RewardTier.Mid ? "中" : "低";
                _build.GrantBuildPick(chosen.Id, rarity, 0, RunBuildState.ChestBuildDelta(_maze.Find(_offerRoom).Kind == MazeNodeKind.LargeChest));
                _build.AddGold(EconomyGold.ChestGold(_maze.Find(_offerRoom).Kind == MazeNodeKind.LargeChest, RunSeconds / 60f));
            }

            ApplyOwnedStats(row);
            if (!string.IsNullOrEmpty(_offerRoom))
                _takenRewards.Add(_offerRoom);
            var charge = _player != null ? _player.GetComponent<PlayerCharge>() : null;
            if (charge != null)
                charge.BindOwnedRewards(_build.OwnedRewardIds);
            RogueShooter.Audio.JianHaiAudio.Emit("jh_reward_commit",this);
            var confirmedAltar = _offerIsAltar ? AltarView(_offerRoom) : null;
            CloseReward();
            UpdateProgressionGates();
            if (confirmedAltar != null) confirmedAltar.SetExitReady(true);
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
            if (!RunPause.RunSettled)
                Time.timeScale = 1f;
            _offerRoom = null;
            _offerIsShop=false;
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
            // Room session owns the lock; the selected door view follows its transitions.
            for (int i = 0; i < _doors.Count; i++)
            {
                GameObject d = _doors[i];
                if (d == null)
                    continue;
                if (!d.name.StartsWith("Door_" + roomId + "_", StringComparison.Ordinal))
                    continue;
                var gate = d.GetComponent<Stage1DoorGate>();
                if (gate != null)
                    gate.SetLocked(locked);
            }
        }

        void ContainPlayer()
        {
            if (_player == null || _maze == null)
                return;
            Vector3 p = _player.position;
            if (_tutorial != null && _tutorial.Active)
            {
                MazeNode start = _maze.Find("START");
                if (start != null && !start.Contains(p.x,p.y,LockEdge))
                {
                    p.x = Mathf.Clamp(p.x,start.Center.X-start.Width*.5f+LockEdge,start.Center.X+start.Width*.5f-LockEdge);
                    p.y = Mathf.Clamp(p.y,start.Center.Y-start.Height*.5f+LockEdge,start.Center.Y+start.Height*.5f-LockEdge);
                    _player.position = p; _lastGood = p; return;
                }
            }
            if(_finalBoss!=null && _finalBoss.FightStarted && !_finalBoss.FightSettled)
            {var room=_level.BossRoom; p.x=Mathf.Clamp(p.x,room.Center.X-room.Width*.5f+LockEdge,room.Center.X+room.Width*.5f-LockEdge);p.y=Mathf.Clamp(p.y,room.Center.Y-room.Height*.5f+LockEdge,room.Center.Y+room.Height*.5f-LockEdge);_player.position=p;_lastGood=p;return;}
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

            // Transform-driven movement does not resolve Collider2D contacts. Keep
            // the sliding band solid until the room has unlocked and it is fully clear.
            for (int i = 0; i < _doors.Count; i++)
            {
                var gate = _doors[i] != null ? _doors[i].GetComponent<Stage1DoorGate>() : null;
                if (gate != null && MazeCollision.FirstBlockT(null, new[] { _doors[i] }, _lastGood, p, Stage1Coin.BodyRadius) <= 1f)
                {
                    _player.position = _lastGood;
                    return;
                }
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
                Flash("战斗中，请先清理房间");
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
                        { parked[i].SetActive(false); Destroy(parked[i]); }
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
                    arrows[i].CancelFlight();
            }

            MageOrbProjectile[] orbs = UnityEngine.Object.FindObjectsOfType<MageOrbProjectile>();
            for (int i = 0; i < orbs.Length; i++)
            {
                if (orbs[i] != null)
                {
                    orbs[i].gameObject.SetActive(false);
                    Destroy(orbs[i].gameObject);
                }
            }
        }

        void ClearLive()
        {
            for (int i = 0; i < _live.Count; i++)
            {
                if (_live[i] != null)
                    { _live[i].SetActive(false); Destroy(_live[i]); }
            }

            _live.Clear();
        }

        void ClearWorld(bool keepPlayer = false)
        {
            if (_cadence != null)
            {
                StopCoroutine(_cadence);
                _cadence = null;
            }
            _portalWaiting = false;
            _skipPortalWait = false;
            RewardStatHooks.ResetRoomEnter();
            ClearPrespawn();
            ClearShots();
            ClearPortals();
            ClearLive();
            _sessions.Clear();
            _altars.Clear(); _forwardGates.Clear(); _returnGates.Clear();
            _roomFloors.Clear();
            _chests.Clear();
            _doors.Clear();
            _takenRewards.Clear();
            _chestRolls.Clear();
            _altarRolls.Clear();
            _connSettle = false;
            _runWon=false; _shop=null; _finalBoss=null; _finalExit=null; RunPause.BossTransition=false; RunPause.RitualActing=false;
            _menuPausedSeconds = 0f; _menuPauseAt = -1f; RunPause.MenuOpen = false;
            _settleSeconds = -1f;
            _deathFrozen = false;
            RunPause.RunSettled = false;
            RunPause.ClearCombatEnd();
            CloseReward();
            _active = null; _tutorial = null;
            if (_player != null && !keepPlayer)
            {
                _player.gameObject.SetActive(false);
                Destroy(_player.gameObject);
                _player = null;
            }

            for (int i = 0; i < _world.Count; i++)
            {
                if (_world[i] != null && (!keepPlayer || _player == null || _world[i] != _player.gameObject))
                {
                    _world[i].SetActive(false);
                    Destroy(_world[i]);
                }
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
            if (_connSettle && !string.IsNullOrEmpty(_flash) && Time.unscaledTime <= _flashUntil)
            {
                var prompt = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 22,
                    alignment = TextAnchor.MiddleCenter
                };
                GUI.Label(new Rect(0f, Screen.height * 0.72f, Screen.width, 40f), _flash, prompt);
            }

            var hud=GetComponent<Stage1PlayHud>();
            if (!_connSettle || (hud!=null&&hud.isActiveAndEnabled))
                return;
            Stage1PlayHud.DrawSettlementPanel(this);
        }
    }
}
