// The vortex storm (U.RICO-T96, owner rule: port faithfully). vortex_step.gml never hurt anyone itself: once set off the
// device grew for 250 steps, then pulled every shape (obj_polygon_parent) whose bounding box was within vortexRadius
// (200 px) toward it at 1 px a step for the rest of its growth and the 300-step wait, then blew: every shape still within
// the radius was sent straight out at 10 px a step. A tank was hurt only when a shape moving faster than 5 px a step
// struck it (obj_bottom's collision with obj_polygon_parent), and that hurt it once: every shape stopped and the turn ended.
// Nothing in the script touches a hazard or the border, and nothing ever puts a moved shape back: the shapes stay where the
// storm left them for the rest of the round (U.RICO-T134: Play says where each went, Level.Apply keeps it, the level view
// walks the shapes along it). Design reference only (UR-LAW-2); this is its own model: the shapes are the level's walls
// grouped by shared corners, each moving as one rigid piece, and every step is one GameMaker step. The shapes also turn: every
// step of the pull and the wait vortex_step.gml does image_angle -= 1 on each shape in reach (a degree clockwise, as Core's
// angles run counterclockwise), 550 degrees in all, never while blowing; a turn about the shape's own origin, modelled as the
// centre of its bounding box (StormShape.Pivot), which the pull carries along.
using System;
using System.Collections.Generic;

namespace Ricochet.Core.Arsenal
{
    using Ricochet.Core.Aim;
    using Ricochet.Core.Level;

    /// <summary>Plays out a set-off vortex's pull and blow over a level's shapes and says which tank a flung shape strikes.</summary>
    public static class VortexStorm
    {
        /// <summary>Steps spent pulling (the second half of the growth to 1 at 0.002 a step, then the 300-step wait).</summary>
        public const int PullSteps = 250 + 300;

        /// <summary>Steps spent flinging (blowing for 100 steps, then waiting2 for 300) before the vortex finishes.</summary>
        public const int FlingSteps = 100 + 300;

        /// <summary>Pixels a step a shape is pulled in (move_towards_point(..., 1)).</summary>
        public const double PullPixels = 1;

        /// <summary>Pixels a step a shape is flung out (move_towards_point(..., -10)).</summary>
        public const double FlingPixels = 10;

        /// <summary>The step of the implosion (counted from it going off) the pull starts at: spr_vortex reaches half size at 0.002 a step.</summary>
        public const int PullStart = 250;

        /// <summary>The step the blow starts at: 500 steps of growth, then the 300-step wait.</summary>
        public const int FlingStart = 500 + 300;

        /// <summary>Degrees a step a shape in reach turns while pulled (image_angle -= 1; negative is clockwise).</summary>
        public const double SpinPerStep = -1;

        /// <summary>The whole turn a pulled shape makes: <see cref="PullSteps"/> steps of <see cref="SpinPerStep"/>.</summary>
        public const double SpinDegrees = SpinPerStep * PullSteps;

        /// <summary>
        /// The target a flung shape strikes first, or null when none does. <paramref name="radius"/> is the pull radius in
        /// arena units; <paramref name="seed"/> picks the exit direction of a shape sitting dead on the vortex.
        /// </summary>
        public static Target? Strike(Vec2 center, double radius, Level level, IReadOnlyList<Target> targets, int seed)
        {
            return Play(center, radius, level, targets, seed).Struck;
        }

