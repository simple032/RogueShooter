using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RogueShooter.Maze
{
    /// <summary>
    /// Separate 28×22 generation. Stage1Maze and MazeRules stay 36×28 / 58×50.
    /// Entry: <see cref="Generate"/>.
    /// </summary>
    public static class Stage1MazeCopy
    {
        public static readonly int[] MeasureSeeds = { 42, 7, 99 };

        public static Stage1Maze Generate(int seed)
        {
            return Stage1MazeGen.Generate(seed, MazeLayout.Copy28x22);
        }

        public static string Run()
        {
            string mainErr = Stage1MazeChecks.Run();
            if (mainErr != null)
                return "main checks " + mainErr;

            for (int i = 0; i < MeasureSeeds.Length; i++)
            {
                int seed = MeasureSeeds[i];
                Stage1Maze main = Stage1MazeGen.Generate(seed);
                Stage1Maze copy = Generate(seed);
                if (main.Nodes[0].Width != 36f || main.Nodes[0].Height != 28f)
                    return "main size changed seed=" + seed;
                string err = CheckCopy(main, copy);
                if (err != null)
                    return "seed=" + seed + " " + err;
            }

            Stage1Maze again = Stage1MazeGen.Generate(42);
            Stage1Maze viaMain = Stage1MazeGen.Generate(42, MazeLayout.Main);
            if (again.Signature != viaMain.Signature)
                return "main layout overload drifted";
            return null;
        }

        public static string Report()
        {
            var sb = new StringBuilder();
            sb.AppendLine("entry=Stage1MazeCopy.Generate");
            sb.AppendLine("rooms=28x22 pitch=50/44 gap=22/22 corridor=8 speed=6");
            sb.AppendLine("main stays 36x28 pitch=58/50");
            for (int i = 0; i < MeasureSeeds.Length; i++)
                sb.Append(FormatSeed(MeasureSeeds[i]));
            return sb.ToString();
        }

        static string CheckCopy(Stage1Maze main, Stage1Maze copy)
        {
            if (Topology(main) != Topology(copy))
                return "kinds or links changed";
            MazeLayout layout = MazeLayout.Copy28x22;
            for (int i = 0; i < copy.Nodes.Length; i++)
            {
                if (copy.Nodes[i].Width != layout.Width || copy.Nodes[i].Height != layout.Height)
                    return copy.Nodes[i].Id + " size";
            }

            if (System.Math.Abs(layout.PitchX - layout.Width - 22f) > 0.01f
                || System.Math.Abs(layout.PitchY - layout.Height - 22f) > 0.01f)
                return "gap not 22/22";
            for (int i = 0; i < copy.Edges.Length; i++)
            {
                if (System.Math.Abs(copy.Edges[i].Width - 8f) > 0.01f)
                    return "corridor width";
            }

            return null;
        }

        static string FormatSeed(int seed)
        {
            Stage1Maze maze = Generate(seed);
            MazePacing pace = Stage1MazeGen.MeasurePacing(maze, MazeRules.PlayMoveSpeed);
            var sb = new StringBuilder();
            sb.Append("seed=").Append(seed);
            sb.Append(" tpl=").Append(maze.TemplateId);
            sb.Append(" rooms=");
            for (int i = 0; i < maze.Nodes.Length; i++)
            {
                if (i > 0)
                    sb.Append(',');
                sb.Append(maze.Nodes[i].Id).Append(':').Append(MazeRules.Label(maze.Nodes[i].Kind));
            }

            sb.AppendLine();
            sb.Append("path=");
            int[] tour = Stage1MazeGen.VisitOrder(maze);
            for (int i = 0; i < tour.Length; i++)
            {
                if (i > 0)
                    sb.Append('-');
                sb.Append(maze.Nodes[tour[i]].Id);
            }

            sb.AppendLine();
            sb.Append("poly=");
            sb.AppendLine(Polyline(maze, tour));
            sb.Append("walk=").Append(pace.FullWalkSeconds.ToString("0.0", CultureInfo.InvariantCulture));
            sb.Append("s full=").Append(pace.FullTotalEstimate.ToString("0.0", CultureInfo.InvariantCulture));
            sb.AppendLine("s");
            return sb.ToString();
        }

        static string Polyline(Stage1Maze maze, int[] tour)
        {
            var sb = new StringBuilder();
            bool any = false;
            for (int i = 0; i < tour.Length - 1; i++)
            {
                MazeEdge edge = FindEdge(maze, maze.Nodes[tour[i]].Id, maze.Nodes[tour[i + 1]].Id);
                if (edge == null || edge.Points == null)
                    continue;
                for (int p = 0; p < edge.Points.Length; p++)
                {
                    if (any && p == 0)
                        continue;
                    if (any)
                        sb.Append('-');
                    sb.Append(edge.Points[p].X.ToString("0.#", CultureInfo.InvariantCulture));
                    sb.Append(',');
                    sb.Append(edge.Points[p].Y.ToString("0.#", CultureInfo.InvariantCulture));
                    any = true;
                }
            }

            return sb.ToString();
        }

        static MazeEdge FindEdge(Stage1Maze maze, string a, string b)
        {
            for (int i = 0; i < maze.Edges.Length; i++)
            {
                if (maze.Edges[i].Connects(a, b))
                    return maze.Edges[i];
            }

            return default(MazeEdge);
        }

        static string Topology(Stage1Maze maze)
        {
            var names = new List<string>();
            for (int i = 0; i < maze.Nodes.Length; i++)
                names.Add(maze.Nodes[i].Id + ":" + (int)maze.Nodes[i].Kind);
            names.Sort(System.StringComparer.Ordinal);
            var eds = new List<string>();
            for (int i = 0; i < maze.Edges.Length; i++)
            {
                string a = maze.Edges[i].FromId;
                string b = maze.Edges[i].ToId;
                if (string.CompareOrdinal(a, b) > 0)
                {
                    string t = a;
                    a = b;
                    b = t;
                }

                eds.Add(a + "--" + b);
            }

            eds.Sort(System.StringComparer.Ordinal);
            return string.Join(";", names.ToArray()) + "|" + string.Join(";", eds.ToArray());
        }
    }
}
