using UnityEngine;

namespace JianHai.ArtSamples
{
    // Local inspection controls only; no dependency on run state or the main scene.
    public sealed class S1MapRoomWalkthrough : MonoBehaviour
    {
        public Transform Player;
        public Camera View;
        public Sprite[] Idle;
        public Sprite[] Walk;
        public bool ShowHelp = true;
        int facing = 6;
        float frame;
        int viewMode = 2;
        bool moving;
        readonly Vector3[] viewpoints = { Vector3.zero, new Vector3(0, 7, 0), new Vector3(0, -9, 0), new Vector3(0, 0, 0) };

        public void Inspect(int number)
        {
            viewMode = Mathf.Clamp(number, 1, 4);
            View.orthographicSize = viewMode == 4 ? 15 : 6;
            View.transform.position = viewpoints[viewMode - 1] + new Vector3(0, 0, -10);
        }

        public void MoveForReview(Vector2 delta)
        {
            Move(delta);
        }

        void Move(Vector2 delta)
        {
            moving = delta.sqrMagnitude > 0.000001f;
            if (!moving) return;
            facing = (Mathf.RoundToInt(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg / 45) + 8) % 8;
            Vector3 p = Player.position + (Vector3)delta;
            bool entrance = Mathf.Abs(p.x) < 1.5f && p.y < -10;
            p.x = Mathf.Clamp(p.x, -13.3f, 13.3f);
            p.y = Mathf.Clamp(p.y, entrance ? -14 : -10.3f, 9.7f);
            // Solid focal prop; the decorative pieces elsewhere remain outside the walk area.
            if (Mathf.Abs(p.x) < 1.1f && p.y > 6.3f && p.y < 8.7f) p = Player.position;
            Player.position = p;
            viewMode = 0;
            View.orthographicSize = 6;
            View.transform.position = p + new Vector3(0, 0.65f, -10);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) Inspect(1);
            if (Input.GetKeyDown(KeyCode.Alpha2)) Inspect(2);
            if (Input.GetKeyDown(KeyCode.Alpha3)) Inspect(3);
            if (Input.GetKeyDown(KeyCode.Alpha4)) Inspect(4);
            if (Input.GetKeyDown(KeyCode.H)) ShowHelp = !ShowHelp;
            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (input.sqrMagnitude > 0) Move(input.normalized * (6 * Time.deltaTime));
            else moving = false;
            frame += Time.deltaTime * (moving ? 12 : 4);
            SpriteRenderer sr = Player.GetComponent<SpriteRenderer>();
            Sprite[] source = moving ? Walk : Idle;
            int span = moving ? 6 : 3;
            if (source != null && source.Length >= 8 * span) sr.sprite = source[facing * span + (int)frame % span];
        }

        void OnGUI()
        {
            if (!ShowHelp) return;
            GUI.color = new Color(1, 1, 1, .86f);
            GUI.Box(new Rect(16, Screen.height - 43, 635, 27), "ART SAMPLE   /   WASD move   1 Court   2 Altar   3 Entry   4 Overview   H Hide");
        }
    }

}
