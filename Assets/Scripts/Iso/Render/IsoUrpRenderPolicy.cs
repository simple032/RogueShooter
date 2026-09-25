using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace RogueShooter.Iso.Render
{
    /// <summary>
    /// Built-in to URP 2D with no scene edits and no default look change.
    /// Serialized Sprite/Tilemap slots still point at Sprites-Default (49 of them).
    /// URP 14's UniversalRenderPipelineAsset.default2DMaterial is editor-only
    /// (GetMaterial returns null in the player), so AddComponent&lt;SpriteRenderer&gt;
    /// does not reliably pick up Sprite-Lit-Default in a build. This policy assigns
    /// that material, keeps one white global Light2D, and enables camera post-processing
    /// against a Bloom volume whose intensity is 0.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class IsoUrpRenderPolicy : MonoBehaviour
    {
        const string RefsResource = "Iso/IsoUrpRenderRefs";
        const string SpritesDefaultShader = "Sprites/Default";
        const string LitShaderName = "Universal Render Pipeline/2D/Sprite-Lit-Default";

        // TagManager sorting layers: Default, Ground, Decal, Prop, Entity, FX, UI.
        static readonly int[] SortingLayerIds =
        {
            0,
            3100100001,
            3100100002,
            3100100003,
            3100100004,
            3100100005,
            3100100006,
        };

        static readonly FieldInfo ApplyToSortingLayers = typeof(Light2D).GetField(
            "m_ApplyToSortingLayers",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Material _lit;
        bool _warnedMissingLit;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstPolicy() != null)
                return;
            var go = new GameObject("IsoUrpRenderPolicy");
            DontDestroyOnLoad(go);
            go.AddComponent<IsoUrpRenderPolicy>();
        }

        static IsoUrpRenderPolicy FindFirstPolicy()
        {
            var found = FindObjectsByType<IsoUrpRenderPolicy>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            return found.Length > 0 ? found[0] : null;
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplyScene();
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyScene();
        }

        void LateUpdate()
        {
            // Start() spawners (and later projectile / FX marks) run after sceneLoaded.
            EnsureLitMaterial();
            SwapSpritesAndTilemaps();
            EnsureCameraPostProcessing();
        }

        void ApplyScene()
        {
            EnsureLitMaterial();
            SwapSpritesAndTilemaps();
            EnsureGlobalLight();
            EnsureGlobalVolume();
            EnsureCameraPostProcessing();
        }

        void EnsureLitMaterial()
        {
            if (_lit != null)
                return;
            var refs = Resources.Load<IsoUrpRenderRefs>(RefsResource);
            if (refs != null)
                _lit = refs.spriteLit;
            if (_lit != null)
                return;
            var shader = Shader.Find(LitShaderName);
            if (shader != null)
                _lit = new Material(shader) { name = "IsoUrpSpriteLit-Runtime" };
            if (_lit == null && !_warnedMissingLit)
            {
                _warnedMissingLit = true;
                Debug.LogWarning("[IsoUrp] Sprite-Lit-Default material is missing. Sprites stay on Sprites/Default.");
            }
        }

        void SwapSpritesAndTilemaps()
        {
            if (_lit == null)
                return;
            var sprites = FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < sprites.Length; i++)
                AssignLit(sprites[i]);
            var tiles = FindObjectsByType<TilemapRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < tiles.Length; i++)
                AssignLit(tiles[i]);
        }

        void AssignLit(Renderer renderer)
        {
            if (renderer == null)
                return;
            var mat = renderer.sharedMaterial;
            if (mat == _lit)
                return;
            if (mat != null && mat.shader != null && mat.shader.name != SpritesDefaultShader)
                return;
            renderer.sharedMaterial = _lit;
        }

        void EnsureGlobalLight()
        {
            var lights = FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].lightType == Light2D.LightType.Global)
                    return;
            }

            // Inactive until configured so Light2D.OnEnable registers the global light,
            // not the Point default AddComponent would publish.
            var go = new GameObject("IsoUrpGlobalLight2D");
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.color = Color.white;
            light.intensity = 1f;
            light.blendStyleIndex = 0; // Renderer blend style 0 is Multiply, so white * 1 matches unlit.
            if (ApplyToSortingLayers != null)
                ApplyToSortingLayers.SetValue(light, (int[])SortingLayerIds.Clone());
            go.SetActive(true);
        }

        void EnsureGlobalVolume()
        {
            var volumes = FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < volumes.Length; i++)
            {
                if (volumes[i] != null && volumes[i].isGlobal)
                    return;
            }

            var refs = Resources.Load<IsoUrpRenderRefs>(RefsResource);
            VolumeProfile profile = refs != null ? refs.volumeProfile : null;
            if (profile == null)
                profile = CreateFallbackProfile();

            var go = new GameObject("IsoUrpGlobalVolume");
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = profile;
            go.SetActive(true);
        }

        static VolumeProfile CreateFallbackProfile()
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "IsoUrpVolumeProfile-Runtime";
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(0f);
            bloom.scatter.Override(0.7f);
            return profile;
        }

        void EnsureCameraPostProcessing()
        {
            var cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                var cam = cameras[i];
                if (cam == null)
                    continue;
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null && !data.renderPostProcessing)
                    data.renderPostProcessing = true;
            }
        }
    }
}
