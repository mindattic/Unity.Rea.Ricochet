// A plain 2D vector in double precision, so the bounce solver has no UnityEngine dependency and runs under dotnet
// test. World units, y up. Angles are degrees, counterclockwise from +x: GameMaker's lengthdir_x/lengthdir_y
// convention (0 = right, 90 = up), which is what the 2013 original's aim angles mean.
using System;

namespace Ricochet.Core
{
    public readonly struct Vec2 : IEquatable<Vec2>
    {
        public readonly double X;
        public readonly double Y;

        public Vec2(double x, double y)
        {
            X = x;
            Y = y;
        }

        public static Vec2 Zero => new Vec2(0, 0);

        public double Length => Math.Sqrt(X * X + Y * Y);

        public double LengthSquared => X * X + Y * Y;

        /// <summary>This vector scaled to length 1 (zero stays zero).</summary>
        public Vec2 Normalized
        {
            get
            {
                double length = Length;
                return length < 1e-12 ? Zero : new Vec2(X / length, Y / length);
            }
        }

        /// <summary>A unit vector pointing at <paramref name="degrees"/> (GML lengthdir_x/lengthdir_y with length 1).</summary>
        public static Vec2 FromAngle(double degrees)
        {
            double radians = degrees * Math.PI / 180.0;
            return new Vec2(Math.Cos(radians), Math.Sin(radians));
        }

        /// <summary>This vector's direction in degrees, 0 to 360 (GML point_direction).</summary>
        public double Angle => Angles.Normalize(Math.Atan2(Y, X) * 180.0 / Math.PI);

        public static double Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;

        /// <summary>The z of the 3D cross product: positive when <paramref name="b"/> is counterclockwise of <paramref name="a"/>.</summary>
        public static double Cross(Vec2 a, Vec2 b) => a.X * b.Y - a.Y * b.X;

        public static double Distance(Vec2 a, Vec2 b) => (a - b).Length;

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);

        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);

        public static Vec2 operator -(Vec2 a) => new Vec2(-a.X, -a.Y);

        public static Vec2 operator *(Vec2 a, double s) => new Vec2(a.X * s, a.Y * s);

        public static Vec2 operator *(double s, Vec2 a) => new Vec2(a.X * s, a.Y * s);

        public static Vec2 operator /(Vec2 a, double s) => new Vec2(a.X / s, a.Y / s);

        public static bool operator ==(Vec2 a, Vec2 b) => a.Equals(b);

        public static bool operator !=(Vec2 a, Vec2 b) => !a.Equals(b);

        public bool Equals(Vec2 other) => X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is Vec2 other && Equals(other);

        public override int GetHashCode() => X.GetHashCode() * 397 ^ Y.GetHashCode();

        public override string ToString() => "(" + X.ToString("0.###") + ", " + Y.ToString("0.###") + ")";
    }

    /// <summary>Degree helpers.</summary>
    public static class Angles
    {
        /// <summary><paramref name="degrees"/> wrapped into [0, 360).</summary>
        public static double Normalize(double degrees)
        {
            double wrapped = degrees % 360.0;
            if (wrapped < 0)
            {
                wrapped += 360.0;
            }

            return wrapped >= 360.0 ? 0.0 : wrapped;
        }
    }
}
