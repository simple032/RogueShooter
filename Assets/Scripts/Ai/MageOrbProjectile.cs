using UnityEngine;
using RogueShooter.Player;
using RogueShooter.Vision;

namespace RogueShooter.Ai
{
    /// <summary>
    /// Linear mage orb. Despawns on max range (camera width×0.7), camera edge, or player hit.
    /// Owner waits for despawn before the next volley.
    /// </summary>
    public class MageOrbProjectile : MonoBehaviour
    {
        Vector3 _dir;
        float _speed;
        float _maxRange;
        float _traveled;
        float _dmg;
        Transform _player;
        System.Action<MageOrbProjectile, string> _onDespawn;
        bool _dead;

        public bool Alive => !_dead && isActiveAndEnabled;

        public void Launch(
            Vector3 origin,
            Vector3 direction,
            float speed,
            float maxRange,
            float damage,
            Transform player,
            System.Action<MageOrbProjectile, string> onDespawn)
        {
            transform.position = origin;
            direction.z = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector3.right;
            _dir = direction.normalized;
            _speed = speed > 0.01f ? speed : 1f;
            _maxRange = maxRange > 0.01f ? maxRange : 1f;
            _traveled = 0f;
            _dmg = damage;
            _player = player;
            _onDespawn = onDespawn;
            _dead = false;
        }

        void Update()
        {
            if (_dead)
                return;
            float dt = Time.deltaTime;
            float step = _speed * dt;
            transform.position += _dir * step;
            _traveled += step;

            if (_traveled >= _maxRange)
            {
                Despawn("edge");
                return;
            }

            if (OffCamera())
            {
                Despawn("edge");
                return;
            }

            if (_player != null)
            {
                Vector3 d = _player.position - transform.position;
                d.z = 0f;
                if (d.sqrMagnitude <= EnemyCombatRules.OrbHitRadiusStub * EnemyCombatRules.OrbHitRadiusStub)
                {
                    var vitals = _player.GetComponent<PlayerVitals>();
                    if (vitals != null)
                        vitals.ApplyHit(_dmg, "ORB");
                    Despawn("hit");
                }
            }
        }

        bool OffCamera()
        {
            Camera cam = Camera.main;
            if (cam == null || !cam.orthographic)
                return false;
            Rect rect = CameraViewMath.GetOrthographicWorldRect(
                cam.transform.position, cam.orthographicSize, CameraViewMath.ResolveAspect(cam));
            return !CameraViewMath.ContainsInclusive(rect, transform.position);
        }

        void Despawn(string reason)
        {
            if (_dead)
                return;
            _dead = true;
            var cb = _onDespawn;
            _onDespawn = null;
            cb?.Invoke(this, reason);
            Destroy(gameObject);
        }
    }
}
