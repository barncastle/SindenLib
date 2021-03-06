using System;
using System.Collections.Generic;

namespace SindenLib.Imaging
{
    /// <summary>
    /// Set of tools for processing collection of points in 2D space.
    /// </summary>
    /// 
    /// <remarks><para>The static class contains set of routines, which provide different
    /// operations with collection of points in 2D space. For example, finding the
    /// furthest point from a specified point or line.</para>
    /// 
    /// <para>Sample usage:</para>
    /// <code>
    /// // create points' list
    /// List&lt;IntPoint&gt; points = new List&lt;IntPoint&gt;( );
    /// points.Add( new IntPoint( 10, 10 ) );
    /// points.Add( new IntPoint( 20, 15 ) );
    /// points.Add( new IntPoint( 15, 30 ) );
    /// points.Add( new IntPoint( 40, 12 ) );
    /// points.Add( new IntPoint( 30, 20 ) );
    /// // get furthest point from the specified point
    /// IntPoint p1 = PointsCloud.GetFurthestPoint( points, new IntPoint( 15, 15 ) );
    /// Console.WriteLine( p1.X + ", " + p1.Y );
    /// // get furthest point from line
    /// IntPoint p2 = PointsCloud.GetFurthestPointFromLine( points,
    ///     new IntPoint( 50, 0 ), new IntPoint( 0, 50 ) );
    /// Console.WriteLine( p2.X + ", " + p2.Y );
    /// </code>
    /// </remarks>
    /// 
    internal static class PointsCloud
    {
        /// <summary>
        /// Get bounding rectangle of the specified list of points.
        /// </summary>
        /// 
        /// <param name="cloud">Collection of points to get bounding rectangle for.</param>
        /// <param name="minXY">Point comprised of smallest X and Y coordinates.</param>
        /// <param name="maxXY">Point comprised of biggest X and Y coordinates.</param>
        /// 
        public static void GetBoundingRectangle(IEnumerable<IntPoint> cloud, out IntPoint minXY, out IntPoint maxXY)
        {
            var minX = int.MaxValue;
            var maxX = int.MinValue;
            var minY = int.MaxValue;
            var maxY = int.MinValue;

            foreach (var pt in cloud)
            {
                var x = pt.X;
                var y = pt.Y;

                // check X coordinate
                if (x < minX)
                    minX = x;
                if (x > maxX)
                    maxX = x;

                // check Y coordinate
                if (y < minY)
                    minY = y;
                if (y > maxY)
                    maxY = y;
            }

            if (minX > maxX) // if no point appeared to set either minX or maxX
                throw new ArgumentException("List of points can not be empty.");

            minXY = new IntPoint(minX, minY);
            maxXY = new IntPoint(maxX, maxY);
        }

        /// <summary>
        /// Find furthest point from the specified point.
        /// </summary>
        /// 
        /// <param name="cloud">Collection of points to search furthest point in.</param>
        /// <param name="referencePoint">The point to search furthest point from.</param>
        /// 
        /// <returns>Returns a point, which is the furthest away from the <paramref name="referencePoint"/>.</returns>
        /// 
        public static IntPoint GetFurthestPoint(IEnumerable<IntPoint> cloud, IntPoint referencePoint)
        {
            var furthestPoint = referencePoint;
            var maxDistance = -1;

            var rx = referencePoint.X;
            var ry = referencePoint.Y;

            foreach (var point in cloud)
            {
                var dx = rx - point.X;
                var dy = ry - point.Y;
                var distance = dx * dx + dy * dy;

                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    furthestPoint = point;
                }
            }

            return furthestPoint;
        }

