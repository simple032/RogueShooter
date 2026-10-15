using UnityEngine;
using RogueShooter.Maze;
namespace RogueShooter.Demo {
public static class RoomWalkShape {
 public static void Configure(MazeNode n,int seed) {
  // S2/S3 only (StageLevelMap configures each late-stage part): altar rooms
  // share the combat rooms' corner-cut walk shape, so their tiles and props
  // stop where the air walls actually cut. S1 parts are never configured.
  if(!n.SpawnsEnemies)return;
  int hash=seed;foreach(char c in n.Id)hash=unchecked(hash*31+c);
  float arm=5.0f+(uint)hash%3*.5f;
  float x=n.Center.X,y=n.Center.Y,hw=n.Width*.5f,hh=n.Height*.5f;
  n.FloorVoids=new[]{new Rect(x-hw,y-hh,hw-arm,hh-arm),new Rect(x+arm,y-hh,hw-arm,hh-arm),new Rect(x-hw,y+arm,hw-arm,hh-arm),new Rect(x+arm,y+arm,hw-arm,hh-arm)};
 }
 public static void BuildWalls(MazeNode n,Transform root) {
  if(n.FloorVoids==null)return;
  foreach(var r in n.FloorVoids) {
   var go=new GameObject("Missing floor air wall "+n.Id);go.transform.SetParent(root,false);go.transform.position=r.center;
   var box=go.AddComponent<BoxCollider2D>();box.size=r.size;
   // An inset dark lip distinguishes a cut floor from the room's masonry wall.
   // Cover ground decals that were authored before the cut corners.
   Stage1IsoArt.PlaceMesh("Floor void",new Vector3(r.xMin,r.yMin,0),new Vector3(r.width,r.height,1),Stage1IsoArt.QuadMesh(),Stage1IsoArt.BandMaterial(new Color(.035f,.039f,.049f)), -600,root);
  }
 }
}
}