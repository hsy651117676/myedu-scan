// Services/ImageCacheManager.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Drawing.Imaging;
using ScanTool.Helpers;
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
                foreach (var kv in _dirtyFlags) if (kv.Value) count++;
                return count;
            }
        }

        public void Store(string filePath, Bitmap image)
        {
            if (string.IsNullOrEmpty(filePath) || image == null) return;
            var tmpPath = filePath + ".tmp";
            ImageSaveHelper.SaveJpeg(image, tmpPath);
            _dirtyFlags[filePath] = true;
        }

        public Bitmap GetImage(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return null;

            var tmpPath = filePath + ".tmp";
            if (File.Exists(tmpPath))
            {
                using (var fs = new FileStream(tmpPath, FileMode.Open, FileAccess.Read))
                    return new Bitmap(fs);
            }

            if (File.Exists(filePath))
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                    return new Bitmap(fs);
            }
            return null;
        }

        public void Save(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            var tmpPath = filePath + ".tmp";
            if (File.Exists(tmpPath))
            {
                File.Delete(filePath);
                File.Move(tmpPath, filePath);
            }
            Release(filePath);
        }

        public void SaveAll()
        {
            int saved = 0;
            var keys = new List<string>(_dirtyFlags.Keys);
            foreach (var key in keys)
            {
                if (!_dirtyFlags[key]) continue;
                var tmpPath = key + ".tmp";
                if (File.Exists(tmpPath))
                {
                    File.Delete(key);
                    File.Move(tmpPath, key);
                    UpdateMd5(key);  // 更新 MD5 缓存
                    saved++;
                }
            }
            _dirtyFlags.Clear();
        }

        public void Release(string filePath)
        {
            _dirtyFlags.Remove(filePath);
            var tmpPath = filePath + ".tmp";
            if (File.Exists(tmpPath)) File.Delete(tmpPath);
        }

        public void ReleaseAll()
        {
            foreach (var kv in _dirtyFlags)
            {
                var tmpPath = kv.Key + ".tmp";
                if (File.Exists(tmpPath)) File.Delete(tmpPath);
            }
            _dirtyFlags.Clear();
            _md5Cache.Clear();
        }

        public void Delete(string filePath)
        {
            if (File.Exists(filePath)) File.Delete(filePath);
            Release(filePath);
        }

        public bool IsDirty(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return false;
            return _dirtyFlags.TryGetValue(filePath, out var dirty) && dirty;
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
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return null;
            if (_md5Cache.TryGetValue(filePath, out var md5)) return md5;
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