        /// <summary>
        /// Find two furthest points from the specified line.
        /// </summary>
        /// 
        /// <param name="cloud">Collection of points to search furthest points in.</param>
        /// <param name="linePoint1">First point forming the line.</param>
        /// <param name="linePoint2">Second point forming the line.</param>
        /// <param name="furthestPoint1">First found furthest point.</param>
        /// <param name="distance1">Distance between the first found point and the given line.</param>
        /// <param name="furthestPoint2">Second found furthest point (which is on the
        /// opposite side from the line compared to the <paramref name="furthestPoint1"/>);</param>
        /// <param name="distance2">Distance between the second found point and the given line.</param>
        /// 
        /// <remarks><para>The method finds two furthest points from the specified line,
        /// where one point is on one side from the line and the second point is on
        /// another side from the line.</para></remarks>
        ///
        public static void GetFurthestPointsFromLine(IEnumerable<IntPoint> cloud, IntPoint linePoint1, IntPoint linePoint2,
            out IntPoint furthestPoint1, out float distance1, out IntPoint furthestPoint2, out float distance2)
        {
            furthestPoint1 = linePoint1;
            distance1 = 0;

            furthestPoint2 = linePoint2;
            distance2 = 0;

            if (linePoint2.X != linePoint1.X)
            {
                // line's equation y(x) = k * x + b
                var k = (float)(linePoint2.Y - linePoint1.Y) / (linePoint2.X - linePoint1.X);
                var b = linePoint1.Y - k * linePoint1.X;
                var div = (float)Math.Sqrt(k * k + 1);

                foreach (var point in cloud)
                {
                    var distance = (k * point.X + b - point.Y) / div;
                    if (distance > distance1)
                    {
                        distance1 = distance;
                        furthestPoint1 = point;
                    }
                    if (distance < distance2)
                    {
                        distance2 = distance;
                        furthestPoint2 = point;
                    }
                }
            }
            else
            {
                var lineX = linePoint1.X;
                foreach (var point in cloud)
                {
                    var distance = lineX - point.X;
                    if (distance > distance1)
                    {
                        distance1 = distance;
                        furthestPoint1 = point;
                    }
                    if (distance < distance2)
                    {
                        distance2 = distance;
                        furthestPoint2 = point;
                    }
                }
            }

            distance2 = -distance2;
        }

