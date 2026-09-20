using UnityEngine;

namespace RogueShooter.Boss
{
    /// <summary>OnGUI settle panel: 胜负 / Build / 进门时间段 / 跨段.</summary>
    public sealed class BossSettlePanel : MonoBehaviour
    {
        public bool Visible { get; private set; }
        public BossSettleReport Report { get; private set; }

        public void Show(BossSettleReport report)
        {
            Report = report;
            Visible = true;
            Debug.Log("[BossSettle] " + report.FormatLines());
        }

        public void Hide()
        {
            Visible = false;
        }

        void OnGUI()
        {
            if (!Visible)
                return;

            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = old;

            int w = 420;
            int h = 200;
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;
            GUI.Box(new Rect(x, y, w, h), "");

            var title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            var style = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            string headline = Report.Outcome == BossSettleOutcome.Win ? "通关 · 结算" : "失败 · 结算";
            GUI.Label(new Rect(x + 16, y + 12, w - 32, 28), headline, title);
            GUI.Label(new Rect(x + 16, y + 48, w - 32, 22), "胜负：" + Report.OutcomeLabel, style);
            GUI.Label(new Rect(x + 16, y + 74, w - 32, 22), "Build：" + Report.BuildCount, style);
            GUI.Label(new Rect(x + 16, y + 100, w - 32, 22),
                "进 BOSS 时间段：" + Report.EnterPhaseLabel + " (" + Report.EnterPhaseId + ")", style);
            GUI.Label(new Rect(x + 16, y + 126, w - 32, 22),
                "战中跨段：" + Report.CrossLabel
                + "  (T" + Report.EnterTimeTier + "→T" + Report.EndTimeTier + ")", style);
            GUI.Label(new Rect(x + 16, y + 158, w - 32, 22),
                "dmg " + Report.EnterDmgMul.ToString("0.00") + "→" + Report.EndDmgMul.ToString("0.00"), style);
        }
    }
}
