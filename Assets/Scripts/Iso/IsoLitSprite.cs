using UnityEngine;

namespace RogueShooter.Iso
{
    [ExecuteAlways]
    public sealed class IsoLitSprite : MonoBehaviour
    {
        public Material LitMaterial;
        SpriteRenderer _renderer;
        MaterialPropertyBlock _block;

        void OnEnable()
        {
            Apply();
        }

        public void Apply()
        {
            if (_renderer == null)
                _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null || _renderer.sprite == null || LitMaterial == null)
                return;
            bool archer = _renderer.sprite.name.StartsWith("jh_archer_");
            bool wall = _renderer.sprite.name.StartsWith("jh_iso_wall_");
            if (!archer)
            {
                if (_renderer.sharedMaterial != LitMaterial)
                    _renderer.sharedMaterial = LitMaterial;
            }
            else if (_renderer.sharedMaterial == null || _renderer.sharedMaterial.shader != LitMaterial.shader)
            {
                _renderer.sharedMaterial = LitMaterial;
            }
            if (_block == null)
                _block = new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_block);

            bool hasNormal = false;
            bool hasEmission = false;
            var count = _renderer.sprite.GetSecondaryTextureCount();
            if (count > 0)
            {
                var textures = new SecondarySpriteTexture[count];
                _renderer.sprite.GetSecondaryTextures(textures);
                for (int i = 0; i < textures.Length; i++)
                {
                    if (textures[i].texture == null)
                        continue;
                    if (textures[i].name == "_NormalMap")
                    {
                        _block.SetTexture("_NormalMap", textures[i].texture);
                        hasNormal = true;
                    }
                    else if (textures[i].name == "_EmissionTex")
                    {
                        _block.SetTexture("_EmissionTex", textures[i].texture);
                        hasEmission = true;
                    }
                }
            }

            _block.SetFloat("_HasNormal", hasNormal ? 1f : 0f);
            _block.SetFloat("_NormalGain", wall ? 8f : 1f);
            _block.SetFloat("_HasEmission", hasEmission ? 1f : 0f);
            if (!(archer && Application.isPlaying))
                _block.SetFloat("_Form", archer ? 1f : 0f);
            IsoLight2D light = IsoLight2D.Current;
            if (light == null)
                light = FindObjectOfType<IsoLight2D>();
            if (light != null)
            {
                _block.SetVector("_LightDir", light.Direction.normalized);
                _block.SetColor("_LightColor", light.LightColor);
                _block.SetColor("_Ambient", light.Ambient);
            }

            _renderer.SetPropertyBlock(_block);
            if (archer && Application.isPlaying)
            {
                Material runtime = _renderer.material;
                runtime.SetFloat("_Form", 1f);
                if (_renderer.sprite.texture != null)
                    runtime.SetTexture("_MainTex", _renderer.sprite.texture);
            }
        }
    }
}
