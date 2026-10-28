// The vortex storm (U.RICO-T96, owner rule: port faithfully). vortex_step.gml never hurt anyone itself: once set off the
// device grew for 250 steps, then pulled every shape (obj_polygon_parent) whose bounding box was within vortexRadius
// (200 px) toward it at 1 px a step for the rest of its growth and the 300-step wait, then blew: every shape still within
// the radius was sent straight out at 10 px a step. A tank was hurt only when a shape moving faster than 5 px a step
// struck it (obj_bottom's collision with obj_polygon_parent), and that hurt it once: every shape stopped and the turn ended.
// Nothing in the script touches a hazard or the border. Design reference only (UR-LAW-2); this is its own model: the shapes
// are the level's walls grouped by shared corners, each moving as one rigid piece, and every step is one GameMaker step.
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

        /// <summary>
        /// The target a flung shape strikes first, or null when none does. <paramref name="radius"/> is the pull radius in
        /// arena units; <paramref name="seed"/> picks the exit direction of a shape sitting dead on the vortex.
        /// </summary>
        public static Target? Strike(Vec2 center, double radius, Level level, IReadOnlyList<Target> targets, int seed)
        {
            if (level == null || targets == null || targets.Count == 0 || !(radius > 0))
            {
                return null;
            }

            List<List<Wall>> shapes = Shapes(level.Walls);
            double pull = PullPixels * ShapeLibrary.UnitsPerPixel;
            double fling = FlingPixels * ShapeLibrary.UnitsPerPixel;
            var random = new Random(seed);

            // Each shape: its walls' corners, shifted as it moves.
            var corners = new List<Vec2[]>(shapes.Count);
            foreach (List<Wall> shape in shapes)
            {
                var points = new Vec2[shape.Count * 2];
                for (int i = 0; i < shape.Count; i++)
                {
                    points[i * 2] = shape[i].A;
                    points[i * 2 + 1] = shape[i].B;
                }

                corners.Add(points);
            }

            // Pulling in: each shape in reach drifts to the vortex a step at a time (it keeps pulling while it stays in reach).
            for (int s = 0; s < corners.Count; s++)
            {
                if (BoxDistance(corners[s], center) > radius)
                {
                    continue;
                }

                Vec2 origin = BoxCenter(corners[s]);
                double travel = Math.Min(Vec2.Distance(origin, center), PullSteps * pull);
                Shift(corners[s], (center - origin).Normalized * travel);
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

            for (int step = 0; step < FlingSteps && flying.Count > 0; step++)
            {
                foreach (int s in flying)
                {
                    Shift(corners[s], heading[s] * fling);
                }

                foreach (int s in flying)
                {
                    foreach (Target target in targets)
                    {
                        if (Touches(corners[s], target))
                        {
                            return target;
                        }
                    }
                }
            }

            return null;
        }

        // Walls sharing a corner belong to one shape (a random level lays each shape's outline as a closed loop).
        private static List<List<Wall>> Shapes(IReadOnlyList<Wall> walls)
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

            var byRoot = new Dictionary<int, List<Wall>>();
            var shapes = new List<List<Wall>>();
            for (int i = 0; i < n; i++)
            {
                int root = Find(parent, i);
                if (!byRoot.TryGetValue(root, out List<Wall> shape))
                {
                    shape = new List<Wall>();
                    byRoot[root] = shape;
                    shapes.Add(shape);
                }

                shape.Add(walls[i]);
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
}
