using UnityEngine;

namespace RogueShooter.Spawning
{
    /// <summary>
    /// Hub / Altar / Chest / Shop / Pre keep a no-spawn radius (LOCK cores).
    /// </summary>
    public class NoSpawnCore : MonoBehaviour
    {
        [SerializeField] string coreId;
        [SerializeField] string kind;
        [SerializeField] float radius = 2.5f;

        public string CoreId => coreId;
        public string Kind => kind;
        public float Radius => radius;
        public Vector3 WorldPosition => transform.position;

        public void Configure(string id, string coreKind, float coreRadius)
        {
            coreId = id;
            kind = coreKind;
            radius = Mathf.Max(0.01f, coreRadius);
            gameObject.name = "Core_" + id;
        }

        void OnEnable() => SpawnCoreGate.Register(this);

        void OnDisable() => SpawnCoreGate.Unregister(this);
    }
}
