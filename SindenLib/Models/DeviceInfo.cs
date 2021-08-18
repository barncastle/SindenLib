using SindenLib.Static;
using System;

namespace SindenLib.Models
{
    public class DeviceInfo
    {
        public Versions Version { get; internal set; }
        public string UniqueId { get; internal set; }
        public string Colour { get; internal set; }
        public string ManufactureDate { get; internal set; }
        public string LinkedCamera { get; internal set; }
        public double CalibrationX { get; internal set; }
        public double CalibrationY { get; internal set; }
        public bool RequireCalibration { get; internal set; }
        public DateTime LastButtonPushed { get; internal set; } = DateTime.Now;
    }
}