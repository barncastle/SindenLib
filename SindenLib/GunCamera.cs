using SindenLib.Imaging;
using SindenLib.Models;
using SindenLib.Static;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

namespace SindenLib
{
    internal class GunCamera
    {
        public Bitmap FilteredFrame;
        public GunContext Context;

        private readonly BlobCounter BlobCounter;
        private readonly SimpleShapeChecker SimpleShapeChecker;
        private readonly CircularBuffer<(double X, double Y)> FrameBuffer;
        private readonly Color MinBrightness = Color.FromArgb(64, 64, 64);

        private bool GotNewLastReadings;
        private Handedness FrameHandiness;
        private readonly double[] LastFrameOffsets;
        private Rectangle LastFrame;

        private int LastWidthFound;
        private int LastHeightFound;
        private int LastLeftSideFound;
        private int LastTopSideFound;
        private double FilterRadiusSqr;

        public GunCamera(GunContext context)
        {
            Context = context;

            LastFrame = new Rectangle();
            FrameBuffer = new CircularBuffer<(double X, double Y)>(5, (1000.0, 1000.0));
            SimpleShapeChecker = new SimpleShapeChecker();
            BlobCounter = new BlobCounter()
            {
                FilterBlobs = true,
                MinHeight = 15,
                MinWidth = 15,
                CoupledSizeFiltering = true
            };
        }