        /// <summary>
        /// Plays the storm out: the target a flung shape strikes first (or none) and where every shape that moved went, as a pull
        /// and a fling each, so a view can walk the shapes along it and the level can keep them where they ended (U.RICO-T134).
        /// The level itself is not changed here; <see cref="Level.Apply"/> keeps the result.
        /// </summary>
        public static StormResult Play(Vec2 center, double radius, Level level, IReadOnlyList<Target> targets, int seed)
        {
            if (level == null || !(radius > 0))
            {
                return StormResult.None;
            }

            List<List<int>> shapes = Shapes(level.Walls);
            double pull = PullPixels * ShapeLibrary.UnitsPerPixel;
            double fling = FlingPixels * ShapeLibrary.UnitsPerPixel;
            var random = new Random(seed);

            // Each shape: its walls' corners, shifted as it moves.
            var corners = new List<Vec2[]>(shapes.Count);
            foreach (List<int> shape in shapes)
            {
                var points = new Vec2[shape.Count * 2];
                for (int i = 0; i < shape.Count; i++)
                {
                    points[i * 2] = level.Walls[shape[i]].A;
                    points[i * 2 + 1] = level.Walls[shape[i]].B;
                }

                corners.Add(points);
            }

            // Pulling in: each shape in reach drifts to the vortex a step at a time (it keeps pulling while it stays in reach).
            var pulled = new Vec2[corners.Count];
            var pivots = new Vec2[corners.Count];
            var reached = new bool[corners.Count];
            for (int s = 0; s < corners.Count; s++)
            {
                if (BoxDistance(corners[s], center) > radius)
                {
                    continue;
                }

                Vec2 origin = BoxCenter(corners[s]);
                pivots[s] = origin;
                reached[s] = true;
                double travel = Math.Min(Vec2.Distance(origin, center), PullSteps * pull);
                pulled[s] = (center - origin).Normalized * travel;
                Shift(corners[s], pulled[s]);
                Turn(corners[s], origin + pulled[s], SpinDegrees);
            }

            // Blowing out: every shape still in reach leaves along the line from the vortex through itself.
            var heading = new Vec2[corners.Count];
            var flying = new List<int>();
            for (int s = 0; s < corners.Count; s++)
            {
                if (BoxDistance(corners[s], center) > radius)
                {
                    continue;
                }

                Vec2 away = BoxCenter(corners[s]) - center;
                heading[s] = away.Length > 1e-9 ? away.Normalized : Vec2.FromAngle(random.Next(0, 360));
                flying.Add(s);
            }

            Target? struck = null;
            int flung = 0;
            for (int step = 0; step < FlingSteps && flying.Count > 0 && struck == null; step++)
            {
                flung++;
                foreach (int s in flying)
                {
                    Shift(corners[s], heading[s] * fling);
                }

                if (targets == null)
                {
                    continue;
                }

                foreach (int s in flying)
                {
                    foreach (Target target in targets)
                    {
                        if (struck == null && Touches(corners[s], target))
                        {
                            struck = target;
                        }
                    }
                }
            }

            var moved = new List<StormShape>();
            for (int s = 0; s < corners.Count; s++)
            {
                bool flies = flying.Contains(s);
                if (flies || reached[s])
                {
                    moved.Add(new StormShape(shapes[s], pulled[s], flies ? heading[s] * (fling * flung) : default, pivots[s]));
                }
            }

            return new StormResult(struck, moved, flung);
        }

        // Walls sharing a corner belong to one shape (a random level lays each shape's outline as a closed loop); as indices into the walls.
        private static List<List<int>> Shapes(IReadOnlyList<Wall> walls)
        {
            int n = walls.Count;
            var parent = new int[n];
            for (int i = 0; i < n; i++)
            {
                parent[i] = i;
            }

            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    if (Vec2.Distance(walls[i].A, walls[j].A) < 1e-6 || Vec2.Distance(walls[i].A, walls[j].B) < 1e-6
                        || Vec2.Distance(walls[i].B, walls[j].A) < 1e-6 || Vec2.Distance(walls[i].B, walls[j].B) < 1e-6)
                    {
                        parent[Find(parent, i)] = Find(parent, j);
                    }
                }
            }

            var byRoot = new Dictionary<int, List<int>>();
            var shapes = new List<List<int>>();
            for (int i = 0; i < n; i++)
            {
                int root = Find(parent, i);
                if (!byRoot.TryGetValue(root, out List<int> shape))
                {
                    shape = new List<int>();
                    byRoot[root] = shape;
                    shapes.Add(shape);
                }

                shape.Add(i);
            }

