namespace RogueShooter.Build
{
    public enum RewardScreenKind
    {
        Chest = 0,
        Altar = 1,
        Shop = 2
    }

    /// <summary>
    /// §9.1 timing: chest/altar play 6 frames at 12 FPS once, then show choices.
    /// Shop shows immediately. Open pauses world and wall clock until close.
    /// </summary>
    public sealed class RewardScreenSession
    {
        public const int FrameCount = 6;
        public const float FramesPerSecond = 12f;

        public RewardScreenKind Kind { get; private set; }
        public bool Open { get; private set; }
        public bool WorldPaused { get; private set; }
        public bool ClockPaused { get; private set; }
        public bool ChoicesVisible { get; private set; }
        public int AnimFrame { get; private set; }
        public string ClipId { get; private set; }
        float _elapsed;

        public void OpenChest()
        {
            Begin(RewardScreenKind.Chest, "jh_fx_chest_reward_", animate: true);
        }

        public void OpenAltar()
        {
            Begin(RewardScreenKind.Altar, "jh_fx_altar_reward_", animate: true);
        }

        public void OpenShop()
        {
            Begin(RewardScreenKind.Shop, "", animate: false);
            ChoicesVisible = true;
        }

        public void Tick(float unscaledDelta)
        {
            if (!Open || ChoicesVisible || Kind == RewardScreenKind.Shop)
                return;
            if (unscaledDelta < 0f)
                unscaledDelta = 0f;
            _elapsed += unscaledDelta;
            float duration = FrameCount / FramesPerSecond;
            if (_elapsed >= duration)
            {
                AnimFrame = FrameCount - 1;
                ChoicesVisible = true;
                return;
            }

            AnimFrame = (int)(_elapsed * FramesPerSecond);
            if (AnimFrame < 0)
                AnimFrame = 0;
            if (AnimFrame > FrameCount - 1)
                AnimFrame = FrameCount - 1;
        }

        public void Close()
        {
            Open = false;
            WorldPaused = false;
            ClockPaused = false;
            ChoicesVisible = false;
            AnimFrame = 0;
            ClipId = "";
            _elapsed = 0f;
        }

        void Begin(RewardScreenKind kind, string clip, bool animate)
        {
            Kind = kind;
            ClipId = clip;
            Open = true;
            WorldPaused = true;
            ClockPaused = true;
            ChoicesVisible = !animate;
            AnimFrame = 0;
            _elapsed = 0f;
        }
    }
}
