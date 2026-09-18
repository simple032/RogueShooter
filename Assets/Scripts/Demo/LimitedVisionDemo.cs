using System.Collections;
using System.Text;
using UnityEngine;
using RogueShooter.Art;
using RogueShooter.Player;
using RogueShooter.Spawning;
using RogueShooter.Vision;

namespace RogueShooter.Demo
{
    /// <summary>
    /// Generic test corridor for limited vision + spawn-skip-in-view.
    /// Layout is not the campaign map (path count / anchor IDs still TBD).
    /// </summary>
    public class LimitedVisionDemo : MonoBehaviour
    {
        [Tooltip("Serialized on LimitedVisionDemo.scene — Unity uses that value, not the C# initializer. Keep CameraViewService.LockedOrthographicSize (2.75).")]
        [SerializeField] float orthographicSize = CameraViewService.LockedOrthographicSize;
        [SerializeField] float moveSpeed = 7f;
        [SerializeField] float spawnRetryInterval = 0.35f;

        SpawnAnchor[] _anchors;
        AnchorSpawner _spawner;
        CameraViewService _view;
        bool _acceptanceChecked;
        bool _acceptancePass;
        string _acceptanceLine = "waiting…";

        IEnumerator Start()
        {
            BuildWorld();
            yield return null;
            _spawner.ArmAndEvaluate("start");
            RunAcceptanceCheck();
        }

        void BuildWorld()
        {
            Transform root = transform;

            DemoPrimitives.Quad("Floor_Corridor", new Vector3(0f, 0f, 1f), new Vector2(64f, 6f),
                new Color(0.20f, 0.19f, 0.18f), 0, root);

            Color wall = new Color(0.07f, 0.08f, 0.09f);
            DemoPrimitives.Quad("Wall_N", new Vector3(0f, 3.35f, 1f), new Vector2(64f, 0.7f), wall, 1, root);
            DemoPrimitives.Quad("Wall_S", new Vector3(0f, -3.35f, 1f), new Vector2(64f, 0.7f), wall, 1, root);
            DemoPrimitives.Quad("Wall_W", new Vector3(-32.35f, 0f, 1f), new Vector2(0.7f, 7f), wall, 1, root);
            DemoPrimitives.Quad("Wall_E", new Vector3(32.35f, 0f, 1f), new Vector2(0.7f, 7f), wall, 1, root);

            DemoPrimitives.Quad("Marker_West", new Vector3(-30f, 0f, 0f), new Vector2(1.4f, 1.4f),
                new Color(0.85f, 0.25f, 0.75f), 2, root);
            DemoPrimitives.Quad("Marker_East", new Vector3(30f, 0f, 0f), new Vector2(1.4f, 1.4f),
                new Color(0.25f, 0.85f, 0.40f), 2, root);

            GameObject player = new GameObject("Player");
            player.transform.position = Vector3.zero;
            JianHaiBind.ApplyTo(player, JianHaiArtCatalog.PlayerIdle);
            player.AddComponent<PlayerMotor2D>().Configure(moveSpeed);

            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                cam = camGo.AddComponent<Camera>();
                camGo.tag = "MainCamera";
                camGo.AddComponent<AudioListener>();
            }

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.045f, 0.06f);
            cam.orthographic = true;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;

            _view = cam.GetComponent<CameraViewService>();
            if (_view == null)
                _view = cam.gameObject.AddComponent<CameraViewService>();
            _view.Configure(orthographicSize);

            CameraFollow2D follow = cam.GetComponent<CameraFollow2D>();
            if (follow == null)
                follow = cam.gameObject.AddComponent<CameraFollow2D>();
            follow.SetTarget(player.transform);
            AimReticle.Ensure();

            _anchors = new[]
            {
                MakeAnchor("Demo_InView", new Vector3(0f, 1.2f, 0f), new Color(0.35f, 0.75f, 1f), root),
                MakeAnchor("Demo_Near", new Vector3(2.2f, 0f, 0f), new Color(0.35f, 0.75f, 1f), root),
                MakeAnchor("Demo_East", new Vector3(24f, 0f, 0f), new Color(1f, 0.82f, 0.25f), root),
                MakeAnchor("Demo_West", new Vector3(-24f, 0f, 0f), new Color(1f, 0.82f, 0.25f), root),
            };

            GameObject stubPrefab = new GameObject("StubEnemyPrefab");
            stubPrefab.transform.SetParent(root, false);
            stubPrefab.SetActive(false);
            JianHaiBind.ApplyTo(stubPrefab, JianHaiArtCatalog.EnemyE1Idle);
            stubPrefab.AddComponent<StubEnemy>();

