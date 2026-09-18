using UnityEngine;

namespace RogueShooter.Demo
{
    /// <summary>
    /// Placeholder enemy. AI / combat is out of scope.
    /// </summary>
    public class StubEnemy : MonoBehaviour
    {
        [SerializeField] float pulse = 2.4f;

        Vector3 _baseScale;

        void Awake()
        {
            _baseScale = transform.localScale;
        }

        void Update()
        {
            float s = 1f + 0.08f * Mathf.Sin(Time.time * pulse);
            transform.localScale = _baseScale * s;
        }
    }
}