        public unsafe void ProcessFrame(Bitmap frame)
        {
            if (!GotNewLastReadings)
            {
                LastFrame.Width = frame.Width;
                LastFrame.Height = frame.Height;
                LastFrame.X = 0; // LastLeftSideFound
                LastFrame.Y = 0; // LastTopSideFound

                LastWidthFound = frame.Width; // width2
                LastHeightFound = frame.Height; // height2
                LastLeftSideFound = 0; // x
                LastTopSideFound = 0; // y
            }

            FilterRadiusSqr = Context.VideoSettings.FilterRadius * Context.VideoSettings.FilterRadius;

            var greyFrame = new Bitmap(LastWidthFound / 2, LastHeightFound / 2, PixelFormat.Format8bppIndexed);
            var frameData = frame.LockBits(new Rectangle(LastLeftSideFound, LastTopSideFound, LastWidthFound, LastHeightFound), ImageLockMode.ReadOnly, frame.PixelFormat);
            var greyData = greyFrame.LockBits(new Rectangle(0, 0, greyFrame.Width, greyFrame.Height), ImageLockMode.ReadWrite, greyFrame.PixelFormat);

            FilterImage(frameData, greyData);
            BlobCounter.MinHeight = frameData.Width > 600 ? 30 : 15;
            BlobCounter.MinWidth = BlobCounter.MinHeight;
            BlobCounter.ProcessImage(greyData);
            greyFrame.UnlockBits(greyData);

            var camWidth = frame.Width / 2;
            var camHeight = frame.Height / 2;
            var blobsEdgePoints = GetEdgePoints(camWidth, camHeight);

            if (SimpleShapeChecker.IsConvexPolygon(blobsEdgePoints, out var points) && points.Count == 4)
            {
                var centreX2 = camWidth + Context.DeviceInfo.CalibrationX / 100.0 * (camWidth * 2);
                var centreY2 = camHeight + Context.DeviceInfo.CalibrationY / 100.0 * (camHeight * 2);

                for (var i = 0; i < points.Count; i++)
                    points[i] = new IntPoint(points[i].X * 2, points[i].Y * 2);

                var frameSrc = (byte*)frameData.Scan0.ToPointer();
                var corners = SortCorners(points);
                var pixelMap = new bool[2, 2];

                for (var i = 0; i < 4; i++)
                {
                    pixelMap[0, 0] = CheckPixel(corners[i].X, corners[i].Y, frameData.Stride, frameSrc);
                    pixelMap[1, 0] = CheckPixel(corners[i].X + 1, corners[i].Y, frameData.Stride, frameSrc);
                    pixelMap[0, 1] = CheckPixel(corners[i].X, corners[i].Y + 1, frameData.Stride, frameSrc);
                    pixelMap[1, 1] = CheckPixel(corners[i].X + 1, corners[i].Y + 1, frameData.Stride, frameSrc);

                    switch (i)
                    {
                        case 0:
                            {
                                if (pixelMap[0, 0] || (pixelMap[1, 0] && pixelMap[0, 1]))
                                    continue;
                                else if (pixelMap[1, 0])
                                    corners[0] = new IntPoint(corners[0].X + 1, corners[0].Y);
                                else if (pixelMap[0, 1])
                                    corners[0] = new IntPoint(corners[0].X, corners[0].Y + 1);
                                else
                                    corners[0] = new IntPoint(corners[0].X + 1, corners[0].Y + 1);
                            }
                            break;
                        case 1:
                            {
                                if (pixelMap[1, 0])
                                    corners[1] = new IntPoint(corners[1].X + 1, corners[1].Y);
                                else if (pixelMap[0, 0] && pixelMap[1, 1])
                                    corners[1] = new IntPoint(corners[1].X + 1, corners[1].Y);
                                else if (pixelMap[1, 1] && (pixelMap[0, 0] || pixelMap[1, 1]))
                                    corners[1] = new IntPoint(corners[1].X + 1, corners[1].Y + 1);
                                else if (!pixelMap[1, 1])
                                    corners[1] = new IntPoint(corners[1].X, corners[1].Y + 1);
                            }
                            break;
                        case 2:
                            {
                                if (pixelMap[1, 1])
                                    corners[2] = new IntPoint(corners[2].X + 1, corners[2].Y + 1);
                                else if (pixelMap[1, 0] && pixelMap[0, 1])
                                    corners[2] = new IntPoint(corners[2].X + 1, corners[2].Y + 1);
                                else if (pixelMap[1, 0])
                                    corners[2] = new IntPoint(corners[2].X + 1, corners[2].Y);
                                else if (pixelMap[0, 1])
                                    corners[2] = new IntPoint(corners[2].X, corners[2].Y + 1);
                            }
                            break;
                        case 3:
                            {
                                if (pixelMap[0, 1])
                                    corners[3] = new IntPoint(corners[3].X, corners[3].Y + 1);
                                else if (pixelMap[0, 0] && pixelMap[1, 1])
                                    corners[3] = new IntPoint(corners[3].X, corners[3].Y + 1);
                                else if (pixelMap[0, 0])
                                    corners[3] = new IntPoint(corners[3].X, corners[3].Y);
                                else if (pixelMap[1, 1])
                                    corners[3] = new IntPoint(corners[3].X + 1, corners[3].Y + 1);
                                else
                                    corners[3] = new IntPoint(corners[3].X + 1, corners[3].Y);
                            }
                            break;
                    }
                }

                for (var i = 0; GotNewLastReadings && i < corners.Length; i++)
                    corners[i] = new IntPoint(corners[i].X + 2 * LastLeftSideFound, corners[i].Y + 2 * LastTopSideFound);

                var xypercentagesMatrix = GetXYPercentagesMatrix(corners, centreX2, centreY2, camWidth, camHeight);
                var xPercentage = xypercentagesMatrix[0];
                var yPercentage = xypercentagesMatrix[1];
                var quadCentre = WorkOutCentreOfQuad(corners, 0.0, Context.VideoSettings.YSightOffset);

                Context.DeviceInfo.CalibrationX = -1.0 * ((camWidth - quadCentre[0]) / (camWidth * 2)) * 100.0;
                Context.DeviceInfo.CalibrationY = -1.0 * ((camHeight - quadCentre[1]) / (camHeight * 2)) * 100.0;

                if (xPercentage > -50.0 & xPercentage < 150.0 &&
                    yPercentage > -50.0 & yPercentage < 150.0)
                {
                    var processFrame = !Context.VideoSettings.UseAntiJitter;

                    if (Context.VideoSettings.UseAntiJitter)
                    {
                        foreach (var (X, Y) in FrameBuffer)
                        {
                            if (X - xPercentage > Context.VideoSettings.JitterMoveThreshold)
                                processFrame = true;
                            if (Y - yPercentage > Context.VideoSettings.JitterMoveThreshold)
                                processFrame = true;
                            if (processFrame)
                                break;
                        }
                    }

                    if (processFrame)
                    {
                        var xscalar = (short)(xPercentage / 100D * 32767D);
                        var yscalar = (short)(yPercentage / 100D * 32767D);
                        Context.SetCursorOffset(xscalar, yscalar);

                        FrameBuffer.Add((xPercentage, yPercentage));

                        LastLeftSideFound = camWidth;
                        LastTopSideFound = camHeight;

                        for (var i = 0; i < corners.Length; i++)
                            corners[i] = new IntPoint(corners[i].X / 2, corners[i].Y / 2);

                        var LastRightSideFound = 0;
                        var LastBottomSideFound = 0;
                        for (var i = 0; i < corners.Length; i++)
                        {
                            if (corners[i].X < LastLeftSideFound)
                                LastLeftSideFound = corners[i].X;
                            if (corners[i].Y < LastTopSideFound)
                                LastTopSideFound = corners[i].Y;
                            if (corners[i].X > LastRightSideFound)
                                LastRightSideFound = corners[i].X;
                            if (corners[i].Y > LastBottomSideFound)
                                LastBottomSideFound = corners[i].Y;
                        }

                        LastWidthFound = LastRightSideFound - LastLeftSideFound;
                        LastHeightFound = LastBottomSideFound - LastTopSideFound;
                        LastLeftSideFound -= (short)(LastWidthFound * 0.15);
                        LastTopSideFound -= (short)(LastHeightFound * 0.15);
                        LastWidthFound = (short)(LastWidthFound * 1.3);
                        LastHeightFound = (short)(LastHeightFound * 1.3);

                        if (LastLeftSideFound < 0)
                            LastLeftSideFound = 0;
                        if (LastTopSideFound < 0)
                            LastTopSideFound = 0;

                        GotNewLastReadings =
                            LastWidthFound + LastLeftSideFound <= camWidth &&
                            LastHeightFound + LastTopSideFound <= camHeight &&
                            LastWidthFound >= camWidth / 8 &&
                            LastHeightFound >= camHeight / 8;
                    }
                }
            }

            FilteredFrame = greyFrame;
        }

