using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace RogueShooter.Iso
{
    public sealed class HeroMeshyLaunch:MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Launch()
        {
            var args=Environment.GetCommandLineArgs();if(Array.IndexOf(args,"--hero-meshy-review")>=0&&SceneManager.GetActiveScene().name!="HeroMeshyPreview")SceneManager.LoadScene("HeroMeshyPreview");
            else if(Array.IndexOf(args,"--hero-meshy-smoke")>=0&&FindObjectOfType<HeroMeshyLaunch>()==null){var go=new GameObject("Hero standalone smoke launcher");DontDestroyOnLoad(go);go.AddComponent<HeroMeshyLaunch>();}
        }
        IEnumerator Start(){SceneManager.LoadScene("Stage1Maze");yield return null;yield return null;gameObject.AddComponent<HeroMeshyRuntimeChecks>();}
    }
}
