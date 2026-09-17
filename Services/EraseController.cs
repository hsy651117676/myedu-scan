// Services/EraseController.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ScanTool.Services
{
    public enum EraserShape { Circle, Rectangle }

    public struct ErasePoint
    {
        public Point Location;
        public int Size;
        public EraserShape Shape;

        public ErasePoint(Point location, int size, EraserShape shape)
        {
            Location = location;
            Size = size;
            Shape = shape;
        }
    }

    public class EraseStroke
    {
        public List<ErasePoint> Points { get; } = new List<ErasePoint>();
        public Guid StrokeId { get; } = Guid.NewGuid();
        public DateTime StartTime { get; } = DateTime.Now;

        public void AddPoint(ErasePoint pt)
        {
            Points.Add(pt);
        }

        public bool ContainsPoint(Point pt)
        {
            foreach (var ep in Points)
            {
                int threshold = Math.Max(10, ep.Size / 2);
                if (Math.Abs(ep.Location.X - pt.X) <= threshold &&
                    Math.Abs(ep.Location.Y - pt.Y) <= threshold)
                {
                    return true;
                }
            }
            return false;
        }
    }

    public class EraseController
    {
        private readonly PictureBox _picBox;
        private readonly Action<string> _onStatusChanged;
        private bool _isActive;
        private Bitmap _snapshot;
        private bool _hasDrawn;

        private readonly List<EraseStroke> _strokes = new List<EraseStroke>();
        private EraseStroke _currentStroke;

        private Image _originalImage;
        private float _zoomFactor = 1.0f;
        private Point _panOffset = Point.Empty;

        private bool _applied = false;

        private int _currentEraserSize;
        private EraserShape _currentShape;

        private bool _isDrawing = false;
        private Point _lastScreenPoint;
        private bool _hasLastPoint;

        public bool IsActive => _isActive;
        public EraserShape Shape
        {
            get => _currentShape;
            private set
            {
                _currentShape = value;
                UserSettings.Set("EraserShape", value == EraserShape.Rectangle ? "Rectangle" : "Circle");
            }
        }
        public bool HasDrawn => _hasDrawn && !_applied;

        public EraseController(PictureBox picBox, Action<string> onStatusChanged = null)
        {
            _picBox = picBox;
            _onStatusChanged = onStatusChanged;

            _currentEraserSize = UserSettings.Get<int>("EraserSize");
            if (_currentEraserSize < 10) _currentEraserSize = 20;

            var savedShape = UserSettings.Get<string>("EraserShape");
            _currentShape = savedShape == "Rectangle" ? EraserShape.Rectangle : EraserShape.Circle;
        }

        public void SetViewportParams(Image originalImage, float zoomFactor, Point panOffset)
        {
            _originalImage = originalImage;
            _zoomFactor = zoomFactor;
            _panOffset = panOffset;
        }

        // 新增：更新视口参数（缩放/平移后同步）
        public void UpdateViewportParams(float zoomFactor, Point panOffset)
        {
            _zoomFactor = zoomFactor;
            _panOffset = panOffset;
        }

        private int ToImageSize(int screenSize)
        {
            if (_zoomFactor > 0.01f)
                return (int)Math.Round(screenSize / _zoomFactor);
            return screenSize;
        }

        private int ToScreenSize(int imageSize)
        {
            if (_zoomFactor > 0.01f)
                return (int)Math.Round(imageSize * _zoomFactor);
            return imageSize;
        }

        private Point ScreenToImage(Point screenPt)
        {
            if (_originalImage == null) return screenPt;

            try
            {
                int imgW = (int)(_originalImage.Width * _zoomFactor);
                int imgH = (int)(_originalImage.Height * _zoomFactor);
                int offsetX = (_picBox.ClientSize.Width - imgW) / 2 + _panOffset.X;
                int offsetY = (_picBox.ClientSize.Height - imgH) / 2 + _panOffset.Y;

                int imgX = (int)((screenPt.X - offsetX) / _zoomFactor);
                int imgY = (int)((screenPt.Y - offsetY) / _zoomFactor);

                return new Point(
                    Math.Max(0, Math.Min(imgX, _originalImage.Width - 1)),
                    Math.Max(0, Math.Min(imgY, _originalImage.Height - 1)));
            }
            catch
            {
                return screenPt;
            }
        }

        private Point ImageToScreen(Point imgPt)
        {
            if (_originalImage == null) return imgPt;

            try
            {
                int imgW = (int)(_originalImage.Width * _zoomFactor);
                int imgH = (int)(_originalImage.Height * _zoomFactor);
                int offsetX = (_picBox.ClientSize.Width - imgW) / 2 + _panOffset.X;
                int offsetY = (_picBox.ClientSize.Height - imgH) / 2 + _panOffset.Y;

                return new Point(
                    (int)(imgPt.X * _zoomFactor + offsetX),
                    (int)(imgPt.Y * _zoomFactor + offsetY));
            }
            catch
            {
                return imgPt;
            }
        }

        private void NotifyStatus()
        {
            if (!_isActive) return;
            string sn = _currentShape == EraserShape.Circle ? "圆形" : "矩形";
            _onStatusChanged?.Invoke($"擦除模式: {sn} | 大小: {_currentEraserSize}px | E切换形状 | +/-调整大小 | 笔画数: {_strokes.Count}");
        }

        public void Start()
        {
            _currentEraserSize = UserSettings.Get<int>("EraserSize");
            if (_currentEraserSize < 10) _currentEraserSize = 20;

            var savedShape = UserSettings.Get<string>("EraserShape");
            _currentShape = savedShape == "Rectangle" ? EraserShape.Rectangle : EraserShape.Circle;

            _isActive = true;
            _hasDrawn = false;
            _applied = false;
            _strokes.Clear();
            _currentStroke = null;
            _isDrawing = false;
            _hasLastPoint = false;

            _snapshot?.Dispose();
            _snapshot = _picBox.Image != null ? new Bitmap(_picBox.Image) : null;

            UpdateCursor();

            _picBox.MouseDown += OnMouseDown;
            _picBox.MouseMove += OnMouseMove;
            _picBox.MouseUp += OnMouseUp;
            _picBox.MouseEnter += OnMouseEnter;
            _picBox.Paint += OnPaint;

            NotifyStatus();
        }

        public void SizeUp()
        {
            _currentEraserSize = Math.Min(_currentEraserSize + 10, 200);
            UserSettings.Set("EraserSize", _currentEraserSize);
            UpdateCursor();
            NotifyStatus();
        }

        public void SizeDown()
        {
            _currentEraserSize = Math.Max(_currentEraserSize - 10, 10);
            UserSettings.Set("EraserSize", _currentEraserSize);
            UpdateCursor();
            NotifyStatus();
        }

        public void ToggleShape()
        {
            Shape = _currentShape == EraserShape.Circle ? EraserShape.Rectangle : EraserShape.Circle;
            UpdateCursor();
            NotifyStatus();
        }

        private void UpdateCursor()
        {
            int size = _currentEraserSize + 4;
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                if (_currentShape == EraserShape.Circle)
                    g.DrawEllipse(Pens.Red, 2, 2, _currentEraserSize, _currentEraserSize);
                else
                    g.DrawRectangle(Pens.Red, 2, 2, _currentEraserSize, _currentEraserSize);
            }
            _picBox.Cursor = new Cursor(bmp.GetHicon());
        }

        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (!_isActive || _applied || _picBox.Image == null) return;
            if (e.Button != MouseButtons.Left) return;

            _currentStroke = new EraseStroke();
            _strokes.Add(_currentStroke);
            _isDrawing = true;

            var imgPt = ScreenToImage(e.Location);
            int imageEraserSize = ToImageSize(_currentEraserSize);

            _currentStroke.AddPoint(new ErasePoint(imgPt, imageEraserSize, _currentShape));
            _hasDrawn = true;

            _lastScreenPoint = e.Location;
            _hasLastPoint = true;

            _picBox.Invalidate();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isActive || _applied) return;
            if (e.Button != MouseButtons.Left) return;
            if (_picBox.Image == null) return;
            if (_currentStroke == null || !_isDrawing) return;

            int imageEraserSize = ToImageSize(_currentEraserSize);

            if (_hasLastPoint)
            {
                float dx = e.X - _lastScreenPoint.X;
                float dy = e.Y - _lastScreenPoint.Y;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);

                if (dist > 100)
                {
                    Debug.WriteLine($"[Erase] 检测到异常跳跃: dist={dist:F1}px，跳过插值");
                }
                else
                {
                    int steps = (int)(dist / 2);
                    if (steps > 0)
                    {
                        for (int i = 1; i <= steps; i++)
                        {
                            int ix = _lastScreenPoint.X + (int)(dx * i / steps);
                            int iy = _lastScreenPoint.Y + (int)(dy * i / steps);
                            var imgPt = ScreenToImage(new Point(ix, iy));
                            _currentStroke.AddPoint(new ErasePoint(imgPt, imageEraserSize, _currentShape));
                        }
                    }
                }
            }

            var currentImgPt = ScreenToImage(e.Location);
            _currentStroke.AddPoint(new ErasePoint(currentImgPt, imageEraserSize, _currentShape));

            _lastScreenPoint = e.Location;
            _hasLastPoint = true;

            _picBox.Invalidate();
        }

        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            if (_currentStroke != null)
            {
                Debug.WriteLine($"[Erase] 结束笔画: StrokeId={_currentStroke.StrokeId}, 点数={_currentStroke.Points.Count}");
            }

            _currentStroke = null;
            _isDrawing = false;
            _hasLastPoint = false;

            NotifyStatus();
        }

        private void OnPaint(object sender, PaintEventArgs e)
        {
            if (!_isActive || _strokes.Count == 0) return;

            foreach (var stroke in _strokes)
            {
                if (stroke.Points.Count > 0)
                {
                    DrawStrokeOnScreen(e.Graphics, stroke);
                }
            }
        }

        private void DrawStrokeOnScreen(Graphics g, EraseStroke stroke)
        {
            if (stroke.Points.Count == 0) return;

            if (stroke.Points.Count == 1)
            {
                var ep = stroke.Points[0];
                var screenPt = ImageToScreen(ep.Location);
                int screenSize = ToScreenSize(ep.Size);
                int half = screenSize / 2;

                using var brush = new SolidBrush(Color.White);
                if (ep.Shape == EraserShape.Circle)
                {
                    g.FillEllipse(brush, screenPt.X - half, screenPt.Y - half, screenSize, screenSize);
                }
                else
                {
                    g.FillRectangle(brush, screenPt.X - half, screenPt.Y - half, screenSize, screenSize);
                }
                return;
            }

            int startIdx = 0;
            for (int i = 1; i <= stroke.Points.Count; i++)
            {
                bool isLast = (i == stroke.Points.Count);
                bool sizeChanged = !isLast && stroke.Points[i].Size != stroke.Points[i - 1].Size;
                bool shapeChanged = !isLast && stroke.Points[i].Shape != stroke.Points[i - 1].Shape;

                if (isLast || sizeChanged || shapeChanged)
                {
                    DrawStrokeSegmentOnScreen(g, stroke, startIdx, i - 1);
                    startIdx = i;
                }
            }
        }

        private void DrawStrokeSegmentOnScreen(Graphics g, EraseStroke stroke, int startIdx, int endIdx)
        {
            int count = endIdx - startIdx + 1;
            if (count < 2) return;

            int imageSize = stroke.Points[startIdx].Size;
            int screenSize = ToScreenSize(imageSize);
            EraserShape shape = stroke.Points[startIdx].Shape;

            var screenPts = new Point[count];
            for (int i = 0; i < count; i++)
            {
                screenPts[i] = ImageToScreen(stroke.Points[startIdx + i].Location);
            }

            using var pen = new Pen(Color.White, screenSize);

            if (shape == EraserShape.Circle)
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
            }
            else
            {
                pen.StartCap = LineCap.Square;
                pen.EndCap = LineCap.Square;
                pen.LineJoin = LineJoin.Miter;
            }

            g.DrawLines(pen, screenPts);
        }

        private void OnMouseEnter(object sender, EventArgs e)
        {
            if (_isActive)
                UpdateCursor();
        }

        public Bitmap Stop()
        {
            _isActive = false;
            _currentStroke = null;
            _isDrawing = false;
            _hasLastPoint = false;

            _picBox.Cursor = Cursors.Default;
            _picBox.MouseDown -= OnMouseDown;
            _picBox.MouseMove -= OnMouseMove;
            _picBox.MouseUp -= OnMouseUp;
            _picBox.MouseEnter -= OnMouseEnter;
            _picBox.Paint -= OnPaint;
            _picBox.Invalidate();

            Debug.WriteLine($"[Erase] 停止擦除模式，总笔画数={_strokes.Count}");

            return _snapshot;
        }

        public Bitmap ApplyToOriginal(Bitmap original)
        {
            if (!_hasDrawn || _applied || original == null || _strokes.Count == 0)
                return new Bitmap(original);

            _applied = true;
            var result = new Bitmap(original);

            using (var g = Graphics.FromImage(result))
            {
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.CompositingMode = CompositingMode.SourceCopy;

                foreach (var stroke in _strokes)
                {
                    if (stroke.Points.Count > 0)
                    {
                        DrawStrokeOnImage(g, stroke);
                    }
                }
            }

            Debug.WriteLine($"[Erase] 应用擦除完成，总笔画数={_strokes.Count}");

            return result;
        }

        private void DrawStrokeOnImage(Graphics g, EraseStroke stroke)
        {
            if (stroke.Points.Count == 0) return;

            if (stroke.Points.Count == 1)
            {
                var ep = stroke.Points[0];
                int imageSize = ep.Size;
                int half = imageSize / 2;

                using var brush = new SolidBrush(Color.White);
                if (ep.Shape == EraserShape.Circle)
                {
                    g.FillEllipse(brush, ep.Location.X - half, ep.Location.Y - half, imageSize, imageSize);
                }
                else
                {
                    g.FillRectangle(brush, ep.Location.X - half, ep.Location.Y - half, imageSize, imageSize);
                }
                return;
            }

            int startIdx = 0;
            for (int i = 1; i <= stroke.Points.Count; i++)
            {
                bool isLast = (i == stroke.Points.Count);
                bool sizeChanged = !isLast && stroke.Points[i].Size != stroke.Points[i - 1].Size;
                bool shapeChanged = !isLast && stroke.Points[i].Shape != stroke.Points[i - 1].Shape;

                if (isLast || sizeChanged || shapeChanged)
                {
                    DrawStrokeSegmentOnImage(g, stroke, startIdx, i - 1);
                    startIdx = i;
                }
            }
        }

        private void DrawStrokeSegmentOnImage(Graphics g, EraseStroke stroke, int startIdx, int endIdx)
        {
            int count = endIdx - startIdx + 1;
            if (count < 2) return;

            int imageSize = stroke.Points[startIdx].Size;
            EraserShape shape = stroke.Points[startIdx].Shape;

            using var path = new GraphicsPath();
            var pts = new Point[count];
            for (int i = 0; i < count; i++)
            {
                pts[i] = stroke.Points[startIdx + i].Location;
            }

            if (pts.Length > 3)
                pts = SimplifyPath(pts, 1.0f);

            if (pts.Length >= 2)
                path.AddLines(pts);

            using var pen = new Pen(Color.White, imageSize);

            if (shape == EraserShape.Circle)
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
            }
            else
            {
                pen.StartCap = LineCap.Square;
                pen.EndCap = LineCap.Square;
                pen.LineJoin = LineJoin.Miter;
            }

            g.DrawPath(pen, path);
        }

        private Point[] SimplifyPath(Point[] points, float tolerance)
        {
            if (points.Length <= 2) return points;

            float maxDist = 0;
            int maxIndex = 0;
            int end = points.Length - 1;

            for (int i = 1; i < end; i++)
            {
                float dist = PerpendicularDistance(points[i], points[0], points[end]);
                if (dist > maxDist)
                {
                    maxDist = dist;
                    maxIndex = i;
                }
            }

            if (maxDist > tolerance)
            {
                int leftLen = maxIndex + 1;
                var leftPoints = new Point[leftLen];
                Array.Copy(points, 0, leftPoints, 0, leftLen);

                int rightLen = points.Length - maxIndex;
                var rightPoints = new Point[rightLen];
                Array.Copy(points, maxIndex, rightPoints, 0, rightLen);

                var left = SimplifyPath(leftPoints, tolerance);
                var right = SimplifyPath(rightPoints, tolerance);

                var result = new Point[left.Length + right.Length - 1];
                Array.Copy(left, 0, result, 0, left.Length);
                Array.Copy(right, 1, result, left.Length, right.Length - 1);
                return result;
            }
            else
            {
                return new Point[] { points[0], points[end] };
            }
        }

        private float PerpendicularDistance(Point point, Point lineStart, Point lineEnd)
        {
            float dx = lineEnd.X - lineStart.X;
            float dy = lineEnd.Y - lineStart.Y;
            float mag = (float)Math.Sqrt(dx * dx + dy * dy);

            if (mag < 0.0001f) return 0;

            float u = ((point.X - lineStart.X) * dx + (point.Y - lineStart.Y) * dy) / (mag * mag);

            float ix, iy;
            if (u < 0)
            {
                ix = lineStart.X;
                iy = lineStart.Y;
            }
            else if (u > 1)
            {
                ix = lineEnd.X;
                iy = lineEnd.Y;
            }
            else
            {
                ix = lineStart.X + u * dx;
                iy = lineStart.Y + u * dy;
            }

            float distX = point.X - ix;
            float distY = point.Y - iy;
            return (float)Math.Sqrt(distX * distX + distY * distY);
        }

        public Rectangle GetEraseBounds()
        {
            if (_strokes.Count == 0) return Rectangle.Empty;

            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;
            int maxImageSize = 0;

            foreach (var stroke in _strokes)
            {
                foreach (var ep in stroke.Points)
                {
                    if (ep.Location.X < minX) minX = ep.Location.X;
                    if (ep.Location.Y < minY) minY = ep.Location.Y;
                    if (ep.Location.X > maxX) maxX = ep.Location.X;
                    if (ep.Location.Y > maxY) maxY = ep.Location.Y;
                    if (ep.Size > maxImageSize) maxImageSize = ep.Size;
                }
            }

            int half = maxImageSize / 2 + 1;
            return new Rectangle(
                Math.Max(0, minX - half),
                Math.Max(0, minY - half),
                Math.Min(_originalImage?.Width ?? 0, maxX - minX + maxImageSize + 2),
                Math.Min(_originalImage?.Height ?? 0, maxY - minY + maxImageSize + 2));
        }
    }
}