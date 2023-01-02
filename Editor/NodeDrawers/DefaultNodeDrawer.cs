using Minerva.Module;
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Amlos.AI.Editor
{
    /// <summary>
    /// Default implementation of node drawer
    /// </summary>
    public class DefaultNodeDrawer : NodeDrawerBase
    {
        public override void Draw()
        {
            //Service service;
            var type = node.GetType();
            var fields = type.GetFields();
            DrawFields(fields);
        }

        private void DrawFields(FieldInfo[] fields)
        {
            foreach (var field in fields)
            {
                if (field.IsLiteral && !field.IsInitOnly) continue;
                if (field.FieldType.IsSubclassOf(typeof(UnityEngine.Object))) continue;
                if (field.Name == nameof(node.name)) continue;
                if (field.Name == nameof(node.uuid)) continue;
                if (field.Name == nameof(node.parent)) continue;
                if (field.Name == nameof(node.services)) continue;
                if (field.Name == nameof(node.behaviourTree)) continue;
                string labelName = field.Name.ToTitleCase();

                if (!Attribute.IsDefined(field, typeof(SpaceAttribute)))
                {
                    foreach (SpaceAttribute item in Attribute.GetCustomAttributes(field, typeof(SpaceAttribute)).Cast<SpaceAttribute>())
                    {
                        GUILayout.Space(item.height);
                    }
                }

                if (!Attribute.IsDefined(field, typeof(DisplayIfAttribute)))
                {
                    DrawField(labelName, field, node);
                    continue;
                }

                bool draw;
                try
                {
                    draw = ConditionalFieldAttribute.IsTrue(node, field);
                }
                catch (Exception)
                {
                    EditorGUILayout.LabelField(labelName, "DisplayIf attribute breaks, ask for help now");
                    continue;
                }

                if (draw) DrawField(labelName, field, node);
            }
        }
    }
}