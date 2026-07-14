// Services/EraseController.cs
using System;
using System.Drawing;
using System.Windows.Forms;

namespace ScanTool.Services
{
    public class EraseController
    {
        private readonly PictureBox _picBox;
        private bool _isActive;
        private int _eraserSize = 20;
        private Bitmap _snapshot;
        private Point _lastPoint;
        private bool _drawing;
        private bool _hasDrawn;

        public bool IsActive => _isActive;
        public int EraserSize => _eraserSize;
        public bool HasDrawn => _hasDrawn;

        public EraseController(PictureBox picBox)
        {
            _picBox = picBox;
        }

        public void Start()
        {
            _isActive = true;
            _drawing = false;
            _hasDrawn = false;
            _eraserSize = 20;
            _snapshot?.Dispose();
            _snapshot = _picBox.Image != null ? new Bitmap(_picBox.Image) : null;
            UpdateCursor();

            _picBox.MouseDown += OnMouseDown;
            _picBox.MouseMove += OnMouseMove;
            _picBox.MouseUp += OnMouseUp;
        }

        public void SizeUp() { _eraserSize = Math.Min(_eraserSize + 5, 100); UpdateCursor(); }
        public void SizeDown() { _eraserSize = Math.Max(_eraserSize - 5, 5); UpdateCursor(); }

        private void UpdateCursor()
        {
            int size = _eraserSize + 2;
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.DrawEllipse(Pens.Black, 1, 1, _eraserSize, _eraserSize);
            }
            _picBox.Cursor = new Cursor(bmp.GetHicon());
        }

        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            _drawing = true;
            _lastPoint = e.Location;
            EraseAt(e.Location);
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_drawing || e.Button != MouseButtons.Left) return;
            int dx = e.X - _lastPoint.X, dy = e.Y - _lastPoint.Y;
            int dist = (int)Math.Sqrt(dx * dx + dy * dy);
            int steps = Math.Max(1, dist / (_eraserSize / 3));
            for (int i = 0; i <= steps; i++)
                EraseAt(new Point(_lastPoint.X + dx * i / steps, _lastPoint.Y + dy * i / steps));
            _lastPoint = e.Location;
        }

        private void OnMouseUp(object sender, MouseEventArgs e) { _drawing = false; }

        private void EraseAt(Point pt)
        {
            if (_picBox.Image == null) return;
            _hasDrawn = true;
            using var g = Graphics.FromImage(_picBox.Image);
            g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            using var brush = new SolidBrush(Color.White);
            g.FillEllipse(brush, pt.X - _eraserSize / 2, pt.Y - _eraserSize / 2, _eraserSize, _eraserSize);
            _picBox.Invalidate();
        }

        public Bitmap Stop()
        {
            _isActive = false;
            _picBox.Cursor = Cursors.Default;
            _picBox.MouseDown -= OnMouseDown;
            _picBox.MouseMove -= OnMouseMove;
            _picBox.MouseUp -= OnMouseUp;
            return _snapshot;
        }
    }
}