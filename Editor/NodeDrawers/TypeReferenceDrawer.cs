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
        public enum Mode
        {
            search,
            dropDown,
        }

        private const float COMPONENT_REFERENCE_BACKGROUND_COLOR = 32f / 255f;
        private const string Label = "Class Full Name";


        private Mode mode;
        private TypeReference typeReference;
        private GUIContent label;
        private Vector2 listRect;
        private bool expanded;
        private IEnumerable<string> options;
        private Tries<Type> types;


        public TypeReference TypeReference { get => typeReference; set => typeReference = value; }
        public Tries<Type> MatchClasses { get => types ??= TypeSearch.GetTypesDerivedFrom(typeReference.BaseType); }

        public TypeReferenceDrawer(TypeReference tr, string labelName) : this(tr, new GUIContent(labelName)) { }
        public TypeReferenceDrawer(TypeReference tr, GUIContent label)
        {
            this.typeReference = tr;
            this.label = label;
        }

        public void Reset(TypeReference typeReference, string labelName) => Reset(typeReference, new GUIContent(labelName));
        public void Reset(TypeReference typeReference, GUIContent label)
        {
            this.typeReference = typeReference;
            this.label = label;
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
            if (!string.IsNullOrEmpty(typeReference.assemblyFullName))
            {
                var status = GUI.enabled;
                GUI.enabled = false;
                EditorGUILayout.LabelField(" ", "Assembly Full Name: " + typeReference.assemblyFullName);
                GUI.enabled = status;
            }
        }

        private void DrawSearch()
        {
            GUILayout.BeginVertical();
            var newName = EditorGUILayout.TextField(Label, typeReference.classFullName);
            if (newName != typeReference.classFullName)
            {
                typeReference.classFullName = newName;
                EndCheck();
                if (typeReference.ReferType == null) options = GetUniqueNames(MatchClasses, typeReference.classFullName);
            }
            options ??= GetUniqueNames(MatchClasses, typeReference.classFullName);

            GUILayout.BeginHorizontal();
            GUILayout.Space(180);
            if (!string.IsNullOrEmpty(typeReference.classFullName) && GUILayout.Button(".."))
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
            options = GetUniqueNames(MatchClasses, typeReference.classFullName);
        }

        private void DrawDropdown()
        {
            string name = typeReference.classFullName;
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
            typeReference.classFullName = typeReference.classFullName.TrimEnd('.');
            if (MatchClasses.TryGetValue(typeReference.classFullName, out var type))
            {
                typeReference.SetReferType(type);
                GUI.contentColor = Color.green;
                EditorGUILayout.LabelField("class: " + typeReference.classFullName.Split('.').LastOrDefault(), GUILayout.MaxWidth(150));
            }
            else
            {
                typeReference.SetReferType(null);
                GUI.contentColor = Color.red;
                EditorGUILayout.LabelField("Invalid Type", GUILayout.MaxWidth(150));
            }
            GUI.contentColor = color;
        }




        void Append(string append)
        {
            typeReference.classFullName = Append(TypeReference.classFullName, append);
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
                typeReference.classFullName = Backward(typeReference.classFullName.TrimEnd('.'));
            }
            while (GetUniqueNames(MatchClasses, typeReference.classFullName).Count() == 1 && !string.IsNullOrEmpty(typeReference.classFullName));
            if (typeReference.classFullName.EndsWith('.'))
                typeReference.classFullName = typeReference.classFullName[..^1];
        }






        public static IEnumerable<string> GetUniqueNames(Tries<Type> classes, string key)
        {
            if (classes.TryGetSubTrie(key, out var trie))
            {
                var firstLevelKeys = trie.FirstLevelKeys;
                // special case: only 1 then down to buttom
                if (firstLevelKeys.Count == 1)
                {
                    return GetUniqueNames(classes, $"{key}.{firstLevelKeys.First()}");
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