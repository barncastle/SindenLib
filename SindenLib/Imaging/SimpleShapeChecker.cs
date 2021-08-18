using System;
using System.Collections.Generic;

namespace SindenLib.Imaging
{
    internal class SimpleShapeChecker
    {
        private const float MaxAngleToKeep = 160;
        private const float MinAcceptableDistortion = 0.5f;
        private const float RelativeDistortionLimit = 0.03f;

        public bool IsConvexPolygon(List<IntPoint> edgePoints, out List<IntPoint> corners)
        {
            corners = OptimizeShape(PointsCloud.FindQuadrilateralCorners(edgePoints));
            return CheckIfPointsFitShape(edgePoints, corners);
        }

        public bool CheckIfPointsFitShape(List<IntPoint> edgePoints, List<IntPoint> corners)
        {
            var cornersCount = corners.Count;

            // lines coefficients (for representation as y(x)=k*x+b)
            var k = new float[cornersCount];
            var b = new float[cornersCount];
            var div = new float[cornersCount]; // precalculated divisor
            var isVert = new bool[cornersCount];

            for (var i = 0; i < cornersCount; i++)
            {
                var currentPoint = corners[i];
                var nextPoint = (i + 1 == cornersCount) ? corners[0] : corners[i + 1];

                if (!(isVert[i] = nextPoint.X == currentPoint.X))
                {
                    k[i] = (float)(nextPoint.Y - currentPoint.Y) / (nextPoint.X - currentPoint.X);
                    b[i] = currentPoint.Y - k[i] * currentPoint.X;
                    div[i] = (float)Math.Sqrt(k[i] * k[i] + 1);
                }
            }

            // calculate distances between edge points and polygon sides
            var meanDistance = 0f;
            for (int i = 0, n = edgePoints.Count; i < n; i++)
            {
                var minDistance = float.MaxValue;

                for (var j = 0; j < cornersCount; j++)
                {
                    float distance;
                    if (!isVert[j])
                        distance = (float)Math.Abs((k[j] * edgePoints[i].X + b[j] - edgePoints[i].Y) / div[j]);
                    else
                        distance = Math.Abs(edgePoints[i].X - corners[j].X);

                    if (distance < minDistance)
                        minDistance = distance;
                }

                meanDistance += minDistance;
            }

            meanDistance /= edgePoints.Count;

            // get bounding rectangle of the corners list
            PointsCloud.GetBoundingRectangle(corners, out IntPoint minXY, out IntPoint maxXY);

            var rectSize = maxXY - minXY;
            var maxDistance = Math.Max(MinAcceptableDistortion, (rectSize.X + rectSize.Y) / 2 * RelativeDistortionLimit);

            return meanDistance <= maxDistance;
        }

        private List<IntPoint> OptimizeShape(List<IntPoint> shape)
        {
            if (shape.Count <= 3)
                return shape;

            // optimized shape
            // add first 2 points to the new shape
            var pointsInOptimizedHull = 2;
            var optimizedShape = new List<IntPoint>
            {
                shape[0],
                shape[1]
            };

            float angle;
            for (int i = 2, n = shape.Count; i < n; i++)
            {
                // add new point
                optimizedShape.Add(shape[i]);
                pointsInOptimizedHull++;

                // get angle between 2 vectors, which start from the next to last point
                angle = GetAngleBetweenVectors(
                    optimizedShape[pointsInOptimizedHull - 2],
                    optimizedShape[pointsInOptimizedHull - 3],
                    optimizedShape[pointsInOptimizedHull - 1]);

                if (angle > MaxAngleToKeep && (pointsInOptimizedHull > 3 || i < n - 1))
                {
                    // remove the next to last point
                    optimizedShape.RemoveAt(pointsInOptimizedHull - 2);
                    pointsInOptimizedHull--;
                }
            }

            if (pointsInOptimizedHull > 3)
            {
                // check the last point
                angle = GetAngleBetweenVectors(
                    optimizedShape[pointsInOptimizedHull - 1],
                    optimizedShape[pointsInOptimizedHull - 2],
                    optimizedShape[0]);

                if (angle > MaxAngleToKeep)
                {
                    optimizedShape.RemoveAt(pointsInOptimizedHull - 1);
                    pointsInOptimizedHull--;
                }

                if (pointsInOptimizedHull > 3)
                {
                    // check the first point
                    angle = GetAngleBetweenVectors(
                        optimizedShape[0],
                        optimizedShape[pointsInOptimizedHull - 1],
                        optimizedShape[1]);

                    if (angle > MaxAngleToKeep)
                        optimizedShape.RemoveAt(0);
                }
            }

            return optimizedShape;
        }

        private static float GetAngleBetweenVectors(IntPoint startPoint, IntPoint vector1end, IntPoint vector2end)
        {
            var x1 = vector1end.X - startPoint.X;
            var y1 = vector1end.Y - startPoint.Y;
            var x2 = vector2end.X - startPoint.X;
            var y2 = vector2end.Y - startPoint.Y;

            return (float)(Math.Acos((x1 * x2 + y1 * y2) / (Math.Sqrt(x1 * x1 + y1 * y1) * Math.Sqrt(x2 * x2 + y2 * y2))) * 180.0 / Math.PI);
        }
    }
}
