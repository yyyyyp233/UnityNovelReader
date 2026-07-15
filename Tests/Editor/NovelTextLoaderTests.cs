using System;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace UnityNovelReader.Editor.Tests
{
    internal sealed class NovelTextLoaderTests
    {
        private string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "UnityNovelReaderTextTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void Load_ReadsUtf8AndBuildsChapterIndex()
        {
            string path = Path.Combine(directory, "sample.txt");
            File.WriteAllText(path, "第1章 开始\n正文\n第2章 继续\n正文", new UTF8Encoding(false));

            NovelDocument document = NovelTextLoader.Load(path);

            Assert.That(document.Title, Is.EqualTo("sample"));
            Assert.That(document.EncodingName, Is.EqualTo("UTF-8"));
            Assert.That(document.Chapters.Count, Is.EqualTo(2));
        }

        [Test]
        public void Load_ReadsGb18030()
        {
            string path = Path.Combine(directory, "gb18030.txt");
            Encoding encoding = Encoding.GetEncoding(54936);
            File.WriteAllBytes(path, encoding.GetBytes("第1章 牧神\n正文内容"));

            NovelDocument document = NovelTextLoader.Load(path);

            Assert.That(document.EncodingName, Is.EqualTo("GB18030"));
            Assert.That(document.Content, Does.Contain("牧神"));
            Assert.That(document.Chapters.Count, Is.EqualTo(1));
        }

        [Test]
        public void Load_OptionalExternalSmokeFile_WhenConfigured()
        {
            string path = Environment.GetEnvironmentVariable("UNITY_NOVEL_READER_SMOKE_FILE");
            if (string.IsNullOrWhiteSpace(path))
            {
                Assert.Ignore("UNITY_NOVEL_READER_SMOKE_FILE is not configured.");
            }

            NovelDocument document = NovelTextLoader.Load(path);
            Assert.That(document.Content.Length, Is.GreaterThan(0));
            Assert.That(document.Chapters.Count, Is.GreaterThan(0));

            string expectedValue = Environment.GetEnvironmentVariable("UNITY_NOVEL_READER_EXPECTED_CHAPTERS");
            int expected;
            if (int.TryParse(expectedValue, out expected))
            {
                Assert.That(document.Chapters.Count, Is.EqualTo(expected));
            }

            TestContext.WriteLine(
                "Loaded '{0}' using {1}: {2} characters, {3} chapters.",
                document.Title,
                document.EncodingName,
                document.Content.Length,
                document.Chapters.Count);
        }
    }
}
