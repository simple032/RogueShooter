using UnityEngine;

namespace JianHai.ArtSamples
{
    [ExecuteAlways]
    public sealed class S1SampleDepth : MonoBehaviour
    {
        public float FootOffset;
        void LateUpdate()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = 500 - Mathf.RoundToInt((transform.position.y + FootOffset) * 24);
        }
    }
}
