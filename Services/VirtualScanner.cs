// Services/VirtualScanner.cs
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ScanTool.Services
{
    public class VirtualScanner
    {
        private static int _scanIndex = 0;

        public Bitmap Scan(string paperSize = "A4", int dpi = 300, bool vertical = true)
        {
            int baseW, baseH;
            switch (paperSize)
            {
                case "A3": baseW = 3508; baseH = 4961; break;
                case "A4": baseW = 2480; baseH = 3508; break;
                case "A5": baseW = 1748; baseH = 2480; break;
                case "B5": baseW = 2079; baseH = 2953; break;
                default: baseW = 2480; baseH = 3508; break;
            }

            double scale = dpi / 300.0;
            int paperW = (int)((vertical ? baseW : baseH) * scale);
            int paperH = (int)((vertical ? baseH : baseW) * scale);

            int bgW = paperW + paperW / 5;
            int bgH = paperH + paperH / 5;

            var bmp = new Bitmap(bgW, bgH);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.FromArgb(40, 40, 40));

                _scanIndex++;
                var rand = new Random(DateTime.Now.Millisecond + _scanIndex);

                // 随机偏移（可负值，模拟纸张超出边缘）
                int offsetX = rand.Next(-bgW / 12, bgW / 6);
                int offsetY = rand.Next(-bgH / 12, bgH / 6);

                // 随机旋转：-5~5度，偶尔大角度或放反
                float rotation = (float)(rand.NextDouble() * 10 - 5);
                if (rand.Next(15) == 0) rotation += 180;    // 偶尔放反
                if (rand.Next(20) == 0) rotation += (float)(rand.NextDouble() * 40 - 20); // 偶尔大角度

                System.Diagnostics.Debug.WriteLine($"虚拟扫描: paperSize={paperSize}, dpi={dpi}, vertical={vertical}, paperW={paperW}, paperH={paperH}, offset=({offsetX},{offsetY}), rotation={rotation:F2}°");

                var state = g.Save();
                g.TranslateTransform(offsetX + paperW / 2, offsetY + paperH / 2);
                g.RotateTransform(rotation);

                g.FillRectangle(Brushes.White, -paperW / 2, -paperH / 2, paperW, paperH);

                using (var pen = new Pen(Color.FromArgb(180, 180, 180), 3))
                    g.DrawRectangle(pen, -paperW / 2, -paperH / 2, paperW, paperH);

                g.Clip = new Region(new Rectangle(-paperW / 2, -paperH / 2, paperW, paperH));

                using (var pen = new Pen(Color.FromArgb(230, 230, 230), 1))
                {
                    for (int x = -paperW / 2; x < paperW / 2; x += paperW / 15)
                        g.DrawLine(pen, x, -paperH / 2, x, paperH / 2);
                    for (int y = -paperH / 2; y < paperH / 2; y += paperH / 20)
                        g.DrawLine(pen, -paperW / 2, y, paperW / 2, y);
                }

                using (var font = new Font("黑体", paperH / 40, FontStyle.Bold))
                {
                    string title = "干部档案扫描件（模拟）";
                    var sz = g.MeasureString(title, font);
                    g.DrawString(title, font, Brushes.DarkRed, -sz.Width / 2, -paperH / 2 + paperH / 15);
                }

                using (var font = new Font("宋体", paperH / 50))
                {
                    var s2 = g.Save();
                    g.TranslateTransform(0, -paperH / 8);
                    g.RotateTransform(2f);
                    string text = $"虚拟扫描 {paperSize} {(vertical ? "纵向" : "横向")} {dpi}DPI 第{_scanIndex}次";
                    var sz = g.MeasureString(text, font);
                    g.DrawString(text, font, Brushes.Black, -sz.Width / 2, -sz.Height / 2);
                    g.Restore(s2);
                }

                int cross = paperW / 25;
                using (var pen = new Pen(Color.LightGray, 1))
                {
                    g.DrawLine(pen, -cross, 0, cross, 0);
                    g.DrawLine(pen, 0, -cross, 0, cross);
                }

                int cs = paperW / 35;
                int m = paperW / 45;
                using (var pen = new Pen(Color.Black, 3))
                {
                    g.DrawLine(pen, -paperW / 2 + m, -paperH / 2 + m, -paperW / 2 + m + cs, -paperH / 2 + m);
                    g.DrawLine(pen, -paperW / 2 + m, -paperH / 2 + m, -paperW / 2 + m, -paperH / 2 + m + cs);
                    g.DrawLine(pen, paperW / 2 - m, -paperH / 2 + m, paperW / 2 - m - cs, -paperH / 2 + m);
                    g.DrawLine(pen, paperW / 2 - m, -paperH / 2 + m, paperW / 2 - m, -paperH / 2 + m + cs);
                    g.DrawLine(pen, -paperW / 2 + m, paperH / 2 - m, -paperW / 2 + m + cs, paperH / 2 - m);
                    g.DrawLine(pen, -paperW / 2 + m, paperH / 2 - m, -paperW / 2 + m, paperH / 2 - m - cs);
                    g.DrawLine(pen, paperW / 2 - m, paperH / 2 - m, paperW / 2 - m - cs, paperH / 2 - m);
                    g.DrawLine(pen, paperW / 2 - m, paperH / 2 - m, paperW / 2 - m, paperH / 2 - m - cs);
                }

                using (var font = new Font("微软雅黑", paperH / 80))
                {
                    string info = $"纸张:{paperW}x{paperH}  {DateTime.Now:HH:mm:ss}";
                    var sz = g.MeasureString(info, font);
                    g.DrawString(info, font, Brushes.DarkBlue, -sz.Width / 2, paperH / 2 - paperH / 10);
                }

                g.Restore(state);
            }

            return bmp;
        }
    }
}