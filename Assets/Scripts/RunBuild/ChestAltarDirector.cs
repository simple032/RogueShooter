using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using RogueShooter.Balance;
using RogueShooter.Demo;
using RogueShooter.Layout;
using RogueShooter.Player;
using RogueShooter.Spawning;

namespace RogueShooter.Build
{
    /// <summary>
    /// Run-start chest P_spawn rolls, always-on altars, E interact 3-pick, 5-shelf shop.
    /// </summary>
    public class ChestAltarDirector : MonoBehaviour
    {
        BalanceLockData _lock;
        Transform _player;
        SpawnBandClock _clock;
        RunBuildState _build;
        readonly List<SiteRuntime> _sites = new List<SiteRuntime>();
        Dictionary<string, bool> _chestPresent = new Dictionary<string, bool>();
        System.Random _rng;
        int _seed;
        int _pendingInherit;
        bool _deathNoted;
        RewardOption[] _offers = Array.Empty<RewardOption>();
        AltarPick[] _altarPicks = Array.Empty<AltarPick>();
        ShopShelf[] _shopShelves = Array.Empty<ShopShelf>();
        bool _altarOffer;
        bool _shopOffer;
        SiteRuntime _offering;
        SiteRuntime _nearest;
        string _flash = "";
        float _flashUntil;
        readonly List<string> _emptyIds = new List<string>();
        RewardScreenView _screen;

        public RunBuildState Build => _build;
        public int Seed => _seed;
        public int ChestPresentCount => ChestPresenceRoller.CountPresent(_chestPresent);
        public int ChestSlotCount => _chestPresent.Count;
        public int AltarCount { get; private set; }
        public SiteRuntime Nearest => _nearest;
        public bool Offering => _offering != null;
        public IReadOnlyList<ShopShelf> ShopShelves => _shopShelves;
        public IReadOnlyList<string> EmptyChestIds => _emptyIds;

        public void Bind(BalanceLockData data, Transform player, List<SiteRuntime> sites, int seed)
        {
            Bind(data, player, sites, seed, null);
        }

        public void Bind(BalanceLockData data, Transform player, List<SiteRuntime> sites, int seed, SpawnBandClock clock)
        {
            _lock = data;
            _player = player;
            _clock = clock;
            _sites.Clear();
            if (sites != null)
                _sites.AddRange(sites);
            AltarCount = 0;
            for (int i = 0; i < _sites.Count; i++)
            {
                if (_sites[i] == null)
                    continue;
                if (_sites[i].Kind == SiteKind.Altar)
                {
                    AltarCount++;
                    _sites[i].SetAltarSize(AltarRewardRoll.SizeForHookId(_sites[i].Id));
                }
                if (_sites[i].Kind == SiteKind.Chest)
                    _sites[i].SetLargeChest(IsLargeChestId(_sites[i].Id));
            }

            int gold = EconomyGold.StartGold;
            _build = new RunBuildState(
                data != null ? data.powerBuildCoef : 0.45f,
                data != null ? data.powerRarityCoef : 0.55f,
                gold);
            BeginRun(seed);
        }

        static bool IsLargeChestId(string id)
        {
            // Stub tags until level hooks mark dead-end large chests.
            return id == "Chest_05" || id == "Chest_09" || id == "Chest_03";
        }

        float WallMinutes => _clock != null ? _clock.WallMinutes : 0f;

