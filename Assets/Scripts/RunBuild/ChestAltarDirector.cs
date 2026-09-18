using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using RogueShooter.Balance;
using RogueShooter.Layout;
using RogueShooter.Player;

namespace RogueShooter.Build
{
    /// <summary>
    /// Run-start chest P_spawn rolls, always-on altars, E interact 3-pick, stub shop.
    /// </summary>
    public class ChestAltarDirector : MonoBehaviour
    {
        BalanceLockData _lock;
        Transform _player;
        RunBuildState _build;
        readonly List<SiteRuntime> _sites = new List<SiteRuntime>();
        Dictionary<string, bool> _chestPresent = new Dictionary<string, bool>();
        System.Random _rng;
        int _seed;
        RewardOption[] _offers = Array.Empty<RewardOption>();
        SiteRuntime _offering;
        SiteRuntime _nearest;
        string _flash = "";
        float _flashUntil;
        readonly List<string> _emptyIds = new List<string>();

        public RunBuildState Build => _build;
        public int Seed => _seed;
        public int ChestPresentCount => ChestPresenceRoller.CountPresent(_chestPresent);
        public int ChestSlotCount => _chestPresent.Count;
        public int AltarCount { get; private set; }
        public SiteRuntime Nearest => _nearest;
        public bool Offering => _offering != null;
        public IReadOnlyList<string> EmptyChestIds => _emptyIds;

        public void Bind(BalanceLockData data, Transform player, List<SiteRuntime> sites, int seed)
        {
            _lock = data;
            _player = player;
            _sites.Clear();
            if (sites != null)
                _sites.AddRange(sites);
            AltarCount = 0;
            for (int i = 0; i < _sites.Count; i++)
            {
                if (_sites[i] != null && _sites[i].Kind == SiteKind.Altar)
                    AltarCount++;
            }

            int gold = data != null ? data.shopStartGold : 0;
            _build = new RunBuildState(
                data != null ? data.powerBuildCoef : 0.45f,
                data != null ? data.powerRarityCoef : 0.55f,
                gold);
            BeginRun(seed);
        }

        public void BeginRun(int seed)
        {
            CloseOffer(restoreTime: true);
            _seed = seed;
            _rng = new System.Random(seed);
            var chestIds = new List<string>();
            for (int i = 0; i < _sites.Count; i++)
            {
                SiteRuntime s = _sites[i];
                if (s != null && s.Kind == SiteKind.Chest)
                    chestIds.Add(s.Id);
            }

            float p = _lock != null ? _lock.pSpawn : 0.9f;
            _chestPresent = ChestPresenceRoller.RollChests(chestIds, p, _rng);
            _emptyIds.Clear();
            for (int i = 0; i < _sites.Count; i++)
            {
                SiteRuntime s = _sites[i];
                if (s == null)
                    continue;
                if (s.Kind == SiteKind.Chest)
                {
                    bool present;
                    if (!_chestPresent.TryGetValue(s.Id, out present))
                        present = false;
                    s.SetPresent(present);
                    if (!present)
                        _emptyIds.Add(s.Id);
                }
                else
                {
                    s.SetPresent(true);
                }
            }

            int gold = _lock != null ? _lock.shopStartGold : 0;
            if (_build != null)
                _build.Reset(gold);

            string empty = _emptyIds.Count == 0 ? "(none)" : string.Join(",", _emptyIds.ToArray());
            Debug.Log($"[ChestRoll] seed={_seed} P_spawn={p:0.00} present={ChestPresentCount}/{ChestSlotCount} empty={empty}");
            Debug.Log($"[Altar] always present count={AltarCount} (no roll)");
        }

        void OnDisable()
        {
            CloseOffer(restoreTime: true);
        }

        void Update()
        {
            if (_player == null)
                return;

            if (Offering)
            {
                HandleOfferInput();
                return;
            }

            RefreshNearest();

            if (Input.GetKeyDown(KeyCode.E))
                TryInteract();
            if (Input.GetKeyDown(KeyCode.N))
                BeginRun(Environment.TickCount);
        }

        void RefreshNearest()
        {
            float range = _lock != null && _lock.interactRange > 0f ? _lock.interactRange : 1.7f;
            float best = range;
            SiteRuntime hit = null;
            Vector3 p = _player.position;
            for (int i = 0; i < _sites.Count; i++)
            {
                SiteRuntime s = _sites[i];
                if (s == null)
                    continue;
                s.InRange = false;
                if (s.Kind != SiteKind.Chest && s.Kind != SiteKind.Altar && s.Kind != SiteKind.Shop)
                    continue;
                float d = Vector2.Distance(new Vector2(p.x, p.y), new Vector2(s.WorldPosition.x, s.WorldPosition.y));
                if (d <= best)
                {
                    best = d;
                    hit = s;
                }
            }

            _nearest = hit;
            if (_nearest != null)
                _nearest.InRange = true;
        }

