using Amlos.AI.References;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Amlos.AI.Editor
{
    [Obsolete]
    public class ComponentReferenceDrawer
    {
        public enum Mode
        {
            search,
            dropDown,
        }

        private const float COMPONENT_REFERENCE_BACKGROUND_COLOR = 32f / 255f;
        private const string Label = "Class Full Name";
        private static Type[] allComponentClasses;

        private Mode mode;
        private ComponentReference componentReference;
        private string labelName;
        private Vector2 listRect;
        private bool expanded;

        public ComponentReference ComponentReference { get => componentReference; set => componentReference = value; }
        public static Type[] AllComponentClasses { get => allComponentClasses ??= GetAllComponentType(); }

        public ComponentReferenceDrawer(ComponentReference componentReference, string labelName)
        {
            this.componentReference = componentReference;
            this.labelName = labelName;
        }

        public void Reset(ComponentReference componentReference, string labelName)
        {
            this.componentReference = componentReference;
            this.labelName = labelName;
        }

        public void Draw()
        {
            expanded = EditorGUILayout.Foldout(expanded, labelName);
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


                mode = (Mode)EditorGUILayout.EnumPopup(mode, GUILayout.MaxWidth(100));
                GUILayout.EndHorizontal();
                DrawAssemblyFullName();
                GUILayout.EndVertical();

                GUI.backgroundColor = color;
                EditorGUI.indentLevel--;
            }
        }

        private void DrawAssemblyFullName()
        {
            if (!string.IsNullOrEmpty(componentReference.assemblyFullName))
            {
                var status = GUI.enabled;
                GUI.enabled = false;
                EditorGUILayout.LabelField(" ", "Assembly Full Name: " + componentReference.assemblyFullName);
                GUI.enabled = status;
            }
        }

        private void DrawSearch()
        {
            string name = componentReference.classFullName;
            GUILayout.BeginVertical();
            name = EditorGUILayout.TextField(Label, name);
            var names = GetUniqueNames(AllComponentClasses, name + ".");
            //var labels = names.Prepend("..").Prepend(name).ToArray();
            //var result = EditorGUILayout.Popup(labelName, 0, labels);
            //if (result != 0 && result != 1)
            //{
            //    name = labels[result];
            //}
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(180);
            if (GUILayout.Button("..")) name = Backward(name, true);
            EditorGUILayout.EndHorizontal();
            listRect = EditorGUILayout.BeginScrollView(listRect, GUILayout.MaxHeight(22 * Mathf.Min(8, names.Count())));

            foreach (var item in names)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(180);
                if (GUILayout.Button(item)) name = item;
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            GUILayout.EndVertical();
            componentReference.classFullName = name;
        }

        private void DrawDropdown()
        {
            string name = componentReference.classFullName;
            string currentNamespace = name + ".";
            string broadNamespace = Backward(name);

            var names = GetUniqueNames(AllComponentClasses, currentNamespace);
            var labels = names.Prepend("..").Prepend(name).ToArray();
            var result = EditorGUILayout.Popup(Label, 0, labels);
            if (result == 1)
            {
                componentReference.classFullName = broadNamespace;
            }
            else if (result > 1)
            {
                componentReference.classFullName = labels[result];
            }
        }

        private void EndCheck()
        {
            var color = GUI.contentColor;
            componentReference.classFullName = componentReference.classFullName.TrimEnd('.');
            try
            {
                componentReference.assemblyFullName = AllComponentClasses.FirstOrDefault(t => t.FullName == componentReference.classFullName).AssemblyQualifiedName;
                GUI.contentColor = Color.green;
                EditorGUILayout.LabelField("class: " + componentReference.classFullName.Split('.').LastOrDefault(), GUILayout.MaxWidth(150));
            }
            catch (Exception)
            {
                componentReference.assemblyFullName = string.Empty;
                GUI.contentColor = Color.red;
                EditorGUILayout.LabelField("Invalid Component", GUILayout.MaxWidth(150));
            }
            GUI.contentColor = color;
        }

        /// <summary>
        /// Backward a class
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public string Backward(string name)
        {
            return (name.Contains(".") ? name[..name.LastIndexOf('.')] + "." : "");
        }

        /// <summary>
        /// Backward a class
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public string Backward(string name, bool continous)
        {
            if (continous)
            {
                do
                {
                    name = Backward(name.TrimEnd('.'));
                }
                while (GetUniqueNames(AllComponentClasses, name).Count() == 1 && !string.IsNullOrEmpty(name));
            }
            else name = Backward(name);
            return name;
        }

        public static IEnumerable<string> GetUniqueNames(IEnumerable<Type> classes, string key)
        {
            HashSet<string> set = new HashSet<string>();
            if (key == ".")
            {
                key = "";
            }
            foreach (var item in classes.Where(t => t.FullName.StartsWith(key)))
            {
                string after = item.FullName[key.Length..];
                set.Add(key + after.Split(".").FirstOrDefault());
            }
            if (set.Count == 1)
            {
                //Debug.Log(key);
                string onlyKey = set.FirstOrDefault();

                //Debug.Log(onlyKey);
                var ret = GetUniqueNames(classes, onlyKey + ".");
                return ret.Count() == 0 ? set : ret;
            }
            return set;
        }

        /// <summary>
        /// Get all component type
        /// </summary>
        /// <returns></returns>
        private static Type[] GetAllComponentType()
        {
            return TypeCache.GetTypesDerivedFrom<Component>().ToArray();
        }

    }
}