        private List<IntPoint> GetEdgePoints(int camWidth, int camHeight)
        {
            var distance = 0.0;
            int target = 0;

            var objectsInformation = BlobCounter.GetObjectsInformation();

            for (var i = 0; i < objectsInformation.Length; i++)
            {
                var blobsEdgePoints = BlobCounter.GetBlobsEdgePoints(objectsInformation[i]);

                if (SimpleShapeChecker.IsConvexPolygon(blobsEdgePoints, out var corners) && corners.Count == 4)
                {
                    var valid = true;
                    if (Context.VideoSettings.OnlyMatchWherePointing)
                    {
                        var centreX = camWidth / 2 + Convert.ToDouble(Context.DeviceInfo.CalibrationX) / 100.0 * camWidth;
                        var centreY = camHeight / 2 + Convert.ToDouble(Context.DeviceInfo.CalibrationY) / 100.0 * camHeight;

                        for (var j = 0; GotNewLastReadings && j < corners.Count; j++)
                            corners[j] = new IntPoint(corners[j].X + LastLeftSideFound, corners[j].Y + LastTopSideFound);

                        var xypercentagesMatrix = GetXYPercentagesMatrix(corners, centreX, centreY, camWidth, camHeight);

                        valid = xypercentagesMatrix[0] >= 0.0 &&
                            xypercentagesMatrix[0] <= 100.0 &&
                            xypercentagesMatrix[1] >= 0.0 + Context.VideoSettings.YSightOffset &&
                            xypercentagesMatrix[1] <= 100.0 + Context.VideoSettings.YSightOffset;
                    }

                    if (valid)
                    {
                        var minX = int.MaxValue;
                        var maxX = int.MinValue;
                        var minY = int.MaxValue;
                        var maxY = int.MinValue;

                        for (var j = 0; j < 4; j++)
                        {
                            if (corners[j].X < minX)
                                minX = corners[j].X;
                            if (corners[j].X > maxX)
                                maxX = corners[j].X;
                            if (corners[j].Y < minY)
                                minY = corners[j].Y;
                            if (corners[j].Y > maxY)
                                maxY = corners[j].Y;
                        }

                        var dist = Math.Abs((maxX - minX) * (maxY - minY));
                        if (dist > distance)
                        {
                            distance = dist;
                            target = i;
                        }
                    }
                }
            }

            return BlobCounter.GetBlobsEdgePoints(objectsInformation[target]);
        }

