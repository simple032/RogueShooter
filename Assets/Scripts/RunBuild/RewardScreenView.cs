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
        public bool Disabled;
    }

    /// <summary>
    /// Screen-center reward UI using the delivered panel, cards, and 6-frame clips.
    /// </summary>
    public sealed class RewardScreenView : MonoBehaviour
    {
        public const float PanelWidth = 1600f;
        public const float PanelHeight = 760f;
        /// <summary>
        /// Opaque card frame over the three dark slots of jh_ui_reward_panel_3choice (1600×760).
        /// Slot interiors x 287–597, 645–955, 1003–1313 and y 155–606. Card padding is 8,18,4,16.
        /// </summary>
        public static readonly Vector2 CardSize = new Vector2(335.2f, 470.1f);
        public static readonly Vector2[] CardSlots =
        {
            new Vector2(-352.9f, -6.4f),
            new Vector2(5.1f, -6.4f),
            new Vector2(363.1f, -6.4f),
        };

        public RewardScreenSession Session { get; private set; }
        public bool ChoicesVisible => Session != null && Session.ChoicesVisible;

        ChestAltarDirector _director;
        public System.Action<int> OnPick;
        Canvas _canvas;
        Image _clip;
        GameObject _choiceRoot;
        Image _panel;
        Font _font;

        public void Bind(ChestAltarDirector director)
        {
            _director = director;
            if (Session == null)
                Session = new RewardScreenSession();
        }

        public void ShowChest(RewardCardData[] cards)
        {
            EnsureUi();
            if (Session == null)
                Session = new RewardScreenSession();
            if(!Session.Open)RogueShooter.Audio.JianHaiAudio.Emit("jh_ui_open",this);
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
            if (Session == null)
                Session = new RewardScreenSession();
            if(!Session.Open)RogueShooter.Audio.JianHaiAudio.Emit("jh_ui_open",this);
            Session.OpenAltar();
            ApplyPause(true);
            _clip.gameObject.SetActive(true);
            _choiceRoot.SetActive(false);
            ApplyFrame();
            BuildCards(cards, shop: false);
        }

        public void ShowShop(RewardCardData[] cards, string caption = "遗库补给 · 1–6 购买 · E / Esc 关闭")
        {
            EnsureUi();
            if(Session==null) Session=new RewardScreenSession();
            if(!Session.Open)RogueShooter.Audio.JianHaiAudio.Emit("jh_ui_open",this);
            Session.OpenShop();
            ApplyPause(true);
            _clip.gameObject.SetActive(false);
            _choiceRoot.SetActive(true);
            _panel.sprite = Load("jh_ui_shop_panel");
            _panel.rectTransform.sizeDelta = new Vector2(1100f, 620f);
            _panel.preserveAspect=false;
            BuildCards(cards, shop: true);
            // Fit the delivered 1600x900 panel's header and currency slots.
            var parts=(caption??"").Split('·');
            AddText(_choiceRoot.transform,parts.Length>0?parts[0].Trim():"遗库补给",24,new Vector2(0,262),350,44);
            string gold=parts.Length>1&&parts[1].Contains("金")?parts[1].Trim():"补给";
            AddText(_choiceRoot.transform,gold,20,new Vector2(388,253),155,32);
            AddText(_choiceRoot.transform,"1–6 购买 · E / Esc 关闭",18,new Vector2(0,-274),800,28);
        }

        public void Hide()
        {
            if(Session!=null&&Session.Open)RogueShooter.Audio.JianHaiAudio.Emit("jh_ui_close",this);
            if (Session != null)
                Session.Close();
            ApplyPause(false);
            if (_canvas != null)
                _canvas.gameObject.SetActive(false);
        }

        void Update()
        {
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
                _panel.rectTransform.sizeDelta = new Vector2(PanelWidth, PanelHeight);
                _panel.preserveAspect = false;
            }
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
            if (paused)
                Time.timeScale = 0f;
            else if (!RunPause.RunSettled)
                Time.timeScale = 1f;
            if (_canvas != null)
                _canvas.gameObject.SetActive(paused || (Session != null && Session.Open));
        }

        void EnsureUi()
        {
            if (_canvas != null)
                return;
            _font = Font.CreateDynamicFontFromOSFont("Microsoft YaHei UI", 32);
            if (_font == null)
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                var events = new GameObject("EventSystem");
                events.AddComponent<UnityEngine.EventSystems.EventSystem>();
                events.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            var root = new GameObject("RewardScreen", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            _canvas = root.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 200;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            var veil=MakeImage(root.transform,"Modal backdrop",Vector2.zero,Vector2.one);
            veil.rectTransform.anchorMin=Vector2.zero;veil.rectTransform.anchorMax=Vector2.one;
            veil.rectTransform.offsetMin=veil.rectTransform.offsetMax=Vector2.zero;
            veil.color=new Color(.008f,.014f,.022f,.72f);veil.raycastTarget=true;

            _clip = MakeImage(root.transform, "Clip", Vector2.zero, new Vector2(256f, 256f));
            _choiceRoot = new GameObject("Choices", typeof(RectTransform));
            _choiceRoot.transform.SetParent(root.transform, false);
            var choiceRt = _choiceRoot.GetComponent<RectTransform>();
            choiceRt.anchorMin = choiceRt.anchorMax = new Vector2(0.5f, 0.5f);
            choiceRt.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            _panel = MakeImage(_choiceRoot.transform, "Panel", Vector2.zero, new Vector2(PanelWidth, PanelHeight));
            _choiceRoot.SetActive(false);
        }

        void BuildCards(RewardCardData[] cards, bool shop)
        {
            for (int i = _choiceRoot.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = _choiceRoot.transform.GetChild(i);
                if (child != _panel.transform)
                    { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            }

            if (cards == null)
                return;
            if(!shop)AddText(_choiceRoot.transform,"选择强化",24,new Vector2(0,PanelHeight*.425f),500f);
            int n = cards.Length;
            float cardW = shop ? 288f : CardSize.x;
            float cardH = shop ? 198f : CardSize.y;
            float gap = shop ? 24f : 24f;
            float total = n * cardW + (n - 1) * gap;
            float x0 = -total * 0.5f + cardW * 0.5f;
            bool slotted = !shop && n == CardSlots.Length;
            for (int i = 0; i < n; i++)
            {
                RewardCardData card = cards[i];
                var go = new GameObject("Card" + i, typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(_choiceRoot.transform, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(cardW, cardH);
                rt.anchoredPosition = shop ? new Vector2((i%3-1)*330f,i<3?100f:-134f) : slotted ? CardSlots[i] : new Vector2(x0+i*(cardW+gap),0f);
                var image = go.GetComponent<Image>();
                image.sprite = Load(CardSprite(card.Tier));
                image.type = Image.Type.Simple;
                image.material = TierMaterial(card.Tier);
                image.preserveAspect = !shop && !slotted;
                AddTierFrame(go.transform, cardW, cardH, card.Tier, card.Disabled);
                var button=go.GetComponent<Button>();button.targetGraphic=image;button.interactable=!card.Disabled;
                var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.12f,1.08f,.98f);colors.pressedColor=new Color(.72f,.68f,.60f);colors.disabledColor=new Color(.35f,.35f,.35f);colors.fadeDuration=.12f;button.colors=colors;
                var hover=go.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                var entry=new UnityEngine.EventSystems.EventTrigger.Entry{eventID=UnityEngine.EventSystems.EventTriggerType.PointerEnter};
                entry.callback.AddListener(_ => {if(!card.Disabled)RogueShooter.Audio.JianHaiAudio.Emit("jh_ui_move",this);});hover.triggers.Add(entry);
                int index = card.Index;
                go.GetComponent<Button>().onClick.AddListener(() =>
                {
                    if (_director != null)
                        _director.NotifyUiPick(index);
                    else if (OnPick != null)
                        OnPick(index);
                });
                if (!string.IsNullOrEmpty(card.Icon))
                {
                    float iconW = shop ? 44f : 96f;
                    float iconH = iconW;
                    Image icon = MakeImage(go.transform, "Icon", new Vector2(0f, cardH * (shop ? .245f : .23f)), new Vector2(iconW, iconH));
                    icon.sprite = Load(card.Icon);
                    icon.raycastTarget = false;
                }
                AddText(go.transform, card.Mark, shop ? 12 : 24, new Vector2(0f, cardH * (shop ? .43f : .39f)), cardW * 0.8f,shop?24f:0f);
                AddText(go.transform, card.Name, shop ? 18 : 28, new Vector2(0f, cardH * (shop ? .033f : .029f)), cardW * (272f / 360f),shop?28f:0f);
                Image cleanDesc=MakeImage(go.transform,"Clean description",new Vector2(0f,shop?-38f:-cardH*.204f),new Vector2(cardW*.80f,shop?50f:cardH*.235f));
                cleanDesc.color=new Color(.12f,.105f,.085f,1f);cleanDesc.raycastTarget=false;
                AddText(go.transform, card.Desc, shop ? 14 : 22, new Vector2(0f, shop?-38f:-cardH * 0.204f), cardW * (292f / 360f),shop?50f:cardH*.23f);
                if (!shop && string.IsNullOrEmpty(card.Price))
                    AddText(go.transform, "["+(card.Index+1)+"]",18,new Vector2(0f,-cardH*.402f),cardW*.5f);
                if (!string.IsNullOrEmpty(card.Price))
                    AddText(go.transform, card.Price, shop ? 15 : Scale(cardH, 0.05f), new Vector2(0f, -cardH * 0.402f), cardW * (176f / 360f),shop?24f:0f);
            }
        }

        static readonly Material[] TierMaterials = new Material[3];
        static Material TierMaterial(RewardTier tier)
        {
            int index=(int)tier;
            if(TierMaterials[index]!=null)return TierMaterials[index];
            var shader=Resources.Load<Shader>("JianHaiReward/JianHaiRewardTier");
            if(shader==null)return null;
            var material=new Material(shader){name="Reward "+AltarRewardRoll.TierLabel(tier),hideFlags=HideFlags.HideAndDontSave};
            material.SetColor("_MetalColor",TierColor(tier));TierMaterials[index]=material;return material;
        }
        public static Color TierColor(RewardTier tier) => tier == RewardTier.High ? new Color(1f,.78f,.26f)
            : tier == RewardTier.Mid ? new Color(.76f,.85f,.94f) : new Color(.79f,.46f,.27f);
        void AddTierFrame(Transform parent, float w, float h, RewardTier tier, bool disabled)
        {
            Color color = TierColor(tier) * (disabled ? .45f : 1f); color.a=1f;
            // Only the outer rails are filled; the center and existing artwork stay untouched.
            foreach (var side in new[] { new Vector4(0,h*.5f,w,3), new Vector4(0,-h*.5f,w,3),
                new Vector4(-w*.5f,0,3,h), new Vector4(w*.5f,0,3,h) })
            {
                var rail=MakeImage(parent,"Tier frame",new Vector2(side.x,side.y),new Vector2(side.z,side.w));
                rail.color=color;rail.raycastTarget=false;
            }
            AddText(parent,AltarRewardRoll.TierLabel(tier),18,new Vector2(-w*.39f,h*.39f),32,26);
            var label=parent.GetChild(parent.childCount-1).GetComponent<Text>();label.color=color;
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

        void AddText(Transform parent, string value, int size, Vector2 pos, float width,float height=0f)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, height>0?height:size+18f);
            rt.anchoredPosition = pos;
            var text = go.GetComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.raycastTarget=false;
            if(height>0){text.resizeTextForBestFit=false;text.lineSpacing=1.1f;}
            text.alignment = TextAnchor.MiddleCenter;
            text.color = RogueShooter.Demo.JianHaiUiTheme.Bone;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.text = value ?? "";
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
