using UnityEngine;
using UnityEngine.UI;
using RogueShooter.Player;

namespace RogueShooter.Build
{
    public struct RewardCardData
    {
        public RewardTier Tier;
        public string Name;
        public string Desc;
        public string Price;
        public string Mark;
        public int Index;
        public string Icon;
    }

    /// <summary>
    /// Screen-center reward UI using the delivered panel, cards, and 6-frame clips.
    /// </summary>
    public sealed class RewardScreenView : MonoBehaviour
    {
        /// <summary>True while any reward/shop panel is up. Debug HUDs hide so they never cover cards.</summary>
        public static bool PanelOpen { get; private set; }

        public RewardScreenSession Session { get; private set; }
        public bool ChoicesVisible => Session != null && Session.ChoicesVisible;

        ChestAltarDirector _director;
        Canvas _canvas;
        Image _clip;
        GameObject _choiceRoot;
        Image _panel;
        Text _title;
        Text _coin;
        int _coinShown = int.MinValue;
        Font _font;

        /// <summary>Chest / altar 3-choice title bar copy.</summary>
        public const string ChoiceTitle = "选择强化";

        public void Bind(ChestAltarDirector director)
        {
            _director = director;
            if (Session == null)
                Session = new RewardScreenSession();
        }

        public void ShowChest(RewardCardData[] cards)
        {
            EnsureUi();
            Session.OpenChest();
            ApplyPause(true);
            _clip.gameObject.SetActive(true);
            _choiceRoot.SetActive(false);
            ApplyFrame();
            BuildCards(cards, shop: false);
        }

        public void ShowAltar(RewardCardData[] cards)
        {
            EnsureUi();
            Session.OpenAltar();
            ApplyPause(true);
            _clip.gameObject.SetActive(true);
            _choiceRoot.SetActive(false);
            ApplyFrame();
            BuildCards(cards, shop: false);
        }

        public void ShowShop(RewardCardData[] cards)
        {
            EnsureUi();
            Session.OpenShop();
            ApplyPause(true);
            _clip.gameObject.SetActive(false);
            _choiceRoot.SetActive(true);
            _panel.sprite = Load("jh_ui_shop_panel");
            _panel.rectTransform.sizeDelta = new Vector2(1100f, 620f);
            BuildCards(cards, shop: true);
            // Title count comes from the shelves actually shown (ShopStock.ShelfCount = 6).
            // Sits in the shop panel's gold title bar (panel 1100x620, bar centre ≈ +262).
            int n = cards != null ? cards.Length : 0;
            _title = AddText(_choiceRoot.transform, ShopTitle(n), 24, new Vector2(0f, 262f), 360f);
            // Coin box (top-right of jh_ui_shop_panel: 1600x900 art → 1100x620, box text area
            // right of the coin dot ≈ (+405, +253), pixel-measured). Refreshed every frame from held gold.
            _coin = AddText(_choiceRoot.transform, "", 22, CoinBoxPos, 124f);
            _coin.alignment = TextAnchor.MiddleLeft;
            _coin.color = new Color(1f, 0.86f, 0.45f, 1f);
            _coinShown = int.MinValue;
            RefreshCoin();
        }

        public static readonly Vector2 CoinBoxPos = new Vector2(405f, 253f);

        /// <summary>Coin box copy: the held coin count only (the art already has the coin dot).</summary>
        public static string CoinText(int gold)
        {
            return gold.ToString();
        }

        /// <summary>Current coin box text ("" when the shop is not open). Tests read this.</summary>
        public string CoinLabel => _coin != null ? _coin.text : "";

        void RefreshCoin()
        {
            if (_coin == null || _director == null)
                return;
            int gold = _director.CurrentGold;
            if (gold == _coinShown)
                return;
            _coinShown = gold;
            _coin.text = CoinText(gold);
        }

        public static string ShopTitle(int shelfCount)
        {
            return "商店 · " + shelfCount + " 件商品 · 不刷新";
        }

        public void Hide()
        {
            if (Session != null)
                Session.Close();
            ApplyPause(false);
            if (_canvas != null)
                _canvas.gameObject.SetActive(false);
        }

        void Update()
        {
            if (Session != null && Session.Open && Session.Kind == RewardScreenKind.Shop)
                RefreshCoin();
            if (Session == null || !Session.Open || Session.ChoicesVisible)
                return;
            Session.Tick(Time.unscaledDeltaTime);
            ApplyFrame();
            if (Session.ChoicesVisible)
                RevealChoices();
        }

        public void Advance(float unscaledDelta)
        {
            if (Session == null)
                return;
            Session.Tick(unscaledDelta);
            ApplyFrame();
            if (Session.ChoicesVisible && Session.Kind != RewardScreenKind.Shop)
                RevealChoices();
        }

