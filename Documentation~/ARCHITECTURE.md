# Architecture

The package is split into four independent concerns:

1. `NovelTextLoader` detects encoding and opens external files.
2. `ChapterParser` and `NovelPaginator` create chapter and page views using character offsets.
3. `ReaderStateStore` serializes paths, offsets, preferences, and bookmarks under the user's local application data directory.
4. `NovelReaderWindow` renders an Editor-only IMGUI interface and connects rebindable Unity shortcuts.

There is no Runtime assembly and no dependency on project assets.

## Distribution forms

- The GitHub repository and UPM installation retain the complete C# source, tests, and documentation.
- The drag-and-drop `.unitypackage` is a minimal binary distribution containing only one compiled Editor DLL at the `Assets` root; its `.meta` forces Editor-only compatibility, and the complete MIT notice is embedded in assembly metadata.

`Tools~/Build-UnityPackage.ps1` uses separate temporary compile and export projects so source and standalone license files cannot leak into the binary package.