        private double[] GetXYPercentagesMatrix(IList<IntPoint> corners, double centreX, double centreY, int width, int height)
        {
            var points = SortCorners(corners);
            var newpoints = new IntPoint[4];

            if (points[0].DistanceTo(points[1]) > points[0].DistanceTo(points[2])) // frame is same handiness as previous
            {
                FrameHandiness = Handedness.None;
            }
            else if (Context.VideoSettings.Handedness != Handedness.Auto) // handiness is user defined
            {
                FrameHandiness = Context.VideoSettings.Handedness;
            }
            else // handiness is automatically calculated
            {
                var useRightHand = true; // default to right hand

                // recalculate frame handiness if not calculated previously and cursor is was on screen
                if (FrameHandiness == Handedness.None)
                {
                    if (LastFrameOffsets[0] > 0.0 && LastFrameOffsets[0] < 100.0 && LastFrameOffsets[1] > 0.0 && LastFrameOffsets[1] < 100.0)
                    {
                        newpoints[0] = points[1];
                        newpoints[1] = points[3];
                        newpoints[3] = points[2];
                        newpoints[2] = points[0];
                        var xybackRH = QuadTransforms.GetXYBack(newpoints, centreX, centreY, width, height);

                        newpoints[0] = points[2];
                        newpoints[1] = points[0];
                        newpoints[3] = points[1];
                        newpoints[2] = points[3];
                        var xybackLH = QuadTransforms.GetXYBack(newpoints, centreX, centreY, width, height);

                        if (xybackRH[0] > 0.0 && xybackRH[0] < 100.0 && xybackRH[1] > 0.0 && xybackRH[1] < 100.0)
                        {
                            if (LastFrameOffsets[0] <= 48.0 || LastFrameOffsets[0] >= 52.0)
                                useRightHand = Math.Abs(LastFrameOffsets[0] - xybackRH[0]) <= Math.Abs(LastFrameOffsets[0] - xybackLH[0]);
                            else if (LastFrameOffsets[1] <= 48.0 || LastFrameOffsets[1] >= 52.0)
                                useRightHand = Math.Abs(LastFrameOffsets[1] - xybackRH[1]) <= Math.Abs(LastFrameOffsets[1] - xybackLH[1]);
                        }
                    }
                }

                FrameHandiness = useRightHand ? Handedness.Right : Handedness.Left;
            }

            switch (FrameHandiness)
            {
                case Handedness.None:
                    newpoints[0] = points[0];
                    newpoints[1] = points[1];
                    newpoints[2] = points[3];
                    newpoints[3] = points[2];
                    break;
                case Handedness.Left:
                    newpoints[0] = points[2];
                    newpoints[1] = points[0];
                    newpoints[3] = points[1];
                    newpoints[2] = points[3];
                    break;
                case Handedness.Right:
                    newpoints[0] = points[1];
                    newpoints[1] = points[3];
                    newpoints[3] = points[2];
                    newpoints[2] = points[0];
                    break;
            }

            var matrix = QuadTransforms.GetXYBack(newpoints, centreX, centreY, width, height);

            if (FrameHandiness == Handedness.None)
            {
                LastFrameOffsets[0] = matrix[0];
                LastFrameOffsets[1] = matrix[1];
            }

            return matrix;
        }

