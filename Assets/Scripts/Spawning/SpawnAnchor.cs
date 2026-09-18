using UnityEngine;

namespace RogueShooter.Spawning
{
    /// <summary>
    /// Named world-space spawn point. Ids are data (Chest_01, A3, BOSS, …) — not a baked map graph.
    /// </summary>
    public class SpawnAnchor : MonoBehaviour
    {
        [SerializeField] string anchorId = "Unnamed";

        public string AnchorId => anchorId;
        public Vector3 WorldPosition => transform.position;

        public void Configure(string id, Vector3 worldPos)
        {
            anchorId = id;
            transform.position = worldPos;
            gameObject.name = "Anchor_" + id;
        }
    }
}
