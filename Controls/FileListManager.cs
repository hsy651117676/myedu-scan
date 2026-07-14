using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ScanTool.Helpers;
using ScanTool.Models;

namespace ScanTool.Controls
{
    public class FileListManager
    {
        private readonly ListBox _lst;
        private readonly string _scanDir;
        private List<FileItem> _currentFiles = new List<FileItem>();

        public FileListManager(ListBox lst, string scanDir)
        {
            _lst = lst;
            _scanDir = scanDir;
        }

        public List<FileItem> CurrentFiles => _currentFiles;
        public int SelectedIndex => _lst.SelectedIndex;

        public void LoadFiles(string rsid, string fl, string archid, List<ScanRecord> scanList, string nodeText)
        {
            _lst.BeginUpdate();
            _lst.Items.Clear();
            _currentFiles.Clear();

            string rsidPadded = rsid.PadLeft(8, '0');
            string localDir = Path.Combine(_scanDir, rsidPadded, fl, archid);

            var localFiles = new List<string>();
            if (Directory.Exists(localDir)) localFiles.AddRange(Directory.GetFiles(localDir, "*.JPG"));

            if (localFiles.Count > 0 || scanList.Count > 0)
            {
                var fileDict = new Dictionary<string, FileItem>();
                foreach (var s in scanList)
                {
                    bool local = File.Exists(Path.Combine(localDir, s.Filename));
                    fileDict[s.Filename] = new FileItem
                    {
                        Filename = s.Filename,
                        DisplayText = $"{s.Filename} ({FormatHelper.FileSize(s.Length)}) [{(local ? "已上传" : "服务器")}]",
                        IsLocal = local,
                        LocalPath = local ? Path.Combine(localDir, s.Filename) : null,
                        Length = s.Length
                    };
                }
                foreach (var f in localFiles)
                {
                    var fi = new FileInfo(f);
                    if (!fileDict.ContainsKey(fi.Name))
                    {
                        fileDict[fi.Name] = new FileItem
                        {
                            Filename = fi.Name,
                            DisplayText = $"{fi.Name} ({FormatHelper.FileSize(fi.Length)}) [本地未上传]",
                            IsLocal = true,
                            LocalPath = f,
                            Length = fi.Length
                        };
                    }
                }
                _currentFiles = fileDict.Values.OrderBy(f => f.Filename, new NaturalStringComparer()).ToList();
            }
            else
            {
                var match = System.Text.RegularExpressions.Regex.Match(nodeText, @"\((\d+)(?:,\d+)?页\)");
                int ys = match.Success ? int.Parse(match.Groups[1].Value) : 0;
                for (int i = 1; i <= ys; i++)
                    _currentFiles.Add(new FileItem { Filename = $"{i:D3}.JPG", DisplayText = $"{i:D3}.JPG (未扫描)", IsLocal = false });
            }

            foreach (var f in _currentFiles) _lst.Items.Add(f.DisplayText);
            _lst.EndUpdate();
        }

        public FileItem GetFile(int index)
        {
            if (index < 0 || index >= _currentFiles.Count) return null;
            return _currentFiles[index];
        }

        public void MarkAsDownloaded(int index, long size, string localPath)
        {
            if (index < 0 || index >= _currentFiles.Count) return;
            _currentFiles[index].IsLocal = true;
            _currentFiles[index].LocalPath = localPath;
            _currentFiles[index].DisplayText = $"{_currentFiles[index].Filename} ({FormatHelper.FileSize(size)}) [已上传]";
            _lst.Items[index] = _currentFiles[index].DisplayText;
        }
    }
}