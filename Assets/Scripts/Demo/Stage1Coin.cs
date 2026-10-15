using System;
using UnityEngine;

namespace RogueShooter.Demo
{
    /// <summary>
    /// Kill drop. Collected when the player's body overlaps it.
    /// </summary>
    public sealed class Stage1Coin : MonoBehaviour
    {
        public const float BodyRadius = 0.55f;
        /// <summary>Visible standing height of a mob sprite (~195px at PPU 128). Shots
        /// hit anywhere through this silhouette, not only the feet circle.</summary>
        public const float BodyHeight = 1.5f;

        Transform _player;
        int _amount;
        Action<int> _collect;
        bool _taken;

        public void Bind(Transform player, int amount, Action<int> collect)
        {
            _player = player;
            _amount = amount;
            _collect = collect;
            SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 4;
            Sprite sprite = Stage1IsoArt.PickupCoin();
            if (sprite != null)
                renderer.sprite = sprite;
        }

        void Update()
        {
            if (_taken || _player == null)
                return;
            Vector3 d = _player.position - transform.position;
            d.z = 0f;
            if (d.sqrMagnitude > BodyRadius * BodyRadius)
                return;
            _taken = true;
            RogueShooter.Audio.JianHaiAudio.Emit("jh_coin_pickup",this);
            if (_collect != null)
                _collect(_amount);
            Destroy(gameObject);
        }
    }
}
