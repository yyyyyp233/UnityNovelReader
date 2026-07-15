# Privacy and data location

Unity Novel Reader is local-only.

- It does not make network requests.
- It reads only the book selected by the user.
- It stores file paths, character offsets, preferences, and bookmarks.
- It does not copy book contents into its state file.

The default Windows location is `%LOCALAPPDATA%\UnityNovelReader\state.json`. A previous valid state is retained as `state.json.bak` when a new state is written.

Set `UNITY_NOVEL_READER_HOME` before Unity starts to choose another directory. The value should point outside the Unity project if project isolation is required.

Removing the package intentionally keeps this directory. Delete it manually only when reading history and bookmarks should be erased.
