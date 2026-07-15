using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UnityNovelReader.Editor
{
    internal static class NovelTextLoader
    {
        private static readonly HashSet<string> SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".txt",
            ".text",
            ".md"
        };

        internal static NovelDocument Load(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("A file path is required.", "filePath");
            }

            string fullPath = Path.GetFullPath(filePath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("Novel file not found.", fullPath);
            }

            string extension = Path.GetExtension(fullPath);
            if (!SupportedExtensions.Contains(extension))
            {
                throw new NotSupportedException("Supported formats are .txt, .text, and .md.");
            }

            byte[] bytes = File.ReadAllBytes(fullPath);
            Encoding encoding = DetectEncoding(bytes);
            string content = encoding.GetString(bytes);
            if (content.Length > 0 && content[0] == '\uFEFF')
            {
                content = content.Substring(1);
            }

            FileInfo fileInfo = new FileInfo(fullPath);
            return new NovelDocument
            {
                FilePath = fullPath,
                Title = Path.GetFileNameWithoutExtension(fullPath),
                EncodingName = GetFriendlyEncodingName(encoding),
                Content = content,
                FileLength = fileInfo.Length,
                LastWriteUtcTicks = fileInfo.LastWriteTimeUtc.Ticks,
                Chapters = ChapterParser.Parse(content)
            };
        }

        private static Encoding DetectEncoding(byte[] bytes)
        {
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                return new UTF8Encoding(true, true);
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                return new UnicodeEncoding(false, true, true);
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                return new UnicodeEncoding(true, true, true);
            }

            UTF8Encoding strictUtf8 = new UTF8Encoding(false, true);
            try
            {
                strictUtf8.GetString(bytes);
                return strictUtf8;
            }
            catch (DecoderFallbackException)
            {
                // Chinese TXT archives commonly use GB18030 or GBK.
            }

            try
            {
                return Encoding.GetEncoding(54936);
            }
            catch (ArgumentException)
            {
                try
                {
                    return Encoding.GetEncoding(936);
                }
                catch (ArgumentException)
                {
                    return Encoding.Default;
                }
            }
        }

        private static string GetFriendlyEncodingName(Encoding encoding)
        {
            if (encoding.CodePage == 54936) return "GB18030";
            if (encoding.CodePage == 936) return "GBK";
            if (encoding.CodePage == Encoding.UTF8.CodePage) return "UTF-8";
            if (encoding.CodePage == Encoding.Unicode.CodePage) return "UTF-16 LE";
            if (encoding.CodePage == Encoding.BigEndianUnicode.CodePage) return "UTF-16 BE";
            return encoding.WebName;
        }
    }
}
