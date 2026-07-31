using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using ScanTool.Models;

namespace ScanTool.Services
{
    public class AutoRepairService
    {
        // ==================== 分析入口 ====================

        public static RepairProfile Analyze(Bitmap bmp)
        {
            var profile = new RepairProfile { Name = "自动判断修复" };

            using (var mat = bmp.ToMat())
            {
                double rBg = GetChannelMedian(mat, 0, 0.15, 1.0);
                double gBg = GetChannelMedian(mat, 1, 0.15, 1.0);
                double bBg = GetChannelMedian(mat, 2, 0.15, 1.0);
                double vBg = GetBrightnessMedian(mat);

                double rText = GetChannelMedian(mat, 0, 0.0, 0.05);

                bool hasStamp = DetectStamp(mat);
                double skewAngle = DetectSkewAngle(mat);
                bool hasBlackBorder = DetectBlackBorder(mat);
                bool hasBindingHoles = DetectBindingHoles(mat);

                profile.BackgroundBrightness = rBg;

                Debug.WriteLine($"[AutoRepair] === 诊断开始 ===");
                Debug.WriteLine($"[AutoRepair] 背景 R:{rBg:F0} G:{gBg:F0} B:{bBg:F0} V:{vBg:F0}");
                Debug.WriteLine($"[AutoRepair] 文字 R:{rText:F0}");
                Debug.WriteLine($"[AutoRepair] 公章: {(hasStamp ? "有" : "无")}");
                Debug.WriteLine($"[AutoRepair] 黑边: {(hasBlackBorder ? "有" : "无")}");
                Debug.WriteLine($"[AutoRepair] 装订孔: {(hasBindingHoles ? "有" : "无")}");

                // 倾斜检测
                if (Math.Abs(skewAngle) > 1.5)
                {
                    profile.AutoDeskew = true;
                    Debug.WriteLine($"[AutoRepair] 倾斜: {skewAngle:F1}°");
                }

                // 黑边检测（独立处理，不受背景色影响）
                profile.RemoveBlackBorder = hasBlackBorder;

                // 装订孔检测
                profile.FillBindingHoles = hasBindingHoles;

                // ========== 决策：背景处理 ==========
                double fBase;
                double maskThreshold;

                if (hasStamp)
                {
                    profile.Diagnosis = "有公章";
                    profile.NeedBackgroundRemove = true;
                    fBase = 1.03;
                    maskThreshold = 210;
                }
                else if (vBg < 180)
                {
                    profile.Diagnosis = "发黑";
                    profile.NeedBackgroundRemove = true;
                    fBase = 1.15;
                    maskThreshold = 100;
                }
                else if (rBg < 190)
                {
                    profile.Diagnosis = "暗黄";
                    profile.NeedBackgroundRemove = true;
                    fBase = 1.12;
                    maskThreshold = 140;
                }
                else if (bBg < 200)
                {
                    profile.Diagnosis = "暖黄";
                    profile.NeedBackgroundRemove = true;
                    fBase = 1.10;
                    maskThreshold = 130;
                }
                else if (rBg < 210)
                {
                    profile.Diagnosis = "发黄";
                    profile.NeedBackgroundRemove = true;
                    fBase = 1.08;
                    maskThreshold = 170;
                }
                else if (rBg < 225)
                {
                    profile.Diagnosis = "中黄";
                    profile.NeedBackgroundRemove = true;
                    fBase = 1.06;
                    maskThreshold = 195;
                }
                else if (rBg < 240)
                {
                    profile.Diagnosis = "微黄";
                    profile.NeedBackgroundRemove = true;
                    fBase = 1.05;
                    maskThreshold = 210;
                }
                else
                {
                    profile.Diagnosis = "正常";
                    profile.NeedBackgroundRemove = false;
                    profile.Contrast = false;
                    fBase = 1.0;
                    maskThreshold = 210;
                }

                // 文字偏淡时增强对比度
                if (rText > 120) fBase += 0.03;
                else if (rText > 100) fBase += 0.02;

                // 如果有黑边，即使背景正常也要进行背景处理
                if (hasBlackBorder && !profile.NeedBackgroundRemove)
                {
                    profile.NeedBackgroundRemove = true;
                    profile.Contrast = true;
                    fBase = 1.02;
                    Debug.WriteLine($"[AutoRepair] 检测到黑边，启用背景处理");
                }

                profile.Contrast = true;
                profile.ContrastValue = fBase;
                profile.MaskThreshold = maskThreshold;

                Debug.WriteLine($"[AutoRepair] 判定: {profile.Diagnosis} → 遮罩阈值={maskThreshold} F={fBase:F2} RemoveBlackBorder={profile.RemoveBlackBorder}");
            }

            return profile;
        }