        private double[] WorkOutCentreOfQuad(IList<IntPoint> corners, double offsetX, double offsetY)
        {
            var points = new IntPoint[4];
            var sorted = SortCorners(corners);

            for (var i = 0; i < 2; i++)
            {
                if (sorted[i].Y < sorted[i + 1].Y)
                {
                    points[i + 0].X = sorted[i * 2 + 0].X;
                    points[i + 0].Y = sorted[i * 2 + 0].Y;
                    points[i + 2].X = sorted[i * 2 + 1].X;
                    points[i + 2].Y = sorted[i * 2 + 1].Y;
                }
                else
                {
                    points[i + 0].X = sorted[i * 2 + 1].X;
                    points[i + 0].Y = sorted[i * 2 + 1].Y;
                    points[i + 2].X = sorted[i * 2 + 0].X;
                    points[i + 2].Y = sorted[i * 2 + 0].Y;
                }
            }

            if (points[0].DistanceTo(points[1]) > points[0].DistanceTo(points[2]) || FrameHandiness == Handedness.None)
            {
                (points[0], points[1], points[2], points[3]) = (points[0], points[1], points[3], points[2]);
            }
            else if (FrameHandiness == Handedness.Right)
            {
                (points[0], points[1], points[2], points[3]) = (points[1], points[3], points[2], points[0]);
            }
            else
            {
                (points[0], points[1], points[2], points[3]) = (points[2], points[0], points[1], points[3]);
            }

            return QuadTransforms.GetXY(points, offsetX, offsetY);
        }

        private unsafe bool CheckPixel(int x, int y, int stride, byte* src)
        {
            var ptr = src + y * stride + x * 3;
            var r = Context.VideoSettings.BorderColour.R - ptr[2];
            var g = Context.VideoSettings.BorderColour.G - ptr[1];
            var b = Context.VideoSettings.BorderColour.B - ptr[0];

            return (ptr[0] > MinBrightness.B || ptr[1] > MinBrightness.G || ptr[2] > MinBrightness.R) &&
                r * r + g * g + b * b <= FilterRadiusSqr;
        }

        private unsafe void FilterImage(BitmapData frameData, BitmapData greyData)
        {
            var framePtr = (byte*)frameData.Scan0.ToPointer();
            var greyPtr = (byte*)greyData.Scan0.ToPointer();
            var frameStride = frameData.Stride;
            var greyStride = greyData.Stride;

            int xOffset, yOffset;
            for (var i = 0; i < greyData.Height; i++)
            {
                xOffset = 2 * i;

                var ptr1 = framePtr + 2 * i * frameStride;
                var ptr2 = framePtr + (2 * i + 1) * frameStride;
                var outPtr = greyPtr + i * greyStride;

                for (var j = 0; j < greyData.Width; j++)
                {
                    yOffset = j * 2;

                    if (CheckPixel(xOffset, yOffset * 3, frameStride, framePtr))
                        outPtr[j] = byte.MaxValue;
                    else if (CheckPixel(xOffset, (yOffset + 1) * 3, frameStride, framePtr))
                        outPtr[j] = byte.MaxValue;
                    else if (CheckPixel(xOffset + 1, yOffset * 3, frameStride, framePtr))
                        outPtr[j] = byte.MaxValue;
                    else if (CheckPixel(xOffset + 1, (yOffset + 1) * 3, frameStride, framePtr))
                        outPtr[j] = byte.MaxValue;
                }
            }
        }

        private IntPoint[] SortCorners(IList<IntPoint> corners)
        {
            var points = new IntPoint[4];
            var temp = new IntPoint[4];

            // sort corners
            corners.CopyTo(temp, 0);
            Array.Sort(temp, (a, b) => a.X.CompareTo(b.X));

            for (var i = 0; i < 2; i++)
            {
                if (temp[i].Y < temp[i + 1].Y)
                {
                    points[i + 0] = temp[i * 2 + 0];
                    points[i + 2] = temp[i * 2 + 1];
                }
                else
                {
                    points[i + 0] = temp[i * 2 + 1];
                    points[i + 2] = temp[i * 2 + 0];
                }
            }

            return points;
        }
    }
}
