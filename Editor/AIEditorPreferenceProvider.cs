using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    /// <summary>
    /// Author: Codex
    /// Registers user-local Preferences for the Aethiumian AI editor.
    /// </summary>
    internal static class AIEditorPreferenceProvider
    {
        internal const string PREFERENCE_PATH = "Preferences/Aethiumian AI/AI Editor";

        private static Vector2 scrollPosition;

        private static class Styles
        {
            internal static readonly GUIContent commonNodes = new(
                "Common usage",
                "A list of nodes that will show on the top of the node creation list");
        }

        /// <summary>
        /// Opens the AI editor preferences page.
        /// </summary>
        /// <returns>No return value.</returns>
        internal static void OpenPreferences()
        {
            SettingsService.OpenUserPreferences(PREFERENCE_PATH);
        }

        /// <summary>
        /// Creates the Unity Preferences provider for AI editor settings.
        /// </summary>
        /// <returns>The settings provider used by Unity Preferences.</returns>
        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            return new SettingsProvider(PREFERENCE_PATH, SettingsScope.User)
            {
                label = "AI Editor",
                guiHandler = DrawPreferences,
                keywords = new HashSet<string>(new[] { "Aethiumian", "AI", "Editor", "Graph", "Debug", "Safe Mode", "Common Nodes" })
            };
        }

        private static void DrawPreferences(string searchContext)
        {
            AIEditorSetting settings = AIEditorSetting.GetOrCreateSettings();
            bool shouldSave = false;

            using (new EditorGUI.IndentLevelScope(1))
            using (EditorGUILayout.ScrollViewScope scrollScope = new(scrollPosition))
            {
                scrollPosition = scrollScope.scrollPosition;

                DrawLegacySettingsNotice();

                EditorGUI.BeginChangeCheck();

                Header("Tree", false);
                SerializedObject serializedSettings = new(settings);
                SerializedProperty commonNodes = serializedSettings.FindProperty(nameof(AIEditorSetting.commonNodes));
                EditorGUILayout.PropertyField(commonNodes, Styles.commonNodes);
                serializedSettings.ApplyModifiedProperties();
                serializedSettings.Dispose();

                using (ButtonIndent())
                {
                    if (GUILayout.Button("Reset common nodes", GUILayout.Height(30), GUILayout.Width(200)))
                    {
                        settings.InitializeCommonNodes();
                        shouldSave = true;
                    }
                }

                Header("Graph (Experimental)");
                using (ButtonIndent())
                {
                    if (!settings.enableGraph && GUILayout.Button("Enable Graph View", GUILayout.Height(30), GUILayout.Width(200)))
                    {
                        settings.enableGraph = true;
                        shouldSave = true;
                    }

                    if (settings.enableGraph && GUILayout.Button("Disable Graph View", GUILayout.Height(30), GUILayout.Width(200)))
                    {
                        settings.enableGraph = false;
                        shouldSave = true;
                    }
                }

                Header("Debug");
                settings.debugMode = EditorGUILayout.Toggle("Debug Mode", settings.debugMode);

                Header("Other");
                settings.safeMode = EditorGUILayout.Toggle("Enable Safe Mode", settings.safeMode);
                using (ButtonIndent())
                {
                    if (GUILayout.Button("Reset Settings", GUILayout.Height(30), GUILayout.Width(200)))
                    {
                        settings = AIEditorSetting.ResetSettings();
                        shouldSave = true;
                    }
                }

                Header("Credit");
                GUIStyle style = new() { richText = true };
                EditorGUILayout.TextField(
                    "2026 Minerva Game Studio, Documentation see: <a href=\"https://github.com/minerva-studio/aethiumian-ai/blob/main/DOC_EN.md\">Documentation link</a>",
                    style);

                settings.SanitizeCommonNodes();
                if (EditorGUI.EndChangeCheck() || shouldSave)
                {
                    AIEditorSetting.SaveSettings(settings);
                }
            }
        }

        private static void DrawLegacySettingsNotice()
        {
            if (!AIEditorSetting.HasLegacySettings())
            {
                return;
            }

            EditorGUILayout.HelpBox(
                $"Legacy AI Editor settings still exist at {AIEditorSetting.LEGACY_SETTING_PATH}. Preferences now save to {AIEditorSetting.SETTING_PATH}; remove the legacy asset manually when you no longer need it.",
                MessageType.Info);

            using (ButtonIndent())
            {
                if (GUILayout.Button("Reveal legacy settings asset", GUILayout.Height(30), GUILayout.Width(200)))
                {
                    UnityEngine.Object legacySettings = AssetDatabase.LoadAssetAtPath<AIEditorSetting>(AIEditorSetting.LEGACY_SETTING_PATH);
                    if (legacySettings)
                    {
                        EditorGUIUtility.PingObject(legacySettings);
                        Selection.activeObject = legacySettings;
                    }
                }
            }
        }

        private static void Header(string title, bool space = true)
        {
            if (space)
            {
                EditorGUILayout.Space(EditorGUIUtility.singleLineHeight);
            }

            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        private static IDisposable ButtonIndent()
        {
            EditorGUILayout.HorizontalScope horizontalScope = new();
            GUILayout.Space(20);
            return horizontalScope;
        }
    }
}
