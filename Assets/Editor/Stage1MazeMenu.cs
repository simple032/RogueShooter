using UnityEditor;
using UnityEngine;
using RogueShooter.Maze;
using RogueShooter.Spawning;
using RogueShooter.Combat;

namespace RogueShooter.Tools
{
    public static class Stage1MazeMenu
    {
        [MenuItem("Tools/JianHai/Sample Stage-1 Maze (DRAFT)")]
        public static void Sample()
        {
            string dir = EnemyPoolDraft.ResolveDirectory();
            if (!EnemyPoolDraft.TryLoadFromDirectory(dir, out string loadErr))
                Debug.LogWarning("[S1Maze] draft CSV " + loadErr);

            string path = Stage1MazeSampler.WriteDefault();
            string playPath = Stage1PlayableSampler.StreamingPath();
            string err = Stage1MazeChecks.Run();
            if (err == null)
                Debug.Log("[S1Maze] " + Stage1MazeChecks.FormatPass() + "\nfile=" + path + "\nplayable=" + playPath);
            else
                Debug.LogError("[S1Maze] ACCEPTANCE FAIL " + err + "\nfile=" + path);
        }
    }
}
