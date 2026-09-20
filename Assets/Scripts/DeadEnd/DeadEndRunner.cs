using System.Collections.Generic;
using UnityEngine;
using RogueShooter.Art;
using RogueShooter.Build;
using RogueShooter.Demo;
using RogueShooter.Layout;
using RogueShooter.Spawning;

namespace RogueShooter.DeadEnd
{
    /// <summary>
    /// Enter-once DeadEnd events for ThreeRouteScaffold. Four types, no BOSS feed.
    /// </summary>
    [DefaultExecutionOrder(60)]
    public class DeadEndRunner : MonoBehaviour
    {
        public const float EnterRadius = 2.5f;

        Transform _player;
        ChestAltarDirector _buildDir;
        SpawnBandDirector _director;
        Transform _root;
        readonly DeadEndOnceTracker _once = new DeadEndOnceTracker();
        readonly List<SiteDef> _sites = new List<SiteDef>();
        readonly Dictionary<string, GameObject> _markers = new Dictionary<string, GameObject>();
        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<SiteRuntime> _revealed = new List<SiteRuntime>();
        Vector2 _staticPos;
        bool _staticReady;
        bool _staticInspected;

        public DeadEndOnceTracker Tracker => _once;
        public string HudLine => _once.HudLine();

        public void Bind(
            Transform player,
            ChestAltarDirector buildDir,
            SpawnBandDirector director,
            Transform root,
            IDictionary<string, GameObject> markers)
        {
            _player = player;
            _buildDir = buildDir;
            _director = director;
            _root = root != null ? root : transform;
            _markers.Clear();
            if (markers != null)
            {
                foreach (var kv in markers)
                    _markers[kv.Key] = kv.Value;
            }

            _sites.Clear();
            for (int i = 0; i < LockSiteCatalog.Sites.Length; i++)
            {
                SiteDef site = LockSiteCatalog.Sites[i];
                if (DeadEndEventTypes.TryResolve(site, out _))
                    _sites.Add(site);
            }

            var types = new System.Text.StringBuilder();
            for (int i = 0; i < _sites.Count; i++)
            {
                if (types.Length > 0)
                    types.Append(' ');
                types.Append(_sites[i].Id).Append('=').Append(DeadEndEventTypes.FromSite(_sites[i]));
            }

            Debug.Log("[DeadEnd] runner bound sites=" + _sites.Count
                      + " enterR=" + EnterRadius.ToString("0.0") + " " + types);
        }

        public void ResetRun()
        {
            for (int i = 0; i < _revealed.Count; i++)
            {
                if (_buildDir != null)
                    _buildDir.UnregisterSite(_revealed[i]);
            }

            _revealed.Clear();
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                    Destroy(_spawned[i]);
            }

            _spawned.Clear();
            ClearEventMobs();
            _staticReady = false;
            _staticInspected = false;
            _once.Reset();
            RestoreMarkerLabels();
            Debug.Log("[DeadEnd] reset once-flags");
        }

