// Services/AutoRepairService.cs
using System;
using System.Collections.Generic;
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
                using var gray = ToGray(mat);
                double bgBefore = Cv2.Mean(gray).Val0;

                // 背景已经非常白，跳过所有处理
                if (bgBefore > 240)
                    return profile;

                // 尝试 R+F，看效果
                bool rfGood = false;
                bool needSecondF = false;
                using (var test = mat.Clone())
                {
                    test.ConvertTo(test, -1, 1, 20);   // 亮度+20
                    test.ConvertTo(test, -1, 1.2, 0);  // 对比度 1.2
                    double bgAfter = Cv2.Mean(ToGray(test)).Val0;

                    if (bgAfter >= bgBefore * 0.9)
                    {
                        rfGood = true;

                        // 二次判断：对比度还低就再 F 一次
                        Cv2.MinMaxLoc(ToGray(test), out double minVal, out double maxVal);
                        double contrastAfter = (maxVal - minVal) / 255.0;
                        if (contrastAfter < 0.25)
                            needSecondF = true;
                    }
                }

                if (rfGood)
                {
                    profile.Brightness = true;
                    profile.BrightnessValue = 20;
                    profile.Contrast = true;
                    profile.ContrastValue = 1.2;
                    profile.ContrastTwice = needSecondF;
                }

                // 倾斜
                double skewAngle = DetectSkewAngle(mat);
                if (Math.Abs(skewAngle) > 1.5)
                    profile.AutoDeskew = true;

                // 黑边
                if (DetectBlackBorder(mat))
                    profile.RemoveBlackBorder = true;

                // 装订孔
                if (DetectBindingHoles(mat))
                    profile.FillBindingHoles = true;
            }

            return profile;
        }

        // ==================== 执行入口 ====================

        public static Bitmap Execute(Bitmap bmp, RepairProfile profile)
        {
            if (profile == null || profile.Name == "不自动修复")
                return new Bitmap(bmp);

            var result = bmp.ToMat();

            // 倾斜矫正
            if (profile.AutoDeskew)
                result = Deskew(result);

            // 装订孔
            if (profile.FillBindingHoles)
                FillBindingHoles(result);

            // 去黑边
            if (profile.RemoveBlackBorder)
                result = RemoveBlackBorder(result);

            // 亮度
            if (profile.Brightness)
                Brightness(result, profile.BrightnessValue);

            // 对比度（可能需要两次）
            if (profile.Contrast)
            {
                Contrast(result, profile.ContrastValue);
                if (profile.ContrastTwice)
                    Contrast(result, profile.ContrastValue);
            }

            var output = result.ToBitmap();
            result.Dispose();
            return output;
        }

        // ==================== 倾斜检测与矫正 ====================

        private static double DetectSkewAngle(Mat src)
        {
            using var gray = ToGray(src);
            // 缩小加速
            using var small = new Mat();
            Cv2.Resize(gray, small, new OpenCvSharp.Size(gray.Width / 4, gray.Height / 4));

            using var edges = new Mat();
            Cv2.Canny(small, edges, 50, 150);
            var lines = Cv2.HoughLines(edges, 1, Math.PI / 180, 150); // 提高阈值过滤短线

            if (lines.Length == 0) return 0;

            var angles = new List<double>();
            for (int i = 0; i < Math.Min(lines.Length, 10); i++)
            {
                double a = lines[i].Theta * 180 / Math.PI;
                if (a > 85 && a < 95) continue;  // 跳过竖线
                double s = a > 90 ? a - 180 : a;
                if (Math.Abs(s) < 15) angles.Add(s); // 只取 ±15° 内
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

        // ==================== 亮度/对比度 ====================

        private static void Brightness(Mat src, int value)
        {
            src.ConvertTo(src, -1, 1, value);
        }

        private static void Contrast(Mat src, double factor)
        {
            src.ConvertTo(src, -1, factor, 0);
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
                if (area > 200 && area < 15000)
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
                if (area > 200 && area < 15000)
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