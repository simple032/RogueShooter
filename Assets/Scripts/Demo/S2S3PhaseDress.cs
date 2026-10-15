using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using RogueShooter.Maze;

namespace RogueShooter.Demo
{
    /// <summary>
    /// Phase dressing for the S2/S3 sample houses. Stage 2 keeps the accepted
    /// stone vocabulary and adds rusted-copper corner guards, abandoned weapon
    /// racks, low railings, decorative wall shields and a one-sided collapse,
    /// plus the copper gate strip in mechanism rooms. Stage 3 shifts every
    /// surface cold, lays broken ring paving, raises bone-white pillars and
    /// keeps warm fire inside the great altar only, plus the sector disc in
    /// stronghold rooms. Racks, shields, railings and pillars are pure visuals:
    /// foot volumes in production are registered by RoomSolidProps; samples remain visual.
    /// </summary>
    public static class S2S3PhaseDress
    {
        const string DecalRoot = "Assets/Art/JianHai/Iso/Tiles/S1/Decal/jh_iso_decal_s1_";
        const string PropRoot = "Assets/Art/JianHai/Iso/Props/jh_iso_prop_";

        // Fixture geometry. The copper strip and the sector disc both sit north
        // of the spawn court and clear of every door mouth by construction;
        // S2S3SampleChecks asserts the numbers.
        public const float GateTrackY = 3.6f;     // local y of the copper strip
        public const float GateTrackHalf = 8f;    // track half length
        public const float GateSlabX = 6.2f;      // parked slab local x
        public const float SectorOffsetX = 5.5f;  // sector disc centre offsets
        public const float SectorOffsetY = -3.0f;
        public const float SectorRadius = 4f;     // first-round disc radius

        static readonly Color CopperColor = new Color(.43f, .31f, .20f);
        static readonly Color CopperDarkColor = new Color(.44f, .26f, .15f);
        static readonly Color CopperRustColor = new Color(.20f, .29f, .25f);
        static readonly Color RackWoodColor = new Color(.30f, .21f, .13f);
        static readonly Color RackPostColor = new Color(.24f, .17f, .11f);
        static readonly Color IronColor = new Color(.17f, .18f, .20f);
        static readonly Color RailingColor = new Color(.19f, .21f, .24f);
        static readonly Color ShieldRimColor = new Color(.24f, .19f, .13f);
        static readonly Color ShieldFaceColor = new Color(.42f, .30f, .18f);
        static readonly Color ShieldBossColor = new Color(.62f, .50f, .30f);
        static readonly Color SlabColor = new Color(.30f, .33f, .36f);
        static readonly Color RubbleTint = new Color(.63f, .68f, .72f, .85f);
        static readonly Color SectorTint = new Color(.35f, .42f, .62f, .30f);
        static readonly Color SectorCoreTint = new Color(.30f, .36f, .55f, .35f);
        static readonly Color SectorRingColor = new Color(.72f, .34f, .18f, .95f);
        static readonly Color SectorRuneColor = new Color(.78f, .42f, .24f);
        static readonly Color ShadowColor = new Color(0f, 0f, 0f, .30f);

        const int GroundMarkOrder = -640;   // above rubble, below props
        const int WallDecorOrder = -15;     // in front of the masonry face
        const int FixtureOrder = -5;
        const int LabelOrder = 40;

        static Mesh _solidDisc;

        // ---- per-room dressing ----