        void TryInteract()
        {
            if (_nearest == null)
            {
                Flash("no site in range");
                return;
            }

            if (_nearest.IsShop)
            {
                BuyShop();
                return;
            }

            if (_nearest.IsEmptyChest)
            {
                Flash(_nearest.Id + " empty — no Build");
                Debug.Log("[Chest] " + _nearest.Id + " EMPTY (P_spawn miss) Build unchanged B=" +
                          _build.BuildCount + " RS=" + _build.RarityScore);
                return;
            }

            if (_nearest.Consumed)
            {
                Flash(_nearest.Id + " already taken");
                return;
            }

            if (!_nearest.CanOfferBuild)
                return;

            string source = _nearest.Kind == SiteKind.Altar ? "altar" : "chest";
            _offers = RewardOffer.RollUnique(_lock, source, _rng);
            if (_offers == null || _offers.Length == 0)
            {
                Flash("reward pool empty");
                return;
            }

            _offering = _nearest;
            RunPause.InteractOpen = true;
            Time.timeScale = 0f;
            Debug.Log($"[Offer] {_offering.Id} source={source} n={_offers.Length} paused");
        }

        void BuyShop()
        {
            if (_build == null)
                return;
            int b = _build.BuildCount;
            int rs = _build.RarityScore;
            int price = _lock != null ? _lock.shopStubPrice : 25;
            bool ok = _build.TryShopBuy(price);
            string line = ok
                ? $"[Shop] buy ok price={price} gold={_build.Gold} B={_build.BuildCount} RS={_build.RarityScore} (unchanged Build)"
                : $"[Shop] buy FAIL need={price} gold={_build.Gold} B={_build.BuildCount} RS={_build.RarityScore}";
            Debug.Log(line);
            if (ok && (_build.BuildCount != b || _build.RarityScore != rs))
                Debug.LogError("[Shop] BUG shop mutated Build");
            Flash(ok
                ? "shop buy gold-" + price + "  B/RS unchanged"
                : "shop: not enough gold");
        }

        void HandleOfferInput()
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E))
            {
                Flash("offer cancelled");
                CloseOffer(restoreTime: true);
                return;
            }

            int pick = -1;
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) pick = 0;
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) pick = 1;
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) pick = 2;
            if (pick < 0 || pick >= _offers.Length)
                return;

            RewardOption opt = _offers[pick];
            _build.GrantBuildPick(opt.Id, opt.Rarity, opt.Score);
            _offering.MarkConsumed();
            Debug.Log($"[Build] {_offering.Id} pick {opt.Id} {opt.Rarity} tag={opt.Tag} +{opt.Score} " +
                      $"B={_build.BuildCount} RS={_build.RarityScore} Power={_build.Power:0.00} " +
                      $"({_build.PowerFormulaLine()})");
            Flash("took " + opt.Id);
            CloseOffer(restoreTime: true);
        }

        void CloseOffer(bool restoreTime)
        {
            _offering = null;
            _offers = Array.Empty<RewardOption>();
            RunPause.InteractOpen = false;
            if (restoreTime)
                Time.timeScale = 1f;
        }

        void Flash(string msg)
        {
            _flash = msg ?? "";
            _flashUntil = Time.unscaledTime + 1.8f;
        }

        public string FlashMessage()
        {
            return Time.unscaledTime <= _flashUntil ? _flash : "";
        }

        public string EmptyLine()
        {
            if (_emptyIds.Count == 0)
                return "(none)";
            return string.Join(",", _emptyIds.ToArray());
        }

        public void DrawOfferGui()
        {
            if (!Offering)
                return;

            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = old;

            int w = 520;
            int h = 200;
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;
            GUI.Box(new Rect(x, y, w, h), "");
            var title = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            var style = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            string src = _offering.Kind == SiteKind.Altar ? "altar (damage bias)" : "chest (survival bias)";
            GUI.Label(new Rect(x + 16, y + 10, w - 32, 24),
                _offering.Id + "  —  pick 1 of " + _offers.Length + "   [" + src + "]", title);
            GUI.Label(new Rect(x + 16, y + 36, w - 32, 20), "1 / 2 / 3 select · Esc/E cancel (no Build)", style);
            for (int i = 0; i < _offers.Length; i++)
            {
                RewardOption o = _offers[i];
                GUI.Label(new Rect(x + 16, y + 64 + i * 28, w - 32, 26),
                    (i + 1) + ")  " + o.Id + "   " + o.Rarity + "  +" + o.Score + " RS   tag=" + o.Tag,
                    style);
            }
        }

        public string SummaryLine()
        {
            if (_build == null)
                return "Build —";
            var sb = new StringBuilder();
            sb.Append("B=").Append(_build.BuildCount);
            sb.Append("  RS=").Append(_build.RarityScore);
            sb.Append("  Power=").Append(_build.PowerFormulaLine());
            sb.Append(" = ").Append(_build.Power.ToString("0.00"));
            sb.Append("  gold=").Append(_build.Gold);
            sb.Append("  shopBuys=").Append(_build.ShopBuys);
            return sb.ToString();
        }
    }
}
