using System.Collections.Generic;
using UnityEngine;
using RogueShooter.Maze;
namespace RogueShooter.Demo {
public static class RoomSolidProps {
 public static void Bind(MazeNode node,Transform root) {
  foreach(var t in root.GetComponentsInChildren<Transform>()) {
   string id=t.name.ToLowerInvariant();
   bool pillar=id.StartsWith("bone-stone pillar")||id.StartsWith("dressing pillar")||id.StartsWith("dressing pedestal")||id.StartsWith("dressing stub")||id.StartsWith("dressing plinth");
   bool brazier=id=="brazier";
   bool rack=id.StartsWith("weapon rack")||id.StartsWith("abandoned weapon rack");
   if(!pillar&&!brazier&&!rack)continue;
   Vector2 p=t.position;if(!node.Contains(p.x,p.y,1f)||!node.HasFloor(p.x,p.y))continue;
   if(t.GetComponent<BoxCollider2D>()!=null)continue;
   Vector2 size=id.StartsWith("dressing")?new Vector2(.7f,.45f):rack?new Vector2(2.4f,.65f):pillar?new Vector2(1.3f,.8f):new Vector2(.75f,.65f);
   if(Mathf.Abs(p.x-node.Center.X)<2.4f&&Mathf.Abs(p.y-node.Center.Y)<2.4f){t.gameObject.SetActive(false);continue;}
   var b=t.gameObject.AddComponent<BoxCollider2D>();b.size=size;
   node.SolidProps.Add(new Rect(p-size*.5f,size));
  }
 }
}
}