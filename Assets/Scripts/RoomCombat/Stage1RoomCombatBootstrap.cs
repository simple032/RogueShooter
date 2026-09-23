using System.Collections.Generic;
using UnityEngine;
using RogueShooter.Spawning;
using RogueShooter.StageMap;

namespace RogueShooter.RoomCombat
{
    /// <summary>
    /// Boots §4.5 combat from §6.8 StageMapGraph (START + FirstNormal + 4 shuffled slots).
    /// Geometry is abstract module rects — level templates may replace positions later.
    /// </summary>
    public sealed class Stage1RoomCombatBootstrap : MonoBehaviour
    {
        public const float RoomW = 20f;
        public const float RoomH = 16f;

        RoomCombatDriver[] _drivers;
        Transform _player;
        RoomCombatDriver _activeLock;
        StageMapGraph _graph;

        public RoomCombatDriver[] Drivers => _drivers;
        public RoomCombatDriver ActiveLock => _activeLock;
        public StageMapGraph Graph => _graph;

        public void Bind(Transform player, SpawnBandDirector director, Transform roomsRoot)
        {
            Bind(player, director, roomsRoot, runSeed: 20260923, stageIndex: 0);
        }

        public void Bind(Transform player, SpawnBandDirector director, Transform roomsRoot, int runSeed, int stageIndex)
        {
            _player = player;
            int seed = StageMapGenerator.StageSeed(runSeed, stageIndex);
            _graph = StageMapGenerator.Generate(seed, stageIndex);

            var list = new List<RoomCombatDriver>();
            for (int i = 0; i < _graph.Nodes.Length; i++)
            {
                StageMapNode node = _graph.Nodes[i];
                if (node == null || !node.IsCombatRoom)
                    continue;

                Rect bounds = BoundsFor(node);
                int w1 = node.CombatKind == RoomCombatKind.Normal ? 3 : 2;
                int w2 = RoomCombatRules.WantsSecondWave(node.CombatKind) ? 2 : 0;
                Rect inner = Shrink(bounds, 1.5f);
                if (!RoomCombatRules.TryPackPoints(inner, w1, RoomCombatRules.DefaultFootRadius, out Vector2[] p1))
                    p1 = new[] { Center(bounds) };
                Vector2[] p2 = System.Array.Empty<Vector2>();
                if (w2 > 0)
                {
                    Rect inner2 = Shrink(bounds, 2.2f);
                    if (!RoomCombatRules.TryPackPoints(inner2, w2, RoomCombatRules.DefaultFootRadius, out p2))
                        p2 = OffsetCopy(p1, new Vector2(0.7f, 0.7f));
                }

                var go = new GameObject("RoomCombat_" + node.Id);
                go.transform.SetParent(transform, false);
                var drv = go.AddComponent<RoomCombatDriver>();
                Transform interior = roomsRoot != null ? roomsRoot.Find(node.Id) : null;
                drv.Bind(node.Id, node.CombatKind, bounds, p1, p2, interior, null, null, director);
                drv.PreSpawnWave1("Z1");
                list.Add(drv);
            }

            _drivers = list.ToArray();
            Debug.Log("[RoomCombat] StageMap bootstrap rooms=" + _drivers.Length
                      + " seed=" + seed + " slots=" + string.Join(",", _graph.SlotOrder)
                      + " CONN=" + _graph.AltarConnId);
        }

        /// <summary>
        /// Module layout: START at origin (no combat). FirstNormal east of START.
        /// Four slots north/south/east/west of FirstNormal (star). CONN is a flag on altar, not a room.
        /// </summary>
        public static Rect BoundsFor(StageMapNode node)
        {
            if (node == null)
                return new Rect(0f, 0f, RoomW, RoomH);

            if (node.Role == StageNodeRole.FirstNormal)
                return new Rect(RoomW + 4f, 0f, RoomW, RoomH);

            if (node.Role == StageNodeRole.Slot)
            {
                float cx = RoomW + 4f + RoomW * 0.5f;
                float cy = RoomH * 0.5f;
                float gap = 4f;
                switch (node.SlotIndex)
                {
                    case 0: return new Rect(cx - RoomW * 0.5f, cy + RoomH * 0.5f + gap, RoomW, RoomH);
                    case 1: return new Rect(cx + RoomW * 0.5f + gap, cy - RoomH * 0.5f, RoomW, RoomH);
                    case 2: return new Rect(cx - RoomW * 0.5f, cy - RoomH * 1.5f - gap, RoomW, RoomH);
                    default: return new Rect(cx - RoomW * 1.5f - gap, cy - RoomH * 0.5f, RoomW, RoomH);
                }
            }

            // START spawn pad (non-combat)
            return new Rect(0f, (RoomH - 6f) * 0.5f, 6f, 6f);
        }

        void Update()
        {
            if (_player == null || _drivers == null)
                return;
            Vector2 p = _player.position;
            for (int i = 0; i < _drivers.Length; i++)
            {
                RoomCombatDriver d = _drivers[i];
                if (d == null || d.Brain == null)
                    continue;
                if (d.Brain.Phase == RoomCombatPhase.Outside && d.ContainsWorld(p))
                    d.NotifyPlayerEnter();

                if (d.Brain.DoorLocked)
                {
                    _activeLock = d;
                    Vector2 c = d.ClampInside(p);
                    if ((c - p).sqrMagnitude > 1e-6f)
                        _player.position = new Vector3(c.x, c.y, _player.position.z);
                }
            }

            if (_activeLock != null && (_activeLock.Brain == null || !_activeLock.Brain.DoorLocked))
                _activeLock = null;
        }

        static Rect Shrink(Rect r, float pad)
        {
            return Rect.MinMaxRect(r.xMin + pad, r.yMin + pad, r.xMax - pad, r.yMax - pad);
        }

        static Vector2 Center(Rect r)
        {
            return new Vector2(r.xMin + r.width * 0.5f, r.yMin + r.height * 0.5f);
        }

        static Vector2[] OffsetCopy(Vector2[] src, Vector2 delta)
        {
            if (src == null || src.Length == 0)
                return System.Array.Empty<Vector2>();
            var dst = new Vector2[src.Length];
            for (int i = 0; i < src.Length; i++)
                dst[i] = src[i] + delta;
            return dst;
        }
    }
}
