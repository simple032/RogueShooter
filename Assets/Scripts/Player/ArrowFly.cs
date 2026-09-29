using System;
using UnityEngine;
using RogueShooter.Demo;

namespace RogueShooter.Player
{
    /// <summary>
    /// Visible arrow. Damage is applied only when it reaches the point aimed at release.
    /// Speed matches the accepted archer preview.
    /// </summary>
    public sealed class ArrowFly : MonoBehaviour
    {
        public const float Speed = 8f;

        Vector3 _dest;
        Action _onArrive;
        float _fly;
        SpriteRenderer _renderer;
        bool _done;

        public void Launch(Vector3 origin, Vector3 dest, Action onArrive)
        {
            transform.position = origin;
            dest.z = origin.z;
            _dest = dest;
            _onArrive = onArrive;
            _renderer = gameObject.AddComponent<SpriteRenderer>();
            _renderer.sortingOrder = 5;
            Sprite first = Stage1IsoArt.ArrowFlySprite(0);
            if (first != null)
                _renderer.sprite = first;
            Vector3 delta = _dest - origin;
            delta.z = 0f;
            if (delta.sqrMagnitude > 0.0001f)
            {
                float ang = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, 0f, ang);
            }
        }

        void Update()
        {
            if (_done)
                return;
            Vector3 delta = _dest - transform.position;
            delta.z = 0f;
            float step = Speed * Time.deltaTime;
            if (delta.sqrMagnitude <= step * step || delta.sqrMagnitude < 0.0001f)
            {
                transform.position = _dest;
                _done = true;
                Action arrive = _onArrive;
                _onArrive = null;
                if (arrive != null)
                    arrive();
                Destroy(gameObject);
                return;
            }

            transform.position += delta.normalized * step;
            _fly += Time.deltaTime;
            if (_renderer == null)
                return;
            Sprite frame = Stage1IsoArt.ArrowFlySprite(Mathf.FloorToInt(_fly * 12f));
            if (frame != null)
                _renderer.sprite = frame;
        }
    }
}
