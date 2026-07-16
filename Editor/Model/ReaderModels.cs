using System;
using System.Collections.Generic;

namespace UnityNovelReader.Editor
{
    internal enum DecoyWindowTarget
    {
        Scene,
        Console,
        Profiler,
        Animator,
        Project
    }

    internal enum ReaderAppearance
    {
        Console,
        Classic
    }

    internal sealed class ChapterInfo
    {
        internal ChapterInfo(string title, int offset)
        {
            Title = title;
            Offset = offset;
        }

        internal string Title { get; private set; }
        internal int Offset { get; private set; }
    }

    internal sealed class NovelDocument
    {
        internal string FilePath;
        internal string Title;
        internal string EncodingName;
        internal string Content;
        internal long FileLength;
        internal long LastWriteUtcTicks;
        internal List<ChapterInfo> Chapters;
    }

    [Serializable]
    internal sealed class ReaderStateData
    {
        internal const int CurrentSchemaVersion = 5;

        public int schemaVersion = CurrentSchemaVersion;
        public string lastBookId = string.Empty;
        public ReaderPreferences preferences = new ReaderPreferences();
        public List<BookState> books = new List<BookState>();

        internal void EnsureDefaults()
        {
            if (preferences == null)
            {
                preferences = new ReaderPreferences();
            }

            if (schemaVersion < 2)
            {
                preferences.decoyWindow = DecoyWindowTarget.Console;
            }

            if (schemaVersion < 3)
            {
                preferences.appearance = ReaderAppearance.Console;
                preferences.showSidebar = false;
                preferences.consoleFontSize = 12;
                preferences.colorInfoRows = true;
                preferences.colorWarningRows = true;
                preferences.colorErrorRows = true;
            }

            if (schemaVersion < 4)
            {
                preferences.simulateConsoleHeaders = false;
            }

            if (schemaVersion < 5)
            {
                preferences.strongHoverDisguise = false;
            }

            schemaVersion = CurrentSchemaVersion;

            preferences.Normalize();
            if (books == null)
            {
                books = new List<BookState>();
            }

            for (int i = 0; i < books.Count; i++)
            {
                if (books[i] != null)
                {
                    books[i].EnsureDefaults();
                }
            }
        }
    }

    [Serializable]
    internal sealed class ReaderPreferences
    {
        public int fontSize = 16;
        public int consoleFontSize = 12;
        public int charactersPerPage = 900;
        public bool showSidebar;
        public bool useDarkPage = true;
        public bool colorInfoRows = true;
        public bool colorWarningRows = true;
        public bool colorErrorRows = true;
        public bool simulateConsoleHeaders;
        public bool strongHoverDisguise;
        public ReaderAppearance appearance = ReaderAppearance.Console;
        public DecoyWindowTarget decoyWindow = DecoyWindowTarget.Console;

        internal void Normalize()
        {
            fontSize = Math.Max(11, Math.Min(32, fontSize));
            consoleFontSize = Math.Max(11, Math.Min(16, consoleFontSize));
            charactersPerPage = Math.Max(200, Math.Min(5000, charactersPerPage));
            if (!Enum.IsDefined(typeof(ReaderAppearance), appearance))
            {
                appearance = ReaderAppearance.Console;
            }

            if (!Enum.IsDefined(typeof(DecoyWindowTarget), decoyWindow))
            {
                decoyWindow = DecoyWindowTarget.Console;
            }
        }
    }

    [Serializable]
    internal sealed class BookState
    {
        public string id = string.Empty;
        public string filePath = string.Empty;
        public string title = string.Empty;
        public string encodingName = string.Empty;
        public long fileLength;
        public long lastWriteUtcTicks;
        public int charOffset;
        public long lastOpenedUtcTicks;
        public List<BookmarkState> bookmarks = new List<BookmarkState>();

        internal void EnsureDefaults()
        {
            if (id == null) id = string.Empty;
            if (filePath == null) filePath = string.Empty;
            if (title == null) title = string.Empty;
            if (encodingName == null) encodingName = string.Empty;
            if (bookmarks == null) bookmarks = new List<BookmarkState>();
            charOffset = Math.Max(0, charOffset);
        }
    }

    [Serializable]
    internal sealed class BookmarkState
    {
        public string id = string.Empty;
        public string title = string.Empty;
        public int charOffset;
        public long createdUtcTicks;
    }

    internal sealed class PageSlice
    {
        internal int StartOffset;
        internal int EndOffset;
        internal string Text;
    }
}
