// Services/FileOperation.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace ScanTool.Services
{
    /// <summary>
    /// 文件操作：保存、删除、批量保存、导出PDF
    /// </summary>
    public class FileOperation
    {
        public void Save(Bitmap bitmap, string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            using (var bmp = new Bitmap(bitmap))
            {
                bmp.Save(path, ImageFormat.Jpeg);
            }
        }

        public void Delete(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }

        public void BatchSave(List<(Bitmap bitmap, string path)> files)
        {
            foreach (var (bitmap, path) in files)
                Save(bitmap, path);
        }

        public void ExportPdf(List<string> imagePaths, string pdfPath)
        {
            // 用 iTextSharp 或 PDFSharp
        }
    }
}