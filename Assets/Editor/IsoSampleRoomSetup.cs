using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using RogueShooter.Iso;

public static class IsoSampleRoomSetup
{
    public const string ScenePath = "Assets/Scenes/IsoSampleRoom.scene";
    const string Root = "Assets/Art/JianHai/Iso";
    const string LitMatPath = Root + "/IsoSpriteLit.mat";

    public static string Build()
    {
        var pngs = Directory.GetFiles(Root, "*.png", SearchOption.AllDirectories);
        if (pngs.Length != 294)
            return "png count " + pngs.Length;
        AssetDatabase.Refresh();

        AssetDatabase.StartAssetEditing();
        try
        {
            for (int i = 0; i < pngs.Length; i++)
            {
                string asset = ToAsset(pngs[i]);
                var importer = AssetImporter.GetAtPath(asset) as TextureImporter;
                if (importer == null)
                    return "importer " + asset;
                Configure(importer, asset);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.Refresh();
        string bind = BindSecondaries();
        if (bind != null)
            return bind;

        var shader = Shader.Find("JianHai/IsoSpriteLit");
        if (shader == null)
            return "lit shader";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(LitMatPath);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, LitMatPath);
        }
        else
        {
            mat.shader = shader;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        string built = Populate(scene, mat);
        if (built != null)
            return built;
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            return "save " + ScenePath;
        AssetDatabase.SaveAssets();
        return null;
    }

    static string BindSecondaries()
    {
        var pngs = Directory.GetFiles(Root, "*.png", SearchOption.AllDirectories);
        AssetDatabase.StartAssetEditing();
        try
        {
            for (int i = 0; i < pngs.Length; i++)
            {
                string asset = ToAsset(pngs[i]);
                if (asset.EndsWith("_n.png") || asset.EndsWith("_e.png"))
                    continue;
                string normal = asset.Substring(0, asset.Length - 4) + "_n.png";
                string emission = asset.Substring(0, asset.Length - 4) + "_e.png";
                var extras = new List<SecondarySpriteTexture>();
                if (File.Exists(normal))
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(normal);
                    if (tex == null)
                        return "normal " + normal;
                    extras.Add(new SecondarySpriteTexture { name = "_NormalMap", texture = tex });
                }

                if (File.Exists(emission))
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(emission);
                    if (tex == null)
                        return "emission " + emission;
                    extras.Add(new SecondarySpriteTexture { name = "_EmissionTex", texture = tex });
                }

                if (extras.Count == 0)
                    continue;
                var importer = AssetImporter.GetAtPath(asset) as TextureImporter;
                importer.secondarySpriteTextures = extras.ToArray();
                EditorUtility.SetDirty(importer);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.Refresh();
        return null;
    }

    static string Populate(Scene scene, Material mat)
    {
        var lightGo = new GameObject("IsoLight");
        var light = lightGo.AddComponent<IsoLight2D>();
        light.Direction = new Vector3(-0.55f, 0.75f, 0.37f);

        var camGo = new GameObject("IsoCamera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5.5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 1f);
        cam.transparencySortMode = TransparencySortMode.CustomAxis;
        cam.transparencySortAxis = new Vector3(0f, 1f, 0f);
        camGo.AddComponent<IsoEmissionBloom>();
        camGo.AddComponent<IsoSortAxis>().Apply();

        var gridGo = new GameObject("IsoGrid");
        var grid = gridGo.AddComponent<Grid>();
        grid.cellLayout = GridLayout.CellLayout.Isometric;
        grid.cellSize = new Vector3(1f, 0.5f, 1f);

        var floorGo = new GameObject("Floor");
        floorGo.transform.SetParent(gridGo.transform, false);
        var floorMap = floorGo.AddComponent<Tilemap>();
        var floorRenderer = floorGo.AddComponent<TilemapRenderer>();
        floorRenderer.sortingOrder = -10;
        var floorA = MakeTile(Root + "/Tiles/S1/Floor/jh_iso_floor_s1_room_00.png", Root + "/Generated/floor_00.asset");
        var floorB = MakeTile(Root + "/Tiles/S1/Floor/jh_iso_floor_s1_room_01.png", Root + "/Generated/floor_01.asset");
        if (floorA == null || floorB == null)
            return "floor sprite";
        for (int x = 1; x <= 4; x++)
        {
            for (int y = 1; y <= 3; y++)
                floorMap.SetTile(new Vector3Int(x, y, 0), ((x + y) & 1) == 0 ? floorA : floorB);
        }

        var wallsGo = new GameObject("Walls");
        wallsGo.transform.SetParent(gridGo.transform, false);
        var wallMap = wallsGo.AddComponent<Tilemap>();
        var wallRenderer = wallsGo.AddComponent<TilemapRenderer>();
        wallRenderer.mode = TilemapRenderer.Mode.Individual;
        wallRenderer.enabled = false;
        var wallL = MakeTile(Root + "/Tiles/S1/Wall/jh_iso_wall_s1_l_00.png", Root + "/Generated/wall_l.asset");
        var wallR = MakeTile(Root + "/Tiles/S1/Wall/jh_iso_wall_s1_r_00.png", Root + "/Generated/wall_r.asset");
        var corner = MakeTile(Root + "/Tiles/S1/Wall/jh_iso_wall_s1_corner_in_00.png", Root + "/Generated/wall_corner.asset");
        if (wallL == null || wallR == null || corner == null)
            return "wall sprite";
        wallMap.SetTile(new Vector3Int(1, 2, 0), wallL);
        wallMap.SetTile(new Vector3Int(1, 3, 0), corner);
        wallMap.SetTile(new Vector3Int(2, 3, 0), wallR);
        wallMap.SetTile(new Vector3Int(3, 3, 0), wallR);
        wallMap.SetTile(new Vector3Int(4, 3, 0), wallR);

        var litRoot = new GameObject("Lit").transform;
        Place(litRoot, "WallL", LoadSprite(Root + "/Tiles/S1/Wall/jh_iso_wall_s1_l_00.png"), At(grid, 1, 2), mat, false);
        Place(litRoot, "Corner", LoadSprite(Root + "/Tiles/S1/Wall/jh_iso_wall_s1_corner_in_00.png"), At(grid, 1, 3), mat, false);
        Place(litRoot, "WallR_2", LoadSprite(Root + "/Tiles/S1/Wall/jh_iso_wall_s1_r_00.png"), At(grid, 2, 3), mat, false);
        Place(litRoot, "WallR_3", LoadSprite(Root + "/Tiles/S1/Wall/jh_iso_wall_s1_r_00.png"), At(grid, 3, 3), mat, false);
        Place(litRoot, "WallR_4", LoadSprite(Root + "/Tiles/S1/Wall/jh_iso_wall_s1_r_00.png"), At(grid, 4, 3), mat, false);
        Place(litRoot, "Door", LoadSprite(Root + "/Tiles/S1/Wall/jh_iso_wall_s1_door_l_00.png"), (At(grid, 1, 1) + At(grid, 1, 2)) * 0.5f, mat, false);
        Place(litRoot, "LowL", LoadSprite(Root + "/Tiles/S1/Wall/jh_iso_wall_s1_low_l_00.png"), At(grid, 2, 0), mat, false);
        Place(litRoot, "LowR", LoadSprite(Root + "/Tiles/S1/Wall/jh_iso_wall_s1_low_r_00.png"), At(grid, 4, 0), mat, false);
        Place(litRoot, "LowCorner", LoadSprite(Root + "/Tiles/S1/Wall/jh_iso_wall_s1_low_corner_00.png"), At(grid, 3, 0), mat, false);
        Place(litRoot, "Rubble", LoadSprite(Root + "/Tiles/S1/Decal/jh_iso_decal_s1_rubble_00.png"), At(grid, 3, 1), mat, false);
        Place(litRoot, "Brazier", LoadSprite(Root + "/Props/jh_iso_prop_brazier_00.png"), At(grid, 3, 2), mat, false);

        var fire = Place(litRoot, "Fire", LoadSprite(Root + "/FX/jh_iso_fx_brazier_fire_00.png"), At(grid, 3, 2) + new Vector3(0f, 0.55f, 0f), mat, false);
        var frames = new Sprite[6];
        for (int i = 0; i < 6; i++)
            frames[i] = LoadSprite(Root + "/FX/jh_iso_fx_brazier_fire_0" + i + ".png");
        var loop = fire.AddComponent<IsoBrazierFire>();
        loop.Frames = frames;

        Place(litRoot, "ArcherInRoom", LoadSprite(Root + "/Characters/Archer/jh_archer_idle_s_00.png"), At(grid, 2, 2), mat, false);

        var facing = new GameObject("Facing").transform;
        for (int i = 0; i < IsoFacing.Eight.Length; i++)
        {
            string dir = IsoFacing.Eight[i];
            var archer = Place(facing, "Archer_" + dir,
                LoadSprite(Root + "/Characters/Archer/jh_archer_idle_" + dir + "_00.png"),
                At(grid, 7, i), mat, IsoFacing.ArcherFlips(dir));
            if (archer.GetComponent<SpriteRenderer>().sprite == null)
                return "archer " + dir;
            if (!IsoFacing.TrySkeleton(dir, out string source, out bool flip))
                return "skel facing " + dir;
            var skel = Place(facing, "Skel_" + dir,
                LoadSprite(Root + "/Enemies/Skel/jh_skel_idle_" + source + "_00.png"),
                At(grid, 9, i), mat, flip);
            if (skel.GetComponent<SpriteRenderer>().sprite == null)
                return "skel " + dir;
        }

        Place(litRoot, "NormalProbe", LoadSprite(Root + "/Tiles/S1/Wall/jh_iso_wall_s1_l_00.png"), new Vector3(20f, 0f, 0f), mat, false);
        var center = At(grid, 2, 2);
        camGo.transform.position = new Vector3(center.x, center.y, -10f);
        EditorSceneManager.MarkSceneDirty(scene);
        return null;
    }

    static GameObject Place(Transform parent, string name, Sprite sprite, Vector3 pos, Material mat, bool flipX)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.flipX = flipX;
        var lit = go.AddComponent<IsoLitSprite>();
        lit.LitMaterial = mat;
        lit.Apply();
        return go;
    }

    static Vector3 At(Grid grid, int x, int y)
    {
        return grid.GetCellCenterWorld(new Vector3Int(x, y, 0));
    }

    static Tile MakeTile(string spritePath, string tilePath)
    {
        string dir = Path.GetDirectoryName(tilePath)?.Replace('\\', '/') ?? Root;
        if (!AssetDatabase.IsValidFolder(dir))
        {
            Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();
        }
        var sprite = LoadSprite(spritePath);
        if (sprite == null)
            return null;
        var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, tilePath);
        }

