using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    internal static class AIProjectSettingsProvider
    {
        internal const string SETTINGS_PATH = "Project/Aethiumian AI/AI Settings";

        private static Vector2 scrollPosition;

        private static class Styles
        {
            internal static readonly GUIContent globalVariables = new(
                "Global Variables",
                "Variables that are available to every behaviour tree.");

            internal static readonly GUIContent globalRandomSource = new(
                "Global Random Source",
                "Default random source used by AI randomization.");
        }

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            return new SettingsProvider(SETTINGS_PATH, SettingsScope.Project)
            {
                label = "AI Settings",
                guiHandler = DrawSettings,
                keywords = new HashSet<string>(new[] { "Aethiumian", "AI", "Settings", "Global Variables", "Random Source" })
            };
        }

        [MenuItem("Window/Aethiumian AI/AI Settings")]
        private static void OpenSettingsFromMenu()
        {
            OpenProjectSettings();
        }

        internal static void OpenProjectSettings()
        {
            SettingsService.OpenProjectSettings(SETTINGS_PATH);
        }

        private static void DrawSettings(string searchContext)
        {
            AISetting settings = AISetting.GetOrCreateSettings();
            if (!settings)
            {
                EditorGUILayout.HelpBox("AI settings asset could not be loaded.", MessageType.Error);
                return;
            }

            using (EditorGUILayout.ScrollViewScope scrollScope = new(scrollPosition))
            {
                scrollPosition = scrollScope.scrollPosition;

                EditorGUILayout.HelpBox($"Settings asset: {AISetting.EDITOR_SETTING_PATH}", MessageType.None);

                using SerializedObject serializedSettings = AISetting.GetSerializedSettings();
                serializedSettings.UpdateIfRequiredOrScript();

                EditorGUI.BeginChangeCheck();

                Header("Variables", false);
                EditorGUILayout.PropertyField(
                    serializedSettings.FindProperty(nameof(AISetting.globalVariables)),
                    Styles.globalVariables,
                    true);

                Header("Randomization");
                EditorGUILayout.PropertyField(
                    serializedSettings.FindProperty(nameof(AISetting.globalRandomSource)),
                    Styles.globalRandomSource);

                if (EditorGUI.EndChangeCheck())
                {
                    serializedSettings.ApplyModifiedProperties();
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssetIfDirty(settings);
                }

                Header("Asset");
                using (ButtonIndent())
                {
                    if (GUILayout.Button("Reveal AI Settings Asset", GUILayout.Height(30), GUILayout.Width(200)))
                    {
                        EditorGUIUtility.PingObject(settings);
                        Selection.activeObject = settings;
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
