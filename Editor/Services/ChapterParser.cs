using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace UnityNovelReader.Editor
{
    internal static class ChapterParser
    {
        private const int MaximumHeadingLength = 120;

        private static readonly Regex HeadingPrefix = new Regex(
            @"^(?:第\s*[零〇一二三四五六七八九十百千万两\d]+\s*[章节回卷集部篇]|chapter\s+\d+|序章|楔子|尾声|终章|后记|番外(?:篇)?(?:\s*\d+)?)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        internal static List<ChapterInfo> Parse(string content)
        {
            List<ChapterInfo> chapters = new List<ChapterInfo>();
            if (string.IsNullOrEmpty(content))
            {
                return chapters;
            }

            int lineStart = 0;
            for (int cursor = 0; cursor <= content.Length; cursor++)
            {
                bool atEnd = cursor == content.Length;
                if (!atEnd && content[cursor] != '\n')
                {
                    continue;
                }

                int lineLength = cursor - lineStart;
                if (lineLength > 0 && content[lineStart + lineLength - 1] == '\r')
                {
                    lineLength--;
                }

                if (lineLength > 0 && lineLength <= MaximumHeadingLength + 16)
                {
                    string rawLine = content.Substring(lineStart, lineLength);
                    string title = rawLine.Trim();
                    if (title.Length > 0 && title.Length <= MaximumHeadingLength && HeadingPrefix.IsMatch(title))
                    {
                        chapters.Add(new ChapterInfo(title, lineStart));
                    }
                }

                lineStart = cursor + 1;
            }

            return chapters;
        }
    }
}