        public static void DressRoom(S2S3Layout layout, S2S3Room room, Stage1Maze maze, Transform root, List<GameObject> into)
        {
            MazeNode node = maze.Find(room.Id);
            if (node == null)
                return;
            if (room.Type == S2S3RoomType.Chest)
            {
                Transform chest = root.Find("Chest_" + room.Id);
                S2S3EnvironmentPolish.ChestInlay(chest != null ? (Vector2)chest.position : room.Center, room.Id, layout.Phase3, root, into);
            }
            if (layout.Phase3)
            {
                BonePillars(layout, room, node, maze, root, into);
                if (room.Type == S2S3RoomType.Stronghold && !layout.Production)
                    SectorDisc(room, root, into, !layout.Production);
                if (room.Type == S2S3RoomType.Altar)
                    AltarWarmth(room, root, into);
            }
            else
            {
                CopperQuoins(room, node, maze, root, into);
                S2S3EnvironmentPolish.CopperCorners(room.Center, node.Width * .5f, node.Height * .5f, room.Id, root, into);
                if (room.Type != S2S3RoomType.Altar)
                {
                    if (!layout.Production || room.DecorVariant == 0 || room.DecorVariant == 2)
                        WeaponRack(layout, room, node, maze, root, into);
                    if (!layout.Production || room.DecorVariant == 0 || room.DecorVariant == 3)
                        WallShield(layout, room, node, maze, root, into);
                    if (!layout.Production || room.DecorVariant == 1 || room.DecorVariant == 2)
                        SideCollapse(layout, room, node, maze, root, into);
                }
                if (!layout.Production || room.DecorVariant != 2) LowRailing(room, node, maze, root, into);
                if (room.Type == S2S3RoomType.Mechanism)
                    CopperGate(room, root, into, !layout.Production);
            }
        }

        /// <summary>Stage-3 whole-map pass: cold floors, cold wall/rubble
        /// materials and the broken ring paving. Runs after every wall of the
        /// layout exists so the material clones cover corridors too.</summary>
        public static void FinishPhase(S2S3Layout layout, Stage1Maze maze, Transform root, List<GameObject> into, Tilemap floors)
        {
            if (!layout.Phase3)
                return;
            ColdMaterials(root);
            if (floors != null)
            {
                ColdFloorCells(floors);
                for (int i = 0; i < layout.Rooms.Length; i++)
                {
                    S2S3Room room = layout.Rooms[i];
                    if (room.Type == S2S3RoomType.Altar)
                        continue;
                    S2S3EnvironmentPolish.RingPaving(room.Center, room.Id, root, into);
                }
            }
        }

        // ---- stage 2 vocabulary ----

        static void CopperQuoins(S2S3Room room, MazeNode node, Stage1Maze maze, Transform root, List<GameObject> into)
        {
            Vector2 c = room.Center;
            float x0 = c.x - node.Width * 0.5f, x1 = c.x + node.Width * 0.5f;
            float y1 = c.y + node.Height * 0.5f, y0 = c.y - node.Height * 0.5f;
            // Vertical copper quoins on both ends of the north masonry face.
            for (int i = 0; i < 2; i++)
            {
                float qx = i == 0 ? x0 : x1 - 0.5f;
                Stage1IsoArt.Band("Copper quoin " + room.Id, new Vector3(qx, y1, 0f), new Vector2(0.5f, 1.45f), CopperColor, WallDecorOrder, root, into);
                Stage1IsoArt.Band("Copper quoin cap " + room.Id, new Vector3(qx - 0.06f, y1 + 1.45f, 0f), new Vector2(0.62f, 0.14f), CopperDarkColor, WallDecorOrder + 1, root, into);
                Stage1IsoArt.Band("Copper quoin rust " + room.Id, new Vector3(qx + 0.08f, y1 + 0.3f, 0f), new Vector2(0.2f, 0.55f), CopperRustColor, WallDecorOrder + 2, root, into);
                Stage1IsoArt.Band("Copper plinth cap " + room.Id, new Vector3(i == 0 ? x0 : x1 - 0.5f, y0 - 0.45f, 0f), new Vector2(0.5f, 0.45f), CopperDarkColor, -9, root, into);
            }
        }

