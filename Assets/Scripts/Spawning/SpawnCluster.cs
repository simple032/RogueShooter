using UnityEngine;

namespace RogueShooter.Spawning
{
    /// <summary>
    /// Tunable multi-mob spawn near one in-view point. Count/radius filled when numeric/level locks numbers.
    /// Default: 2–3 stubs within Radius.
    /// </summary>
    public static class SpawnCluster
    {
        public static int CountMin = 2;
        public static int CountMax = 3;
        public static float Radius = 0.85f;

        public static void Configure(int countMin, int countMax, float radius)
        {
            CountMin = Mathf.Max(1, countMin);
            CountMax = Mathf.Max(CountMin, countMax);
            Radius = Mathf.Max(0.05f, radius);
        }

        public static int RollCount()
        {
            if (CountMax <= CountMin)
                return CountMin;
            return Random.Range(CountMin, CountMax + 1);
        }

        public static Vector3 Offset(Vector3 center, int index, int total)
        {
            if (total <= 1 || Radius <= 0.01f)
                return center;
            float ang = (Mathf.PI * 2f * index) / total + 0.35f;
            return center + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * Radius;
        }
    }
}
