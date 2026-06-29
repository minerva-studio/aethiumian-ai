using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    /// <summary>
    /// Author: Codex
    /// Applies consistent title content to Aethiumian AI editor windows.
    /// </summary>
    internal static class AIEditorTitleContent
    {
        internal const string AI_EDITOR_ICON_GUID = "83d31a3c719c41359836599723fadbb2";
        internal const string AI_INSPECTOR_ICON_GUID = "7ef7f783fdaa41878374dc381fdb5517";

        /// <summary>
        /// Applies the behaviour tree editor title and icon.
        /// </summary>
        /// <param name="window">The window receiving the title content.</param>
        /// <param name="title">The title text to display in the window tab.</param>
        /// <returns>No return value.</returns>
        internal static void ApplyEditorTitle(EditorWindow window, string title)
        {
            Apply(window, title, AI_EDITOR_ICON_GUID);
        }

        /// <summary>
        /// Applies the runtime inspector title and icon.
        /// </summary>
        /// <param name="window">The window receiving the title content.</param>
        /// <param name="title">The title text to display in the window tab.</param>
        /// <returns>No return value.</returns>
        internal static void ApplyInspectorTitle(EditorWindow window, string title)
        {
            Apply(window, title, AI_INSPECTOR_ICON_GUID);
        }

        /// <summary>
        /// Applies shared title content while keeping the native window name in sync.
        /// </summary>
        /// <param name="window">The window receiving the title content.</param>
        /// <param name="title">The title text to display in the window tab.</param>
        /// <param name="iconGuid">The GUID of the tab icon asset.</param>
        /// <returns>No return value.</returns>
        private static void Apply(EditorWindow window, string title, string iconGuid)
        {
            Texture2D icon = LoadIcon(iconGuid);
            window.titleContent = new GUIContent(title, icon);
            window.name = title;
        }

        /// <summary>
        /// Loads an icon by GUID so package install location does not affect lookup.
        /// </summary>
        /// <param name="iconGuid">The GUID of the tab icon asset.</param>
        /// <returns>The loaded texture, or null when the asset cannot be resolved.</returns>
        internal static Texture2D LoadIcon(string iconGuid)
        {
            string iconPath = AssetDatabase.GUIDToAssetPath(iconGuid);
            if (string.IsNullOrEmpty(iconPath))
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
        }
    }
}