        static void WeaponRack(S2S3Layout layout, S2S3Room room, MazeNode node, Stage1Maze maze, Transform root, List<GameObject> into)
        {
            Vector2 c = room.Center;
            float y1 = c.y + node.Height * 0.5f;
            var doors = Stage1IsoArt.CollectDoors(node, maze);
            int roll = Stage1IsoArt.Hash(maze.Seed, layout.Production ? StageMapGen.IdHash(room.Id) : room.Id.GetHashCode(), 711) % 11 - 5;
            float rx = c.x + roll * 1.35f; // -6.75 .. +6.75
            if (HasRim(doors, 'N') && Mathf.Abs(rx - c.x) < 5.5f)
                rx = c.x + (rx >= c.x ? 6.2f : -6.2f);
            Stage1IsoArt.Band("Rack post L " + room.Id, new Vector3(rx - 1.15f, y1, 0f), new Vector2(0.14f, 1.05f), RackPostColor, WallDecorOrder, root, into);
            Stage1IsoArt.Band("Rack post R " + room.Id, new Vector3(rx + 1.15f, y1, 0f), new Vector2(0.14f, 1.05f), RackPostColor, WallDecorOrder, root, into);
            Stage1IsoArt.Band("Rack crossbar " + room.Id, new Vector3(rx - 1.3f, y1 + 0.82f, 0f), new Vector2(2.6f, 0.15f), RackWoodColor, WallDecorOrder + 1, root, into);
            S2S3EnvironmentPolish.RackDetails(new Vector2(rx, y1), room.Id, root, into);
            GameObject scrap = Stage1IsoArt.Place("Rack scrap " + room.Id, new Vector3(rx, y1 - 0.45f, 0f),
                Stage1IsoArt.LoadSprite(DecalRoot + "scrap_00.png"), -700, false, root);
            into.Add(scrap);
        }

        static void LowRailing(S2S3Room room, MazeNode node, Stage1Maze maze, Transform root, List<GameObject> into)
        {
            Vector2 c = room.Center;
            float x0 = c.x - node.Width * 0.5f, x1 = c.x + node.Width * 0.5f;
            float y0 = c.y - node.Height * 0.5f;
            var doors = Stage1IsoArt.CollectDoors(node, maze);
            float railY = y0 - 0.08f;
            var runs = new List<Vector2>();
            if (HasRim(doors, 'S'))
            {
                runs.Add(new Vector2(x0 + 1f, c.x - 5.5f));
                runs.Add(new Vector2(c.x + 5.5f, x1 - 1f));
            }
            else
                runs.Add(new Vector2(x0 + 1f, x1 - 1f));
            for (int i = 0; i < runs.Count; i++)
            {
                float a = runs[i].x, b = runs[i].y;
                if (b - a < 0.5f)
                    continue;
                Stage1IsoArt.Band("Railing rail " + room.Id, new Vector3(a, railY + 0.32f, 0f), new Vector2(b - a, 0.08f), RailingColor, -9, root, into);
                int posts = Mathf.Max(2, Mathf.RoundToInt((b - a) / 1.9f) + 1);
                for (int p = 0; p < posts; p++)
                {
                    float px = Mathf.Lerp(a, b, posts == 1 ? 0.5f : (float)p / (posts - 1));
                    Stage1IsoArt.Band("Railing post " + room.Id, new Vector3(px - 0.06f, railY, 0f), new Vector2(0.12f, 0.42f), RailingColor, -9, root, into);
                }
            }
        }

        static void WallShield(S2S3Layout layout, S2S3Room room, MazeNode node, Stage1Maze maze, Transform root, List<GameObject> into)
        {
            Vector2 c = room.Center;
            float y1 = c.y + node.Height * 0.5f;
            var doors = Stage1IsoArt.CollectDoors(node, maze);
            int roll = Stage1IsoArt.Hash(maze.Seed, layout.Production ? StageMapGen.IdHash(room.Id) : room.Id.GetHashCode(), 911) % 9 - 4;
            float sx = c.x + roll * 1.5f; // -6 .. +6
            if (HasRim(doors, 'N') && Mathf.Abs(sx - c.x) < 5.5f)
                sx = c.x + (sx >= c.x ? 6.4f : -6.4f);
            Vector3 at = new Vector3(sx, y1 + 0.72f, 0f);
            SolidDisc("Wall shield rim " + room.Id, at, 0.68f, ShieldRimColor, WallDecorOrder, root, into);
            SolidDisc("Wall shield face " + room.Id, at, 0.56f, ShieldFaceColor, WallDecorOrder + 1, root, into);
            SolidDisc("Wall shield boss " + room.Id, at, 0.17f, ShieldBossColor, WallDecorOrder + 2, root, into);
        }

