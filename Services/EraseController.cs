// Services/EraseController.cs
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ScanTool.Services
{
    public enum EraserShape { Circle, Rectangle }

    public class EraseController
    {
        private readonly PictureBox _picBox;
        private readonly Action<string> _onStatusChanged;
        private bool _isActive;
        private Bitmap _snapshot;
        private Point _lastPoint;
        private bool _drawing;
        private bool _hasDrawn;

        public bool IsActive => _isActive;
        public int EraserSize => UserSettings.Get<int>("EraserSize");
        public EraserShape Shape => UserSettings.Get<string>("EraserShape") == "Rectangle" ? EraserShape.Rectangle : EraserShape.Circle;
        public bool HasDrawn => _hasDrawn;

        public EraseController(PictureBox picBox, Action<string> onStatusChanged = null)
        {
            _picBox = picBox;
            _onStatusChanged = onStatusChanged;
        }

        private void NotifyStatus()
        {
            if (!_isActive) return;
            string sn = Shape == EraserShape.Circle ? "圆形" : "矩形";
            _onStatusChanged?.Invoke($"擦除模式: {sn} | 大小: {EraserSize}px | E切换形状 | +/-调整大小");
        }

        public void Start()
        {
            _isActive = true;
            _drawing = false;
            _hasDrawn = false;
            _snapshot?.Dispose();
            _snapshot = _picBox.Image != null ? new Bitmap(_picBox.Image) : null;
            UpdateCursor();

            _picBox.MouseDown += OnMouseDown;
            _picBox.MouseMove += OnMouseMove;
            _picBox.MouseUp += OnMouseUp;
            _picBox.MouseEnter += OnMouseEnter;
            NotifyStatus();
        }

        public void SizeUp()
        {
            UserSettings.Set("EraserSize", Math.Min(EraserSize + 10, 200));
            UpdateCursor();
            NotifyStatus();
        }

        public void SizeDown()
        {
            UserSettings.Set("EraserSize", Math.Max(EraserSize - 10, 10));
            UpdateCursor();
            NotifyStatus();
        }

        public void ToggleShape()
        {
            UserSettings.Set("EraserShape", Shape == EraserShape.Circle ? "Rectangle" : "Circle");
            UpdateCursor();
            NotifyStatus();
        }

        private void UpdateCursor()
        {
            int size = EraserSize + 4;
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                if (Shape == EraserShape.Circle)
                    g.DrawEllipse(Pens.Red, 2, 2, EraserSize, EraserSize);
                else
                    g.DrawRectangle(Pens.Red, 2, 2, EraserSize, EraserSize);
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
            int dx = e.X - _lastPoint.X;
            int dy = e.Y - _lastPoint.Y;
            int dist = (int)Math.Sqrt(dx * dx + dy * dy);
            int steps = Math.Max(1, dist / (EraserSize / 4));
            for (int i = 0; i <= steps; i++)
                EraseAt(new Point(_lastPoint.X + dx * i / steps, _lastPoint.Y + dy * i / steps));
            _lastPoint = e.Location;
        }

        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            _drawing = false;
        }

        private void EraseAt(Point pt)
        {
            if (_picBox.Image == null) return;
            _hasDrawn = true;
            using var g = Graphics.FromImage(_picBox.Image);
            g.CompositingMode = CompositingMode.SourceCopy;
            using var brush = new SolidBrush(Color.White);
            int half = EraserSize / 2;
            if (Shape == EraserShape.Circle)
                g.FillEllipse(brush, pt.X - half, pt.Y - half, EraserSize, EraserSize);
            else
                g.FillRectangle(brush, pt.X - half, pt.Y - half, EraserSize, EraserSize);
            _picBox.Invalidate();
        }

        private void OnMouseEnter(object sender, EventArgs e)
        {
            if (_isActive)
                UpdateCursor();
        }

        public Bitmap Stop()
        {
            _isActive = false;
            _picBox.Cursor = Cursors.Default;
            _picBox.MouseDown -= OnMouseDown;
            _picBox.MouseMove -= OnMouseMove;
            _picBox.MouseUp -= OnMouseUp;
            _picBox.MouseEnter -= OnMouseEnter;
            return _snapshot;
        }
    }
}