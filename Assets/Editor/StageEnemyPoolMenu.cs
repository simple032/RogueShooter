using UnityEditor;
using UnityEngine;
using RogueShooter.Spawning;

namespace RogueShooter.Tools
{
    public static class StageEnemyPoolMenu
    {
        [MenuItem("Tools/JianHai/Sample Stage Enemy Pools (DRAFT)")]
        public static void Sample()
        {
            string dir = EnemyPoolDraft.ResolveDirectory();
            if (!EnemyPoolDraft.TryLoadFromDirectory(dir, out string loadErr))
            {
                Debug.LogError("[StagePool] " + loadErr);
                return;
            }

            string path = EnemyStagePoolSampler.WriteDefault();
            string err = StageEnemyPoolChecks.Run();
            if (err == null)
                Debug.Log("[StagePool] " + StageEnemyPoolChecks.FormatPass() + "\ncsv=" + path);
            else
                Debug.LogError("[StagePool] ACCEPTANCE FAIL " + err + "\ncsv=" + path);
        }
    }
}