        static void SideCollapse(S2S3Layout layout, S2S3Room room, MazeNode node, Stage1Maze maze, Transform root, List<GameObject> into)
        {
            Vector2 c = room.Center;
            var doors = Stage1IsoArt.CollectDoors(node, maze);
            int side = Stage1IsoArt.Hash(maze.Seed, layout.Production ? StageMapGen.IdHash(room.Id) : room.Id.GetHashCode(), 131) % 4; // 0 N 1 E 2 W 3 S
            Vector2 spot;
            char rim;
            if (side == 0) { spot = new Vector2(c.x - 3.2f, c.y + 9.4f); rim = 'N'; }
            else if (side == 1) { spot = new Vector2(c.x + 12.4f, c.y - 2.4f); rim = 'E'; }
            else if (side == 2) { spot = new Vector2(c.x - 12.4f - 1.6f, c.y - 2.4f); rim = 'W'; }
            else { spot = new Vector2(c.x - 3.2f, c.y - 9.6f); rim = 'S'; }
            if (HasRim(doors, rim))
            {
                // Shift the pile off the door mouth on this side.
                if (rim == 'N' || rim == 'S')
                    spot.x = c.x + (spot.x >= c.x ? 6.8f : -6.8f);
                else
                    spot.y = c.y + (spot.y >= c.y ? 6.6f : -6.6f);
            }
            // Corner-cut rooms (production S2) delete their corner tiles; slide
            // the pile inboard when it would sit on the missing floor. The pad
            // covers the whole spread (crack sits +1.6 right of the pile heart).
            spot = Stage1IsoArt.KeepOnFloor(node, spot, 2f);
            for (int j = 0; j < 3; j++)
            {
                Vector3 pos = new Vector3(spot.x + j * 0.5f, spot.y - (j % 2) * 0.34f, 0f);
                GameObject go = Stage1IsoArt.Place("Collapse rubble " + room.Id + j, pos,
                    Stage1IsoArt.LoadSprite(DecalRoot + "rubble_0" + (j % 2) + ".png"), -700, false, root);
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null)
                    sr.color = RubbleTint;
                into.Add(go);
            }
            GameObject crack = Stage1IsoArt.Place("Collapse crack " + room.Id, new Vector3(spot.x + 1.6f, spot.y + 0.6f, 0f),
                Stage1IsoArt.LoadSprite(DecalRoot + "crack_00.png"), -700, false, root);
            into.Add(crack);
            GameObject slab = Stage1IsoArt.PlaceMesh("Collapse slab " + room.Id, new Vector3(spot.x - 0.7f, spot.y + 0.4f, 0f),
                new Vector3(1.6f, 1.0f, 1f), Stage1IsoArt.QuadMesh(), Stage1IsoArt.BandMaterial(SlabColor), -690, root);
            slab.transform.Rotate(0f, 0f, side % 2 == 0 ? -9f : 12f);
            into.Add(slab);
            Stage1IsoArt.Disc("Collapse shadow " + room.Id, new Vector3(spot.x + 0.6f, spot.y, 0f), 3.4f, 1.3f, ShadowColor, -750, root, into);
        }

        static void CopperGate(S2S3Room room, Transform root, List<GameObject> into, bool label)
        {
            S2S3EnvironmentPolish.CopperTrack(room.Center+(label?Vector2.zero:Vector2.down*GateTrackY), room.Id, root, into);
            if (label) Label("铜闸机关 · 清场停机", new Vector3(room.Center.x, room.Center.y + GateTrackY - 1.3f, 0f), 0.35f, CopperTextColor, root, into);
        }

