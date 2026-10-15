using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using RogueShooter.Iso;
using RogueShooter.Maze;

namespace RogueShooter.Demo
{
    /// <summary>
    /// Builds one hand-authored S2/S3 sample-house layout at runtime, the same
    /// way Stage1MazeDemo dresses the stage-1 maze: Stage1IsoArt paints floors,
    /// room shells and frame doors, S2S3CorridorShell clips the corridor walls
    /// at turns and forks, S2S3PhaseDress adds the phase look plus the copper
    /// gate / sector fixtures, and the labels spell out room types and first-
    /// room contents. Acceptance checks run at Start and log one line per map.
    /// </summary>
    public sealed class S2S3SampleRig : MonoBehaviour
    {
        [SerializeField] string templateId = "S2_M01";
        [SerializeField] bool configureCamera = true;
        [SerializeField] bool showLabels = false;
        public bool ShowLabels { get { return showLabels; } set { showLabels = value; ApplyLabels(); } }

        int _focusRoom = -1;
        bool _nearView;
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.L)) ShowLabels = !ShowLabels;
            if (Layout == null) return;
            if (Input.GetKeyDown(KeyCode.Tab)) FocusRoom((_focusRoom + 1) % Layout.Rooms.Length);
            if (Input.GetKeyDown(KeyCode.V)) { _focusRoom = -1; FitCamera(Layout); }
            if (Input.GetKeyDown(KeyCode.Z)) { _nearView = !_nearView; FocusRoom(Mathf.Max(0, _focusRoom)); }
        }
        public void FocusRoom(int index)
        {
            if (Layout == null || index < 0 || index >= Layout.Rooms.Length || Camera.main == null) return;
            _focusRoom = index;
            Vector2 c = Layout.Rooms[index].Center;
            Camera.main.orthographicSize = _nearView ? 6f : 12.2f;
            Camera.main.transform.position = new Vector3(c.x, c.y + .9f, -12f);
        }
        void ApplyLabels()
        {
            foreach (var text in GetComponentsInChildren<TextMesh>(true))
            {
                var renderer = text.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.enabled = showLabels;
            }
        }

        readonly List<GameObject> _world = new List<GameObject>();
        bool _built;

        public string TemplateId { get { return templateId; } set { templateId = value; } }
        public bool ConfigureCamera { get { return configureCamera; } set { configureCamera = value; } }
        public S2S3Layout Layout { get; private set; }

        void Start()
        {
            if (!_built)
                Build();
        }

        public void Build()
        {
            _built = true;
            S2S3Layout layout = S2S3Layouts.Build(templateId);
            Stage1Maze maze = layout.ToMaze();
            Layout = layout;
            var root = new GameObject("S2S3 " + layout.Id).transform;
            root.SetParent(transform, false);

            GameObject floorRoot = Stage1IsoArt.BuildFloors(maze, root);
            Tilemap floors = floorRoot != null ? floorRoot.GetComponentInChildren<Tilemap>() : null;

            var doors = new List<GameObject>();
            for (int i = 0; i < layout.Rooms.Length; i++)
            {
                S2S3Room room = layout.Rooms[i];
                MazeNode node = maze.Find(room.Id);
                // Stage 3 keeps fire inside the great altar only, and the altar
                // places its own four braziers clear of the bone pillars.
                bool braziers = !layout.Phase3;
                Stage1IsoArt.BuildRoomShell(node, maze, root, _world, S2S3Layouts.Seed, doors, braziers);
                GameObject prop = Stage1IsoArt.BuildProp(node, root);
                if (prop != null)
                {
                    _world.Add(prop);
                    if (layout.Phase3 && room.Type == S2S3RoomType.Altar)
                        prop.transform.localScale = Vector3.one * 1.3f;
                }
                S2S3PhaseDress.DressRoom(layout, room, maze, root, _world);
            }

            S2S3CorridorShell.Build(layout, root, _world);
            S2S3PhaseDress.FinishPhase(layout, maze, root, _world, floors);
            S2S3PhaseDress.StandIns(layout, root, _world);
            S2S3PhaseDress.Labels(layout, maze, root, _world);
            ApplyLabels();

            if (configureCamera)
                FitCamera(layout);

            string err = S2S3SampleChecks.Run(templateId);
            if (err == null)
                Debug.Log("[S2S3] " + templateId + " ACCEPTANCE PASS | " + S2S3SampleChecks.FormatPass(templateId));
            else
                Debug.LogError("[S2S3] " + templateId + " ACCEPTANCE FAIL " + err);
        }

        public void TearDown()
        {
            for (int i = 0; i < _world.Count; i++)
            {
                if (_world[i] != null)
                    Destroy(_world[i].gameObject);
            }
            _world.Clear();
            for (int c = transform.childCount - 1; c >= 0; c--)
                Destroy(transform.GetChild(c).gameObject);
            _built = false;
            Layout = null;
        }

        // ---- shared camera framing ----

        public static Rect FitRect(S2S3Layout layout)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < layout.Rooms.Length; i++)
            {
                Vector2 c = layout.Rooms[i].Center;
                if (c.x - 14f < minX) minX = c.x - 14f;
                if (c.x + 14f > maxX) maxX = c.x + 14f;
                if (c.y - 11f < minY) minY = c.y - 11f;
                if (c.y + 11f > maxY) maxY = c.y + 11f;
            }
            return Rect.MinMaxRect(minX - 6f, minY - 7f, maxX + 6f, maxY + 17f);
        }

        public static Camera FitCamera(S2S3Layout layout)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.045f, 0.06f);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;
            Rect r = FitRect(layout);
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            if (aspect < 0.5f)
                aspect = 1.6f;
            float halfH = r.height * 0.5f;
            float halfW = r.width * 0.5f / aspect;
            cam.orthographicSize = Mathf.Max(halfH, halfW);
            cam.transform.position = new Vector3(r.center.x, r.center.y, -12f);
            if (cam.GetComponent<IsoSortAxis>() == null)
                cam.gameObject.AddComponent<IsoSortAxis>();
            return cam;
        }
    }

    /// <summary>
    /// Verify-scene driver: shows one S2/S3 layout at a time so each can be
    /// recorded on its own without the six maps stacking at the shared origin.
    /// </summary>
    public sealed class S2S3VerifyDriver : MonoBehaviour
    {
        GameObject _current;

        public int Show(int index)
        {
            if (index < 0 || index >= S2S3Layouts.All.Length)
                return -1;
            if (_current != null)
                Destroy(_current);
            string id = S2S3Layouts.All[index];
            _current = new GameObject("S2S3Verify " + id);
            _current.transform.SetParent(transform, false);
            var rig = _current.AddComponent<S2S3SampleRig>();
            rig.TemplateId = id;
            rig.ConfigureCamera = false;
            rig.Build();
            S2S3SampleRig.FitCamera(rig.Layout);
            return index;
        }
    }
}
