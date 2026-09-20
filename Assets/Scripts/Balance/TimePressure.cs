using UnityEngine;

namespace RogueShooter.Balance
{
    /// <summary>
    /// N34+N31 continuous wall-clock pressure from balance_time_scale_箭骸.csv.
    /// Attr knots same as N31; interval knots 1.10/1.00/0.85/0.72, floor 0.45.
    /// P1–P4 labels are HUD / gold-band tags only.
    /// </summary>
    public static class TimePressure
    {
        public const float Band2Min = 4f;
        public const float Band3Min = 7f;
        public const float LateMin = 10f;
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
                case "P1": return "时间一";
                case "P2": return "时间二";
                case "P3": return "时间三";
                default: return "时间四";
            }
        }

        public static float AttrMul(float minutes)
        {
            if (minutes < 0f)
                minutes = 0f;
            if (minutes <= Band2Min)
                return Knot0Attr + AttrSlope0To4 * minutes;
            if (minutes <= Band3Min)
                return Knot4Attr + AttrSlope4To7 * (minutes - Band2Min);
            if (minutes <= LateMin)
                return Knot7Attr + AttrSlope7To10 * (minutes - Band3Min);
            return Knot10Attr + LateAttrSlope * (minutes - LateMin);
        }

        public static float IntervalMul(float minutes)
        {
            if (minutes < 0f)
                minutes = 0f;
            if (minutes <= Band2Min)
                return Knot0Int - IntSlope0To4 * minutes;
            if (minutes <= Band3Min)
                return Knot4Int - IntSlope4To7 * (minutes - Band2Min);
            if (minutes <= LateMin)
                return Knot7Int - IntSlope7To10 * (minutes - Band3Min);
            float m = Knot10Int * (1f - LateIntSlope * (minutes - LateMin));
            return Mathf.Max(IntervalFloor, m);
        }

        /// <summary>Null on pass. Checks t=4/7/10 left/right continuity (Δ≈0).</summary>
        public static string ContinuityCheck(float eps = 0.001f)
        {
            float[] knots = { Band2Min, Band3Min, LateMin };
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
