using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RogueShooter.Demo
{
    /// <summary>Editable, batched decorative geometry; never changes walkable layout or collision.</summary>
    public static class S2S3EnvironmentPolish
    {
        static Material _vertexMaterial;
        static Material VertexMaterial
        {
            get
            {
                if (_vertexMaterial == null) _vertexMaterial = new Material(Shader.Find("Sprites/Default"))
                    { name = "S2S3 refined vertex surfaces", mainTexture = Texture2D.whiteTexture, color = Color.white };
                return _vertexMaterial;
            }
        }
        sealed class Draw
        {
            readonly List<Vector3> _v = new List<Vector3>();
            readonly List<Color> _c = new List<Color>();
            readonly List<int> _t = new List<int>();
            public void Poly(Color color, params Vector2[] points)
            {
                int first = _v.Count;
                foreach (var p in points) { _v.Add(p); _c.Add(QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color); }
                for (int i = 1; i < points.Length - 1; i++) _t.AddRange(new[] { first, first + i + 1, first + i });
            }
            public void Rect(float x, float y, float w, float h, Color c)
            { Poly(c, new Vector2(x,y), new Vector2(x+w,y), new Vector2(x+w,y+h), new Vector2(x,y+h)); }
            public void Line(Vector2 a, Vector2 b, float width, Color color)
            {
                Vector2 n = new Vector2(-(b-a).y, (b-a).x).normalized * width * .5f;
                Poly(color, a-n, b-n, b+n, a+n);
            }
            public void Disc(Vector2 at, float rx, float ry, Color c)
            {
                var points = new Vector2[12];
                for (int i=0;i<points.Length;i++) { float a=i*Mathf.PI*2/points.Length; points[i]=at+new Vector2(Mathf.Cos(a)*rx,Mathf.Sin(a)*ry); }
                Poly(c, points);
            }
            public void Save(string name, Vector2 at, int order, Transform root, List<GameObject> into, bool sortAtFeet=false)
            {
                var go=new GameObject("Refined " + name); go.transform.SetParent(root,false);go.transform.position=at;
                var mesh=new Mesh { name=name };mesh.SetVertices(_v);mesh.SetColors(_c);mesh.SetTriangles(_t,0);mesh.RecalculateBounds();
                go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var mr=go.AddComponent<MeshRenderer>();mr.sharedMaterial=VertexMaterial;mr.sortingOrder=order;
                mr.shadowCastingMode=ShadowCastingMode.Off;mr.receiveShadows=false;
                if(sortAtFeet)go.AddComponent<SortingGroup>().sortingOrder=0;
                go.AddComponent<S2S3PolishMeshOwner>().OwnedMesh=mesh;into.Add(go);
            }
        }

        static readonly Color Stone = new Color(.34f,.36f,.38f);
        static readonly Color StoneDark = new Color(.17f,.20f,.23f);
        static readonly Color StoneLight = new Color(.44f,.45f,.43f);
        static readonly Color Bone = new Color(.50f,.49f,.43f);
        static readonly Color Iron = new Color(.12f,.15f,.16f);
        static readonly Color Copper = new Color(.37f,.26f,.17f);
        static readonly Color CopperEdge = new Color(.56f,.40f,.24f);
        static readonly Color Patina = new Color(.19f,.29f,.26f);

        public static void CopperCorners(Vector2 center, float halfWidth, float halfHeight, string id, Transform root, List<GameObject> into)
        {
            var d = new Draw();
            foreach (float x in new[] { -halfWidth, halfWidth - .5f })
            {
                d.Rect(x,halfHeight,.065f,1.45f,CopperEdge);
                d.Rect(x+.43f,halfHeight,.065f,1.45f,Iron);
                foreach(float y in new[]{.18f,.66f,1.17f})d.Disc(new Vector2(x+.26f,halfHeight+y),.035f,.035f,Iron);
                d.Poly(Patina,new Vector2(x+.08f,halfHeight+.27f),new Vector2(x+.34f,halfHeight+.33f),new Vector2(x+.23f,halfHeight+.64f));
                d.Rect(x+.08f,-halfHeight-.34f,.34f,.035f,CopperEdge);
            }
            d.Save("aged copper corner fittings " + id,center,-12,root,into);
        }

        public static void Pillar(Vector2 at, string id, Transform root, List<GameObject> into)
        {
            Stage1IsoArt.Disc("Refined pillar contact " + id, at + new Vector2(.12f,-.06f),1.6f,.65f,new Color(0,0,0,.30f),-750,root,into);
            var d=new Draw();
            // Plinth, chipped shaft, narrow shadow face and bevelled capital.
            d.Poly(StoneDark,new Vector2(-.66f,0),new Vector2(.60f,0),new Vector2(.70f,.25f),new Vector2(-.54f,.25f));
            d.Poly(Stone,new Vector2(-.54f,.25f),new Vector2(.70f,.25f),new Vector2(.49f,.43f),new Vector2(-.65f,.43f));
            d.Poly(Stone,new Vector2(-.40f,.43f),new Vector2(.28f,.43f),new Vector2(.28f,2.78f),new Vector2(-.34f,2.78f),new Vector2(-.40f,2.61f));
            d.Poly(StoneDark,new Vector2(.28f,.43f),new Vector2(.49f,.56f),new Vector2(.49f,2.86f),new Vector2(.28f,2.78f));
            d.Rect(-.39f,.58f,.065f,1.86f,StoneLight);
            d.Rect(-.25f,.51f,.08f,1.9f,new Color(.27f,.30f,.32f));
            d.Rect(.08f,.56f,.065f,1.91f,new Color(.26f,.29f,.30f));
            d.Poly(StoneDark,new Vector2(-.53f,2.70f),new Vector2(.42f,2.70f),new Vector2(.54f,2.91f),new Vector2(-.48f,2.91f));
            d.Poly(StoneLight,new Vector2(-.48f,2.91f),new Vector2(.54f,2.91f),new Vector2(.35f,3.06f),new Vector2(-.60f,3.06f));
            d.Rect(-.38f,.50f,.67f,.10f,new Color(.24f,.25f,.24f));
            d.Line(new Vector2(-.31f,1.74f),new Vector2(-.08f,1.49f),.035f,StoneDark);
            d.Line(new Vector2(-.08f,1.49f),new Vector2(-.17f,1.25f),.03f,StoneDark);
            // Small bone inset reads as ornament rather than a whole bright white pillar.
            d.Disc(new Vector2(-.04f,2.32f),.15f,.17f,Bone);
            d.Disc(new Vector2(-.10f,2.34f),.036f,.043f,StoneDark);d.Disc(new Vector2(.025f,2.34f),.036f,.043f,StoneDark);
            d.Rect(-.11f,2.13f,.14f,.075f,Bone);
            for(int i=0;i<5;i++)d.Rect(-.32f+i*.12f,.73f+(i%2)*.13f,.05f,.026f,StoneLight);
            d.Save("bone-stone pillar " + id,at,0,root,into,true);
        }

        public static void RingPaving(Vector2 center, string id, Transform root, List<GameObject> into)
        {
            var d=new Draw();
            foreach(float radius in new[]{9.2f,5.4f})
            {
                float ry=radius*.63f;
                for(int i=0;i<72;i++)
                {
                    if(i%18>13 || i%11==4)continue;
                    float a=(i+.1f)*Mathf.PI*2/72,b=(i+.86f)*Mathf.PI*2/72;
                    Vector2 mid=new Vector2(Mathf.Cos((a+b)*.5f)*radius,Mathf.Sin((a+b)*.5f)*ry);
                    if(Mathf.Abs(mid.x)<2.8f||Mathf.Abs(mid.y)<2.8f)continue;
                    Vector2 p=new Vector2(Mathf.Cos(a)*radius,Mathf.Sin(a)*ry),q=new Vector2(Mathf.Cos(b)*radius,Mathf.Sin(b)*ry);
                    Vector2 p2=new Vector2(Mathf.Cos(a)*(radius+.28f),Mathf.Sin(a)*(ry+.18f)),q2=new Vector2(Mathf.Cos(b)*(radius+.28f),Mathf.Sin(b)*(ry+.18f));
                    d.Poly(new Color(.25f,.28f,.32f,i%3==0?.50f:.36f),p,q,q2,p2);
                    d.Line(p2,q2,.028f,new Color(.37f,.39f,.40f,.35f));
                    if(i%7==0)d.Poly(new Color(.24f,.27f,.29f,.5f),mid+new Vector2(.30f,-.12f),mid+new Vector2(.54f,-.12f),mid+new Vector2(.43f,.03f));
                }
            }
            d.Save("broken stone ring " + id,center,-650,root,into);
        }

        public static void CopperTrack(Vector2 center, string id, Transform root, List<GameObject> into)
        {
            float y=S2S3PhaseDress.GateTrackY;
            var ground=new Draw();ground.Rect(-8,y-.10f,16,.48f,new Color(.16f,.18f,.18f));
            ground.Rect(-8,y+.06f,16,.12f,new Color(.075f,.095f,.10f));
            ground.Rect(-8,y+.24f,16,.075f,Copper);ground.Rect(-8,y-.07f,16,.055f,CopperEdge);
            for(int i=0;i<16;i++){float x=-7.7f+i;ground.Rect(x,y+.25f,.19f,.025f,Patina);ground.Disc(new Vector2(x,y-.045f),.045f,.022f,new Color(.47f,.37f,.26f));}
            ground.Save("recessed copper guide " + id,center,-640,root,into);
            var door=new Draw();float sx=S2S3PhaseDress.GateSlabX;
            door.Rect(sx,y,1.3f,1.75f,Copper);
            door.Poly(Iron,new Vector2(sx+1.3f,y),new Vector2(sx+1.55f,y+.18f),new Vector2(sx+1.55f,y+1.93f),new Vector2(sx+1.3f,y+1.75f));
            door.Poly(CopperEdge,new Vector2(sx,y+1.75f),new Vector2(sx+1.3f,y+1.75f),new Vector2(sx+1.55f,y+1.93f),new Vector2(sx+.25f,y+1.93f));
            door.Rect(sx+.12f,y+.10f,1.05f,1.50f,new Color(.29f,.22f,.16f));
            for(int i=0;i<3;i++)door.Rect(sx+.20f+i*.4f,y+.10f,.075f,1.53f,Iron);
            foreach(float h in new[]{.25f,1.30f}) {door.Rect(sx+.07f,y+h,1.16f,.09f,CopperEdge);foreach(float x in new[]{.16f,.60f,1.08f})door.Disc(new Vector2(sx+x,y+h+.045f),.035f,.035f,Iron);}
            door.Poly(Patina,new Vector2(sx+.14f,y+.16f),new Vector2(sx+.46f,y+.19f),new Vector2(sx+.33f,y+.51f),new Vector2(sx+.20f,y+.42f));
            foreach(float x in new[]{-8f,7.75f}){door.Rect(x,y-.12f,.28f,.65f,Iron);door.Rect(x+.03f,y+.37f,.23f,.09f,CopperEdge);}
            door.Save("parked copper gate " + id,center,-5,root,into);
            Stage1IsoArt.Disc("Refined copper contact " + id,center+new Vector2(sx+.7f,y),1.9f,.55f,new Color(0,0,0,.27f),-750,root,into);
        }

        public static void AltarDais(Vector2 center, string id, Transform root, List<GameObject> into)
        {
            var d=new Draw();
            // Projected tiers remain ground decoration, with no new physical obstruction.
            for(int i=0;i<3;i++)
            {
                float w=3.3f-i*.38f,h=1.7f-i*.20f,y=i*.13f;
                float cut=.48f;
                d.Poly(StoneDark,new Vector2(-w+cut,-h+y-.18f),new Vector2(w-cut,-h+y-.18f),new Vector2(w,-h+y+cut-.18f),new Vector2(w,-h+y+cut),new Vector2(w-cut,-h+y),new Vector2(-w+cut,-h+y),new Vector2(-w,-h+y+cut),new Vector2(-w,-h+y+cut-.18f));
                d.Poly(new Color(.22f+i*.025f,.25f+i*.025f,.29f+i*.025f),
                    new Vector2(-w+cut,-h+y),new Vector2(w-cut,-h+y),new Vector2(w,-h+y+cut),new Vector2(w,h+y-cut),
                    new Vector2(w-cut,h+y),new Vector2(-w+cut,h+y),new Vector2(-w,h+y-cut),new Vector2(-w,-h+y+cut));
                d.Line(new Vector2(-w+cut,-h+y),new Vector2(w-cut,-h+y),.04f,StoneLight);
                d.Line(new Vector2(-w,-h+y+cut),new Vector2(-w+cut,-h+y),.035f,StoneLight);
                d.Line(new Vector2(w-cut,-h+y),new Vector2(w,-h+y+cut),.035f,new Color(.30f,.32f,.34f));
            }
            foreach(float x in new[]{-2.35f,2.35f})
            {
                d.Line(new Vector2(x,-1.03f),new Vector2(x,1.34f),.055f,new Color(.34f,.31f,.24f));
                for(int i=0;i<4;i++)d.Rect(x-.10f,-.8f+i*.57f,.20f,.12f,new Color(.36f,.34f,.28f));
            }
            d.Line(new Vector2(-3f,-1.25f),new Vector2(-2.6f,-.8f),.03f,StoneDark);
            d.Line(new Vector2(2.48f,.63f),new Vector2(1.94f,.93f),.035f,StoneDark);
            d.Line(new Vector2(-1.9f,-.65f),new Vector2(0,1.2f),.04f,new Color(.33f,.33f,.30f));
            d.Line(new Vector2(0,1.2f),new Vector2(1.9f,-.65f),.04f,new Color(.33f,.33f,.30f));
            d.Save("altar stepped dais " + id,center,-620,root,into);
        }

        public static void ChestInlay(Vector2 center, string id, bool phase3, Transform root, List<GameObject> into)
        {
            var d=new Draw();Color line=phase3?new Color(.29f,.32f,.38f,.55f):new Color(.37f,.28f,.18f,.55f);
            foreach(float x in new[]{-2.1f,2.1f})d.Line(new Vector2(x,-1.25f),new Vector2(x,1.25f),.10f,line);
            foreach(float y in new[]{-1.25f,1.25f})d.Line(new Vector2(-2.1f,y),new Vector2(2.1f,y),.10f,line);
            for(int i=0;i<5;i++)d.Rect(-1.75f+i*.8f,-1.40f,.24f,.075f,line);
            d.Save("treasury stone inlay " + id,center,-650,root,into);
        }

        public static void RackDetails(Vector2 at, string id, Transform root, List<GameObject> into)
        {
            var d=new Draw();
            // Spear, axe and chipped sword silhouettes replace three plain sticks.
            d.Line(new Vector2(-.65f,.1f),new Vector2(-.41f,1.23f),.06f,new Color(.26f,.21f,.15f));
            d.Poly(new Color(.40f,.43f,.43f),new Vector2(-.52f,1.16f),new Vector2(-.36f,1.61f),new Vector2(-.28f,1.13f));
            d.Line(new Vector2(0,.1f),new Vector2(.09f,1.3f),.075f,new Color(.29f,.23f,.16f));
            d.Poly(new Color(.29f,.33f,.34f),new Vector2(.06f,1.12f),new Vector2(.50f,1.32f),new Vector2(.46f,.92f),new Vector2(.08f,.94f));
            d.Line(new Vector2(.70f,.18f),new Vector2(.48f,1.45f),.10f,new Color(.32f,.36f,.37f));
            d.Line(new Vector2(.47f,.42f),new Vector2(.81f,.36f),.08f,Copper);
            for(int i=0;i<2;i++){d.Rect(-1.17f+i*2.3f,.76f,.18f,.23f,Iron);d.Disc(new Vector2(-1.09f+i*2.3f,.88f),.027f,.027f,CopperEdge);}
            d.Save("weapon rack fittings " + id,at,-13,root,into);
        }
    }

    public sealed class S2S3PolishMeshOwner : MonoBehaviour
    {
        public Mesh OwnedMesh;
        void OnDestroy(){if(OwnedMesh!=null){if(Application.isPlaying)Destroy(OwnedMesh);else DestroyImmediate(OwnedMesh);}}
    }
}