        void RevealChoices()
        {
            if (_clip != null)
                _clip.gameObject.SetActive(false);
            if (_choiceRoot != null)
                _choiceRoot.SetActive(true);
            if (_panel != null)
            {
                _panel.sprite = Load("jh_ui_reward_panel_3choice");
                _panel.rectTransform.sizeDelta = new Vector2(1000f, 475f);
            }

            // Fill the panel's gold title bar (panel 1000x475, bar centre ≈ +199). Once per open.
            if (_title == null && _choiceRoot != null)
                _title = AddText(_choiceRoot.transform, ChoiceTitle, 26, new Vector2(0f, 199f), 340f);
        }

        void ApplyFrame()
        {
            if (_clip == null || Session == null || string.IsNullOrEmpty(Session.ClipId))
                return;
            string id = Session.ClipId + Session.AnimFrame.ToString("00");
            Sprite sprite = Load(id);
            if (sprite != null)
                _clip.sprite = sprite;
        }

        void ApplyPause(bool paused)
        {
            RunPause.InteractOpen = paused;
            PanelOpen = paused;
            Time.timeScale = paused ? 0f : 1f;
            if (_canvas != null)
                _canvas.gameObject.SetActive(paused || (Session != null && Session.Open));
        }

        void OnDisable()
        {
            PanelOpen = false;
        }

        void EnsureUi()
        {
            if (_canvas != null)
                return;
            // Bundled project font first (not Windows-only); see BuiltinUiFont.LoadUi.
            _font = RogueShooter.Demo.BuiltinUiFont.LoadUi();

            var root = new GameObject("RewardScreen", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            _canvas = root.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 200;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);

            _clip = MakeImage(root.transform, "Clip", Vector2.zero, new Vector2(256f, 256f));
            _choiceRoot = new GameObject("Choices", typeof(RectTransform));
            _choiceRoot.transform.SetParent(root.transform, false);
            var choiceRt = _choiceRoot.GetComponent<RectTransform>();
            choiceRt.anchorMin = choiceRt.anchorMax = new Vector2(0.5f, 0.5f);
            choiceRt.sizeDelta = new Vector2(1200f, 700f);
            _panel = MakeImage(_choiceRoot.transform, "Panel", Vector2.zero, new Vector2(1000f, 475f));
            _choiceRoot.SetActive(false);
        }

        void BuildCards(RewardCardData[] cards, bool shop)
        {
            for (int i = _choiceRoot.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = _choiceRoot.transform.GetChild(i);
                if (child != _panel.transform)
                    Destroy(child.gameObject);
            }
            _title = null;
            _coin = null;

            if (cards == null)
                return;
            int n = cards.Length;
            float cardW = shop ? 150f : 200f;
            float cardH = shop ? 216f : 288f;
            float gap = shop ? 12f : 24f;
            float total = n * cardW + (n - 1) * gap;
            float x0 = -total * 0.5f + cardW * 0.5f;
            for (int i = 0; i < n; i++)
            {
                RewardCardData card = cards[i];
                var go = new GameObject("Card" + i, typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(_choiceRoot.transform, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(cardW, cardH);
                rt.anchoredPosition = new Vector2(x0 + i * (cardW + gap), shop ? -10f : 0f);
                var image = go.GetComponent<Image>();
                image.sprite = Load(CardSprite(card.Tier));
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
                int index = card.Index;
                go.GetComponent<Button>().onClick.AddListener(() =>
                {
                    if (_director != null)
                        _director.NotifyUiPick(index);
                });
                if (!string.IsNullOrEmpty(card.Icon))
                {
                    float iconW = cardW * (96f / 360f);
                    float iconH = cardH * (96f / 520f);
                    Image icon = MakeImage(go.transform, "Icon", new Vector2(0f, cardH * 0.25f), new Vector2(iconW, iconH));
                    icon.sprite = Load(card.Icon);
                    icon.raycastTarget = false;
                }
                AddText(go.transform, card.Mark, Scale(cardH, 0.07f), new Vector2(0f, cardH * 0.38f), cardW * 0.7f);
                AddText(go.transform, card.Name, Scale(cardH, 0.062f), new Vector2(0f, cardH * 0.029f), cardW * (272f / 360f));
                AddText(go.transform, card.Desc, Scale(cardH, 0.046f), new Vector2(0f, -cardH * 0.204f), cardW * (292f / 360f));
                if (!string.IsNullOrEmpty(card.Price))
                    AddText(go.transform, card.Price, Scale(cardH, 0.05f), new Vector2(0f, -cardH * 0.402f), cardW * (176f / 360f));
            }
        }

        static int Scale(float cardH, float fraction)
        {
            int size = Mathf.RoundToInt(cardH * fraction);
            return size < 10 ? 10 : size;
        }

        static string CardSprite(RewardTier tier)
        {
            switch (tier)
            {
                case RewardTier.Mid: return "jh_ui_reward_card_mid";
                case RewardTier.High: return "jh_ui_reward_card_high";
                default: return "jh_ui_reward_card_low";
            }
        }

        Text AddText(Transform parent, string value, int size, Vector2 pos, float width)
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
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
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

        public static Sprite Load(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return Resources.Load<Sprite>("JianHaiReward/" + id);
        }
    }
}