            return shapes;
        }

        private static int Find(int[] parent, int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }

            return i;
        }

        private static void Shift(Vec2[] points, Vec2 by)
        {
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = points[i] + by;
            }
        }

        private static void Turn(Vec2[] points, Vec2 about, double degrees)
        {
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = StormShape.Turned(points[i], about, degrees);
            }
        }

        private static void Bounds(Vec2[] points, out double minX, out double minY, out double maxX, out double maxY)
        {
            minX = minY = double.MaxValue;
            maxX = maxY = double.MinValue;
            foreach (Vec2 p in points)
            {
                minX = Math.Min(minX, p.X);
                minY = Math.Min(minY, p.Y);
                maxX = Math.Max(maxX, p.X);
                maxY = Math.Max(maxY, p.Y);
            }
        }

        private static Vec2 BoxCenter(Vec2[] points)
        {
            Bounds(points, out double minX, out double minY, out double maxX, out double maxY);
            return new Vec2((minX + maxX) / 2, (minY + maxY) / 2);
        }

        // distance_to_point: from a point to the instance's bounding box (0 inside it).
        private static double BoxDistance(Vec2[] points, Vec2 p)
        {
            Bounds(points, out double minX, out double minY, out double maxX, out double maxY);
            double dx = Math.Max(Math.Max(minX - p.X, 0), p.X - maxX);
            double dy = Math.Max(Math.Max(minY - p.Y, 0), p.Y - maxY);
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static bool Touches(Vec2[] points, Target target)
        {
            for (int i = 0; i < points.Length; i += 2)
            {
                if (Geometry.SegmentDistance(points[i], points[i + 1], target.Center, out _) <= target.Radius)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>One shape the storm moved: the walls it is made of, how far the pull took it and how far the fling then did.</summary>
    public sealed class StormShape
    {
        public StormShape(IReadOnlyList<int> walls, Vec2 pull, Vec2 fling, Vec2 pivot)
        {
            Pivot = pivot;
            Walls = walls;
            Pull = pull;
            Fling = fling;
        }

        /// <summary>Indices into the level's walls.</summary>
        public IReadOnlyList<int> Walls { get; }

        /// <summary>The centre of the shape's bounding box before the storm: what it turns about (the pull carries it along).</summary>
        public Vec2 Pivot { get; }

        /// <summary>The way it was drawn in (arena units).</summary>
        public Vec2 Pull { get; }

        /// <summary>The way it was then flung out, as far as it went before the storm ended.</summary>
        public Vec2 Fling { get; }

        /// <summary><paramref name="point"/> turned <paramref name="degrees"/> (counterclockwise positive) about <paramref name="about"/>.</summary>
        public static Vec2 Turned(Vec2 point, Vec2 about, double degrees)
        {
            double radians = degrees * Math.PI / 180.0;
            double cos = Math.Cos(radians), sin = Math.Sin(radians);
            Vec2 d = point - about;
            return new Vec2(about.X + d.X * cos - d.Y * sin, about.Y + d.X * sin + d.Y * cos);
        }

        /// <summary>How far the shape has turned <paramref name="steps"/> steps after the vortex went off: a degree clockwise a step of the pull and the wait, still after.</summary>
        public static double AngleAt(double steps)
        {
            return VortexStorm.SpinPerStep * Math.Max(0, Math.Min(steps - VortexStorm.PullStart, VortexStorm.PullSteps));
        }

        /// <summary>
        /// Where a point of the shape (as the level laid it) is <paramref name="steps"/> steps after the vortex went off: turned
        /// about the pivot as the pull carries it, then offset as <see cref="OffsetAt"/> says.
        /// </summary>
        public Vec2 PoseAt(Vec2 point, double steps, int flingSteps)
        {
            return Turned(point, Pivot, AngleAt(steps)) + OffsetAt(steps, flingSteps);
        }

        /// <summary>Where a point of the shape ends up once the storm is over (what <see cref="Level.Apply"/> keeps).</summary>
        public Vec2 Final(Vec2 point)
        {
            return Turned(point, Pivot, VortexStorm.SpinDegrees) + Pull + Fling;
        }

        /// <summary>
        /// Where the shape is, as an offset from where it started, <paramref name="steps"/> steps after the vortex went off
        /// (<paramref name="flingSteps"/> is <see cref="StormResult.FlingSteps"/>): still until the pull starts, drawn in a pixel a
        /// step, still while the vortex waits, then flung out 10 px a step until the storm ends.
        /// </summary>
        public Vec2 OffsetAt(double steps, int flingSteps)
        {
            double drawn = Math.Max(0, Math.Min(steps - VortexStorm.PullStart, VortexStorm.PullSteps));
            Vec2 offset = Pull.Length > 1e-12
                ? Pull.Normalized * Math.Min(Pull.Length, drawn * VortexStorm.PullPixels * ShapeLibrary.UnitsPerPixel)
                : default;
            if (flingSteps > 0 && Fling.Length > 1e-12)
            {
                double flung = Math.Max(0, Math.Min(steps - VortexStorm.FlingStart, flingSteps));
                offset = offset + Fling * (flung / flingSteps);
            }

            return offset;
        }
    }

    /// <summary>What a vortex's storm did: who a flung shape struck (if anyone), where each shape went and for how long it flew.</summary>
    public sealed class StormResult
    {
        public static readonly StormResult None = new StormResult(null, new List<StormShape>(), 0);

        public StormResult(Target? struck, IReadOnlyList<StormShape> shapes, int flingSteps)
        {
            Struck = struck;
            Shapes = shapes;
            FlingSteps = flingSteps;
        }

        public Target? Struck { get; }

        /// <summary>Every shape that moved (empty when none was in reach).</summary>
        public IReadOnlyList<StormShape> Shapes { get; }

        /// <summary>Steps the flung shapes flew (cut short at the step one struck a tank).</summary>
        public int FlingSteps { get; }

        /// <summary>The step, counted from the vortex going off, after which nothing moves any more.</summary>
        public int EndStep => VortexStorm.FlingStart + FlingSteps;

        /// <summary>True when <paramref name="other"/> moves the same walls the same way (the same storm seen by another peer).</summary>
        public bool SameAs(StormResult other)
        {
            if (other == null || FlingSteps != other.FlingSteps || Shapes.Count != other.Shapes.Count)
            {
                return false;
            }

            for (int i = 0; i < Shapes.Count; i++)
            {
                StormShape a = Shapes[i], b = other.Shapes[i];
                if (a.Walls.Count != b.Walls.Count || Vec2.Distance(a.Pull, b.Pull) > 1e-9 || Vec2.Distance(a.Fling, b.Fling) > 1e-9 || Vec2.Distance(a.Pivot, b.Pivot) > 1e-9)
                {
                    return false;
                }

                for (int w = 0; w < a.Walls.Count; w++)
                {
                    if (a.Walls[w] != b.Walls[w])
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
