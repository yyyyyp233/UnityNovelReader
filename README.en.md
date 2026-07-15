# Unity Novel Reader

[简体中文](README.md)

Unity Novel Reader is a local text reader that runs inside the Unity Editor. Its default reading mode uses a Console-style layout, including a Console tab title, toolbar, log rows, severity icons, search field, and counters.

The reader supports paging, chapters, bookmarks, progress persistence, and configurable shortcuts. A boss key can hide or restore the reader and focus a Scene, Console, Profiler, Animator, or Project window.

Books, reading progress, and bookmarks stay outside the Unity project. The tool can be removed without losing reading data or leaving source files in the project.

## Console-style reading mode

The default Console-style mode provides the following behavior:

- The tab title/icon, toolbar, alternating rows, severity icons, search field, and right-side counters use a layout similar to Unity's Console.
- `Open TXT`, `Prev`, `Next`, `Mark`, and `Editor ▼` provide file, navigation, bookmark, panel, and settings actions.
- `Clear` hides or restores the current text without changing the page, progress, bookmarks, or scroll position.
- Right-click any Log row to add a temporary blue position marker. Right-clicking another row moves it, while paging, chapter jumps, or switching books clears it without saving anything.
- Chapter titles use Error, paragraph starts use Log/Info, and continuation rows use Warning. The severity switches mute or restore both text and matching icons; they do not filter text.
- Rows split or grow with the window width so the complete text remains visible.
- The chapter/bookmark sidebar starts collapsed and can be expanded when needed.
- Timestamped synthetic log headers are optional and disabled by default.

This mode changes presentation only. Original text, whitespace, offsets, reading progress, and bookmarks remain intact.

## Features

- Provides a Console-style default mode and a Classic Reader fallback.
- Opens `.txt`, `.text`, and `.md` files without importing them into `Assets`.
- Detects UTF-8, UTF-16, GB18030, and GBK text.
- Finds common Chinese and English chapter headings.
- Remembers books, progress, preferences, and bookmarks across Unity projects.
- Keeps all state in the user data directory, never in the Unity project.
- Provides chapter search, page navigation, progress seeking, and bookmarks.
- Keeps the chapter/bookmark sidebar collapsed by default in Console mode, with explicit expand and collapse controls.
- Offers rebindable shortcuts, a boss key, and selectable Unity decoy windows.
- Includes an in-window Settings page for shortcut bindings, reader appearance, synthetic headers, and disguise strategy.
- Keeps visible `Prev` and `Next` controls, a reversible `Clear`, and color toggles that never filter out text.
- Contains Editor-only code and is excluded from player builds.

## Install

### Drag-and-drop release (recommended)

