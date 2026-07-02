using Aethiumian.AI.Randomization;
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

            internal static readonly GUIContent defaultRandomSource = new(
                "Default Random Source",
                "Default random source binding used when a tree or node does not override it.");
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
                    serializedSettings.FindProperty(nameof(AISetting.defaultRandomSource)),
                    Styles.defaultRandomSource,
                    true);

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

    [CustomPropertyDrawer(typeof(RandomSourceBinding))]
    internal sealed class RandomSourceBindingDrawer : PropertyDrawer
    {
        private static readonly GUIContent ScopeContent = new("Scope");
        private const float HelpBoxHeight = 42f;
        private const float MultiLineHelpBoxHeight = 58f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight * 2 + EditorGUIUtility.standardVerticalSpacing;
            if (TryGetHelpBox(property, out string message, out _))
            {
                height += GetHelpBoxHeight(message) + EditorGUIUtility.standardVerticalSpacing;
            }

            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty sourceProperty = property.FindPropertyRelative(nameof(RandomSourceBinding.source));
            SerializedProperty scopeProperty = property.FindPropertyRelative(nameof(RandomSourceBinding.scope));
            if (sourceProperty == null || scopeProperty == null)
            {
                EditorGUI.LabelField(position, label, new GUIContent("Invalid random source binding"));
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            Rect sourceRect = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            Rect scopeRect = new(position.x, sourceRect.yMax + EditorGUIUtility.standardVerticalSpacing, position.width, EditorGUIUtility.singleLineHeight);
            RandomSourceAsset source = sourceProperty.objectReferenceValue as RandomSourceAsset;

            EditorGUI.PropertyField(sourceRect, sourceProperty, label);
            EditorGUI.PropertyField(EditorGUI.IndentedRect(scopeRect), scopeProperty, ScopeContent);

            if (TryGetHelpBox(property, out string message, out MessageType messageType))
            {
                Rect warningRect = new(position.x, scopeRect.yMax + EditorGUIUtility.standardVerticalSpacing, position.width, GetHelpBoxHeight(message));
                EditorGUI.HelpBox(warningRect, message, messageType);
            }

            EditorGUI.EndProperty();
        }

        private static bool TryGetHelpBox(SerializedProperty property, out string message, out MessageType messageType)
        {
            message = string.Empty;
            messageType = MessageType.None;
            SerializedProperty sourceProperty = property.FindPropertyRelative(nameof(RandomSourceBinding.source));
            SerializedProperty scopeProperty = property.FindPropertyRelative(nameof(RandomSourceBinding.scope));
            RandomSourceAsset source = sourceProperty?.objectReferenceValue as RandomSourceAsset;
            if (scopeProperty == null)
            {
                return false;
            }

            if (!source)
            {
                message = GetEmptySourceMessage(property);
                messageType = MessageType.Info;
                return true;
            }

            RandomSourceScope requestedScope = (RandomSourceScope)scopeProperty.enumValueIndex;
            RandomSourceScope normalizedScope = source.NormalizeScope(requestedScope);
            if (requestedScope != normalizedScope)
            {
                message = $"{source.name} does not support {requestedScope}. Runtime will use {normalizedScope}.";
                messageType = MessageType.Warning;
                return true;
            }

            return false;
        }

        private static string GetEmptySourceMessage(SerializedProperty property)
        {
            if (property.propertyPath.Contains("randomSourceOverride"))
            {
                return "No source override. This node will use the behaviour tree random source.";
            }

            if (property.serializedObject.targetObject is AISetting)
            {
                return "No default source. Runtime will fall back to Unity random.";
            }

            return "No tree source. Runtime will use the project default random source.";
        }

        private static float GetHelpBoxHeight(string message)
        {
            return message.Contains('\n') ? MultiLineHelpBoxHeight : HelpBoxHeight;
        }
    }
}
