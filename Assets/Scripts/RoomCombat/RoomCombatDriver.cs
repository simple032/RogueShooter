using System.Collections.Generic;
using UnityEngine;
using RogueShooter.Demo;
using RogueShooter.Spawning;

namespace RogueShooter.RoomCombat
{
    /// <summary>
    /// One combat room: hide interior until enter, pre-spawn wave 1, lock exits,
    /// optional wave 2 by kind, unlock when clear. Level supplies bounds + spawn points.
    /// </summary>
    public sealed class RoomCombatDriver : MonoBehaviour
    {
        public RoomCombatBrain Brain { get; private set; }
        public string RoomId { get; private set; }
        public Rect WalkBounds { get; private set; }
        public Vector2[] Wave1Points { get; private set; }
        public Vector2[] Wave2Points { get; private set; }

        [SerializeField] Transform interiorRoot;
        [SerializeField] Transform[] doorVisuals;
        [SerializeField] GameObject[] doorBarriers;

        readonly List<StubEnemy> _wave1 = new List<StubEnemy>();
        readonly List<StubEnemy> _wave2 = new List<StubEnemy>();
        SpawnBandDirector _director;
        bool _wave2Kickoff;

        public void Bind(
            string roomId,
            RoomCombatKind kind,
            Rect walkBounds,
            Vector2[] wave1Points,
            Vector2[] wave2Points,
            Transform interior,
            Transform[] doors,
            GameObject[] barriers,
            SpawnBandDirector director)
        {
            RoomId = roomId;
            WalkBounds = walkBounds;
            Wave1Points = wave1Points ?? System.Array.Empty<Vector2>();
            Wave2Points = wave2Points ?? System.Array.Empty<Vector2>();
            interiorRoot = interior;
            doorVisuals = doors;
            doorBarriers = barriers;
            _director = director;

            Brain = new RoomCombatBrain();
            Brain.Configure(kind);
            ApplyVision(false);
            ApplyDoor(false);
        }

        /// <summary>Pre-spawn wave 1 before the player can cross the entry line.</summary>
        public int PreSpawnWave1(string band)
        {
            if (Brain == null || !RoomCombatRules.UsesRoomCombat(Brain.Kind))
                return 0;
            if (Brain.Wave1PreSpawned)
                return Brain.Wave1Alive;
            if (!RoomCombatRules.FootCirclesClear(Wave1Points, RoomCombatRules.DefaultFootRadius))
            {
                Debug.LogWarning("[RoomCombat] " + RoomId + " wave1 points fail foot-circle spacing");
                return 0;
            }

            int n = SpawnAtPoints(RoomId + "_W1", Wave1Points, band, _wave1);
            Brain.MarkWave1PreSpawned(n);
            ApplyVision(false);
            Debug.Log("[RoomCombat] " + RoomId + " pre-spawn W1 n=" + n + " kind=" + Brain.Kind);
            return n;
        }

        public void NotifyPlayerEnter()
        {
            if (Brain == null)
                return;
            if (Brain.Phase != RoomCombatPhase.Outside)
                return;
            Brain.NotifyEnter();
            ApplyVision(true);
            ApplyDoor(true);
            Debug.Log("[RoomCombat] " + RoomId + " ENTER lock vision=on phase=" + Brain.Phase);
        }

        public void Tick()
        {
            if (Brain == null)
                return;

            if (Brain.Phase == RoomCombatPhase.LockedWave2 && !Brain.Wave2Spawned && !_wave2Kickoff)
            {
                _wave2Kickoff = true;
                SpawnWave2Immediate("Z1");
            }
        }

        public bool ContainsWorld(Vector2 world)
        {
            return WalkBounds.Contains(world);
        }

        public Vector2 ClampInside(Vector2 world)
        {
            float x = Mathf.Clamp(world.x, WalkBounds.xMin, WalkBounds.xMax);
            float y = Mathf.Clamp(world.y, WalkBounds.yMin, WalkBounds.yMax);
            return new Vector2(x, y);
        }

        void SpawnWave2Immediate(string band)
        {
            if (!Brain.WantsSecondWave)
            {
                Brain.MarkWave2Spawned(0);
                ApplyDoor(false);
                return;
            }

            if (!RoomCombatRules.FootCirclesClear(Wave2Points, RoomCombatRules.DefaultFootRadius))
                Debug.LogWarning("[RoomCombat] " + RoomId + " wave2 points fail foot-circle spacing");

            int n = SpawnAtPoints(RoomId + "_W2", Wave2Points, band, _wave2);
            Brain.MarkWave2Spawned(n);
            ApplyDoor(Brain.DoorLocked);
            Debug.Log("[RoomCombat] " + RoomId + " spawn W2 n=" + n + " phase=" + Brain.Phase);
            if (!Brain.DoorLocked)
                ApplyDoor(false);
        }

        int SpawnAtPoints(string id, Vector2[] points, string band, List<StubEnemy> track)
        {
            track.Clear();
            if (_director == null || points == null || points.Length == 0)
                return 0;

            int n = _director.SpawnRoomCombatWave(id, points, band);
            // Track via name prefix — StubEnemy Died hook below on next frame scan.
            var stubs = FindObjectsOfType<StubEnemy>();
            for (int i = 0; i < stubs.Length; i++)
            {
                StubEnemy e = stubs[i];
                if (e == null || e.gameObject == null)
                    continue;
                if (!e.gameObject.name.StartsWith("Stub_" + id))
                    continue;
                track.Add(e);
                e.Died -= OnTrackedDied;
                e.Died += OnTrackedDied;
            }

            return track.Count > 0 ? track.Count : n;
        }

        void OnTrackedDied(StubEnemy enemy)
        {
            if (Brain == null || enemy == null)
                return;
            if (_wave1.Remove(enemy))
            {
                Brain.NotifyWave1Death();
                if (Brain.Phase == RoomCombatPhase.Cleared)
                    ApplyDoor(false);
                else if (Brain.Phase == RoomCombatPhase.LockedWave2 && !Brain.Wave2Spawned)
                    Tick();
                return;
            }

            if (_wave2.Remove(enemy))
            {
                Brain.NotifyWave2Death();
                if (Brain.Phase == RoomCombatPhase.Cleared)
                    ApplyDoor(false);
            }
        }

        void ApplyVision(bool visible)
        {
            if (interiorRoot == null)
                return;
            var renderers = interiorRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].enabled = visible;

            // Pre-spawned stubs parented under scene root — hide by wave1 list when outside.
            if (!visible)
            {
                for (int i = 0; i < _wave1.Count; i++)
                {
                    if (_wave1[i] == null)
                        continue;
                    var sr = _wave1[i].GetComponent<Renderer>();
                    if (sr != null)
                        sr.enabled = false;
                }
            }
            else
            {
                for (int i = 0; i < _wave1.Count; i++)
                {
                    if (_wave1[i] == null)
                        continue;
                    var sr = _wave1[i].GetComponent<Renderer>();
                    if (sr != null)
                        sr.enabled = true;
                }
            }
        }

        void ApplyDoor(bool locked)
        {
            if (doorBarriers != null)
            {
                for (int i = 0; i < doorBarriers.Length; i++)
                {
                    if (doorBarriers[i] != null)
                        doorBarriers[i].SetActive(locked);
                }
            }

            if (doorVisuals == null)
                return;
            for (int i = 0; i < doorVisuals.Length; i++)
            {
                if (doorVisuals[i] == null)
                    continue;
                // Prefer closed sprite when locked — name convention Door_*_Closed / Open.
                doorVisuals[i].gameObject.SetActive(true);
            }
        }

        void Update()
        {
            Tick();
        }
    }
}
