using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.ShortcutManagement;

namespace UnityNovelReader.Editor.Tests
{
    internal sealed class ReaderStateStoreTests
    {
        private string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "UnityNovelReaderTests", Guid.NewGuid().ToString("N"));
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
        public void SaveAndLoad_RoundTripsProgressBookmarksAndPreferences()
        {
            string path = Path.Combine(directory, "state.json");
            var store = new ReaderStateStore(path);
            var state = new ReaderStateData();
            state.lastBookId = "book-1";
            state.preferences.fontSize = 19;
            state.preferences.consoleFontSize = 14;
            state.preferences.showSidebar = false;
            state.preferences.appearance = ReaderAppearance.Classic;
            state.preferences.colorWarningRows = false;
            state.preferences.simulateConsoleHeaders = true;
            state.preferences.strongHoverDisguise = true;
            state.preferences.decoyWindow = DecoyWindowTarget.Profiler;
            state.books.Add(new BookState
            {
                id = "book-1",
                filePath = "D:/Books/Test.txt",
                title = "Test",
                charOffset = 1234,
                bookmarks =
                {
                    new BookmarkState { id = "mark-1", title = "Chapter", charOffset = 1000 }
                }
            });

            store.Save(state);
            ReaderStateData loaded = store.Load();

            Assert.That(loaded.lastBookId, Is.EqualTo("book-1"));
            Assert.That(loaded.preferences.fontSize, Is.EqualTo(19));
            Assert.That(loaded.preferences.consoleFontSize, Is.EqualTo(14));
            Assert.That(loaded.preferences.showSidebar, Is.False);
            Assert.That(loaded.preferences.appearance, Is.EqualTo(ReaderAppearance.Classic));
            Assert.That(loaded.preferences.colorWarningRows, Is.False);
            Assert.That(loaded.preferences.simulateConsoleHeaders, Is.True);
            Assert.That(loaded.preferences.strongHoverDisguise, Is.True);
            Assert.That(loaded.preferences.decoyWindow, Is.EqualTo(DecoyWindowTarget.Profiler));
            Assert.That(loaded.books.Count, Is.EqualTo(1));
            Assert.That(loaded.books[0].charOffset, Is.EqualTo(1234));
            Assert.That(loaded.books[0].bookmarks.Count, Is.EqualTo(1));
        }

        [Test]
        public void DefaultDataDirectory_IsNotProjectRelative()
        {
            string dataDirectory = Path.GetFullPath(ReaderStateStore.GetDefaultDataDirectory());
            string currentDirectory = Path.GetFullPath(Directory.GetCurrentDirectory()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

            Assert.That(dataDirectory.StartsWith(currentDirectory, StringComparison.OrdinalIgnoreCase), Is.False);
        }

        [Test]
        public void Load_LegacyStateDefaultsDecoyToConsole()
        {
            string path = Path.Combine(directory, "legacy-state.json");
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                path,
                "{\"schemaVersion\":1,\"preferences\":{\"fontSize\":16,\"charactersPerPage\":900}}");

            ReaderStateData loaded = new ReaderStateStore(path).Load();

            Assert.That(loaded.schemaVersion, Is.EqualTo(ReaderStateData.CurrentSchemaVersion));
            Assert.That(loaded.preferences.decoyWindow, Is.EqualTo(DecoyWindowTarget.Console));
            Assert.That(loaded.preferences.appearance, Is.EqualTo(ReaderAppearance.Console));
            Assert.That(loaded.preferences.showSidebar, Is.False);
        }

        [Test]
        public void Load_SchemaTwoStateMigratesToConsoleAppearance()
        {
            string path = Path.Combine(directory, "schema-two-state.json");
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                path,
                "{\"schemaVersion\":2,\"preferences\":{\"fontSize\":16,\"charactersPerPage\":900,\"showSidebar\":true}}");

            ReaderStateData loaded = new ReaderStateStore(path).Load();

