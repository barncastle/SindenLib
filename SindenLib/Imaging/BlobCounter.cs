using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

namespace SindenLib.Imaging
{
    /// <summary>
    /// Base class for different blob counting algorithms.
    /// </summary>
    /// 
    /// <remarks><para>The class is abstract and serves as a base for different blob counting algorithms.
    /// Classes, which inherit from this base class, require to implement BuildObjectsMap
    /// method, which does actual building of object's label's map.</para>
    /// 
    /// <para>For blobs' searcing usually all inherited classes accept binary images, which are actually
    /// grayscale thresholded images. But the exact supported format should be checked in particular class,
    /// inheriting from the base class. For blobs' extraction the class supports grayscale (8 bpp indexed)
    /// and color images (24 and 32 bpp).</para>
    /// 
    /// <para>Sample usage:</para>
    /// <code>
    /// // create an instance of blob counter algorithm
    /// BlobCounterBase bc = new ...
    /// // set filtering options
    /// bc.FilterBlobs = true;
    /// bc.MinWidth  = 5;
    /// bc.MinHeight = 5;
    /// // process binary image
    /// bc.ProcessImage( image );
    /// Blob[] blobs = bc.GetObjects( image, false );
    /// // process blobs
    /// foreach ( Blob blob in blobs )
    /// {
    ///     // ...
    ///     // blob.Rectangle - blob's rectangle
    ///     // blob.Image - blob's image
    /// }
    /// </code>
    /// </remarks>
    /// 
    internal class BlobCounter
    {
        public const short R = 2;
        public const short G = 1;
        public const short B = 0;
        public const short A = 3;

        private const byte ThresholdR = 0;
        private const byte ThresholdG = 0;
        private const byte ThresholdB = 0;

        private readonly List<Blob> Blobs = new List<Blob>();
        private int ImageWidth;
        private int ImageHeight;

        public int ObjectsCount { get; protected set; }
        public int[] ObjectLabels { get; protected set; }
        public bool FilterBlobs { get; set; }
        public bool CoupledSizeFiltering { get; set; }
        public int MinWidth { get; set; } = 1;
        public int MinHeight { get; set; } = 1;
        public int MaxWidth { get; set; } = int.MaxValue;
        public int MaxHeight { get; set; } = int.MaxValue;

        public void ProcessImage(BitmapData image)
        {
            ImageWidth = image.Width;
            ImageHeight = image.Height;

            // do actual objects map building
            BuildObjectsMap(image);

            // collect information about blobs
            CollectObjectsInfo(image);

            // filter blobs by size if required
            if (FilterBlobs)
            {
                // labels remapping array
                var labelsMap = new int[ObjectsCount + 1];
                for (var i = 1; i <= ObjectsCount; i++)
                    labelsMap[i] = i;

                // check dimension of all objects and filter them
                var objectsToRemove = 0;
                for (var i = ObjectsCount - 1; i >= 0; i--)
                {
                    var blobWidth = Blobs[i].Rectangle.Width;
                    var blobHeight = Blobs[i].Rectangle.Height;

                    if (CoupledSizeFiltering == false)
                    {
                        // uncoupled filtering
                        if (blobWidth < MinWidth || blobHeight < MinHeight || blobWidth > MaxWidth || blobHeight > MaxHeight)
                        {
                            labelsMap[i + 1] = 0;
                            objectsToRemove++;
                            Blobs.RemoveAt(i);
                        }
                    }
                    else
                    {
                        // coupled filtering
                        if ((blobWidth < MinWidth && blobHeight < MinHeight) || (blobWidth > MaxWidth && blobHeight > MaxHeight))
                        {
                            labelsMap[i + 1] = 0;
                            objectsToRemove++;
                            Blobs.RemoveAt(i);
                        }
                    }
                }

                // update labels remapping array
                var label = 0;
                for (var i = 1; i <= ObjectsCount; i++)
                {
                    if (labelsMap[i] != 0)
                    {
                        labelsMap[i] = ++label; // update remapping array
                    }
                }

                // repair object labels
                for (int i = 0, n = ObjectLabels.Length; i < n; i++)
                    ObjectLabels[i] = labelsMap[ObjectLabels[i]];

                ObjectsCount -= objectsToRemove;

                // repair IDs
                for (int i = 0, n = Blobs.Count; i < n; i++)
                    Blobs[i].ID = i + 1;
            }
        }

