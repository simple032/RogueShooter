using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using RogueShooter.Art;
using RogueShooter.Combat;
using RogueShooter.Demo;
using RogueShooter.Iso;
using RogueShooter.Maze;
using RogueShooter.Spawning;
using RogueShooter.Vision;
using Debug = UnityEngine.Debug;

namespace RogueShooter.Tools
{
    /// <summary>
    /// Stage-1 generator self-checks. <see cref="RunChecks"/>: Stage1PlayableChecks / Stage1MazeChecks /
    /// L5VisionChecks with iso off and on + generator paint/collision perf (largest generator map and the
    /// 380×238 stress map) → _s1gen_checks.txt. <see cref="RunPlayProbe"/> / <see cref="RunScaffoldProbe"/>:
    /// enter play mode with Stage1GenFlowProbe (batchmode: add <c>-s1probe stage1|scaffold</c>).
    /// </summary>
    public static class Stage1GenProbeMenu
    {
        [MenuItem("Tools/JianHai/Stage1 Generator Checks (iso off+on, perf)")]
        public static void RunChecksMenu()
        {
            bool ok = Checks(out string text);
            if (ok) Debug.Log(text);
            else Debug.LogError(text);
        }

        /// <summary>Batchmode: -executeMethod RogueShooter.Tools.Stage1GenProbeMenu.RunChecks</summary>
        public static void RunChecks()
        {
            bool ok = false;
            try
            {
                ok = Checks(out string text);
                Debug.Log(text);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }

            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static void RunPlayProbe()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Stage1Maze.scene", OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        public static void RunScaffoldProbe()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/ThreeRouteScaffold.scene", OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        static bool Checks(out string text)
        {
            var sb = new StringBuilder();
            bool ok = true;
            string dir = EnemyPoolDraft.ResolveDirectory();
            if (!EnemyPoolDraft.TryLoadFromDirectory(dir, out string loadErr))
                sb.AppendLine("[S1Gen] draft CSV " + loadErr);
            bool iso0 = IsoConfig.Enabled;
            try
            {
                for (int iso = 0; iso < 2; iso++)
                {
                    IsoConfig.Enabled = iso == 1;
                    string play = Stage1PlayableChecks.Run();
                    IsoConfig.Enabled = iso == 1;
                    string maze = Stage1MazeChecks.Run();
                    IsoConfig.Enabled = iso == 1;
                    string l5 = L5VisionChecks.Run();
                    sb.AppendLine("[S1Gen] iso=" + iso + " Stage1PlayableChecks=" + (play ?? "PASS")
                                  + " | Stage1MazeChecks=" + (maze ?? "PASS") + " | L5VisionChecks=" + (l5 ?? "PASS"));
                    ok &= play == null && maze == null && l5 == null;
                }

                sb.AppendLine("[S1Gen] gen raster seeds=" + Stage1PlayableChecks.GenSeedsChecked
                              + " maxRaster=" + Stage1PlayableChecks.GenMaxRasterW + "x" + Stage1PlayableChecks.GenMaxRasterH
                              + " maxRingWalls=" + Stage1PlayableChecks.GenMaxWallCells + " maxWallRects=" + Stage1PlayableChecks.GenMaxWallRects
                              + " (full fill would be " + Stage1PlayableChecks.GenMaxFullFill + ") corridorProbes=" + Stage1PlayableChecks.GenCorridorProbes);
                sb.Append(L5VisionChecks.Report());
            }
            finally
            {
                IsoConfig.Enabled = iso0;
            }

            ok &= Perf(sb);
            text = sb.ToString();
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "_s1gen_checks.txt");
            File.WriteAllText(path, (ok ? "CHECKS PASS\n" : "CHECKS FAIL\n") + text);
            return ok;
        }

        /// <summary>Edit-mode paint + composite timings (5 runs each, first = cold).</summary>
        static bool Perf(StringBuilder sb)
        {
            var ids = JianHaiStage1Art.TileIds;
            var assets = new TileBase[ids.Length];
            for (int i = 0; i < ids.Length; i++)
                assets[i] = AssetDatabase.LoadAssetAtPath<Tile>(JianHaiStage1Art.TilesFolder + ids[i] + ".asset");
            int bestSeed = 1, bestCells = 0;
            for (int s = 1; s <= 200; s++)
            {
                Stage1MazeRaster r = Stage1MazeRaster.Build(Stage1MazeGen.Generate(s));
                if (r.W * r.H > bestCells)
                {
                    bestCells = r.W * r.H;
                    bestSeed = s;
                }
            }

            var cases = new List<KeyValuePair<string, Stage1Maze>>
            {
                new KeyValuePair<string, Stage1Maze>("generator seed " + bestSeed + " (largest bbox of seeds 1-200)", Stage1MazeGen.Generate(bestSeed)),
                new KeyValuePair<string, Stage1Maze>("stress 5x4 rooms 380x238", Stage1MazeRaster.StressMaze())
            };
            bool ok = true;
            foreach (var c in cases)
            {
                var go = new GameObject("PerfGen");
                var gen = go.AddComponent<Stage1GenWorld>();
                gen.SetTiles(ids, assets);
                var line = new StringBuilder();
                float maxT = 0f, maxC = 0f, maxR = 0f;
                for (int run = 0; run < 5; run++)
                {
                    gen.Build(c.Value, 42);
                    line.Append(run == 0 ? " cold " : " ").Append(gen.LastTilemapMs.ToString("0.0")).Append('/').Append(gen.LastColliderMs.ToString("0.0"));
                    if (run > 0)
                    {
                        maxT = Mathf.Max(maxT, gen.LastTilemapMs);
                        maxC = Mathf.Max(maxC, gen.LastColliderMs);
                        maxR = Mathf.Max(maxR, gen.LastRasterMs);
                    }
                }

                Stage1MazeRaster r = gen.Raster;
                string ring = r.CheckRing();
                ok &= ring == null && gen.MissingTiles == 0;
                sb.AppendLine("[S1Gen perf] " + c.Key + ": " + r.Describe()
                              + " floorMapCells=" + CountTiles(gen.FloorMap) + " wallsMapCells=" + CountTiles(gen.WallsMap)
                              + " compositeBoxes=" + gen.LastFootprintBoxes + " paths=" + gen.LastCompositePaths
                              + " | warm max: raster " + maxR.ToString("0.0") + "ms tilemap " + maxT.ToString("0.0") + "ms composite " + maxC.ToString("0.0")
                              + "ms | runs tilemap/composite ms:" + line + (ring != null ? " RING FAIL " + ring : "") + " missingTiles=" + gen.MissingTiles);
                Object.DestroyImmediate(go);
            }

            return ok;
        }

        static int CountTiles(Tilemap map)
        {
            map.CompressBounds();
            BoundsInt b = map.cellBounds;
            int n = 0;
            foreach (Vector3Int p in b.allPositionsWithin)
                if (map.HasTile(p)) n++;
            return n;
        }
    }
}
