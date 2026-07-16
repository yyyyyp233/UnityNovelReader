using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace UnityNovelReader.Editor.Tests
{
    internal sealed class ConsoleTextLayoutTests
    {
        [Test]
        public void BuildSegments_ReconstructsOriginalTextWithoutLoss()
        {
            const string text = "第1章 开始\r\n\r\n第一段正文😀。\nSecond paragraph remains complete.";

            var segments = ConsoleTextLayout.BuildSegments(text, value => value.Length, 9f);

            Assert.That(string.Concat(segments.Select(segment => segment.Text)), Is.EqualTo(text));
            Assert.That(segments.Select(segment => segment.Start), Is.Ordered);
        }

        [Test]
        public void BuildSegments_UsesPreferredHeightWithoutClippingOrdinaryText()
        {
            const string text = "abcdefghijklmnopqrstuvwxyz0123456789";

            var segments = ConsoleTextLayout.BuildSegments(text, value => value.Length, 7f);

            Assert.That(segments, Is.Not.Empty);
            Assert.That(segments.All(segment => segment.Text.Length <= 7), Is.True);
            Assert.That(string.Concat(segments.Select(segment => segment.Text)), Is.EqualTo(text));
        }

        [Test]
        public void BuildSegments_OneLineBodyBudgetKeepsEveryOrdinarySegmentVisible()
        {
            const string text = "第一段正文需要被安全拆成多条Console明细行并保持原始顺序";
            System.Func<string, float> measureHeight = value =>
                (float)System.Math.Ceiling(value.Length / 8f) * 16f;

            var segments = ConsoleTextLayout.BuildSegments(text, measureHeight, 16.5f);

            Assert.That(segments, Is.Not.Empty);
            Assert.That(segments.All(segment => measureHeight(segment.Text) <= 16.5f), Is.True);
            Assert.That(string.Concat(segments.Select(segment => segment.Text)), Is.EqualTo(text));
        }

        [Test]
        public void BuildSegments_DoesNotSplitSurrogatePairsOrCrLf()
        {
            const string text = "甲😀乙\r\n丙😀丁";

            var segments = ConsoleTextLayout.BuildSegments(text, value => value.Length, 1f);

            Assert.That(string.Concat(segments.Select(segment => segment.Text)), Is.EqualTo(text));
            for (int i = 0; i < segments.Count - 1; i++)
            {
                string left = segments[i].Text;
                string right = segments[i + 1].Text;
                Assert.That(char.IsHighSurrogate(left[left.Length - 1]) && char.IsLowSurrogate(right[0]), Is.False);
                Assert.That(left[left.Length - 1] == '\r' && right[0] == '\n', Is.False);
            }
        }

        [Test]
        public void BuildSegments_EmptyTextProducesNoRows()
        {
            var segments = ConsoleTextLayout.BuildSegments(string.Empty, value => value.Length, 20f);

            Assert.That(segments, Is.Empty);
        }

        [Test]
        public void BuildVisibleSegments_FoldsBlankLinesWithoutLosingSourceText()
        {
            const string text = "\r\n\u3000\u3000First paragraph\r\n\r\n\t Second paragraph\r\n";

            var segments = ConsoleTextLayout.BuildVisibleSegments(text, value => value.Length, 8f);

            Assert.That(segments, Is.Not.Empty);
            Assert.That(segments.All(segment => !string.IsNullOrWhiteSpace(segment.Text)), Is.True);
            Assert.That(segments.All(segment => !char.IsWhiteSpace(segment.Text[0])), Is.True);
            Assert.That(segments.All(segment => !char.IsWhiteSpace(segment.Text[segment.Text.Length - 1])), Is.True);
            Assert.That(segments[0].Start, Is.EqualTo(0));
            Assert.That(string.Concat(segments.Select(segment => segment.SourceText)), Is.EqualTo(text));
        }

        [Test]
        public void ClassifySegment_UsesChapterThenParagraphThenOrdinaryPriority()
        {
            const string text = "\u7b2c1\u7ae0 \u5f00\u59cb\r\n\u3000\u3000\u7b2c\u4e00\u6bb5\uff0c\u540e\u534a\u6bb5";
            int paragraphOffset = text.IndexOf("\u3000\u3000");
            int ordinaryOffset = text.IndexOf("\u540e\u534a\u6bb5");
            var chapters = new List<ChapterInfo> { new ChapterInfo("\u7b2c1\u7ae0 \u5f00\u59cb", 0) };
            var chapter = new ConsoleTextSegment(0, "\u7b2c1\u7ae0 \u5f00\u59cb", text.Substring(0, paragraphOffset));
            var paragraph = new ConsoleTextSegment(paragraphOffset, "\u7b2c\u4e00\u6bb5", "\u3000\u3000\u7b2c\u4e00\u6bb5\uff0c");
            var ordinary = new ConsoleTextSegment(ordinaryOffset, "\u540e\u534a\u6bb5", "\u540e\u534a\u6bb5");

            Assert.That(ConsoleTextLayout.ClassifySegment(chapter, text, 0, chapters), Is.EqualTo(ConsoleTextRole.ChapterTitle));
            Assert.That(ConsoleTextLayout.ClassifySegment(paragraph, text, 0, chapters), Is.EqualTo(ConsoleTextRole.ParagraphStart));
            Assert.That(ConsoleTextLayout.ClassifySegment(ordinary, text, 0, chapters), Is.EqualTo(ConsoleTextRole.Ordinary));
        }

        [Test]
        public void MuteConsoleIconPixel_RemovesSeverityColorAndPreservesAlpha()
        {
            var source = new UnityEngine.Color32(255, 180, 0, 191);

            UnityEngine.Color32 muted = NovelReaderWindow.MuteConsoleIconPixel(source);

            Assert.That(muted.r, Is.EqualTo(muted.g));
            Assert.That(muted.g, Is.EqualTo(muted.b));
            Assert.That(muted.r, Is.LessThan(source.r));
            Assert.That(muted.a, Is.EqualTo(source.a));
        }

        [Test]
        public void CreateMutedConsoleIcon_ConvertsUnreadableBuiltInIconToGrayscale()
        {
            UnityEngine.Texture source = UnityEditor.EditorGUIUtility.IconContent("console.warnicon").image;
            Assert.That(source, Is.Not.Null);

            UnityEngine.Texture2D muted = NovelReaderWindow.CreateMutedConsoleIcon(source);
            try
            {
                Assert.That(muted.width, Is.EqualTo(source.width));
                Assert.That(muted.height, Is.EqualTo(source.height));
                Assert.That(muted.GetPixels32().All(pixel => pixel.r == pixel.g && pixel.g == pixel.b), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(muted);
            }
        }

        [Test]
        public void ConvertConsoleMouseToContent_RemovesViewportOriginAndAddsScroll()
        {
            var viewport = new UnityEngine.Rect(250f, 40f, 700f, 500f);
            var windowMouse = new UnityEngine.Vector2(310f, 135f);
            var scroll = new UnityEngine.Vector2(0f, 80f);

            UnityEngine.Vector2 contentMouse = NovelReaderWindow.ConvertConsoleMouseToContent(
                windowMouse,
                viewport,
                scroll);

            Assert.That(contentMouse.x, Is.EqualTo(60f));
            Assert.That(contentMouse.y, Is.EqualTo(175f));
        }

        [Test]
        public void HasValidConsoleIcon_RejectsDestroyedTextureReference()
        {
            var texture = new UnityEngine.Texture2D(1, 1);
            var content = new UnityEngine.GUIContent { image = texture };
            Assert.That(NovelReaderWindow.HasValidConsoleIcon(content), Is.True);

            UnityEngine.Object.DestroyImmediate(texture);

            Assert.That(NovelReaderWindow.HasValidConsoleIcon(content), Is.False);
        }

        [Test]
        public void ResolveReaderScrollAfterBookLoad_PreservesAutomaticRestorePosition()
        {
            var currentScroll = new UnityEngine.Vector2(0f, 428f);

            Assert.That(
                NovelReaderWindow.ResolveReaderScrollAfterBookLoad(currentScroll, false),
                Is.EqualTo(currentScroll));
            Assert.That(
                NovelReaderWindow.ResolveReaderScrollAfterBookLoad(currentScroll, true),
                Is.EqualTo(UnityEngine.Vector2.zero));
        }

        [Test]
        public void ReaderScroll_IsSerializedAcrossDomainReload()
        {
            System.Reflection.FieldInfo field = typeof(NovelReaderWindow).GetField(
                "readerScroll",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null);
            Assert.That(field.IsDefined(typeof(UnityEngine.SerializeField), false), Is.True);
        }
    }
}
