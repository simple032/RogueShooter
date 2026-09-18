using System;
using UnityEngine;

namespace RogueShooter.Balance
{
    /// <summary>
    /// Runtime catalog for v0.4.2-LOCK. Gameplay reads these fields instead of literals.
    /// Source of truth: Assets/Resources/BalanceLock_v042.json
    /// </summary>
    [Serializable]
    public class BalanceLockData
    {
        public string lockVersion;
        public string sourceOfTruth;
        public float pSpawn;
        public RarityWeight[] chestRarity;
        public RarityWeight[] altarRarity;
        public ClockBand[] clockBands;
        public WaveSpec[] waves;
        public SegmentMul[] segments;
        public TimeScaleMul[] timeScale;
        public SwitchAnchor[] switchAnchors;
        public string[] noSpawnCoreKinds;
        public float noSpawnRadius;
        public float preReadyMidMin;
        public float clearMedianMin;

        public ClockBand GetClockBand(string id)
        {
            return Find(clockBands, id, b => b.id);
        }

        public WaveSpec GetWave(string id)
        {
            return Find(waves, id, w => w.id);
        }

        public SegmentMul GetSegment(string id)
        {
            return Find(segments, id, s => s.id);
        }

        public TimeScaleMul GetTimeScale(string id)
        {
            return Find(timeScale, id, t => t.id);
        }

        public TimeScaleMul TimeScaleAtMinutes(float minutes)
        {
            if (timeScale == null)
                return null;
            for (int i = 0; i < timeScale.Length; i++)
            {
                TimeScaleMul t = timeScale[i];
                if (t == null)
                    continue;
                if (minutes >= t.tStartMin && minutes < t.tEndMin)
                    return t;
            }

            return timeScale.Length > 0 ? timeScale[timeScale.Length - 1] : null;
        }

        public ClockBand ClockBandAtMinutes(float minutes)
        {
            if (clockBands == null)
                return null;
            for (int i = 0; i < clockBands.Length; i++)
            {
                ClockBand b = clockBands[i];
                if (b == null)
                    continue;
                if (minutes >= b.startMin && minutes < b.endMin)
                    return b;
            }

            return null;
        }

        public float RarityWeight(RarityWeight[] table, string rarity)
        {
            if (table == null)
                return 0f;
            for (int i = 0; i < table.Length; i++)
            {
                if (table[i] != null && table[i].rarity == rarity)
                    return table[i].weight;
            }

            return 0f;
        }

        static T Find<T>(T[] items, string id, Func<T, string> getId) where T : class
        {
            if (items == null || string.IsNullOrEmpty(id))
                return null;
            for (int i = 0; i < items.Length; i++)
            {
                T item = items[i];
                if (item != null && getId(item) == id)
                    return item;
            }

            return null;
        }
    }

    [Serializable]
    public class RarityWeight
    {
        public string rarity;
        public float weight;
        public int score;
    }

    [Serializable]
    public class ClockBand
    {
        public string id;
        public float startMin;
        public float endMin;
    }

    [Serializable]
    public class WaveSpec
    {
        public string id;
        public int count;
        public float tWaveMidSeconds;
    }

    [Serializable]
    public class SegmentMul
    {
        public string id;
        public float eBuildMid;
        public float hpMul;
        public float dmgMul;
        public float intervalMul;
        public float baseIntervalSeconds;
        public string timeRef;
        public float expectedEffInterval;
    }

    [Serializable]
    public class TimeScaleMul
    {
        public string id;
        public float tStartMin;
        public float tEndMin;
        public float enemyAttrMul;
        public float spawnIntervalMul;
    }

    [Serializable]
    public class SwitchAnchor
    {
        public string id;
        public string at;
    }

    public static class BalanceMath
    {
        public static float ComposeSpawnInterval(SegmentMul segment, TimeScaleMul time)
        {
            if (segment == null || time == null)
                return 0f;
            return segment.baseIntervalSeconds * segment.intervalMul * time.spawnIntervalMul;
        }

        public static float FinalHpMul(SegmentMul segment, TimeScaleMul time)
        {
            if (segment == null || time == null)
                return 1f;
            return segment.hpMul * time.enemyAttrMul;
        }

        public static float FinalDmgMul(SegmentMul segment, TimeScaleMul time)
        {
            if (segment == null || time == null)
                return 1f;
            return segment.dmgMul * time.enemyAttrMul;
        }
    }

    public static class BalanceLock
    {
        public const string ResourceName = "BalanceLock_v042";

        public static BalanceLockData Current { get; private set; }

        public static bool TryLoadFromResources(out BalanceLockData data, out string error)
        {
            TextAsset asset = Resources.Load<TextAsset>(ResourceName);
            if (asset == null)
            {
                data = null;
                error = "Missing Resources/" + ResourceName + ".json (v0.4.2-LOCK source of truth)";
                return false;
            }

            return TryLoad(asset.text, out data, out error);
        }

        public static bool TryLoad(string json, out BalanceLockData data, out string error)
        {
            data = null;
            error = null;
            if (string.IsNullOrEmpty(json))
            {
                error = "Balance lock JSON is empty";
                return false;
            }

            try
            {
                data = JsonUtility.FromJson<BalanceLockData>(json);
            }
            catch (Exception e)
            {
                error = "Failed to parse balance lock JSON: " + e.Message;
                return false;
            }

            if (data == null)
            {
                error = "Balance lock JSON parsed to null";
                return false;
            }

            error = Validate(data);
            if (error != null)
            {
                data = null;
                return false;
            }

            Current = data;
            return true;
        }

        public static string Validate(BalanceLockData data)
        {
            if (data.pSpawn <= 0f)
                return "pSpawn must be > 0";
            if (data.chestRarity == null || data.chestRarity.Length < 3)
                return "chestRarity needs C/R/E";
            if (data.altarRarity == null || data.altarRarity.Length < 3)
                return "altarRarity needs C/R/E";
            if (data.segments == null || data.segments.Length < 3)
                return "segments need Z1/Z2/Z3";
            if (data.timeScale == null || data.timeScale.Length < 4)
                return "timeScale needs T0–T3";
            if (data.waves == null || data.waves.Length < 3)
                return "waves need Z1/Z2/Z3";
            if (data.clockBands == null || data.clockBands.Length < 3)
                return "clockBands need Z1/Z2/Z3";
            if (data.noSpawnRadius <= 0f)
                return "noSpawnRadius must be > 0";

            string[] ids = { "Z1", "Z2", "Z3" };
            for (int i = 0; i < ids.Length; i++)
            {
                if (data.GetSegment(ids[i]) == null)
                    return "missing segment " + ids[i];
                if (data.GetWave(ids[i]) == null)
                    return "missing waves " + ids[i];
                if (data.GetClockBand(ids[i]) == null)
                    return "missing clock band " + ids[i];
            }

            string[] times = { "T0", "T1", "T2", "T3" };
            for (int i = 0; i < times.Length; i++)
            {
                if (data.GetTimeScale(times[i]) == null)
                    return "missing time scale " + times[i];
            }

            return null;
        }
    }
}
