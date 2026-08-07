// Services/ImageEditor.cs
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ScanTool.Services
{
    public class ImageEditor
    {
        private readonly PictureBox _picBox;
        private readonly Action<string> _onStatusChanged;
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

        private bool _isPanning;
        private Point _panStart;
        public float ZoomFactor => _zoomFactor;
        public Point PanOffset => _panOffset;
        public ImageEditor(PictureBox picBox, Action<string> onStatusChanged = null)
        {
            _picBox = picBox;
            _onStatusChanged = onStatusChanged;
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
            catch (Exception ex)
            {
                Debug.WriteLine($"[ImageEditor.FitToScreen] 错误: {ex.Message}");
                // 降级处理：直接设置图片
                try
                {
                    _picBox.Image = new Bitmap(_originalImage);
                }
                catch { }
            }
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

                // 如果 PictureBox 尺寸为 0，直接设置原始图
                if (boxW == 0 || boxH == 0)
                {
                    _picBox.Image = new Bitmap(_originalImage);
                    return;
                }

                var bmp = new Bitmap(boxW, boxH);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.White);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    int x = (boxW - imgW) / 2 + _panOffset.X;
                    int y = (boxH - imgH) / 2 + _panOffset.Y;
                    g.DrawImage(_originalImage, x, y, imgW, imgH);
                }
                var old = _picBox.Image;
                _picBox.Image = bmp;
                old?.Dispose();

                // 强制刷新
                _picBox.Invalidate();
                _picBox.Update();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ImageEditor.ApplyZoom] 错误: {ex.Message}");
                try
                {
                    _picBox.Image = new Bitmap(_originalImage);
                }
                catch { }
            }
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

            // 移除条件判断，直接设置图片并强制刷新
            if (_picBox.IsHandleCreated && _picBox.ClientSize.Width > 0 && _picBox.ClientSize.Height > 0)
            {
                FitToScreen();
            }
            else
            {
                _picBox.Image = new Bitmap(_originalImage);
            }

            // 强制刷新 PictureBox
            _picBox.Invalidate();
            _picBox.Update();
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
            _onStatusChanged?.Invoke("裁剪模式: 拖动选择区域 | 双击确认 | Esc取消");
        }

        private void OnCropMouseDown(object sender, MouseEventArgs e) { _cropStart = e.Location; }
        private void OnCropMouseMove(object sender, MouseEventArgs e)
        {
            if (_isCropping && e.Button == MouseButtons.Left)
            {
                _cropRect = new Rectangle(Math.Min(_cropStart.X, e.X), Math.Min(_cropStart.Y, e.Y),
                    Math.Abs(e.X - _cropStart.X), Math.Abs(e.Y - _cropStart.Y));
                _onStatusChanged?.Invoke($"裁剪模式: {_cropRect.Width}×{_cropRect.Height} | 双击确认 | Esc取消");
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
            _onStatusChanged?.Invoke("手动纠偏: 沿一排文字或表格线，从左到右画一条对齐线");
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
                double dx = _deskewEnd.X - _deskewStart.X;
                double dy = _deskewEnd.Y - _deskewStart.Y;
                double angle = Math.Atan2(dy, dx) * 180 / Math.PI;
                _onStatusChanged?.Invoke($"手动纠偏: 当前角度 {angle:F1}° | 松开确认");
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
            var imgStart = ScreenToImage(_deskewStart);
            var imgEnd = ScreenToImage(_deskewEnd);

            double dx = imgEnd.X - imgStart.X;
            double dy = imgEnd.Y - imgStart.Y;

            if (Math.Abs(dx) < 5 && Math.Abs(dy) < 5)
            {
                StopDeskew();
                return;
            }

            double angle = Math.Atan2(dy, dx) * 180 / Math.PI;
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