        static void ClearEventMobs()
        {
            StubEnemy[] all = FindObjectsOfType<StubEnemy>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null)
                    continue;
                if (all[i].name.IndexOf("DeadEnd_", System.StringComparison.Ordinal) >= 0)
                    Destroy(all[i].gameObject);
            }
        }

        void Update()
        {
            if (_player == null)
                return;
            if (Input.GetKeyDown(KeyCode.N))
                ResetRun();

            TryStaticInspect();

            Vector2 p = new Vector2(_player.position.x, _player.position.y);
            for (int i = 0; i < _sites.Count; i++)
            {
                SiteDef site = _sites[i];
                if (_once.Fired(site.Id))
                    continue;
                if ((p - site.Position).sqrMagnitude > EnterRadius * EnterRadius)
                    continue;
                Fire(site);
            }
        }

        void Fire(SiteDef site)
        {
            if (!DeadEndEventTypes.TryResolve(site, out DeadEndEventType type))
                return;
            if (!_once.TryMark(site.Id, type))
                return;

            Debug.Log("[DeadEnd] " + site.Id + " type=" + type + " once"); // 预配置类型，整局一次

            switch (type)
            {
                case DeadEndEventType.ChestReveal:
                    RevealChest(site);
                    break;
                case DeadEndEventType.MobWave:
                    SpawnMobWave(site);
                    break;
                case DeadEndEventType.StaticRoom:
                    SpawnStaticRoom(site);
                    break;
                case DeadEndEventType.EmptySoft:
                    SoftEmpty(site);
                    break;
            }

            string accept = _once.ConsumeAcceptanceIfReady();
            if (!string.IsNullOrEmpty(accept))
                Debug.Log(accept);
        }

        void RevealChest(SiteDef site)
        {
            string chestId = "Chest_" + site.Id;
            Vector3 pos = new Vector3(site.Position.x, site.Position.y, 0f);
            string art = JianHaiArtCatalog.SpriteName(JianHaiArtCatalog.ChestRoot, "closed");
            GameObject go = JianHaiBind.Spawn(chestId, pos, art, _root);
            var chestDef = new SiteDef(chestId, site.Position, SiteKind.Chest, site.Route, site.Note, 2f);
            AddLabel(go, chestId, new Vector3(0f, 1.25f, 0f));
            var runtime = go.AddComponent<SiteRuntime>();
            runtime.Configure(chestDef, true);
            if (_buildDir != null)
                _buildDir.RegisterRevealedChest(runtime);
            _revealed.Add(runtime);
            _spawned.Add(go);
            MarkMarker(site.Id, site.Id + " REVEAL");
            if (_buildDir != null)
                _buildDir.Notify(chestId + " revealed — E open (Build +1)");
            Debug.Log("[DeadEnd] " + site.Id + " ChestReveal spawned " + chestId + " Build path ready (small +1)");
        }

        void SpawnMobWave(SiteDef site)
        {
            Vector3 pos = new Vector3(site.Position.x, site.Position.y, 0f);
            int n = 0;
            if (_director != null)
            {
                n = _director.SpawnEventWave(
                    "DeadEnd_" + site.Id, pos, null,
                    DeadEndEventTypes.MobWaveCountMin, DeadEndEventTypes.MobWaveCountMax);
            }
            MarkMarker(site.Id, site.Id + " WAVE");
            if (_buildDir != null)
                _buildDir.Notify(site.Id + " MobWave ×" + n + " (1–3, four-state AI)");
            Debug.Log("[DeadEnd] " + site.Id + " MobWave spawned=" + n
                      + " (1–3 existing kinds + MobFourStateAi, no new AI/CSV)");
        }

        void SpawnStaticRoom(SiteDef site)
        {
            Vector3 pos = new Vector3(site.Position.x, site.Position.y, 0f);
            GameObject prop = JianHaiBind.Spawn(site.Id + "_Static", pos, "jh_prop_brazier", _root);
            AddLabel(prop, "StaticRoom  E", new Vector3(0f, 1.15f, 0f));
            _spawned.Add(prop);
            _staticPos = site.Position;
            _staticReady = true;
            _staticInspected = false;
            MarkMarker(site.Id, site.Id + " STATIC");
            if (_buildDir != null)
                _buildDir.Notify(site.Id + " StaticRoom — E inspect");
            Debug.Log("[DeadEnd] " + site.Id + " StaticRoom stub visible (no Build, no BOSS)");
        }

        void SoftEmpty(SiteDef site)
        {
            int gold = EconomyGold.EmptySoftMin;
            if (_buildDir != null && _buildDir.Build != null)
                _buildDir.Build.AddGold(gold);
            MarkMarker(site.Id, site.Id + " EMPTY");
            if (_buildDir != null)
                _buildDir.Notify(site.Id + " empty — turn back");
            Debug.Log("[DeadEnd] " + site.Id + " EmptySoft gold="
                      + EconomyGold.EmptySoftMin + "–" + EconomyGold.EmptySoftMax
                      + " (no Build, no BOSS)");
        }

        void TryStaticInspect()
        {
            if (!_staticReady || _staticInspected || _player == null)
                return;
            if (!Input.GetKeyDown(KeyCode.E))
                return;
            Vector2 p = new Vector2(_player.position.x, _player.position.y);
            if ((p - _staticPos).sqrMagnitude > EnterRadius * EnterRadius)
                return;
            _staticInspected = true;
            if (_buildDir != null)
                _buildDir.Notify("StaticRoom — nothing else");
            Debug.Log("[DeadEnd] DE04 static inspect (no Build, no BOSS)");
        }

        void MarkMarker(string id, string label)
        {
            GameObject go;
            if (!_markers.TryGetValue(id, out go) || go == null)
                return;
            var tm = go.GetComponentInChildren<TextMesh>();
            if (tm != null)
                tm.text = label;
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.color = new Color(0.72f, 0.68f, 0.42f);
        }

        void RestoreMarkerLabels()
        {
            for (int i = 0; i < _sites.Count; i++)
            {
                SiteDef site = _sites[i];
                GameObject go;
                if (!_markers.TryGetValue(site.Id, out go) || go == null)
                    continue;
                var tm = go.GetComponentInChildren<TextMesh>();
                if (tm != null)
                    tm.text = site.Id;
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null)
                    sr.color = LockSiteCatalog.ColorFor(SiteKind.DeadEnd);
            }
        }

        static void AddLabel(GameObject marker, string text, Vector3 localOffset)
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
    }
}