        // ==================== 执行入口 ====================

        public static Bitmap Execute(Bitmap bmp, RepairProfile profile)
        {
            Debug.WriteLine($"[Execute] RemoveBlackBorder={profile.RemoveBlackBorder}, NeedBgRemove={profile.NeedBackgroundRemove}, Contrast={profile.Contrast}");
            if (profile == null || profile.Name == "不自动修复")
                return new Bitmap(bmp);

            Mat result = null;
            Mat textMask = null;
            Mat bgMask = null;
            try
            {
                result = bmp.ToMat();

                // ✅ 优先去除黑边（独立执行，不受其他条件影响）
                if (profile.RemoveBlackBorder)
                {
                    Debug.WriteLine($"[AutoRepair] 执行黑边去除");
                    var tmp = RemoveBlackBorder(result);
                    result.Dispose();
                    result = tmp;
                }

                // 创建文字遮罩（基于去黑边后的图像）
                textMask = CreateTextMask(result, profile.MaskThreshold);

                // 倾斜矫正
                if (profile.AutoDeskew)
                {
                    var tmp = Deskew(result);
                    result.Dispose();
                    result = tmp;
                    textMask.Dispose();
                    textMask = CreateTextMask(result, profile.MaskThreshold);
                }

                // 填充装订孔
                if (profile.FillBindingHoles)
                    FillBindingHoles(result);

                // 背景变白
                if (profile.NeedBackgroundRemove)
                {
                    Debug.WriteLine($"[AutoRepair] 执行背景变白");
                    bgMask = new Mat();
                    Cv2.BitwiseNot(textMask, bgMask);
                    result.SetTo(new Scalar(255, 255, 255), bgMask);
                }

                // 对比度增强
                if (profile.Contrast)
                {
                    Debug.WriteLine($"[AutoRepair] 执行 F: {profile.ContrastValue:F2}");
                    result.ConvertTo(result, -1, profile.ContrastValue, 0);
                }

                return result.ToBitmap();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AutoRepair] 异常: {ex.Message}");
                if (result != null)
                {
                    var b = result.ToBitmap();
                    result.Dispose();
                    return b;
                }
                return bmp;
            }
            finally
            {
                result?.Dispose();
                textMask?.Dispose();
                bgMask?.Dispose();
            }
        }

        // ==================== 文字遮罩 ====================