        /// <summary>
        /// Find corners of quadrilateral or triangular area, which contains the specified collection of points.
        /// </summary>
        /// 
        /// <param name="cloud">Collection of points to search quadrilateral for.</param>
        /// 
        /// <returns>Returns a list of 3 or 4 points, which are corners of the quadrilateral or
        /// triangular area filled by specified collection of point. The first point in the list
        /// is the point with lowest X coordinate (and with lowest Y if there are several points
        /// with the same X value). The corners are provided in counter clockwise order
        /// (<a href="http://en.wikipedia.org/wiki/Cartesian_coordinate_system">Cartesian
        /// coordinate system</a>).</returns>
        /// 
        /// <remarks><para>The method makes an assumption that the specified collection of points
        /// form some sort of quadrilateral/triangular area. With this assumption it tries to find corners
        /// of the area.</para>
        /// 
        /// <para><note>The method does not search for <b>bounding</b> quadrilateral/triangular area,
        /// where all specified points are <b>inside</b> of the found quadrilateral/triangle. Some of the
        /// specified points potentially may be outside of the found quadrilateral/triangle, since the
        /// method takes corners only from the specified collection of points, but does not calculate such
        /// to form true bounding quadrilateral/triangle.</note></para>
        /// 
        /// <para>See QuadrilateralRelativeDistortionLimit property for additional information.</para>
        /// </remarks>
        /// 
        public static List<IntPoint> FindQuadrilateralCorners(IEnumerable<IntPoint> cloud)
        {
            // quadrilateral's corners
            var corners = new List<IntPoint>();

            // get bounding rectangle of the points list
            GetBoundingRectangle(cloud, out var minXY, out var maxXY);

            var cloudSize = maxXY - minXY; // get cloud's size
            var center = minXY + cloudSize / 2; // calculate center point
            var distortionLimit = 0.1f * (cloudSize.X + cloudSize.Y) / 2; // acceptable deviation limit
            var point1 = GetFurthestPoint(cloud, center); // get the furthest point from (0,0)            
            var point2 = GetFurthestPoint(cloud, point1); // get the furthest point from the first point

            corners.Add(point1);
            corners.Add(point2);

            // get two furthest points from line

            GetFurthestPointsFromLine(cloud, point1, point2, out var point3, out float distance3, out var point4, out float distance4);

            // ideally points 1 and 2 form a diagonal of the
            // quadrilateral area, and points 3 and 4 form another diagonal

            // but if one of the points (3 or 4) is very close to the line
            // connecting points 1 and 2, then it is one the same line ...
            // which means corner was not found.
            // in this case we deal with a trapezoid or triangle, where
            // (1-2) line is one of it sides.

            // another interesting case is when both points (3) and (4) are
            // very close the (1-2) line. in this case we may have just a flat
            // quadrilateral.

            if ((distance3 >= distortionLimit && distance4 >= distortionLimit) ||
                (distance3 < distortionLimit && distance3 != 0 && distance4 < distortionLimit && distance4 != 0))
            {
                // don't add one of the corners, if the point is already in the corners list
                // (this may happen when both #3 and #4 points are very close to the line
                // connecting #1 and #2)
                if (!corners.Contains(point3))
                    corners.Add(point3);
                if (!corners.Contains(point4))
                    corners.Add(point4);
            }
            else
            {
                // it seems that we deal with kind of trapezoid,
                // where point 1 and 2 are on the same edge

                var tempPoint = (distance3 > distance4) ? point3 : point4;

                // try to find 3rd point
                GetFurthestPointsFromLine(cloud, point1, tempPoint, out point3, out distance3, out point4, out distance4);

                var thirdPointIsFound = false;
                if (distance3 >= distortionLimit && distance4 >= distortionLimit)
                {
                    if (point4.DistanceTo(point2) > point3.DistanceTo(point2))
                        point3 = point4;

                    thirdPointIsFound = true;
                }
                else
                {
                    GetFurthestPointsFromLine(cloud, point2, tempPoint, out point3, out distance3, out point4, out distance4);

                    if (distance3 >= distortionLimit && distance4 >= distortionLimit)
                    {
                        if (point4.DistanceTo(point1) > point3.DistanceTo(point1))
                            point3 = point4;

                        thirdPointIsFound = true;
                    }
                }

                if (!thirdPointIsFound)
                {
                    // failed to find 3rd edge point, which is away enough from the temp point.
                    // this means that the clound looks more like triangle
                    corners.Add(tempPoint);
                }
                else
                {
                    corners.Add(point3);

                    // try to find 4th point
                    GetFurthestPointsFromLine(cloud, point1, point3, out tempPoint, out float tempDistance, out point4, out distance4);

                    if ((distance4 >= distortionLimit) && (tempDistance >= distortionLimit))
                    {
                        if (tempPoint.DistanceTo(point2) > point4.DistanceTo(point2))
                            point4 = tempPoint;
                    }
                    else
                    {
                        GetFurthestPointsFromLine(cloud, point2, point3, out tempPoint, out _, out point4, out _);

                        if ((tempPoint.DistanceTo(point1) > point4.DistanceTo(point1)) &&
                            (tempPoint != point2) && (tempPoint != point3))
                            point4 = tempPoint;

                    }

                    if ((point4 != point1) && (point4 != point2) && (point4 != point3))
                        corners.Add(point4);
                }
            }

            // put the point with lowest X as the first
            for (int i = 1, n = corners.Count; i < n; i++)
            {
                if (corners[i].X < corners[0].X ||
                    (corners[i].X == corners[0].X && corners[i].Y < corners[0].Y))
                {
                    var temp = corners[i];
                    corners[i] = corners[0];
                    corners[0] = temp;
                }
            }


            // sort other points in counter clockwise order
            var k1 = (corners[1].X != corners[0].X) ?
                ((float)(corners[1].Y - corners[0].Y) / (corners[1].X - corners[0].X)) :
                ((corners[1].Y > corners[0].Y) ? float.PositiveInfinity : float.NegativeInfinity);

            var k2 = (corners[2].X != corners[0].X) ?
                ((float)(corners[2].Y - corners[0].Y) / (corners[2].X - corners[0].X)) :
                ((corners[2].Y > corners[0].Y) ? float.PositiveInfinity : float.NegativeInfinity);

            if (k2 < k1)
            {
                var temp = corners[1];
                corners[1] = corners[2];
                corners[2] = temp;

                var tk = k1;
                k1 = k2;
                k2 = tk;
            }

            if (corners.Count == 4)
            {
                var k3 = (corners[3].X != corners[0].X) ?
                    ((float)(corners[3].Y - corners[0].Y) / (corners[3].X - corners[0].X)) :
                    ((corners[3].Y > corners[0].Y) ? float.PositiveInfinity : float.NegativeInfinity);

                if (k3 < k1)
                {
                    var temp = corners[1];
                    corners[1] = corners[3];
                    corners[3] = temp;

                    var tk = k1;
                    k3 = tk;
                }
                if (k3 < k2)
                {
                    var temp = corners[2];
                    corners[2] = corners[3];
                    corners[3] = temp;
                }
            }

            return corners;
        }
    }
}
