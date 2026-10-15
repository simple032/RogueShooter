using UnityEngine;

namespace RogueShooter.Demo
{
    /// <summary>
    /// Room-owned lock state, with a sliding or hinged view in production and
    /// the original sprite-frame fields retained for older scene references.
    /// </summary>
    public sealed class Stage1DoorGate : MonoBehaviour
    {
        public SpriteRenderer Body;
        public Sprite[] CloseFrames;
        public Sprite[] OpenFrames;
        public float Fps = 10f;
        public Stage1HingedDoorView HingedView;
        public Stage1SlidingDoorView SlidingView;

        enum Phase { IdleOpen, Closing, IdleClosed, Opening }

        Phase _phase = Phase.IdleOpen;
        float _frame;

        void Start()
        {
            if (_phase == Phase.IdleOpen)
                ShowOpen();
        }

        public bool IsLocked { get { return _phase == Phase.IdleClosed || _phase == Phase.Closing; } }

        public void SetLocked(bool locked)
        {
            if (locked && !IsLocked)
            {
                _phase = Phase.Closing;
                if (SlidingView != null) SlidingView.AnimateTo(0f);
                _frame = 0f;
                enabled = true;
            }
            else if (!locked && _phase != Phase.IdleOpen && _phase != Phase.Opening)
            {
                _phase = Phase.Opening;
                if (SlidingView != null) SlidingView.AnimateTo(1f);
                _frame = 0f;
                enabled = true;
            }
        }

        void Update()
        {
            if (_phase != Phase.Closing && _phase != Phase.Opening)
            {
                enabled = false;
                return;
            }

            if (SlidingView != null)
            {
                float target = _phase == Phase.Closing ? 0f : 1f;
                if (!SlidingView.Animating && Mathf.Approximately(SlidingView.Open01, target))
                {
                    _phase = target == 0f ? Phase.IdleClosed : Phase.IdleOpen;
                    enabled = false;
                }
                return;
            }

            if (HingedView != null)
            {
                float target = _phase == Phase.Closing ? 0f : 1f;
                HingedView.SetPose(Mathf.MoveTowards(HingedView.Open01, target,
                    Time.deltaTime / Stage1HingedDoorView.OpeningSeconds));
                if (Mathf.Approximately(HingedView.Open01, target))
                {
                    _phase = target == 0f ? Phase.IdleClosed : Phase.IdleOpen;
                    enabled = false;
                }
                return;
            }

            _frame += Time.deltaTime * Fps;
            int index = Mathf.FloorToInt(_frame);
            if (_phase == Phase.Closing)
            {
                if (CloseFrames == null || index >= CloseFrames.Length)
                {
                    ShowClosed();
                    _phase = Phase.IdleClosed;
                    enabled = false;
                    return;
                }
                Show(CloseFrames, index);
            }
            else
            {
                if (OpenFrames == null || index >= OpenFrames.Length)
                {
                    ShowOpen();
                    _phase = Phase.IdleOpen;
                    enabled = false;
                    return;
                }
                Show(OpenFrames, index);
            }
        }

        void Show(Sprite[] frames, int index)
        {
            if (Body != null && frames != null && index >= 0 && index < frames.Length && frames[index] != null)
                Body.sprite = frames[index];
        }

        void ShowOpen()
        {
            if (SlidingView != null) SlidingView.SetPoseImmediate(1f);
            if (HingedView != null) HingedView.SetPose(1f);
            if (OpenFrames != null && OpenFrames.Length > 0)
                Show(OpenFrames, OpenFrames.Length - 1);
        }

        void ShowClosed()
        {
            if (SlidingView != null) SlidingView.SetPoseImmediate(0f);
            if (HingedView != null) HingedView.SetPose(0f);
            if (CloseFrames != null && CloseFrames.Length > 0)
                Show(CloseFrames, CloseFrames.Length - 1);
        }
    }
}