            _spawner = gameObject.AddComponent<AnchorSpawner>();
            _spawner.Bind(_anchors, stubPrefab, spawnRetryInterval);
        }

        static SpawnAnchor MakeAnchor(string id, Vector3 pos, Color color, Transform parent)
        {
            GameObject go = DemoPrimitives.Quad("Anchor_" + id, pos, new Vector2(0.55f, 0.55f), color, 5, parent);
            var anchor = go.AddComponent<SpawnAnchor>();
            anchor.Configure(id, pos);
            return anchor;
        }

        void RunAcceptanceCheck()
        {
            if (_view == null || _spawner == null)
            {
                _acceptanceChecked = true;
                _acceptancePass = false;
                _acceptanceLine = "FAIL missing view/spawner";
                Debug.LogError("[LimitedVision] " + _acceptanceLine);
                return;
            }

            Rect rect = _view.GetViewRect();
            bool pass = true;
            var sb = new StringBuilder();
            sb.Append($"view={rect.xMin:F2},{rect.yMin:F2}..{rect.xMax:F2},{rect.yMax:F2} size={orthographicSize}");
            bool orthoOk = Mathf.Abs(orthographicSize - CameraViewService.LockedOrthographicSize) < 0.01f
                           && Mathf.Abs(_view.OrthographicSize - CameraViewService.LockedOrthographicSize) < 0.01f;
            pass &= orthoOk;
            if (!orthoOk)
                sb.Append(" orthoFAIL");

            for (int i = 0; i < _anchors.Length; i++)
            {
                SpawnAnchor a = _anchors[i];
                bool inView = _view.IsInCameraView(a.WorldPosition);
                bool spawned = _spawner.WasSpawned(a);
                bool ok = inView != spawned;
                pass &= ok;
                sb.Append($" | {a.AnchorId} inView={inView} spawned={spawned} {(ok ? "ok" : "FAIL")}");
            }

            _acceptanceChecked = true;
            _acceptancePass = pass;
            _acceptanceLine = (pass ? "ACCEPTANCE PASS" : "ACCEPTANCE FAIL") + " " + sb;
            if (pass)
                Debug.Log("[LimitedVision] " + _acceptanceLine);
            else
                Debug.LogError("[LimitedVision] " + _acceptanceLine);
        }

        void OnGUI()
        {
            const int pad = 10;
            int w = 520;
            int h = 220;
            GUI.Box(new Rect(pad, pad, w, h), "");
            var style = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            var title = new GUIStyle(style) { fontSize = 16, fontStyle = FontStyle.Bold };
            GUI.Label(new Rect(pad + 8, pad + 6, w - 16, 24), "有限视野 / Limited Vision  (no fog)", title);

            Rect rect = _view != null ? _view.GetViewRect() : new Rect();
            GUI.Label(new Rect(pad + 8, pad + 32, w - 16, 48),
                $"WASD / Arrows. Local view — far ends (next chest / branch stand-in) stay off-screen.\n" +
                $"GetViewRect() = ({rect.xMin:F1},{rect.yMin:F1})..({rect.xMax:F1},{rect.yMax:F1})  " +
                $"orthoSize={orthographicSize}  (16:9 half-width ≈ {orthographicSize * 16f / 9f:F1})",
                style);

            string status = !_acceptanceChecked
                ? "acceptance: waiting for first camera frame…"
                : (_acceptancePass
                    ? "<color=#88ff88>ACCEPTANCE PASS</color>"
                    : "<color=#ff8888>" + _acceptanceLine + "</color>");
            var rich = new GUIStyle(style) { richText = true };
            GUI.Label(new Rect(pad + 8, pad + 84, w - 16, 22), status, rich);

            if (_anchors == null || _spawner == null)
                return;

            float y = pad + 108;
            for (int i = 0; i < _anchors.Length; i++)
            {
                SpawnAnchor a = _anchors[i];
                bool inView = _view.IsInCameraView(a.WorldPosition);
                string line = $"{a.AnchorId,-16} ({a.WorldPosition.x,5:F1},{a.WorldPosition.y,5:F1})  " +
                              (inView ? "IN VIEW → skip spawn" : "OUT OF VIEW") + "  " +
                              _spawner.ReasonFor(a);
                GUI.Label(new Rect(pad + 8, y, w - 16, 18), line, style);
                y += 18;
            }

            DrawViewBorder();
        }

        static void DrawViewBorder()
        {
            Color old = GUI.color;
            GUI.color = new Color(0.35f, 0.9f, 1f, 0.85f);
            const int t = 3;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, Screen.height - t, Screen.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, 0, t, Screen.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(Screen.width - t, 0, t, Screen.height), Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
