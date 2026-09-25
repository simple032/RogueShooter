using UnityEditor;
using UnityEngine;
using RogueShooter.Iso;

namespace RogueShooter.Tools
{
    public static class IsoProjectionMenu
    {
        [MenuItem("Tools/JianHai/Run Iso Projection Checks")]
        public static void Run()
        {
            string err = IsoProjectionChecks.Run();
            if (err == null)
                Debug.Log("[Iso] " + IsoProjectionChecks.FormatPass());
            else
                Debug.LogError("[Iso] ACCEPTANCE FAIL " + err);
        }
    }
}