        // ---- stage 3 vocabulary ----

        static void BonePillars(S2S3Layout layout, S2S3Room room, MazeNode node, Stage1Maze maze, Transform root, List<GameObject> into)
        {
            Vector2 c = room.Center;
            var doors = Stage1IsoArt.CollectDoors(node, maze);
            if (room.Type == S2S3RoomType.Altar)
            {
                BonePillar(c.x - 7.5f, c.y - 5.5f, room.Id + "a", root, into);
                BonePillar(c.x + 7.5f, c.y - 5.5f, room.Id + "b", root, into);
                BonePillar(c.x - 7.5f, c.y + 5.5f, room.Id + "c", root, into);
                BonePillar(c.x + 7.5f, c.y + 5.5f, room.Id + "d", root, into);
                return;
            }
            int flip = layout.Production ? room.DecorVariant % 2 : Stage1IsoArt.Hash(maze.Seed, layout.Production ? StageMapGen.IdHash(room.Id) : room.Id.GetHashCode(), 313) % 2;
            float ly = flip == 0 ? 6.8f : -6.2f;
            float pillarX=layout.Production && node.FloorVoids!=null ? node.FloorVoids[1].xMin-c.x-.9f : 9.5f;
            if (layout.Production && room.DecorVariant >= 2)
            {
                BonePillar(c.x - pillarX, c.y - ly, room.Id + "c", root, into);
                if (room.DecorVariant == 3) BonePillar(c.x + pillarX, c.y - ly, room.Id + "d", root, into);
            }
            BonePillar(c.x - pillarX, c.y + ly, room.Id + "a", root, into);
            BonePillar(c.x + pillarX, c.y + (flip == 0 ? 6.8f : -6.2f), room.Id + "b", root, into);
        }

        static void BonePillar(float px, float py, string id, Transform root, List<GameObject> into)
        {
            S2S3EnvironmentPolish.Pillar(new Vector2(px, py), id, root, into);
        }

        static void SectorDisc(S2S3Room room, Transform root, List<GameObject> into, bool label)
        {
            Vector2 c = room.Center + new Vector2(SectorOffsetX, SectorOffsetY);
            Stage1IsoArt.Disc("Sector disc " + room.Id, new Vector3(c.x, c.y, 0f), SectorRadius * 2f, SectorRadius, SectorTint, GroundMarkOrder, root, into);
            Stage1IsoArt.Disc("Sector core " + room.Id, new Vector3(c.x, c.y, 0f), SectorRadius * 1.4f, SectorRadius * 0.7f, SectorCoreTint, GroundMarkOrder + 1, root, into);
            // Broken warning ring: dashes along the squashed circle edge.
            for (int i = 0; i < 12; i++)
            {
                if (i % 4 == 3)
                    continue; // the gaps make the ring read as a warning, not a wall
                float t = i * Mathf.PI * 2f / 12f;
                Vector2 at = c + new Vector2(Mathf.Cos(t) * (SectorRadius + 0.35f), Mathf.Sin(t) * (SectorRadius + 0.35f) * 0.5f);
                float tangent = Mathf.Atan2(Mathf.Cos(t) * 0.5f, -Mathf.Sin(t)) * Mathf.Rad2Deg;
                GameObject dash = Stage1IsoArt.PlaceMesh("Sector ring dash " + room.Id + i, new Vector3(at.x - 0.3f, at.y - 0.08f, 0f),
                    new Vector3(0.6f, 0.16f, 1f), Stage1IsoArt.QuadMesh(), Stage1IsoArt.BandMaterial(SectorRingColor), GroundMarkOrder + 2, root);
                dash.transform.Rotate(0f, 0f, tangent);
                into.Add(dash);
            }
            GameObject runeA = Stage1IsoArt.PlaceMesh("Sector rune A " + room.Id, new Vector3(c.x - 0.5f, c.y - 0.09f, 0f),
                new Vector3(1.0f, 0.18f, 1f), Stage1IsoArt.QuadMesh(), Stage1IsoArt.BandMaterial(SectorRuneColor), GroundMarkOrder + 3, root);
            runeA.transform.Rotate(0f, 0f, 45f);
            into.Add(runeA);
            GameObject runeB = Stage1IsoArt.PlaceMesh("Sector rune B " + room.Id, new Vector3(c.x - 0.5f, c.y - 0.09f, 0f),
                new Vector3(1.0f, 0.18f, 1f), Stage1IsoArt.QuadMesh(), Stage1IsoArt.BandMaterial(SectorRuneColor), GroundMarkOrder + 3, root);
            runeB.transform.Rotate(0f, 0f, -45f);
            into.Add(runeB);
            if (label) Label("断仪扇区 · 首轮圆盘 r≈4\n圆盘外可站", new Vector3(c.x, c.y + 3.1f, 0f), 0.5f, SectorTextColor, root, into);
        }