        public void OnEnemyKilled(StubEnemy enemy)
        {
            if (_build == null || enemy == null)
                return;
            float t = WallMinutes;
            int gain = EconomyGold.KillGold(enemy.KindId, t);
            _build.AddGold(gain);
            Debug.Log($"[Gold] kill {enemy.KindId} +{gain} gold={_build.Gold} phase={TimePressure.PhaseId(t)} t={t:0.00}′");
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

            int gold = ConsumeOpeningGold();
            if (_build != null)
                _build.Reset(gold);
            _deathNoted = false;

            RewardCatalog.BindPoolOwned(_build != null ? _build.OwnedRewardIds : null);
            _shopShelves = ShopStock.RollShelves(_rng);
            Debug.Log($"[Shop] stock n={_shopShelves.Length} no-refresh shelves={ShopStock.FormatShelves(_shopShelves)} gold={gold}");

            string empty = _emptyIds.Count == 0 ? "(none)" : string.Join(",", _emptyIds.ToArray());
            Debug.Log($"[ChestRoll] seed={_seed} P_spawn={p:0.00} present={ChestPresentCount}/{ChestSlotCount} empty={empty}");
            var altarSizes = new StringBuilder();
            for (int i = 0; i < _sites.Count; i++)
            {
                if (_sites[i] == null || _sites[i].Kind != SiteKind.Altar)
                    continue;
                if (altarSizes.Length > 0)
                    altarSizes.Append(',');
                altarSizes.Append(_sites[i].Id).Append('=')
                    .Append(AltarRewardRoll.SizeLabel(_sites[i].AltarSize));
            }

            Debug.Log($"[Altar] always present count={AltarCount} sizes={altarSizes}");
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

            NoteDeathIfDown();

            if (Input.GetKeyDown(KeyCode.E))
                TryInteract();
            if (Input.GetKeyDown(KeyCode.N))
                BeginRun(Environment.TickCount);
        }

        /// <summary>N27: snapshot held gold on death so the next BeginRun grants inherit.</summary>
        public void NoteDeathHeldGold(int held)
        {
            _pendingInherit = EconomyGold.DeathInherit(held);
            _deathNoted = true;
            Debug.Log($"[Gold] death inherit pending={_pendingInherit} from held={held} rate={EconomyGold.DeathInheritRate:0.00} cap={EconomyGold.DeathInheritCap}");
        }

        int ConsumeOpeningGold()
        {
            int gold = EconomyGold.StartGold + _pendingInherit;
            Debug.Log($"[Gold] open start={EconomyGold.StartGold} inherit={_pendingInherit} gold={gold}");
            _pendingInherit = 0;
            return gold;
        }

        void NoteDeathIfDown()
        {
            if (_deathNoted || _player == null || _build == null)
                return;
            var vitals = _player.GetComponent<PlayerVitals>();
            if (vitals == null || vitals.Hp > 0f)
                return;
            NoteDeathHeldGold(_build.Gold);
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
                OpenShop();
                return;
            }

            if (_nearest.Kind == SiteKind.Altar)
            {
                TryLightAltar();
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

            string source = "chest";
            _altarOffer = false;
            _altarPicks = Array.Empty<AltarPick>();
            _offers = RewardOffer.RollUnique(_lock, source, _rng);
            if (_offers == null || _offers.Length == 0)
            {
                Flash("reward pool empty");
                return;
            }

            System.Array.Sort(_offers, (a, b) => TierOf(a).CompareTo(TierOf(b)));
            _offering = _nearest;
            PauseForScreen();
            RewardView().ShowChest(CardsFromOffers(_offers));
            Debug.Log($"[Offer] {_offering.Id} source={source} n={_offers.Length} paused");
        }

        void OpenShop()
        {
            if (_nearest == null || _shopShelves == null || _shopShelves.Length != ShopStock.ShelfCount)
            {
                Debug.Log("[Shop] open FAIL shelves missing");
                return;
            }

            _shopOffer = true;
            _altarOffer = false;
            _altarPicks = Array.Empty<AltarPick>();
            _offers = Array.Empty<RewardOption>();
            _offering = _nearest;
            PauseForScreen();
            RewardView().ShowShop(CardsFromShop(_shopShelves));
            Debug.Log($"[Shop] open {_offering.Id} n={_shopShelves.Length} no-refresh shelves={ShopStock.FormatShelves(_shopShelves)} " +
                      $"gold={_build.Gold} B={_build.BuildCount}");
        }

