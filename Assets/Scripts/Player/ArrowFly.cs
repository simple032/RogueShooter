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
        /// <summary>The slim sheet (200×28 at PPU 128) is drawn at its final world
        /// size: 1.56 long, the oak shaft band 5px thick, with a shorter, thinner launch scale.</summary>
        public const float BodyLength = 1.171875f;
        public const float ShaftThickness = 0.0254f;

        Vector3 _start;
        Vector3 _dir;
        float _len;
        float _u;
        Action _onArrive;
        Func<Vector3, Vector3, ArrowPathStop> _sweep;
        float _fly;
        SpriteRenderer _renderer;
        bool _done;
        string _heroFacing;

        /// <summary>
        /// The arrow flies one straight segment: it leaves the drawn bow aimed at
        /// the mouse point (the reticle) and lands on it, capped by fixed range.
        /// </summary>
        public void Launch(Vector3 origin, Vector3 dir, float length, Func<Vector3, Vector3, ArrowPathStop> sweep, Action onArrive)
        {
            origin.z = 0f;
            dir.z = 0f;
            if (dir.sqrMagnitude < 0.001f)
                dir = Vector3.right;
            transform.position = origin;
            _start = origin;
            _dir = dir.normalized;
            _len = Mathf.Max(0.05f, length);
            _u = 0f;
            _sweep = sweep;
            _onArrive = onArrive;
            _renderer = gameObject.AddComponent<SpriteRenderer>();
            _renderer.sortingOrder = 40;
            if(RogueShooter.Iso.HeroMeshyArt.Available)
            {
                _renderer.sortingLayerName=RogueShooter.Art.JianHaiArtCatalog.LayerFx;
                _heroFacing=RogueShooter.Iso.HeroMeshyArt.Facing(_dir);
                _renderer.sprite=RogueShooter.Iso.HeroMeshyArt.Projectile(_heroFacing);
                transform.localScale=Vector3.one;
            }
            else
            {
                Sprite first = Stage1IsoArt.ArrowFlySprite(0);
                if (first != null)
                {
                    _renderer.sprite = first;
                    // Rendering only: retain flight speed and collision sweeps.
                    transform.localScale = new Vector3(.75f, .65f, 1f);
                }
            }
            AimVisual(_dir);
        }

        // Keep the drawn arrow aligned with its actual motion: the Meshy sheet has
        // only 8 fixed directions, so rotate the nearest bucket sprite by the
        // remaining angle (the plain sheet rotates exactly).
        void AimVisual(Vector3 delta)
        {
            delta.z = 0f;
            if (delta.sqrMagnitude < 0.000001f)
                return;
            float ang = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            if (_heroFacing != null)
            {
                string facing = RogueShooter.Iso.HeroMeshyArt.Facing(delta);
                if (facing != _heroFacing)
                {
                    _heroFacing = facing;
                    if (_renderer != null)
                        _renderer.sprite = RogueShooter.Iso.HeroMeshyArt.Projectile(facing);
                }
                float residual = ang - RogueShooter.Iso.HeroMeshyArt.Index(_heroFacing) * 45f;
                while (residual > 180f) residual -= 360f;
                while (residual < -180f) residual += 360f;
                transform.rotation = Quaternion.Euler(0f, 0f, residual);
            }
            else
                transform.rotation = Quaternion.Euler(0f, 0f, ang);
        }

        /// <summary>May be called by a hit callback during this arrow's own sweep.</summary>
        public void CancelFlight()
        {
            if (_done) return;
            _done = true; _sweep = null; _onArrive = null;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        void Update()
        {
            if (_done || RunPause.IsPaused)
                return;
            float step = Speed * Time.deltaTime;
            Vector3 prev = transform.position;
            bool arriving = _u + step >= _len;
            _u = arriving ? _len : _u + step;
            Vector3 next = _start + _dir * _u;
            ArrowPathStop stop = ArrowPathStop.Clear;
            if (_sweep != null)
                stop = _sweep(prev, next);
            // A phase transition can cancel this very arrow from inside _sweep.
            if (this == null || _done) return;
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
                transform.position = next;
                _done = true;
                Action arrive = _onArrive;
                _onArrive = null;
                if (arrive != null)
                    arrive();
                if (this != null) Destroy(gameObject);
                return;
            }

            transform.position = next;
            AimVisual(_dir);
            _fly += Time.deltaTime;
            if (_renderer == null)
                return;
            if(_heroFacing!=null)return;
            Sprite frame = Stage1IsoArt.ArrowFlySprite(Mathf.FloorToInt(_fly * 12f));
            if (frame != null)
                _renderer.sprite = frame;
        }
    }
}
