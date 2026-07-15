using System.Collections.Generic;
using NUnit.Framework;

namespace UnityNovelReader.Editor.Tests
{
    internal sealed class NovelPaginatorTests
    {
        [Test]
        public void GetPage_ClampsOffsetsAndPreservesSurrogatePairs()
        {
            const string content = "甲乙😀丙丁";

            PageSlice first = NovelPaginator.GetPage(content, -20, 3);
            PageSlice second = NovelPaginator.GetPage(content, first.EndOffset, 3);

            Assert.That(first.StartOffset, Is.EqualTo(0));
            Assert.That(char.IsHighSurrogate(first.Text[first.Text.Length - 1]), Is.False);
            Assert.That(first.Text + second.Text, Is.EqualTo(content));
        }

        [Test]
        public void FindChapterIndex_ReturnsCurrentChapter()
        {
            var chapters = new List<ChapterInfo>
            {
                new ChapterInfo("One", 10),
                new ChapterInfo("Two", 50),
                new ChapterInfo("Three", 100)
            };

            Assert.That(NovelPaginator.FindChapterIndex(chapters, 0), Is.EqualTo(-1));
            Assert.That(NovelPaginator.FindChapterIndex(chapters, 10), Is.EqualTo(0));
            Assert.That(NovelPaginator.FindChapterIndex(chapters, 99), Is.EqualTo(1));
            Assert.That(NovelPaginator.FindChapterIndex(chapters, 100), Is.EqualTo(2));
        }
    }
}
