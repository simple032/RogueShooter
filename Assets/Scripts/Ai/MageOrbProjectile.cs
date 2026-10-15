using UnityEngine;
using System.Collections.Generic;
using RogueShooter.Maze;
using RogueShooter.Demo;
using RogueShooter.Player;

namespace RogueShooter.Ai
{
    /// <summary>
    /// Linear mage orb. Despawns on max range (camera width×0.7), camera edge, or player hit.
    /// Owner waits for despawn before the next volley.
    /// </summary>
    public sealed class MageOrbVolley
    {
        readonly HashSet<int> _hit=new HashSet<int>();
        readonly HashSet<int> _near=new HashSet<int>();
        public bool TryNear(Transform player)=>player!=null&&_near.Add(player.GetInstanceID());
        public int HitCount=>_hit.Count;
        public bool TryDamage(PlayerVitals target,float damage,Vector3 impactAway=default(Vector3))
        {if(target==null||target.IsDown||!_hit.Add(target.GetInstanceID()))return false;target.ApplyHit(damage,"ORB",impactAway);return true;}
    }
    public class MageOrbProjectile : MonoBehaviour
    {
        public const float WallRadius = .2f;
        public const float GrazeHitRadius = .75f;
        MageOrbVolley _volley;
        Stage1Maze _maze;
        IReadOnlyList<GameObject> _doors;
        public void BindMaze(Stage1Maze maze, IReadOnlyList<GameObject> doors = null) { _maze = maze; _doors = doors; }
        Vector3 _dir;
        float _speed;
        float _maxRange;
        float _traveled;
        float _dmg;
        Transform _player;
        System.Action<MageOrbProjectile, string> _onDespawn;
        bool _dead,_nearPlayed;
        float _fly;
        SpriteRenderer _renderer;

        public bool Alive => !_dead && isActiveAndEnabled;

        public void Launch(
            Vector3 origin,
            Vector3 direction,
            float speed,
            float maxRange,
            float damage,
            Transform player,
            System.Action<MageOrbProjectile, string> onDespawn, MageOrbVolley volley = null)
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
            _volley = volley;
            _dead = false;_nearPlayed=false;
        }

        void Update()
        {
            if (_dead || RunPause.IsPaused)
                return;
            float dt = Time.deltaTime;
            float step = _speed * dt;
            Vector3 from = transform.position, to = from + _dir * step;
            float wallT = MazeCollision.FirstBlockT(_maze, _doors, from, to, WallRadius);
            float bodyT = _player != null ? MazeCollision.CircleEntryT(_player.position, from, to, GrazeHitRadius) : 2f;
            if(bodyT<=1f && _player!=null && MazeCollision.FirstBlockT(_maze,_doors,Vector3.Lerp(from,to,bodyT),_player.position)<=1f) bodyT=2f;
            if (wallT <= 1f && wallT <= bodyT)
            {
                transform.position = Vector3.Lerp(from, to, Mathf.Max(0f, wallT-.001f));
                Despawn("wall");
                return;
            }
            if (bodyT <= 1f)
            {
                var vitals = _player.GetComponent<PlayerVitals>();
                if (_volley != null) _volley.TryDamage(vitals,_dmg,_dir);
                else if (vitals != null) vitals.ApplyHit(_dmg, "ORB",_dir);
                Despawn("hit");
                return;
            }
            // A near miss is resolved when the orb passes its closest point, outside the hit radius.
            if(!_nearPlayed&&_player!=null)
            {
                Vector3 delta=_player.position-from;delta.z=0;float along=Vector3.Dot(delta,_dir);
                float perpendicular=(delta-_dir*along).sqrMagnitude;
                if(along>=0&&along<=step&&perpendicular>GrazeHitRadius*GrazeHitRadius&&perpendicular<=1.25f*1.25f&&MazeCollision.FirstBlockT(_maze,_doors,to,_player.position)>1f)
                {_nearPlayed=true;if(_volley==null||_volley.TryNear(_player))RogueShooter.Audio.JianHaiAudio.Emit("jh_orb_near_miss",this);}
            }
            transform.position = to;
            _traveled += step;
            _fly += dt;
            if (_renderer == null)
                _renderer = GetComponent<SpriteRenderer>();
            if (_renderer != null)
            {
                Sprite frame = Stage1IsoArt.MageOrbSprite(Mathf.FloorToInt(_fly * 12f));
                if (frame != null)
                    _renderer.sprite = frame;
            }

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

        }

        bool OffCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
                return false;
            // The play camera is pitched. An axis-aligned rect on the camera
            // position sits off the floor and was deleting the orb the same frame it spawned.
            Vector3 vp = cam.WorldToViewportPoint(transform.position);
            if (vp.z < 0f)
                return true;
            const float pad = 0.04f;
            return vp.x < -pad || vp.x > 1f + pad || vp.y < -pad || vp.y > 1f + pad;
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