        private static Mat CreateTextMask(Mat src, double threshold)
        {
            Mat[] channels = null;
            Mat minCh = null;
            Mat mask = null;
            Mat hsv = null;
            Mat sm1 = null;
            Mat sm2 = null;
            Mat kernel = null;

            try
            {
                channels = Cv2.Split(src);
                minCh = new Mat();
                Cv2.Min(channels[0], channels[1], minCh);
                Cv2.Min(minCh, channels[2], minCh);

                mask = new Mat();
                Cv2.Threshold(minCh, mask, threshold, 255, ThresholdTypes.BinaryInv);

                // 公章保护
                hsv = new Mat();
                Cv2.CvtColor(src, hsv, ColorConversionCodes.BGR2HSV);
                sm1 = new Mat();
                sm2 = new Mat();
                Cv2.InRange(hsv, new Scalar(0, 50, 50), new Scalar(10, 255, 255), sm1);
                Cv2.InRange(hsv, new Scalar(156, 50, 50), new Scalar(180, 255, 255), sm2);
                Cv2.Add(mask, sm1, mask);
                Cv2.Add(mask, sm2, mask);

                kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(5, 5));
                Cv2.Dilate(mask, mask, kernel, iterations: 0);

                return mask;
            }
            finally
            {
                if (channels != null)
                    foreach (var c in channels) c.Dispose();
                minCh?.Dispose();
                hsv?.Dispose();
                sm1?.Dispose();
                sm2?.Dispose();
                kernel?.Dispose();
            }
        }

        // ==================== 亮度中位数 ====================

        private static double GetBrightnessMedian(Mat src)
        {
            using var hsv = new Mat();
            Cv2.CvtColor(src, hsv, ColorConversionCodes.BGR2HSV);
            var channels = Cv2.Split(hsv);
            using var v = channels[2];

            using var hist = new Mat();
            Cv2.CalcHist(new[] { v }, new[] { 0 }, null, hist, 1, new[] { 256 }, new[] { new Rangef(0, 256) });

            float total = v.Rows * v.Cols;
            float targetSum = total * 0.15f + total * 0.85f / 2.0f;

            float sum = 0;
            int median = 128;
            for (int i = 0; i < 256; i++)
            {
                sum += hist.At<float>(i);
                if (sum >= targetSum) { median = i; break; }
            }

            foreach (var c in channels) c.Dispose();
            return median;
        }

        // ==================== 中位数取样 ====================

        private static double GetChannelMedian(Mat src, int channelIndex, double lowPercent, double highPercent)
        {
            Mat[] channels = null;
            try
            {
                channels = Cv2.Split(src);
                using var ch = channels[channelIndex];

                using var hist = new Mat();
                Cv2.CalcHist(new[] { ch }, new[] { 0 }, null, hist, 1, new[] { 256 }, new[] { new Rangef(0, 256) });

                float total = ch.Rows * ch.Cols;
                float targetSum = (float)(total * lowPercent + total * (highPercent - lowPercent) / 2.0);

                float sum = 0;
                int median = 128;
                for (int i = 0; i < 256; i++)
                {
                    sum += hist.At<float>(i);
                    if (sum >= targetSum) { median = i; break; }
                }

                return median;
            }
            finally
            {
                if (channels != null)
                    foreach (var c in channels) c.Dispose();
            }
        }

        // ==================== 公章检测 ====================

        private static bool DetectStamp(Mat src)
        {
            using var hsv = new Mat();
            Cv2.CvtColor(src, hsv, ColorConversionCodes.BGR2HSV);

            using var mask1 = new Mat();
            using var mask2 = new Mat();
            Cv2.InRange(hsv, new Scalar(0, 50, 50), new Scalar(10, 255, 255), mask1);
            Cv2.InRange(hsv, new Scalar(156, 50, 50), new Scalar(180, 255, 255), mask2);

            using var mask = new Mat();
            Cv2.Add(mask1, mask2, mask);

            double redRatio = Cv2.CountNonZero(mask) / (double)(src.Rows * src.Cols);
            return redRatio > 0.02;
        }

        // ==================== 倾斜检测与矫正 ====================

        private static double DetectSkewAngle(Mat src)
        {
            using var gray = ToGray(src);
            using var small = new Mat();
            Cv2.Resize(gray, small, new OpenCvSharp.Size(gray.Width / 4, gray.Height / 4));

            using var edges = new Mat();
            Cv2.Canny(small, edges, 50, 150);
            var lines = Cv2.HoughLines(edges, 1, Math.PI / 180, 150);

            if (lines.Length == 0) return 0;

            var angles = new List<double>();
            for (int i = 0; i < Math.Min(lines.Length, 10); i++)
            {
                double a = lines[i].Theta * 180 / Math.PI;
                if (a > 85 && a < 95) continue;
                double s = a > 90 ? a - 180 : a;
                if (Math.Abs(s) < 15) angles.Add(s);
            }
            return angles.Count == 0 ? 0 : angles.Average();
        }

        private static Mat Deskew(Mat src)
        {
            double angle = DetectSkewAngle(src);
            if (Math.Abs(angle) < 0.5) return src.Clone();
            var center = new Point2f(src.Width / 2f, src.Height / 2f);
            var rotMat = Cv2.GetRotationMatrix2D(center, angle, 1.0);
            var result = new Mat();
            Cv2.WarpAffine(src, result, rotMat, src.Size(), InterpolationFlags.Cubic, BorderTypes.Constant, new Scalar(255, 255, 255));
            return result;
        }

        // ==================== 黑边检测与去除 ====================

        private static bool DetectBlackBorder(Mat src)
        {
            using var gray = ToGray(src);
            int w = src.Width, h = src.Height;
            int margin = Math.Max(20, (int)(src.Width * 0.03));
            double top = Cv2.Mean(gray[new OpenCvSharp.Rect(0, 0, w, margin)]).Val0;
            double bottom = Cv2.Mean(gray[new OpenCvSharp.Rect(0, h - margin, w, margin)]).Val0;
            double left = Cv2.Mean(gray[new OpenCvSharp.Rect(0, 0, margin, h)]).Val0;
            double right = Cv2.Mean(gray[new OpenCvSharp.Rect(w - margin, 0, margin, h)]).Val0;
            Debug.WriteLine($"[DetectBlackBorder] margin={margin} top={top:F0} bottom={bottom:F0} left={left:F0} right={right:F0}");
            return top < 200 || bottom < 200 || left < 200 || right < 200;
        }
        private static Mat RemoveBlackBorder(Mat src)
        {
            Mat gray;
            if (src.Channels() == 4)
            {
                gray = new Mat();
                Cv2.CvtColor(src, gray, ColorConversionCodes.BGRA2GRAY);
            }
            else if (src.Channels() == 3)
            {
                gray = new Mat();
                Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
            }
            else
            {
                gray = src.Clone();
            }

            using (gray)
            {
                int w = gray.Width, h = gray.Height;
                int sample = 10;
                int margin = Math.Max(20, (int)(w * 0.03));

                double tl = Cv2.Mean(gray[new OpenCvSharp.Rect(0, 0, sample, sample)]).Val0;
                double tr = Cv2.Mean(gray[new OpenCvSharp.Rect(w - sample, 0, sample, sample)]).Val0;
                double bl = Cv2.Mean(gray[new OpenCvSharp.Rect(0, h - sample, sample, sample)]).Val0;
                double br = Cv2.Mean(gray[new OpenCvSharp.Rect(w - sample, h - sample, sample, sample)]).Val0;

                double topStrip = Cv2.Mean(gray[new OpenCvSharp.Rect(0, 0, w, margin)]).Val0;
                double bottomStrip = Cv2.Mean(gray[new OpenCvSharp.Rect(0, h - margin, w, margin)]).Val0;

                Debug.WriteLine($"[RemoveBlackBorder] 四角: TL={tl:F0} TR={tr:F0} BL={bl:F0} BR={br:F0} 条带: top={topStrip:F0} bottom={bottomStrip:F0}");

                int topFill = 0, bottomFill = 0;

                // === 顶部：直接找白纸隔离带 ===
                double topDark = Math.Min(topStrip, Math.Min(tl, tr));
                if (topDark < 200)
                {
                    // 从第0行开始，找连续3行都>240的白纸区域
                    for (int y = 0; y < h / 3; y++)
                    {
                        bool allWhite = true;
                        for (int k = 0; k < 3; k++)
                        {
                            double rowMean = Cv2.Mean(gray[new OpenCvSharp.Rect(0, y + k, w, 1)]).Val0;
                            if (rowMean < 240) { allWhite = false; break; }
                        }
                        if (allWhite)
                        {
                            topFill = y;
                            Debug.WriteLine($"[RemoveBlackBorder] 顶部白纸隔离带: y={y}");
                            break;
                        }
                    }
                }

                // === 底部 ===
                double bottomDark = Math.Min(bottomStrip, Math.Min(bl, br));
                if (bottomDark < 200)
                {
                    for (int y = h - 1; y > h * 2 / 3; y--)
                    {
                        bool allWhite = true;
                        for (int k = 0; k < 3; k++)
                        {
                            double rowMean = Cv2.Mean(gray[new OpenCvSharp.Rect(0, y - k, w, 1)]).Val0;
                            if (rowMean < 240) { allWhite = false; break; }
                        }
                        if (allWhite)
                        {
                            bottomFill = h - 1 - y;
                            Debug.WriteLine($"[RemoveBlackBorder] 底部白纸隔离带: y={y}");
                            break;
                        }
                    }
                }

                if (topFill > h * 0.20) topFill = 0;
                if (bottomFill > h * 0.20) bottomFill = 0;

                Debug.WriteLine($"[RemoveBlackBorder] 填充: top={topFill} bottom={bottomFill}");

                if (topFill == 0 && bottomFill == 0)
                    return src.Clone();

                Mat result = src.Clone();
                if (topFill > 0)
                    result[new OpenCvSharp.Rect(0, 0, w, topFill)].SetTo(Scalar.White);
                if (bottomFill > 0)
                    result[new OpenCvSharp.Rect(0, h - bottomFill, w, bottomFill)].SetTo(Scalar.White);

                return result;
            }
        }

        // ==================== 装订孔检测与填充 ====================

        private static bool DetectBindingHoles(Mat src)
        {
            using var gray = ToGray(src);
            using var binary = new Mat();
            Cv2.Threshold(gray, binary, 25, 255, ThresholdTypes.BinaryInv);

            int margin = src.Width / 7;
            int holeCount = 0;

            using var leftRegion = binary[new OpenCvSharp.Rect(0, 0, margin, src.Height)];
            holeCount += CountHoles(leftRegion);

            using var rightRegion = binary[new OpenCvSharp.Rect(src.Width - margin, 0, margin, src.Height)];
            holeCount += CountHoles(rightRegion);

            return holeCount >= 2;
        }

        private static int CountHoles(Mat region)
        {
            Cv2.FindContours(region, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
            int count = 0;
            foreach (var c in contours)
            {
                double area = Cv2.ContourArea(c);
                if (area > 1500 && area < 15000)
                {
                    var rect = Cv2.BoundingRect(c);
                    double aspectRatio = (double)rect.Width / rect.Height;
                    if (aspectRatio > 0.6 && aspectRatio < 1.6)
                    {
                        double fillRatio = area / (rect.Width * rect.Height);
                        if (fillRatio > 0.5)
                            count++;
                    }
                }
            }
            return count;
        }

        private static void FillBindingHoles(Mat src)
        {
            using var gray = ToGray(src);
            using var binary = new Mat();
            Cv2.Threshold(gray, binary, 25, 255, ThresholdTypes.BinaryInv);

            int margin = src.Width / 7;
            FillHolesInRegion(src, binary, 0, margin);
            FillHolesInRegion(src, binary, src.Width - margin, margin);
        }

        private static void FillHolesInRegion(Mat src, Mat binary, int startX, int width)
        {
            using var region = binary[new OpenCvSharp.Rect(startX, 0, width, src.Height)];
            Cv2.FindContours(region, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
            foreach (var c in contours)
            {
                double area = Cv2.ContourArea(c);
                if (area > 1500 && area < 15000)
                {
                    var rect = Cv2.BoundingRect(c);
                    double aspectRatio = (double)rect.Width / rect.Height;
                    double fillRatio = area / (rect.Width * rect.Height);
                    if (aspectRatio > 0.6 && aspectRatio < 1.6 && fillRatio > 0.5)
                    {
                        int expand = 3;
                        int x = Math.Max(0, startX + rect.X - expand);
                        int y = Math.Max(0, rect.Y - expand);
                        int w = Math.Min(src.Width - x, rect.Width + expand * 2);
                        int h = Math.Min(src.Height - y, rect.Height + expand * 2);
                        src[new OpenCvSharp.Rect(x, y, w, h)].SetTo(Scalar.White);
                    }
                }
            }
        }

        // ==================== 工具 ====================

        private static Mat ToGray(Mat src)
        {
            var gray = new Mat();
            if (src.Channels() == 4)
                Cv2.CvtColor(src, gray, ColorConversionCodes.BGRA2GRAY);
            else if (src.Channels() == 3)
                Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
            else
                return src.Clone();
            return gray;
        }
        public static Bitmap ApplyPreset(Bitmap bmp, RepairProfile profile, Action<Bitmap> onPreview = null)
        {
            if (profile == null || profile.Name == "不自动修复")
                return new Bitmap(bmp);

            Debug.WriteLine($"[ApplyPreset] === {profile.Name} ===");

            Mat result = bmp.ToMat();
            var beforeMean = Cv2.Mean(result).Val0;

            if (profile.AutoDeskew) result = Deskew(result);
            if (profile.Denoise) { var r = new Mat(); Cv2.MedianBlur(result, r, 3); result.Dispose(); result = r; }
            if (profile.Grayscale) { var r = new Mat(); Cv2.CvtColor(result, r, ColorConversionCodes.BGR2GRAY); Cv2.CvtColor(r, result, ColorConversionCodes.GRAY2BGR); r.Dispose(); }
            if (profile.Brightness) result.ConvertTo(result, MatType.CV_8UC3, 1.0, profile.BrightnessValue);
            if (profile.Contrast) result.ConvertTo(result, MatType.CV_8UC3, profile.ContrastValue, 0);

            var afterMean = Cv2.Mean(result).Val0;
            Debug.WriteLine($"[ApplyPreset] 修复前 mean={beforeMean:F0} → 修复后 mean={afterMean:F0}");

            var bmpResult = result.ToBitmap();
            result.Dispose();

            // 回调更新预览
            onPreview?.Invoke(bmpResult);

            return bmpResult;
        }
    }
}