using UnityEngine;
using RogueShooter.Layout;

namespace RogueShooter.Art
{
    /// <summary>
    /// Holds the STYLE_SPEC art root on a HOOKS-named GameObject.
    /// Interact ID (Chest_01) never changes when the sprite file is replaced.
    /// </summary>
    public class JianHaiSpriteSlot : MonoBehaviour
    {
        [SerializeField] string hookId;
        [SerializeField] string artRoot;
        [SerializeField] string state;

        SpriteRenderer _sr;

        public string HookId => hookId;
        public string ArtRoot => artRoot;
        public string ArtId => JianHaiArtCatalog.SpriteName(artRoot, state);

        public void Configure(string siteId, string root, string initialState)
        {
            hookId = siteId;
            artRoot = root;
            state = initialState ?? "";
            Apply();
        }

        public void SetState(string next)
        {
            next = next ?? "";
            if (state == next && _sr != null && _sr.sprite != null)
                return;
            state = next;
            Apply();
        }

        public void Apply()
        {
            if (_sr == null)
                _sr = GetComponent<SpriteRenderer>();
            if (_sr == null)
                _sr = gameObject.AddComponent<SpriteRenderer>();
            JianHaiSprites.Bind(_sr, ArtId);
        }

        public static JianHaiSpriteSlot Add(GameObject go, SiteDef site)
        {
            string root = JianHaiArtCatalog.ArtRootForHook(site.Id);
            var slot = go.GetComponent<JianHaiSpriteSlot>();
            if (slot == null)
                slot = go.AddComponent<JianHaiSpriteSlot>();
            slot.Configure(site.Id, root, JianHaiArtCatalog.DefaultState(site.Kind));
            return slot;
        }
    }
}
