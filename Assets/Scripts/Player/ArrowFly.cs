using System;
using UnityEngine;
using RogueShooter.Demo;

namespace RogueShooter.Player
{
    public enum ArrowPathStop
    {
        Clear = 0,
        Wall = 1,
        Hit = 2
    }

    /// <summary>
    /// Visible arrow. Each step asks the flight path whether a wall or a body is in the way.
    /// Damage is settled by that callback, not when a pre-aimed point is reached.
    /// Speed matches the accepted archer preview.
    /// </summary>
    public sealed class ArrowFly : MonoBehaviour
    {
        public const float Speed = 12f;
        /// <summary>Previous world size was 1.70 long by 0.32 thick. Both come down one step; the ratio stays.</summary>
        public const float BodyLength = 1.40f;
        public const float ShaftThickness = 0.32f * (BodyLength / 1.70f);

        Vector3 _dest;
        Action _onArrive;
        Func<Vector3, Vector3, ArrowPathStop> _sweep;
        float _fly;
        SpriteRenderer _renderer;
        bool _done;

        public void Launch(Vector3 origin, Vector3 dest, Func<Vector3, Vector3, ArrowPathStop> sweep, Action onArrive)
        {
            transform.position = origin;
            dest.z = origin.z;
            _dest = dest;
            _sweep = sweep;
            _onArrive = onArrive;
            _renderer = gameObject.AddComponent<SpriteRenderer>();
            _renderer.sortingOrder = 40;
            Sprite first = Stage1IsoArt.ArrowFlySprite(0);
            if (first != null)
            {
                _renderer.sprite = first;
                // Sheet is 96×32 with a 6px shaft. Scale that shaft so it leaves the body readable.
                Vector2 size = first.bounds.size;
                float length = Mathf.Max(0.05f, size.x * (90f / 96f));
                float shaft = Mathf.Max(0.02f, size.y * (6f / 32f));
                transform.localScale = new Vector3(BodyLength / length, ShaftThickness / shaft, 1f);
            }
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
            bool arriving = delta.sqrMagnitude <= step * step || delta.sqrMagnitude < 0.0001f;
            Vector3 next = arriving ? _dest : transform.position + delta.normalized * step;
            ArrowPathStop stop = ArrowPathStop.Clear;
            if (_sweep != null)
                stop = _sweep(transform.position, next);
            if (stop != ArrowPathStop.Clear)
            {
                _done = true;
                _sweep = null;
                _onArrive = null;
                Destroy(gameObject);
                return;
            }

            if (arriving)
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

            transform.position = next;
            _fly += Time.deltaTime;
            if (_renderer == null)
                return;
            Sprite frame = Stage1IsoArt.ArrowFlySprite(Mathf.FloorToInt(_fly * 12f));
            if (frame != null)
                _renderer.sprite = frame;
        }
    }
}
