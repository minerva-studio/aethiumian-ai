using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Amlos.AI.Editor
{
    public class ComponentReferenceDrawer
    {
        public enum Mode
        {
            dropDown,
            search
        }

        private static Type[] allComponentClasses;

        private Mode mode;
        private ComponentReference componentReference;
        private string labelName;

        public ComponentReference ComponentReference { get => componentReference; set => componentReference = value; }

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
            Initialize();
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
        }

        private void Initialize()
        {
            GetAllComponentType();
        }

        private void DrawSearch()
        {
            string name = componentReference.name;
            GUILayout.BeginVertical();
            name = EditorGUILayout.TextField("", name);
            var names = GetUniqueNames(allComponentClasses, name + ".");
            var labels = names.Prepend("..").Prepend(name).ToArray();
            var result = EditorGUILayout.Popup(labelName, 0, labels);
            GUILayout.EndVertical();
            if (result != 0 && result != 1)
            {
                name = labels[result];
            }
            componentReference.name = name;
        }

        private void DrawDropdown()
        {
            string name = componentReference.name;
            string currentNamespace = name + ".";
            string broadNamespace = (name.Contains(".") ? name[..name.LastIndexOf('.')] : "") + ".";

            var names = GetUniqueNames(allComponentClasses, currentNamespace);
            var labels = names.Prepend("..").Prepend(name).ToArray();
            var result = EditorGUILayout.Popup(labelName, 0, labels);
            if (result == 1)
            {
                componentReference.name = broadNamespace;
            }
            else if (result > 1)
            {
                componentReference.name = labels[result];
            }
        }

        private void EndCheck()
        {
            componentReference.name = componentReference.name.TrimEnd('.');
            try
            {
                componentReference.assemblyFullName = allComponentClasses.FirstOrDefault(t => t.FullName == componentReference.name).AssemblyQualifiedName;
            }
            catch (Exception)
            {
                EditorGUILayout.LabelField("Invalid Component", GUILayout.MaxWidth(150));
            }
        }

        public IEnumerable<string> GetUniqueNames(IEnumerable<Type> classes, string key)
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
            return set;
        }

        private IEnumerable<Type> GetAllComponentType()
        {
            if (allComponentClasses != null)
            {
                return allComponentClasses;
            }
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            IEnumerable<Type> classes = new List<Type>();
            foreach (var item in assemblies)
            {
                classes = classes.Union(item.GetTypes().Where(t => t.IsSubclassOf(typeof(Component))));
            }
            allComponentClasses = classes.ToArray();
            return allComponentClasses;
        }

    }
}