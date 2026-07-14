// Services/PdfExporter.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ScanTool.Helpers;

namespace ScanTool.Services
{
    public class PdfExporter
    {
        private List<string> _imageFiles;
        private int _currentIndex;
        private string _outputPath;
        private Action<string> _onComplete;

        public void ExportOrPrint(string imageDir, string defaultFileName, Action<string> onComplete)
        {
            if (!Directory.Exists(imageDir))
            {
                MessageBox.Show("图片目录不存在。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _imageFiles = Directory.GetFiles(imageDir, "*.JPG")
                .OrderBy(f => f, new NaturalStringComparer())
                .ToList();

            if (_imageFiles.Count == 0)
            {
                MessageBox.Show("目录下没有 JPG 文件。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 弹窗选择
            var choice = ShowChoiceDialog(_imageFiles.Count);
            if (choice == DialogResult.Cancel) return;

            if (choice == DialogResult.Yes)
                SaveAsPdf(defaultFileName, onComplete);
            else
                PrintToPrinter();
        }

        private DialogResult ShowChoiceDialog(int pageCount)
        {
            using (var form = new Form
            {
                Text = "导出或打印",
                Size = new Size(320, 190),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            })
            {
                form.Controls.Add(new Label
                {
                    Text = $"共 {pageCount} 页，请选择操作：",
                    Location = new Point(20, 20),
                    AutoSize = true,
                    Font = new Font("微软雅黑", 10F)
                });

                var btnSave = new Button
                {
                    Text = "💾 保存PDF",
                    Location = new Point(20, 70),
                    Width = 120,
                    Height = 35,
                    Font = new Font("微软雅黑", 10F)
                };
                btnSave.Click += (s, e) => { form.DialogResult = DialogResult.Yes; form.Close(); };
                form.Controls.Add(btnSave);

                var btnPrint = new Button
                {
                    Text = "🖨 打印",
                    Location = new Point(160, 70),
                    Width = 120,
                    Height = 35,
                    Font = new Font("微软雅黑", 10F)
                };
                btnPrint.Click += (s, e) => { form.DialogResult = DialogResult.No; form.Close(); };
                form.Controls.Add(btnPrint);

                var btnCancel = new Button
                {
                    Text = "取消",
                    Location = new Point(100, 115),
                    Width = 100,
                    Height = 30,
                    Font = new Font("微软雅黑", 9F)
                };
                btnCancel.Click += (s, e) => { form.DialogResult = DialogResult.Cancel; form.Close(); };
                form.Controls.Add(btnCancel);

                return form.ShowDialog();
            }
        }

        private void SaveAsPdf(string defaultFileName, Action<string> onComplete)
        {
            using (var sfd = new SaveFileDialog
            {
                Title = "保存PDF",
                Filter = "PDF文件 (*.pdf)|*.pdf",
                FileName = defaultFileName,
                DefaultExt = "pdf"
            })
            {
                if (sfd.ShowDialog() != DialogResult.OK) return;

                _outputPath = sfd.FileName;
                _currentIndex = 0;
                _onComplete = onComplete;

                var printDoc = new PrintDocument();
                printDoc.PrinterSettings.PrinterName = "Microsoft Print to PDF";
                printDoc.PrinterSettings.PrintToFile = true;
                printDoc.PrinterSettings.PrintFileName = _outputPath;
                printDoc.DefaultPageSettings.Margins = new Margins(20, 20, 20, 20);
                printDoc.PrintPage += PrintPage;
                printDoc.EndPrint += (s, e) => _onComplete?.Invoke(_outputPath);

                try { printDoc.Print(); }
                catch (Exception ex)
                {
                    MessageBox.Show($"PDF生成失败: {ex.Message}\n请确保系统已安装 'Microsoft Print to PDF'。",
                        "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void PrintToPrinter()
        {
            using (var psd = new PrintDialog
            {
                AllowSomePages = false,
                AllowPrintToFile = false,
                Document = new PrintDocument()
            })
            {
                if (psd.ShowDialog() != DialogResult.OK) return;

                _currentIndex = 0;
                var printDoc = psd.Document;
                printDoc.PrintPage += PrintPage;
                printDoc.EndPrint += (s, e) =>
                    MessageBox.Show("打印完成。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);

                try { printDoc.Print(); }
                catch (Exception ex)
                {
                    MessageBox.Show($"打印失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void PrintPage(object sender, PrintPageEventArgs e)
        {
            if (_currentIndex >= _imageFiles.Count)
            {
                e.HasMorePages = false;
                return;
            }

            using (var img = Image.FromFile(_imageFiles[_currentIndex]))
            {
                float pageW = e.MarginBounds.Width;
                float pageH = e.MarginBounds.Height;
                float scale = Math.Min(pageW / img.Width, pageH / img.Height);
                float drawW = img.Width * scale;
                float drawH = img.Height * scale;
                float x = e.MarginBounds.Left + (pageW - drawW) / 2;
                float y = e.MarginBounds.Top + (pageH - drawH) / 2;
                e.Graphics.DrawImage(img, x, y, drawW, drawH);
            }

            _currentIndex++;
            e.HasMorePages = _currentIndex < _imageFiles.Count;
        }
    }
}