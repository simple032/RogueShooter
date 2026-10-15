using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using UnityEditor.SceneManagement;
using RogueShooter.Maze;
using RogueShooter.Demo;
using RogueShooter.Iso;

namespace JianHai.ArtSamples
{
    public static class S1MapRoomBuilder
    {
        public const string Folder = "Assets/ArtSamples/S1MapRoom";
        public const string ScenePath = Folder + "/S1MapArtSample.scene";
        const string Art = "Assets/Art/JianHai/Iso/";
        static Transform root;
        static Material wash;
        static Mesh softDisc;
        static readonly List<string> sources = new List<string>();

        [MenuItem("JianHai/Art Samples/Build S1 Map Room")]
        public static void MenuBuild() { Debug.Log(Build()); }

        public static string Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play before rebuilding the art sample.");
            var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (previous.isDirty) throw new InvalidOperationException("The current scene has unsaved changes. Save it before switching scenes.");
            Directory.CreateDirectory(Folder + "/Generated");
            sources.Clear();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            root = new GameObject("S1 Map Art Sample — 28 x 22").transform;
            wash = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Generated/GroundWash.mat");
            if (wash == null) { wash = new Material(Shader.Find("Sprites/Default")); AssetDatabase.CreateAsset(wash, Folder + "/Generated/GroundWash.mat"); }
            softDisc = Disc();

            BuildFloor();
            var architecture = Group("02 Architecture — consistent masonry");
            BuildArchitecture(architecture);

            var props = Group("03 Focal objects — altar and treasure");
            Prop("Altar", "Props/jh_iso_prop_altar_00.png", new Vector3(0,7.5f), props);
            Prop("Chest", "Props/jh_iso_prop_chest_closed_00.png", new Vector3(-9,5), props);
            Shadow(new Vector3(0,7.35f), 1.5f,.5f, .30f, props);
            Shadow(new Vector3(-9,4.9f), .9f,.3f, .25f, props);
            var atmosphere = Group("04 Fire and local warmth");
            foreach (var p in new[]{new Vector3(-3,7.1f),new Vector3(3,7.1f),new Vector3(-2.5f,-9.3f),new Vector3(2.5f,-9.3f),new Vector3(-10.7f,6.2f)}) Brazier(p,atmosphere);

            var dressing = Group("05 Edge dressing — grouped rubble");
            Vector2[] clusters = {new Vector2(-11.5f,8),new Vector2(10.5f,8),new Vector2(-11.5f,-7),new Vector2(11,-8),new Vector2(-11,3)};
            for(int c=0;c<clusters.Length;c++) for(int j=0;j<3;j++)
            {
                Vector3 pos = (Vector3)(clusters[c] + new Vector2(j*.48f,(j%2)*.32f));
                var go = Prop("Rubble cluster " + c + "/" + j, "Tiles/S1/Decal/jh_iso_decal_s1_rubble_0" + (j%2) + ".png",pos,dressing);
                UnityEngine.Object.DestroyImmediate(go.GetComponent<S1SampleDepth>());
                go.GetComponent<SpriteRenderer>().sortingOrder = -700;
                go.GetComponent<SpriteRenderer>().color = new Color(.63f,.68f,.72f,.85f);
            }

