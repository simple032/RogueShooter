using UnityEngine;
using RogueShooter.Spawning;

namespace RogueShooter.RoomCombat
{
    /// <summary>
    /// Boots §4.5 combat on Stage1 layout (RM/CH/AL from JianHaiStage1MazeBuilder floors).
    /// Does not change room geometry — only binds walk rects + packs spawn points by foot-circle rule.
    /// </summary>
    public sealed class Stage1RoomCombatBootstrap : MonoBehaviour
    {
        static readonly (string id, RoomCombatKind kind, Rect bounds)[] Rooms =
        {
            ("RoomA_Normal", RoomCombatKind.Normal, RectFromTiles(3, 2, 22, 17)),
            ("RoomB_Chest", RoomCombatKind.SmallChest, RectFromTiles(29, 2, 48, 17)),
            ("RoomD_Normal", RoomCombatKind.Normal, RectFromTiles(29, 22, 48, 37)),
            ("RoomC_Altar", RoomCombatKind.Altar, RectFromTiles(3, 22, 22, 37)),
        };

        RoomCombatDriver[] _drivers;
        Transform _player;
        RoomCombatDriver _activeLock;

        public RoomCombatDriver[] Drivers => _drivers;
        public RoomCombatDriver ActiveLock => _activeLock;

        public void Bind(Transform player, SpawnBandDirector director, Transform roomsRoot)
        {
            _player = player;
            var list = new System.Collections.Generic.List<RoomCombatDriver>();
            for (int i = 0; i < Rooms.Length; i++)
            {
                var def = Rooms[i];
                int w1 = def.kind == RoomCombatKind.Normal ? 3 : 2;
                int w2 = RoomCombatRules.WantsSecondWave(def.kind) ? 2 : 0;
                Rect inner = Shrink(def.bounds, 1.5f);
                if (!RoomCombatRules.TryPackPoints(inner, w1, RoomCombatRules.DefaultFootRadius, out Vector2[] p1))
                    p1 = new[] { Center(def.bounds) };
                Vector2[] p2 = System.Array.Empty<Vector2>();
                if (w2 > 0)
                {
                    Rect inner2 = Shrink(def.bounds, 2.2f);
                    if (!RoomCombatRules.TryPackPoints(inner2, w2, RoomCombatRules.DefaultFootRadius, out p2))
                        p2 = OffsetCopy(p1, new Vector2(0.7f, 0.7f));
                }

                var go = new GameObject("RoomCombat_" + def.id);
                go.transform.SetParent(transform, false);
                var drv = go.AddComponent<RoomCombatDriver>();
                Transform interior = roomsRoot != null ? roomsRoot.Find(def.id) : null;
                drv.Bind(def.id, def.kind, def.bounds, p1, p2, interior, null, null, director);
                drv.PreSpawnWave1("Z1");
                list.Add(drv);
            }

            _drivers = list.ToArray();
            Debug.Log("[RoomCombat] Stage1 bootstrap rooms=" + _drivers.Length);
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

        static Rect RectFromTiles(int x0, int y0, int x1, int y1)
        {
            // inclusive tile indices → world AABB (1u/tile)
            return Rect.MinMaxRect(x0, y0, x1 + 1f, y1 + 1f);
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
