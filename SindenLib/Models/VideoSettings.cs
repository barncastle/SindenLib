using SindenLib.Static;
using System.Drawing;

namespace SindenLib.Models
{
    public class VideoSettings
    {
        /// <summary>
        /// Primary border colour
        /// </summary>
        public Color BorderColour { get; set; } = Color.White;
        /// <summary>
        /// Border Colour Match Radius
        /// </summary>
        public int FilterRadius { get; set; } = 50;
        /// <summary>
        /// Hand the controller is held
        /// <para>AKA Gangsta Mode</para>
        /// </summary>
        public Handedness Handedness { get; set; } = Handedness.Auto;
        /// <summary>
        /// Increases accuracy with bright sunlight but breaks offscreen reload
        /// </summary>
        public bool OnlyMatchWherePointing { get; set; }
        /// <summary>
        /// Enable/disable anti jitter
        /// </summary>
        public bool UseAntiJitter { get; set; } = true;
        /// <summary>
        /// Minimum percentage of movement to be consider not jitter
        /// </summary>
        public double JitterMoveThreshold { get; set; } = 0.5;
        /// <summary>
        /// Offset to counter for sight
        /// </summary>
        public double YSightOffset { get; set; } = 4.9;
    }
}
