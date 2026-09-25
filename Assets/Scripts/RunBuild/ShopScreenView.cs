using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace RogueShooter.Build
{
    /// <summary>
    /// 商店购买界面 v0.3 (制作人 09-25 版式): standalone purchase screen, not the 3-choice reward screen.
    /// LEFT: 6-row list (普通→中级→高级→回复→灵活1→灵活2), each row ONLY icon / name / price / rarity
    /// (rarity = border colour ONLY, no rarity name text anywhere; heal row = neutral border; flex = rolled tier colour).
    /// Sold rows stay, fully greyed (border too) with 「已售罄」.
    /// RIGHT: nothing selected → only 「选择一件商品查看详情」, no button. Selected → big icon, name, rarity border colour,
    /// full effect (叠加方式 + 当前层数), price, 购买 button (已售罄 > 金币不足 > 购买). Gold sits above the detail area.
    /// Mouse: click row = select (hover never selects, double-click never buys); only 购买 buys; 关闭 closes.
    /// Keyboard / gamepad: ↑↓ / left stick (first press only selects the first unsold row; clamp, no wrap, sold not skipped),
    /// left/right do nothing; A / Enter / Space = press 购买 (first press when nothing is selected only selects);
    /// B / Esc = close. Confirm ignored for <see cref="ShopSession.OpenInputLockSeconds"/> after each open.
    /// Layout = UI规格_商店_v01 (1600×900 reference): list x40 y116 640×744 (6 rows 608×112, gap 8), detail x696 y116 864×744,
    /// 购买 x968 y736 320×80, icon 96 (row) / 192 (detail). Art slots <see cref="ArtNames"/> load from Resources/<see cref="ArtFolder"/>
    /// and silently fall back to colour quads until the PNGs are delivered (9-slice → Sliced, PPU multiplier 3.125 for PPU32 art).
    /// </summary>
    public sealed class ShopScreenView : MonoBehaviour
    {
        /// <summary>True while the purchase screen is up (debug HUDs hide, like RewardScreenView.PanelOpen).</summary>
        public static bool IsOpen { get; private set; }

        // UI规格_商店_v01 (reference 1600×900, top-left rects → centred anchored positions)
        public const float RowW = 608f;
        public const float RowH = 112f;
        public const float RowGap = 8f;
        public const float RowIcon = 96f;
        public const float DetailIcon = 192f;
        public const float SlicedPpuMultiplier = 3.125f; // PPU32 art on a 100-PPU canvas
        static readonly Rect ListRect = new Rect(40f, 116f, 640f, 744f);
        static readonly Rect DetailRect = new Rect(696f, 116f, 864f, 744f);
        static readonly Rect BuyRect = new Rect(968f, 736f, 320f, 80f);

        /// <summary>Reserved art (not delivered yet): Resources/JianHaiShop/&lt;name&gt;.png. Missing → colour placeholder, no error.</summary>
        public const string ArtFolder = "JianHaiShop/";
        public const string ArtBg = "jh_ui_shop_bg";
        public const string ArtListFrame = "jh_ui_shop_list_frame";
        public const string ArtDetailFrame = "jh_ui_shop_detail_frame";
        public const string ArtRow = "jh_ui_shop_row";
        public const string ArtRowSelected = "jh_ui_shop_row_selected";
        public const string ArtRowSold = "jh_ui_shop_row_sold";
        public const string ArtBtnBuy = "jh_ui_shop_btn_buy";
        public const string ArtBtnDisabled = "jh_ui_shop_btn_disabled";
        public const string ArtBtnClose = "jh_ui_shop_btn_close";
        public const string ArtCoin = "jh_ui_shop_coin";
        public static readonly string[] ArtNames =
        {
            ArtBg, ArtListFrame, ArtDetailFrame, ArtRow, ArtRowSelected, ArtRowSold, ArtBtnBuy, ArtBtnDisabled, ArtBtnClose, ArtCoin
        };
        public const float ShakeSeconds = 0.3f;
        public const string SoldText = "已售罄";
        public const string LeaveText = "关闭";
        public const string HintText = "选择一件商品查看详情";
        public const string GoldLabel = "金币";
        public const string BuyText = "购买";
        public const string NoGoldText = "金币不足";

        // Rarity border colours (v0.3 §4.1.4, UI 定 placeholder). Hues sampled from the delivered reward-card
        // frames jh_ui_reward_card_low/mid/high (#5B6E82 / #A97935 / #5BBACB), brightened / saturated so 普通 does not read
        // as the sold grey. Heal = neutral warm white. Sold = grey border (§6).
        public static readonly Color RarityLow = new Color32(0x8C, 0xA6, 0xC4, 0xFF);
        public static readonly Color RarityMid = new Color32(0xE0, 0xA0, 0x40, 0xFF);
        public static readonly Color RarityHigh = new Color32(0x38, 0xD4, 0xF0, 0xFF);
        public static readonly Color SoldBorder = new Color32(0x40, 0x40, 0x40, 0xFF);
        /// <summary>Heal row border (configurable, <see cref="ShopUiConfig.HealColor"/>, default #5A8F7B).</summary>
        public static Color RarityNeutral => ShopUiConfig.HealColor;
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
        Image _detailBorder;
        Image _coin;
        readonly Dictionary<string, Sprite> _art = new Dictionary<string, Sprite>();
        RectTransform _leaveRt;
        GameObject _detailRoot;
        Text _hint;
        Vector2 _buyHome;
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
            public Image Border;
            public Image Bg;
            public Image Icon;
            public Text SoldTag;
            public Text Name;
            public Text Price;
            public Text HealTag;
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
                _leaveRequested = true; // cancel = close, any time
                return;
            }

            // Up/down only (left/right are ignored, v0.3 §7.2). Not time-locked (§9 假设1).
            int dy = 0;
            if (Input.GetKeyDown(KeyCode.UpArrow)) dy = -1;
            if (Input.GetKeyDown(KeyCode.DownArrow)) dy = 1;
            bool keyHeld = Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow);
            if (dy == 0 && !keyHeld)
                dy = ReadStick();
            if (dy != 0)
            {
                Session.MoveSelection(dy);
                Refresh();
            }

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
                    _leaveRequested = true;
                    return;
                }

                for (int i = 0; i < _rows.Length; i++)
                {
                    if (_rows[i] != null && Hit(_rows[i].Root, mouse))
                    {
                        Select(i); // click row = select only (a double click is two selects)
                        return;
                    }
                }
                // blank click: keep the current selection (§9 假设5)
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
                || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0))
                ConfirmKey(now);
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

        /// <summary>Mouse click on a row: select it. Never buys.</summary>
        public void Select(int index)
        {
            if (Session == null)
                return;
            Session.Select(index);
            Refresh();
        }

        /// <summary>
        /// 购买 button (mouse) — buys the selected row at once, no second confirm.
        /// Sold: no effect. Not enough gold: deny sound + button shake, gold / Build unchanged.
        /// </summary>
        public ShopBuyResult PressBuy(float now)
        {
            if (Session == null || !Session.HasSelection)
                return ShopBuyResult.Invalid;
            ShopShelf shelf;
            ShopBuyResult r = Session.TryBuy(Session.Focus, now, _build, out shelf);
            AfterPress(shelf, r, now);
            return r;
        }

        /// <summary>Confirm key (A / Enter / Space): first press with nothing selected only selects; then = 购买.</summary>
        public ShopBuyResult ConfirmKey(float now)
        {
            if (Session == null)
                return ShopBuyResult.Invalid;
            ShopShelf shelf;
            ShopBuyResult r = Session.ConfirmKey(now, _build, out shelf);
            AfterPress(shelf, r, now);
            return r;
        }

        void AfterPress(ShopShelf shelf, ShopBuyResult r, float now)
        {
            if (r == ShopBuyResult.Bought && _onBought != null)
                _onBought(shelf);
            if (r == ShopBuyResult.NoGold)
            {
                _shakeIndex = Session.Focus;
                _shakeUntil = now + ShakeSeconds;
                PlayDeny();
            }

            if (_onResult != null && r != ShopBuyResult.Invalid)
                _onResult(shelf, r);
            Refresh();
        }

        /// <summary>关闭 button / Esc / B: applied in LateUpdate.</summary>
        public void RequestClose()
        {
            _leaveRequested = true;
        }

        /// <summary>Re-judge every row on the live gold (§6.2 即时刷新) and redraw gold / selection / detail / button.</summary>
        public void Refresh()
        {
            if (Session == null || _build == null || _canvas == null)
                return;
            int gold = _build.Gold;
            LastShownGold = gold;
            _gold.text = GoldLabel + " " + gold + "   Build " + _build.BuildCount;
            for (int i = 0; i < _rows.Length; i++)
            {
                Row r = _rows[i];
                ShopShelf shelf = Session.Shelves[i];
                ShopSlotState st = Session.StateOf(i, gold);
                bool sold = st == ShopSlotState.Sold;
                bool selected = Session.Focus == i;
                Sprite rowArt = Art(selected ? ArtRowSelected : sold ? ArtRowSold : ArtRow);
                ApplySprite(r.Bg, rowArt);
                r.Bg.color = rowArt != null ? (sold && !selected ? new Color(0.6f, 0.6f, 0.6f, 1f) : Color.white)
                    : selected ? RowSelected : sold ? RowDim : RowIdle;
                if (r.HealTag != null)
                    r.HealTag.color = sold ? TextDim : RarityNeutral;
                r.Border.color = BorderColor(shelf, sold);
                r.Name.color = sold ? TextDim : Color.white;
                if (r.Icon != null)
                    r.Icon.color = sold ? new Color(1f, 1f, 1f, 0.3f) : Color.white;
                r.Price.color = sold ? TextDim : st == ShopSlotState.NoGold ? PriceShort : PriceOk;
                r.SoldTag.gameObject.SetActive(sold);
            }

            RefreshDetail(gold);
        }

        void RefreshDetail(int gold)
        {
            bool sel = Session.HasSelection;
            _hint.gameObject.SetActive(!sel);
            _detailRoot.SetActive(sel);
            _buyImage.gameObject.SetActive(sel);
            if (!sel)
            {
                _detailBorder.color = DetailBg;
                return;
            }

            int i = Session.Focus;
            ShopShelf s = Session.Shelves[i];
            ShopSlotState st = Session.StateOf(i, gold);
            _detailBorder.color = BorderColor(s, st == ShopSlotState.Sold);
            RewardCardData card = s.Empty
                ? new RewardCardData { Name = SoldText, Desc = "", Icon = "" }
                : RewardPresent.ToCard(s.Id, s.Tier, "", "", i, false);
            _detailIcon.enabled = !string.IsNullOrEmpty(card.Icon);
            if (_detailIcon.enabled)
                _detailIcon.sprite = RewardScreenView.Load(card.Icon);
            _detailIcon.color = st == ShopSlotState.Sold ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
            _detailName.text = card.Name;
            _detailMeta.text = s.Empty ? "" : s.Price + " " + GoldLabel
                + (s.IsHeal && !string.IsNullOrEmpty(ShopUiConfig.HealLabel) ? "    " + ShopUiConfig.HealLabel : "");
            _detailMeta.color = st == ShopSlotState.Sold ? TextDim : st == ShopSlotState.NoGold ? PriceShort : PriceOk;
            int stacks = ShopSession.OwnedStacks(_build.OwnedRewardIds, s.Id);
            string stack = ShopSession.StackLabel(s.Id);
            _detailBody.text = s.Empty ? "" : card.Desc + "\n效果：" + s.Effect + "\n叠加：" + stack
                + (s.IsHeal ? "" : "    当前 " + stacks + " 层");

            // §6: 已售罄 > 金币不足 > 可购买
            Sprite btnArt = Art(st == ShopSlotState.Buyable ? ArtBtnBuy : ArtBtnDisabled);
            ApplySprite(_buyImage, btnArt);
            _buyImage.color = btnArt != null ? Color.white : st == ShopSlotState.Buyable ? BtnBuyFocus : BtnDisabled;
            _buyText.text = st == ShopSlotState.Sold ? SoldText : st == ShopSlotState.NoGold ? NoGoldText : BuyText;
            _buyText.color = st == ShopSlotState.Buyable ? Color.white : TextDim;
            float now = Time.unscaledTime;
            float dx = 0f;
            if (_shakeIndex == i && now < _shakeUntil)
                dx = Mathf.Sin(now * 70f) * 8f * ((_shakeUntil - now) / ShakeSeconds);
            _buyRt.anchoredPosition = _buyHome + new Vector2(dx, 0f);
        }

        /// <summary>
        /// Rarity border colour (no text). Uses the tier the shelf actually rolled, so a flex row gets its rolled
        /// tier colour (§4.1.4 / 假设7); heal row = neutral; sold = grey border (§6 / 假设8).
        /// </summary>
        public static Color BorderColor(ShopShelf s, bool sold)
        {
            if (sold)
                return SoldBorder;
            switch (s.ContentRole)
            {
                case ShopSlotRole.Heal: return RarityNeutral;
                case ShopSlotRole.High: return RarityHigh;
                case ShopSlotRole.Mid: return RarityMid;
                default: return RarityLow;
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

        /// <summary>Top-left reference rect (1600×900) → centred anchored position.</summary>
        static Vector2 Centre(Rect r)
        {
            return new Vector2(r.x + r.width * 0.5f - 800f, 450f - (r.y + r.height * 0.5f));
        }

        Sprite Art(string name)
        {
            Sprite sp;
            if (_art.TryGetValue(name, out sp))
                return sp;
            sp = Resources.Load<Sprite>(ArtFolder + name); // silent: null until the art is delivered
            _art[name] = sp;
            return sp;
        }

        /// <summary>Assign art if present: 9-slice sprites → Sliced with PPU multiplier 3.125 (PPU32 art).</summary>
        static void ApplySprite(Image img, Sprite sp)
        {
            if (img == null)
                return;
            img.sprite = sp;
            if (sp != null && sp.border != Vector4.zero)
            {
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = SlicedPpuMultiplier;
                img.preserveAspect = false;
            }
            else
            {
                img.type = Image.Type.Simple;
            }
        }

        Image MakeFramed(Transform parent, string name, Vector2 pos, Vector2 size, string art, Color fallback)
        {
            Image img = MakeImage(parent, name, pos, size);
            img.preserveAspect = false;
            Sprite sp = Art(art);
            ApplySprite(img, sp);
            img.color = sp != null ? Color.white : fallback;
            return img;
        }

        void EnsureUi()
        {
            if (_canvas != null)
                return;
            ShopUiConfig.Load();
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

            MakeFramed(root.transform, "Bg", Vector2.zero, new Vector2(1600f, 900f), ArtBg, new Color(0f, 0f, 0f, 0.72f));
            // header band (y 0..116): title over the list, gold over the detail area, 关闭 top-right
            AddText(root.transform, "商店", 34, new Vector2(Centre(ListRect).x, 392f), 640f, TextAnchor.MiddleCenter);
            MakeFramed(root.transform, "List", Centre(ListRect), ListRect.size, ArtListFrame, DetailBg);

            Vector2 dc = Centre(DetailRect);
            _coin = MakeImage(root.transform, "Coin", new Vector2(dc.x - 200f, 392f), new Vector2(48f, 48f));
            Sprite coin = Art(ArtCoin);
            _coin.sprite = coin;
            _coin.gameObject.SetActive(coin != null);
            _gold = AddText(root.transform, "", 30, new Vector2(dc.x + 30f, 392f), 400f, TextAnchor.MiddleLeft);
            _gold.color = PriceOk;

            _detailBorder = MakeImage(root.transform, "DetailBorder", dc, DetailRect.size + new Vector2(8f, 8f));
            _detailBorder.preserveAspect = false;
            _detailBorder.raycastTarget = false;
            _detailBorder.color = DetailBg;
            Image detail = MakeFramed(root.transform, "Detail", dc, DetailRect.size, ArtDetailFrame, DetailBg);
            _hint = AddText(detail.transform, HintText, 32, Vector2.zero, 800f, TextAnchor.MiddleCenter);
            _hint.color = TextDim;
            _detailRoot = new GameObject("Selected", typeof(RectTransform));
            _detailRoot.transform.SetParent(detail.transform, false);
            float left = -DetailRect.width * 0.5f, top = DetailRect.height * 0.5f;
            _detailIcon = MakeImage(_detailRoot.transform, "Icon", new Vector2(left + 40f + DetailIcon * 0.5f, top - 40f - DetailIcon * 0.5f),
                new Vector2(DetailIcon, DetailIcon));
            float textX = left + 40f + DetailIcon + 32f; // right of the big icon
            float textW = DetailRect.width * 0.5f - 40f - textX;
            _detailName = AddText(_detailRoot.transform, "", 40, new Vector2(textX + textW * 0.5f, top - 90f), textW, TextAnchor.MiddleLeft);
            _detailMeta = AddText(_detailRoot.transform, "", 30, new Vector2(textX + textW * 0.5f, top - 160f), textW, TextAnchor.MiddleLeft);
            _detailBody = AddText(_detailRoot.transform, "", 26, new Vector2(0f, top - 272f - 140f), DetailRect.width - 80f, TextAnchor.UpperLeft);
            _detailBody.rectTransform.sizeDelta = new Vector2(DetailRect.width - 80f, 280f);
            _detailBody.verticalOverflow = VerticalWrapMode.Overflow;
            _detailBody.lineSpacing = 1.2f;

            _buyHome = Centre(BuyRect);
            _buyImage = MakeImage(root.transform, "Buy", _buyHome, BuyRect.size);
            _buyImage.preserveAspect = false;
            _buyRt = _buyImage.rectTransform;
            _buyText = AddText(_buyImage.transform, BuyText, 32, Vector2.zero, BuyRect.width, TextAnchor.MiddleCenter);

            _leaveImage = MakeFramed(root.transform, "Close", new Vector2(700f, 392f), new Vector2(120f, 56f), ArtBtnClose, BtnLeave);
            _leaveRt = _leaveImage.rectTransform;
            AddText(_leaveImage.transform, LeaveText, 26, Vector2.zero, 120f, TextAnchor.MiddleCenter);

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
            Vector2 lc = Centre(ListRect);
            float pad = (ListRect.height - (6f * RowH + 5f * RowGap)) * 0.5f; // 16px top/bottom
            float firstY = ListRect.height * 0.5f - pad - RowH * 0.5f;
            for (int i = 0; i < shelves.Length; i++)
                _rows[i] = MakeRow(shelves[i], i, new Vector2(lc.x, lc.y + firstY - i * (RowH + RowGap)));
        }

        Row MakeRow(ShopShelf shelf, int index, Vector2 home)
        {
            var r = new Row { Home = home };
            // Rarity border = outer quad in rarity colour, inner row background inset by 4px (row art goes on the inset).
            r.Border = MakeImage(_canvas.transform, "Row" + index, home, new Vector2(RowW, RowH));
            r.Border.preserveAspect = false;
            r.Root = r.Border.rectTransform;
            r.Bg = MakeImage(r.Root, "Bg", Vector2.zero, new Vector2(RowW - 8f, RowH - 8f));
            r.Bg.preserveAspect = false;
            r.Bg.raycastTarget = false;
            RewardCardData card = shelf.Empty
                ? new RewardCardData { Name = SoldText, Icon = "" }
                : RewardPresent.ToCard(shelf.Id, shelf.Tier, "", "", index, false);
            float left = -RowW * 0.5f;
            if (!string.IsNullOrEmpty(card.Icon))
            {
                r.Icon = MakeImage(r.Root, "Icon", new Vector2(left + 12f + RowIcon * 0.5f, 0f), new Vector2(RowIcon, RowIcon));
                r.Icon.sprite = RewardScreenView.Load(card.Icon);
                r.Icon.raycastTarget = false;
            }

            float nameX = left + 12f + RowIcon + 20f;
            r.Name = AddText(r.Root, card.Name, 30, new Vector2(nameX + 140f, 0f), 280f, TextAnchor.MiddleLeft);
            r.Price = AddText(r.Root, shelf.Empty ? "" : shelf.Price + " " + GoldLabel, 28,
                new Vector2(RowW * 0.5f - 20f - 70f, 0f), 140f, TextAnchor.MiddleRight);
            r.SoldTag = AddText(r.Root, SoldText, 24, new Vector2(150f, 0f), 100f, TextAnchor.MiddleCenter);
            r.SoldTag.color = new Color(1f, 0.85f, 0.5f);
            if (shelf.IsHeal && !string.IsNullOrEmpty(ShopUiConfig.HealLabel))
            {
                r.HealTag = AddText(r.Root, ShopUiConfig.HealLabel, 22, new Vector2(150f, 30f), 100f, TextAnchor.MiddleCenter);
                r.HealTag.color = RarityNeutral;
            }

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

    /// <summary>
    /// Shop UI config (StreamingAssets/JianHaiUI/shop_ui_config.csv): heal_label (empty = not shown, v0.3 §12),
    /// heal_color (#RRGGBB, default #5A8F7B from UI规格_商店_v01). Missing file / row / bad value → defaults, no error.
    /// </summary>
    public static class ShopUiConfig
    {
        public const string RelativePath = "JianHaiUI/shop_ui_config.csv";
        public static readonly Color DefaultHealColor = new Color32(0x5A, 0x8F, 0x7B, 0xFF);
        public static string HealLabel { get; private set; } = "";
        public static Color HealColor { get; private set; } = DefaultHealColor;
        public static bool Loaded { get; private set; }

        public static void Load()
        {
            HealLabel = "";
            HealColor = DefaultHealColor;
            Loaded = false;
            try
            {
                string sa = Application.streamingAssetsPath;
                if (string.IsNullOrEmpty(sa))
                    return;
                string path = Path.Combine(sa, RelativePath);
                if (!File.Exists(path))
                    return;
                Parse(File.ReadAllLines(path));
                Loaded = true;
            }
            catch (Exception)
            {
                // silent: keep defaults
            }
        }

        public static void Parse(string[] lines)
        {
            if (lines == null)
                return;
            foreach (string raw in lines)
            {
                string line = raw == null ? "" : raw.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;
                string[] cells = line.Split(',');
                if (cells.Length < 2)
                    continue;
                string key = cells[0].Trim().TrimStart('\uFEFF');
                string value = cells[1].Trim();
                if (key == "heal_label")
                    HealLabel = value;
                else if (key == "heal_color" && ColorUtility.TryParseHtmlString(value, out Color c))
                    HealColor = c;
            }
        }
    }
}
