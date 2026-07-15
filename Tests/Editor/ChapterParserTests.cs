using NUnit.Framework;

namespace UnityNovelReader.Editor.Tests
{
    internal sealed class ChapterParserTests
    {
        [Test]
        public void Parse_FindsNumberedAndSpecialHeadingsWithoutTreatingProseAsHeadings()
        {
            string content =
                "序章 风起\n" +
                "这是正文。\n" +
                "第1章 天黑别出门\n" +
                "第二日天色阴沉，这只是正文。\n" +
                "第 2 章 四灵血\n" +
                "Chapter 3 A Visitor\n" +
                "番外篇 重逢\n";

            var chapters = ChapterParser.Parse(content);

            Assert.That(chapters.Count, Is.EqualTo(5));
            Assert.That(chapters[0].Title, Is.EqualTo("序章 风起"));
            Assert.That(chapters[1].Title, Is.EqualTo("第1章 天黑别出门"));
            Assert.That(chapters[2].Title, Is.EqualTo("第 2 章 四灵血"));
            Assert.That(chapters[4].Title, Is.EqualTo("番外篇 重逢"));
        }

        [Test]
        public void Parse_RecordsCharacterOffsets()
        {
            const string content = "前言\r\n第1章 开始\r\n正文";

            var chapters = ChapterParser.Parse(content);

            Assert.That(chapters.Count, Is.EqualTo(1));
            Assert.That(chapters[0].Offset, Is.EqualTo(content.IndexOf("第1章", System.StringComparison.Ordinal)));
        }
    }
}
