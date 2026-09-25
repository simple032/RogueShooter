using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using RogueShooter.Ai;
using RogueShooter.Build;
using RogueShooter.Combat;
using RogueShooter.Iso;
using RogueShooter.Maze;
using RogueShooter.Player;
using RogueShooter.Vision;

namespace RogueShooter.Demo
{
    /// <summary>
    /// Play-mode probe (editor / batchmode only, started with <c>-s1probe stage1|scaffold</c>, see
    /// Editor/Stage1GenProbeMenu). Stage1: for iso off and on, seeds 1–12 — walk every room of the generator
    /// maze (DFS through the corridors), check reveal → lock (2.0u) order, doorway lift, lock strips, L5 spawn,
    /// clear, doors reopen, camera room clamp; wall ring vs the Rigidbody composite; corridor walls vs a
    /// pushed Rigidbody player, a mob box (CollisionWorld.TryMove), real arrows and mage orbs. Writes
    /// _probe_stage1.txt (+ screenshots) in the project root. Scaffold: re-runs ThreeRouteScaffold acceptance
    /// at several aspect ratios → _probe_scaffold.txt.
    /// </summary>
    public class Stage1GenFlowProbe : MonoBehaviour
    {
        const float Step = 0.5f;
        readonly StringBuilder _out = new StringBuilder();
        readonly List<string> _fails = new List<string>();
        string _mode;
        Stage1MazeDemo _demo;
        readonly Collider2D[] _hits = new Collider2D[8];
        ContactFilter2D _wallFilter;
        int _pathOverlaps;
        string _lastDespawn;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != "-s1probe")
                    continue;
                var go = new GameObject("Stage1GenFlowProbe");
                DontDestroyOnLoad(go);
                go.AddComponent<Stage1GenFlowProbe>()._mode = args[i + 1];
                return;
            }
        }
#endif

        IEnumerator Start()
        {
            _wallFilter = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = 1 << CollisionRules.UnityLayerWall };
            Application.logMessageReceived += OnLog;
            for (int i = 0; i < 10; i++)
                yield return null;
            bool ok = false;
            try
            {
                _out.AppendLine("# probe " + _mode + " " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " screen=" + Screen.width + "x" + Screen.height);
            }
            catch (Exception) { }

            if (_mode == "scaffold")
                yield return RunScaffold();
            else
                yield return RunStage1();
            ok = _fails.Count == 0;
            _out.AppendLine(ok ? "PROBE PASS" : "PROBE FAIL (" + _fails.Count + ")");
            for (int i = 0; i < _fails.Count && i < 40; i++)
                _out.AppendLine("FAIL " + _fails[i]);
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "_probe_" + _mode + ".txt");
            File.WriteAllText(path, _out.ToString());
            Debug.Log("[S1Probe] wrote " + path + " " + (ok ? "PASS" : "FAIL"));
            Application.logMessageReceived -= OnLog;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(ok ? 0 : 2);
