using System;
using System.Collections.Generic;
using RogueShooter.Maze;
using RogueShooter.Spawning;

namespace RogueShooter.Demo
{
    /// <summary>Independent seeded route/type/decor draws over the approved six wireframes.
    /// Authoring layouts are never mutated. A run's map remains fixed until the next stage.</summary>
    public static class StageMapGen
    {
        public const string Version = "s2s3-production-v1";
        public static int IdHash(string id)
        {
            unchecked { int h = 216613626; foreach (char c in id) h = (h ^ c) * 16777619; return h & int.MaxValue; }
        }
        public static int SubSeed(int runSeed, StageId stage, int salt)
        {
            unchecked
            {
                uint h = (uint)(runSeed ^ ((int)stage * 19349663) ^ (salt * 83492791));
                h ^= h >> 16; h *= 0x85ebca6b; h ^= h >> 13; h *= 0xc2b2ae35; h ^= h >> 16;
                return (int)(h & 0x7fffffff);
            }
        }
        public static S2S3Layout Generate(StageId stage, int runSeed, int largeChestBudget = 2)
        {
            if (stage != StageId.S2 && stage != StageId.S3) throw new ArgumentOutOfRangeException(nameof(stage));
            int pick = new Random(SubSeed(runSeed, stage, 11)).Next(3);
            return GenerateTemplate((stage == StageId.S2 ? "S2_M0" : "S3_M0") + (pick + 1), runSeed, largeChestBudget);
        }
        public static S2S3Layout GenerateTemplate(string id, int runSeed, int largeChestBudget = 2)
        {
            var layout = S2S3Layouts.Fresh(id);
            var stage = layout.Phase3 ? StageId.S3 : StageId.S2;
            layout.Seed = SubSeed(runSeed, stage, 101);
            layout.Production = true;
            var rng = new Random(SubSeed(runSeed, stage, IdHash(id) ^ 23));
            if (id == "S2_M01")
            {
                bool flip = rng.Next(2) == 0;
                layout.Find("C").Type = flip ? S2S3RoomType.Chest : S2S3RoomType.Mechanism;
                layout.Find("D").Type = flip ? S2S3RoomType.Mechanism : S2S3RoomType.Chest;
            }
            else if (id == "S2_M02" || id == "S2_M03")
            {
                // Choose uniformly from legal assignments instead of retrying an arbitrary number of times.
                var choices = new List<S2S3RoomType[]>();
                string[] slots = { "B", "C", "D", "E", "F" };
                for (int mech = 0; mech < 5; mech++)
                for (int box1 = 0; box1 < 5; box1++)
                for (int box2 = box1 + 1; box2 < 5; box2++)
                {
                    if (mech == box1 || mech == box2 || (box1 < 3 && box2 < 3)) continue;
                    if (id == "S2_M03" && mech >= 3) continue;
                    var types = new[] { S2S3RoomType.Normal, S2S3RoomType.Normal, S2S3RoomType.Normal, S2S3RoomType.Normal, S2S3RoomType.Normal };
                    types[mech] = S2S3RoomType.Mechanism; types[box1] = types[box2] = S2S3RoomType.Chest;
                    choices.Add(types);
                }
                var choice = choices[rng.Next(choices.Count)];
                for (int i = 0; i < slots.Length; i++) layout.Find(slots[i]).Type = choice[i];
            }
            else if (id == "S3_M02")
            {
                string[] slots = { "B", "C", "E", "F" };
                int first = rng.Next(4), second = rng.Next(3);
                if (second >= first) second++;
                for (int i = 0; i < slots.Length; i++)
                    layout.Find(slots[i]).Type = i == first || i == second ? S2S3RoomType.Chest : S2S3RoomType.Normal;
            }
            else if (id == "S3_M03")
            {
                string[] slots = { "B", "C", "F" };
                int box = rng.Next(3);
                for (int i = 0; i < slots.Length; i++)
                    layout.Find(slots[i]).Type = i == box ? S2S3RoomType.Chest : S2S3RoomType.Normal;
            }
            foreach (var room in layout.Rooms)
                room.DecorVariant = new Random(SubSeed(runSeed, stage, IdHash(room.Id) ^ 733)).Next(4);
            var chestRng = new Random(SubSeed(runSeed, stage, 919));
            if (largeChestBudget > 0 && chestRng.Next(100) < 30)
            {
                var chests = Array.FindAll(layout.Rooms, r => r.Type == S2S3RoomType.Chest);
                chests[chestRng.Next(chests.Length)].LargeChest = true;
            }
            layout.PickNote = "Seeded production map " + Version;
            return layout;
        }
    }
}
