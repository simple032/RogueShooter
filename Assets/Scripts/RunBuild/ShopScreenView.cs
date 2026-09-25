using System;
using UnityEngine;
using UnityEngine.UI;
using RogueShooter.Player;

namespace RogueShooter.Build
{
    /// <summary>
    /// 商店购买界面 v0.2: standalone purchase screen (not the 3-choice reward screen).
    /// 6 shelves in a 2×3 grid + 离开, live gold top-right, detail panel for the focused shelf.
    /// Reuses the delivered shop panel / reward card / icon sprites (RewardScreenView.Load, RewardPresent)
    /// and the bundled JianHaiUI-SC font. No new PNGs: focus frame and buttons are plain uGUI colour quads.
    /// Keyboard / gamepad / mouse: arrows·WASD·left stick move, Enter·Space·A buy, Esc·B leave,
    /// hover = focus, left click = buy. 1–6 still buy a shelf directly (QA shortcut, same rules).
    /// </summary>
    public sealed class ShopScreenView : MonoBehaviour
    {
        /// <summary>True while the purchase screen is up (debug HUDs hide, like RewardScreenView.PanelOpen).</summary>
        public static bool IsOpen { get; private set; }

        public const int Columns = 3;
        public const float CardW = 150f;
        public const float CardH = 216f;
        public const float ShakeSeconds = 0.3f;
        public const string SoldText = "已售";
        public const string LeaveText = "离开";
        public const string GoldLabel = "金币";

        static readonly Color PriceOk = Color.white;
        static readonly Color PriceShort = new Color(1f, 0.32f, 0.28f);
        static readonly Color CardDim = new Color(0.45f, 0.45f, 0.45f, 1f);
        static readonly Color CardSold = new Color(0.3f, 0.3f, 0.3f, 1f);
        static readonly Color FocusColor = new Color(1f, 0.82f, 0.32f, 0.95f);
        static readonly Color LeaveIdle = new Color(0.16f, 0.12f, 0.1f, 0.92f);
        static readonly Color LeaveFocus = new Color(0.55f, 0.4f, 0.16f, 0.95f);

        public ShopSession Session { get; private set; }

        RunBuildState _build;
        Action<ShopShelf> _onBought;
        Action<ShopShelf, ShopBuyResult> _onResult;
        Action _onClosed;

        Canvas _canvas;
        Font _font;
        Image _panel;
        Text _title;
        Text _gold;
        Text _detail;
        RectTransform _leaveRt;
        Image _leaveImage;
        Card[] _cards = new Card[0];
        AudioSource _audio;
        AudioClip _deny;
        int _shakeIndex = -1;
        float _shakeUntil;
        Vector3 _lastMouse;
        bool _axisArmed = true;
        bool _leaveRequested;
        public int LastShownGold { get; private set; }

        sealed class Card
        {
            public RectTransform Root;
            public Vector2 Home;
            public Image Frame;
            public Image Body;
            public Image Icon;
            public Text Mark;
            public Text Name;
            public Text Desc;
            public Text Price;
            public Text Sold;
        }

        public void Open(ShopSession session, RunBuildState build, Action<ShopShelf> onBought,
            Action<ShopShelf, ShopBuyResult> onResult, Action onClosed)
        {
            if (session == null || build == null)
                return;
            EnsureUi();
            Session = session;
            _build = build;
            _onBought = onBought;
            _onResult = onResult;
            _onClosed = onClosed;
            BuildCards();
            session.Open(Time.unscaledTime, build.Gold);
            _shakeIndex = -1;
            _lastMouse = Input.mousePosition;
            _axisArmed = false; // a held stick at open must return to centre first
            _leaveRequested = false;
            _canvas.gameObject.SetActive(true);
            IsOpen = true;
            Refresh();
        }

        /// <summary>Close without invoking the owner callback (owner is already closing).</summary>
        public void HideSilently()
        {
            _leaveRequested = false;
            if (Session != null)
                Session.Close();
            if (_canvas != null)
                _canvas.gameObject.SetActive(false);
            IsOpen = false;
            _onClosed = null;
        }

        /// <summary>离开 button / Esc / gamepad B.</summary>
        public void Leave()
        {
            Action cb = _onClosed;
            HideSilently();
            if (cb != null)
                cb();
        }

        void OnDisable()
        {
            if (Session != null && Session.IsOpen)
                Session.Close();
            IsOpen = false;
        }

        void Update()
        {
            if (Session == null || !Session.IsOpen || _leaveRequested)
                return;
            HandleInput();
            if (Session != null && Session.IsOpen)
                Refresh();
        }