        void BuyShopShelf(int index)
        {
            if (_build == null || _shopShelves == null || index < 0 || index >= _shopShelves.Length)
                return;
            ShopShelf shelf = _shopShelves[index];
            if (shelf.Sold)
            {
                Debug.Log($"[Shop] buy FAIL {shelf.Id} already sold B={_build.BuildCount}");
                Flash(shelf.Id + " sold");
                return;
            }

            int b = _build.BuildCount;
            int equiv = ShopStock.BuildEquivFor(shelf.ContentRole);
            bool ok = _build.TryShopBuy(shelf.Price, equiv, shelf.Id);
            if (ok)
            {
                shelf.Sold = true;
                _shopShelves[index] = shelf;
            }

            string line = ok
                ? $"[Shop] buy ok {shelf.Id} tier={AltarRewardRoll.TierLabel(shelf.Tier)} price={shelf.Price} equiv=+{equiv} " +
                  $"effect={shelf.Effect} gold={_build.Gold} B={b}→{_build.BuildCount}"
                : $"[Shop] buy FAIL {shelf.Id} need={shelf.Price} gold={_build.Gold} B={_build.BuildCount}";
            Debug.Log(line);
            Flash(ok
                ? "bought " + shelf.Id + " -" + shelf.Price + "g B+" + equiv
                : "shop: not enough gold");
        }

        void TryLightAltar()
        {
            SiteRuntime altar = _nearest;
            if (altar == null || altar.Lit)
                return;
            if (altar.AltarSize == AltarSize.None)
            {
                Debug.Log("[Altar] no-size " + altar.Id + " not lit B=" + _build.BuildCount);
                return;
            }

            if (_build.AltarSizeClaimed(altar.AltarSize))
            {
                altar.MarkLit();
                Debug.Log("[Altar] lit-no-reward " + altar.Id + " size=" + AltarRewardRoll.SizeLabel(altar.AltarSize)
                    + " lit=true B=" + _build.BuildCount);
                return;
            }

            RewardCatalog.BindPoolOwned(_build != null ? _build.OwnedRewardIds : null);
            _altarPicks = AltarRewardRoll.RollThree(altar.AltarSize, _rng);
            if (_altarPicks == null || _altarPicks.Length == 0)
            {
                Debug.Log("[Altar] offer empty " + altar.Id);
                return;
            }

            System.Array.Sort(_altarPicks, (a, b) => a.Tier.CompareTo(b.Tier));
            _altarOffer = true;
            _offers = Array.Empty<RewardOption>();
            _offering = altar;
            PauseForScreen();
            RewardView().ShowAltar(CardsFromAltar(_altarPicks));
            var order = new StringBuilder();
            for (int i = 0; i < _altarPicks.Length; i++)
            {
                if (i > 0)
                    order.Append(',');
                order.Append(AltarRewardRoll.TierLabel(_altarPicks[i].Tier));
            }

            Debug.Log("[Altar] offer " + altar.Id + " size=" + AltarRewardRoll.SizeLabel(altar.AltarSize)
                + " n=" + _altarPicks.Length + " tiers=" + order + " lit=false B=" + _build.BuildCount);
        }

        void CancelCurrentOffer()
        {
            if (_altarOffer && _offering != null)
            {
                Debug.Log("[Altar] cancel " + _offering.Id + " size=" + AltarRewardRoll.SizeLabel(_offering.AltarSize)
                    + " lit=" + _offering.Lit + " B=" + _build.BuildCount);
            }
            else if (_shopOffer && _offering != null)
            {
                Debug.Log("[Shop] close " + _offering.Id + " shelves=" + ShopStock.FormatShelves(_shopShelves)
                    + " B=" + _build.BuildCount + " (no refresh)");
            }

            Flash("offer cancelled");
            CloseOffer(restoreTime: true);
        }

