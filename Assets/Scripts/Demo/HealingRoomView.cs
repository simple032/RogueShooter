using System.Collections.Generic;
using UnityEngine;
using RogueShooter.Player;
using RogueShooter.Maze;
namespace RogueShooter.Demo {
public sealed class HealingRoomView : MonoBehaviour {
 public const float RestoreFraction=.4f;
 public bool Collected {get;private set;}
 Transform _heart; Mesh _mesh; Material _material; Vector3 _home;
 public static HealingRoomView Create(MazeNode n,Transform parent,List<GameObject> world) {
  var go=new GameObject("Healing room "+n.Id);go.transform.SetParent(parent,false);go.transform.position=new Vector3(n.Center.X,n.Center.Y,0);
  var view=go.AddComponent<HealingRoomView>();view.Build();world.Add(go);return view;
 }
 void Build() {
  _home=transform.position;
  var local=new List<GameObject>();
  Stage1IsoArt.Disc("Healing inlay",_home,3.5f,2.2f,new Color(.58f,.17f,.16f,.2f),-630,transform,local);
  Stage1IsoArt.Brazier(_home+new Vector3(-4,1,0),transform,local);Stage1IsoArt.Brazier(_home+new Vector3(4,1,0),transform,local);
  var vertices=new Vector3[65];var tris=new int[64*3];vertices[0]=Vector3.zero;
  for(int i=0;i<64;i++){float t=i*Mathf.PI*2/64;vertices[i+1]=new Vector3(Mathf.Pow(Mathf.Sin(t),3)*.45f,(13*Mathf.Cos(t)-5*Mathf.Cos(2*t)-2*Mathf.Cos(3*t)-Mathf.Cos(4*t))/16f*.45f,0);tris[i*3]=0;tris[i*3+1]=i+1;tris[i*3+2]=(i+1)%64+1;}
  _mesh=new Mesh();_mesh.vertices=vertices;_mesh.triangles=tris;_mesh.RecalculateBounds();
  _material=new Material(Shader.Find("Sprites/Default"));_material.color=new Color(.96f,.15f,.22f);
  _heart=Stage1IsoArt.PlaceMesh("Red heart",_home+Vector3.up*.7f,Vector3.one,_mesh,_material,15,transform).transform;
 }
 public bool TryCollect(PlayerVitals vitals) {
  if(Collected||vitals==null||vitals.IsDown||vitals.Hp>=vitals.MaxHp)return false;
  vitals.Heal(vitals.MaxHp*RestoreFraction);Collected=true;RogueShooter.Audio.JianHaiAudio.Emit("jh_heal_pickup",this);_heart.gameObject.SetActive(false);return true;
 }
 void Update(){if(Collected||RunPause.IsPaused)return;_heart.position=_home+Vector3.up*(.7f+Mathf.Sin(Time.time*2.5f)*.12f);var demo=FindObjectOfType<Stage1MazeDemo>();if(demo!=null&&demo.PlayerBody!=null&&Vector2.Distance(demo.PlayerBody.position,_home)<1f)TryCollect(demo.PlayerBody.GetComponent<PlayerVitals>());}
 void OnDestroy(){if(_mesh!=null)Destroy(_mesh);if(_material!=null)Destroy(_material);}
}
}