        /// <summary>
        /// Get objects' information.
        /// </summary>
        /// 
        /// <returns>Returns array of partially initialized blobs (without Blob.Image property initialized).</returns>
        /// 
        /// <remarks><para>By the amount of provided information, the method is between GetObjectsRectangles and
        /// GetObjects( UnmanagedImage, bool ) methods. The method provides array of blobs without initialized their image.
        /// Blob's image may be extracted later using ExtractBlobsImage( Bitmap, Blob, bool )
        /// or ExtractBlobsImage( UnmanagedImage, Blob, bool ) method.
        /// </para></remarks>
        /// 
        /// <example>
        /// <code>
        /// // create blob counter and process image
        /// BlobCounter bc = new BlobCounter( sourceImage );
        /// // specify sort order
        /// bc.ObjectsOrder = ObjectsOrder.Size;
        /// // get objects' information (blobs without image)
        /// Blob[] blobs = bc.GetObjectInformation( );
        /// // process blobs
        /// foreach ( Blob blob in blobs )
        /// {
        ///     // check blob's properties
        ///     if ( blob.Rectangle.Width > 50 )
        ///     {
        ///         // the blob looks interesting, let's extract it
        ///         bc.ExtractBlobsImage( sourceImage, blob );
        ///     }
        /// }
        /// </code>
        /// </example>
        /// 
        /// <exception cref="ApplicationException">No image was processed before, so objects' information
        /// can not be collected.</exception>
        /// 
        public Blob[] GetObjectsInformation()
        {
            // check if objects map was collected
            if (ObjectLabels == null)
                throw new ApplicationException("Image should be processed before to collect objects map.");

            var blobsToReturn = new Blob[ObjectsCount];
            for (var k = 0; k < ObjectsCount; k++)
                blobsToReturn[k] = new Blob(Blobs[k]);

            return blobsToReturn;
        }

