﻿using Amlos.AI.References;
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
        public enum Mode
        {
            search,
            dropDown,
        }

        private const float COMPONENT_REFERENCE_BACKGROUND_COLOR = 32f / 255f;
        private const string Label = "Class Full Name";


        private Mode mode;
        private TypeReference typeReference;
        private UnityEngine.Object targetObject;
        private GUIContent label;
        private Vector2 listRect;
        private bool expanded;
        private IEnumerable<string> options;
        private Tries<Type> types;


        public TypeReference TypeReference { get => typeReference; set => typeReference = value; }
        public Tries<Type> MatchClasses { get => types ??= TypeSearch.GetTypesDerivedFrom(typeReference.BaseType); }

        public TypeReferenceDrawer(TypeReference tr, string labelName, UnityEngine.Object targetObject = null) : this(tr, new GUIContent(labelName), targetObject) { }
        public TypeReferenceDrawer(TypeReference tr, GUIContent label, UnityEngine.Object targetObject = null)
        {
            this.typeReference = tr;
            this.label = label;
            this.targetObject = targetObject;
        }

        public void Reset(TypeReference typeReference, string labelName, UnityEngine.Object targetObject = null) => Reset(typeReference, new GUIContent(labelName));
        public void Reset(TypeReference typeReference, GUIContent label, UnityEngine.Object targetObject = null)
        {
            this.typeReference = typeReference;
            this.label = label;
            this.targetObject = targetObject;
            this.options = null;
        }

        public void Draw()
        {
            EditorGUILayout.BeginVertical();
            expanded = EditorGUILayout.Foldout(expanded, label + (expanded ? "" : ":\t" + TypeReference.ReferType?.FullName));
            if (expanded)
            {
                EditorGUI.indentLevel++;
                var color = GUI.backgroundColor;
                GUI.backgroundColor = Color.white * COMPONENT_REFERENCE_BACKGROUND_COLOR;
                var colorStyle = new GUIStyle();
                colorStyle.normal.background = Texture2D.whiteTexture;

                GUILayout.BeginVertical(colorStyle);
                GUILayout.BeginHorizontal();
                switch (mode)
                {
                    case Mode.dropDown:
                        DrawDropdown();
                        break;
                    case Mode.search:
                        DrawSearch();
                        break;
                    default:
                        break;
                }
                EndCheck();

                mode = (Mode)EditorGUILayout.EnumPopup(mode, GUILayout.MaxWidth(150));
                GUILayout.EndHorizontal();
                DrawAssemblyFullName();
                GUILayout.EndVertical();

                GUI.backgroundColor = color;
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawAssemblyFullName()
        {
            if (!string.IsNullOrEmpty(typeReference.assemblyName))
            {
                var status = GUI.enabled;
                GUI.enabled = false;
                EditorGUILayout.LabelField(" ", "Assembly Full Name: " + typeReference.SimpleQualifiedName);
                GUI.enabled = status;
            }
        }

        private void DrawSearch()
        {
            GUILayout.BeginVertical();
            var newName = EditorGUILayout.TextField(Label, typeReference.fullName);
            if (newName != typeReference.fullName)
            {
                typeReference.fullName = newName;
                EndCheck();
                if (typeReference.ReferType == null) options = GetUniqueNames(MatchClasses, typeReference.fullName);
            }
            options ??= GetUniqueNames(MatchClasses, typeReference.fullName);

            GUILayout.BeginHorizontal();
            GUILayout.Space(180);
            if (!string.IsNullOrEmpty(typeReference.fullName) && GUILayout.Button(".."))
            {
                Backward();
                UpdateOptions();
            }

            GUILayout.EndHorizontal();
            listRect = GUILayout.BeginScrollView(listRect, GUILayout.MaxHeight(22 * Mathf.Min(8, options.Count())));

            bool updated = false;
            foreach (var item in options)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(180);
                if (GUILayout.Button(item))
                {
                    Append(item);
                    updated = true;
                    GUILayout.EndHorizontal();
                    break;
                }
                GUILayout.EndHorizontal();
            }
            if (updated)
            {
                UpdateOptions();
            }
            GUILayout.EndScrollView();

            GUILayout.EndVertical();
        }

        private void UpdateOptions()
        {
            options = GetUniqueNames(MatchClasses, typeReference.fullName);
        }

        private void DrawDropdown()
        {
            string name = typeReference.fullName;
            string currentNamespace = name;

            var names = GetUniqueNames(MatchClasses, currentNamespace);
            var labels = names.Prepend("..").Prepend(name).ToArray();
            var result = EditorGUILayout.Popup(Label, 0, labels);
            if (result == 1)
            {
                Backward();
                //typeReference.classFullName = Backward(name);
            }
            else if (result > 1)
            {
                Append(labels[result]);
            }
        }

        private void EndCheck()
        {
            var color = GUI.contentColor;
            typeReference.fullName = typeReference.fullName.TrimEnd('.');
            if (MatchClasses.TryGetValue(typeReference.fullName, out var type))
            {
                if (targetObject) Undo.RecordObject(targetObject, $"Set type reference to {typeReference.ReferType}");

                typeReference.SetReferType(type);
                GUI.contentColor = Color.green;
                EditorGUILayout.LabelField("class: " + typeReference.fullName.Split('.').LastOrDefault(), GUILayout.MaxWidth(150));
            }
            else
            {
                if (targetObject && string.IsNullOrEmpty(typeReference.fullName)) Undo.RecordObject(targetObject, $"Clear type reference");

                typeReference.SetReferType(null);
                GUI.contentColor = Color.red;
                EditorGUILayout.LabelField("Invalid Type", GUILayout.MaxWidth(150));
            }
            GUI.contentColor = color;
        }




        void Append(string append)
        {
            typeReference.fullName = Append(TypeReference.fullName, append);
        }

        /// <summary>
        /// Backward a class
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        void Backward()
        {
            do
            {
                typeReference.fullName = Backward(typeReference.fullName.TrimEnd('.'));
            }
            while (GetUniqueNames(MatchClasses, typeReference.fullName).Count() == 1 && !string.IsNullOrEmpty(typeReference.fullName));
            if (typeReference.fullName.EndsWith('.'))
                typeReference.fullName = typeReference.fullName[..^1];
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







        static string Append(string name, string append)
        {
            if (string.IsNullOrEmpty(name) || name[^1] == '.')
            {
                name += append;
            }
            else
            {
                name += $".{append}";
            }
            return name;
        }

        /// <summary>
        /// Backward a class
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        static string Backward(string name)
        {
            if (name.Contains("."))
            {
                return name[..name.LastIndexOf('.')] + ".";
            }
            else
            {
                return "";
            }
        }
    }
}