using UnityEngine;

namespace RogueShooter.Balance
{
    /// <summary>
    /// Effective run-clock pressure. DamageMul is the v0.61 runtime authority.
    /// Attr knots same as N31; interval knots 1.10/1.00/0.85/0.72, floor 0.45.
    /// P1–P4 labels are HUD / gold-band tags only.
    /// </summary>
    public static class TimePressure
    {
        public const float Band2Min = 4f;
        public const float Band3Min = 8f;
        public const float LateMin = 12f;
        public const float Knot0Attr = 1.00f;
        public const float Knot4Attr = 1.15f;
        public const float Knot7Attr = 1.35f;
        public const float Knot10Attr = 1.55f;
        public const float Knot0Int = 1.10f;
        public const float Knot4Int = 1.00f;
        public const float Knot7Int = 0.85f;
        public const float Knot10Int = 0.72f;
        public const float LateAttrSlope = 0.03f;
        public const float LateIntSlope = 0.05f;
        public const float IntervalFloor = 0.45f;

        const float AttrSlope0To4 = 0.0375f;
        const float AttrSlope4To7 = 0.20f / 3f;
        const float AttrSlope7To10 = 0.20f / 3f;
        const float IntSlope0To4 = 0.025f;
        const float IntSlope4To7 = 0.05f;
        const float IntSlope7To10 = 0.13f / 3f;

        public static string PhaseId(float minutes)
        {
            if (minutes < Band2Min) return "P1";
            if (minutes < Band3Min) return "P2";
            if (minutes < LateMin) return "P3";
            return "P4";
        }

        public static string PhaseLabel(float minutes)
        {
            switch (PhaseId(minutes))
            {
                case "P1": return "简单";
                case "P2": return "普通";
                case "P3": return "困难";
                default: return "极难";
            }
        }

        public static float AttrMul(float minutes)
        {
            float t = Mathf.Max(0f, minutes);
            if (t <= 4f) return 1f + .0375f*t;
            if (t <= 7f) return 1.15f + (.20f/3f)*(t-4f);
            if (t <= 10f) return 1.35f + (.20f/3f)*(t-7f);
            if (t <= 15f) return 1.55f + .03f*(t-10f);
            return Mathf.Min(1.75f, 1.70f + .015f*(t-15f));
        }

        public static float DamageMul(float minutes)
        {
            float t = Mathf.Max(0f, minutes);
            if (t <= 4f) return 1f + .025f*t;
            if (t <= 8f) return 1.10f + .05f*(t-4f);
            if (t <= 12f) return 1.30f + .0625f*(t-8f);
            if (t <= 15f) return 1.55f + (.20f/3f)*(t-12f);
            return Mathf.Min(2f, 1.75f + .05f*(t-15f));
        }

        public static float IntervalMul(float minutes)
        {
            float t = Mathf.Max(0f, minutes);
            if(t<=4f) return 1.10f-.025f*t;
            if(t<=7f) return 1f-.05f*(t-4f);
            if(t<=10f) return .85f-(.13f/3f)*(t-7f);
            return Mathf.Max(IntervalFloor,.72f*(1f-.05f*(t-10f)));
        }

        /// <summary>Null on pass. Checks t=4/7/10 left/right continuity (Δ≈0).</summary>
        public static string ContinuityCheck(float eps = 0.001f)
        {
            float[] knots = { 4f, 7f, 8f, 10f, 12f, 15f, 20f };
            for (int i = 0; i < knots.Length; i++)
            {
                float t = knots[i];
                float aL = AttrMul(t - eps);
                float aR = AttrMul(t + eps);
                float a0 = AttrMul(t);
                float iL = IntervalMul(t - eps);
                float iR = IntervalMul(t + eps);
                float i0 = IntervalMul(t);
                if (Mathf.Abs(aL - aR) > 0.001f || Mathf.Abs(a0 - aL) > 0.001f || Mathf.Abs(a0 - aR) > 0.001f)
                    return "attr jump at t=" + t.ToString("0") + " L=" + aL.ToString("0.000")
                        + " C=" + a0.ToString("0.000") + " R=" + aR.ToString("0.000");
                if (Mathf.Abs(iL - iR) > 0.001f || Mathf.Abs(i0 - iL) > 0.001f || Mathf.Abs(i0 - iR) > 0.001f)
                    return "interval jump at t=" + t.ToString("0") + " L=" + iL.ToString("0.000")
                        + " C=" + i0.ToString("0.000") + " R=" + iR.ToString("0.000");
            }

            return null;
        }
    }
}
