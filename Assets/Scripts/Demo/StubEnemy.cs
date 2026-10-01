using UnityEngine;
using RogueShooter.Ai;
using RogueShooter.Player;

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
        int _baseHp = 1;
        bool _pressureSettled;

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
            _baseHp = hitPoints;
            _pressureSettled = false;
            _elite = elite;
            _dead = false;
            if (elite)
                _pressureScale = 1.18f;
        }

        public void SetPressureScale(float scale)
        {
            _pressureScale = scale > 0.01f ? scale : 1f;
        }

        /// <summary>Once, at engage. Uses the locked time-pressure mul on the spawned HP.</summary>
        public void SettlePressure(float mul)
        {
            if (_pressureSettled || _dead)
                return;
            _pressureSettled = true;
            float m = mul > 0.01f ? mul : 1f;
            int next = Mathf.Max(1, Mathf.RoundToInt(_baseHp * m));
            float ratio = _maxHp > 0 ? hitPoints / (float)_maxHp : 1f;
            _maxHp = next;
            hitPoints = Mathf.Max(0, Mathf.RoundToInt(next * ratio));
            RefreshHpBar();
        }

        public void TakeDamage(int amount)
        {
            if (_dead || RunPause.RunSettled)
                return;
            Damaged?.Invoke();
            hitPoints -= amount < 1 ? 1 : amount;
            if (hitPoints < 0)
                hitPoints = 0;
            if (hitPoints > 0)
            {
                RefreshHpBar();
                return;
            }

            _dead = true;
            SetHpBar(false);
            MobBangMarker bang = GetComponent<MobBangMarker>();
            if (bang != null)
                bang.SetVisible(false);
            ShatterCurrentFrame();
            Died?.Invoke(this);
        }

        void Update()
        {
            if (_dead)
            {
                MobBangMarker bang = GetComponent<MobBangMarker>();
                if (bang != null && bang.Visible)
                    bang.SetVisible(false);
                return;
            }
            float s = 1f + 0.08f * Mathf.Sin(Time.time * pulse);
            transform.localScale = _baseScale * _pressureScale * s;
        }

        void RefreshHpBar()
        {
            float ratio = _maxHp > 0 ? hitPoints / (float)_maxHp : 1f;
            if (ratio >= 0.999f)
            {
                SetHpBar(false);
                return;
            }

            Transform bar = transform.Find("HpBar");
            if (bar == null)
            {
                Sprite track = Stage1IsoArt.LoadSprite("Assets/Art/JianHai/UI/jh_ui_bar_mob_hp_track.png");
                Sprite fill = Stage1IsoArt.LoadSprite("Assets/Art/JianHai/UI/jh_ui_bar_mob_hp_fill.png");
                if (track == null || fill == null)
                    return;
                var root = new GameObject("HpBar");
                root.transform.SetParent(transform, false);
                root.transform.localPosition = new Vector3(0f, 1.15f, 0f);
                SpriteRenderer trackRenderer = root.AddComponent<SpriteRenderer>();
                trackRenderer.sprite = track;
                trackRenderer.sortingOrder = 8;
                var fillGo = new GameObject("Fill");
                fillGo.transform.SetParent(root.transform, false);
                SpriteRenderer fillRenderer = fillGo.AddComponent<SpriteRenderer>();
                fillRenderer.sprite = fill;
                fillRenderer.sortingOrder = 9;
                bar = root.transform;
            }

            SetHpBar(true);
            Transform fillTransform = bar.Find("Fill");
            if (fillTransform != null)
                fillTransform.localScale = new Vector3(Mathf.Clamp01(ratio), 1f, 1f);
        }

        void SetHpBar(bool visible)
        {
            Transform bar = transform.Find("HpBar");
            if (bar != null)
                bar.gameObject.SetActive(visible);
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
