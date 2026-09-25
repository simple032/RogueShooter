using UnityEditor;
using UnityEngine;
using RogueShooter.Vision;

namespace RogueShooter.Tools
{
    public static class L5VisionMenu
    {
        [MenuItem("Tools/JianHai/Run L5 Vision Checks")]
        public static void Run()
        {
            string err = L5VisionChecks.Run();
            if (err == null)
                Debug.Log("[L5] " + L5VisionChecks.FormatPass() + "\n" + L5VisionChecks.Report());
            else
                Debug.LogError("[L5] ACCEPTANCE FAIL " + err + "\n" + L5VisionChecks.Report());
        }
    }
}
