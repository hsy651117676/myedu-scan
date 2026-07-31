using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using WIA;

namespace ScanTool.Services
{
    public enum ScanColorMode { Color, Grayscale, BlackWhite }

    public class ScannerInfo
    {
        public string Name { get; set; }
        public bool HasFeeder { get; set; }
        public bool HasDuplex { get; set; }
        public bool HasFlatbed { get; set; }
        public string Manufacturer { get; set; }
    }

    public class ScanService
    {
        private Device _scanner;
        private TwainService _twainService = new TwainService();
        private byte[] _lastScanHash;
        private const double SimilarityThreshold = 0.95;
        private CropPresetManager _presetManager;
        private bool _isTwain;
        private string _currentScannerName;

        // WIA 色彩模式：用属性名称匹配，而不是数字 ID
        private static readonly Dictionary<string, int> WiaColorValues = new Dictionary<string, int>
        {
            { "彩色", 1 },
            { "灰度", 2 },
            { "黑白", 4 }
        };

        public void SetPresetManager(CropPresetManager manager) => _presetManager = manager;

        public List<string> GetScanners()
        {
            var list = new List<string>();
            // WIA
            try
            {
                var manager = new DeviceManager();
                foreach (DeviceInfo info in manager.DeviceInfos)
                    if (info.Type == WiaDeviceType.ScannerDeviceType)
                        list.Add("[WIA] " + info.Properties["Name"].get_Value().ToString());
            }
            catch { }
            // TWAIN
            var twainList = _twainService.GetScanners();
            foreach (var name in twainList)
                list.Add("[TWAIN] " + name);
            return list;
        }

        public bool ConnectScanner(string name)
        {
            _currentScannerName = name;
            if (name.StartsWith("[TWAIN]"))
            {
                _isTwain = true;
                return true;
            }
            else
            {
                var wiaName = name.StartsWith("[WIA] ") ? name.Substring(6) : name;
                try
                {
                    var manager = new DeviceManager();
                    foreach (DeviceInfo info in manager.DeviceInfos)
                        if (info.Properties["Name"].get_Value().ToString() == wiaName)
                        { _scanner = info.Connect(); _isTwain = false; return true; }
                }
                catch { }
                _isTwain = true;
                return true;
            }
        }

        public ScannerInfo GetScannerInfo(string scannerName)
        {
            try
            {
                var manager = new DeviceManager();
                foreach (DeviceInfo info in manager.DeviceInfos)
                {
                    if (info.Type == WiaDeviceType.ScannerDeviceType &&
                        info.Properties["Name"].get_Value().ToString() == scannerName)
                    {
                        var scanner = info.Connect();
                        return new ScannerInfo
                        {
                            Name = scannerName,
                            HasFeeder = HasProperty(scanner, "3088"),
                            HasDuplex = HasProperty(scanner, "3087"),
                            HasFlatbed = scanner.Items.Count > 1,
                            Manufacturer = GetPropertySafe(info.Properties, "Manufacturer", "")
                        };
                    }
                }
            }
            catch { }
            return new ScannerInfo { Name = scannerName };
        }

