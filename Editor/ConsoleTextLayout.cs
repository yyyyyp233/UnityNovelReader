using System;
using System.Collections.Generic;

namespace UnityNovelReader.Editor
{
    internal enum ConsoleTextRole
    {
        Ordinary,
        ParagraphStart,
        ChapterTitle
    }

    internal sealed class ConsoleTextSegment
    {
        internal ConsoleTextSegment(int start, string text)
            : this(start, text, text)
        {
        }

        internal ConsoleTextSegment(int start, string text, string sourceText)
        {
            Start = start;
            Text = text;
            SourceText = sourceText;
        }

        internal int Start { get; private set; }
        internal string Text { get; private set; }
        internal string SourceText { get; private set; }

        internal void AppendSourceText(string text)
        {
            SourceText += text;
        }
    }

    internal static class ConsoleTextLayout
    {
        internal static List<ConsoleTextSegment> BuildSegments(
            string text,
            Func<string, float> measureHeight,
            float preferredMaximumHeight)
        {
            if (measureHeight == null)
            {
                throw new ArgumentNullException("measureHeight");
            }

            var segments = new List<ConsoleTextSegment>();
            if (string.IsNullOrEmpty(text))
            {
                return segments;
            }

            float maximumHeight = Math.Max(1f, preferredMaximumHeight);
            int start = 0;
            while (start < text.Length)
            {
                int maximumLength = FindMaximumFittingLength(text, start, measureHeight, maximumHeight);
                int preferredLength = FindPreferredBreakLength(text, start, maximumLength);
                int safeLength = KeepTextElementsTogether(text, start, preferredLength);
                segments.Add(new ConsoleTextSegment(start, text.Substring(start, safeLength)));
                start += safeLength;
            }

            return segments;
        }

        internal static List<ConsoleTextSegment> BuildVisibleSegments(
            string text,
            Func<string, float> measureHeight,
            float preferredMaximumHeight)
        {
            List<ConsoleTextSegment> sourceSegments = BuildSegments(
                text,
                measureHeight,
                preferredMaximumHeight);
            var visibleSegments = new List<ConsoleTextSegment>();
            string pendingSourceText = string.Empty;
            int pendingSourceStart = -1;
            for (int i = 0; i < sourceSegments.Count; i++)
            {
                ConsoleTextSegment segment = sourceSegments[i];
                if (pendingSourceText.Length == 0)
                {
                    pendingSourceStart = segment.Start;
                }

                pendingSourceText += segment.SourceText;
                string visibleText = segment.Text.Trim();
                if (string.IsNullOrWhiteSpace(visibleText))
                {
                    continue;
                }

                visibleSegments.Add(new ConsoleTextSegment(
                    pendingSourceStart,
                    visibleText,
                    pendingSourceText));
                pendingSourceText = string.Empty;
                pendingSourceStart = -1;
            }

            if (pendingSourceText.Length > 0 && visibleSegments.Count > 0)
            {
                visibleSegments[visibleSegments.Count - 1].AppendSourceText(pendingSourceText);
            }

            return visibleSegments;
        }

        internal static ConsoleTextRole ClassifySegment(
            ConsoleTextSegment segment,
            string fullText,
            int pageStart,
            IList<ChapterInfo> chapters)
        {
            if (segment == null)
            {
                throw new ArgumentNullException("segment");
            }

            int sourceStart = Math.Max(0, pageStart + segment.Start);
            int sourceLength = string.IsNullOrEmpty(segment.SourceText) ? 0 : segment.SourceText.Length;
            int sourceEnd = sourceStart + sourceLength;
            if (chapters != null)
            {
                for (int i = 0; i < chapters.Count; i++)
                {
                    ChapterInfo chapter = chapters[i];
                    if (chapter != null && chapter.Offset >= sourceStart && chapter.Offset < sourceEnd)
                    {
                        return ConsoleTextRole.ChapterTitle;
                    }
                }
            }

            if (sourceStart == 0
                || (!string.IsNullOrEmpty(segment.SourceText) && char.IsWhiteSpace(segment.SourceText[0])))
            {
                return ConsoleTextRole.ParagraphStart;
            }

            if (!string.IsNullOrEmpty(fullText) && sourceStart <= fullText.Length)
            {
                char previous = fullText[sourceStart - 1];
                if (previous == '\r' || previous == '\n')
                {
                    return ConsoleTextRole.ParagraphStart;
                }
            }

            return ConsoleTextRole.Ordinary;
        }

        private static int FindMaximumFittingLength(
            string text,
            int start,
            Func<string, float> measureHeight,
            float maximumHeight)
        {
            int remaining = text.Length - start;
            int low = 1;
            int high = remaining;
            int best = 1;
            while (low <= high)
            {
                int candidateLength = low + ((high - low) / 2);
                string candidate = text.Substring(start, candidateLength);
                if (candidateLength == 1 || measureHeight(candidate) <= maximumHeight)
                {
                    best = candidateLength;
                    low = candidateLength + 1;
                }
                else
                {
                    high = candidateLength - 1;
                }
            }

            return best;
        }

        private static int FindPreferredBreakLength(string text, int start, int maximumLength)
        {
            if (start + maximumLength >= text.Length || maximumLength < 4)
            {
                return maximumLength;
            }

            int minimumLength = Math.Max(1, (int)(maximumLength * 0.65f));
            for (int length = maximumLength; length >= minimumLength; length--)
            {
                char candidate = text[start + length - 1];
                if (IsNaturalBreak(candidate))
                {
                    return length;
                }
            }

            return maximumLength;
        }

        private static bool IsNaturalBreak(char value)
        {
            return char.IsWhiteSpace(value)
                || value == '。'
                || value == '！'
                || value == '？'
                || value == '；'
                || value == '，'
                || value == '、'
                || value == '.'
                || value == '!'
                || value == '?'
                || value == ';'
                || value == ',';
        }

        private static int KeepTextElementsTogether(string text, int start, int requestedLength)
        {
            int remaining = text.Length - start;
            int length = Math.Max(1, Math.Min(requestedLength, remaining));

            if (start + length < text.Length)
            {
                char previous = text[start + length - 1];
                char next = text[start + length];
                if ((char.IsHighSurrogate(previous) && char.IsLowSurrogate(next))
                    || (previous == '\r' && next == '\n'))
                {
                    length--;
                }
            }

            if (length > 0)
            {
                return length;
            }

            if (remaining >= 2
                && ((char.IsHighSurrogate(text[start]) && char.IsLowSurrogate(text[start + 1]))
                    || (text[start] == '\r' && text[start + 1] == '\n')))
            {
                return 2;
            }

            return 1;
        }
    }
}
