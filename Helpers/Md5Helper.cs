using System;
using System.IO;
using System.Security.Cryptography;

namespace ScanTool.Helpers
{
    public static class Md5Helper
    {
        private static readonly object _md5Lock = new object();
        public static string CalcFileMd5(string filePath)
        {
            lock (_md5Lock)
            {
                using (var md5 = System.Security.Cryptography.MD5.Create())
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    return BitConverter.ToString(md5.ComputeHash(fs)).Replace("-", "").ToLower();
            }
        }

        public static string CalcBytesMd5(byte[] data)
        {
            lock (_md5Lock)
            {
                using (var md5 = System.Security.Cryptography.MD5.Create())
                    return BitConverter.ToString(md5.ComputeHash(data)).Replace("-", "").ToLower();
            }
        }
    }
}