            Assert.That(loaded.schemaVersion, Is.EqualTo(ReaderStateData.CurrentSchemaVersion));
            Assert.That(loaded.preferences.appearance, Is.EqualTo(ReaderAppearance.Console));
            Assert.That(loaded.preferences.showSidebar, Is.False);
            Assert.That(loaded.preferences.consoleFontSize, Is.EqualTo(12));
            Assert.That(loaded.preferences.colorInfoRows, Is.True);
            Assert.That(loaded.preferences.colorWarningRows, Is.True);
            Assert.That(loaded.preferences.colorErrorRows, Is.True);
            Assert.That(loaded.preferences.simulateConsoleHeaders, Is.False);
        }

        [Test]
        public void Load_SchemaThreeStateDisablesSyntheticHeadersByDefault()
        {
            string path = Path.Combine(directory, "schema-three-state.json");
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                path,
                "{\"schemaVersion\":3,\"preferences\":{\"fontSize\":16,\"charactersPerPage\":900,\"appearance\":0}}");

            ReaderStateData loaded = new ReaderStateStore(path).Load();

            Assert.That(loaded.schemaVersion, Is.EqualTo(ReaderStateData.CurrentSchemaVersion));
            Assert.That(loaded.preferences.simulateConsoleHeaders, Is.False);
        }

        [Test]
        public void Load_SchemaFourStateDisablesStrongHoverDisguiseByDefault()
        {
            string path = Path.Combine(directory, "schema-four-state.json");
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                path,
                "{\"schemaVersion\":4,\"preferences\":{\"fontSize\":16,\"charactersPerPage\":900,\"appearance\":0}}");

            ReaderStateData loaded = new ReaderStateStore(path).Load();

            Assert.That(loaded.schemaVersion, Is.EqualTo(ReaderStateData.CurrentSchemaVersion));
            Assert.That(loaded.preferences.strongHoverDisguise, Is.False);
        }

        [TestCase(DecoyWindowTarget.Scene, "Window/General/Scene")]
        [TestCase(DecoyWindowTarget.Console, "Window/General/Console")]
        [TestCase(DecoyWindowTarget.Profiler, "Window/Analysis/Profiler")]
        [TestCase(DecoyWindowTarget.Animator, "Window/Animation/Animator")]
        [TestCase(DecoyWindowTarget.Project, "Window/General/Project")]
        public void DecoyWindowTarget_MapsToExpectedUnityMenu(DecoyWindowTarget target, string expectedPath)
        {
            Assert.That(NovelReaderWindow.GetDecoyMenuPath(target), Is.EqualTo(expectedPath));
        }

        [Test]
        public void PreferencesNormalize_UnknownDecoyFallsBackToConsole()
        {
            var preferences = new ReaderPreferences { decoyWindow = (DecoyWindowTarget)999 };

            preferences.Normalize();

            Assert.That(preferences.decoyWindow, Is.EqualTo(DecoyWindowTarget.Console));
        }

        [Test]
        public void PreferencesNormalize_ClampsConsoleFontAndUnknownAppearance()
        {
            var preferences = new ReaderPreferences
            {
                consoleFontSize = 99,
                appearance = (ReaderAppearance)999
            };

            preferences.Normalize();

            Assert.That(preferences.consoleFontSize, Is.EqualTo(16));
            Assert.That(preferences.appearance, Is.EqualTo(ReaderAppearance.Console));
        }

        [Test]
        public void ReaderShortcuts_AreRegisteredWithUnity()
        {
            string[] availableIds = ShortcutManager.instance.GetAvailableShortcutIds().ToArray();

            CollectionAssert.Contains(availableIds, NovelReaderShortcuts.ToggleShortcutId);
            CollectionAssert.Contains(availableIds, NovelReaderShortcuts.QuickHideShortcutId);
        }

        [Test]
        public void EditorAssembly_EmbedsCompleteMitLicenseNotice()
        {
            AssemblyMetadataAttribute license = typeof(ReaderStateData).Assembly
                .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
                .Cast<AssemblyMetadataAttribute>()
                .Single(attribute => attribute.Key == "LicenseText");

            StringAssert.Contains("MIT License", license.Value);
            StringAssert.Contains("Copyright (c) 2026 Unity Novel Reader Contributors", license.Value);
            StringAssert.Contains("The above copyright notice and this permission notice", license.Value);
        }
    }
}
