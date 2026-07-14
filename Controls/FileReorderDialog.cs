using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ScanTool.Services;

namespace ScanTool.Controls
{
    public class FileReorderDialog
    {
        private readonly string _localDir;
        private readonly ImageCacheManager _imageCache;
        private readonly Action _onSaved;

        public FileReorderDialog(string localDir, ImageCacheManager imageCache, Action onSaved)
        {
            _localDir = localDir;
            _imageCache = imageCache;
            _onSaved = onSaved;
        }

        public void Show()
        {
            if (!Directory.Exists(_localDir))
            {
                MessageBox.Show("本地没有文件", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var localFiles = Directory.GetFiles(_localDir, "*.JPG")
                .Where(f => File.Exists(f))
                .OrderBy(f => f, new NaturalStringComparer())
                .ToList();

            if (localFiles.Count == 0)
            {
                MessageBox.Show("本地没有文件", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var form = new Form
            {
                Text = "调整文件顺序",
                Size = new Size(450, 420),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var lstFiles = new ListBox
            {
                Location = new Point(15, 15),
                Size = new Size(280, 300),
                Font = new Font("微软雅黑", 11F)
            };
            foreach (var f in localFiles)
                lstFiles.Items.Add(Path.GetFileName(f));

            var btnUp = new Button { Text = "⬆ 上移", Location = new Point(310, 60), Size = new Size(100, 32) };
            var btnDown = new Button { Text = "⬇ 下移", Location = new Point(310, 100), Size = new Size(100, 32) };

            btnUp.Click += (s, e) =>
            {
                int idx = lstFiles.SelectedIndex;
                if (idx > 0)
                {
                    var item = lstFiles.Items[idx];
                    lstFiles.Items.RemoveAt(idx);
                    lstFiles.Items.Insert(idx - 1, item);
                    lstFiles.SelectedIndex = idx - 1;
                }
            };

            btnDown.Click += (s, e) =>
            {
                int idx = lstFiles.SelectedIndex;
                if (idx >= 0 && idx < lstFiles.Items.Count - 1)
                {
                    var item = lstFiles.Items[idx];
                    lstFiles.Items.RemoveAt(idx);
                    lstFiles.Items.Insert(idx + 1, item);
                    lstFiles.SelectedIndex = idx + 1;
                }
            };

            var btnSave = new Button { Text = "保存顺序", Location = new Point(80, 330), Size = new Size(100, 32), BackColor = Color.FromArgb(64, 158, 255), ForeColor = Color.White };
            var btnCancel = new Button { Text = "取消", Location = new Point(220, 330), Size = new Size(100, 32) };

            btnSave.Click += (s, e) =>
            {
                try
                {
                    var tmpPaths = new List<string>();
                    for (int i = 0; i < lstFiles.Items.Count; i++)
                    {
                        string oldName = lstFiles.Items[i].ToString();
                        string oldPath = Path.Combine(_localDir, oldName);
                        if (!File.Exists(oldPath)) continue;

                        string tmpPath = Path.Combine(_localDir, $"_reorder_{i:D4}_.tmp");
                        File.Move(oldPath, tmpPath);
                        tmpPaths.Add(tmpPath);
                    }

                    for (int i = 0; i < tmpPaths.Count; i++)
                    {
                        string newName = $"{(i + 1):D3}.JPG";
                        string newPath = Path.Combine(_localDir, newName);
                        File.Move(tmpPaths[i], newPath);
                    }

                    _imageCache?.ReleaseAll();
                    form.Close();
                    _onSaved?.Invoke();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"调整失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            btnCancel.Click += (s, e) => form.Close();

            form.Controls.AddRange(new Control[] { lstFiles, btnUp, btnDown, btnSave, btnCancel });
            form.ShowDialog();
        }
    }

    public class NaturalStringComparer : IComparer<string>
    {
        [System.Runtime.InteropServices.DllImport("shlwapi.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern int StrCmpLogicalW(string x, string y);

        public int Compare(string x, string y) => StrCmpLogicalW(x, y);
    }
}