namespace ScanTool.Models
{
    public class RepairProfile
    {
        public string Name { get; set; } = "不自动修复";

        // 诊断
        public string Diagnosis { get; set; }
        public double BackgroundBrightness { get; set; }
        public double TextDepth { get; set; }
        public double Separation { get; set; }
        public bool HasStamp { get; set; }

        // 修复参数（新）
        public bool NeedGamma { get; set; }
        public double GammaValue { get; set; } = 1.0;
        public bool NeedContrast { get; set; }
        public double ContrastAlpha { get; set; } = 1.0;
        public bool NeedClahe { get; set; }
        public bool NeedBackgroundRemove { get; set; }

        // 旧字段（手动预设 + 兼容）
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
        public bool ContrastTwice { get; set; }
        public bool Enhance { get; set; }
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