1. Download `UnityNovelReader-0.2.4.unitypackage` from the [latest release](https://github.com/yyyyyp233/UnityNovelReader/releases/latest).
2. Drag the file into Unity or use **Assets > Import Package > Custom Package...**.
3. Keep both listed files selected and import them.

This installs only `Assets/UnityNovelReader.Editor.dll` and its `.meta`. If upgrading from an older source-based `.unitypackage`, first delete `Assets/UnityNovelReader`, then import the current release.

### From a Git URL

In **Window > Package Manager**, choose **+ > Add package from git URL...** and enter:

```text
https://github.com/yyyyyp233/UnityNovelReader.git#v0.2.4
```

### From a local clone

1. Clone or copy this repository somewhere outside the Unity project.
2. In Unity, open **Window > Package Manager**.
3. Select **+ > Add package from disk...**.
4. Choose this repository's `package.json`.

You can also use the local toggle script:

```powershell
pwsh -File .\Tools~\Toggle-LocalPackage.ps1 -Action Install -ProjectPath C:\Path\To\UnityProject
```

## Build the `.unitypackage`

Build a traditional Unity asset package that can be dragged directly into the editor:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools~\Build-UnityPackage.ps1
```

The artifact is written to `Releases~/UnityNovelReader-<version>.unitypackage`. The trailing `~` keeps Unity from scanning release artifacts as package assets. Import adds only `Assets/UnityNovelReader.Editor.dll` and its `.meta`: one asset path and two filesystem files with no extra directory. Plugin settings force the DLL to Editor-only so it cannot enter a player build. The complete MIT notice is embedded in assembly metadata; readable source and license files remain in the GitHub/UPM repository. Delete the two imported files to uninstall; external reading data is unaffected.

See [Release process](Documentation~/RELEASING.md) for reproducible build and verification details.

## Remove

Remove `Unity Novel Reader` in Package Manager, run the toggle script with `-Action Uninstall`, or revert the local changes to `Packages/manifest.json` and `Packages/packages-lock.json`.

Removing the package does not remove reading data. By default it remains at:

```text
Windows: %LOCALAPPDATA%\UnityNovelReader\state.json
```

Use **Tools > Unity Novel Reader > Open Data Folder** to inspect or back it up. Set `UNITY_NOVEL_READER_HOME` before starting Unity to choose another data directory.

## Use

Open **Tools > Unity Novel Reader > Open Reader**, then choose a local text file. The default window title and visual language follow Unity's Console: alternating log-style rows, built-in severity icons, a toolbar search field, and Console-like counters.

The Console skin changes presentation only. By default, the original novel text starts directly on the first line and targets roughly two visual lines per entry, using Unity's full-size severity icon. Consecutive source line breaks are folded into adjacent rows, and display-only paragraph indentation is trimmed so every visible line shares the same left edge. Chapter titles use Error, paragraph starts use Log/Info, and ordinary continuation rows use Warning. `Editor ▼ > Synthetic Headers` optionally adds the timestamped fake log header; the same switch is available in Settings. Source spans retain every original character and offset in order. The right-side severity buttons mute or restore both row text and matching icons; they never hide text.

`Clear` toggles the visible buffer off and on. Clicking it again restores the same page and scroll position; it never closes the book or changes progress, bookmarks, or saved state. Opening a book, changing page, or jumping to a chapter also restores the display.

The toolbar search field searches chapter titles and automatically expands the chapter panel while a query is active. In Console mode the chapter/bookmark sidebar starts collapsed; use the narrow `▶` handle or `Editor ▼ > Panels` to expand it, and `◀` to collapse it again.

`Editor ▼ > Settings` switches the current reader window to its lightweight Settings page. Shortcut bindings, Console appearance, optional synthetic headers, and the boss-key target can all be changed there without opening another plugin window. Shortcut bindings are stored by Unity's user-level Shortcut Manager rather than in the project.

| Action | Control or default shortcut |
| --- | --- |
| Open a local book | `Open TXT` |
| Previous / next page | Visible `Prev` / `Next` buttons |
| Hide / restore the visible text | `Clear` |
| Add a bookmark | `Mark` |
| Chapters, bookmarks, library, settings | `Editor ▼`; chapter panel also uses `▶` / `◀` |
| Toggle reader | `Ctrl+Alt+R` |
| Quick hide to the selected decoy / restore | `Ctrl+Alt+H` |
| Hide while focused | `Esc` |
| Next page | `Space`, `PageDown`, `Right Arrow` |
| Previous page | `PageUp`, `Left Arrow` |

Shortcuts can be changed directly on the reader's Settings page or in Unity's Shortcut Manager.

Use `Editor ▼ > Boss-key target` to choose `Scene`, `Console`, `Profiler`, `Animator`, or `Project` as the quick-hide target. `Editor ▼ > Classic Reader` restores the original reading layout; its Settings page can switch back to the Console skin. If the selected decoy cannot be opened, the reader falls back to `Scene`.

## Privacy and project isolation

- No network requests.
- No book content is copied into the project or the package.
- Reader data never uses `Assets`, `EditorPrefs`, or project-relative databases.
- UPM installation only causes the normal local entries in `manifest.json` and `packages-lock.json`.
- Traditional `.unitypackage` installation imports only one Editor-only DLL and its `.meta` at the `Assets` root, with no source, books, or reader state.

See [Privacy](Documentation~/PRIVACY.md) and [Installation details](Documentation~/INSTALLATION.md).

## Development

The package targets Unity 2020.3 or newer. EditMode tests are under `Tests/Editor`. Version `0.2.4` passed import and compilation checks plus all 30 EditMode tests on Unity `2022.3.62f2`, including a real GB18030 long-form text smoke test with 1,840 detected chapters.

## License

[MIT](LICENSE)
