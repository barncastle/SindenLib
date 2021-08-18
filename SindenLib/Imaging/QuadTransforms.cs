using System;
using System.Collections.Generic;

namespace SindenLib.Imaging
{
    internal static class QuadTransforms
    {
        private const double Tolerance = 1E-13;

        public static double[] GetXYBack(IList<IntPoint> corners, double offsetX, double offsetY, int width, int height)
        {
            GetBoundingRectangle(corners, out IntPoint minXY, out IntPoint maxXY);

            minXY.X = Math.Max(minXY.X, 0);
            minXY.Y = Math.Max(minXY.Y, 0);
            maxXY.X = Math.Min(maxXY.X, width - 1);
            maxXY.Y = Math.Min(maxXY.Y, height - 1);

            var quad = MapQuadToQuad(corners, new IntPoint[]
            {
                new IntPoint(0, 0),
                new IntPoint(99, 0),
                new IntPoint(99, 99),
                new IntPoint(0, 99)
            });

            var divisor = quad[2, 0] * offsetX + quad[2, 1] * offsetY + quad[2, 2];

            return new double[]
            {
                (quad[0, 0] * offsetX + quad[0, 1] * offsetY + quad[0, 2]) / divisor,
                (quad[1, 0] * offsetX + quad[1, 1] * offsetY + quad[1, 2]) / divisor
            };
        }

        public static double[] GetXY(IList<IntPoint> corners, double offsetX, double offsetY)
        {
            var quad = MapQuadToQuad(new IntPoint[]
            {
                new IntPoint(0, 0),
                new IntPoint(99, 0),
                new IntPoint(99, 99),
                new IntPoint(0, 99)
            }, corners);

            // add center point
            offsetX += 50.0;
            offsetY += 50.0;

            var divisor = quad[2, 0] * offsetX + quad[2, 1] * offsetY + quad[2, 2];

            return new double[]
            {
                (quad[0, 0] * offsetX + quad[0, 1] * offsetY + quad[0, 2]) / divisor,
                (quad[1, 0] * offsetX + quad[1, 1] * offsetY + quad[1, 2]) / divisor
            };
        }      

        private static double Det2(double a, double b, double c, double d) => a * d - b * c;

        private static double[,] MultiplyMatrix(double[,] a, double[,] b)
        {
            var matrix = new double[3, 3];
            matrix[0, 0] = a[0, 0] * b[0, 0] + a[0, 1] * b[1, 0] + a[0, 2] * b[2, 0];
            matrix[0, 1] = a[0, 0] * b[0, 1] + a[0, 1] * b[1, 1] + a[0, 2] * b[2, 1];
            matrix[0, 2] = a[0, 0] * b[0, 2] + a[0, 1] * b[1, 2] + a[0, 2] * b[2, 2];
            matrix[1, 0] = a[1, 0] * b[0, 0] + a[1, 1] * b[1, 0] + a[1, 2] * b[2, 0];
            matrix[1, 1] = a[1, 0] * b[0, 1] + a[1, 1] * b[1, 1] + a[1, 2] * b[2, 1];
            matrix[1, 2] = a[1, 0] * b[0, 2] + a[1, 1] * b[1, 2] + a[1, 2] * b[2, 2];
            matrix[2, 0] = a[2, 0] * b[0, 0] + a[2, 1] * b[1, 0] + a[2, 2] * b[2, 0];
            matrix[2, 1] = a[2, 0] * b[0, 1] + a[2, 1] * b[1, 1] + a[2, 2] * b[2, 1];
            matrix[2, 2] = a[2, 0] * b[0, 2] + a[2, 1] * b[1, 2] + a[2, 2] * b[2, 2];
            return matrix;
        }

        private static double[,] AdjugateMatrix(double[,] a)
        {
            var matrix = new double[3, 3];
            matrix[0, 0] = Det2(a[1, 1], a[1, 2], a[2, 1], a[2, 2]);
            matrix[1, 0] = Det2(a[1, 2], a[1, 0], a[2, 2], a[2, 0]);
            matrix[2, 0] = Det2(a[1, 0], a[1, 1], a[2, 0], a[2, 1]);
            matrix[0, 1] = Det2(a[2, 1], a[2, 2], a[0, 1], a[0, 2]);
            matrix[1, 1] = Det2(a[2, 2], a[2, 0], a[0, 2], a[0, 0]);
            matrix[2, 1] = Det2(a[2, 0], a[2, 1], a[0, 0], a[0, 1]);
            matrix[0, 2] = Det2(a[0, 1], a[0, 2], a[1, 1], a[1, 2]);
            matrix[1, 2] = Det2(a[0, 2], a[0, 0], a[1, 2], a[1, 0]);
            matrix[2, 2] = Det2(a[0, 0], a[0, 1], a[1, 0], a[1, 1]);
            return matrix;
        }

        private static double[,] MapSquareToQuad(IList<IntPoint> quad)
        {
            var result = new double[3, 3];
            var minX = quad[0].X - quad[1].X + quad[2].X - quad[3].X;
            var minY = quad[0].Y - quad[1].Y + quad[2].Y - quad[3].Y;

            if (minX < Tolerance && minX > -Tolerance && minY < Tolerance && minY > -Tolerance)
            {
                result[0, 0] = quad[1].X - quad[0].X;
                result[0, 1] = quad[2].X - quad[1].X;
                result[0, 2] = quad[0].X;
                result[1, 0] = quad[1].Y - quad[0].Y;
                result[1, 1] = quad[2].Y - quad[1].Y;
                result[1, 2] = quad[0].Y;
                result[2, 0] = 0.0;
                result[2, 1] = 0.0;
                result[2, 2] = 1.0;
            }
            else
            {
                var a = quad[1].X - quad[2].X;
                var b = quad[3].X - quad[2].X;
                var c = quad[1].Y - quad[2].Y;
                var d = quad[3].Y - quad[2].Y;
                var det = Det2(a, b, c, d);

                if (det == 0.0)
                    return null;

                result[2, 0] = Det2(minX, b, minY, d) / det;
                result[2, 1] = Det2(a, minX, c, minY) / det;
                result[2, 2] = 1.0;
                result[0, 0] = quad[1].X - quad[0].X + result[2, 0] * quad[1].X;
                result[0, 1] = quad[3].X - quad[0].X + result[2, 1] * quad[3].X;
                result[0, 2] = quad[0].X;
                result[1, 0] = quad[1].Y - quad[0].Y + result[2, 0] * quad[1].Y;
                result[1, 1] = quad[3].Y - quad[0].Y + result[2, 1] * quad[3].Y;
                result[1, 2] = quad[0].Y;
            }

            return result;
        }

        private static double[,] MapQuadToQuad(IList<IntPoint> input, IList<IntPoint> output)
        {
            return MultiplyMatrix(MapSquareToQuad(output), AdjugateMatrix(MapSquareToQuad(input)));
        }

        private static void GetBoundingRectangle(IList<IntPoint> cloud, out IntPoint minXY, out IntPoint maxXY)
        {
            var minX = int.MaxValue;
            var maxX = int.MinValue;
            var minY = int.MaxValue;
            var maxY = int.MinValue;

            for (var i = 0; i < cloud.Count; i++)
            {
                var x = cloud[i].X;
                var y = cloud[i].Y;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }

            if (minX > maxX)
                throw new ArgumentException("List of points can not be empty.");

            minXY = new IntPoint(minX, minY);
            maxXY = new IntPoint(maxX, maxY);
        }
    }
}
