// Services/ImageEditor.cs
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ScanTool.Services
{
    public class ImageEditor
    {
        private readonly PictureBox _picBox;
        private Image _originalImage;
        private float _zoomFactor = 1.0f;
        private Point _panOffset = Point.Empty;
        private DateTime _lastPanTime = DateTime.MinValue;

        private bool _isCropping;
        private Point _cropStart;
        private Rectangle _cropRect;

        private bool _isDeskewing;
        private Point _deskewStart;
        private Point _deskewEnd;
        private Action<double> _onDeskewComplete;

        private bool _isErasing;
        private int _eraserSize = 20;

        private bool _isPanning;
        private Point _panStart;

        public ImageEditor(PictureBox picBox)
        {
            _picBox = picBox;
            _picBox.MouseWheel += OnMouseWheel;
        }

        // ==================== 缩放（以鼠标为中心） ====================

        public void ZoomIn()
        {
            var mousePos = _picBox.PointToClient(Cursor.Position);
            float oldZoom = _zoomFactor;
            _zoomFactor = Math.Min(_zoomFactor * 1.25f, 10f);
            AdjustPanForZoom(mousePos, oldZoom);
            ApplyZoom();
        }

        public void ZoomOut()
        {
            var mousePos = _picBox.PointToClient(Cursor.Position);
            float oldZoom = _zoomFactor;
            _zoomFactor = Math.Max(_zoomFactor / 1.25f, 0.1f);
            AdjustPanForZoom(mousePos, oldZoom);
            ApplyZoom();
        }

        public void FitToScreen()
        {
            _panOffset = Point.Empty;
            if (_originalImage == null) return;
            try
            {
                float wRatio = (float)_picBox.ClientSize.Width / _originalImage.Width;
                float hRatio = (float)_picBox.ClientSize.Height / _originalImage.Height;
                _zoomFactor = Math.Min(wRatio, hRatio);
                ApplyZoom();
            }
            catch { }
        }

        public void ResetZoom()
        {
            _panOffset = Point.Empty;
            _zoomFactor = 1.0f;
            ApplyZoom();
        }

        private void OnMouseWheel(object sender, MouseEventArgs e)
        {
            if (Control.ModifierKeys == Keys.Control)
            {
                if (e.Delta > 0) ZoomIn();
                else ZoomOut();
            }
        }

        private void AdjustPanForZoom(Point mousePos, float oldZoom)
        {
            float ratio = _zoomFactor / oldZoom;
            _panOffset.X = (int)(mousePos.X - ratio * (mousePos.X - _panOffset.X));
            _panOffset.Y = (int)(mousePos.Y - ratio * (mousePos.Y - _panOffset.Y));
        }

        private void ApplyZoom()
        {
            if (_originalImage == null) return;
            try
            {
                int imgW = (int)(_originalImage.Width * _zoomFactor);
                int imgH = (int)(_originalImage.Height * _zoomFactor);
                int boxW = _picBox.ClientSize.Width;
                int boxH = _picBox.ClientSize.Height;

                var bmp = new Bitmap(boxW, boxH);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.FromArgb(245, 245, 245));
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    int x = (boxW - imgW) / 2 + _panOffset.X;
                    int y = (boxH - imgH) / 2 + _panOffset.Y;
                    g.DrawImage(_originalImage, x, y, imgW, imgH);
                }
                var old = _picBox.Image;
                _picBox.Image = bmp;
                old?.Dispose();
            }
            catch { }
        }

        public void SetImage(Image img)
        {
            if (img == null)
            {
                _originalImage?.Dispose();
                _originalImage = null;
                _picBox.Image = null;
                return;
            }
            _originalImage?.Dispose();
            _originalImage = new Bitmap(img);
            _zoomFactor = 1.0f;
            _panOffset = Point.Empty;

            if (_picBox.IsHandleCreated && _picBox.ClientSize.Width > 0)
                FitToScreen();
            else
                _picBox.Image = new Bitmap(_originalImage);
        }

        // ==================== 平移 ====================

        public void StartPan()
        {
            _isPanning = true;
            _picBox.Cursor = Cursors.Hand;
            _picBox.MouseDown += OnPanMouseDown;
            _picBox.MouseMove += OnPanMouseMove;
            _picBox.MouseUp += OnPanMouseUp;
        }

        private void OnPanMouseDown(object sender, MouseEventArgs e) { _panStart = e.Location; }
        private void OnPanMouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanning && e.Button == MouseButtons.Left)
            {
                if ((DateTime.Now - _lastPanTime).TotalMilliseconds < 30) return;
                _lastPanTime = DateTime.Now;
                _panOffset.X += e.X - _panStart.X;
                _panOffset.Y += e.Y - _panStart.Y;
                _panStart = e.Location;
                ApplyZoom();
            }
        }
        private void OnPanMouseUp(object sender, MouseEventArgs e) { StopPan(); }

        public void StopPan()
        {
            _isPanning = false;
            _picBox.Cursor = Cursors.Default;
            _picBox.MouseDown -= OnPanMouseDown;
            _picBox.MouseMove -= OnPanMouseMove;
            _picBox.MouseUp -= OnPanMouseUp;
        }

        // ==================== 裁剪 ====================

        public void StartCrop()
        {
            _isCropping = true;
            _picBox.Cursor = Cursors.Cross;
            _picBox.MouseDown += OnCropMouseDown;
            _picBox.MouseMove += OnCropMouseMove;
            _picBox.MouseUp += OnCropMouseUp;
            _picBox.Paint += OnCropPaint;
        }

        private void OnCropMouseDown(object sender, MouseEventArgs e) { _cropStart = e.Location; }
        private void OnCropMouseMove(object sender, MouseEventArgs e)
        {
            if (_isCropping && e.Button == MouseButtons.Left)
            {
                _cropRect = new Rectangle(Math.Min(_cropStart.X, e.X), Math.Min(_cropStart.Y, e.Y),
                    Math.Abs(e.X - _cropStart.X), Math.Abs(e.Y - _cropStart.Y));
                _picBox.Invalidate();
            }
        }
        private void OnCropPaint(object sender, PaintEventArgs e)
        {
            if (_isCropping && _cropRect.Width > 0)
            { using var pen = new Pen(Color.Red, 2) { DashStyle = DashStyle.Dash }; e.Graphics.DrawRectangle(pen, _cropRect); }
        }
        private void OnCropMouseUp(object sender, MouseEventArgs e)
        {
            var imgStart = ScreenToImage(_cropStart);
            var imgEnd = ScreenToImage(e.Location);
            var imgRect = new Rectangle(
                Math.Min(imgStart.X, imgEnd.X),
                Math.Min(imgStart.Y, imgEnd.Y),
                Math.Abs(imgEnd.X - imgStart.X),
                Math.Abs(imgEnd.Y - imgStart.Y));
            StopCrop();
            CropCompleted?.Invoke(imgRect);
        }
        public event Action<Rectangle> CropCompleted;

        public void StopCrop()
        {
            _isCropping = false;
            _picBox.Cursor = Cursors.Default;
            _picBox.MouseDown -= OnCropMouseDown;
            _picBox.MouseMove -= OnCropMouseMove;
            _picBox.MouseUp -= OnCropMouseUp;
            _picBox.Paint -= OnCropPaint;
            _picBox.Invalidate();
        }

        // ==================== 手动纠偏 ====================

        public void StartDeskew(Action<double> onComplete)
        {
            _onDeskewComplete = onComplete;
            _isDeskewing = true;
            _picBox.Cursor = Cursors.Cross;
            _picBox.MouseDown += OnDeskewMouseDown;
            _picBox.MouseMove += OnDeskewMouseMove;
            _picBox.MouseUp += OnDeskewMouseUp;
            _picBox.Paint += OnDeskewPaint;
        }

        private void OnDeskewMouseDown(object sender, MouseEventArgs e)
        {
            _deskewStart = e.Location;
            _deskewEnd = e.Location;
        }

        private void OnDeskewMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDeskewing && e.Button == MouseButtons.Left)
            {
                _deskewEnd = e.Location;
                _picBox.Invalidate();
            }
        }

        private void OnDeskewPaint(object sender, PaintEventArgs e)
        {
            if (_isDeskewing)
            {
                using var pen = new Pen(Color.Lime, 2);
                e.Graphics.DrawLine(pen, _deskewStart, _deskewEnd);
            }
        }

        private void OnDeskewMouseUp(object sender, MouseEventArgs e)
        {
            // 转换到图像坐标再计算角度
            var imgStart = ScreenToImage(_deskewStart);
            var imgEnd = ScreenToImage(_deskewEnd);

            double dx = imgEnd.X - imgStart.X;
            double dy = imgEnd.Y - imgStart.Y;

            if (Math.Abs(dx) < 5 && Math.Abs(dy) < 5)
            {
                // 线太短，忽略
                StopDeskew();
                return;
            }

            // 计算角度：线的角度，需要纠正到水平
            double angle = Math.Atan2(dy, dx) * 180 / Math.PI;
            // 纸张边缘应该是水平的，所以纠正角 = -angle
            _onDeskewComplete?.Invoke(angle);
            StopDeskew();
        }

        public void StopDeskew()
        {
            _isDeskewing = false;
            _picBox.Cursor = Cursors.Default;
            _picBox.MouseDown -= OnDeskewMouseDown;
            _picBox.MouseMove -= OnDeskewMouseMove;
            _picBox.MouseUp -= OnDeskewMouseUp;
            _picBox.Paint -= OnDeskewPaint;
            _picBox.Invalidate();
        }

        // ==================== 擦除 ====================

        public void StartErase(int size = 20)
        {
            _eraserSize = size;
            _isErasing = true;
            _picBox.MouseDown += OnEraseMouseDown;
            _picBox.MouseMove += OnEraseMouseMove;
            _picBox.MouseUp += OnEraseMouseUp;
        }

        private void OnEraseMouseDown(object sender, MouseEventArgs e) { EraseAt(e.Location); }
        private void OnEraseMouseMove(object sender, MouseEventArgs e)
        { if (_isErasing && e.Button == MouseButtons.Left) EraseAt(e.Location); }
        private void OnEraseMouseUp(object sender, MouseEventArgs e) { StopErase(); EraseCompleted?.Invoke(); }

        private void EraseAt(Point location)
        {
            if (_picBox.Image == null) return;
            using var g = Graphics.FromImage(_picBox.Image);
            g.CompositingMode = CompositingMode.SourceCopy;
            using var brush = new SolidBrush(Color.White);
            g.FillEllipse(brush, location.X - _eraserSize / 2, location.Y - _eraserSize / 2, _eraserSize, _eraserSize);
            _picBox.Invalidate();
        }

        public void StopErase()
        {
            _isErasing = false;
            _picBox.MouseDown -= OnEraseMouseDown;
            _picBox.MouseMove -= OnEraseMouseMove;
            _picBox.MouseUp -= OnEraseMouseUp;
        }
        public event Action EraseCompleted;

        // ==================== 坐标转换 ====================

        private Point ScreenToImage(Point screenPt)
        {
            if (_originalImage == null) return screenPt;
            float zoom = _zoomFactor;
            int imgW = (int)(_originalImage.Width * zoom);
            int imgH = (int)(_originalImage.Height * zoom);
            int offsetX = (_picBox.ClientSize.Width - imgW) / 2 + _panOffset.X;
            int offsetY = (_picBox.ClientSize.Height - imgH) / 2 + _panOffset.Y;
            float imgX = (screenPt.X - offsetX) / zoom;
            float imgY = (screenPt.Y - offsetY) / zoom;
            return new Point((int)imgX, (int)imgY);
        }
    }
}