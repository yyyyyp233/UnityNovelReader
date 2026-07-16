# Release process

Releases are built from a clean tagged commit on Windows with an installed Unity Editor.

## Build

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools~\Build-UnityPackage.ps1
```

Use `-UnityPath` and `-OutputPath` when the default Unity Hub installation or output path is not suitable. The script creates temporary compile and export projects, then writes `Releases~/UnityNovelReader-<version>.unitypackage`.

Before export, the build script replaces Unity's absolute compiler PDB path in the DLL with the neutral filename `UnityNovelReader.Editor.pdb`. The build fails if it cannot identify the path safely or if an absolute PDB path remains.

## Verify

Before tagging a release:

1. Run the EditMode tests under `Tests/Editor`.
2. Import the artifact into a clean project using the oldest supported Unity version available for release testing.
3. Confirm there are no compile errors and the reader opens from **Tools > Unity Novel Reader > Open Reader**.
4. Confirm the package imports only `Assets/UnityNovelReader.Editor.dll` and its `.meta`.
5. Confirm the DLL importer enables Editor only and disables all player platforms.
6. Scan the exported DLL and confirm it contains no absolute PDB, user-profile, temporary-build, or project path.
7. Open representative UTF-8, UTF-16, and GB18030 files; verify chapters, progress, bookmarks, shortcuts, and each disguise target.
8. Record the Unity version and SHA-256 checksum in the release notes.

## Publish

Create an annotated `v<version>` tag on the verified commit, push the tag, and create a GitHub Release containing the `.unitypackage`. Keep the repository source, `package.json`, tests, documentation, and MIT license available so UPM and binary users can audit the implementation.
