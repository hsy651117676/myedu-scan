// Services/ViewportController.cs
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ScanTool.Services
{
    /// <summary>
    /// 视口控制：缩放（以可视中心为基准）、平移、适应窗口、重置
    /// </summary>
    public class ViewportController : IDisposable
    {
        private readonly PictureBox _picBox;
        private Image _originalImage;
        private float _zoomFactor = 1.0f;
        private Point _panOffset = Point.Empty;

        public float ZoomFactor => _zoomFactor;

        public ViewportController(PictureBox picBox)
        {
            _picBox = picBox;
            _picBox.MouseWheel += OnMouseWheel;
        }

        public void SetOriginalImage(Image img)
        {
            _originalImage?.Dispose();
            _originalImage = img != null ? new Bitmap(img) : null;
            _zoomFactor = 1.0f;
            _panOffset = Point.Empty;
        }

        // ==================== 缩放（以 PictureBox 可视中心为基准） ====================

        public void ZoomIn()
        {
            float old = _zoomFactor;
            _zoomFactor = Math.Min(_zoomFactor * 1.25f, 10f);
            AdjustPanForCenterZoom(old);
            Render();
        }

        public void ZoomOut()
        {
            float old = _zoomFactor;
            _zoomFactor = Math.Max(_zoomFactor / 1.25f, 0.1f);
            AdjustPanForCenterZoom(old);
            Render();
        }

        private void AdjustPanForCenterZoom(float oldZoom)
        {
            float ratio = _zoomFactor / oldZoom;
            float cx = _picBox.ClientSize.Width / 2f;
            float cy = _picBox.ClientSize.Height / 2f;
            _panOffset.X = (int)(cx - ratio * (cx - _panOffset.X));
            _panOffset.Y = (int)(cy - ratio * (cy - _panOffset.Y));
        }

        private void OnMouseWheel(object sender, MouseEventArgs e)
        {
            if (Control.ModifierKeys == Keys.Control)
            {
                if (e.Delta > 0) ZoomIn();
                else ZoomOut();
            }
        }

        // ==================== 平移 ====================

        public void PanUp() { _panOffset.Y += 30; Render(); }
        public void PanDown() { _panOffset.Y -= 30; Render(); }
        public void PanLeft() { _panOffset.X += 30; Render(); }
        public void PanRight() { _panOffset.X -= 30; Render(); }

        // ==================== 适应窗口 / 重置 ====================

        public void FitToScreen()
        {
            _panOffset = Point.Empty;
            if (_originalImage == null || _picBox.ClientSize.Width <= 0) return;
            float wRatio = (float)_picBox.ClientSize.Width / _originalImage.Width;
            float hRatio = (float)_picBox.ClientSize.Height / _originalImage.Height;
            _zoomFactor = Math.Min(wRatio, hRatio);
            Render();
        }

        public void ResetZoom()
        {
            _panOffset = Point.Empty;
            _zoomFactor = 1.0f;
            Render();
        }

        // ==================== 渲染 ====================

        public void Render()
        {
            if (_originalImage == null || _picBox.ClientSize.Width <= 0) return;

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

        public void Dispose()
        {
            _originalImage?.Dispose();
            _originalImage = null;
        }
    }
}