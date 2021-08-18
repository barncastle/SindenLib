using System.Drawing;

namespace SindenLib.Imaging
{
    internal class Blob
    {
        public int ID { get; set; }
        public Rectangle Rectangle { get; }
        public int Area { get; set; }
        public double Fullness { get; set; }
        public Color ColorMean { get; set; } = Color.Black;
        public Color ColorStdDev { get; set; } = Color.Black;

        public Blob(int id, Rectangle rect)
        {
            ID = id;
            Rectangle = rect;
        }

        public Blob(Blob source)
        {
            ID = source.ID;
            Rectangle = source.Rectangle;
            Area = source.Area;
            Fullness = source.Fullness;
            ColorMean = source.ColorMean;
            ColorStdDev = source.ColorStdDev;
        }
    }
}
