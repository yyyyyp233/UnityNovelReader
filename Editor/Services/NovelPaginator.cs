using System;
using System.Collections.Generic;

namespace UnityNovelReader.Editor
{
    internal static class NovelPaginator
    {
        internal static PageSlice GetPage(string content, int requestedOffset, int charactersPerPage)
        {
            if (content == null)
            {
                content = string.Empty;
            }

            charactersPerPage = Math.Max(1, charactersPerPage);
            int start = Math.Max(0, Math.Min(content.Length, requestedOffset));
            if (start > 0 && start < content.Length && char.IsLowSurrogate(content[start]) && char.IsHighSurrogate(content[start - 1]))
            {
                start--;
            }

            int end = Math.Min(content.Length, start + charactersPerPage);
            if (end < content.Length && end > start && char.IsHighSurrogate(content[end - 1]) && char.IsLowSurrogate(content[end]))
            {
                end++;
            }

            return new PageSlice
            {
                StartOffset = start,
                EndOffset = end,
                Text = content.Substring(start, end - start)
            };
        }

        internal static int GetPreviousOffset(int currentOffset, int charactersPerPage)
        {
            return Math.Max(0, currentOffset - Math.Max(1, charactersPerPage));
        }

        internal static float GetProgress(int offset, int contentLength)
        {
            if (contentLength <= 0)
            {
                return 0f;
            }

            return Math.Max(0f, Math.Min(1f, (float)offset / contentLength));
        }

        internal static int FindChapterIndex(IList<ChapterInfo> chapters, int offset)
        {
            if (chapters == null || chapters.Count == 0)
            {
                return -1;
            }

            int low = 0;
            int high = chapters.Count - 1;
            int result = -1;
            while (low <= high)
            {
                int middle = low + ((high - low) / 2);
                if (chapters[middle].Offset <= offset)
                {
                    result = middle;
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return result;
        }
    }
}
