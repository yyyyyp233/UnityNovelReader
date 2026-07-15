using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace UnityNovelReader.Editor
{
    internal static class BookIdentity
    {
        internal static string FromPath(string filePath)
        {
            string normalized = Path.GetFullPath(filePath).Trim();
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                normalized = normalized.ToUpperInvariant();
            }

            byte[] input = Encoding.UTF8.GetBytes(normalized);
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(input);
                StringBuilder result = new StringBuilder(24);
                for (int i = 0; i < 12; i++)
                {
                    result.Append(hash[i].ToString("x2"));
                }

                return result.ToString();
            }
        }
    }
}
