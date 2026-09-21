using UnityEngine;
using RogueShooter.Demo;

namespace RogueShooter.Ai
{
    /// <summary>Placeholder front shield disc. Shatter hides it.</summary>
    public class MobShieldVisual : MonoBehaviour
    {
        GameObject _disc;

        public void Ensure()
        {
            if (_disc != null)
                return;
            _disc = DemoPrimitives.Quad(
                "ShieldDisc",
                transform.position,
                new Vector2(0.55f, 0.85f),
                new Color(0.35f, 0.62f, 0.95f, 0.85f),
                12,
                transform);
            _disc.transform.localPosition = new Vector3(0.42f, 0.05f, 0f);
            _disc.SetActive(false);
        }

        public void SetRaised(bool raised, Vector3 facing)
        {
            Ensure();
            _disc.SetActive(raised);
            if (!raised)
                return;
            facing.z = 0f;
            if (facing.sqrMagnitude < 0.0001f)
                facing = Vector3.right;
            facing.Normalize();
            float ang = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
            _disc.transform.localRotation = Quaternion.Euler(0f, 0f, ang);
            _disc.transform.localPosition = new Vector3(facing.x * 0.42f, facing.y * 0.42f, 0f);
        }
    }
}