        /// <summary>
        /// Leave is applied after every Update of this frame, so the Esc / Enter / Space that closed the shop
        /// cannot also reach gameplay (dodge, clock pause, E interact) while the world is still paused.
        /// </summary>
        void LateUpdate()
        {
            if (_leaveRequested)
                Leave();
        }

        void RequestLeave()
        {
            _leaveRequested = true;
        }

        void HandleInput()
        {
            float now = Time.unscaledTime;
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton1))
            {
                RequestLeave();
                return;
            }

            int dx = 0, dy = 0;
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) dx = -1;
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) dx = 1;
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) dy = -1;
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) dy = 1;
            bool keyHeld = Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow)
                           || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow)
                           || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.D)
                           || Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.S);
            if (dx == 0 && dy == 0 && !keyHeld)
                ReadStick(ref dx, ref dy);
            if (dx != 0)
                Session.MoveFocus(dx, 0, Columns);
            else if (dy != 0)
                Session.MoveFocus(0, dy, Columns);

            // Mouse: hover = focus (only when the mouse actually moved), click = buy / leave.
            Vector3 mouse = Input.mousePosition;
            bool moved = (mouse - _lastMouse).sqrMagnitude > 0.25f;
            _lastMouse = mouse;
            int hover = HitTest(mouse);
            if (moved && hover >= 0)
                Session.Focus = hover;
            if (Input.GetMouseButtonDown(0) && hover >= 0)
            {
                Session.Focus = hover;
                Confirm(hover, now);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
                || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0))
            {
                Confirm(Session.Focus, now);
                return;
            }

            for (int i = 0; i < 6 && i < Session.LeaveIndex; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                {
                    Session.Focus = i;
                    Confirm(i, now);
                    return;
                }
            }
        }

        void ReadStick(ref int dx, ref int dy)
        {
            float h = 0f, v = 0f;
            try
            {
                h = Input.GetAxisRaw("Horizontal");
                v = Input.GetAxisRaw("Vertical");
            }
            catch (ArgumentException)
            {
                return;
            }

            float mag = Mathf.Max(Mathf.Abs(h), Mathf.Abs(v));
            if (mag < 0.3f)
            {
                _axisArmed = true;
                return;
            }

            if (!_axisArmed || mag < 0.6f)
                return;
            _axisArmed = false;
            if (Mathf.Abs(h) >= Mathf.Abs(v))
                dx = h > 0f ? 1 : -1;
            else
                dy = v > 0f ? -1 : 1; // stick up = previous row
        }

        /// <summary>One confirm. Shelf: buy (no second confirm). 离开: close. Both ignored in the open lock window.</summary>
        public ShopBuyResult Confirm(int index, float now)
        {
            if (Session == null)
                return ShopBuyResult.Invalid;
            if (index == Session.LeaveIndex)
            {
                if (Session.InputLocked(now))
                    return ShopBuyResult.Locked;
                RequestLeave();
                return ShopBuyResult.Invalid;
            }

            ShopShelf shelf;
            ShopBuyResult r = Session.TryBuy(index, now, _build, out shelf);
            if (r == ShopBuyResult.Bought && _onBought != null)
                _onBought(shelf);
            if (r == ShopBuyResult.NoGold)
            {
                _shakeIndex = index;
                _shakeUntil = now + ShakeSeconds;
                PlayDeny();
            }

            if (_onResult != null)
                _onResult(shelf, r);
            Refresh();
            return r;
        }

        int HitTest(Vector3 mouse)
        {
            for (int i = 0; i < _cards.Length; i++)
            {
                if (_cards[i] != null && RectTransformUtility.RectangleContainsScreenPoint(_cards[i].Root, mouse, null))
                    return i;
            }

            if (_leaveRt != null && RectTransformUtility.RectangleContainsScreenPoint(_leaveRt, mouse, null))
                return Session != null ? Session.LeaveIndex : -1;
            return -1;
        }

        /// <summary>Re-judge every shelf on the live gold (§4.1 即时刷新) and redraw gold / focus / detail.</summary>
        public void Refresh()
        {
            if (Session == null || _build == null || _canvas == null)
                return;
            int gold = _build.Gold;
            LastShownGold = gold;
            _gold.text = GoldLabel + " " + gold + "\nBuild " + _build.BuildCount;
            float now = Time.unscaledTime;
            for (int i = 0; i < _cards.Length; i++)
            {
                Card c = _cards[i];
                ShopSlotState st = Session.StateOf(i, gold);
                c.Body.color = st == ShopSlotState.Buyable ? Color.white : st == ShopSlotState.NoGold ? CardDim : CardSold;
                if (c.Icon != null)
                    c.Icon.color = st == ShopSlotState.Sold ? new Color(1f, 1f, 1f, 0.2f)
                        : st == ShopSlotState.NoGold ? CardDim : Color.white;
                c.Price.gameObject.SetActive(st != ShopSlotState.Sold);
                c.Price.color = st == ShopSlotState.NoGold ? PriceShort : PriceOk;
                c.Sold.gameObject.SetActive(st == ShopSlotState.Sold);
                c.Frame.enabled = Session.Focus == i;
                float dx = 0f;
                if (i == _shakeIndex && now < _shakeUntil)
                {
                    float k = (_shakeUntil - now) / ShakeSeconds;
                    dx = Mathf.Sin(now * 70f) * 9f * k;
                }

                c.Root.anchoredPosition = c.Home + new Vector2(dx, 0f);
            }

            if (_leaveImage != null)
                _leaveImage.color = Session.Focus == Session.LeaveIndex ? LeaveFocus : LeaveIdle;
            _detail.text = DetailText(Session.Focus, gold);
        }

        string DetailText(int index, int gold)
        {
            if (Session.Shelves == null || index < 0 || index >= Session.Shelves.Length)
                return "离开商店\n货架不刷新，可再次进入";
            ShopShelf s = Session.Shelves[index];
            if (s.Empty)
                return SoldText;
            RewardCardData card = RewardPresent.ToCard(s.Id, s.Tier, "", "", index, false);
            int stacks = ShopSession.OwnedStacks(_build.OwnedRewardIds, s.Id);
            string stack = ShopSession.StackLabel(s.Id);
            ShopSlotState st = Session.StateOf(index, gold);
            string state = st == ShopSlotState.Sold ? SoldText
                : st == ShopSlotState.NoGold ? "金币不足"
                : "可购买";
            return card.Name + "\n"
                   + MarkFor(s) + " · " + s.Price + GoldLabel + " · " + state + "\n"
                   + card.Desc + "\n"
                   + stack + (s.IsHeal ? "" : " · 已有 " + stacks + " 层");
        }

        static string MarkFor(ShopShelf s)
        {
            string tier = s.ContentRole == ShopSlotRole.Heal ? "回复"
                : s.ContentRole == ShopSlotRole.High ? "高级"
                : s.ContentRole == ShopSlotRole.Mid ? "中级"
                : "普通";
            return s.IsElastic ? "灵活·" + tier : tier;
        }

        void PlayDeny()
        {
            if (_audio == null)
                return;
            if (_deny == null)
                _deny = MakeDenyClip();
            _audio.PlayOneShot(_deny);
        }

        /// <summary>Short low "嘟" generated in code (no new audio asset).</summary>
        static AudioClip MakeDenyClip()
        {
            const int rate = 22050;
            int n = rate / 8;
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float env = 1f - i / (float)n;
                data[i] = Mathf.Sin(2f * Mathf.PI * 220f * t) * 0.35f * env;
            }

            AudioClip clip = AudioClip.Create("jh_shop_deny", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void EnsureUi()
        {
            if (_canvas != null)
                return;
            _font = RogueShooter.Demo.BuiltinUiFont.LoadUi();
            var root = new GameObject("ShopScreen", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            _canvas = root.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 210;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);

            _panel = MakeImage(root.transform, "Panel", Vector2.zero, new Vector2(1100f, 620f));
            _panel.sprite = RewardScreenView.Load("jh_ui_shop_panel");
            _title = AddText(root.transform, "商店 · 不刷新", 24, new Vector2(0f, 262f), 360f, TextAnchor.MiddleCenter);
            _gold = AddText(root.transform, "", 22, new Vector2(430f, 250f), 200f, TextAnchor.MiddleRight);
            _gold.rectTransform.sizeDelta = new Vector2(200f, 60f);
            _detail = AddText(root.transform, "", 20, new Vector2(300f, 40f), 400f, TextAnchor.UpperLeft);
            _detail.rectTransform.sizeDelta = new Vector2(400f, 260f);
            _detail.verticalOverflow = VerticalWrapMode.Overflow;

            _leaveImage = MakeImage(root.transform, "Leave", new Vector2(300f, -210f), new Vector2(220f, 52f));
            _leaveImage.preserveAspect = false;
            _leaveRt = _leaveImage.rectTransform;
            AddText(_leaveImage.transform, LeaveText + " (Esc)", 22, Vector2.zero, 220f, TextAnchor.MiddleCenter);

            _audio = gameObject.GetComponent<AudioSource>();
            if (_audio == null)
                _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.ignoreListenerPause = true;
            _audio.spatialBlend = 0f;
            _canvas.gameObject.SetActive(false);
        }

        void BuildCards()
        {
            for (int i = 0; i < _cards.Length; i++)
            {
                if (_cards[i] == null || _cards[i].Root == null)
                    continue;
                if (Application.isPlaying)
                    Destroy(_cards[i].Root.gameObject);
                else
                    DestroyImmediate(_cards[i].Root.gameObject);
            }

            ShopShelf[] shelves = Session.Shelves ?? new ShopShelf[0];
            _cards = new Card[shelves.Length];
            float gap = 16f;
            float x0 = -360f;
            float y0 = 118f;
            for (int i = 0; i < shelves.Length; i++)
            {
                int col = i % Columns;
                int row = i / Columns;
                var home = new Vector2(x0 + col * (CardW + gap), y0 - row * (CardH + 18f));
                _cards[i] = MakeCard(shelves[i], i, home);
            }
        }

        Card MakeCard(ShopShelf shelf, int index, Vector2 home)
        {
            var c = new Card { Home = home };
            var go = new GameObject("Shelf" + index, typeof(RectTransform));
            go.transform.SetParent(_canvas.transform, false);
            c.Root = go.GetComponent<RectTransform>();
            c.Root.anchorMin = c.Root.anchorMax = new Vector2(0.5f, 0.5f);
            c.Root.sizeDelta = new Vector2(CardW, CardH);
            c.Root.anchoredPosition = home;

            c.Frame = MakeImage(go.transform, "Focus", Vector2.zero, new Vector2(CardW + 12f, CardH + 12f));
            c.Frame.preserveAspect = false;
            c.Frame.color = FocusColor;
            c.Frame.raycastTarget = false;
            c.Body = MakeImage(go.transform, "Card", Vector2.zero, new Vector2(CardW, CardH));
            c.Body.sprite = RewardScreenView.Load(CardSprite(shelf.Tier, shelf.IsHeal));
            c.Body.raycastTarget = false;

            RewardCardData card = shelf.Empty
                ? new RewardCardData { Name = "", Desc = "", Icon = "" }
                : RewardPresent.ToCard(shelf.Id, shelf.Tier, shelf.Price + GoldLabel, MarkFor(shelf), index, false);
            if (!string.IsNullOrEmpty(card.Icon))
            {
                c.Icon = MakeImage(go.transform, "Icon", new Vector2(0f, CardH * 0.25f),
                    new Vector2(CardW * (96f / 360f), CardH * (96f / 520f)));
                c.Icon.sprite = RewardScreenView.Load(card.Icon);
                c.Icon.raycastTarget = false;
            }

            c.Mark = AddText(go.transform, MarkFor(shelf), Scale(0.07f), new Vector2(0f, CardH * 0.38f), CardW * 0.8f, TextAnchor.MiddleCenter);
            c.Name = AddText(go.transform, card.Name, Scale(0.062f), new Vector2(0f, CardH * 0.029f), CardW * (272f / 360f), TextAnchor.MiddleCenter);
            c.Desc = AddText(go.transform, card.Desc, Scale(0.046f), new Vector2(0f, -CardH * 0.204f), CardW * (292f / 360f), TextAnchor.MiddleCenter);
            c.Price = AddText(go.transform, shelf.Empty ? "" : shelf.Price + GoldLabel, Scale(0.06f), new Vector2(0f, -CardH * 0.402f), CardW * 0.6f, TextAnchor.MiddleCenter);
            c.Sold = AddText(go.transform, SoldText, Scale(0.1f), new Vector2(0f, CardH * 0.1f), CardW, TextAnchor.MiddleCenter);
            c.Sold.color = new Color(1f, 0.85f, 0.5f);
            return c;
        }

        /// <summary>Same card art mapping as the reward screen (low / mid / high). Heal uses the mid card (price tier 当量2).</summary>
        static string CardSprite(RewardTier tier, bool heal)
        {
            if (heal)
                return "jh_ui_reward_card_mid";
            switch (tier)
            {
                case RewardTier.Mid: return "jh_ui_reward_card_mid";
                case RewardTier.High: return "jh_ui_reward_card_high";
                default: return "jh_ui_reward_card_low";
            }
        }

        static int Scale(float fraction)
        {
            int size = Mathf.RoundToInt(CardH * fraction);
            return size < 10 ? 10 : size;
        }

        Text AddText(Transform parent, string value, int size, Vector2 pos, float width, TextAnchor align)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, size + 18f);
            rt.anchoredPosition = pos;
            var text = go.GetComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.alignment = align;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.text = value ?? "";
            return text;
        }

        static Image MakeImage(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var image = go.GetComponent<Image>();
            image.preserveAspect = true;
            image.color = Color.white;
            return image;
        }
    }
}
