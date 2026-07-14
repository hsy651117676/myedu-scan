// Models/ScannerInfo.cs
namespace ScanTool.Models
{
    public class ScannerDeviceInfo
    {
        public string Name { get; set; }
        public string DriverType { get; set; } // "WIA" 或 "TWAIN"
        public override string ToString() => $"{Name} [{DriverType}]";
    }
}