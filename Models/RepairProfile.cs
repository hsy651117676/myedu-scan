// Models/RepairProfile.cs
namespace ScanTool.Models
{
    public class RepairProfile
    {
        public string Name { get; set; }
        public bool AutoDeskew { get; set; }
        public bool Denoise { get; set; }
        public bool RemoveBlackBorder { get; set; }
        public bool Grayscale { get; set; }
        public bool Brightness { get; set; }
        public int BrightnessValue { get; set; } = 10;
        public bool Contrast { get; set; }
        public double ContrastValue { get; set; } = 1.1;
        public bool FillBindingHoles { get; set; }
        public bool FixBacksideText { get; set; }
        public bool HasStamp { get; set; }
        public bool ContrastTwice { get; set; }
    }

    public static class RepairLevels
    {
        public static RepairProfile None => new RepairProfile { Name = "不自动修复" };

        public static RepairProfile Standard => new RepairProfile
        {
            Name = "适当修复",
            AutoDeskew = true,
            Denoise = true,
            Brightness = true,
            BrightnessValue = 10,
            Contrast = true,
            ContrastValue = 1.1
        };

        public static RepairProfile Deep => new RepairProfile
        {
            Name = "深度修复",
            AutoDeskew = true,
            Denoise = true,
            Brightness = true,
            BrightnessValue = 20,
            Contrast = true,
            ContrastValue = 1.2
        };
    }
}