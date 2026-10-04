using UnityEditor;
using UnityEngine;
using RogueShooter.Boss;

namespace RogueShooter.Tools
{
    public static class FinalBossMenu
    {
        [MenuItem("Tools/JianHai/Run Final Boss Checks")]
        public static void Run()
        {
            string err = FinalBossChecks.Run();
            if (err == null)
                Debug.Log("[FinalBoss] " + FinalBossChecks.FormatPass());
            else
                Debug.LogError("[FinalBoss] ACCEPTANCE FAIL " + err);
        }
    }
}
