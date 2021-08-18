using System;

namespace SindenLib.Imaging
{
    internal struct IntPoint
    {
        public int X;
        public int Y;

        public IntPoint(int x, int y) => (X, Y) = (x, y);

        public double DistanceTo(IntPoint o)
        {
            return Math.Sqrt((X - o.X) * (X - o.X) + (Y - o.Y) * (Y - o.Y));
        }

        public override bool Equals(object obj)
        {
            return obj is IntPoint point &&
                   X == point.X &&
                   Y == point.Y;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }

        public static IntPoint operator +(IntPoint point1, IntPoint point2)
        {
            return new IntPoint(point1.X + point2.X, point1.Y + point2.Y);
        }

        public static IntPoint operator -(IntPoint point1, IntPoint point2)
        {
            return new IntPoint(point1.X - point2.X, point1.Y - point2.Y);
        }

        public static IntPoint operator *(IntPoint point, int factor)
        {
            return new IntPoint(point.X * factor, point.Y * factor);
        }

        public static IntPoint operator /(IntPoint point, int factor)
        {
            return new IntPoint(point.X / factor, point.Y / factor);
        }

        public static bool operator ==(IntPoint point1, IntPoint point2)
        {
            return (point1.X == point2.X) && (point1.Y == point2.Y);
        }

        public static bool operator !=(IntPoint point1, IntPoint point2)
        {
            return (point1.X != point2.X) || (point1.Y != point2.Y);
        }
    }
}
