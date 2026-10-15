using UnityEngine;
using RogueShooter.Player;
namespace RogueShooter.Demo {
[DefaultExecutionOrder(100)] public sealed class HeroTorchRitual : MonoBehaviour {
 public bool Busy=>_altar!=null&&_altar.IsIgniting;
 public float Progress=>_altar==null?0:_altar.Ignition01;
 public Vector3 Aim=>_altar==null?Vector3.up:_altar.transform.position-transform.position;
 public Vector3 ContactTarget=>_altar==null?transform.position:_altar.transform.position+Vector3.up*((int)_altar.Stage*.33f+.48f);
 StageAltarView _altar;GameObject _torch;SpriteRenderer _fire;Material _material;Mesh _mesh;
 public void Begin(StageAltarView altar) {
  _altar=altar;RunPause.RitualActing=true;
  _torch=new GameObject("Hand held ignition torch");_torch.transform.SetParent(transform,false);
  if(RogueShooter.Iso.HeroMeshyArt.Available){var flame=new GameObject("Meshy hand torch flame");flame.transform.SetParent(_torch.transform,false);flame.transform.localScale=new Vector3(.35f,.45f,1);_fire=flame.AddComponent<SpriteRenderer>();_fire.sortingLayerName=RogueShooter.Art.JianHaiArtCatalog.LayerFx;_fire.sortingOrder=22;return;}
  _mesh=new Mesh();_mesh.vertices=new[]{new Vector3(-.055f,0),new Vector3(.055f,0),new Vector3(.055f,.65f),new Vector3(-.055f,.65f)};_mesh.triangles=new[]{0,1,2,0,2,3};_mesh.RecalculateBounds();
  _material=new Material(Shader.Find("Sprites/Default"));_material.color=new Color(.47f,.27f,.10f);
  _torch.AddComponent<MeshFilter>().sharedMesh=_mesh;var r=_torch.AddComponent<MeshRenderer>();r.sharedMaterial=_material;r.sortingOrder=20;
  var fire=new GameObject("Hand torch flame");fire.transform.SetParent(_torch.transform,false);fire.transform.localPosition=new Vector3(0,.58f,0);fire.transform.localScale=new Vector3(.45f,.55f,1);_fire=fire.AddComponent<SpriteRenderer>();_fire.sortingOrder=22;
 }
 void LateUpdate(){if(_altar==null)return;if(!Busy){Finish();return;}float p=Progress;var hero=GetComponent<RogueShooter.Iso.HeroMeshyPresentation>();if(hero!=null&&RogueShooter.Iso.HeroMeshyArt.Available){_torch.transform.position=transform.position+hero.TorchOffset;_torch.SetActive(hero.CurrentClip=="ritual"&&p>.2f&&p<.8f);_fire.sprite=Stage1IsoArt.LoadSprite("Assets/Art/JianHai/Iso/FX/jh_iso_fx_brazier_fire_0"+(Mathf.FloorToInt(Time.time*12)%6)+".png");return;}Vector3 hand=transform.position+new Vector3(.3f,.75f,0);Vector3 basin=_altar.transform.position+Vector3.up*((int)_altar.Stage*.33f+.48f);Vector3 tip=Vector3.Lerp(hand+Vector3.up*.65f,basin,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.2f,.6f,p)));_torch.transform.position=tip-Vector3.up*.6f;_torch.SetActive(p>.06f);_fire.sprite=Stage1IsoArt.LoadSprite("Assets/Art/JianHai/Iso/FX/jh_iso_fx_brazier_fire_0"+(Mathf.FloorToInt(Time.time*12)%6)+".png");}
 public void Finish(){_altar=null;RunPause.RitualActing=false;if(_torch!=null)Destroy(_torch);if(_mesh!=null)Destroy(_mesh);if(_material!=null)Destroy(_material);}
 void OnDisable(){if(_altar!=null)Finish();}
}
}