        void ConfirmCurrentOffer(int pick)
        {
            if (_shopOffer)
            {
                BuyShopShelf(pick);
                return;
            }

            if (_altarOffer)
            {
                if (_offering == null || pick < 0 || pick >= _altarPicks.Length)
                    return;
                AltarSize size = _offering.AltarSize;
                string rewardId = _altarPicks[pick].Id;
                bool added = _build.ConfirmAltarPick(size, rewardId);
                _offering.MarkLit();
                Debug.Log("[Altar] confirm " + _offering.Id + " size=" + AltarRewardRoll.SizeLabel(size)
                    + " pick=" + rewardId + " added=" + added + " delta=" + AltarRewardRoll.BuildDelta(size)
                    + " lit=" + _offering.Lit + " B=" + _build.BuildCount);
                Flash("lit " + _offering.Id + " " + rewardId);
                CloseOffer(restoreTime: true);
                return;
            }

            if (pick < 0 || pick >= _offers.Length || _offering == null)
                return;

            RewardOption opt = _offers[pick];
            float t = WallMinutes;
            bool large = _offering.LargeChest;
            int buildDelta = RunBuildState.ChestBuildDelta(large);
            _build.GrantBuildPick(opt.Id, opt.Rarity, opt.Score, buildDelta);
            int chestGold = EconomyGold.ChestGold(large, t);
            _build.AddGold(chestGold);
            _offering.MarkConsumed();
            Debug.Log($"[Build] {_offering.Id} {(large ? "large" : "small")} pick {opt.Id} {opt.Rarity} tag={opt.Tag} " +
                      $"+{opt.Score} B+{buildDelta} B={_build.BuildCount} RS={_build.RarityScore} Power={_build.Power:0.00} " +
                      $"({_build.PowerFormulaLine()})");
            Debug.Log($"[Gold] chest {_offering.Id} {(large ? "large" : "small")} +{chestGold} gold={_build.Gold} " +
                      $"phase={TimePressure.PhaseId(t)} t={t:0.00}′");
            Flash("took " + opt.Id + " B+" + buildDelta + " +" + chestGold + "g");
            CloseOffer(restoreTime: true);
        }

        void HandleOfferInput()
        {
            if (_screen != null && _screen.Session != null && _screen.Session.Open && !_screen.ChoicesVisible)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                    CancelCurrentOffer();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape) || (!_shopOffer && Input.GetKeyDown(KeyCode.E)))
            {
                CancelCurrentOffer();
                return;
            }

            if (_shopOffer && Input.GetKeyDown(KeyCode.E))
            {
                CancelCurrentOffer();
                return;
            }

