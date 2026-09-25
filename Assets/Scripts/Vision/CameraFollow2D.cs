using System;
using UnityEngine;
using RogueShooter.Iso;

namespace RogueShooter.Vision
{
    /// <summary>
    /// Follows a target with an orthographic camera. Z stays at <see cref="offset"/>.
    /// Optional room clamp (<see cref="RoomBounds"/>): while the target is inside a room the view rect is kept
    /// inside the room's box — iso on: the room's projected (view-space) bounding box — and centred on the room
    /// on an axis where the view is larger than the room (<see cref="ClampViewCentre"/>, the same math
    /// L5VisionChecks measures). A clamp on/off switch (entering / leaving a room, teleport) glides instead of
    /// jumping; otherwise the camera snaps every frame as before.
    /// </summary>
    public class CameraFollow2D : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector3 offset = new Vector3(0f, 0f, -10f);

        /// <summary>Glide speed floor (u/s); faster than any move speed so the glide always catches up.</summary>
        public const float CatchUpSpeed = 40f;
        /// <summary>Goal jumps larger than this in one frame start a glide.</summary>
        public const float JumpThreshold = 0.75f;

        /// <summary>Logic room rect containing a logic position, or null (corridor → no clamp).</summary>
        public Func<Vector3, Rect?> RoomBounds;

        Camera _cam;
        Vector3 _lastGoal;
        bool _hasGoal;
        bool _gliding;

        public Transform Target => target;
        public bool Clamped { get; private set; }
        public bool Gliding => _gliding;

        public void SetTarget(Transform followTarget)
        {
            target = followTarget;
            SnapNow();
        }

        /// <summary>Hard snap to the (clamped) goal; cancels any glide.</summary>
        public void SnapNow()
        {
            if (target == null)
                return;
            Vector3 goal = Goal();
            transform.position = goal;
            _lastGoal = goal;
            _hasGoal = true;
            _gliding = false;
        }

        /// <summary>Camera position the follow aims for this frame (view space + offset).</summary>
        public Vector3 Goal()
        {
            // Iso on: the camera frames the isometric view plane (logic → view). Iso off: identity.
            Vector3 view = ViewSpace.LogicToCamera(target.position);
            Clamped = false;
            if (RoomBounds != null)
            {
                Rect? room = RoomBounds(target.position);
                if (room.HasValue)
                {
                    if (_cam == null)
                        _cam = GetComponent<Camera>();
                    if (_cam != null && _cam.orthographic)
                    {
                        view = ClampViewCentre(room.Value, view, _cam.orthographicSize, CameraViewMath.ResolveAspect(_cam), ViewSpace.IsoOn);
                        Clamped = true;
                    }
                }
            }

            return view + offset;
        }

        void LateUpdate()
        {
            if (target == null)
                return;
            Vector3 goal = Goal();
            if (!_hasGoal || RoomBounds == null)
            {
                // No room clamp (other demos): snap every frame exactly as before.
                SnapNow();
                return;
            }

            if ((goal - _lastGoal).sqrMagnitude > JumpThreshold * JumpThreshold)
                _gliding = true;
            _lastGoal = goal;
            if (!_gliding)
            {
                transform.position = goal;
                return;
            }

            Vector3 p = transform.position;
            float d = Vector3.Distance(p, goal);
            float step = Mathf.Max(CatchUpSpeed, d * 8f) * Time.unscaledDeltaTime;
            if (d <= step || d < 0.01f)
            {
                transform.position = goal;
                _gliding = false;
                return;
            }

            transform.position = Vector3.MoveTowards(p, goal, step);
        }

        /// <summary>
        /// Camera centre (view space) for a view of <paramref name="ortho"/> × aspect kept inside the room's box.
        /// Iso on: the box is the room's four corners projected to view space. Centred on the room on an axis
        /// where the view is wider than that box.
        /// </summary>
        public static Vector3 ClampViewCentre(Rect roomLogic, Vector3 desiredView, float ortho, float aspect, bool iso)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                var c = new Vector3((i & 1) == 0 ? roomLogic.xMin : roomLogic.xMax, (i & 2) == 0 ? roomLogic.yMin : roomLogic.yMax, 0f);
                Vector3 v = iso ? IsoProjection.LogicToView(c) : c;
                minX = Mathf.Min(minX, v.x);
                maxX = Mathf.Max(maxX, v.x);
                minY = Mathf.Min(minY, v.y);
                maxY = Mathf.Max(maxY, v.y);
            }

            float halfH = ortho, halfW = ortho * aspect;
            float cx = maxX - minX <= 2f * halfW ? (minX + maxX) * 0.5f : Mathf.Clamp(desiredView.x, minX + halfW, maxX - halfW);
            float cy = maxY - minY <= 2f * halfH ? (minY + maxY) * 0.5f : Mathf.Clamp(desiredView.y, minY + halfH, maxY - halfH);
            return new Vector3(cx, cy, desiredView.z);
        }
    }
}
