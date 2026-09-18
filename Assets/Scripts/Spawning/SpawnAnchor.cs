using UnityEngine;

namespace RogueShooter.Spawning
{
    /// <summary>
    /// Named world-space spawn point. <see cref="AnchorId"/> is an opaque string
    /// (campaign IDs are not frozen). This type does not encode path layout.
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