        tile.sprite = sprite;
        EditorUtility.SetDirty(tile);
        return tile;
    }

    public static Sprite LoadSprite(string path)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath(path);
        if (assets == null)
            return null;
        for (int i = 0; i < assets.Length; i++)
        {
            var sprite = assets[i] as Sprite;
            if (sprite != null)
                return sprite;
        }

        return null;
    }

    static void Configure(TextureImporter importer, string asset)
    {
        bool normal = asset.EndsWith("_n.png");
        bool emission = asset.EndsWith("_e.png");
        bool sprite = !normal && !emission;
        importer.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = sprite || emission;
        importer.sRGBTexture = !normal;
        importer.npotScale = TextureImporterNPOTScale.None;
        if (!sprite)
            return;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 128f;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = PivotFor(asset);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
    }

    public static Vector2 PivotFor(string asset)
    {
        string name = Path.GetFileNameWithoutExtension(asset);
        if (name.StartsWith("jh_archer_") || name.StartsWith("jh_skel_"))
            return new Vector2(0.5f, 0.125f);
        if (name.Contains("floor_") || name.Contains("decal_"))
            return new Vector2(0.5f, 0.5f);
        if (name.Contains("door_"))
            return new Vector2(0.5f, 0.15f);
        if (name.Contains("low_"))
            return new Vector2(0.5f, 0.333f);
        if (name.Contains("wall_"))
            return new Vector2(0.5f, 0.125f);
        if (name.Contains("brazier_fire"))
            return new Vector2(0.5f, 0f);
        if (name.Contains("brazier_"))
            return new Vector2(0.5f, 0.167f);
        return new Vector2(0.5f, 0.5f);
    }

    static string ToAsset(string full)
    {
        full = full.Replace('\\', '/');
        int at = full.IndexOf("Assets/", System.StringComparison.Ordinal);
        return at >= 0 ? full.Substring(at) : full;
    }
}
