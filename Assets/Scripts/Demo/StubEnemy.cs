using UnityEngine;

namespace RogueShooter.Demo
{
    /// <summary>
    /// Placeholder enemy. Combat is a stub; four-state AI lives on MobFourStateAi.
    /// </summary>
    public class StubEnemy : MonoBehaviour
    {
        [SerializeField] float pulse = 2.4f;
        [SerializeField] int hitPoints = 1;

        Vector3 _baseScale;
        float _pressureScale = 1f;
        string _kindId = "E1";
        bool _dead;

        public string KindId => _kindId;
        public bool IsDead => _dead;
        public event System.Action Damaged;
        public event System.Action<StubEnemy> Died;

        public int Hp => hitPoints;
        public int MaxHp => _maxHp;
        public bool IsFullHp => !_dead && hitPoints >= _maxHp && _maxHp > 0;

        void Awake()
        {
            _baseScale = transform.localScale;
        }

        bool _elite;
        int _maxHp = 1;

        public bool Elite => _elite;

        public void ConfigureKind(string kindId, int hp = 1)
        {
            ConfigureKind(kindId, hp, false);
        }

        public void ConfigureKind(string kindId, int hp, bool elite)
        {
            _kindId = string.IsNullOrEmpty(kindId) ? "E1" : kindId;
            hitPoints = hp < 1 ? 1 : hp;
            _maxHp = hitPoints;
            _elite = elite;
            _dead = false;
            if (elite)
                _pressureScale = 1.18f;
        }

        public void SetPressureScale(float scale)
        {
            _pressureScale = scale > 0.01f ? scale : 1f;
        }

        public void TakeDamage(int amount)
        {
            if (_dead)
                return;
            Damaged?.Invoke();
            hitPoints -= amount < 1 ? 1 : amount;
            if (hitPoints < 0)
                hitPoints = 0;
            if (hitPoints > 0)
            {
                ShowRemainingHp(hitPoints);
                return;
            }

            _dead = true;
            ShatterCurrentFrame();
            Died?.Invoke(this);
        }

        void Update()
        {
            if (_dead)
                return;
            float s = 1f + 0.08f * Mathf.Sin(Time.time * pulse);
            transform.localScale = _baseScale * _pressureScale * s;
        }

        void ShowRemainingHp(int remaining)
        {
            Transform existing = transform.Find("HpReadout");
            GameObject go = existing != null ? existing.gameObject : new GameObject("HpReadout");
            if (existing == null)
            {
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            }

            TextMesh label = go.GetComponent<TextMesh>();
            if (label == null)
                label = go.AddComponent<TextMesh>();
            label.anchor = TextAnchor.LowerCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = 0.12f;
            label.fontSize = 32;
            label.color = Color.white;
            label.text = remaining.ToString();
            BuiltinUiFont.Apply(label);
        }

        void ShatterCurrentFrame()
        {
            SpriteRenderer renderer = GetComponent<SpriteRenderer>();
            Sprite frame = renderer != null ? renderer.sprite : null;
            Color color = renderer != null ? renderer.color : Color.white;
            Vector3 scale = transform.localScale * 0.35f;
            if (renderer != null)
                renderer.enabled = false;
            Collider2D[] cols = GetComponents<Collider2D>();
            for (int i = 0; i < cols.Length; i++)
                cols[i].enabled = false;
            const int pieces = 6;
            for (int i = 0; i < pieces; i++)
            {
                float ang = i * (360f / pieces) * Mathf.Deg2Rad;
                var bit = new GameObject("Shatter");
                bit.transform.position = transform.position;
                bit.transform.localScale = scale;
                var shard = bit.AddComponent<SpriteRenderer>();
                shard.sprite = frame;
                shard.color = color;
                shard.sortingOrder = 8;
                var kick = bit.AddComponent<MobShatterBit>();
                kick.Kick(new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 2.4f);
            }
        }
    }
}