        public Bitmap Scan(CropPreset preset)
        {
            Bitmap rawScan = ScanMaxArea(preset);
            if (rawScan == null) return null;

            Bitmap result;
            if (preset != null)
                result = ApplyPreset(rawScan, preset);
            else
            {
                var (cropped, _, _) = AutoDetectAndCrop(rawScan, 300);
                result = cropped;
            }
            rawScan.Dispose();

            if (IsDuplicate(result))
            {
                var dr = MessageBox.Show("⚠ 当前扫描内容与上一张高度相似！\n\n可能忘记换纸，是否仍然保存？\n\n【是】保存  【否】丢弃并重扫",
                    "重复扫描警告", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (dr == DialogResult.No) { result.Dispose(); return null; }
            }
            _lastScanHash = GetImageHash(result);
            return result;
        }

        public Bitmap ScanMaxArea(CropPreset preset = null)
        {
            string colorMode = preset?.ColorMode ?? "彩色";

            // TWAIN 分支
            if (_isTwain)
            {
                if (!_twainService.Initialized)
                    _twainService.Connect(null, true);
                var bmp = _twainService.Scan(300, colorMode);
                if (bmp != null) return bmp;
            }

            // WIA 分支
            if (_scanner != null)
            {
                try
                {
                    var item = _scanner.Items[1];

                    // 设置 DPI（用名称 + ID 兜底）
                    SetWiaProperty(item.Properties, "Horizontal Resolution", 300);
                    SetWiaProperty(item.Properties, "Vertical Resolution", 300);

                    // 设置 JPEG 质量
                    SetWiaProperty(item.Properties, "Compression Quality", 100);

                    // 设置色彩模式（用名称 + ID 兜底）
                    int colorVal = WiaColorValues.TryGetValue(colorMode, out int v) ? v : 1;
                    SetWiaProperty(item.Properties, "Current Intent", colorVal);
                    // 有些扫描仪用 "Color Mode" 名称
                    SetWiaProperty(item.Properties, "Color Mode", colorVal);
                    // 有些扫描仪用 "Data Type" 控制位深度
                    int bitsPerPixel = colorMode switch { "彩色" => 24, "灰度" => 8, "黑白" => 1, _ => 24 };
                    SetWiaProperty(item.Properties, "Data Type", bitsPerPixel);

                    // 传输
                    var imgFile = (ImageFile)item.Transfer("{B96B3CAB-0728-11D3-9D7B-0000F81EF32E}");
                    var bytes = (byte[])imgFile.FileData.get_BinaryData();
                    if (bytes != null && bytes.Length > 0)
                        using (var ms = new MemoryStream(bytes))
                            return new Bitmap(ms);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[WIA] Scan error: {ex.Message}");
                }
            }
            return null;
        }

        /// <summary>
        /// 用属性名称设置 WIA 属性，遍历匹配名称
        /// </summary>
        private void SetWiaProperty(WIA.Properties props, string name, object value)
        {
            try
            {
                foreach (Property prop in props)
                {
                    try
                    {
                        if (prop.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            prop.set_Value(value);
                            System.Diagnostics.Debug.WriteLine($"[WIA] 设置 {prop.Name} ({prop.PropertyID}) = {value}");
                            return;
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        public Bitmap ApplyPreset(Bitmap scanned, CropPreset preset)
        {
            using var mat = scanned.ToMat();
            int cx = Math.Max(0, preset.X);
            int cy = Math.Max(0, preset.Y);
            int cw = Math.Min(preset.Width, mat.Width - cx);
            int ch = Math.Min(preset.Height, mat.Height - cy);
            var cr = new OpenCvSharp.Rect(cx, cy, cw, ch);
            using var cropped = new Mat(mat, cr);
            if (preset.Rotation != 0)
            {
                RotateFlags flag = preset.Rotation switch
                {
                    90 => RotateFlags.Rotate90Clockwise,
                    180 => RotateFlags.Rotate180,
                    270 => RotateFlags.Rotate90Counterclockwise,
                    _ => RotateFlags.Rotate90Clockwise
                };
                var rotated = new Mat();
                Cv2.Rotate(cropped, rotated, flag);
                var bmp = rotated.ToBitmap();
                rotated.Dispose();
                return bmp;
            }
            return cropped.ToBitmap();
        }

        public Bitmap PreviewPreset(Bitmap scanned, CropPreset preset) => ApplyPreset(scanned, preset);

        private (Bitmap result, string paperSize, bool vertical) AutoDetectAndCrop(Bitmap scanned, int dpi)
        {
            try
            {
                using var mat = scanned.ToMat();
                using var gray = new Mat();
                if (mat.Channels() == 4) Cv2.CvtColor(mat, gray, ColorConversionCodes.BGRA2GRAY);
                else if (mat.Channels() == 3) Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);
                else mat.CopyTo(gray);
                using var binary = new Mat();
                Cv2.Threshold(gray, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
                using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(5, 5));
                Cv2.MorphologyEx(binary, binary, MorphTypes.Open, kernel);
                Cv2.MorphologyEx(binary, binary, MorphTypes.Close, kernel);
                Cv2.FindContours(binary, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
                if (contours.Length == 0) return (new Bitmap(scanned), "A4", true);
                int bestIdx = 0; double maxArea = 0;
                for (int i = 0; i < contours.Length; i++)
                { double area = Cv2.ContourArea(contours[i]); if (area > maxArea) { maxArea = area; bestIdx = i; } }
                var rect = Cv2.BoundingRect(contours[bestIdx]);
                if (rect.Width < 50 || rect.Height < 50) return (new Bitmap(scanned), "未知", true);
                int m = 3, sx = Math.Max(0, rect.X - m), sy = Math.Max(0, rect.Y - m);
                int sw = Math.Min(mat.Width - sx, rect.Width + m * 2), sh = Math.Min(mat.Height - sy, rect.Height + m * 2);
                bool vertical = sh > sw;
                double mmW = sw / (double)dpi * 25.4, mmH = sh / (double)dpi * 25.4;
                string paperSize = MatchPaperSize(mmW, mmH);
                var cropRect = new OpenCvSharp.Rect(sx, sy, sw, sh);
                using var cropped = new Mat(mat, cropRect);
                using var deskewed = AutoDeskew(cropped);
                return (deskewed.ToBitmap(), paperSize, vertical);
            }
            catch { return (new Bitmap(scanned), "A4", true); }
        }

        private Mat AutoDeskew(Mat src)
        {
            using var gray = new Mat();
            if (src.Channels() == 3) Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
            else src.CopyTo(gray);
            using var edges = new Mat(); Cv2.Canny(gray, edges, 50, 150);
            var lines = Cv2.HoughLines(edges, 1, Math.PI / 180, 200);
            if (lines.Length == 0) return src.Clone();
            var angles = new List<double>();
            for (int i = 0; i < Math.Min(lines.Length, 20); i++)
            {
                var a = lines[i].Theta * 180 / Math.PI;
                if (a > 85 && a < 95) continue;
                var s = a > 90 ? a - 180 : a;
                if (Math.Abs(s) < 30) angles.Add(s);
            }
            if (angles.Count == 0) return src.Clone();
            double avg = angles.Average();
            var center = new Point2f(src.Width / 2f, src.Height / 2f);
            using var rotMat = Cv2.GetRotationMatrix2D(center, avg, 1.0);
            var result = new Mat();
            Cv2.WarpAffine(src, result, rotMat, src.Size(), InterpolationFlags.Cubic, BorderTypes.Constant, new Scalar(255, 255, 255));
            return result;
        }

        private string MatchPaperSize(double mmW, double mmH)
        {
            var papers = new (string, double, double)[] { ("A4", 210, 297), ("A3", 297, 420), ("A5", 148, 210), ("B5", 176, 250) };
            double best = double.MaxValue; string bestP = "A4";
            foreach (var (n, w, h) in papers) { double d = Math.Abs(mmW - w) + Math.Abs(mmH - h); if (d < best) { best = d; bestP = n; } }
            return best > 30 ? "自定义" : bestP;
        }

        private bool IsDuplicate(Bitmap bmp)
        {
            if (_lastScanHash == null) return false;
            var h = GetImageHash(bmp);
            return CompareHash(_lastScanHash, h) >= SimilarityThreshold;
        }

        private byte[] GetImageHash(Bitmap bmp)
        {
            using var s = new Bitmap(bmp, 8, 8);
            int[] p = new int[64]; int sum = 0;
            for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
                { var c = s.GetPixel(x, y); int g = (c.R * 299 + c.G * 587 + c.B * 114) / 1000; p[y * 8 + x] = g; sum += g; }
            int avg = sum / 64; byte[] hash = new byte[8];
            for (int i = 0; i < 64; i++) if (p[i] >= avg) hash[i / 8] |= (byte)(1 << (7 - i % 8));
            return hash;
        }

        private double CompareHash(byte[] a, byte[] b)
        {
            int same = 0;
            for (int i = 0; i < 8; i++) { byte d = (byte)(a[i] ^ b[i]); for (int j = 0; j < 8; j++) if ((d & (1 << j)) == 0) same++; }
            return same / 64.0;
        }

        private bool HasProperty(Device device, string id) { try { foreach (Item item in device.Items) { try { var v = item.Properties[id]; return true; } catch { } } } catch { } return false; }
        private string GetPropertySafe(WIA.Properties p, string n, string d) { try { return p[n].get_Value()?.ToString() ?? d; } catch { return d; } }
    }
}