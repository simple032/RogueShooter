using UnityEngine;

namespace RogueShooter.Iso
{
    [RequireComponent(typeof(Camera))]
    public sealed class IsoEmissionBloom : MonoBehaviour
    {
        public float Threshold = 0.72f;
        public float Intensity = 0.9f;
        Material _material;

        void OnEnable()
        {
            var shader = Shader.Find("JianHai/IsoBloom");
            if (shader != null)
                _material = new Material(shader);
        }

        void OnDisable()
        {
            if (_material != null)
                DestroyImmediate(_material);
        }

        void OnRenderImage(RenderTexture src, RenderTexture dest)
        {
            if (_material == null)
            {
                Graphics.Blit(src, dest);
                return;
            }

            _material.SetFloat("_Threshold", Threshold);
            _material.SetFloat("_Intensity", Intensity);
            Graphics.Blit(src, dest, _material);
        }
    }
}
