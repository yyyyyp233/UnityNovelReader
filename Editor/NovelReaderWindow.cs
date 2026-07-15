using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace UnityNovelReader.Editor
{
    internal sealed class NovelReaderWindow : EditorWindow
    {
        private const float SidebarWidth = 250f;
        private const float CollapsedSidebarWidth = 28f;
        private const float SidebarRowHeight = 22f;
        private const float ConsoleIconGutterWidth = 40f;
        private const float ConsoleIconSize = 32f;
        private const float ConsoleRowVerticalPadding = 3f;
        private const float ConsoleMinimumRowHeight = 40f;

        private enum ConsoleRowSeverity
        {
            Info,
            Warning,
            Error
        }

        private sealed class ConsoleRenderRow
        {
            internal string Header;
            internal string Text;
            internal string SourceText;
            internal ConsoleRowSeverity Severity;
            internal float Top;
            internal float Height;
            internal float HeaderHeight;
            internal float TextHeight;
        }

        private ReaderStateStore stateStore;
        private ReaderStateData state;
        private NovelDocument document;
        private BookState activeBook;
        private PageSlice currentPage;
        private Vector2 readerScroll;
        private Vector2 chapterScroll;
        private Vector2 bookmarkScroll;
        private Vector2 settingsScroll;
        [SerializeField] private bool consoleBufferCleared;
        private string chapterFilter = string.Empty;
        private string appliedChapterFilter = null;
        private string statusMessage = string.Empty;
        private int sidebarTab;
        private int mainPage;
        private bool shortcutDraftLoaded;
        private KeyCode toggleShortcutKey = KeyCode.R;
        private ShortcutModifiers toggleShortcutModifiers = ShortcutModifiers.Control | ShortcutModifiers.Alt;
        private KeyCode quickHideShortcutKey = KeyCode.H;
        private ShortcutModifiers quickHideShortcutModifiers = ShortcutModifiers.Control | ShortcutModifiers.Alt;
        private string settingsStatusMessage = string.Empty;
        private bool settingsStatusIsError;
        private readonly List<int> filteredChapterIndices = new List<int>();
        private readonly List<ConsoleRenderRow> consoleRows = new List<ConsoleRenderRow>();
        private float consoleRowsHeight;
        private float cachedConsoleWidth = -1f;
        private int cachedConsolePageStart = -1;
        private int cachedConsolePageEnd = -1;
        private int cachedConsoleFontSize = -1;
        private int consoleInfoCount;
        private int consoleWarningCount;
        private int consoleErrorCount;
        private GUIStyle readerStyle;
        private GUIStyle chapterRowStyle;
        private GUIStyle selectedChapterRowStyle;
        private GUIStyle consoleHeaderStyle;
        private GUIStyle consoleTextStyle;
        private GUIContent consoleInfoIcon;
        private GUIContent consoleWarningIcon;
        private GUIContent consoleErrorIcon;
        private GUIContent consoleInfoRowIcon;
        private GUIContent consoleWarningRowIcon;
        private GUIContent consoleErrorRowIcon;
        private Texture2D mutedConsoleInfoRowIcon;
        private Texture2D mutedConsoleWarningRowIcon;
        private Texture2D mutedConsoleErrorRowIcon;
        private bool mutedConsoleIconCreationFailed;

        [MenuItem("Tools/Unity Novel Reader/Open Reader", false, 2100)]
        internal static NovelReaderWindow OpenWindow()
        {
            NovelReaderWindow window = GetWindow<NovelReaderWindow>();
            window.UpdateWindowTitle();
            window.minSize = new Vector2(420f, 320f);
            window.Show();
            window.Focus();
            return window;
        }

        [MenuItem("Tools/Unity Novel Reader/Open Data Folder", false, 2102)]
        private static void OpenDataFolderMenu()
        {
            string directory = ReaderStateStore.GetDefaultDataDirectory();
            Directory.CreateDirectory(directory);
            EditorUtility.RevealInFinder(directory);
        }

        internal static void ToggleWindow(bool focusDecoyWhenHiding)
        {
            NovelReaderWindow existing = FindOpenWindow();
            if (existing != null)
            {
                DecoyWindowTarget decoyTarget = existing.state != null && existing.state.preferences != null
                    ? existing.state.preferences.decoyWindow
                    : DecoyWindowTarget.Console;
                existing.SaveState();
                existing.Close();
                if (focusDecoyWhenHiding)
                {
                    EditorApplication.delayCall += delegate { FocusDecoyWindow(decoyTarget); };
                }

                return;
            }

            OpenWindow();
        }

        private static NovelReaderWindow FindOpenWindow()
        {
            NovelReaderWindow[] windows = Resources.FindObjectsOfTypeAll<NovelReaderWindow>();
            return windows.Length > 0 ? windows[0] : null;
        }

        internal static string GetDecoyMenuPath(DecoyWindowTarget target)
        {
            switch (target)
            {
                case DecoyWindowTarget.Scene:
                    return "Window/General/Scene";
                case DecoyWindowTarget.Profiler:
                    return "Window/Analysis/Profiler";
                case DecoyWindowTarget.Animator:
                    return "Window/Animation/Animator";
                case DecoyWindowTarget.Project:
                    return "Window/General/Project";
                default:
                    return "Window/General/Console";
            }
        }

        private static void FocusDecoyWindow(DecoyWindowTarget target)
        {
            if (EditorApplication.ExecuteMenuItem(GetDecoyMenuPath(target)))
            {
                return;
            }

            SceneView sceneView = SceneView.lastActiveSceneView != null
                ? SceneView.lastActiveSceneView
                : GetWindow<SceneView>();
            sceneView.Show();
            sceneView.Focus();
        }

        private void OnEnable()
        {
            minSize = new Vector2(420f, 320f);
            stateStore = new ReaderStateStore();
            state = stateStore.Load();
            UpdateWindowTitle();
            TryRestoreLastBook();
        }

        private void OnDisable()
        {
            ReleaseMutedConsoleIcons();
            SaveState();
        }

        private void OnGUI()
        {
            EnsureStyles();
            HandleReaderKeys(Event.current);
            UpdateCurrentPage();
            DrawToolbar();

            if (mainPage == 1)
            {
                DrawSettingsPage();
                return;
            }

            if (document == null || activeBook == null)
            {
                if (UsesConsoleAppearance())
                {
                    DrawConsoleEmptyPane();
                }
                else
                {
                    DrawEmptyState();
                }

                return;
            }

            if (UsesConsoleAppearance())
            {
                DrawConsoleReader();
            }
            else
            {
                DrawReader();
            }
        }

        private void EnsureStyles()
        {
            int fontSize = state != null && state.preferences != null ? state.preferences.fontSize : 16;
            if (readerStyle == null)
            {
                readerStyle = new GUIStyle(EditorStyles.label)
                {
                    wordWrap = true,
                    richText = false,
                    stretchWidth = true,
                    padding = new RectOffset(14, 14, 10, 10)
                };
            }

            readerStyle.fontSize = fontSize;
            readerStyle.normal.textColor = state.preferences.useDarkPage
                ? new Color(0.84f, 0.84f, 0.84f)
                : EditorStyles.label.normal.textColor;

            if (consoleTextStyle == null)
            {
                consoleTextStyle = new GUIStyle(EditorStyles.label)
                {
                    wordWrap = true,
                    richText = false,
                    clipping = TextClipping.Clip,
                    alignment = TextAnchor.UpperLeft,
                    padding = new RectOffset(0, 0, 0, 0)
                };
            }

            consoleTextStyle.fontSize = state.preferences.consoleFontSize;

            if (consoleHeaderStyle == null)
            {
                consoleHeaderStyle = new GUIStyle(EditorStyles.label)
                {
                    wordWrap = false,
                    richText = false,
                    clipping = TextClipping.Clip,
                    alignment = TextAnchor.UpperLeft,
                    padding = new RectOffset(0, 0, 0, 0)
                };
            }

            consoleHeaderStyle.fontSize = state.preferences.consoleFontSize;

            if (consoleInfoIcon == null)
            {
                consoleInfoIcon = EditorGUIUtility.IconContent("console.infoicon.sml");
                consoleWarningIcon = EditorGUIUtility.IconContent("console.warnicon.sml");
                consoleErrorIcon = EditorGUIUtility.IconContent("console.erroricon.sml");
                consoleInfoRowIcon = EditorGUIUtility.IconContent("console.infoicon");
                consoleWarningRowIcon = EditorGUIUtility.IconContent("console.warnicon");
                consoleErrorRowIcon = EditorGUIUtility.IconContent("console.erroricon");
            }

            if (chapterRowStyle == null)
            {
                chapterRowStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Clip,
                    padding = new RectOffset(6, 4, 1, 1)
                };
            }

            if (selectedChapterRowStyle == null)
            {
                selectedChapterRowStyle = new GUIStyle(chapterRowStyle);
                selectedChapterRowStyle.normal.textColor = new Color(0.3f, 0.72f, 1f);
                selectedChapterRowStyle.fontStyle = FontStyle.Bold;
            }
        }

        private bool UsesConsoleAppearance()
        {
            return state == null
                || state.preferences == null
                || state.preferences.appearance == ReaderAppearance.Console;
        }

        private void UpdateWindowTitle()
        {
            if (UsesConsoleAppearance())
            {
                Texture2D consoleIcon = EditorGUIUtility.FindTexture("UnityEditor.ConsoleWindow");
                titleContent = new GUIContent(
                    "Console",
                    consoleIcon);
            }
            else
            {
                titleContent = new GUIContent("Novel Reader");
            }
        }

        private void UpdateCurrentPage()
        {
            currentPage = document != null && activeBook != null
                ? NovelPaginator.GetPage(document.Content, activeBook.charOffset, state.preferences.charactersPerPage)
                : null;
        }

        private void DrawToolbar()
        {
            if (UsesConsoleAppearance())
            {
                DrawConsoleToolbar();
            }
            else
            {
                DrawClassicToolbar();
            }
        }

        private void DrawClassicToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Open TXT", EditorStyles.toolbarButton, GUILayout.Width(72f)))
            {
                OpenNovelFile();
            }

            using (new EditorGUI.DisabledScope(state == null || state.books == null || state.books.Count == 0))
            {
                if (GUILayout.Button("Library ▼", EditorStyles.toolbarButton, GUILayout.Width(75f)))
                {
                    ShowLibraryMenu();
                }
            }

            int nextMainPage = GUILayout.Toolbar(
                mainPage,
                new[] { "Reader", "Settings" },
                EditorStyles.toolbarButton,
                GUILayout.Width(132f));
            if (nextMainPage != mainPage)
            {
                mainPage = nextMainPage;
                GUI.FocusControl(null);
                if (mainPage == 1)
                {
                    LoadShortcutDrafts();
                }
            }

            if (mainPage == 0)
            {
                string sidebarLabel = state.preferences.showSidebar ? "◀ Contents" : "▶ Contents";
                if (GUILayout.Button(new GUIContent(sidebarLabel, "Expand or collapse the chapter directory"), EditorStyles.toolbarButton, GUILayout.Width(88f)))
                {
                    SetSidebarVisible(!state.preferences.showSidebar);
                }
            }

            GUILayout.FlexibleSpace();
            if (document != null)
            {
                GUILayout.Label(document.EncodingName + " · " + document.Chapters.Count + " chapters", EditorStyles.miniLabel);
            }

            if (GUILayout.Button("Data", EditorStyles.toolbarButton, GUILayout.Width(44f)))
            {
                OpenDataFolderMenu();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawConsoleToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button(new GUIContent("Clear", "Toggle the visible reader buffer without changing progress"), EditorStyles.toolbarDropDown, GUILayout.Width(54f)))
            {
                ToggleConsoleBuffer();
            }

            if (GUILayout.Button("Open TXT", EditorStyles.toolbarButton, GUILayout.Width(68f)))
            {
                OpenNovelFile();
            }

            using (new EditorGUI.DisabledScope(document == null || activeBook == null || activeBook.charOffset <= 0))
            {
                if (GUILayout.Button(new GUIContent("Prev", "Previous page"), EditorStyles.toolbarButton, GUILayout.Width(40f)))
                {
                    PreviousPage();
                }
            }

            using (new EditorGUI.DisabledScope(document == null || currentPage == null || currentPage.EndOffset >= document.Content.Length))
            {
                if (GUILayout.Button(new GUIContent("Next", "Next page"), EditorStyles.toolbarButton, GUILayout.Width(40f)))
                {
                    NextPage();
                }
            }

            using (new EditorGUI.DisabledScope(document == null || activeBook == null))
            {
                if (GUILayout.Button(new GUIContent("Mark", "Bookmark the current reading position"), EditorStyles.toolbarButton, GUILayout.Width(42f)))
                {
                    AddBookmark();
                }
            }

            if (GUILayout.Button("Editor", EditorStyles.toolbarDropDown, GUILayout.Width(58f)))
            {
                ShowConsoleReaderMenu();
            }

            GUILayout.FlexibleSpace();
            if (position.width >= 560f)
            {
                float searchWidth = Mathf.Clamp(position.width * 0.20f, 72f, 240f);
                string nextFilter = EditorGUILayout.TextField(chapterFilter, EditorStyles.toolbarSearchField, GUILayout.Width(searchWidth));
                if (!string.Equals(nextFilter, chapterFilter, StringComparison.Ordinal))
                {
                    chapterFilter = nextFilter;
                    appliedChapterFilter = null;
                    if (!string.IsNullOrEmpty(chapterFilter))
                    {
                        sidebarTab = 0;
                        SetSidebarVisible(true);
                    }
                }
            }

            DrawConsoleColorToggle(
                ref state.preferences.colorInfoRows,
                BuildConsoleCounterContent(consoleInfoIcon, consoleInfoCount, "Toggle info-row coloring"));
            DrawConsoleColorToggle(
                ref state.preferences.colorWarningRows,
                BuildConsoleCounterContent(consoleWarningIcon, consoleWarningCount, "Toggle warning-row coloring"));
            DrawConsoleColorToggle(
                ref state.preferences.colorErrorRows,
                BuildConsoleCounterContent(consoleErrorIcon, consoleErrorCount, "Toggle error-row coloring"));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawConsoleColorToggle(ref bool value, GUIContent content)
        {
            bool nextValue = GUILayout.Toggle(value, content, EditorStyles.toolbarButton, GUILayout.Width(38f));
            if (nextValue != value)
            {
                value = nextValue;
                SaveState();
                Repaint();
            }
        }

        private static GUIContent BuildConsoleCounterContent(GUIContent icon, int count, string tooltip)
        {
            Texture image = icon != null ? icon.image : null;
            return new GUIContent(count.ToString(), image, tooltip);
        }

        private void ShowConsoleReaderMenu()
        {
            GenericMenu menu = new GenericMenu();
            if (document != null && activeBook != null)
            {
                if (activeBook.charOffset > 0)
                {
                    menu.AddItem(new GUIContent("Navigate/Previous Page"), false, PreviousPage);
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("Navigate/Previous Page"));
                }

                if (currentPage != null && currentPage.EndOffset < document.Content.Length)
                {
                    menu.AddItem(new GUIContent("Navigate/Next Page"), false, NextPage);
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("Navigate/Next Page"));
                }

                menu.AddItem(new GUIContent("Navigate/Add Bookmark"), false, AddBookmark);
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Navigate/Previous Page"));
                menu.AddDisabledItem(new GUIContent("Navigate/Next Page"));
                menu.AddDisabledItem(new GUIContent("Navigate/Add Bookmark"));
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Panels/Chapters"), state.preferences.showSidebar && sidebarTab == 0, delegate
            {
                bool wasVisible = state.preferences.showSidebar && sidebarTab == 0;
                sidebarTab = 0;
                SetSidebarVisible(!wasVisible);
            });
            menu.AddItem(new GUIContent("Panels/Bookmarks"), state.preferences.showSidebar && sidebarTab == 1, delegate
            {
                bool wasVisible = state.preferences.showSidebar && sidebarTab == 1;
                sidebarTab = 1;
                SetSidebarVisible(!wasVisible);
            });
            menu.AddItem(new GUIContent("Library"), false, ShowLibraryMenu);
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Settings"), mainPage == 1, delegate
            {
                mainPage = mainPage == 1 ? 0 : 1;
                GUI.FocusControl(null);
                if (mainPage == 1)
                {
                    LoadShortcutDrafts();
                }

                Repaint();
            });
            menu.AddItem(
                new GUIContent("Synthetic Headers"),
                state.preferences.simulateConsoleHeaders,
                delegate { SetSimulateConsoleHeaders(!state.preferences.simulateConsoleHeaders); });
            menu.AddItem(new GUIContent("Classic Reader"), false, delegate { SetReaderAppearance(ReaderAppearance.Classic); });
            menu.AddItem(new GUIContent("Open Data Folder"), false, OpenDataFolderMenu);
            menu.AddSeparator("Boss-key target/");
            AddDecoyMenuItem(menu, DecoyWindowTarget.Scene);
            AddDecoyMenuItem(menu, DecoyWindowTarget.Console);
            AddDecoyMenuItem(menu, DecoyWindowTarget.Profiler);
            AddDecoyMenuItem(menu, DecoyWindowTarget.Animator);
            AddDecoyMenuItem(menu, DecoyWindowTarget.Project);
            menu.ShowAsContext();
        }

        private void AddDecoyMenuItem(GenericMenu menu, DecoyWindowTarget target)
        {
            DecoyWindowTarget capturedTarget = target;
            menu.AddItem(
                new GUIContent("Boss-key target/" + target),
                state.preferences.decoyWindow == target,
                delegate { SetDecoyWindow(capturedTarget); });
        }

        private void ToggleConsoleBuffer()
        {
            consoleBufferCleared = !consoleBufferCleared;
            InvalidateConsoleLayout();
            Repaint();
        }

        private void DrawEmptyState()
        {
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginVertical(GUILayout.MaxWidth(440f));
            GUILayout.Label("Unity Novel Reader", EditorStyles.boldLabel);
            GUILayout.Space(6f);
            GUILayout.Label("Open a local TXT file. The file stays outside Assets, and reading data stays outside the Unity project.", EditorStyles.wordWrappedLabel);
            GUILayout.Space(10f);
            if (GUILayout.Button("Open TXT / TEXT / MD", GUILayout.Height(32f)))
            {
                OpenNovelFile();
            }

            GUILayout.Space(8f);
            GUILayout.Label("Configure shortcuts in Settings. Esc also hides to " + state.preferences.decoyWindow + ".", EditorStyles.miniLabel);
            if (!string.IsNullOrEmpty(statusMessage))
            {
                GUILayout.Space(8f);
                EditorGUILayout.HelpBox(statusMessage, MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
        }

        private void DrawConsoleEmptyPane()
        {
            Rect area = GUILayoutUtility.GetRect(
                40f,
                100000f,
                40f,
                100000f,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(area, GetConsoleBackgroundColor());
            if (string.IsNullOrEmpty(statusMessage))
            {
                return;
            }

            float textWidth = Math.Max(40f, area.width - ConsoleIconGutterWidth - 12f);
            bool simulateHeaders = state.preferences.simulateConsoleHeaders;
            float headerHeight = simulateHeaders ? GetConsoleOneLineHeight(textWidth) : 0f;
            float textHeight = Math.Max(20f, consoleTextStyle.CalcHeight(new GUIContent(statusMessage), textWidth));
            float rowHeight = Math.Max(
                ConsoleMinimumRowHeight,
                (ConsoleRowVerticalPadding * 2f) + headerHeight + textHeight);
            Rect row = new Rect(area.x, area.y, area.width, rowHeight);
            EditorGUI.DrawRect(row, GetConsoleRowColor(0));
            DrawConsoleIcon(
                new Rect(
                    row.x + 4f,
                    row.y + Math.Max(4f, (row.height - ConsoleIconSize) * 0.5f),
                    ConsoleIconSize,
                    ConsoleIconSize),
                ConsoleRowSeverity.Error);
            if (simulateHeaders)
            {
                DrawConsoleHeader(
                    new Rect(
                        row.x + ConsoleIconGutterWidth,
                        row.y + ConsoleRowVerticalPadding,
                        textWidth,
                        headerHeight),
                    BuildConsoleHeader(DateTime.Now, ConsoleRowSeverity.Error, 0));
            }

            DrawConsoleText(
                new Rect(
                    row.x + ConsoleIconGutterWidth,
                    row.y + ConsoleRowVerticalPadding + headerHeight,
                    textWidth,
                    textHeight),
                statusMessage,
                ConsoleRowSeverity.Error);
        }

        private void DrawConsoleReader()
        {
            EditorGUILayout.BeginHorizontal();
            if (state.preferences.showSidebar)
            {
                DrawConsoleSidebar();
            }

            DrawConsoleReadingPane();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawConsoleSidebar()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(SidebarWidth), GUILayout.ExpandHeight(true));
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            sidebarTab = GUILayout.Toolbar(
                sidebarTab,
                new[] { "Chapters", "Bookmarks" },
                EditorStyles.toolbarButton,
                GUILayout.ExpandWidth(true));
            if (GUILayout.Button(new GUIContent("×", "Close panel"), EditorStyles.toolbarButton, GUILayout.Width(22f)))
            {
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                SetSidebarVisible(false);
                return;
            }

            EditorGUILayout.EndHorizontal();
            if (sidebarTab == 0)
            {
                DrawChapterList(false);
            }
            else
            {
                DrawBookmarks();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawConsoleReadingPane()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            Rect viewport = GUILayoutUtility.GetRect(
                40f,
                100000f,
                40f,
                100000f,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(viewport, GetConsoleBackgroundColor());
            if (consoleBufferCleared || currentPage == null || string.IsNullOrEmpty(currentPage.Text))
            {
                EditorGUILayout.EndVertical();
                return;
            }

            float contentWidth = Math.Max(1f, viewport.width - 16f);
            float textWidth = Math.Max(40f, contentWidth - ConsoleIconGutterWidth - 8f);
            EnsureConsoleLayout(textWidth);
            Rect contentRect = new Rect(0f, 0f, contentWidth, Math.Max(viewport.height, consoleRowsHeight));
            readerScroll = GUI.BeginScrollView(viewport, readerScroll, contentRect);
            for (int i = 0; i < consoleRows.Count; i++)
            {
                ConsoleRenderRow row = consoleRows[i];
                if (row.Top + row.Height < readerScroll.y - ConsoleMinimumRowHeight
                    || row.Top > readerScroll.y + viewport.height + ConsoleMinimumRowHeight)
                {
                    continue;
                }

                Rect rowRect = new Rect(0f, row.Top, contentWidth, row.Height);
                EditorGUI.DrawRect(rowRect, GetConsoleRowColor(i));
                DrawConsoleIcon(
                    new Rect(
                        4f,
                        row.Top + Math.Max(4f, (row.Height - ConsoleIconSize) * 0.5f),
                        ConsoleIconSize,
                        ConsoleIconSize),
                    row.Severity);
                if (row.HeaderHeight > 0f && !string.IsNullOrEmpty(row.Header))
                {
                    DrawConsoleHeader(
                        new Rect(
                            ConsoleIconGutterWidth,
                            row.Top + ConsoleRowVerticalPadding,
                            textWidth,
                            row.HeaderHeight),
                        row.Header);
                }

                DrawConsoleText(
                    new Rect(
                        ConsoleIconGutterWidth,
                        row.Top + ConsoleRowVerticalPadding + row.HeaderHeight,
                        textWidth,
                        row.TextHeight),
                    row.Text,
                    row.Severity);
            }

            GUI.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void EnsureConsoleLayout(float textWidth)
        {
            if (currentPage == null || consoleBufferCleared)
            {
                return;
            }

            if (cachedConsolePageStart == currentPage.StartOffset
                && cachedConsolePageEnd == currentPage.EndOffset
                && cachedConsoleFontSize == state.preferences.consoleFontSize
                && Math.Abs(cachedConsoleWidth - textWidth) < 0.5f)
            {
                return;
            }

            consoleRows.Clear();
            consoleRowsHeight = 0f;
            consoleInfoCount = 0;
            consoleWarningCount = 0;
            consoleErrorCount = 0;

            float oneLineHeight = GetConsoleOneLineHeight(textWidth);
            bool simulateHeaders = state.preferences.simulateConsoleHeaders;
            float preferredTextHeight = simulateHeaders
                ? oneLineHeight + 0.5f
                : (oneLineHeight * 2f) + 1f;
            List<ConsoleTextSegment> segments = ConsoleTextLayout.BuildVisibleSegments(
                currentPage.Text,
                value => consoleTextStyle.CalcHeight(new GUIContent(value), textWidth),
                preferredTextHeight);
            DateTime latestLogTime = DateTime.Now;
            for (int i = 0; i < segments.Count; i++)
            {
                ConsoleTextSegment segment = segments[i];
                float measuredHeight = Math.Max(
                    oneLineHeight,
                    consoleTextStyle.CalcHeight(new GUIContent(segment.Text), textWidth));
                ConsoleRowSeverity severity = GetConsoleSeverity(segment);
                var row = new ConsoleRenderRow
                {
                    Header = simulateHeaders
                        ? BuildConsoleHeader(
                            latestLogTime.AddSeconds(i - segments.Count + 1),
                            severity,
                            currentPage.StartOffset + segment.Start)
                        : null,
                    Text = segment.Text,
                    SourceText = segment.SourceText,
                    Severity = severity,
                    Top = consoleRowsHeight,
                    Height = Math.Max(
                        ConsoleMinimumRowHeight,
                        (ConsoleRowVerticalPadding * 2f)
                            + (simulateHeaders ? oneLineHeight : 0f)
                            + measuredHeight),
                    HeaderHeight = simulateHeaders ? oneLineHeight : 0f,
                    TextHeight = measuredHeight
                };
                consoleRows.Add(row);
                consoleRowsHeight += row.Height;
                IncrementConsoleCount(severity);
            }

            cachedConsolePageStart = currentPage.StartOffset;
            cachedConsolePageEnd = currentPage.EndOffset;
            cachedConsoleFontSize = state.preferences.consoleFontSize;
            cachedConsoleWidth = textWidth;
            Repaint();
        }

        private float GetConsoleOneLineHeight(float textWidth)
        {
            return Math.Max(
                16f,
                consoleTextStyle.CalcHeight(new GUIContent("Ag"), textWidth));
        }

        private static string BuildConsoleHeader(
            DateTime timestamp,
            ConsoleRowSeverity severity,
            int absoluteOffset)
        {
            uint hash = unchecked(((uint)absoluteOffset + 1u) * 2654435761u);
            string message;
            switch (severity)
            {
                case ConsoleRowSeverity.Warning:
                    message = (hash & 1u) == 0u
                        ? "Asset import completed with warnings."
                        : "Editor state changed during update.";
                    break;
                case ConsoleRowSeverity.Error:
                    message = (hash & 1u) == 0u
                        ? "Editor task completed with errors."
                        : "Failed to process editor callback.";
                    break;
                default:
                    switch (hash % 3u)
                    {
                        case 0u:
                            message = "EditorApplication update completed.";
                            break;
                        case 1u:
                            message = "AssetDatabase refresh completed.";
                            break;
                        default:
                            message = "Scene view repaint completed.";
                            break;
                    }

                    break;
            }

            return "[" + timestamp.ToString("HH:mm:ss") + "] " + message;
        }

        private ConsoleRowSeverity GetConsoleSeverity(ConsoleTextSegment segment)
        {
            ConsoleTextRole role = ConsoleTextLayout.ClassifySegment(
                segment,
                document.Content,
                currentPage.StartOffset,
                document.Chapters);
            switch (role)
            {
                case ConsoleTextRole.ChapterTitle:
                    return ConsoleRowSeverity.Error;
                case ConsoleTextRole.ParagraphStart:
                    return ConsoleRowSeverity.Info;
                default:
                    return ConsoleRowSeverity.Warning;
            }
        }

        private void IncrementConsoleCount(ConsoleRowSeverity severity)
        {
            switch (severity)
            {
                case ConsoleRowSeverity.Warning:
                    consoleWarningCount++;
                    break;
                case ConsoleRowSeverity.Error:
                    consoleErrorCount++;
                    break;
                default:
                    consoleInfoCount++;
                    break;
            }
        }

        private void DrawConsoleIcon(Rect rect, ConsoleRowSeverity severity)
        {
            GUIContent icon;
            switch (severity)
            {
                case ConsoleRowSeverity.Warning:
                    icon = consoleWarningRowIcon;
                    break;
                case ConsoleRowSeverity.Error:
                    icon = consoleErrorRowIcon;
                    break;
                default:
                    icon = consoleInfoRowIcon;
                    break;
            }

            if (icon == null || icon.image == null)
            {
                return;
            }

            bool useSeverityColor = IsConsoleSeverityColorEnabled(severity);
            Texture image = icon.image;
            if (!useSeverityColor)
            {
                Texture2D mutedIcon = GetMutedConsoleIcon(severity, image);
                if (mutedIcon != null)
                {
                    image = mutedIcon;
                }
            }

            Color previousColor = GUI.color;
            if (!useSeverityColor && ReferenceEquals(image, icon.image))
            {
                GUI.color = new Color(
                    previousColor.r,
                    previousColor.g,
                    previousColor.b,
                    previousColor.a * 0.35f);
            }

            GUI.DrawTexture(rect, image, ScaleMode.ScaleToFit, true);
            GUI.color = previousColor;
        }

        private Texture2D GetMutedConsoleIcon(ConsoleRowSeverity severity, Texture source)
        {
            if (mutedConsoleIconCreationFailed)
            {
                return null;
            }

            Texture2D cached;
            switch (severity)
            {
                case ConsoleRowSeverity.Warning:
                    cached = mutedConsoleWarningRowIcon;
                    break;
                case ConsoleRowSeverity.Error:
                    cached = mutedConsoleErrorRowIcon;
                    break;
                default:
                    cached = mutedConsoleInfoRowIcon;
                    break;
            }

            if (cached != null)
            {
                return cached;
            }

            try
            {
                cached = CreateMutedConsoleIcon(source);
            }
            catch (Exception)
            {
                mutedConsoleIconCreationFailed = true;
                return null;
            }

            switch (severity)
            {
                case ConsoleRowSeverity.Warning:
                    mutedConsoleWarningRowIcon = cached;
                    break;
                case ConsoleRowSeverity.Error:
                    mutedConsoleErrorRowIcon = cached;
                    break;
                default:
                    mutedConsoleInfoRowIcon = cached;
                    break;
            }

            return cached;
        }

        internal static Texture2D CreateMutedConsoleIcon(Texture source)
        {
            RenderTexture temporary = null;
            RenderTexture previousActive = RenderTexture.active;
            Texture2D result = null;
            try
            {
                temporary = RenderTexture.GetTemporary(
                    source.width,
                    source.height,
                    0,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.Default);
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;

                result = new Texture2D(
                    source.width,
                    source.height,
                    TextureFormat.RGBA32,
                    false,
                    false)
                {
                    name = source.name + " (Muted)",
                    hideFlags = HideFlags.HideAndDontSave
                };
                result.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0, false);
                Color32[] pixels = result.GetPixels32();
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = MuteConsoleIconPixel(pixels[i]);
                }

                result.SetPixels32(pixels);
                result.Apply(false, false);
                return result;
            }
            catch (Exception)
            {
                if (result != null)
                {
                    DestroyImmediate(result);
                }

                throw;
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (temporary != null)
                {
                    RenderTexture.ReleaseTemporary(temporary);
                }
            }
        }

        internal static Color32 MuteConsoleIconPixel(Color32 pixel)
        {
            byte maximum = Math.Max(pixel.r, Math.Max(pixel.g, pixel.b));
            byte neutral = (byte)Mathf.RoundToInt(maximum * 0.62f);
            return new Color32(neutral, neutral, neutral, pixel.a);
        }

        private void ReleaseMutedConsoleIcons()
        {
            DestroyMutedConsoleIcon(ref mutedConsoleInfoRowIcon);
            DestroyMutedConsoleIcon(ref mutedConsoleWarningRowIcon);
            DestroyMutedConsoleIcon(ref mutedConsoleErrorRowIcon);
            mutedConsoleIconCreationFailed = false;
        }

        private static void DestroyMutedConsoleIcon(ref Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            DestroyImmediate(texture);
            texture = null;
        }

        private void DrawConsoleHeader(Rect rect, string text)
        {
            Color previousColor = consoleHeaderStyle.normal.textColor;
            consoleHeaderStyle.normal.textColor = EditorGUIUtility.isProSkin
                ? new Color(0.76f, 0.76f, 0.76f)
                : new Color(0.20f, 0.20f, 0.20f);
            GUI.Label(rect, text, consoleHeaderStyle);
            consoleHeaderStyle.normal.textColor = previousColor;
        }

        private void DrawConsoleText(Rect rect, string text, ConsoleRowSeverity severity)
        {
            Color previousColor = consoleTextStyle.normal.textColor;
            consoleTextStyle.normal.textColor = GetConsoleTextColor(severity);
            GUI.Label(rect, text, consoleTextStyle);
            consoleTextStyle.normal.textColor = previousColor;
        }

        private Color GetConsoleTextColor(ConsoleRowSeverity severity)
        {
            Color neutral = EditorGUIUtility.isProSkin
                ? new Color(0.72f, 0.72f, 0.72f)
                : new Color(0.22f, 0.22f, 0.22f);
            bool useSeverityColor = IsConsoleSeverityColorEnabled(severity);
            switch (severity)
            {
                case ConsoleRowSeverity.Warning:
                    return useSeverityColor
                        ? new Color(0.88f, 0.72f, 0.30f)
                        : neutral;
                case ConsoleRowSeverity.Error:
                    return useSeverityColor
                        ? new Color(0.92f, 0.42f, 0.38f)
                        : neutral;
                default:
                    return useSeverityColor
                        ? (EditorGUIUtility.isProSkin ? new Color(0.78f, 0.78f, 0.78f) : neutral)
                        : neutral;
            }
        }

        private bool IsConsoleSeverityColorEnabled(ConsoleRowSeverity severity)
        {
            switch (severity)
            {
                case ConsoleRowSeverity.Warning:
                    return state.preferences.colorWarningRows;
                case ConsoleRowSeverity.Error:
                    return state.preferences.colorErrorRows;
                default:
                    return state.preferences.colorInfoRows;
            }
        }

        private static Color GetConsoleBackgroundColor()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(0.145f, 0.145f, 0.145f)
                : new Color(0.84f, 0.84f, 0.84f);
        }

        private static Color GetConsoleRowColor(int rowIndex)
        {
            if (EditorGUIUtility.isProSkin)
            {
                return rowIndex % 2 == 0
                    ? new Color(0.185f, 0.185f, 0.185f)
                    : new Color(0.165f, 0.165f, 0.165f);
            }

            return rowIndex % 2 == 0
                ? new Color(0.91f, 0.91f, 0.91f)
                : new Color(0.86f, 0.86f, 0.86f);
        }

        private void InvalidateConsoleLayout()
        {
            consoleRows.Clear();
            consoleRowsHeight = 0f;
            consoleInfoCount = 0;
            consoleWarningCount = 0;
            consoleErrorCount = 0;
            cachedConsolePageStart = -1;
            cachedConsolePageEnd = -1;
            cachedConsoleFontSize = -1;
            cachedConsoleWidth = -1f;
        }

        private void DrawReader()
        {
            EditorGUILayout.BeginHorizontal();
            if (state.preferences.showSidebar)
            {
                DrawSidebar();
            }
            else
            {
                DrawCollapsedSidebarHandle();
            }

            DrawReadingPane();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSidebar()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(SidebarWidth), GUILayout.ExpandHeight(true));
            EditorGUILayout.BeginHorizontal();
            sidebarTab = GUILayout.Toolbar(sidebarTab, new[] { "Chapters", "Bookmarks" }, GUILayout.ExpandWidth(true));
            bool collapseRequested = GUILayout.Button(
                new GUIContent("◀", "Collapse chapter directory"),
                EditorStyles.miniButton,
                GUILayout.Width(24f),
                GUILayout.Height(20f));
            EditorGUILayout.EndHorizontal();

            if (collapseRequested)
            {
                EditorGUILayout.EndVertical();
                SetSidebarVisible(false);
                return;
            }

            GUILayout.Space(3f);
            if (sidebarTab == 0)
            {
                DrawChapterList(true);
            }
            else
            {
                DrawBookmarks();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawCollapsedSidebarHandle()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(CollapsedSidebarWidth), GUILayout.ExpandHeight(true));
            if (GUILayout.Button(
                    new GUIContent("▶", "Expand chapter directory"),
                    EditorStyles.miniButton,
                    GUILayout.Width(20f),
                    GUILayout.Height(28f)))
            {
                SetSidebarVisible(true);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
        }

        private void SetSidebarVisible(bool visible)
        {
            if (state == null || state.preferences == null || state.preferences.showSidebar == visible)
            {
                return;
            }

            state.preferences.showSidebar = visible;
            SaveState();
            Repaint();
        }

        private void DrawSettingsPage()
        {
            if (!shortcutDraftLoaded)
            {
                LoadShortcutDrafts();
            }

            settingsScroll = EditorGUILayout.BeginScrollView(settingsScroll);
            GUILayout.Space(12f);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginVertical(GUILayout.MaxWidth(640f));

            GUILayout.Label("Reader Settings", EditorStyles.boldLabel);
            GUILayout.Space(6f);

            GUILayout.Label("Appearance", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            ReaderAppearance nextAppearance = (ReaderAppearance)EditorGUILayout.EnumPopup(
                new GUIContent("Reader skin", "Console keeps the main reader visually close to Unity's Console"),
                state.preferences.appearance);
            if (nextAppearance != state.preferences.appearance)
            {
                SetReaderAppearance(nextAppearance);
            }

            int nextConsoleFontSize = EditorGUILayout.IntSlider(
                new GUIContent("Console text size", "Rows grow when necessary; text is never clipped"),
                state.preferences.consoleFontSize,
                11,
                16);
            if (nextConsoleFontSize != state.preferences.consoleFontSize)
            {
                state.preferences.consoleFontSize = nextConsoleFontSize;
                InvalidateConsoleLayout();
                SaveState();
            }

            bool nextSimulateHeaders = EditorGUILayout.Toggle(
                new GUIContent(
                    "Synthetic headers",
                    "Adds generated timestamped Console messages above the novel text"),
                state.preferences.simulateConsoleHeaders);
            if (nextSimulateHeaders != state.preferences.simulateConsoleHeaders)
            {
                SetSimulateConsoleHeaders(nextSimulateHeaders);
            }

            EditorGUILayout.HelpBox(
                state.preferences.simulateConsoleHeaders
                    ? "Each row uses a generated Console header plus the original text. Disable Synthetic headers to let the novel text start on the first line."
                    : "Novel text starts on the first line and targets roughly two visual lines per row. Enable Synthetic headers only when a more literal Console disguise is useful.",
                MessageType.None);
            EditorGUILayout.EndVertical();
            GUILayout.Space(12f);

            GUILayout.Label("Shortcuts", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Bindings are stored by Unity's user-level Shortcut Manager and are not written into the project.",
                MessageType.Info);
            DrawShortcutBindingEditor("Toggle reader", ref toggleShortcutKey, ref toggleShortcutModifiers);
            DrawShortcutBindingEditor("Boss key / quick hide", ref quickHideShortcutKey, ref quickHideShortcutModifiers);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply shortcuts", GUILayout.Height(26f)))
            {
                ApplyShortcutDrafts();
            }

            if (GUILayout.Button("Reset defaults", GUILayout.Height(26f)))
            {
                ResetShortcutDrafts();
            }

            if (GUILayout.Button("Unity Shortcut Manager", GUILayout.Height(26f)))
            {
                OpenUnityShortcutManager();
            }

            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(settingsStatusMessage))
            {
                EditorGUILayout.HelpBox(
                    settingsStatusMessage,
                    settingsStatusIsError ? MessageType.Error : MessageType.Info);
            }

            GUILayout.Space(16f);
            GUILayout.Label("Disguise strategy", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DecoyWindowTarget nextTarget = (DecoyWindowTarget)EditorGUILayout.EnumPopup(
                new GUIContent("Boss-key target", "Window focused or opened after the reader hides"),
                state.preferences.decoyWindow);
            if (nextTarget != state.preferences.decoyWindow)
            {
                SetDecoyWindow(nextTarget);
            }

            EditorGUILayout.HelpBox(
                "The configured boss key and Esc close the reader and focus the selected Unity window. Press the boss key again to restore the reader. If the target is unavailable, Scene is used as the fallback.",
                MessageType.None);
            if (GUILayout.Button("Test disguise now", GUILayout.Height(26f)))
            {
                ToggleWindow(true);
                GUIUtility.ExitGUI();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        private static void DrawShortcutBindingEditor(
            string label,
            ref KeyCode keyCode,
            ref ShortcutModifiers modifiers)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label(label, EditorStyles.miniBoldLabel);
            keyCode = (KeyCode)EditorGUILayout.EnumPopup("Key", keyCode);
            modifiers = (ShortcutModifiers)EditorGUILayout.EnumFlagsField("Modifiers", modifiers);
            EditorGUILayout.EndVertical();
        }

        private void LoadShortcutDrafts()
        {
            toggleShortcutKey = KeyCode.R;
            toggleShortcutModifiers = ShortcutModifiers.Control | ShortcutModifiers.Alt;
            quickHideShortcutKey = KeyCode.H;
            quickHideShortcutModifiers = ShortcutModifiers.Control | ShortcutModifiers.Alt;

            TryReadShortcutBinding(
                NovelReaderShortcuts.ToggleShortcutId,
                ref toggleShortcutKey,
                ref toggleShortcutModifiers);
            TryReadShortcutBinding(
                NovelReaderShortcuts.QuickHideShortcutId,
                ref quickHideShortcutKey,
                ref quickHideShortcutModifiers);
            shortcutDraftLoaded = true;
        }

        private static void TryReadShortcutBinding(
            string shortcutId,
            ref KeyCode keyCode,
            ref ShortcutModifiers modifiers)
        {
            try
            {
                KeyCombination[] combinations = ShortcutManager.instance
                    .GetShortcutBinding(shortcutId)
                    .keyCombinationSequence
                    .ToArray();
                if (combinations.Length > 0)
                {
                    keyCode = combinations[0].keyCode;
                    modifiers = combinations[0].modifiers;
                }
            }
            catch (Exception)
            {
                // Keep declared defaults until Unity finishes registering shortcuts.
            }
        }

        private void ApplyShortcutDrafts()
        {
            if (toggleShortcutKey == KeyCode.None || quickHideShortcutKey == KeyCode.None)
            {
                SetSettingsStatus("Choose a key for both shortcuts.", true);
                return;
            }

            if (toggleShortcutKey == quickHideShortcutKey && toggleShortcutModifiers == quickHideShortcutModifiers)
            {
                SetSettingsStatus("The two reader shortcuts must be different.", true);
                return;
            }

            IShortcutManager manager = ShortcutManager.instance;
            ShortcutBinding previousToggle = manager.GetShortcutBinding(NovelReaderShortcuts.ToggleShortcutId);
            ShortcutBinding previousQuickHide = manager.GetShortcutBinding(NovelReaderShortcuts.QuickHideShortcutId);
            try
            {
                manager.RebindShortcut(
                    NovelReaderShortcuts.ToggleShortcutId,
                    new ShortcutBinding(new KeyCombination(toggleShortcutKey, toggleShortcutModifiers)));
                manager.RebindShortcut(
                    NovelReaderShortcuts.QuickHideShortcutId,
                    new ShortcutBinding(new KeyCombination(quickHideShortcutKey, quickHideShortcutModifiers)));
                shortcutDraftLoaded = false;
                LoadShortcutDrafts();
                SetSettingsStatus("Shortcut bindings saved.", false);
            }
            catch (Exception exception)
            {
                try
                {
                    manager.RebindShortcut(NovelReaderShortcuts.ToggleShortcutId, previousToggle);
                    manager.RebindShortcut(NovelReaderShortcuts.QuickHideShortcutId, previousQuickHide);
                }
                catch (Exception)
                {
                    // Unity's Shortcut Manager remains the source of truth if rollback fails.
                }

                SetSettingsStatus("Could not save shortcuts: " + exception.Message, true);
            }
        }

        private void ResetShortcutDrafts()
        {
            toggleShortcutKey = KeyCode.R;
            toggleShortcutModifiers = ShortcutModifiers.Control | ShortcutModifiers.Alt;
            quickHideShortcutKey = KeyCode.H;
            quickHideShortcutModifiers = ShortcutModifiers.Control | ShortcutModifiers.Alt;
            ApplyShortcutDrafts();
        }

        private static void OpenUnityShortcutManager()
        {
            if (!EditorApplication.ExecuteMenuItem("Edit/Shortcuts..."))
            {
                EditorApplication.ExecuteMenuItem("Edit/Shortcuts");
            }
        }

        private void SetSettingsStatus(string message, bool isError)
        {
            settingsStatusMessage = message;
            settingsStatusIsError = isError;
            Repaint();
        }

        private void SetDecoyWindow(DecoyWindowTarget target)
        {
            if (state == null || state.preferences == null || state.preferences.decoyWindow == target)
            {
                return;
            }

            state.preferences.decoyWindow = target;
            SaveState();
            Repaint();
        }

        private void SetReaderAppearance(ReaderAppearance appearance)
        {
            if (state == null || state.preferences == null || state.preferences.appearance == appearance)
            {
                return;
            }

            state.preferences.appearance = appearance;
            mainPage = 0;
            UpdateWindowTitle();
            InvalidateConsoleLayout();
            SaveState();
            Repaint();
        }

        private void SetSimulateConsoleHeaders(bool enabled)
        {
            if (state == null
                || state.preferences == null
                || state.preferences.simulateConsoleHeaders == enabled)
            {
                return;
            }

            state.preferences.simulateConsoleHeaders = enabled;
            InvalidateConsoleLayout();
            SaveState();
            Repaint();
        }

        private void DrawChapterList(bool showSearchField)
        {
            if (showSearchField)
            {
                string nextFilter = EditorGUILayout.TextField(chapterFilter, EditorStyles.toolbarSearchField);
                if (nextFilter != chapterFilter)
                {
                    chapterFilter = nextFilter;
                }
            }

            RebuildChapterFilterIfNeeded();
            Rect viewport = GUILayoutUtility.GetRect(10f, 100000f, 10f, 100000f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            float contentHeight = Math.Max(viewport.height, filteredChapterIndices.Count * SidebarRowHeight);
            Rect contentRect = new Rect(0f, 0f, Math.Max(1f, viewport.width - 16f), contentHeight);
            chapterScroll = GUI.BeginScrollView(viewport, chapterScroll, contentRect);

            int first = Math.Max(0, (int)(chapterScroll.y / SidebarRowHeight) - 1);
            int visibleCount = Math.Max(1, (int)(viewport.height / SidebarRowHeight) + 3);
            int last = Math.Min(filteredChapterIndices.Count, first + visibleCount);
            int currentChapter = NovelPaginator.FindChapterIndex(document.Chapters, activeBook.charOffset);
            for (int visibleIndex = first; visibleIndex < last; visibleIndex++)
            {
                int chapterIndex = filteredChapterIndices[visibleIndex];
                ChapterInfo chapter = document.Chapters[chapterIndex];
                Rect row = new Rect(0f, visibleIndex * SidebarRowHeight, contentRect.width, SidebarRowHeight);
                GUIStyle style = chapterIndex == currentChapter ? selectedChapterRowStyle : chapterRowStyle;
                if (GUI.Button(row, new GUIContent(chapter.Title, chapter.Title), style))
                {
                    JumpTo(chapter.Offset);
                }
            }

            GUI.EndScrollView();
        }

        private void DrawBookmarks()
        {
            if (activeBook.bookmarks == null || activeBook.bookmarks.Count == 0)
            {
                GUILayout.Label("No bookmarks yet.", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                return;
            }

            bookmarkScroll = EditorGUILayout.BeginScrollView(bookmarkScroll);
            for (int i = activeBook.bookmarks.Count - 1; i >= 0; i--)
            {
                BookmarkState bookmark = activeBook.bookmarks[i];
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(bookmark.title, EditorStyles.miniButtonLeft, GUILayout.ExpandWidth(true)))
                {
                    JumpTo(bookmark.charOffset);
                }

                if (GUILayout.Button("×", EditorStyles.miniButtonRight, GUILayout.Width(24f)))
                {
                    activeBook.bookmarks.RemoveAt(i);
                    SaveState();
                    GUIUtility.ExitGUI();
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawReadingPane()
        {
            currentPage = NovelPaginator.GetPage(document.Content, activeBook.charOffset, state.preferences.charactersPerPage);
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label(document.Title, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            string chapterTitle = GetCurrentChapterTitle();
            if (!string.IsNullOrEmpty(chapterTitle))
            {
                GUILayout.Label(chapterTitle, EditorStyles.miniLabel);
            }

            EditorGUILayout.EndHorizontal();

            Rect readingArea = GUILayoutUtility.GetRect(40f, 100000f, 40f, 100000f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            Color pageColor = state.preferences.useDarkPage
                ? new Color(0.105f, 0.11f, 0.12f)
                : new Color(0.92f, 0.9f, 0.82f);
            EditorGUI.DrawRect(readingArea, pageColor);
            Rect contentRect = new Rect(0f, 0f, Math.Max(1f, readingArea.width - 16f), Math.Max(readingArea.height, readerStyle.CalcHeight(new GUIContent(currentPage.Text), Math.Max(1f, readingArea.width - 44f)) + 20f));
            readerScroll = GUI.BeginScrollView(readingArea, readerScroll, contentRect);
            GUI.Label(new Rect(8f, 4f, Math.Max(1f, contentRect.width - 16f), contentRect.height - 8f), currentPage.Text, readerStyle);
            GUI.EndScrollView();

            DrawBottomControls();
            EditorGUILayout.EndVertical();
        }

        private void DrawBottomControls()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            using (new EditorGUI.DisabledScope(activeBook.charOffset <= 0))
            {
                if (GUILayout.Button("◀ Prev", EditorStyles.toolbarButton, GUILayout.Width(62f)))
                {
                    PreviousPage();
                }
            }

            using (new EditorGUI.DisabledScope(currentPage.EndOffset >= document.Content.Length))
            {
                if (GUILayout.Button("Next ▶", EditorStyles.toolbarButton, GUILayout.Width(62f)))
                {
                    NextPage();
                }
            }

            if (GUILayout.Button("★ Bookmark", EditorStyles.toolbarButton, GUILayout.Width(82f)))
            {
                AddBookmark();
            }

            GUILayout.Space(6f);
            float currentProgress = NovelPaginator.GetProgress(activeBook.charOffset, document.Content.Length);
            float nextProgress = GUILayout.HorizontalSlider(currentProgress, 0f, 1f, GUILayout.MinWidth(80f), GUILayout.ExpandWidth(true));
            if (Math.Abs(nextProgress - currentProgress) > 0.0001f)
            {
                JumpTo((int)(nextProgress * document.Content.Length));
            }

            GUILayout.Label((currentProgress * 100f).ToString("F1") + "%", EditorStyles.miniLabel, GUILayout.Width(48f));

            int nextFontSize = EditorGUILayout.IntSlider(state.preferences.fontSize, 11, 32, GUILayout.Width(120f));
            if (nextFontSize != state.preferences.fontSize)
            {
                state.preferences.fontSize = nextFontSize;
                SaveState();
            }

            int[] pageSizes = { 400, 600, 900, 1200, 1800, 2500 };
            string[] pageSizeNames = { "400", "600", "900", "1200", "1800", "2500" };
            int nextPageSize = EditorGUILayout.IntPopup(state.preferences.charactersPerPage, pageSizeNames, pageSizes, EditorStyles.toolbarPopup, GUILayout.Width(60f));
            if (nextPageSize != state.preferences.charactersPerPage)
            {
                state.preferences.charactersPerPage = nextPageSize;
                readerScroll = Vector2.zero;
                SaveState();
            }

            bool nextDarkPage = GUILayout.Toggle(state.preferences.useDarkPage, "Dark", EditorStyles.toolbarButton, GUILayout.Width(42f));
            if (nextDarkPage != state.preferences.useDarkPage)
            {
                state.preferences.useDarkPage = nextDarkPage;
                SaveState();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void HandleReaderKeys(Event currentEvent)
        {
            if (currentEvent == null || currentEvent.type != EventType.KeyDown)
            {
                return;
            }

            if (currentEvent.keyCode == KeyCode.Escape)
            {
                currentEvent.Use();
                ToggleWindow(true);
                GUIUtility.ExitGUI();
                return;
            }

            if (mainPage != 0 || document == null)
            {
                return;
            }

            if (EditorGUIUtility.editingTextField)
            {
                return;
            }

            if (currentEvent.keyCode == KeyCode.Space || currentEvent.keyCode == KeyCode.PageDown || currentEvent.keyCode == KeyCode.RightArrow)
            {
                currentEvent.Use();
                NextPage();
            }
            else if (currentEvent.keyCode == KeyCode.PageUp || currentEvent.keyCode == KeyCode.LeftArrow)
            {
                currentEvent.Use();
                PreviousPage();
            }
        }

        private void OpenNovelFile()
        {
            string initialDirectory = document != null
                ? Path.GetDirectoryName(document.FilePath)
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string path = EditorUtility.OpenFilePanelWithFilters(
                "Open local novel",
                initialDirectory,
                new[] { "Text files", "txt,text,md", "All files", "*" });
            if (!string.IsNullOrEmpty(path))
            {
                TryLoadBook(path, true);
            }
        }

        private void TryRestoreLastBook()
        {
            if (state == null || string.IsNullOrEmpty(state.lastBookId))
            {
                return;
            }

            BookState book = FindBook(state.lastBookId);
            if (book == null || string.IsNullOrEmpty(book.filePath) || !File.Exists(book.filePath))
            {
                return;
            }

            TryLoadBook(book.filePath, false);
        }

        private void TryLoadBook(string path, bool showDialogOnError)
        {
            try
            {
                NovelDocument loaded = NovelTextLoader.Load(path);
                string id = BookIdentity.FromPath(loaded.FilePath);
                BookState book = FindBook(id);
                if (book == null)
                {
                    book = new BookState { id = id, filePath = loaded.FilePath };
                    state.books.Add(book);
                }

                book.EnsureDefaults();
                book.title = loaded.Title;
                book.filePath = loaded.FilePath;
                book.encodingName = loaded.EncodingName;
                book.fileLength = loaded.FileLength;
                book.lastWriteUtcTicks = loaded.LastWriteUtcTicks;
                book.lastOpenedUtcTicks = DateTime.UtcNow.Ticks;
                book.charOffset = Math.Max(0, Math.Min(loaded.Content.Length, book.charOffset));

                document = loaded;
                activeBook = book;
                state.lastBookId = id;
                statusMessage = string.Empty;
                appliedChapterFilter = null;
                consoleBufferCleared = false;
                readerScroll = Vector2.zero;
                InvalidateConsoleLayout();
                SaveState();
                Repaint();
            }
            catch (Exception exception)
            {
                statusMessage = exception.Message;
                if (showDialogOnError)
                {
                    EditorUtility.DisplayDialog("Unity Novel Reader", exception.Message, "OK");
                }
            }
        }

        private void ShowLibraryMenu()
        {
            GenericMenu menu = new GenericMenu();
            for (int i = 0; i < state.books.Count; i++)
            {
                BookState capturedBook = state.books[i];
                if (capturedBook == null || string.IsNullOrEmpty(capturedBook.filePath))
                {
                    continue;
                }

                bool selected = activeBook != null && activeBook.id == capturedBook.id;
                string label = string.IsNullOrEmpty(capturedBook.title) ? capturedBook.filePath : capturedBook.title;
                if (File.Exists(capturedBook.filePath))
                {
                    menu.AddItem(new GUIContent(label), selected, delegate { TryLoadBook(capturedBook.filePath, true); });
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent(label + " (missing)"), selected);
                }
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Open another file..."), false, OpenNovelFile);
            menu.ShowAsContext();
        }

        private BookState FindBook(string id)
        {
            if (state == null || state.books == null)
            {
                return null;
            }

            for (int i = 0; i < state.books.Count; i++)
            {
                BookState book = state.books[i];
                if (book != null && string.Equals(book.id, id, StringComparison.Ordinal))
                {
                    return book;
                }
            }

            return null;
        }

        private void RebuildChapterFilterIfNeeded()
        {
            if (document == null)
            {
                filteredChapterIndices.Clear();
                return;
            }

            if (string.Equals(appliedChapterFilter, chapterFilter, StringComparison.Ordinal))
            {
                return;
            }

            appliedChapterFilter = chapterFilter;
            filteredChapterIndices.Clear();
            for (int i = 0; i < document.Chapters.Count; i++)
            {
                if (string.IsNullOrEmpty(chapterFilter) || document.Chapters[i].Title.IndexOf(chapterFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    filteredChapterIndices.Add(i);
                }
            }

            chapterScroll = Vector2.zero;
        }

        private string GetCurrentChapterTitle()
        {
            int chapterIndex = NovelPaginator.FindChapterIndex(document.Chapters, activeBook.charOffset);
            return chapterIndex >= 0 && chapterIndex < document.Chapters.Count
                ? document.Chapters[chapterIndex].Title
                : string.Empty;
        }

        private void NextPage()
        {
            if (document == null || currentPage == null)
            {
                return;
            }

            JumpTo(currentPage.EndOffset);
        }

        private void PreviousPage()
        {
            if (document == null || activeBook == null)
            {
                return;
            }

            JumpTo(NovelPaginator.GetPreviousOffset(activeBook.charOffset, state.preferences.charactersPerPage));
        }

        private void JumpTo(int offset)
        {
            if (document == null || activeBook == null)
            {
                return;
            }

            activeBook.charOffset = Math.Max(0, Math.Min(document.Content.Length, offset));
            activeBook.lastOpenedUtcTicks = DateTime.UtcNow.Ticks;
            consoleBufferCleared = false;
            readerScroll = Vector2.zero;
            InvalidateConsoleLayout();
            SaveState();
            Repaint();
        }

        private void AddBookmark()
        {
            if (document == null || activeBook == null)
            {
                return;
            }

            string chapterTitle = GetCurrentChapterTitle();
            float progress = NovelPaginator.GetProgress(activeBook.charOffset, document.Content.Length) * 100f;
            activeBook.bookmarks.Add(new BookmarkState
            {
                id = Guid.NewGuid().ToString("N"),
                charOffset = activeBook.charOffset,
                title = string.IsNullOrEmpty(chapterTitle) ? progress.ToString("F1") + "%" : chapterTitle + " · " + progress.ToString("F1") + "%",
                createdUtcTicks = DateTime.UtcNow.Ticks
            });
            SaveState();
        }

        private void SaveState()
        {
            if (stateStore == null || state == null)
            {
                return;
            }

            try
            {
                stateStore.Save(state);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Unity Novel Reader could not save its state: " + exception.Message);
            }
        }
    }
}
