using System.Collections.Generic;

namespace RogueShooter.StageMap
{
    /// <summary>
    /// Stitch stages: only previous Altar CONN → next START. No exits on Normal/Chest/START/corridor.
    /// </summary>
    public sealed class StageChain
    {
        public int RunSeed;
        public StageMapGraph[] Stages;
        public StageMapEdge[] InterStageLinks;

        public StageMapGraph Stage(int index)
        {
            if (Stages == null || index < 0 || index >= Stages.Length)
                return null;
            return Stages[index];
        }
    }

    public static class StageChainBuilder
    {
        public static StageChain Build(int runSeed, int stageCount)
        {
            if (stageCount < 1)
                stageCount = 1;

            var stages = new StageMapGraph[stageCount];
            var links = new List<StageMapEdge>();

            for (int s = 0; s < stageCount; s++)
            {
                int seed = StageMapGenerator.StageSeed(runSeed, s);
                stages[s] = StageMapGenerator.Generate(seed, s);
            }

            for (int s = 0; s < stageCount - 1; s++)
            {
                StageMapGraph a = stages[s];
                StageMapGraph b = stages[s + 1];
                links.Add(new StageMapEdge
                {
                    FromId = a.NextStageExitId,
                    ToId = b.StartId,
                    IsCorridor = true
                });
            }

            return new StageChain
            {
                RunSeed = runSeed,
                Stages = stages,
                InterStageLinks = links.ToArray()
            };
        }
    }
}
