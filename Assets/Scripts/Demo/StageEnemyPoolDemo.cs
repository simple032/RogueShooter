using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using RogueShooter.Ai;
using RogueShooter.Art;
using RogueShooter.Balance;
using RogueShooter.Player;
using RogueShooter.Spawning;
using RogueShooter.Vision;

namespace RogueShooter.Demo
{
    /// <summary>
    /// Hotkeys: F5/F6/F7 stage, [ ] room kind, F8 spawn composition, F9 write Logs CSV.
    /// Console contract: [StagePool] S? room=? comp=… kinds=…
    /// </summary>
    public class StageEnemyPoolDemo : MonoBehaviour
    {
        BalanceLockData _lock;
        Transform _player;
        GameObject _stubPrefab;
        Transform _root;
        StageId _stage = StageId.S1;
        CombatRoomKind _room = CombatRoomKind.Normal;
        DrawnComposition _last;
        readonly List<GameObject> _spawned = new List<GameObject>();
        System.Random _rng = new System.Random(17);

        public StageId Stage => _stage;
        public CombatRoomKind Room => _room;
        public DrawnComposition Last => _last;
        public string LastLog => _last.CompId != null ? _last.LogLine() : "";

        public void Bind(BalanceLockData data, Transform player, GameObject stubPrefab, Transform root)
        {
            _lock = data;
            _player = player;
            _stubPrefab = stubPrefab;
            _root = root;
            string dir = EnemyPoolDraft.ResolveDirectory();
            if (!string.IsNullOrEmpty(Application.streamingAssetsPath))
            {
                string sa = Path.Combine(Application.streamingAssetsPath, EnemyPoolDraft.FolderName);
                if (Directory.Exists(sa))
                    dir = sa;
            }

            if (!EnemyPoolDraft.TryLoadFromDirectory(dir, out string err))
                Debug.LogError("[StagePool] draft load FAIL " + err);
            else
                Debug.Log("[StagePool] loaded DRAFT from " + EnemyPoolDraft.Source);
            _rng = new System.Random(Environment.TickCount);
            DrawAndLog();
        }

        public void HandleHotkeys()
        {
            if (Input.GetKeyDown(KeyCode.F5))
                SetStage(StageId.S1);
            if (Input.GetKeyDown(KeyCode.F6))
                SetStage(StageId.S2);
            if (Input.GetKeyDown(KeyCode.F7))
                SetStage(StageId.S3);
            if (Input.GetKeyDown(KeyCode.LeftBracket) || Input.GetKeyDown(KeyCode.Comma))
                SetRoom(PrevRoom(_room));
            if (Input.GetKeyDown(KeyCode.RightBracket) || Input.GetKeyDown(KeyCode.Period))
                SetRoom(CombatRoomKindUtil.Next(_room));
            if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0))
                SetRoom(CombatRoomKindUtil.Next(_room));
            if (Input.GetKeyDown(KeyCode.F8))
                SpawnLastOrDraw();
            if (Input.GetKeyDown(KeyCode.F9))
                WriteEvidence();
        }

        public void SetStage(StageId stage)
        {
            _stage = stage;
            DrawAndLog();
        }

        public void SetRoom(CombatRoomKind room)
        {
            _room = room;
            DrawAndLog();
        }

        public DrawnComposition DrawAndLog()
        {
            _last = StageEnemyPool.DrawComposition(_stage, _room, _rng);
            Debug.Log(_last.LogLine());
            Debug.Log("[StagePool] pool=" + StageEnemyPool.FormatKinds(_stage)
                      + " extra=+" + _last.ExtraAdded
                      + " elite=" + _last.EliteCount
                      + " n=" + _last.UnitCount
                      + " DRAFT_NOT_LOCKED");
            if (_stage == StageId.S2)
            {
                Debug.Log("[StagePool] S2 cult mage=E3 same-as-S1 orb walk×2 camW×0.7 " +
                          "next-after-despawn windup+bang ttk≈2s");
            }
            return _last;
        }

        public void SpawnLastOrDraw()
        {
            if (string.IsNullOrEmpty(_last.CompId))
                DrawAndLog();
            Spawn(_last);
        }

        public string WriteEvidence()
        {
            string path = EnemyStagePoolSampler.WriteDefault();
            string err = StageEnemyPoolChecks.Run();
            if (err == null)
                Debug.Log("[StagePool] " + StageEnemyPoolChecks.FormatPass() + " csv=" + path);
            else
                Debug.LogError("[StagePool] ACCEPTANCE FAIL " + err + " csv=" + path);
            return path;
        }

        public string HudLine()
        {
            return "StagePool " + StageIdUtil.Label(_stage)
                + " room=" + CombatRoomKindUtil.Label(_room)
                + " F5/F6/F7 stage  [/] room  F8 spawn  F9 CSV  "
                + (_last.CompId ?? "");
        }

        void Spawn(DrawnComposition drawn)
        {
            ClearSpawned();
            if (_stubPrefab == null || _player == null || drawn.Units == null)
                return;
            Vector3 center = _player.position + Vector3.right * 2.6f;
            for (int i = 0; i < drawn.Units.Length; i++)
            {
                DrawnUnit u = drawn.Units[i];
                Vector3 pos = SpawnCluster.Offset(center, i, drawn.Units.Length);
                GameObject go = Instantiate(_stubPrefab, pos, Quaternion.identity, _root);
                go.name = "Pool_" + drawn.CompId + "_" + i + "_" + u.KindId + (u.Elite ? "_ELITE" : "");
                JianHaiBind.ApplyTo(go, JianHaiArtCatalog.EnemyE1Idle);
                var enemy = go.GetComponent<StubEnemy>();
                if (enemy == null)
                    enemy = go.AddComponent<StubEnemy>();
                enemy.ConfigureKind(u.KindId, u.Hp, u.Elite);
                var ai = go.GetComponent<MobFourStateAi>();
                if (ai == null)
                    ai = go.AddComponent<MobFourStateAi>();
                ai.Configure(_lock, _player, drawn.Stage, true, u.Atk, u.Elite);
                go.SetActive(true);
                _spawned.Add(go);
            }

            Debug.Log("[StagePool] spawned " + drawn.LogLine() + " units=" + drawn.UnitCount);
        }

        void ClearSpawned()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                    Destroy(_spawned[i]);
            }

            _spawned.Clear();
        }

        static CombatRoomKind PrevRoom(CombatRoomKind kind)
        {
            int n = ((int)kind + 3) % 4;
            return (CombatRoomKind)n;
        }
    }
}
