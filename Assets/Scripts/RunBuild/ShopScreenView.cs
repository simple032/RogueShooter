using System;
using UnityEngine;
using UnityEngine.UI;

namespace RogueShooter.Build
{
    /// <summary>
    /// 商店购买界面 (制作人 09-25 版式): standalone purchase screen, not the 3-choice reward screen.
    /// LEFT: shop list — one row per shelf with ONLY icon, name, rarity, price.
    /// RIGHT: details of the selected row + 购买 button (single press buys, no second confirm) + 离开.
    /// Sold / can't-afford read clearly on the row (dimmed, 已售 / red price) and on the button (已售 / 金币不足).
    /// Placeholder styling only: delivered shop panel + reward icons (RewardScreenView.Load / RewardPresent),
    /// bundled JianHaiUI-SC font, and plain uGUI colour quads. No new PNGs (final art pending, v03 doc pending).
    /// Input: mouse click row = select, click 购买 = buy; ↑↓/W S/left stick = select row (then 离开, wraps);
    /// Enter/Space/A = press 购买 for the selected row (or 离开 when it is selected); Esc/B = leave.
    /// 1–6 = select that row and press 购买 (QA shortcut, same rules). Buy/leave presses are ignored for
    /// <see cref="ShopSession.OpenInputLockSeconds"/> after each open.
    /// </summary>
    public sealed class ShopScreenView : MonoBehaviour
    {
        /// <summary>True while the purchase screen is up (debug HUDs hide, like RewardScreenView.PanelOpen).</summary>
        public static bool IsOpen { get; private set; }

        public const float RowW = 470f;
        public const float RowH = 66f;
        public const float RowGap = 8f;
        public const float ShakeSeconds = 0.3f;
        public const string SoldText = "已售";
        public const string LeaveText = "离开";
        public const string GoldLabel = "金币";
        public const string BuyText = "购买";
        public const string NoGoldText = "金币不足";

        static readonly Color PriceOk = new Color(1f, 0.9f, 0.55f);
        static readonly Color PriceShort = new Color(1f, 0.32f, 0.28f);
        static readonly Color RowIdle = new Color(0.12f, 0.1f, 0.09f, 0.88f);
        static readonly Color RowSelected = new Color(0.42f, 0.3f, 0.13f, 0.95f);
        static readonly Color RowDim = new Color(0.08f, 0.08f, 0.08f, 0.8f);
        static readonly Color TextDim = new Color(0.55f, 0.55f, 0.55f, 1f);
        static readonly Color BtnBuy = new Color(0.2f, 0.52f, 0.24f, 0.95f);
        static readonly Color BtnBuyFocus = new Color(0.3f, 0.7f, 0.32f, 1f);
        static readonly Color BtnDisabled = new Color(0.3f, 0.3f, 0.3f, 0.9f);
        static readonly Color BtnLeave = new Color(0.16f, 0.12f, 0.1f, 0.92f);
        static readonly Color BtnLeaveFocus = new Color(0.55f, 0.4f, 0.16f, 0.95f);
        static readonly Color DetailBg = new Color(0.07f, 0.06f, 0.05f, 0.85f);

        public ShopSession Session { get; private set; }
        public Canvas Canvas => _canvas;
        public int LastShownGold { get; private set; }

        RunBuildState _build;
        Action<ShopShelf> _onBought;
        Action<ShopShelf, ShopBuyResult> _onResult;
        Action _onClosed;

        Canvas _canvas;
        Font _font;
        Text _gold;
        Image _detailIcon;
        Text _detailName;
        Text _detailMeta;
        Text _detailBody;
        Image _buyImage;
        Text _buyText;
        RectTransform _buyRt;
        Image _leaveImage;
        RectTransform _leaveRt;
        Row[] _rows = new Row[0];
        AudioSource _audio;
        AudioClip _deny;
        int _shakeIndex = -1;
        float _shakeUntil;
        bool _axisArmed = true;
        bool _leaveRequested;

        sealed class Row
        {
            public RectTransform Root;
            public Vector2 Home;
            public Image Bg;
            public Image Icon;
            public Text Name;
            public Text Rarity;
            public Text Price;
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
            BuildRows();
            session.Open(Time.unscaledTime, build.Gold);
            _shakeIndex = -1;
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

        void HandleInput()
        {
            float now = Time.unscaledTime;
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton1))
            {
                _leaveRequested = true;
                return;
            }

