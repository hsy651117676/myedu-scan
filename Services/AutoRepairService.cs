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
                double gText = GetChannelMedian(mat, 1, 0.0, 0.05);
                double bText = GetChannelMedian(mat, 2, 0.0, 0.05);

                bool hasStamp = DetectStamp(mat);
                double skewAngle = DetectSkewAngle(mat);

                profile.BackgroundBrightness = rBg;

                Debug.WriteLine($"[AutoRepair] === 诊断开始 ===");
                Debug.WriteLine($"[AutoRepair] 背景 R:{rBg:F0} G:{gBg:F0} B:{bBg:F0} V:{vBg:F0}");
                Debug.WriteLine($"[AutoRepair] 文字 R:{rText:F0} G:{gText:F0} B:{bText:F0}");
                Debug.WriteLine($"[AutoRepair] 公章: {(hasStamp ? "有" : "无")}");

                if (Math.Abs(skewAngle) > 1.5)
                {
                    profile.AutoDeskew = true;
                    Debug.WriteLine($"[AutoRepair] 倾斜: {skewAngle:F1}°");
                }

                if (DetectBlackBorder(mat))
                {
                    profile.RemoveBlackBorder = true;
                    Debug.WriteLine($"[AutoRepair] 黑边: 有");
                }

                if (DetectBindingHoles(mat))
                {
                    profile.FillBindingHoles = true;
                    Debug.WriteLine($"[AutoRepair] 装订孔: 有");
                }

                // ========== 决策 ==========
                double fBase = 1.03;
                bool needBgRemove = false;

                if (vBg < 180)
                {
                    profile.Diagnosis = "发黑";
                    needBgRemove = true;
                    fBase = 1.15;
                }
                else if (rBg < 190)
                {
                    profile.Diagnosis = "暗黄";
                    needBgRemove = true;
                    fBase = 1.12;
                }
                else if (rBg < 225)
                {
                    profile.Diagnosis = "发黄";
                    needBgRemove = true;
                    fBase = 1.08;
                }
                else if (vBg < 210)
                {
                    profile.Diagnosis = "偏暗";
                    fBase = 1.06;
                }
                else if (rBg < 240)
                {
                    profile.Diagnosis = "微黄";
                    fBase = 1.05;
                }
                else
                {
                    profile.Diagnosis = "正常";
                    fBase = 1.03;
                }

                // 文字偏淡，加大 F
                if (rText > 100)
                    fBase += 0.03;

                profile.NeedBackgroundRemove = needBgRemove;
                profile.Contrast = true;
                profile.ContrastValue = fBase;

                Debug.WriteLine($"[AutoRepair] 判定: {(hasStamp ? "有公章 | " : "")}{profile.Diagnosis} → F={fBase:F2}" +
                    (needBgRemove ? " +背景减除(文字保护)" : ""));
            }

            return profile;
        }

        // ==================== 执行入口 ====================

        public static Bitmap Execute(Bitmap bmp, RepairProfile profile)
        {
            if (profile == null || profile.Name == "不自动修复")
                return new Bitmap(bmp);

            Mat result = null;
            Mat textMask = null;
            try
            {
                result = bmp.ToMat();

                // 生成文字遮罩（在所有处理之前）
                textMask = CreateTextMask(result);

                if (profile.AutoDeskew)
                {
                    var tmp = Deskew(result);
                    result.Dispose();
                    result = tmp;
                    // 倾斜后重新生成遮罩
                    textMask.Dispose();
                    textMask = CreateTextMask(result);
                }

                if (profile.FillBindingHoles)
                    FillBindingHoles(result);

                if (profile.RemoveBlackBorder)
                {
                    var tmp = RemoveBlackBorder(result);
                    result.Dispose();
                    result = tmp;
                    textMask.Dispose();
                    textMask = CreateTextMask(result);
                }

                if (profile.NeedBackgroundRemove)
                {
                    Debug.WriteLine($"[AutoRepair] 执行背景减除(文字保护)");
                    var tmp = RemoveBackgroundProtected(result, textMask);
                    result.Dispose();
                    result = tmp;
                }

                if (profile.Contrast)
                {
                    Debug.WriteLine($"[AutoRepair] 执行 F: {profile.ContrastValue:F2}");
                    result.ConvertTo(result, -1, profile.ContrastValue, 128 * (1 - profile.ContrastValue));
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
            }
        }

        // ==================== 文字遮罩 ====================

        private static Mat CreateTextMask(Mat src)
        {
            using var gray = new Mat();
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

            // 灰度 < 120 的是文字（保护蓝色笔）
            var mask = new Mat();
            Cv2.Threshold(gray, mask, 120, 255, ThresholdTypes.BinaryInv);

            using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(3, 3));
            Cv2.Dilate(mask, mask, kernel, iterations: 1);

            return mask;
        }

        // ==================== 背景减除（文字保护） ====================

        private static Mat RemoveBackgroundProtected(Mat src, Mat textMask)
        {
            // 对全图做背景减除
            var cleaned = RemoveBackground(src);

            // 把文字区域盖回原图
            using var textMaskInv = new Mat();
            Cv2.BitwiseNot(textMask, textMaskInv);
            src.CopyTo(cleaned, textMask);  // 文字区域用原图

            return cleaned;
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
            var channels = Cv2.Split(src);
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

            foreach (var c in channels) c.Dispose();
            return median;
        }

        // ==================== 背景减除 ====================

        private static Mat RemoveBackground(Mat src)
        {
            var channels = Cv2.Split(src);
            var resultChannels = new Mat[3];

            for (int i = 0; i < 3; i++)
            {
                resultChannels[i] = new Mat();

                int kernelSize = Math.Min(src.Cols, src.Rows) / 15;
                if (kernelSize % 2 == 0) kernelSize++;
                if (kernelSize < 31) kernelSize = 31;
                if (kernelSize > 101) kernelSize = 101;

                using var bg = new Mat();
                Cv2.Blur(channels[i], bg, new OpenCvSharp.Size(kernelSize, kernelSize));

                using var srcFloat = new Mat();
                channels[i].ConvertTo(srcFloat, MatType.CV_32F);
                using var bgFloat = new Mat();
                bg.ConvertTo(bgFloat, MatType.CV_32F);

                using var diff = new Mat();
                Cv2.Subtract(srcFloat, bgFloat, diff);
                Cv2.Add(diff, new Scalar(245), diff);
                diff.ConvertTo(resultChannels[i], MatType.CV_8U);
            }

            var result = new Mat();
            Cv2.Merge(resultChannels, result);

            foreach (var c in channels) c.Dispose();
            foreach (var c in resultChannels) c.Dispose();

            return result;
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
            int w = src.Width, h = src.Height, margin = 10;
            double top = Cv2.Mean(gray[new OpenCvSharp.Rect(0, 0, w, margin)]).Val0;
            double bottom = Cv2.Mean(gray[new OpenCvSharp.Rect(0, h - margin, w, margin)]).Val0;
            double left = Cv2.Mean(gray[new OpenCvSharp.Rect(0, 0, margin, h)]).Val0;
            double right = Cv2.Mean(gray[new OpenCvSharp.Rect(w - margin, 0, margin, h)]).Val0;
            return top < 35 || bottom < 35 || left < 35 || right < 35;
        }

        private static Mat RemoveBlackBorder(Mat src)
        {
            using var gray = ToGray(src);
            using var binary = new Mat();
            Cv2.Threshold(gray, binary, 30, 255, ThresholdTypes.Binary);
            Cv2.FindContours(binary, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
            if (contours.Length == 0) return src.Clone();
            int bestIdx = 0;
            double maxArea = 0;
            for (int i = 0; i < contours.Length; i++)
            {
                double area = Cv2.ContourArea(contours[i]);
                if (area > maxArea) { maxArea = area; bestIdx = i; }
            }
            var rect = Cv2.BoundingRect(contours[bestIdx]);
            int m = 2;
            rect.X = Math.Max(0, rect.X - m);
            rect.Y = Math.Max(0, rect.Y - m);
            rect.Width = Math.Min(src.Width - rect.X, rect.Width + m * 2);
            rect.Height = Math.Min(src.Height - rect.Y, rect.Height + m * 2);
            return new Mat(src, rect).Clone();
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
            if (src.Channels() == 3)
            {
                var gray = new Mat();
                Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
                return gray;
            }
            return src.Clone();
        }
    }
}