        static void AltarWarmth(S2S3Room room, Transform root, List<GameObject> into)
        {
            Vector2 c = room.Center;
            Transform altar = root.Find("Altar_" + room.Id);
            S2S3EnvironmentPolish.AltarDais(altar != null ? (Vector2)altar.position : c, room.Id, root, into);
            // Four fires framing the altar diagonally: clear of the corner bone
            // pillars, the altar pad and every door mouth.
            Stage1IsoArt.Brazier(new Vector3(c.x - 6.5f, c.y - 7.5f, 0f), root, into);
            Stage1IsoArt.Brazier(new Vector3(c.x + 6.5f, c.y - 7.5f, 0f), root, into);
            Stage1IsoArt.Brazier(new Vector3(c.x - 6.5f, c.y + 7.5f, 0f), root, into);
            Stage1IsoArt.Brazier(new Vector3(c.x + 6.5f, c.y + 7.5f, 0f), root, into);
            Stage1IsoArt.Disc("Altar warm wash " + room.Id, new Vector3(c.x, c.y + 1f, 0f), 8f, 4f,
                new Color(.9f, .47f, .16f, .09f), -800, root, into);
        }

        static void ColdMaterials(Transform root)
        {
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer mr = renderers[i];
                if (mr == null || mr.sharedMaterial == null)
                    continue;
                Transform t = mr.transform;
                bool skip = false;
                while (t != null && t != root)
                {
                    // Doors, labels, the altar's warm light and the sector fixtures
                    // keep their authored materials; only stone goes cold.
                    if (t.name.StartsWith("Refined ") || t.name.StartsWith("Door_") || t.name.StartsWith("Label ")
                        || t.name.StartsWith("Sector ") || t.name.StartsWith("Altar warm")
                        || t.name == "Warm wash" || t.name == "Brazier shadow")
                    {
                        skip = true;
                        break;
                    }
                    t = t.parent;
                }
                if (skip)
                    continue;
                mr.sharedMaterial = Cold(mr.sharedMaterial);
            }
            var sprites = root.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < sprites.Length; i++)
            {
                SpriteRenderer sr = sprites[i];
                if (sr == null)
                    continue;
                string n = sr.name;
                if (n.StartsWith("Rubble") || n.StartsWith("Collapse") || n.StartsWith("Dressing") || n.StartsWith("Pillar bones"))
                    sr.color = ColdTint(sr.color);
            }
        }

        static readonly Dictionary<Material, Material> ColdMats = new Dictionary<Material, Material>();

        static Material Cold(Material src)
        {
            Material cold;
            if (ColdMats.TryGetValue(src, out cold))
                return cold;
            cold = new Material(src);
            cold.color = ColdTint(src.color);
            ColdMats[src] = cold;
            return cold;
        }

        static Color ColdTint(Color c)
        {
            return new Color(c.r * 0.78f, c.g * 0.85f, Mathf.Min(1f, c.b * 1.06f), c.a);
        }

        static void ColdFloorCells(Tilemap map)
        {
            BoundsInt bounds = map.cellBounds;
            for (int u = bounds.xMin; u <= bounds.xMax; u++)
            {
                for (int v = bounds.yMin; v <= bounds.yMax; v++)
                {
                    var cell = new Vector3Int(u, v, 0);
                    if (!map.HasTile(cell))
                        continue;
                    map.SetColor(cell, ColdTint(map.GetColor(cell)));
                }
            }
        }

        // ---- stand-ins and labels ----

        static readonly Color CopperTextColor = new Color(.95f, .72f, .45f);
        static readonly Color SectorTextColor = new Color(.78f, .84f, 1.0f);
        static readonly Color LabelTextColor = new Color(.93f, .91f, .86f);

        public static void StandIns(S2S3Layout layout, Transform root, List<GameObject> into)
        {
            S2S3Room first = null;
            for (int i = 0; i < layout.Rooms.Length; i++)
                if (layout.Rooms[i].First)
                    first = layout.Rooms[i];
            if (first == null)
                return;
            Vector2 c = first.Center;
            GameObject player = Stage1IsoArt.Place("Player archer", new Vector3(c.x, c.y - 3.2f, 0f),
                Stage1IsoArt.LoadSprite("Assets/Art/JianHai/Iso/Characters/Archer/jh_archer_idle_s_00.png"), 0, false, root);
            into.Add(player);
            Stage1IsoArt.Disc("Player shadow", new Vector3(c.x, c.y - 3.2f, 0f), 0.9f, 0.35f, ShadowColor, -750, root, into);
            Label("玩家", new Vector3(c.x, c.y - 4.6f, 0f), 0.45f, LabelTextColor, root, into);

            if (layout.Phase3)
            {
                StandIn("邪法师", "Assets/Art/JianHai/Iso/Enemies/Mage/jh_mage_idle_e_00.png", new Vector3(c.x - 3.6f, c.y + 1.6f, 0f), root, into);
                StandIn("普通小怪", "Assets/Art/JianHai/Iso/Enemies/Skel/jh_skel_idle_e_00.png", new Vector3(c.x + 3.2f, c.y + 1.1f, 0f), root, into);
                StandIn("普通小怪", "Assets/Art/JianHai/Iso/Enemies/Skel/jh_skel_idle_e_00.png", new Vector3(c.x + 0.6f, c.y + 2.7f, 0f), root, into);
            }
            else
            {
                StandIn("普通小怪", "Assets/Art/JianHai/Iso/Enemies/Skel/jh_skel_idle_e_00.png", new Vector3(c.x - 3.4f, c.y + 1.7f, 0f), root, into);
                StandIn("普通小怪", "Assets/Art/JianHai/Iso/Enemies/Skel/jh_skel_idle_e_00.png", new Vector3(c.x + 3.4f, c.y + 2.3f, 0f), root, into);
                GameObject shield = StandIn("盾兵", "Assets/Art/JianHai/Iso/Enemies/Skel/jh_skel_idle_e_00.png", new Vector3(c.x + 0.4f, c.y + 0.4f, 0f), root, into);
                if (shield != null)
                    SolidDisc("Shield soldier buckler", new Vector3(c.x + 1.2f, c.y + 1.0f, 0f), 0.62f, ShieldFaceColor, -5, root, into);
            }
        }

        static GameObject StandIn(string tag, string spritePath, Vector3 at, Transform root, List<GameObject> into)
        {
            GameObject go = Stage1IsoArt.Place("Stand-in " + tag, at, Stage1IsoArt.LoadSprite(spritePath), 0, false, root);
            into.Add(go);
            Stage1IsoArt.Disc("Stand-in shadow " + tag, at, 0.8f, 0.3f, ShadowColor, -750, root, into);
            Label(tag, at + Vector3.down * 1.5f, 0.42f, LabelTextColor, root, into);
            return go;
        }

        public static void Labels(S2S3Layout layout, Stage1Maze maze, Transform root, List<GameObject> into)
        {
            float top = float.MinValue;
            for (int i = 0; i < layout.Rooms.Length; i++)
            {
                S2S3Room room = layout.Rooms[i];
                MazeNode node = maze.Find(room.Id);
                Vector2 c = room.Center;
                if (c.y + 11f > top)
                    top = c.y + 11f;
                Label(RoomLabel(layout, room), new Vector3(c.x, c.y + 7.6f, 0f), 0.55f, LabelTextColor, root, into);
            }
            string phase = layout.Phase3 ? "阶段三" : "阶段二";
            Label(layout.Title + "\n" + phase + "样板房 · " + layout.PickNote,
                new Vector3(MapCenterX(layout), top + 9f, 0f), 0.8f, LabelTextColor, root, into);
        }

        static float MapCenterX(S2S3Layout layout)
        {
            float minX = float.MaxValue, maxX = float.MinValue;
            for (int i = 0; i < layout.Rooms.Length; i++)
            {
                Vector2 c = layout.Rooms[i].Center;
                if (c.x < minX) minX = c.x;
                if (c.x > maxX) maxX = c.x;
            }
            return (minX + maxX) * 0.5f;
        }

        static string RoomLabel(S2S3Layout layout, S2S3Room room)
        {
            switch (room.Type)
            {
                case S2S3RoomType.Chest:
                    return room.Id + " 宝箱房 · 2波\n开箱可跳过 · 大宝箱候选位";
                case S2S3RoomType.Mechanism:
                    return room.Id + " 铜闸机关房 · 1波\n无箱无坛 · 清完开门闸停";
                case S2S3RoomType.Stronghold:
                    return room.Id + " 阵地房 · 1波\n无箱无坛 · 断仪扇区";
                case S2S3RoomType.Altar:
                    return room.Id + (layout.Phase3 ? " 大祭坛" : " 中祭坛") + "\nCON → 下一阶段";
                default:
                    if (room.First)
                        return layout.Phase3
                            ? room.Id + " 第一间 · 普通\n邪法师×1 小怪×2 · 只教三连弹"
                            : room.Id + " 第一间 · 普通\n小怪×2 盾兵×1 · 不放铜闸";
                    return room.Id + " 普通房 · 1波";
            }
        }

        public static TextMesh Label(string text, Vector3 at, float size, Color color, Transform root, List<GameObject> into)
        {
            var go = new GameObject("Label " + text.Split('\n')[0]);
            go.transform.SetParent(root, false);
            go.transform.position = at;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.characterSize = size;
            tm.fontSize = 32;
            tm.color = color;
            BuiltinUiFont.Apply(tm);
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
                mr.sortingOrder = LabelOrder;
            into.Add(go);
            return tm;
        }

        static bool HasRim(List<Stage1IsoArt.DoorMark> doors, char rim)
        {
            for (int i = 0; i < doors.Count; i++)
                if (doors[i].Rim == rim)
                    return true;
            return false;
        }

        static GameObject SolidDisc(string name, Vector3 at, float size, Color color, int order, Transform root, List<GameObject> into)
        {
            GameObject go = Stage1IsoArt.PlaceMesh(name, at, new Vector3(size, size, 1f), SolidDiscMesh(), Stage1IsoArt.BandMaterial(color), order, root);
            into.Add(go);
            return go;
        }

        static Mesh SolidDiscMesh()
        {
            if (_solidDisc != null)
                return _solidDisc;
            _solidDisc = new Mesh();
            var verts = new List<Vector3> { Vector3.zero };
            var tris = new List<int>();
            for (int i = 0; i <= 24; i++)
            {
                float a = i * Mathf.PI * 2f / 24f;
                verts.Add(new Vector3(Mathf.Cos(a), Mathf.Sin(a)));
                if (i > 0)
                {
                    tris.Add(0);
                    tris.Add(i);
                    tris.Add(i + 1);
                }
            }
            _solidDisc.SetVertices(verts);
            _solidDisc.SetTriangles(tris, 0);
            _solidDisc.RecalculateBounds();
            return _solidDisc;
        }
    }
}
