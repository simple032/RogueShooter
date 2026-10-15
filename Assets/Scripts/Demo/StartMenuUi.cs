using UnityEngine;
using UnityEngine.SceneManagement;
using RogueShooter.Player;

namespace RogueShooter.Demo
{
    /// <summary>
    /// Game entry screen. Lives in the StartMenu scene; 开始游戏 loads the
    /// playable Stage1Maze scene, 退出游戏 closes the game.
    /// </summary>
    public sealed class StartMenuUi : MonoBehaviour
    {
        public const string MenuSceneName = "StartMenu";
        public const string GameplaySceneName = "Stage1Maze";
        public const string FullscreenKey = "StartMenu.Fullscreen";

        void Awake()
        {
            // A fresh entry must not inherit any pause state left by a previous run.
            RunPause.ResetAll();
        }

        void Start()
        {
            // Honor the display mode chosen in a previous session (fresh installs stay fullscreen).
            Screen.fullScreen = PlayerPrefs.GetInt(FullscreenKey, 1) != 0;
        }

        public void BeginGameplay()
        {
            SceneManager.LoadScene(GameplaySceneName);
        }

        public void ToggleFullscreen()
        {
            bool fullscreen = !Screen.fullScreen;
            Screen.fullScreen = fullscreen;
            PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void OnGUI()
        {
            float scale = JianHaiUiTheme.Scale;
            Matrix4x4 prevMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float logicalWidth = Screen.width / scale;
            float logicalHeight = Screen.height / scale;
            var panel = new Rect((logicalWidth - 460f) * 0.5f, (logicalHeight - 392f) * 0.5f, 460f, 392f);

            Color prev = GUI.color;
            GUI.color = new Color(.025f, .032f, .044f, .92f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = new Color(.77f, .63f, .37f);
            GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 2f), Texture2D.whiteTexture);
            GUI.color = prev;

            var title = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter, fontSize = 40, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(.95f, .87f, .62f) }
            };
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.Label(new Rect(panel.x + 2f, panel.y + 34f, panel.width, 46f), "箭骸行者", title);
            GUI.color = Color.white;
            GUI.Label(new Rect(panel.x, panel.y + 32f, panel.width, 46f), "箭骸行者", title);

            var sub = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter, fontSize = 15,
                normal = { textColor = new Color(.78f, .74f, .68f) }
            };
            GUI.Label(new Rect(panel.x, panel.y + 84f, panel.width, 22f),
                "单局肉鸽 · " + RunFlow.Label, sub);

            var button = JianHaiUiTheme.MenuButton(20);
            float bx = panel.x + 70f;
            float bw = panel.width - 140f;
            if (GUI.Button(new Rect(bx, panel.y + 128f, bw, 46f), "开始游戏", button))
                BeginGameplay();
            if (GUI.Button(new Rect(bx, panel.y + 186f, bw, 46f), "退出游戏", button))
                QuitGame();

            var toggle = JianHaiUiTheme.MenuButton(17);
            if (GUI.Button(new Rect(bx, panel.y + 242f, bw, 34f),
                Screen.fullScreen ? "显示模式：全屏" : "显示模式：窗口", toggle))
                ToggleFullscreen();

            var hint = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter, fontSize = 13, wordWrap = true,
                normal = { textColor = new Color(.64f, .67f, .72f) }
            };
            GUI.Label(new Rect(panel.x + 30f, panel.y + 288f, panel.width - 60f, 100f),
                "WASD 移动 · 鼠标左键蓄力射击 · Shift 翻滚 · F 近战\nQ 凝神窥机 · E 互动 · ESC 暂停 · 滚轮 缩放视野", hint);

            GUI.matrix = prevMatrix;
        }
    }
}
