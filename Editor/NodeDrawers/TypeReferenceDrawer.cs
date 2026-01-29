using Amlos.AI.References;
using Minerva.Module;
using Minerva.Module.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Amlos.AI.Editor
{
    internal class TypeReferenceDrawer
    {
        private const float COMPONENT_REFERENCE_BACKGROUND_COLOR = 32f / 255f;
        private const float BoxPadding = 6f;
        private const float PickerButtonWidth = 80f;
        private const float ClearButtonWidth = 60f;

        private TypeReference typeReference;
        private GUIContent label;
        private bool expanded;
        private Tries<Type> types;
        private GenericMenu menu;

        public TypeReference TypeReference { get => typeReference; set => typeReference = value; }
        public Tries<Type> MatchClasses => types ??= TypeSearch.GetTypesDerivedFrom(typeReference.BaseType);

        public TypeReferenceDrawer(TypeReference tr, string labelName)
            : this(tr, new GUIContent(labelName)) { }

        public TypeReferenceDrawer(TypeReference tr, GUIContent label)
        {
            this.typeReference = tr;
            this.label = label;
        }

        public void Reset(TypeReference typeReference, string labelName)
            => Reset(typeReference, new GUIContent(labelName));

        public void Reset(TypeReference typeReference, GUIContent label)
        {
            this.typeReference = typeReference;
            this.label = label;
            this.menu = null;
        }

        /// <summary>
        /// Draw the type reference using layout by forwarding to the rect-based draw method.
        /// </summary>
        public void Draw()
        {
            float height = GetHeight();
            Rect rect = EditorGUILayout.GetControlRect(true, height);
            Draw(rect);
        }

        /// <summary>
        /// Draw the type reference with explicit positioning.
        /// </summary>
        /// <param name="position">The rect used for drawing.</param>
        public void Draw(Rect position)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            Rect foldoutRect = new Rect(position.x, position.y, position.width, lineHeight);
            string foldoutLabel = expanded ? label.text : $"{label.text}:\t{typeReference.ReferType?.FullName}";
            expanded = EditorGUI.Foldout(foldoutRect, expanded, foldoutLabel, true);

            if (!expanded)
            {
                return;
            }

            Rect contentRect = new Rect(position.x, foldoutRect.yMax + spacing, position.width, position.height - lineHeight - spacing);
            contentRect = EditorGUI.IndentedRect(contentRect);
            Color baseColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.white * COMPONENT_REFERENCE_BACKGROUND_COLOR;
            GUI.Box(contentRect, GUIContent.none, EditorStyles.helpBox);
            GUI.backgroundColor = baseColor;

            Rect innerRect = new Rect(contentRect.x + BoxPadding, contentRect.y + BoxPadding, contentRect.width - BoxPadding * 2f, contentRect.height - BoxPadding * 2f);

            Rect inputRow = GetRowRect(ref innerRect);
            DrawInputRow(inputRow);

            Rect statusRow = GetRowRect(ref innerRect);
            DrawStatusLine(statusRow);

            if (!string.IsNullOrEmpty(typeReference.assemblyName))
            {
                Rect assemblyRow = GetRowRect(ref innerRect);
                DrawAssemblyFullName(assemblyRow);
            }
        }

        public float GetHeight()
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            float height = lineHeight;
            if (!expanded)
            {
                return height;
            }

            int rows = 2; // input + status
            if (!string.IsNullOrEmpty(typeReference.assemblyName))
            {
                rows++;
            }

            height += spacing + rows * (lineHeight + spacing) + BoxPadding * 2f;
            return height;
        }

        private static Rect GetRowRect(ref Rect rect)
        {
            Rect row = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
            rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            rect.height -= EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            return row;
        }

        private void DrawInputRow(Rect rect)
        {
            Rect pickerRect = new Rect(rect.xMax - PickerButtonWidth, rect.y, PickerButtonWidth, rect.height);
            Rect clearRect = new Rect(pickerRect.x - ClearButtonWidth - 4f, rect.y, ClearButtonWidth, rect.height);
            Rect fieldRect = new Rect(rect.x, rect.y, clearRect.x - rect.x - 4f, rect.height);

            typeReference.fullName = EditorGUI.TextField(fieldRect, typeReference.fullName);

            if (GUI.Button(clearRect, "Clear"))
            {
                typeReference.fullName = string.Empty;
            }

            if (GUI.Button(pickerRect, "Pick..."))
            {
                ShowTypePickerMenu();
            }
        }

        private void DrawStatusLine(Rect rect)
        {
            var color = GUI.contentColor;
            typeReference.fullName = typeReference.fullName.TrimEnd('.');

            if (TryResolveType(out var type))
            {
                typeReference.SetReferType(type);
                GUI.contentColor = Color.green;
                EditorGUI.LabelField(rect, $"class: {typeReference.fullName.Split('.').LastOrDefault()}");
            }
            else
            {
                typeReference.SetReferType(null);
                GUI.contentColor = Color.red;
                EditorGUI.LabelField(rect, "Invalid Type");
            }

            GUI.contentColor = color;
        }

        private void DrawAssemblyFullName(Rect rect)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.LabelField(rect, " ", "Assembly Full Name: " + typeReference.SimpleQualifiedName);
            }
        }

        private void ShowTypePickerMenu()
        {
            if (menu == null)
            {
                menu = new GenericMenu();
                foreach (var type in TypeCache.GetTypesDerivedFrom(typeReference.BaseType))
                {
                    if (type.IsAbstract)
                    {
                        continue;
                    }
                    if (type.IsGenericTypeDefinition)
                    {
                        continue;
                    }
                    if (!type.IsPublic)
                    {
                        continue;
                    }

                    string path = string.IsNullOrEmpty(type.FullName) ? type.Name : type.FullName.Replace('.', '/');
                    menu.AddItem(new GUIContent(path), false, () =>
                    {
                        typeReference.fullName = type.FullName ?? type.Name;
                    });
                }
            }
            menu.ShowAsContext();
        }

        private bool TryResolveType(out Type type)
        {
            type = null;
            if (string.IsNullOrEmpty(typeReference.fullName))
            {
                return false;
            }

            return MatchClasses.TryGetValue(typeReference.fullName, out type);
        }

        public static IEnumerable<string> GetUniqueNames(Tries<Type> classes, string key)
        {
            if (string.IsNullOrEmpty(key)) return classes.FirstLayerKeys;
            if (classes.TryGetSegment(key, out TriesSegment<Type> trie))
            {
                var firstLevelKeys = trie.FirstLayerKeys;
                // special case: only 1 then down to buttom
                if (firstLevelKeys.Count == 1)
                {
                    return GetUniqueNames(classes, $"{key}.{firstLevelKeys.First<string>()}");
                }
                return firstLevelKeys;
            }
            return Array.Empty<string>();
        }
    }
}