        /// <summary>
        /// Get list of points on the top and bottom edges of the blob.
        /// </summary>
        /// 
        /// <param name="blob">Blob to collect edge points for.</param>
        /// <param name="topEdge">List of points on the top edge of the blob.</param>
        /// <param name="bottomEdge">List of points on the bottom edge of the blob.</param>
        /// 
        /// <remarks><para>The method scans each column of the blob and finds the most top and the
        /// most bottom points for it adding them to appropriate lists. The method may be very
        /// useful in conjunction with different routines from AForgeCore.Math.Geometry,
        /// which allow finding convex hull or quadrilateral's corners.</para>
        /// 
        /// <para><note>Both lists of points are sorted by X coordinate - points with smaller X
        /// value go first.</note></para>
        /// </remarks>
        /// 
        /// <exception cref="ApplicationException">No image was processed before, so blob
        /// can not be extracted.</exception>
        /// 
        public void GetBlobsTopAndBottomEdges(Blob blob, out List<IntPoint> topEdge, out List<IntPoint> bottomEdge)
        {
            // check if objects map was collected
            if (ObjectLabels == null)
                throw new ApplicationException("Image should be processed before to collect objects map.");

            topEdge = new List<IntPoint>();
            bottomEdge = new List<IntPoint>();

            var xmin = blob.Rectangle.Left;
            var xmax = xmin + blob.Rectangle.Width - 1;
            var ymin = blob.Rectangle.Top;
            var ymax = ymin + blob.Rectangle.Height - 1;
            var label = blob.ID;

            // for each column
            for (var x = xmin; x <= xmax; x++)
            {
                // scan from top to bottom
                var p = ymin * ImageWidth + x;
                for (var y = ymin; y <= ymax; y++, p += ImageWidth)
                {
                    if (ObjectLabels[p] == label)
                    {
                        topEdge.Add(new IntPoint(x, y));
                        break;
                    }
                }

                // scan from bottom to top
                p = ymax * ImageWidth + x;
                for (var y = ymax; y >= ymin; y--, p -= ImageWidth)
                {
                    if (ObjectLabels[p] == label)
                    {
                        bottomEdge.Add(new IntPoint(x, y));
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Get list of object's edge points.
        /// </summary>
        /// 
        /// <param name="blob">Blob to collect edge points for.</param>
        /// 
        /// <returns>Returns unsorted list of blob's edge points.</returns>
        /// 
        /// <remarks><para>The method scans each row and column of the blob and finds the
        /// most top/bottom/left/right points. The method returns similar result as if results of
        /// both GetBlobsLeftAndRightEdges and GetBlobsTopAndBottomEdges
        /// methods were combined, but each edge point occurs only once in the list.</para>
        /// 
        /// <para><note>Edge points in the returned list are not ordered. This makes the list unusable
        /// for visualization with methods, which draw polygon or poly-line. But the returned list
        /// can be used with such algorithms, like convex hull search, shape analyzer, etc.</note></para>
        /// </remarks>
        /// 
        /// <exception cref="ApplicationException">No image was processed before, so blob
        /// can not be extracted.</exception>
        /// 
        public List<IntPoint> GetBlobsEdgePoints(Blob blob)
        {
            // check if objects map was collected
            if (ObjectLabels == null)
                throw new ApplicationException("Image should be processed before to collect objects map.");

            var edgePoints = new List<IntPoint>();
            var xmin = blob.Rectangle.Left;
            var xmax = xmin + blob.Rectangle.Width - 1;
            var ymin = blob.Rectangle.Top;
            var ymax = ymin + blob.Rectangle.Height - 1;
            var label = blob.ID;

            // array of already processed points on left/right edges
            // (index in these arrays represent Y coordinate, but value - X coordinate)
            var leftProcessedPoints = new int[blob.Rectangle.Height];
            var rightProcessedPoints = new int[blob.Rectangle.Height];

            // for each line
            for (var y = ymin; y <= ymax; y++)
            {
                // scan from left to right
                var p = y * ImageWidth + xmin;
                for (var x = xmin; x <= xmax; x++, p++)
                {
                    if (ObjectLabels[p] == label)
                    {
                        edgePoints.Add(new IntPoint(x, y));
                        leftProcessedPoints[y - ymin] = x;
                        break;
                    }
                }

                // scan from right to left
                p = y * ImageWidth + xmax;
                for (var x = xmax; x >= xmin; x--, p--)
                {
                    if (ObjectLabels[p] == label)
                    {
                        // avoid adding the point we already have
                        if (leftProcessedPoints[y - ymin] != x)
                            edgePoints.Add(new IntPoint(x, y));

                        rightProcessedPoints[y - ymin] = x;
                        break;
                    }
                }
            }

            // for each column
            for (var x = xmin; x <= xmax; x++)
            {
                // scan from top to bottom
                var p = ymin * ImageWidth + x;
                for (int y = ymin, y0 = 0; y <= ymax; y++, y0++, p += ImageWidth)
                {
                    if (ObjectLabels[p] == label)
                    {
                        // avoid adding the point we already have
                        if (leftProcessedPoints[y0] != x && rightProcessedPoints[y0] != x)
                            edgePoints.Add(new IntPoint(x, y));

                        break;
                    }
                }

                // scan from bottom to top
                p = ymax * ImageWidth + x;
                for (int y = ymax, y0 = ymax - ymin; y >= ymin; y--, y0--, p -= ImageWidth)
                {
                    if (ObjectLabels[p] == label)
                    {
                        // avoid adding the point we already have
                        if (leftProcessedPoints[y0] != x && rightProcessedPoints[y0] != x)
                            edgePoints.Add(new IntPoint(x, y));

                        break;
                    }
                }
            }

            return edgePoints;
        }

        /// <summary>
        /// Actual objects map building.
        /// </summary>
        /// 
        /// <param name="image">Unmanaged image to process.</param>
        /// 
        /// <remarks><note>By the time this method is called bitmap's pixel format is not
        /// yet checked, so this should be done by the class inheriting from the base class.
        /// ImageWidth and ImageHeight members are initialized
        /// before the method is called, so these members may be used safely.</note></remarks>
        /// 
        /// <summary>
        /// Actual objects map building.
        /// </summary>
        /// 
        /// <param name="image">Unmanaged image to process.</param>
        /// 
        /// <remarks>The method supports 8 bpp indexed grayscale images and 24/32 bpp color images.</remarks>
        /// 
        /// <exception cref="Exception">Unsupported pixel format of the source image.</exception>
        /// <exception cref="InvalidImagePropertiesException">Cannot process images that are one pixel wide. Rotate the image
        /// or use RecursiveBlobCounter.</exception>
        /// 
        private void BuildObjectsMap(BitmapData image)
        {
            var stride = image.Stride;

            // check pixel format
            if (image.PixelFormat != PixelFormat.Format8bppIndexed &&
                image.PixelFormat != PixelFormat.Format24bppRgb &&
                image.PixelFormat != PixelFormat.Format32bppRgb &&
                image.PixelFormat != PixelFormat.Format32bppArgb &&
                image.PixelFormat != PixelFormat.Format32bppPArgb)
                throw new Exception("Unsupported pixel format of the source image.");

            if (ImageWidth == 1)
                throw new Exception("BlobCounter cannot process images that are one pixel wide. Rotate the image or use RecursiveBlobCounter.");

            var imageWidthM1 = ImageWidth - 1;

            // allocate labels array
            ObjectLabels = new int[ImageWidth * ImageHeight];

            var labelsCount = 0;
            var maxObjects = ((ImageWidth / 2) + 1) * ((ImageHeight / 2) + 1) + 1;
            var map = new int[maxObjects];

            // initially map all labels to themself
            for (var i = 0; i < maxObjects; i++)
                map[i] = i;

            // do the job
            unsafe
            {
                var src = (byte*)image.Scan0.ToPointer();
                var p = 0;

                if (image.PixelFormat == PixelFormat.Format8bppIndexed)
                {
                    var offset = stride - ImageWidth;

                    // 1 - for pixels of the first row
                    if (*src > ThresholdG)
                        ObjectLabels[p] = ++labelsCount;

                    ++src;
                    ++p;

                    // process the rest of the first row
                    for (var x = 1; x < ImageWidth; x++, src++, p++)
                    {
                        // check if we need to label current pixel
                        if (*src > ThresholdG)
                        {
                            // check if the previous pixel already was labeled
                            if (src[-1] > ThresholdG)
                                ObjectLabels[p] = ObjectLabels[p - 1]; // label current pixel, as the previous
                            else
                                ObjectLabels[p] = ++labelsCount; // create new label
                        }
                    }

                    src += offset;

                    // 2 - for other rows
                    // for each row
                    for (var y = 1; y < ImageHeight; y++)
                    {
                        // for the first pixel of the row, we need to check
                        // only upper and upper-right pixels
                        if (*src > ThresholdG)
                        {
                            // check surrounding pixels
                            if (src[-stride] > ThresholdG)
                                ObjectLabels[p] = ObjectLabels[p - ImageWidth]; // label current pixel, as the above                            
                            else if (src[1 - stride] > ThresholdG)
                                ObjectLabels[p] = ObjectLabels[p + 1 - ImageWidth]; // label current pixel, as the above right
                            else
                                ObjectLabels[p] = ++labelsCount; // create new label                            
                        }

                        ++src;
                        ++p;

                        // check left pixel and three upper pixels for the rest of pixels
                        for (var x = 1; x < imageWidthM1; x++, src++, p++)
                        {
                            if (*src > ThresholdG)
                            {
                                // check surrounding pixels
                                if (src[-1] > ThresholdG)
                                    ObjectLabels[p] = ObjectLabels[p - 1]; // label current pixel, as the left
                                else if (src[-1 - stride] > ThresholdG)  
                                    ObjectLabels[p] = ObjectLabels[p - 1 - ImageWidth]; // label current pixel, as the above left
                                else if (src[-stride] > ThresholdG)
                                    ObjectLabels[p] = ObjectLabels[p - ImageWidth]; // label current pixel, as the above

                                if (src[1 - stride] > ThresholdG)
                                {
                                    if (ObjectLabels[p] == 0)
                                    {
                                        // label current pixel, as the above right
                                        ObjectLabels[p] = ObjectLabels[p + 1 - ImageWidth];
                                    }
                                    else
                                    {
                                        var l1 = ObjectLabels[p];
                                        var l2 = ObjectLabels[p + 1 - ImageWidth];

                                        if ((l1 != l2) && (map[l1] != map[l2]))
                                        {
                                            // merge
                                            if (map[l1] == l1)
                                            {
                                                // map left value to the right
                                                map[l1] = map[l2];
                                            }
                                            else if (map[l2] == l2)
                                            {
                                                // map right value to the left
                                                map[l2] = map[l1];
                                            }
                                            else
                                            {
                                                // both values already mapped
                                                map[map[l1]] = map[l2];
                                                map[l1] = map[l2];
                                            }

                                            // reindex
                                            for (var i = 1; i <= labelsCount; i++)
                                            {
                                                if (map[i] != i)
                                                {
                                                    // reindex
                                                    var j = map[i];
                                                    while (j != map[j])
                                                        j = map[j];

                                                    map[i] = j;
                                                }
                                            }
                                        }
                                    }
                                }

                                // label the object if it is not yet
                                if (ObjectLabels[p] == 0)
                                    ObjectLabels[p] = ++labelsCount; // create new label                                
                            }
                        }

                        // for the last pixel of the row, we need to check
                        // only upper and upper-left pixels
                        if (*src > ThresholdG)
                        {
                            // check surrounding pixels
                            if (src[-1] > ThresholdG)
                                ObjectLabels[p] = ObjectLabels[p - 1]; // label current pixel, as the left                            
                            else if (src[-1 - stride] > ThresholdG)
                                ObjectLabels[p] = ObjectLabels[p - 1 - ImageWidth]; // label current pixel, as the above left                            
                            else if (src[-stride] > ThresholdG)
                                ObjectLabels[p] = ObjectLabels[p - ImageWidth]; // label current pixel, as the above                            
                            else
                                ObjectLabels[p] = ++labelsCount; // create new label
                        }

                        ++src;
                        ++p;
                        src += offset;
                    }
                }
                else
                {
                    // color images
                    var pixelSize = Image.GetPixelFormatSize(image.PixelFormat) / 8;
                    var offset = stride - ImageWidth * pixelSize;

                    var strideM1 = stride - pixelSize;
                    var strideP1 = stride + pixelSize;

                    // 1 - for pixels of the first row
                    if ((src[R] | src[G] | src[B]) != 0)
                        ObjectLabels[p] = ++labelsCount;

                    src += pixelSize;
                    ++p;

                    // process the rest of the first row
                    for (int x = 1; x < ImageWidth; x++, src += pixelSize, p++)
                    {
                        // check if we need to label current pixel
                        if (src[R] > ThresholdR || src[G] > ThresholdG || src[B] > ThresholdB)
                        {
                            // check if the previous pixel already was labeled
                            if (src[R - pixelSize] > ThresholdR || src[G - pixelSize] > ThresholdG || src[B - pixelSize] > ThresholdB)
                                ObjectLabels[p] = ObjectLabels[p - 1]; // label current pixel, as the previous                            
                            else
                                ObjectLabels[p] = ++labelsCount; // create new label                            
                        }
                    }

                    src += offset;

                    // 2 - for other rows
                    // for each row
                    for (var y = 1; y < ImageHeight; y++)
                    {
                        // for the first pixel of the row, we need to check
                        // only upper and upper-right pixels
                        if (src[R] > ThresholdR || src[G] > ThresholdG || src[B] > ThresholdB)
                        {
                            // check surrounding pixels
                            if (src[R - stride] > ThresholdR || src[G - stride] > ThresholdG || src[B - stride] > ThresholdB)
                                ObjectLabels[p] = ObjectLabels[p - ImageWidth]; // label current pixel, as the above
                            else if (src[R - strideM1] > ThresholdR || src[G - strideM1] > ThresholdG || src[B - strideM1] > ThresholdB)
                                ObjectLabels[p] = ObjectLabels[p + 1 - ImageWidth]; // label current pixel, as the above right
                            else
                                ObjectLabels[p] = ++labelsCount; // create new label                            
                        }

                        src += pixelSize;
                        ++p;

                        // check left pixel and three upper pixels for the rest of pixels
                        for (var x = 1; x < ImageWidth - 1; x++, src += pixelSize, p++)
                        {
                            if (src[R] > ThresholdR || src[G] > ThresholdG || src[B] > ThresholdB)
                            {
                                // check surrounding pixels
                                if (src[R - pixelSize] > ThresholdR || src[G - pixelSize] > ThresholdG || src[B - pixelSize] > ThresholdB)
                                    ObjectLabels[p] = ObjectLabels[p - 1]; // label current pixel, as the left                                
                                else if (src[R - strideP1] > ThresholdR || src[G - strideP1] > ThresholdG || src[B - strideP1] > ThresholdB)
                                    ObjectLabels[p] = ObjectLabels[p - 1 - ImageWidth]; // label current pixel, as the above left                                
                                else if (src[R - stride] > ThresholdR || src[G - stride] > ThresholdG || src[B - stride] > ThresholdB)
                                    ObjectLabels[p] = ObjectLabels[p - ImageWidth]; // label current pixel, as the above                                

                                if (src[R - strideM1] > ThresholdR || src[G - strideM1] > ThresholdG || src[B - strideM1] > ThresholdB)
                                {
                                    if (ObjectLabels[p] == 0)
                                    {
                                        // label current pixel, as the above right
                                        ObjectLabels[p] = ObjectLabels[p + 1 - ImageWidth];
                                    }
                                    else
                                    {
                                        var l1 = ObjectLabels[p];
                                        var l2 = ObjectLabels[p + 1 - ImageWidth];

                                        if ((l1 != l2) && (map[l1] != map[l2]))
                                        {
                                            // merge
                                            if (map[l1] == l1)
                                            {
                                                // map left value to the right
                                                map[l1] = map[l2];
                                            }
                                            else if (map[l2] == l2)
                                            {
                                                // map right value to the left
                                                map[l2] = map[l1];
                                            }
                                            else
                                            {
                                                // both values already mapped
                                                map[map[l1]] = map[l2];
                                                map[l1] = map[l2];
                                            }

                                            // reindex
                                            for (var i = 1; i <= labelsCount; i++)
                                            {
                                                if (map[i] != i)
                                                {
                                                    // reindex
                                                    var j = map[i];
                                                    while (j != map[j])
                                                        j = map[j];
                                                    map[i] = j;
                                                }
                                            }
                                        }
                                    }
                                }

                                // label the object if it is not yet
                                if (ObjectLabels[p] == 0)
                                    ObjectLabels[p] = ++labelsCount;
                            }
                        }

                        // for the last pixel of the row, we need to check
                        // only upper and upper-left pixels
                        if (src[R] > ThresholdR || src[G] > ThresholdG || src[B] > ThresholdB)
                        {
                            // check surrounding pixels
                            if (src[R - pixelSize] > ThresholdR || src[G - pixelSize] > ThresholdG || src[B - pixelSize] > ThresholdB)
                                ObjectLabels[p] = ObjectLabels[p - 1]; // label current pixel, as the left                            
                            else if (src[R - strideP1] > ThresholdR || src[G - strideP1] > ThresholdG || src[B - strideP1] > ThresholdB)
                                ObjectLabels[p] = ObjectLabels[p - 1 - ImageWidth]; // label current pixel, as the above left
                            else if (src[R - stride] > ThresholdR || src[G - stride] > ThresholdG || src[B - stride] > ThresholdB)
                                ObjectLabels[p] = ObjectLabels[p - ImageWidth]; // label current pixel, as the above
                            else
                                ObjectLabels[p] = ++labelsCount; // create new label
                        }

                        src += pixelSize;
                        ++p;

                        src += offset;
                    }
                }
            }

            // allocate remapping array
            var reMap = new int[map.Length];

            // count objects and prepare remapping array
            ObjectsCount = 0;
            for (var i = 1; i <= labelsCount; i++)
                if (map[i] == i)
                    reMap[i] = ++ObjectsCount; // increase objects count

            // second pass to complete remapping
            for (var i = 1; i <= labelsCount; i++)
                if (map[i] != i)
                    reMap[i] = reMap[map[i]];

            // repair object labels
            for (int i = 0, n = ObjectLabels.Length; i < n; i++)
                ObjectLabels[i] = reMap[ObjectLabels[i]];
        }

        // Collect objects' rectangles
        private unsafe void CollectObjectsInfo(BitmapData image)
        {
            int i = 0, label;

            // create object coordinates arrays
            var x1 = new int[ObjectsCount + 1];
            var y1 = new int[ObjectsCount + 1];
            var x2 = new int[ObjectsCount + 1];
            var y2 = new int[ObjectsCount + 1];

            var area = new int[ObjectsCount + 1];
            var xc = new long[ObjectsCount + 1];
            var yc = new long[ObjectsCount + 1];

            var meanR = new long[ObjectsCount + 1];
            var meanG = new long[ObjectsCount + 1];
            var meanB = new long[ObjectsCount + 1];

            var stdDevR = new long[ObjectsCount + 1];
            var stdDevG = new long[ObjectsCount + 1];
            var stdDevB = new long[ObjectsCount + 1];

            for (var j = 1; j <= ObjectsCount; j++)
            {
                x1[j] = ImageWidth;
                y1[j] = ImageHeight;
            }

            var src = (byte*)image.Scan0.ToPointer();

            if (image.PixelFormat == PixelFormat.Format8bppIndexed)
            {
                var offset = image.Stride - ImageWidth;
                byte g; // pixel's grey value

                // walk through labels array
                for (var y = 0; y < ImageHeight; y++)
                {
                    for (var x = 0; x < ImageWidth; x++, i++, src++)
                    {
                        // get current label
                        label = ObjectLabels[i];

                        // skip unlabeled pixels
                        if (label == 0)
                            continue;

                        // check and update all coordinates
                        if (x < x1[label])
                            x1[label] = x;
                        if (x > x2[label])
                            x2[label] = x;
                        if (y < y1[label])
                            y1[label] = y;
                        if (y > y2[label])
                            y2[label] = y;

                        area[label]++;
                        xc[label] += x;
                        yc[label] += y;

                        g = *src;
                        meanG[label] += g;
                        stdDevG[label] += g * g;
                    }

                    src += offset;
                }

                for (var j = 1; j <= ObjectsCount; j++)
                {
                    meanR[j] = meanB[j] = meanG[j];
                    stdDevR[j] = stdDevB[j] = stdDevG[j];
                }
            }
            else
            {
                // color images
                var pixelSize = Image.GetPixelFormatSize(image.PixelFormat) / 8;
                var offset = image.Stride - ImageWidth * pixelSize;
                byte r, g, b; // RGB value

                // walk through labels array
                for (var y = 0; y < ImageHeight; y++)
                {
                    for (var x = 0; x < ImageWidth; x++, i++, src += pixelSize)
                    {
                        // get current label
                        label = ObjectLabels[i];

                        // skip unlabeled pixels
                        if (label == 0)
                            continue;

                        // check and update all coordinates

                        if (x < x1[label])
                            x1[label] = x;
                        if (x > x2[label])
                            x2[label] = x;
                        if (y < y1[label])
                            y1[label] = y;
                        if (y > y2[label])
                            y2[label] = y;

                        area[label]++;
                        xc[label] += x;
                        yc[label] += y;

                        r = src[R];
                        g = src[G];
                        b = src[B];

                        meanR[label] += r;
                        meanG[label] += g;
                        meanB[label] += b;

                        stdDevR[label] += r * r;
                        stdDevG[label] += g * g;
                        stdDevB[label] += b * b;
                    }

                    src += offset;
                }
            }

            // create blobs
            Blobs.Clear();

            for (var j = 1; j <= ObjectsCount; j++)
            {
                var blobArea = area[j];

                var blob = new Blob(j, new Rectangle(x1[j], y1[j], x2[j] - x1[j] + 1, y2[j] - y1[j] + 1))
                {
                    Area = blobArea,
                    Fullness = (double)blobArea / ((x2[j] - x1[j] + 1) * (y2[j] - y1[j] + 1)),
                    ColorMean = Color.FromArgb((byte)(meanR[j] / blobArea), (byte)(meanG[j] / blobArea), (byte)(meanB[j] / blobArea))
                };

                blob.ColorStdDev = Color.FromArgb(
                    (byte)Math.Sqrt(stdDevR[j] / blobArea - blob.ColorMean.R * blob.ColorMean.R),
                    (byte)Math.Sqrt(stdDevG[j] / blobArea - blob.ColorMean.G * blob.ColorMean.G),
                    (byte)Math.Sqrt(stdDevB[j] / blobArea - blob.ColorMean.B * blob.ColorMean.B));

                Blobs.Add(blob);
            }
        }
    }
}
