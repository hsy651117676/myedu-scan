// Services/ImageProcessor.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace ScanTool.Services
{
    public class ImageProcessor : IDisposable
    {
        private readonly List<Mat> _undoStack = new List<Mat>();
        private readonly List<Mat> _redoStack = new List<Mat>();
        private Mat _current;
        private Mat _original;
        private Bitmap _cachedBitmap;

        private const int MaxUndoStack = 20;

        public Bitmap CurrentBitmap
        {
            get
            {
                if (_current == null) return null;
                _cachedBitmap?.Dispose();
                _cachedBitmap = _current.ToBitmap();
                return _cachedBitmap;
            }
        }

        public bool HasUndo => _undoStack.Count > 0;
        public bool HasRedo => _redoStack.Count > 0;
        public int UndoCount => _undoStack.Count;
        public int RedoCount => _redoStack.Count;

        public void LoadImage(Bitmap bmp)
        {
            _current?.Dispose();
            _original?.Dispose();
            _current = bmp.ToMat();
            _original = _current.Clone();
            _undoStack.ForEach(m => m.Dispose());
            _undoStack.Clear();
            _redoStack.ForEach(m => m.Dispose());
            _redoStack.Clear();
        }

        public void ReplaceImage(Bitmap bmp)
        {
            System.Diagnostics.Debug.WriteLine($"[ReplaceImage] 前: _current={_current != null}, Count={_undoStack.Count}");
            SaveState();
            System.Diagnostics.Debug.WriteLine($"[ReplaceImage] SaveState后: Count={_undoStack.Count}");
            _current?.Dispose();
            _current = bmp.ToMat();
            System.Diagnostics.Debug.WriteLine($"[ReplaceImage] 后: Count={_undoStack.Count}");
        }

        public void Clear()
        {
            _current?.Dispose();
            _original?.Dispose();
            _cachedBitmap?.Dispose();
            _current = null;
            _original = null;
            _cachedBitmap = null;
            _undoStack.ForEach(m => m.Dispose());
            _undoStack.Clear();
            _redoStack.ForEach(m => m.Dispose());
            _redoStack.Clear();
        }

        public void RestoreOriginal()
        {
            if (_original == null) return;
            _current?.Dispose();
            _current = _original.Clone();
            _undoStack.ForEach(m => m.Dispose());
            _undoStack.Clear();
            _redoStack.ForEach(m => m.Dispose());
            _redoStack.Clear();
        }

        public void SaveState()
        {
            System.Diagnostics.Debug.WriteLine($"[SaveState] _current={_current != null}, Size={_current?.Width}x{_current?.Height}, Empty={_current?.Empty()}");
            _undoStack.Add(_current.Clone());
            System.Diagnostics.Debug.WriteLine($"[SaveState] 后 Count={_undoStack.Count}");
            if (_undoStack.Count > MaxUndoStack)
            {
                _undoStack[0].Dispose();
                _undoStack.RemoveAt(0);
            }
            _redoStack.ForEach(m => m.Dispose());
            _redoStack.Clear();
        }

        public void Undo()
        {
            if (!HasUndo) return;
            _redoStack.Add(_current);
            _current = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);
        }

        public void Redo()
        {
            if (!HasRedo) return;
            _undoStack.Add(_current);
            _current = _redoStack[_redoStack.Count - 1];
            _redoStack.RemoveAt(_redoStack.Count - 1);
        }

        public void RotateLeft() { SaveState(); Cv2.Rotate(_current, _current, RotateFlags.Rotate90Counterclockwise); }
        public void RotateRight() { SaveState(); Cv2.Rotate(_current, _current, RotateFlags.Rotate90Clockwise); }
        public void FlipHorizontal() { SaveState(); Cv2.Flip(_current, _current, FlipMode.Y); }
        public void FlipVertical() { SaveState(); Cv2.Flip(_current, _current, FlipMode.X); }
        public void Brightness(int delta) { SaveState(); _current.ConvertTo(_current, MatType.CV_8UC3, 1.0, delta); }
        public void Contrast(double factor) { SaveState(); _current.ConvertTo(_current, MatType.CV_8UC3, factor, 0); }
        public void Grayscale() { SaveState(); using var gray = new Mat(); Cv2.CvtColor(_current, gray, ColorConversionCodes.BGR2GRAY); Cv2.CvtColor(gray, _current, ColorConversionCodes.GRAY2BGR); }
        public void Denoise() { SaveState(); var result = new Mat(); Cv2.MedianBlur(_current, result, 3); _current.Dispose(); _current = result; }

        public void AutoDeskew()
        {
            SaveState();
            using var gray = new Mat();
            Cv2.CvtColor(_current, gray, ColorConversionCodes.BGR2GRAY);
            using var edges = new Mat();
            Cv2.Canny(gray, edges, 50, 150);
            var lines = Cv2.HoughLines(edges, 1, Math.PI / 180, 200);
            if (lines.Length > 0)
            {
                var angles = new List<double>();
                for (int i = 0; i < Math.Min(lines.Length, 20); i++)
                {
                    var theta = lines[i].Theta;
                    var angle = theta * 180 / Math.PI;
                    if (angle > 85 && angle < 95) continue;
                    var skew = angle > 90 ? angle - 180 : angle;
                    if (Math.Abs(skew) < 30) angles.Add(skew);
                }
                if (angles.Count > 0)
                {
                    double avg = angles.Average();
                    var center = new Point2f(_current.Width / 2f, _current.Height / 2f);
                    using var rotMat = Cv2.GetRotationMatrix2D(center, avg, 1.0);
                    var result = new Mat();
                    Cv2.WarpAffine(_current, result, rotMat, _current.Size(),
                        InterpolationFlags.Cubic, BorderTypes.Constant, new Scalar(255, 255, 255));
                    _current.Dispose();
                    _current = result;
                }
            }
        }

        public void RemoveBlackBorder()
        {
            SaveState();
            using var gray = new Mat();
            Cv2.CvtColor(_current, gray, ColorConversionCodes.BGR2GRAY);

            // 用 Otsu 自动找阈值
            using var binary = new Mat();
            Cv2.Threshold(gray, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

            // 找所有非零像素的包围矩形
            var rect = Cv2.BoundingRect(binary);

            System.Diagnostics.Debug.WriteLine($"[RemoveBlackBorder] 原图={_current.Width}x{_current.Height}, 检测纸张区域={rect.X},{rect.Y},{rect.Width},{rect.Height}");

            // 如果检测到的区域几乎等于原图，说明没有黑边
            if (rect.X < 5 && rect.Y < 5 && rect.Width > _current.Width - 10 && rect.Height > _current.Height - 10)
                return;

            int margin = 3;
            int x = Math.Max(0, rect.X - margin);
            int y = Math.Max(0, rect.Y - margin);
            int w = Math.Min(_current.Width - x, rect.Width + margin * 2);
            int h = Math.Min(_current.Height - y, rect.Height + margin * 2);

            if (w > 50 && h > 50)
            {
                var cropRect = new OpenCvSharp.Rect(x, y, w, h);
                var cropped = new Mat(_current, cropRect);
                _current.Dispose();
                _current = cropped;
            }
        }

        public void ManualDeskew(double angle)
        {
            SaveState();
            var center = new Point2f(_current.Width / 2f, _current.Height / 2f);
            using var rotMat = Cv2.GetRotationMatrix2D(center, angle, 1.0);
            var result = new Mat();
            Cv2.WarpAffine(_current, result, rotMat, _current.Size(),
                InterpolationFlags.Cubic, BorderTypes.Constant, new Scalar(255, 255, 255));
            _current.Dispose();
            _current = result;
        }
        public void SetCurrentImage(Bitmap bmp)
        {
            _current?.Dispose();
            _current = bmp.ToMat();
            _cachedBitmap?.Dispose();
            _cachedBitmap = null;
        }

        public void Dispose()
        {
            _current?.Dispose();
            _original?.Dispose();
            _cachedBitmap?.Dispose();
            _undoStack.ForEach(m => m.Dispose());
            _redoStack.ForEach(m => m.Dispose());
        }
    }
}