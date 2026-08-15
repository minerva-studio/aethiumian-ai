using Aethiumian.AI.References;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    internal class TypeReferenceDrawer
    {
        private const float COMPONENT_REFERENCE_BACKGROUND_COLOR = 32f / 255f;
        private const float BoxPadding = 6f;
        private const float PickerButtonWidth = 52f;

        private TypeReference typeReference;
        private GUIContent label;
        private bool expanded;
        private IReadOnlyList<Type> types;
        private GenericMenu menu;

        public TypeReference TypeReference { get => typeReference; set => typeReference = value; }
        public IReadOnlyList<Type> MatchClasses => types ??= TypeCache.GetTypesDerivedFrom(typeReference.BaseType)
            .Where(type => !type.IsAbstract && !type.IsGenericTypeDefinition && !string.IsNullOrEmpty(type.FullName))
            .ToArray();

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
            string fullSummary = expanded ? label.text : $"{label.text}:\t{typeReference.ReferType?.FullName}";
            GUIContent foldoutLabel = new(fullSummary, fullSummary);
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
            float overflowWidth = Mathf.Min(GraphInspectorLayout.OverflowWidth, rect.width);
            float remainingWidth = Mathf.Max(0f, rect.width - overflowWidth);
            float pickWidth = Mathf.Min(PickerButtonWidth, remainingWidth);
            Rect valueRect = new(rect.x, rect.y, Mathf.Max(0f, remainingWidth - pickWidth), rect.height);
            Rect pickRect = new(valueRect.xMax, rect.y, pickWidth, rect.height);
            Rect overflowRect = new(pickRect.xMax, rect.y, overflowWidth, rect.height);

            typeReference.fullName = EditorGUI.TextField(valueRect, typeReference.fullName);

            if (GUI.Button(pickRect, "Pick..."))
            {
                ShowTypePickerMenu();
            }

            if (GUI.Button(overflowRect, "⋮", EditorStyles.miniButton))
            {
                GenericMenu overflowMenu = new();
                overflowMenu.AddItem(new GUIContent("Clear"), false, () => typeReference.fullName = string.Empty);
                overflowMenu.ShowAsContext();
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

            type = MatchClasses.FirstOrDefault(candidate => candidate.FullName == typeReference.fullName);
            return type != null;
        }
    }
}