            var cast = Group("06 Scale and silhouette checks");
            var player = Prop("Archer — inspect with WASD","Characters/Archer/jh_archer_idle_s_00.png",new Vector3(0,4.2f),cast);
            Shadow(player.transform.position,.46f,.17f,.33f,player.transform,true);
            Prop("Skeleton — scale reference","Enemies/Skel/jh_skel_idle_s_00.png",new Vector3(-3,1.5f),cast);
            Prop("Mage — scale reference","Enemies/Mage/jh_mage_idle_s_00.png",new Vector3(4,.5f),cast);
            var camGo = new GameObject("Sample Camera"); camGo.tag="MainCamera";
            var cam = camGo.AddComponent<Camera>(); cam.orthographic=true;cam.orthographicSize=6;
            cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.035f,.044f,.061f);
            cam.transparencySortMode=TransparencySortMode.CustomAxis;cam.transparencySortAxis=Vector3.up;
            camGo.AddComponent<AudioListener>();
            var walk = root.gameObject.AddComponent<S1MapRoomWalkthrough>();walk.Player=player.transform;walk.View=cam;
            var idle=new List<Sprite>();var steps=new List<Sprite>();
            foreach(var direction in new[]{"e","ne","n","nw","w","sw","s","se"})
            {
                for(int i=0;i<3;i++)idle.Add(Load("Characters/Archer/jh_archer_idle_"+direction+"_"+i.ToString("00")+".png"));
                for(int i=0;i<6;i++)steps.Add(Load("Characters/Archer/jh_archer_walk_"+direction+"_"+i.ToString("00")+".png"));
            }
            walk.Idle=idle.ToArray();walk.Walk=steps.ToArray();walk.Inspect(2);
            foreach(var depth in UnityEngine.Object.FindObjectsOfType<S1SampleDepth>()) depth.SendMessage("LateUpdate");
            AssetDatabase.SaveAssets();
            if(!EditorSceneManager.SaveScene(scene,ScenePath))throw new Exception("Could not save sample scene");
            sources.Sort(); File.WriteAllLines(Folder+"/SourceAssets.txt",sources);
            SceneView.RepaintAll();
            return "Saved " + ScenePath + "; room=28x22, playerScale=1, playOrtho=6; editable static floor and architecture; main scene untouched.";
        }

        static Transform Group(string name) { var go=new GameObject(name);go.transform.SetParent(root,false);return go.transform; }
        static Sprite Load(string path)
        {
            string full=Art+path;var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(full);
            if(sprite==null)throw new Exception("Missing sample source: "+full);
            if(!sources.Contains(full))sources.Add(full);return sprite;
        }
        static Tile MakeTile(string id,string file)
        {
            string path=Folder+"/Generated/"+id+".asset";var t=AssetDatabase.LoadAssetAtPath<Tile>(path);
            if(t==null){t=ScriptableObject.CreateInstance<Tile>();AssetDatabase.CreateAsset(t,path);}
            t.sprite=Load(file);t.flags=TileFlags.None;t.colliderType=Tile.ColliderType.None;EditorUtility.SetDirty(t);return t;
        }
        static int Hash(int a,int b){unchecked{return (a*73856093 ^ b*19349663 ^ 42*83492791)&int.MaxValue;}}
        static void BuildFloor()
        {
            var gridGo=new GameObject("01 Floor — quiet stone and local inlays");gridGo.transform.SetParent(root,false);
            var grid=gridGo.AddComponent<Grid>();grid.cellLayout=GridLayout.CellLayout.Isometric;grid.cellSize=new Vector3(1,.5f,1);
            var go=new GameObject("Editable ground tiles");go.transform.SetParent(gridGo.transform,false);
            var map=go.AddComponent<Tilemap>();go.AddComponent<TilemapRenderer>().sortingOrder=-1000;
            var clean=MakeTile("QuietStone","Tiles/S1/Floor/jh_iso_floor_s1_room_00.png");
            var worn=MakeTile("WornStone","Tiles/S1/Floor/jh_iso_floor_s1_room_01.png");
            var crack=MakeTile("BrokenEdge","Tiles/S1/Floor/jh_iso_floor_s1_room_02.png");
            for(int u=-80;u<=80;u++)for(int v=-80;v<=80;v++)
            {
                var cell=new Vector3Int(u,v,0);var p=grid.GetCellCenterWorld(cell);
                bool room=Mathf.Abs(p.x)<=14 && p.y>=-11 && p.y<=11;
                bool entry=Mathf.Abs(p.x)<1.55f && p.y>=-15 && p.y< -11;
                if(!room&&!entry)continue;
                int h=Hash(u,v)%100;
                Tile tile=h<92?clean:worn;
                if((Mathf.Abs(p.x)>12.8f||p.y>10)&&h>93)tile=crack;
                float value=.80f+.10f*Mathf.PerlinNoise(p.x*.17f+17,p.y*.17f+29);
                Color color=new Color(value*.96f,value*.99f,value);
                bool route=Mathf.Abs(p.x)<1.5f&&p.y<5;
                if(route||entry){tile=clean;color=new Color(.90f,.88f,.83f);}
                // A restrained broken rectangular court border, not a repeated field of motifs.
                bool court=(Mathf.Abs(Mathf.Abs(p.x)-6)<.30f&&Mathf.Abs(p.y)<4.5f)||(Mathf.Abs(Mathf.Abs(p.y)-4.5f)<.23f&&Mathf.Abs(p.x)<6);
                if(court){tile=worn;color=new Color(.90f,.87f,.79f);}
                if(Mathf.Abs(p.x)<2.4f && Mathf.Abs(p.y-7.3f)<1.7f){tile=clean;color=new Color(.69f,.70f,.71f);}
                if(Mathf.Abs(p.x+9)<1.7f&&Mathf.Abs(p.y-5)<1.3f){tile=clean;color=new Color(.83f,.79f,.67f);}
                map.SetTile(cell,tile);map.SetColor(cell,color);
            }
        }
        static GameObject Prop(string name,string path,Vector3 at,Transform parent)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.position=at;
            go.AddComponent<SpriteRenderer>().sprite=Load(path);go.AddComponent<S1SampleDepth>();return go;
        }
        // Reproject the existing painted masonry face onto a continuous wall ribbon.
        // This sample-only geometry defines the rectangular 28x22 walk boundary;
        // it does not alter source textures or the production room generator.
        static void BuildArchitecture(Transform parent)
        {
            Sprite stone=Load("Tiles/S1/Wall/jh_iso_wall_s1_l_01.png");
            var matPath=Folder+"/Generated/ContinuousMasonry.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if(material==null){material=new Material(Shader.Find("Sprites/Default"));AssetDatabase.CreateAsset(material,matPath);}
            material.mainTexture=stone.texture;EditorUtility.SetDirty(material);
            Mesh face=WallFace(stone.texture);
            for(int i=0;i<20;i++)
            {
                var go=new GameObject("North masonry bay "+i);go.transform.SetParent(parent,false);
                go.transform.position=new Vector3(-14+i*1.4f,11,0);go.transform.localScale=new Vector3(1.4f,1.45f,1);
                go.AddComponent<MeshFilter>().sharedMesh=face;var mr=go.AddComponent<MeshRenderer>();mr.sharedMaterial=material;mr.sortingOrder=164;
            }
            Band("North cornice",new Vector3(-14.15f,12.45f),new Vector2(28.3f,.19f),new Color(.27f,.31f,.35f),166,parent);
            Band("North cornice shadow",new Vector3(-14,12.35f),new Vector2(28,.09f),new Color(.10f,.13f,.16f),167,parent);
            Band("North wall foot",new Vector3(-14,10.83f),new Vector2(28,.18f),new Color(.11f,.14f,.17f),168,parent);
            // Side walls use a narrow cutaway, keeping the entire player silhouette readable.
            foreach(float x in new[]{-14.55f,14f})
            {
                Band("Side stone cutaway",new Vector3(x,-11),new Vector2(.55f,23.45f),new Color(.20f,.24f,.28f),150,parent);
                Band("Side top highlight",new Vector3(x,-11),new Vector2(.10f,23.45f),new Color(.30f,.34f,.37f),151,parent);
                for(int j=0;j<22;j++)Band("Side masonry joint",new Vector3(x,-11+j),new Vector2(.55f,.025f),new Color(.10f,.13f,.16f),152,parent);
            }
            foreach(float x in new[]{-14f,1.7f})
            {
                Band("Entry-side low plinth",new Vector3(x,-11.45f),new Vector2(12.3f,.45f),new Color(.16f,.20f,.24f),850,parent);
                Band("Entry-side top edge",new Vector3(x,-11.08f),new Vector2(12.3f,.10f),new Color(.29f,.33f,.36f),851,parent);
            }
            foreach(float x in new[]{-12f,-6f,0f,6f,12f})
            {
                Band("Shallow wall pier",new Vector3(x-.15f,11),new Vector2(.3f,1.45f),new Color(.24f,.28f,.32f),170,parent);
                Band("Pier cap",new Vector3(x-.21f,12.33f),new Vector2(.42f,.13f),new Color(.31f,.34f,.37f),171,parent);
            }
            // Door trim stays at the entrance; the opening itself remains 3.4 units wide.
            Band("Entry jamb L",new Vector3(-1.95f,-11.1f),new Vector2(.25f,1.2f),new Color(.34f,.33f,.29f),810,parent);
            Band("Entry jamb R",new Vector3(1.7f,-11.1f),new Vector2(.25f,1.2f),new Color(.34f,.33f,.29f),810,parent);
            Band("Entry threshold",new Vector3(-1.7f,-11.08f),new Vector2(3.4f,.16f),new Color(.26f,.26f,.24f),-900,parent);
        }
        static Mesh WallFace(Texture2D texture)
        {
            string path=Folder+"/Generated/MasonryFace.asset";var m=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(m==null){m=new Mesh();AssetDatabase.CreateAsset(m,path);}m.Clear();
            m.vertices=new[]{Vector3.zero,Vector3.right,new Vector3(1,1),Vector3.up};
            // Opaque painted face inside the source tile, excluding the transparent silhouette.
            m.uv=new[]{new Vector2(4f/128,1-180f/256),new Vector2(60f/128,1-208f/256),new Vector2(60f/128,1-56f/256),new Vector2(4f/128,1-28f/256)};
            m.colors=new[]{new Color(.75f,.80f,.86f),new Color(.75f,.80f,.86f),new Color(.75f,.80f,.86f),new Color(.75f,.80f,.86f)};
            m.triangles=new[]{0,1,2,0,2,3};m.RecalculateBounds();EditorUtility.SetDirty(m);return m;
        }
        static void Band(string name,Vector3 at,Vector2 size,Color color,int order,Transform parent)
        {
            string asset=Folder+"/Generated/UnitQuad.asset";var m=AssetDatabase.LoadAssetAtPath<Mesh>(asset);
            if(m==null){m=new Mesh();m.vertices=new[]{Vector3.zero,Vector3.right,new Vector3(1,1),Vector3.up};m.triangles=new[]{0,1,2,0,2,3};m.RecalculateBounds();AssetDatabase.CreateAsset(m,asset);}
            string matPath=Folder+"/Generated/Stone_"+ColorUtility.ToHtmlStringRGBA(color)+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if(mat==null){mat=new Material(Shader.Find("Sprites/Default"));mat.color=color;AssetDatabase.CreateAsset(mat,matPath);}
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.position=at;go.transform.localScale=new Vector3(size.x,size.y,1);
            go.AddComponent<MeshFilter>().sharedMesh=m;var mr=go.AddComponent<MeshRenderer>();mr.sharedMaterial=mat;mr.sortingOrder=order;
        }
        static void Brazier(Vector3 at,Transform parent)
        {
            Prop("Brazier","Props/jh_iso_prop_brazier_00.png",at,parent);
            Shadow(at,.45f,.18f,.30f,parent);
            var fire=Prop("Living flame","FX/jh_iso_fx_brazier_fire_00.png",at+Vector3.up*.55f,parent);
            fire.GetComponent<S1SampleDepth>().FootOffset=-.55f;
            var animation=fire.AddComponent<IsoBrazierFire>();animation.Frames=new Sprite[6];animation.FramesPerSecond=8;
            for(int i=0;i<6;i++)animation.Frames[i]=Load("FX/jh_iso_fx_brazier_fire_0"+i+".png");
            DiscObject("Soft warm ground wash",at,2.1f,1,new Color(.9f,.47f,.16f,.095f),-800,parent);
        }
        static void Shadow(Vector3 at,float x,float y,float alpha,Transform parent,bool follow=false)
        {
            var go=DiscObject("Contact shadow",at,x,y,new Color(0,0,0,alpha),-750,parent);
            if(follow)go.transform.localPosition=Vector3.zero;
        }
        static GameObject DiscObject(string name,Vector3 at,float x,float y,Color tint,int order,Transform parent)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.position=at;go.transform.localScale=new Vector3(x,y,1);
            go.AddComponent<MeshFilter>().sharedMesh=softDisc;var mr=go.AddComponent<MeshRenderer>();mr.sharedMaterial=wash;mr.sortingOrder=order;
            var block=new MaterialPropertyBlock();block.SetColor("_Color",tint);mr.SetPropertyBlock(block);
            // Property blocks are not serialized. Persist the tint in a sample-only material.
            string matPath=Folder+"/Generated/"+(name.Contains("warm")?"WarmWash":"Shadow")+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);if(mat==null){mat=new Material(wash);AssetDatabase.CreateAsset(mat,matPath);}mat.color=tint;EditorUtility.SetDirty(mat);mr.sharedMaterial=mat;mr.SetPropertyBlock(null);return go;
        }
        static Mesh Disc()
        {
            string path=Folder+"/Generated/SoftDisc.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh!=null)return mesh;
            mesh=new Mesh();var vertices=new List<Vector3>{Vector3.zero};var colors=new List<Color>{Color.white};var triangles=new List<int>();
            for(int i=0;i<=32;i++){float a=i*Mathf.PI*2/32;vertices.Add(new Vector3(Mathf.Cos(a),Mathf.Sin(a)));colors.Add(new Color(1,1,1,0));if(i>0){triangles.Add(0);triangles.Add(i);triangles.Add(i+1);}}
            mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
    }
}