            int dy = 0;
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) dy = -1;
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) dy = 1;
            bool keyHeld = Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow)
                           || Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.S);
            if (dy == 0 && !keyHeld)
                dy = ReadStick();
            if (dy != 0)
                Select(Session.MoveFocus(dy, 0, 1));

            if (Input.GetMouseButtonDown(0))
            {
                Vector3 mouse = Input.mousePosition;
                if (Hit(_buyRt, mouse))
                {
                    PressBuy(now);
                    return;
                }

                if (Hit(_leaveRt, mouse))
                {
                    Select(Session.LeaveIndex);
                    PressLeave(now);
                    return;
                }

                for (int i = 0; i < _rows.Length; i++)
                {
                    if (_rows[i] != null && Hit(_rows[i].Root, mouse))
                    {
                        Select(i); // click row = select only
                        return;
                    }
                }
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
                || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0))
            {
                if (Session.Focus == Session.LeaveIndex)
                    PressLeave(now);
                else
                    PressBuy(now);
                return;
            }

            for (int i = 0; i < 6 && i < Session.LeaveIndex; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                {
                    Confirm(i, now);
                    return;
                }
            }
        }

        int ReadStick()
        {
            float v;
            try
            {
                v = Input.GetAxisRaw("Vertical");
            }
            catch (ArgumentException)
            {
                return 0;
            }

            float mag = Mathf.Abs(v);
            if (mag < 0.3f)
            {
                _axisArmed = true;
                return 0;
            }

            if (!_axisArmed || mag < 0.6f)
                return 0;
            _axisArmed = false;
            return v > 0f ? -1 : 1; // stick up = previous row
        }

        static bool Hit(RectTransform rt, Vector3 mouse)
        {
            return rt != null && rt.gameObject.activeInHierarchy
                   && RectTransformUtility.RectangleContainsScreenPoint(rt, mouse, null);
        }

        /// <summary>Select a row (or <see cref="ShopSession.LeaveIndex"/>). Never buys.</summary>
        public void Select(int index)
        {
            if (Session == null)
                return;
            if (index < 0 || index > Session.LeaveIndex)
                return;
            Session.Focus = index;
            Refresh();
        }

        /// <summary>购买 button: buys the selected row at once (no second confirm). Ignored in the open lock window.</summary>
        public ShopBuyResult PressBuy(float now)
        {
            if (Session == null)
                return ShopBuyResult.Invalid;
            int index = Session.Focus;
            if (index < 0 || index >= Session.LeaveIndex)
                return ShopBuyResult.Invalid;
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

        /// <summary>离开 button press (lock window applies; Esc / B always leave).</summary>
        public bool PressLeave(float now)
        {
            if (Session == null || Session.InputLocked(now))
                return false;
            _leaveRequested = true;
            return true;
        }

        /// <summary>Select <paramref name="index"/> then press 购买 (or 离开 for the leave index).</summary>
        public ShopBuyResult Confirm(int index, float now)
        {
            if (Session == null)
                return ShopBuyResult.Invalid;
            Select(index);
            if (index == Session.LeaveIndex)
                return PressLeave(now) ? ShopBuyResult.Invalid : ShopBuyResult.Locked;
            return PressBuy(now);
        }

        /// <summary>Re-judge every row on the live gold (即时刷新) and redraw gold / selection / detail / button.</summary>
        public void Refresh()
        {
            if (Session == null || _build == null || _canvas == null)
                return;
            int gold = _build.Gold;
            LastShownGold = gold;
            _gold.text = GoldLabel + " " + gold + "   Build " + _build.BuildCount;
            float now = Time.unscaledTime;
            for (int i = 0; i < _rows.Length; i++)
            {
                Row r = _rows[i];
                ShopSlotState st = Session.StateOf(i, gold);
                bool selected = Session.Focus == i;
                r.Bg.color = selected ? RowSelected : st == ShopSlotState.Sold ? RowDim : RowIdle;
                Color text = st == ShopSlotState.Buyable ? Color.white : TextDim;
                r.Name.color = text;
                r.Rarity.color = st == ShopSlotState.Buyable ? RarityColor(Session.Shelves[i]) : TextDim;
                if (r.Icon != null)
                    r.Icon.color = st == ShopSlotState.Sold ? new Color(1f, 1f, 1f, 0.25f)
                        : st == ShopSlotState.NoGold ? new Color(0.6f, 0.6f, 0.6f, 1f) : Color.white;
                r.Price.text = st == ShopSlotState.Sold ? SoldText : Session.Shelves[i].Price + " " + GoldLabel;
                r.Price.color = st == ShopSlotState.Sold ? TextDim : st == ShopSlotState.NoGold ? PriceShort : PriceOk;
                float dx = 0f;
                if (i == _shakeIndex && now < _shakeUntil)
                    dx = Mathf.Sin(now * 70f) * 9f * ((_shakeUntil - now) / ShakeSeconds);
                r.Root.anchoredPosition = r.Home + new Vector2(dx, 0f);
            }

            RefreshDetail(gold);
            _leaveImage.color = Session.Focus == Session.LeaveIndex ? BtnLeaveFocus : BtnLeave;
        }

        void RefreshDetail(int gold)
        {
            int i = Session.Focus;
            bool shelfSel = Session.Shelves != null && i >= 0 && i < Session.Shelves.Length;
            if (!shelfSel)
            {
                _detailIcon.enabled = false;
                _detailName.text = LeaveText;
                _detailMeta.text = "";
                _detailBody.text = "货架不刷新，离开后可再次进入";
                _buyImage.color = BtnDisabled;
                _buyText.text = BuyText;
                return;
            }

            ShopShelf s = Session.Shelves[i];
            ShopSlotState st = Session.StateOf(i, gold);
            if (s.Empty)
            {
                _detailIcon.enabled = false;
                _detailName.text = SoldText;
                _detailMeta.text = "";
                _detailBody.text = "";
            }
            else
            {
                RewardCardData card = RewardPresent.ToCard(s.Id, s.Tier, "", "", i, false);
                _detailIcon.enabled = true;
                _detailIcon.sprite = RewardScreenView.Load(card.Icon);
                _detailIcon.color = st == ShopSlotState.Sold ? new Color(1f, 1f, 1f, 0.3f) : Color.white;
                _detailName.text = card.Name;
                _detailMeta.text = RarityText(s) + " · " + s.Price + " " + GoldLabel;
                _detailMeta.color = st == ShopSlotState.NoGold ? PriceShort : PriceOk;
                int stacks = ShopSession.OwnedStacks(_build.OwnedRewardIds, s.Id);
                string stack = ShopSession.StackLabel(s.Id);
                _detailBody.text = card.Desc + "\n" + s.Effect + "\n" + stack
                                   + (s.IsHeal ? "" : " · 已有 " + stacks + " 层");
            }

            bool focusBuy = st == ShopSlotState.Buyable;
            _buyImage.color = focusBuy ? BtnBuyFocus : BtnDisabled;
            _buyText.text = st == ShopSlotState.Sold ? SoldText
                : st == ShopSlotState.NoGold ? NoGoldText + "（" + s.Price + "）"
                : BuyText + "  " + s.Price + " " + GoldLabel;
            _buyText.color = st == ShopSlotState.NoGold ? PriceShort : Color.white;
        }

        public static string RarityText(ShopShelf s)
        {
            string tier = s.ContentRole == ShopSlotRole.Heal ? "回复"
                : s.ContentRole == ShopSlotRole.High ? "高级"
                : s.ContentRole == ShopSlotRole.Mid ? "中级"
                : "普通";
            return s.IsElastic ? tier + "·灵活" : tier;
        }

        static Color RarityColor(ShopShelf s)
        {
            switch (s.ContentRole)
            {
                case ShopSlotRole.Heal: return new Color(0.5f, 0.95f, 0.55f);
                case ShopSlotRole.High: return new Color(1f, 0.7f, 0.3f);
                case ShopSlotRole.Mid: return new Color(0.55f, 0.75f, 1f);
                default: return new Color(0.85f, 0.85f, 0.85f);
            }
        }

        void PlayDeny()
        {
            if (_audio == null)
                return;
            if (_deny == null)
                _deny = MakeDenyClip();
            _audio.PlayOneShot(_deny);
        }

        /// <summary>Short low beep generated in code (no new audio asset).</summary>
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
            scaler.matchWidthOrHeight = 0.5f;

            Image panel = MakeImage(root.transform, "Panel", Vector2.zero, new Vector2(1100f, 620f));
            panel.sprite = RewardScreenView.Load("jh_ui_shop_panel");
            if (panel.sprite == null)
                panel.color = DetailBg;
            AddText(root.transform, "商店 · 不刷新", 24, new Vector2(0f, 262f), 360f, TextAnchor.MiddleCenter);
            _gold = AddText(root.transform, "", 22, new Vector2(390f, 262f), 260f, TextAnchor.MiddleRight);
            _gold.color = PriceOk;

            // Right: detail panel + buttons.
            Image detail = MakeImage(root.transform, "Detail", new Vector2(262f, 20f), new Vector2(430f, 400f));
            detail.preserveAspect = false;
            detail.color = DetailBg;
            _detailIcon = MakeImage(detail.transform, "Icon", new Vector2(-150f, 140f), new Vector2(96f, 96f));
            _detailName = AddText(detail.transform, "", 28, new Vector2(50f, 158f), 290f, TextAnchor.MiddleLeft);
            _detailMeta = AddText(detail.transform, "", 20, new Vector2(50f, 118f), 290f, TextAnchor.MiddleLeft);
            _detailBody = AddText(detail.transform, "", 20, new Vector2(0f, -20f), 390f, TextAnchor.UpperLeft);
            _detailBody.rectTransform.sizeDelta = new Vector2(390f, 200f);
            _detailBody.verticalOverflow = VerticalWrapMode.Overflow;
            _detailBody.lineSpacing = 1.2f;

            _buyImage = MakeImage(root.transform, "Buy", new Vector2(262f, -222f), new Vector2(300f, 56f));
            _buyImage.preserveAspect = false;
            _buyRt = _buyImage.rectTransform;
            _buyText = AddText(_buyImage.transform, BuyText, 24, Vector2.zero, 300f, TextAnchor.MiddleCenter);

            _leaveImage = MakeImage(root.transform, "Leave", new Vector2(460f, -222f), new Vector2(80f, 56f));
            _leaveImage.preserveAspect = false;
            _leaveRt = _leaveImage.rectTransform;
            AddText(_leaveImage.transform, LeaveText, 22, Vector2.zero, 80f, TextAnchor.MiddleCenter);

            _audio = gameObject.GetComponent<AudioSource>();
            if (_audio == null)
                _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.ignoreListenerPause = true;
            _audio.spatialBlend = 0f;
            _canvas.gameObject.SetActive(false);
        }

        void BuildRows()
        {
            for (int i = 0; i < _rows.Length; i++)
            {
                if (_rows[i] == null || _rows[i].Root == null)
                    continue;
                if (Application.isPlaying)
                    Destroy(_rows[i].Root.gameObject);
                else
                    DestroyImmediate(_rows[i].Root.gameObject);
            }

            ShopShelf[] shelves = Session.Shelves ?? new ShopShelf[0];
            _rows = new Row[shelves.Length];
            float top = 200f;
            for (int i = 0; i < shelves.Length; i++)
                _rows[i] = MakeRow(shelves[i], i, new Vector2(-250f, top - i * (RowH + RowGap)));
        }

        Row MakeRow(ShopShelf shelf, int index, Vector2 home)
        {
            var r = new Row { Home = home };
            r.Bg = MakeImage(_canvas.transform, "Row" + index, home, new Vector2(RowW, RowH));
            r.Bg.preserveAspect = false;
            r.Root = r.Bg.rectTransform;
            RewardCardData card = shelf.Empty
                ? new RewardCardData { Name = SoldText, Icon = "" }
                : RewardPresent.ToCard(shelf.Id, shelf.Tier, "", "", index, false);
            if (!string.IsNullOrEmpty(card.Icon))
            {
                r.Icon = MakeImage(r.Root, "Icon", new Vector2(-RowW * 0.5f + 36f, 0f), new Vector2(52f, 52f));
                r.Icon.sprite = RewardScreenView.Load(card.Icon);
                r.Icon.raycastTarget = false;
            }

            r.Name = AddText(r.Root, card.Name, 24, new Vector2(-40f, 0f), 220f, TextAnchor.MiddleLeft);
            r.Rarity = AddText(r.Root, RarityText(shelf), 18, new Vector2(95f, 0f), 100f, TextAnchor.MiddleCenter);
            r.Price = AddText(r.Root, "", 22, new Vector2(RowW * 0.5f - 60f, 0f), 110f, TextAnchor.MiddleRight);
            return r;
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
