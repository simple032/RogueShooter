using System.Collections.Generic;
using UnityEngine;
using RogueShooter.Art;
using RogueShooter.Maze;
using RogueShooter.Player;

namespace RogueShooter.Demo
{
    /// <summary>A training-only arrow body; no AI, HP, rewards or combat-room bookkeeping.</summary>
    public sealed class StartTrainingTarget : MonoBehaviour
    {
        static readonly List<StartTrainingTarget> Live = new List<StartTrainingTarget>();
        static Sprite Board;
        StartRoomTutorial _owner;
        MazeNode _room;
        Rect _solid;
        bool _hasSolid;

        public static StartTrainingTarget Create(StartRoomTutorial owner, MazeNode room, Transform parent)
        {
            var go = new GameObject("StartTrainingTarget");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(room.Center.X+3.4f,room.Center.Y,0f);
            var view = go.AddComponent<SpriteRenderer>();
            view.sprite = BoardSprite();
            view.sortingLayerName = JianHaiArtCatalog.LayerEntity;
            view.spriteSortPoint = SpriteSortPoint.Pivot;
            var target = go.AddComponent<StartTrainingTarget>();
            target._owner = owner; target._room = room;
            Vector2 size = new Vector2(.65f,.4f);
            var collider = go.AddComponent<BoxCollider2D>(); collider.size = size;
            target._solid = new Rect((Vector2)go.transform.position-size*.5f,size);
            room.SolidProps.Add(target._solid); target._hasSolid = true;
            return target;
        }

        void OnEnable() { Live.Add(this); }
        void OnDisable()
        {
            Live.Remove(this);
            if (_hasSolid && _room != null) _room.SolidProps.Remove(_solid);
            _hasSolid = false;
        }
        bool Accepts(Transform shooter) => _owner != null && _owner.Active
            && _owner.Step != StartRoomTutorial.Lesson.Move && _owner.transform == shooter;

        public static StartTrainingTarget FirstHit(Transform shooter, Vector3 a, Vector3 b,
            float limit, out float at)
        {
            StartTrainingTarget best = null; at = limit;
            foreach (var target in Live)
            {
                if (target == null || !target.Accepts(shooter)) continue;
                float t = MazeCollision.CircleEntryT(target.transform.position,a,b,.5f);
                if (t <= 1f && t < at-.0001f) { at=t; best=target; }
            }
            return best;
        }
        public static bool TryAim(Transform shooter, Vector3 cursor, out Vector3 direction)
        {
            direction = Vector3.zero;
            foreach (var target in Live)
            {
                if (target == null || !target.Accepts(shooter)) continue;
                Vector3 face = target.transform.position + Vector3.up*.87f;
                if (Vector2.Distance(cursor,face) > .55f) continue;
                direction = target.transform.position-shooter.position; direction.z=0f;
                if (direction.sqrMagnitude < .001f) continue;
                direction.Normalize(); return true;
            }
            return false;
        }
        /// <summary>Point flavour of TryAim for point-aimed arrows: the cursor-close
        /// training face becomes the shot's landing point.</summary>
        public static bool TryAimPoint(Transform shooter, Vector3 cursor, out Vector3 point)
        {
            point = Vector3.zero;
            foreach (var target in Live)
            {
                if (target == null || !target.Accepts(shooter)) continue;
                Vector3 face = target.transform.position + Vector3.up*.87f;
                if (Vector2.Distance(cursor,face) > .55f) continue;
                Vector3 direction = target.transform.position-shooter.position; direction.z=0f;
                if (direction.sqrMagnitude < .001f) continue;
                point = face; return true;
            }
            return false;
        }
        public void Receive(ChargeShotKind kind, int shotSerial, Vector3 origin)
        {
            ArrowImpactFeedback.Confirm(gameObject,origin,kind==ChargeShotKind.Crit,false);
            _owner?.OnTargetHit(kind,shotSerial);
        }

        static Sprite BoardSprite()
        {
            if (Board != null) return Board;
            var texture = new Texture2D(256,256,TextureFormat.RGBA32,false);
            texture.name="StartTutorialTarget"; texture.filterMode=FilterMode.Bilinear;
            var pixels=new Color32[256*256];
            Color32 wood=new Color32(95,69,44,255), edge=new Color32(42,38,33,255);
            for (int y=0;y<256;y++) for(int x=0;x<256;x++)
            {
                int dx=x-128,dy=y-144; float distance=Mathf.Sqrt(dx*dx+dy*dy);
                Color32 c=new Color32(0,0,0,0);
                if (y>=32 && y<120 && x>=120 && x<=136) c=wood;
                if (y>=31 && y<=39 && x>=98 && x<=158) c=edge;
                if(distance<=54f) c=edge;
                if(distance<=50f) c=new Color32(143,117,74,255);
                if(distance<=39f) c=new Color32(51,52,47,255);
                if(distance<=29f) c=new Color32(176,151,100,255);
                if(distance<=18f) c=new Color32(89,48,38,255);
                if(distance<=7f) c=new Color32(228,197,119,255);
                pixels[y*256+x]=c;
            }
            texture.SetPixels32(pixels); texture.Apply(false,true);
            Board=Sprite.Create(texture,new Rect(0,0,256,256),new Vector2(.5f,.125f),128f);
            Board.name="StartTutorialTarget";
            return Board;
        }
    }
}
