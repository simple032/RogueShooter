using UnityEngine;
using RogueShooter.Vision;

namespace RogueShooter.Demo
{
    /// <summary>
    /// ESC pause menu for the maze run: 继续游戏 / 重新开始本局 / 返回开始界面 / 退出游戏.
    /// Runs before Stage1MazeDemo (order 40 &lt; 50) so one ESC press cannot both
    /// cancel a reward card and open the menu in the same frame; its OnGUI still
    /// draws above the HUD layers.
    /// </summary>
    [DefaultExecutionOrder(40)]
    public sealed class Stage1PauseMenu : MonoBehaviour
    {
        Stage1MazeDemo _demo;

        public void Bind(Stage1MazeDemo demo)
        {
            _demo = demo;
        }

        void Update()
        {
            if (_demo == null)
                _demo = GetComponent<Stage1MazeDemo>();
            if (_demo == null) return;
            bool ended=_demo.ConnSettle || (_demo.GetComponent<Stage1PlayHud>()?.SettlementOpen ?? false);
            if(ended && Input.GetKeyDown(KeyCode.R)) { _demo.RestartRun(); return; }
            if(Input.GetKeyDown(KeyCode.Escape)) HandleEscape();
        }

        public void HandleEscape()
        {
            if(_demo==null)_demo=GetComponent<Stage1MazeDemo>();
            if(_demo==null)return;
            if (_demo.ConnSettle || (_demo.GetComponent<Stage1PlayHud>()?.SettlementOpen ?? false))
            { _demo.ReturnToStartMenu(); return; }
            if (_demo.MenuPaused)
                _demo.CloseMenuPause();
            else if (_demo.CanMenuPause())
                _demo.OpenMenuPause();
        }

        // Same persisted preference as the start-menu toggle, so both menus agree.
        public void ToggleFullscreen()
        {
            bool fullscreen = !Screen.fullScreen;
            Screen.fullScreen = fullscreen;
            PlayerPrefs.SetInt(StartMenuUi.FullscreenKey, fullscreen ? 1 : 0);
            PlayerPrefs.Save();
        }

        void OnGUI()
        {
            if (_demo == null || !_demo.MenuPaused)
                return;

            // Dim the frozen world under a full-screen veil, then draw the panel in
            // 720p logical coordinates so every resolution shares one layout.
            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.62f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;

            float scale = JianHaiUiTheme.Scale;
            Matrix4x4 prevMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float logicalWidth = Screen.width / scale;
            float logicalHeight = Screen.height / scale;
            var panel = new Rect((logicalWidth - 380f) * 0.5f, (logicalHeight - 500f) * 0.5f, 380f, 500f);

            var frame = Resources.Load<Sprite>("JianHaiReward/jh_ui_panel_bone");
            if (frame != null && frame.texture != null)
                GUI.DrawTexture(panel, frame.texture, ScaleMode.StretchToFill);
            else
            {
                GUI.color = new Color(.025f, .032f, .044f, .95f);
                GUI.DrawTexture(panel, Texture2D.whiteTexture);
                GUI.color = new Color(.77f, .63f, .37f);
                GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 2f), Texture2D.whiteTexture);
                GUI.color = prev;
            }

            var title = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter, fontSize = 24, fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.Label(new Rect(panel.x + 1.5f, panel.y + 15f, panel.width, 30f), "游戏已暂停", title);
            GUI.color = Color.white;
            GUI.Label(new Rect(panel.x, panel.y + 14f, panel.width, 30f), "游戏已暂停", title);

            RunEndInfo end = _demo.EndInfo();
            var info = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter, fontSize = 15,
                normal = { textColor = new Color(.86f, .88f, .92f) }
            };
            GUI.Label(new Rect(panel.x, panel.y + 52f, panel.width, 22f),
                "用时 " + end.Seconds + " 秒 · 强化 " + end.Rewards + " · 金币 " + _demo.HeldGold, info);

            if (_demo.Experience != null) GUI.Label(new Rect(panel.x, panel.y+76f, panel.width, 42f), _demo.Experience.PauseText, info);

            var button = JianHaiUiTheme.MenuButton(17);
            float bx = panel.x + 50f;
            float bw = panel.width - 100f;
            if (GUI.Button(new Rect(bx, panel.y + 132f, bw, 42f), "继续游戏", button))
                _demo.CloseMenuPause();
            if (GUI.Button(new Rect(bx, panel.y + 186f, bw, 42f), "重新开始本局", button))
                _demo.RestartFromMenu();
            if (GUI.Button(new Rect(bx, panel.y + 240f, bw, 42f), "返回开始界面", button))
                _demo.ReturnToStartMenu();
            if (GUI.Button(new Rect(bx, panel.y + 294f, bw, 42f), "退出游戏", button))
                _demo.QuitGame();

            // Display options: same button style, height and row pitch as the rows above.
            if (GUI.Button(new Rect(bx, panel.y + 348f, bw, 42f),
                Screen.fullScreen ? "显示模式：全屏" : "显示模式：窗口", button))
                ToggleFullscreen();
            float halfW = (bw - 10f) * 0.5f;
            if (GUI.Button(new Rect(bx, panel.y + 402f, halfW, 42f), "视野：放大", button))
                CameraViewService.SetZoom(CameraViewService.ZoomScale - .1f);
            if (GUI.Button(new Rect(bx + halfW + 10f, panel.y + 402f, halfW, 42f), "视野：缩小", button))
                CameraViewService.SetZoom(CameraViewService.ZoomScale + .1f);

            var hint = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter, fontSize = 13,
                normal = { textColor = new Color(.68f, .71f, .76f) }
            };
            GUI.Label(new Rect(panel.x, panel.y + 456f, panel.width, 24f), "按 ESC 继续游戏", hint);

            GUI.matrix = prevMatrix;
        }
    }
}
