# Installation and removal

Unity Novel Reader is an Editor-only tool distributed as either a UPM package or a traditional `.unitypackage`. Book files and reader state remain outside the Unity project in both forms.

## Local development install

Use Package Manager's **Add package from disk...** and choose `package.json`, or use `Tools~/Toggle-LocalPackage.ps1`.

The toggle script changes only the package entry in `Packages/manifest.json`. Unity may regenerate `Packages/packages-lock.json`. The script writes a manifest backup to the operating system's temporary directory, not into the project.

## Release package

Download `UnityNovelReader-1.0.0.unitypackage` from the [latest GitHub release](https://github.com/yyyyyp233/UnityNovelReader/releases/latest), then drag it into Unity or use **Assets > Import Package > Custom Package...**.

If upgrading from an older source-based `.unitypackage`, delete the old `Assets/UnityNovelReader` directory before importing the current release. This prevents the old sources and new DLL from being compiled together.

## Git install

Use this Git URL with Package Manager:

```text
https://github.com/yyyyyp233/UnityNovelReader.git#v1.0.0
```

## Traditional `.unitypackage`

Run `Tools~/Build-UnityPackage.ps1` to create a drag-and-drop artifact under `Releases~`. The trailing `~` keeps Unity from scanning release artifacts as package assets.

The build is deliberately split into two temporary Unity projects. The first compiles the open-source Editor assembly; the second exports only `Assets/UnityNovelReader.Editor.dll` with Editor-only plugin settings. The complete MIT notice is embedded in assembly metadata. The result contains one asset path and creates two filesystem files: the DLL and its `.meta`. Source, tests, documentation, readable license files, and build tooling stay in the repository.

The traditional package imports its DLL and `.meta` at the `Assets` root, so version-control clients show only those two files as untracked. Do not add them when the reader is intended to remain a personal local tool. Delete both files to unload it; external reader state is unaffected.

## Removal

Remove the package through Package Manager, run the toggle script with `-Action Uninstall`, or revert both package files with the project's version-control client.

Reading data is deliberately not stored in either package file, so removal and version-control cleanup do not affect it.
