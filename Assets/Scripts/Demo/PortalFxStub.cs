using UnityEngine;
using RogueShooter.Art;
namespace RogueShooter.Demo
{
    /// <summary>Existing delivered portal art, held for the wave warning.</summary>
    public class PortalFxStub : MonoBehaviour
    {
        SpriteRenderer _sr;
        Sprite[] _frames;
        float _age;
        public static GameObject SpawnAt(string name,Vector3 world,Transform parent)
        {
            var go=new GameObject(string.IsNullOrEmpty(name)?"PortalFx":name);
            go.transform.SetParent(parent,false);world.z=.05f;go.transform.position=world;
            go.AddComponent<PortalFxStub>().Build();return go;
        }
        void Build()
        {
            _frames=new[]{Stage1IsoArt.LoadSprite("Assets/Art/JianHai/Iso/FX/jh_iso_fx_portal_spawn_00.png"),
                Stage1IsoArt.LoadSprite("Assets/Art/JianHai/Iso/FX/jh_iso_fx_portal_spawn_01.png")};
            _sr=gameObject.AddComponent<SpriteRenderer>();_sr.sprite=_frames[0];
            JianHaiBind.SetLayer(gameObject,JianHaiArtCatalog.LayerFx,12);
            transform.localScale=Vector3.one*2.2f;
        }
        void Update()
        {
            _age+=Time.deltaTime;
            var frame=_frames[(int)(_age*6)%_frames.Length];if(frame!=null)_sr.sprite=frame;
            transform.localScale=Vector3.one*(2.2f*(1f+.06f*Mathf.Sin(_age*8f)));
            _sr.color=new Color(1,1,1,.86f+.14f*Mathf.Sin(_age*8f));
        }
    }
}
