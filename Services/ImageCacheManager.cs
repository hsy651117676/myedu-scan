// Services/ImageCacheManager.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Drawing.Imaging;
using ScanTool.Helpers;

namespace ScanTool.Services
{
    public class ImageCacheManager : IDisposable
    {
        private readonly Dictionary<string, bool> _dirtyFlags = new Dictionary<string, bool>();
        private Dictionary<string, string> _md5Cache = new Dictionary<string, string>();

        public int DirtyCount
        {
            get
            {
                int count = 0;
                foreach (var kv in _dirtyFlags)
                    if (kv.Value)
                        count++;
                return count;
            }
        }

        public void Store(string filePath, Bitmap image)
        {
            if (string.IsNullOrEmpty(filePath) || image == null)
                return;

            var tmpPath = filePath + ".tmp";

            try
            {
                // 保存到临时文件
                ImageSaveHelper.SaveJpeg(image, tmpPath);
                _dirtyFlags[filePath] = true;
            }
            catch (Exception ex)
            {
                // 如果保存失败，清理临时文件
                if (File.Exists(tmpPath))
                {
                    try { File.Delete(tmpPath); } catch { }
                }
                throw new Exception($"保存临时文件失败: {ex.Message}");
            }
        }

        public Bitmap GetImage(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return null;

            var tmpPath = filePath + ".tmp";
            if (File.Exists(tmpPath))
            {
                try
                {
                    // 读取字节数组，避免文件锁
                    var bytes = File.ReadAllBytes(tmpPath);
                    using (var ms = new MemoryStream(bytes))
                        return new Bitmap(ms);
                }
                catch { }
            }

            if (File.Exists(filePath))
            {
                try
                {
                    var bytes = File.ReadAllBytes(filePath);
                    using (var ms = new MemoryStream(bytes))
                        return new Bitmap(ms);
                }
                catch { }
            }

            return null;
        }
        public void Save(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return;

            var tmpPath = filePath + ".tmp";
            if (!File.Exists(tmpPath))
            {
                _dirtyFlags.Remove(filePath);
                return;
            }

            try
            {
                // 直接用 .tmp 覆盖原文件
                File.Copy(tmpPath, filePath, true);

                // 删除 .tmp
                File.Delete(tmpPath);

                // 清理备份
                var backupPath = filePath + ".bak";
                if (File.Exists(backupPath))
                    File.Delete(backupPath);

                // 更新 MD5
                UpdateMd5(filePath);

                // 清除脏标记
                _dirtyFlags.Remove(filePath);
            }
            catch (Exception ex)
            {
                // 清理临时文件
                try { if (File.Exists(tmpPath)) File.Delete(tmpPath); } catch { }
                throw new Exception($"保存文件失败 {filePath}: {ex.Message}");
            }
        }
        public void SaveAll()
        {
            int saved = 0;
            int skipped = 0;
            var errors = new List<string>();
            var keys = new List<string>(_dirtyFlags.Keys);

            foreach (var key in keys)
            {
                try
                {
                    var tmpPath = key + ".tmp";
                    if (!File.Exists(tmpPath))
                    {
                        // 没有 .tmp，跳过
                        _dirtyFlags.Remove(key);
                        skipped++;
                        continue;
                    }

                    Save(key);
                    saved++;
                }
                catch (Exception ex)
                {
                    errors.Add($"{Path.GetFileName(key)}: {ex.Message}");
                }
            }

            _dirtyFlags.Clear();

            if (errors.Count > 0)
            {
                throw new Exception($"保存完成：成功 {saved} 个，跳过 {skipped} 个，失败 {errors.Count} 个\n{string.Join("\n", errors)}");
            }
        }
        public void Release(string filePath)
        {
            _dirtyFlags.Remove(filePath);
            var tmpPath = filePath + ".tmp";
            if (File.Exists(tmpPath))
            {
                try { File.Delete(tmpPath); } catch { }
            }

            // 清理备份文件
            var backupPath = filePath + ".bak";
            if (File.Exists(backupPath))
            {
                try { File.Delete(backupPath); } catch { }
            }
        }

        public void ReleaseAll()
        {
            foreach (var kv in _dirtyFlags)
            {
                var tmpPath = kv.Key + ".tmp";
                if (File.Exists(tmpPath))
                {
                    try { File.Delete(tmpPath); } catch { }
                }

                var backupPath = kv.Key + ".bak";
                if (File.Exists(backupPath))
                {
                    try { File.Delete(backupPath); } catch { }
                }
            }
            _dirtyFlags.Clear();
            _md5Cache.Clear();
        }

        public void Delete(string filePath)
        {
            if (File.Exists(filePath))
            {
                try { File.Delete(filePath); } catch { }
            }
            Release(filePath);
        }

        public bool IsDirty(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return false;

            // 优先检查内存中的标记
            if (_dirtyFlags.TryGetValue(filePath, out var dirty) && dirty)
                return true;

            // 检查磁盘上的 .tmp 文件
            var tmpPath = filePath + ".tmp";
            return File.Exists(tmpPath);
        }

        public bool HasCache(string filePath)
        {
            var tmpPath = filePath + ".tmp";
            return File.Exists(tmpPath);
        }

        public void MarkDirty(string filePath)
        {
            if (!string.IsNullOrEmpty(filePath))
                _dirtyFlags[filePath] = true;
        }

        public void Dispose()
        {
            ReleaseAll();
        }

        public string GetMd5(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return null;

            if (_md5Cache.TryGetValue(filePath, out var md5))
                return md5;

            md5 = Md5Helper.CalcFileMd5(filePath);
            _md5Cache[filePath] = md5;
            return md5;
        }

        public void UpdateMd5(string filePath)
        {
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                _md5Cache[filePath] = Md5Helper.CalcFileMd5(filePath);
        }

        public void RemoveMd5(string filePath)
        {
            _md5Cache.Remove(filePath);
        }
    }
}