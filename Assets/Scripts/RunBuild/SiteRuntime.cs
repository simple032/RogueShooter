using UnityEngine;
using RogueShooter.Art;
using RogueShooter.Layout;

namespace RogueShooter.Build
{
    public class SiteRuntime : MonoBehaviour
    {
        public SiteDef Def { get; private set; }
        public bool Present { get; private set; }
        public bool Consumed { get; private set; }
        public bool InRange;

        SpriteRenderer _sr;
        JianHaiSpriteSlot _slot;
        Color _base = Color.white;
        TextMesh _label;

        public string Id => Def.Id;
        public SiteKind Kind => Def.Kind;
        public Vector3 WorldPosition => transform.position;

        public bool CanOfferBuild =>
            Present && !Consumed && (Kind == SiteKind.Chest || Kind == SiteKind.Altar);

        public bool IsShop => Kind == SiteKind.Shop;
        public bool IsEmptyChest => Kind == SiteKind.Chest && !Present;

        public void Configure(SiteDef def, bool present)
        {
            Def = def;
            Present = present;
            Consumed = false;
            _sr = GetComponent<SpriteRenderer>();
            _slot = GetComponent<JianHaiSpriteSlot>();
            if (_slot == null && !string.IsNullOrEmpty(JianHaiArtCatalog.ArtRootForHook(def.Id)))
                _slot = JianHaiSpriteSlot.Add(gameObject, def);
            if (_sr != null)
                _base = Color.white;
            _label = GetComponentInChildren<TextMesh>();
            RefreshVisual();
        }

        public void MarkConsumed()
        {
            Consumed = true;
            RefreshVisual();
        }

        public void SetPresent(bool present)
        {
            Present = present;
            Consumed = false;
            RefreshVisual();
        }

        public string Prompt()
        {
            if (Kind == SiteKind.Shop)
                return Id + "  E buy (shop never increments Build)";
            if (IsEmptyChest)
                return Id + "  EMPTY (P_spawn miss — no Build)";
            if (Consumed)
                return Id + "  taken";
            if (Kind == SiteKind.Chest)
                return Id + "  E open chest";
            if (Kind == SiteKind.Altar)
                return Id + "  E invoke altar";
            return Id;
        }

        void LateUpdate()
        {
            RefreshVisual();
            if (_sr == null)
                return;
            if (InRange && (CanOfferBuild || IsShop || IsEmptyChest))
                _sr.color = Color.Lerp(_sr.color, Color.white, 0.35f);
        }

        void RefreshVisual()
        {
            if (_slot != null)
            {
                string state = "";
                if (Kind == SiteKind.Chest)
                    state = Consumed ? "open" : "closed";
                else if (Kind == SiteKind.Altar)
                    state = InRange && CanOfferBuild ? "active" : "idle";
                _slot.SetState(state);
                _sr = _slot.GetComponent<SpriteRenderer>();
            }

            if (_sr != null)
            {
                if (Kind == SiteKind.Chest && !Present)
                    _sr.color = new Color(0.38f, 0.36f, 0.32f);
                else if (Consumed)
                    _sr.color = _base * 0.55f;
                else
                    _sr.color = _base;
            }

            if (_label != null)
            {
                if (Kind == SiteKind.Chest && !Present)
                    _label.text = Def.Id + " EMPTY";
                else if (Consumed)
                    _label.text = Def.Id + " TAKEN";
                else
                    _label.text = Def.Id;
            }
        }
    }
}
