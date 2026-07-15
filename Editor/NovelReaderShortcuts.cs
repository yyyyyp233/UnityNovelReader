using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace UnityNovelReader.Editor
{
    internal static class NovelReaderShortcuts
    {
        internal const string ToggleShortcutId = "Unity Novel Reader/Toggle Reader";
        internal const string QuickHideShortcutId = "Unity Novel Reader/Quick Hide";

        [Shortcut(ToggleShortcutId, KeyCode.R, ShortcutModifiers.Control | ShortcutModifiers.Alt)]
        private static void ToggleReader()
        {
            NovelReaderWindow.ToggleWindow(false);
        }

        [Shortcut(QuickHideShortcutId, KeyCode.H, ShortcutModifiers.Control | ShortcutModifiers.Alt)]
        private static void QuickHide()
        {
            NovelReaderWindow.ToggleWindow(true);
        }
    }
}