            int pick = -1;
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) pick = 0;
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) pick = 1;
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) pick = 2;
            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) pick = 3;
            if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5)) pick = 4;
            if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6)) pick = 5;
            if (pick < 0)
                return;
            if (_shopOffer)
            {
                if (pick < _shopShelves.Length)
                    ConfirmCurrentOffer(pick);
                return;
            }

            if (pick > 2)
                return;
            ConfirmCurrentOffer(pick);
        }

        void CloseOffer(bool restoreTime)
        {
            _offering = null;
            _offers = Array.Empty<RewardOption>();
            _altarPicks = Array.Empty<AltarPick>();
            _altarOffer = false;
            _shopOffer = false;
            if (_clock != null)
                _clock.SetPaused(false);
            if (_screen != null)
                _screen.Hide();
            RunPause.InteractOpen = false;
            if (restoreTime)
                Time.timeScale = 1f;
        }

        public void NotifyUiPick(int index)
        {
            if (_screen != null && !_screen.ChoicesVisible)
                return;
            ConfirmCurrentOffer(index);
        }

        public void NotifyUiCancel()
        {
            CancelCurrentOffer();
        }

        void PauseForScreen()
        {
            RunPause.InteractOpen = true;
            Time.timeScale = 0f;
            if (_clock != null)
                _clock.SetPaused(true);
        }

        RewardScreenView RewardView()
        {
            if (_screen == null)
            {
                _screen = GetComponent<RewardScreenView>();
                if (_screen == null)
                    _screen = gameObject.AddComponent<RewardScreenView>();
                _screen.Bind(this);
            }
            return _screen;
        }

        static RewardCardData[] CardsFromOffers(RewardOption[] offers)
        {
            if (offers == null)
                return System.Array.Empty<RewardCardData>();
            var cards = new RewardCardData[offers.Length];
            for (int i = 0; i < offers.Length; i++)
            {
                RewardOption opt = offers[i];
                RewardTier tier = TierOf(opt);
                cards[i] = RewardPresent.ToCard(opt.Id, tier, "", MarkFor(tier, false), i, false);
            }
            return cards;
        }

        static RewardCardData[] CardsFromAltar(AltarPick[] picks)
        {
            if (picks == null)
                return System.Array.Empty<RewardCardData>();
            var cards = new RewardCardData[picks.Length];
            for (int i = 0; i < picks.Length; i++)
            {
                AltarPick pick = picks[i];
                cards[i] = RewardPresent.ToCard(pick.Id, pick.Tier, "", MarkFor(pick.Tier, false), i, false);
            }
            return cards;
        }

        static RewardCardData[] CardsFromShop(ShopShelf[] shelves)
        {
            if (shelves == null)
                return System.Array.Empty<RewardCardData>();
            var cards = new RewardCardData[shelves.Length];
            for (int i = 0; i < shelves.Length; i++)
            {
                ShopShelf shelf = shelves[i];
                cards[i] = RewardPresent.ToCard(
                    shelf.Id,
                    shelf.Tier,
                    shelf.Price + "金",
                    shelf.IsHeal ? "回血" : MarkFor(shelf.Tier, false),
                    i,
                    shelf.Sold);
            }
            return cards;
        }

        static RewardTier TierOf(RewardOption opt)
        {
            string rarity = opt.Rarity ?? "";
            if (rarity.IndexOf("高", System.StringComparison.Ordinal) >= 0
                || string.Equals(rarity, "E", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(rarity, "High", System.StringComparison.OrdinalIgnoreCase))
                return RewardTier.High;
            if (rarity.IndexOf("中", System.StringComparison.Ordinal) >= 0
                || string.Equals(rarity, "R", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(rarity, "Mid", System.StringComparison.OrdinalIgnoreCase))
                return RewardTier.Mid;
            return RewardTier.Low;
        }

        static string MarkFor(RewardTier tier, bool heal)
        {
            if (heal)
                return "回血";
            switch (tier)
            {
                case RewardTier.Mid: return "中";
                case RewardTier.High: return "高";
                default: return "低";
            }
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

            int w = 560;
            int h = _shopOffer ? 280 : 200;
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;
            GUI.Box(new Rect(x, y, w, h), "");
            var title = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            var style = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            if (_shopOffer)
            {
                GUI.Label(new Rect(x + 16, y + 10, w - 32, 24),
                    _offering.Id + "  —  5 shelves (no refresh)", title);
                GUI.Label(new Rect(x + 16, y + 36, w - 32, 20),
                    "1–5 buy · Esc/E close · prices 20/28/35 · never Build", style);
                for (int i = 0; i < _shopShelves.Length; i++)
                {
                    ShopShelf s = _shopShelves[i];
                    string line = s.Sold
                        ? (i + 1) + ")  " + s.Id + "  SOLD"
                        : (i + 1) + ")  " + s.Id + "  " + AltarRewardRoll.TierLabel(s.Tier)
                          + "  " + s.Price + "g  " + s.Effect;
                    GUI.Label(new Rect(x + 16, y + 64 + i * 28, w - 32, 26), line, style);
                }

                return;
            }

            int n = _altarOffer ? _altarPicks.Length : _offers.Length;
            string src = _altarOffer ? "altar light" : "chest";
            GUI.Label(new Rect(x + 16, y + 10, w - 32, 24),
                _offering.Id + "  —  pick 1 of " + n + "   [" + src + "]", title);
            GUI.Label(new Rect(x + 16, y + 36, w - 32, 20), "1 / 2 / 3 select · Esc/E cancel (no Build)", style);
            if (_altarOffer)
            {
                for (int i = 0; i < _altarPicks.Length; i++)
                {
                    AltarPick o = _altarPicks[i];
                    GUI.Label(new Rect(x + 16, y + 64 + i * 28, w - 32, 26),
                        (i + 1) + ")  " + AltarRewardRoll.TierLabel(o.Tier) + "  " + o.Effect, style);
                }

                return;
            }

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
