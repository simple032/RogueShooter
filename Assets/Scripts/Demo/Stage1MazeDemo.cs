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
        CombatRoomSession _active;
        Vector3 _lastGood;
        bool _pass;
        bool _portalWaiting;
        bool _skipPortalWait;
        Coroutine _cadence;
        string _status = "loading…";
        string _flash = "";
        float _flashUntil;

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
            TryEnterRoom();
            SweepDead();
        }

        void BuildRun(int newSeed)
        {
            ClearWorld();
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
                      + "s (no clock lock)");
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
                      + " " + FullChargeKnockback.FormulaNote);
        }

        void BuildWorld()
        {
            Transform root = transform;
            Color floor = new Color(0.165f, 0.188f, 0.220f);
            for (int i = 0; i < _maze.Edges.Length; i++)
            {
                MazeEdge e = _maze.Edges[i];
                GameObject cor = DemoPrimitives.Corridor(
                    "Corridor_" + e.FromId + "_" + e.ToId,
                    new Vector3(e.From.X, e.From.Y, 0f),
                    new Vector3(e.To.X, e.To.Y, 0f),
                    e.Width, floor, 1, root);
                JianHaiBind.SetLayer(cor, JianHaiArtCatalog.LayerGround, 1);
                _world.Add(cor);
            }

            for (int i = 0; i < _maze.Nodes.Length; i++)
            {
                MazeNode n = _maze.Nodes[i];
                Color c = FloorColor(n.Kind);
                GameObject go = DemoPrimitives.Quad(
                    "Room_" + n.Id,
                    new Vector3(n.Center.X, n.Center.Y, 1.1f),
                    new Vector2(n.Width, n.Height),
                    c, 0, root);
                JianHaiBind.SetLayer(go, JianHaiArtCatalog.LayerGround, 0);
                _roomFloors[n.Id] = go;
                _world.Add(go);
                AddWalls(n, root);
                AddWorldLabel(root, n.Id + " " + MazeRules.Label(n.Kind),
                    new Vector3(n.Center.X, n.Center.Y + n.Height * 0.42f, 0f));
                AddProp(n, root);
                _sessions[n.Id] = new CombatRoomSession(n);
            }

            AddDoors(root);

            MazeNode start = _maze.Find("START");
            Vector3 startPos = start != null
                ? new Vector3(start.Center.X, start.Center.Y, 0f)
                : Vector3.zero;

            GameObject player = new GameObject("Player");
            player.transform.position = startPos;
            JianHaiBind.ApplyTo(player, JianHaiArtCatalog.PlayerIdle);
            player.AddComponent<PlayerMotor2D>().Configure(PlaySpeed());
            player.AddComponent<PlayerVitals>().Configure(EnemyDamageCatalog.PlayerMaxHpRef);
            player.AddComponent<PlayerStrike>().Configure(_lock != null ? _lock.strikeRange : 1.85f);
            player.AddComponent<PlayerCharge>();
            player.AddComponent<GuaranteedCritActive>();
            _player = player.transform;
            _lastGood = startPos;
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
            follow.SetTarget(player.transform);

            _stubPrefab = new GameObject("StubEnemyPrefab");
            _stubPrefab.transform.SetParent(root, false);
            _stubPrefab.SetActive(false);
            JianHaiBind.ApplyTo(_stubPrefab, JianHaiArtCatalog.EnemyE1Idle);
            _stubPrefab.AddComponent<StubEnemy>();
            _world.Add(_stubPrefab);
        }

        void AddWalls(MazeNode n, Transform root)
        {
            Color wall = new Color(0.07f, 0.08f, 0.09f);
            float t = 0.45f;
            float x = n.Center.X;
            float y = n.Center.Y;
            float hw = n.Width * 0.5f;
            float hh = n.Height * 0.5f;
            _world.Add(DemoPrimitives.Quad("WallN_" + n.Id, new Vector3(x, y + hh, 1f), new Vector2(n.Width + t, t), wall, 2, root));
            _world.Add(DemoPrimitives.Quad("WallS_" + n.Id, new Vector3(x, y - hh, 1f), new Vector2(n.Width + t, t), wall, 2, root));
            _world.Add(DemoPrimitives.Quad("WallW_" + n.Id, new Vector3(x - hw, y, 1f), new Vector2(t, n.Height), wall, 2, root));
            _world.Add(DemoPrimitives.Quad("WallE_" + n.Id, new Vector3(x + hw, y, 1f), new Vector2(t, n.Height), wall, 2, root));
        }

        void AddProp(MazeNode n, Transform root)
        {
            if (n.Kind != MazeNodeKind.Chest && n.Kind != MazeNodeKind.LargeChest
                && n.Kind != MazeNodeKind.Altar && n.Kind != MazeNodeKind.Connector)
                return;
            string hook = n.Kind == MazeNodeKind.Altar ? "A_S1"
                : n.Kind == MazeNodeKind.Connector ? "CONN_STUB"
                : "Chest_S1";
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
        }

        void AddDoors(Transform root)
        {
            for (int i = 0; i < _maze.Edges.Length; i++)
            {
                MazeEdge e = _maze.Edges[i];
                MazeNode a = _maze.Find(e.FromId);
                MazeNode b = _maze.Find(e.ToId);
                if (a == null || b == null)
                    continue;
                PlaceDoor(root, a, b);
                PlaceDoor(root, b, a);
            }
        }

        void PlaceDoor(Transform root, MazeNode room, MazeNode other)
        {
            Vector3 dir = new Vector3(other.Center.X - room.Center.X, other.Center.Y - room.Center.Y, 0f);
            if (dir.sqrMagnitude < 0.01f)
                return;
            dir.Normalize();
            float hw = room.Width * 0.5f;
            float hh = room.Height * 0.5f;
            Vector3 pos = new Vector3(room.Center.X, room.Center.Y, 0f) + dir * (Mathf.Abs(dir.x) > Mathf.Abs(dir.y) ? hw : hh);
            GameObject door = DemoPrimitives.Quad(
                "Door_" + room.Id + "_" + other.Id,
                pos, new Vector2(2.4f, 0.28f),
                new Color(0.72f, 0.16f, 0.16f), 6, root);
            if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
                door.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            door.SetActive(false);
            _doors.Add(door);
            _world.Add(door);
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
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
                Teleport("N1");
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
                Teleport("N2");
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
                TeleportFirst(MazeNodeKind.Altar);
            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
                TeleportFirstChest();
            if (Input.GetKeyDown(KeyCode.K))
                KillLiveWave();
            if (Input.GetKeyDown(KeyCode.F9))
                WriteEvidence();
            if (Input.GetKeyDown(KeyCode.E))
                TryInteract();
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
            Flash("PORTAL " + session.RoomId + " w" + wave + " " + MazeRules.PortalHoldSeconds.ToString("0.0") + "s");

            float hold = MazeRules.PortalHoldSeconds;
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
                DrawnUnit u = drawn.Units[i];
                Vector3 pos = SpawnCluster.Offset(center, i, n);
                GameObject go = Instantiate(_stubPrefab, pos, Quaternion.identity, transform);
                go.name = "S1_" + roomId + "_w" + wave + "_" + i + "_" + u.KindId + (u.Elite ? "_ELITE" : "");
                JianHaiBind.ApplyTo(go, JianHaiArtCatalog.EnemyE1Idle);
                var enemy = go.GetComponent<StubEnemy>();
                if (enemy == null)
                    enemy = go.AddComponent<StubEnemy>();
                enemy.ConfigureKind(u.KindId, u.Hp, u.Elite);
                var ai = go.GetComponent<MobFourStateAi>();
                if (ai == null)
                    ai = go.AddComponent<MobFourStateAi>();
                ai.Configure(_lock, _player, StageId.S1, true, u.Atk, u.Elite);
                string capturedId = roomId;
                enemy.Died += _ => OnEnemyDied(capturedId);
                go.SetActive(true);
                _live.Add(go);
            }

            session.MarkSpawned(n);
            Flash("wave " + wave + " " + drawn.CompId + " n=" + n);
            if (n <= 0)
                ApplySteps(session, session.NotifyKilled());
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

            Debug.Log("[S1Maze] interact room=" + n.Id + " kind=" + MazeRules.Label(n.Kind));
            Flash("interact " + n.Id);
        }

        void SetDoors(string roomId, bool locked)
        {
            for (int i = 0; i < _doors.Count; i++)
            {
                GameObject d = _doors[i];
                if (d == null)
                    continue;
                if (d.name.StartsWith("Door_" + roomId + "_", StringComparison.Ordinal))
                    d.SetActive(locked);
            }

            GameObject floor;
            if (_roomFloors.TryGetValue(roomId, out floor) && floor != null)
            {
                var sr = floor.GetComponent<SpriteRenderer>();
                MazeNode room = _maze.Find(roomId);
                if (sr != null && room != null)
                {
                    Color baseC = FloorColor(room.Kind);
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
            if (_active != null && _active.DoorsLocked)
            {
                MazeNode room = _maze.Find(_active.RoomId);
                if (room != null && !room.Contains(p.x, p.y, 0.35f))
                {
                    p.x = Mathf.Clamp(p.x, room.Center.X - room.Width * 0.5f + 0.45f, room.Center.X + room.Width * 0.5f - 0.45f);
                    p.y = Mathf.Clamp(p.y, room.Center.Y - room.Height * 0.5f + 0.45f, room.Center.Y + room.Height * 0.5f - 0.45f);
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
                n = _maze != null ? _maze.Find("LARGE") : null;
            if (n != null)
                Teleport(n.Id);
        }

        void WriteEvidence()
        {
            string path = Stage1MazeSampler.WriteTo(Stage1MazeSampler.DefaultPath(), seed);
            Debug.Log("[S1Maze] evidence " + path);
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
            ClearPortals();
            ClearLive();
            _sessions.Clear();
            _roomFloors.Clear();
            _doors.Clear();
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
            const int pad = 8;
            int w = 620;
            int h = 268;
            GUI.Box(new Rect(pad, pad, w, h), "");
            var style = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            var title = new GUIStyle(style) { fontSize = 15, fontStyle = FontStyle.Bold };
            var rich = new GUIStyle(style) { richText = true };
            GUI.Label(new Rect(pad + 8, pad + 4, w - 16, 22), "Stage-1 maze skeleton · Spec v0.5", title);
            GUI.Label(new Rect(pad + 8, pad + 28, w - 16, 54),
                "WASD · hold LMB/C charge · F strike · E interact · K skip-wave · N new seed · R same seed · F9 log\n" +
                "F1 START · F2 CONN stub · F3 ALTAR · F4 CHEST · 1/2 N1/N2\n" +
                "enter combat → lock → [PortalFx] show 1.0s → spawn → clear → open  |  Chest/Altar two waves, same cadence\n" +
                "full charge KB DRAFT · weak-spot ×1.5+stagger · shield-raised body root 0.5s · weak charge none",
                style);
            string graph = _maze != null ? Stage1MazeGen.FormatGraph(_maze) : "";
            GUI.Label(new Rect(pad + 8, pad + 84, w - 16, 36), graph, style);
            string pace = _maze != null
                ? "shortest " + _pace.ShortestTotalEstimate.ToString("0") + "s est · full "
                  + _pace.FullTotalEstimate.ToString("0") + "s est · move=" + PlaySpeed().ToString("0")
                  + " ortho=" + orthographicSize.ToString("0") + " (no clock lock)"
                : "";
            GUI.Label(new Rect(pad + 8, pad + 122, w - 16, 18), pace, style);
            string room = _active != null
                ? "active " + _active.RoomId + " " + _active.Phase + " wave=" + _active.CurrentWave
                  + "/" + _active.WavesTotal + " doors=" + (_active.DoorsLocked ? "LOCKED" : "OPEN")
                  + " live=" + _live.Count
                  + (_portalWaiting ? " PORTAL 1.0s" : "")
                : "walk a combat room to lock + portal + spawn";
            GUI.Label(new Rect(pad + 8, pad + 142, w - 16, 18), room, style);
            string status = _pass
                ? "<color=#88ff88>" + _status + "</color>"
                : "<color=#ffcc88>" + _status + "</color>";
            GUI.Label(new Rect(pad + 8, pad + 164, w - 16, 48), status, rich);
            if (!string.IsNullOrEmpty(_flash) && Time.unscaledTime < _flashUntil)
                GUI.Label(new Rect(pad + 8, pad + 214, w - 16, 18), _flash, style);
        }
    }
}
