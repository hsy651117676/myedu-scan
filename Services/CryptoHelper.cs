using System;
using System.Security.Cryptography;
using System.Text;

namespace ScanTool.Services
{
    public class CryptoHelper
    {
        private readonly byte[] _key;

        public CryptoHelper(string key)
        {
            _key = Encoding.UTF8.GetBytes(key.PadRight(16, '\0').Substring(0, 16));
        }

        public byte[] Encrypt(byte[] rawData)
        {
            int originalSize = rawData.Length;
            int paddedSize = ((originalSize + 15) / 16) * 16;
            byte[] padded = new byte[paddedSize];
            Array.Copy(rawData, padded, originalSize);

            using (var aes = Aes.Create())
            {
                aes.Key = _key;
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.None;
                var encryptor = aes.CreateEncryptor();
                byte[] encrypted = encryptor.TransformFinalBlock(padded, 0, paddedSize);
                byte[] result = new byte[8 + encrypted.Length];
                BitConverter.GetBytes(originalSize).CopyTo(result, 0);
                encrypted.CopyTo(result, 8);
                return result;
            }
        }

        public byte[] Decrypt(byte[] encryptedData)
        {
            int originalSize = BitConverter.ToInt32(encryptedData, 0);
            byte[] enc = new byte[encryptedData.Length - 8];
            Array.Copy(encryptedData, 8, enc, 0, enc.Length);

            using (var aes = Aes.Create())
            {
                aes.Key = _key;
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.None;
                var decryptor = aes.CreateDecryptor();
                byte[] decrypted = decryptor.TransformFinalBlock(enc, 0, enc.Length);
                byte[] result = new byte[originalSize];
                Array.Copy(decrypted, result, originalSize);
                return result;
            }
        }
    }
}