#endif
        }

        void OnLog(string msg, string stack, LogType type)
        {
            if (msg.StartsWith("[Arrow] despawn", StringComparison.Ordinal))
                _lastDespawn = msg;
            if (type == LogType.Exception && _fails.Count < 200)
                _fails.Add("exception: " + msg);
        }

        void Fail(string msg)
        {
            _fails.Add(msg);
            if (_fails.Count <= 120)
                _out.AppendLine("  FAIL " + msg);
        }

        // ------------------------------------------------------------------ ThreeRouteScaffold

        IEnumerator RunScaffold()
        {
            var demo = FindObjectOfType<ThreeRouteScaffoldDemo>();
            for (int i = 0; demo != null && i < 60 && !demo.Passed; i++)
                yield return null;
            if (demo == null)
            {
                Fail("no ThreeRouteScaffoldDemo");
                yield break;
            }

            Camera cam = Camera.main;
            _out.AppendLine("initial aspect=" + CameraViewMath.ResolveAspect(cam).ToString("0.000") + " pass=" + demo.Passed);
            float[] aspects = { 16f / 9f, 16f / 10f, 4f / 3f, 21f / 9f, 5f / 4f, 3f / 2f, 32f / 9f };
            string[] names = { "16:9", "16:10", "4:3", "21:9", "5:4", "3:2", "32:9" };
            for (int i = 0; i < aspects.Length; i++)
            {
                cam.aspect = aspects[i];
                yield return null;
                string st = demo.DebugRerunAcceptance();
                bool pass = demo.Passed;
                int cut = st.IndexOf(" buildOk", StringComparison.Ordinal);
                _out.AppendLine(names[i] + " aspect=" + CameraViewMath.ResolveAspect(cam).ToString("0.000") + " " + (cut > 0 ? st.Substring(0, cut) : st));
                if (!pass)
                    Fail("scaffold " + names[i] + " " + st);
            }

            cam.ResetAspect();
        }

        // ------------------------------------------------------------------ Stage1

        IEnumerator RunStage1()
        {
            _demo = FindObjectOfType<Stage1MazeDemo>();
            for (int i = 0; _demo != null && _demo.Maze == null && i < 120; i++)
                yield return null;
            if (_demo == null || _demo.Maze == null)
            {
                Fail("no Stage1MazeDemo");
                yield break;
            }

            if (Camera.main != null)
                Camera.main.aspect = 16f / 9f; // design aspect (batchmode screen is 640x480)
            for (int iso = 0; iso < 2; iso++)
            {
                IsoConfig.Enabled = iso == 1;
                for (int seed = 1; seed <= 12; seed++)
                    yield return RunSeed(seed);
            }

            IsoConfig.Enabled = false;
            yield return Screenshots();
        }

        IEnumerator RunSeed(int seed)
        {
            int failsBefore = _fails.Count;
            _demo.DebugBuild(seed);
            yield return null;
            yield return null;
            Physics2D.SyncTransforms();
            Stage1Maze maze = _demo.Maze;
            Stage1MazeRaster r = _demo.GenWorld.Raster;
            string tag = "iso=" + (IsoConfig.Enabled ? 1 : 0) + " seed=" + seed;
            _out.AppendLine("## " + tag + " " + maze.TemplateId + " " + maze.Signature + " ortho=" + Camera.main.orthographicSize.ToString("0.00"));
            _out.AppendLine("  " + _demo.GenWorld.PerfLine());
            string ring = r.CheckRing();
            if (ring != null)
                Fail(tag + " " + ring);
            if (_demo.GenWorld.MissingTiles > 0)
                Fail(tag + " missing tiles " + _demo.GenWorld.MissingTiles);
            CheckPhysicsRing(tag, r);

            // Corridor-wall blocking is logic/physics space (iso-independent): once per seed, iso off.
            if (!IsoConfig.Enabled)
                yield return CorridorBlocks(tag, maze);

            // DFS walk through every room.
            _pathOverlaps = 0;
            int combat = 0, cleared = 0, locks = 0, spawned = 0, lifts = 0, clampOk = 0, visited = 0;
            var seen = new HashSet<string>();
            var stack = new List<string>();
            MazeNode start = maze.Find("START");
            _demo.DebugMovePlayer(new Vector3(start.Center.X, start.Center.Y, 0f));
            yield return null;
            seen.Add(start.Id);
            visited++;
            stack.Add(start.Id);
            var result = new int[7];
            while (stack.Count > 0)
            {
                MazeNode cur = maze.Find(stack[stack.Count - 1]);
                MazeNode next = null;
                for (int i = 0; cur.NeighborIds != null && i < cur.NeighborIds.Length; i++)
                {
                    if (!seen.Contains(cur.NeighborIds[i]))
                    {
                        next = maze.Find(cur.NeighborIds[i]);
                        break;
                    }
                }

                if (next == null)
                {
                    stack.RemoveAt(stack.Count - 1);
                    if (stack.Count > 0)
                        yield return WalkRooms(cur, maze.Find(stack[stack.Count - 1]), tag, result, false);
                    continue;
                }

                seen.Add(next.Id);
                visited++;
                Array.Clear(result, 0, result.Length);
                yield return WalkRooms(cur, next, tag, result, true);
                combat += result[0];
                locks += result[1];
                spawned += result[2];
                cleared += result[3];
                lifts += result[4];
                clampOk += result[5];
                stack.Add(next.Id);
            }

            if (seed == 1)
                yield return GlideCheck(tag, maze);

            if (visited != maze.Nodes.Length)
                Fail(tag + " visited " + visited + "/" + maze.Nodes.Length);
            if (_pathOverlaps > 0)
                Fail(tag + " player path overlapped walls x" + _pathOverlaps);
            if (locks != combat || spawned != combat || cleared != combat)
                Fail(tag + " combat=" + combat + " locks=" + locks + " spawned=" + spawned + " cleared=" + cleared);
            _out.AppendLine("  flow rooms=" + visited + "/" + maze.Nodes.Length + " combat=" + combat + " lock=" + locks + " spawn=" + spawned
                            + " clear+open=" + cleared + " doorwayLift=" + lifts + " camClamp=" + clampOk + " pathWallHits=" + _pathOverlaps
                            + " L5 placed=" + _demo.L5Placed + " fallback=" + _demo.L5Fallbacks
                            + " minDist=" + (_demo.L5MinDist < 1e9f ? _demo.L5MinDist.ToString("0.0") : "-")
                            + " minOutsideQuad=" + (_demo.L5MinOutside < 1e9f ? _demo.L5MinOutside.ToString("0.0") : "-")
                            + (_fails.Count == failsBefore ? " OK" : " FAIL"));
        }

        /// <summary>Every ring wall cell centre is inside the composite; no floor cell centre (player circle) touches it.</summary>
        void CheckPhysicsRing(string tag, Stage1MazeRaster r)
        {
            int wallMiss = 0, floorHit = 0;
            for (int y = r.MinY; y < r.MaxY; y++)
            {
                for (int x = r.MinX; x < r.MaxX; x++)
                {
                    MazeCell c = r.Get(x, y);
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    if (c == MazeCell.Wall)
                    {
                        if (Physics2D.OverlapPoint(p, _wallFilter, _hits) == 0)
                        {
                            if (wallMiss == 0)
                                _out.AppendLine("  first wallMiss cell " + x + "," + y + " floorN8=" + r.FloorNeighbour8(x, y) + " tile=" + r.WallTileId(x, y) + " rect=" + RectOf(r, x, y));
                            wallMiss++;
                        }
                    }
                    else if (c != MazeCell.Empty)
                    {
                        if (Physics2D.OverlapCircle(p, Stage1GenWorld.PlayerRadius, _wallFilter, _hits) > 0)
                            floorHit++;
                    }
                }
            }

            if (wallMiss > 0 || floorHit > 0)
                Fail(tag + " physics ring wallMiss=" + wallMiss + " floorBlocked=" + floorHit);
            _out.AppendLine("  physicsRing walls=" + r.WallCount + " wallMiss=" + wallMiss + " floorBlocked=" + floorHit
                            + " compositePaths=" + _demo.GenWorld.LastCompositePaths);
        }

        static string RectOf(Stage1MazeRaster r, int x, int y)
        {
            for (int i = 0; i < r.WallRects.Count; i++)
            {
                CellRect c = r.WallRects[i];
                if (x >= c.X && x < c.X + c.W && y >= c.Y && y < c.Y + c.H)
                    return c.X + "," + c.Y + " " + c.W + "x" + c.H;
            }

            return "-";
        }

        /// <summary>Corridor walls stop a pushed Rigidbody player, a mob box, arrows and mage orbs (both sides, every corridor).</summary>
        IEnumerator CorridorBlocks(string tag, Stage1Maze maze)
        {
            var motor = _demo.PlayerTransform.GetComponent<PlayerMotor2D>();
            var dodge = _demo.PlayerTransform.GetComponent<PlayerDodge>();
            Rigidbody2D body = _demo.PlayerBody;
            float halfC = MazeRules.CorridorWidth * 0.5f;
            int mobOk = 0, arrowOk = 0, orbOk = 0, bodyOk = 0, n = 0;
            var dummy = new GameObject("ProbeMob").transform;
            for (int e = 0; e < maze.Edges.Length; e++)
            {
                MazeEdge edge = maze.Edges[e];
                MazeVec2 a = edge.Points[0], b = edge.Points[edge.Points.Length - 1];
                bool horiz = Math.Abs(b.X - a.X) >= Math.Abs(b.Y - a.Y);
                var mid = new Vector3((a.X + b.X) * 0.5f, (a.Y + b.Y) * 0.5f, 0f);
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    n++;
                    var dir = horiz ? new Vector3(0f, sgn, 0f) : new Vector3(sgn, 0f, 0f);
                    string where = tag + " corridor " + edge.FromId + "-" + edge.ToId + " dir " + dir;

                    // Mob box (MobFourStateAi.Shift → CollisionWorld.TryMove).
                    dummy.position = mid;
                    for (int k = 0; k < 30; k++)
                        CollisionWorld.TryMove(dummy, CollisionRules.MobHalfX, CollisionRules.MobHalfY, dir.x * 0.3f, dir.y * 0.3f);
                    float mobOff = Vector3.Dot(dummy.position - mid, dir);
                    if (mobOff <= halfC - CollisionRules.MobHalfX + 0.02f) mobOk++;
                    else Fail(where + " mob off=" + mobOff.ToString("0.00"));

                    // Real arrow.
                    _lastDespawn = null;
                    ArrowProjectile arrow = ArrowProjectile.Spawn(mid, dir, 1f, ChargeShotKind.Weak, 0f, null, null, 20f);
                    Vector3 last = mid;
                    float until = Time.realtimeSinceStartup + 3f;
                    while (arrow != null && arrow.Alive && Time.realtimeSinceStartup < until)
                    {
                        last = arrow.transform.position;
                        yield return null;
                        if (arrow != null)
                            last = arrow.transform.position;
                    }
                    float arrowOff = Vector3.Dot(last - mid, dir);
                    if (_lastDespawn != null && _lastDespawn.Contains("despawn wall") && arrowOff <= halfC + 0.05f) arrowOk++;
                    else Fail(where + " arrow off=" + arrowOff.ToString("0.00") + " " + _lastDespawn);

                    // Real mage orb.
                    string orbReason = null;
                    MageOrbProjectile orb = MageOrbProjectile.SpawnVisual(mid, Color.white);
                    orb.Launch(mid, dir, 8f, 20f, 1f, null, (o, why) => orbReason = why);
                    last = mid;
                    until = Time.realtimeSinceStartup + 3f;
                    while (orb != null && orbReason == null && Time.realtimeSinceStartup < until)
                    {
                        last = orb.transform.position;
                        yield return null;
                        if (orb != null)
                            last = orb.transform.position;
                    }
                    float orbOff = Vector3.Dot(last - mid, dir);
                    if (orbReason == "wall" && orbOff <= halfC + 0.05f) orbOk++;
                    else Fail(where + " orb off=" + orbOff.ToString("0.00") + " reason=" + orbReason);

                    // Rigidbody player pushed into the corridor wall.
                    if (motor != null) motor.enabled = false;
                    if (dodge != null) dodge.enabled = false;
                    _demo.DebugMovePlayer(mid);
                    for (int k = 0; k < 25; k++)
                    {
                        body.velocity = new Vector2(dir.x, dir.y) * 12f;
                        yield return new WaitForFixedUpdate();
                    }
                    body.velocity = Vector2.zero;
                    float bodyOff = Vector3.Dot((Vector3)body.position + new Vector3(0f, Stage1GenWorld.PlayerFootOffset, 0f) - mid, dir);
                    if (bodyOff <= halfC - Stage1GenWorld.PlayerRadius + 0.05f) bodyOk++;
                    else Fail(where + " rigidbody off=" + bodyOff.ToString("0.00"));
                    if (motor != null) motor.enabled = true;
                    if (dodge != null) dodge.enabled = true;
                }
            }

            Destroy(dummy.gameObject);
            _out.AppendLine("  corridorWalls probes=" + n + " mob=" + mobOk + " arrow=" + arrowOk + " orb=" + orbOk + " rigidbody=" + bodyOk);
            MazeNode start = maze.Find("START");
            _demo.DebugMovePlayer(new Vector3(start.Center.X, start.Center.Y, 0f));
            yield return null;
        }

        /// <summary>Walk room a → room b along the door axis; on first entry of a combat room: lock → clear → reopen.</summary>
        IEnumerator WalkRooms(MazeNode a, MazeNode b, string tag, int[] result, bool firstEntry)
        {
            int side;
            float along;
            MazeCollisionBuilder.DoorOnWall(a, b, out side, out along);
            bool horiz = side == MazeCollisionBuilder.SideE || side == MazeCollisionBuilder.SideW;
            float sgn = side == MazeCollisionBuilder.SideE || side == MazeCollisionBuilder.SideN ? 1f : -1f;
            var pts = new List<Vector3>();
            if (horiz)
            {
                pts.Add(new Vector3(a.Center.X, along, 0f));
                pts.Add(new Vector3(b.Center.X - sgn * b.Width * 0.5f + sgn * 4f, along, 0f));
            }
            else
            {
                pts.Add(new Vector3(along, a.Center.Y, 0f));
                pts.Add(new Vector3(along, b.Center.Y - sgn * b.Height * 0.5f + sgn * 4f, 0f));
            }

            pts.Add(new Vector3(b.Center.X, b.Center.Y, 0f));
            string where = tag + " " + a.Id + "→" + b.Id;
            CombatRoomPhase phase0;
            bool isCombat = _demo.HasSession(b.Id, out phase0) && b.SpawnsEnemies && phase0 == CombatRoomPhase.Vacant && firstEntry;
            bool wasCovered = _demo.IsRoomCovered(b.Id);
            int revealStep = -1, lockStep = -1, stepNo = 0;
            float lockInset = -1f;
            bool lifted = false;
            for (int p = 0; p < pts.Count; p++)
            {
                Vector3 target = pts[p];
                while (true)
                {
                    Vector3 cur = _demo.PlayerTransform.position;
                    Vector3 d = target - cur;
                    d.z = 0f;
                    if (d.magnitude < 0.01f)
                        break;
                    Vector3 nextPos = cur + Vector3.ClampMagnitude(d, Step);
                    _demo.DebugMovePlayer(nextPos);
                    if (_demo.Follow != null)
                        _demo.Follow.SnapNow(); // probe steps 0.5u/frame; the clamp glide is paced for real move speed
                    Physics2D.SyncTransforms();
                    if (Physics2D.OverlapCircle((Vector2)nextPos + new Vector2(0f, Stage1GenWorld.PlayerFootOffset), Stage1GenWorld.PlayerRadius - 0.01f, _wallFilter, _hits) > 0)
                        _pathOverlaps++;
                    yield return null;
                    stepNo++;
                    if (_demo.PlayerLiftedOverCover)
                        lifted = true;
                    if (revealStep < 0 && !_demo.IsRoomCovered(b.Id))
                        revealStep = stepNo;
                    if (isCombat && lockStep < 0 && _demo.DoorsUp(b.Id))
                    {
                        lockStep = stepNo;
                        Vector3 pl = _demo.PlayerTransform.position;
                        lockInset = horiz ? b.Width * 0.5f - Mathf.Abs(pl.x - b.Center.X) : b.Height * 0.5f - Mathf.Abs(pl.y - b.Center.Y);
                    }
                }
            }

            if (wasCovered && revealStep < 0)
                Fail(where + " room never revealed");
            if (wasCovered && firstEntry && !lifted)
                Fail(where + " player never lifted over the cover in the doorway (order " + Stage1MazeDemo.PlayerDoorwayOrder + ")");
            if (lifted) result[4]++;

            if (isCombat)
            {
                result[0]++;
                if (lockStep < 0)
                    Fail(where + " doors never locked");
                else
                {
                    result[1]++;
                    if (revealStep < 0 || revealStep > lockStep)
                        Fail(where + " lock before reveal");
                    if (lockInset < Stage1MazeDemo.LockTriggerInset - 0.01f || lockInset > Stage1MazeDemo.LockTriggerInset + Step + 0.01f)
                        Fail(where + " lock inset " + lockInset.ToString("0.00"));
                }

                if (_demo.DoorCount(b.Id) != (b.NeighborIds != null ? b.NeighborIds.Length : 0))
                    Fail(where + " door count " + _demo.DoorCount(b.Id));

                // Camera clamp inside the 52×40 room (view rect inside the (projected) room box).
                yield return CameraCheck(where, b, result);

                bool sawSpawn = false;
                int placed0 = _demo.L5Placed;
                CombatRoomPhase ph = CombatRoomPhase.Vacant;
                for (int f = 0; f < 1500; f++)
                {
                    _demo.HasSession(b.Id, out ph);
                    if (ph == CombatRoomPhase.Cleared)
                        break;
                    IReadOnlyList<GameObject> live = _demo.LiveMobs;
                    for (int i = 0; i < live.Count; i++)
                    {
                        if (live[i] == null)
                            continue;
                        var se = live[i].GetComponent<StubEnemy>();
                        if (se != null && se.IsDead)
                            continue;
                        sawSpawn = true;
                        Vector3 mp = live[i].transform.position;
                        if (!b.Contains(mp.x, mp.y, 0f))
                            Fail(where + " mob spawned outside the room at " + mp);
                    }

                    if (_demo.PortalWaiting || _demo.LiveCount > 0 || _demo.PendingSpawnCount > 0)
                        _demo.DebugKillWave();
                    yield return null;
                }

                if (sawSpawn || _demo.L5Placed > placed0)
                    result[2]++;
                else
                    Fail(where + " no spawn");
                if (ph != CombatRoomPhase.Cleared)
                    Fail(where + " not cleared (" + ph + ")");
                else
                {
                    yield return null;
                    yield return null;
                    if (_demo.DoorsUp(b.Id))
                        Fail(where + " doors still locked after clear");
                    else
                        result[3]++;
                }

                var dir = _demo.GetComponent<ChestAltarDirector>();
                for (int f = 0; f < 5; f++)
                {
                    if (dir != null && dir.Offering)
                        dir.NotifyUiCancel();
                    yield return null;
                }

                Time.timeScale = 1f;
            }
        }

        /// <summary>Teleport START → N1 centre: the clamp switch glides (no jump) and lands on the clamped goal.</summary>
        IEnumerator GlideCheck(string tag, Stage1Maze maze)
        {
            CameraFollow2D follow = _demo.Follow;
            MazeNode start = maze.Find("START"), n1 = maze.Find("N1");
            _demo.DebugMovePlayer(new Vector3(start.Center.X, start.Center.Y, 0f));
            follow.SnapNow();
            yield return null;
            _demo.DebugMovePlayer(new Vector3(n1.Center.X, n1.Center.Y, 0f));
            yield return null;
            bool started = follow.Gliding;
            float t0 = Time.realtimeSinceStartup;
            while (follow.Gliding && Time.realtimeSinceStartup < t0 + 5f)
                yield return null;
            float took = Time.realtimeSinceStartup - t0;
            float err = Vector3.Distance(follow.transform.position, follow.Goal());
            _out.AppendLine("  camGlide started=" + started + " took=" + took.ToString("0.00") + "s endErr=" + err.ToString("0.000") + " clamped=" + follow.Clamped);
            if (!started || follow.Gliding || err > 0.02f || !follow.Clamped)
                Fail(tag + " camera glide started=" + started + " err=" + err);
            _demo.DebugMovePlayer(new Vector3(start.Center.X, start.Center.Y, 0f));
            follow.SnapNow();
            yield return null;
        }

        IEnumerator CameraCheck(string where, MazeNode room, int[] result)
        {
            CameraFollow2D follow = _demo.Follow;
            float until = Time.realtimeSinceStartup + 5f;
            while (follow != null && follow.Gliding && Time.realtimeSinceStartup < until)
                yield return null;
            yield return null;
            Camera cam = Camera.main;
            if (follow == null || cam == null || !follow.Clamped)
            {
                Fail(where + " camera not clamped in room");
                yield break;
            }

            Rect view = CameraViewMath.GetOrthographicWorldRect(cam.transform.position, cam.orthographicSize, CameraViewMath.ResolveAspect(cam));
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                var c = new Vector3(room.Center.X + ((i & 1) == 0 ? -0.5f : 0.5f) * room.Width, room.Center.Y + ((i & 2) == 0 ? -0.5f : 0.5f) * room.Height, 0f);
                Vector3 v = ViewSpace.LogicToCamera(c);
                minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x);
                minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y);
            }

            const float eps = 0.02f;
            bool fitsX = maxX - minX >= view.width, fitsY = maxY - minY >= view.height;
            bool ok = (!fitsX || (view.xMin >= minX - eps && view.xMax <= maxX + eps))
                      && (!fitsY || (view.yMin >= minY - eps && view.yMax <= maxY + eps));
            if (ok) result[5]++;
            else Fail(where + " camera view " + view + " leaves room box " + minX + ".." + maxX + "," + minY + ".." + maxY);
        }

        // ------------------------------------------------------------------ screenshots

        IEnumerator Screenshots()
        {
            _demo.DebugBuild(1);
            yield return null;
            Stage1Maze maze = _demo.Maze;
            MazeNode start = maze.Find("START"), n1 = maze.Find("N1");
            _demo.DebugMovePlayer(new Vector3(start.Center.X, start.Center.Y, 0f));
            yield return null;
            // Walk in without clearing: portal + first wave visible.
            int side;
            float along;
            MazeCollisionBuilder.DoorOnWall(start, n1, out side, out along);
            bool horiz = side == MazeCollisionBuilder.SideE || side == MazeCollisionBuilder.SideW;
            Vector3 target = horiz ? new Vector3(n1.Center.X - Mathf.Sign(n1.Center.X - start.Center.X) * 12f, along, 0f)
                                   : new Vector3(along, n1.Center.Y - Mathf.Sign(n1.Center.Y - start.Center.Y) * 10f, 0f);
            Vector3 p0 = horiz ? new Vector3(start.Center.X, along, 0f) : new Vector3(along, start.Center.Y, 0f);
            foreach (Vector3 t in new[] { p0, target })
            {
                while ((t - _demo.PlayerTransform.position).magnitude > 0.01f)
                {
                    _demo.DebugMovePlayer(_demo.PlayerTransform.position + Vector3.ClampMagnitude(t - _demo.PlayerTransform.position, Step));
                    _demo.Follow.SnapNow();
                    yield return null;
                }
            }

            float wait = 0f;
            while (wait < 1.6f)
            {
                wait += Time.deltaTime;
                yield return null;
            }

            _demo.Follow.SnapNow();
            yield return null;
            Camera cam = Camera.main;
            string dir = Directory.GetParent(Application.dataPath).FullName;
            Capture(cam, Path.Combine(dir, "_probe_shot_ortho_play.png"));
            // Whole 52×40 room: same camera, larger ortho, centred on the room.
            var follow = _demo.Follow;
            follow.enabled = false;
            float ortho0 = cam.orthographicSize;
            Vector3 pos0 = cam.transform.position;
            cam.orthographicSize = n1.Height * 0.5f + 3f;
            cam.transform.position = new Vector3(n1.Center.X, n1.Center.Y, pos0.z);
            Capture(cam, Path.Combine(dir, "_probe_shot_ortho_room.png"));
            cam.orthographicSize = ortho0;
            cam.transform.position = pos0;
            follow.enabled = true;
            _out.AppendLine("screenshots _probe_shot_ortho_play.png (ortho " + ortho0.ToString("0.00") + ") _probe_shot_ortho_room.png (room N1 52x40)");
        }

        static void Capture(Camera cam, string path)
        {
            const int w = 1920, h = 1080;
            var rt = new RenderTexture(w, h, 24);
            RenderTexture prevT = cam.targetTexture, prevA = RenderTexture.active;
            float aspect0 = cam.aspect;
            cam.targetTexture = rt;
            cam.aspect = (float)w / h;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            cam.targetTexture = prevT;
            RenderTexture.active = prevA;
            cam.aspect = aspect0;
            cam.ResetAspect();
            Destroy(tex);
            rt.Release();
        }
    }
}
