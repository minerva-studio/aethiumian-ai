using Amlos.AI.Nodes;
using Minerva.Module;
using Minerva.Module.Editor;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
namespace Amlos.AI.Editor
{
    /// <summary>
    /// Default implementation of node drawer
    /// </summary>
    public class DefaultNodeDrawer : NodeDrawerBase
    {
        private static MethodInfo getPropertyMethod;

        public override void Draw()
        {
            //Service service;
            if (!editor.editorSetting.useSerializationPropertyDrawer)
            {
                var type = node.GetType();
                var fields = type.GetFields();
                DrawFields(fields);
            }
            else
                DrawSerialized();
        }

        // a new method drawer
        private void DrawSerialized()
        {
            var property = base.property;
            string propertyPath = property.propertyPath;
            property.Next(true);
            while (property.NextVisible(false))
            {
                if (!property.propertyPath.Contains(propertyPath))
                    break;

                if (property.name == nameof(node.name)) continue;
                if (property.name == nameof(node.uuid)) continue;
                if (property.name == nameof(node.parent)) continue;
                if (property.name == nameof(node.services)) continue;
                if (property.name == nameof(node.behaviourTree)) continue;
                if (property.name == nameof(Flow.isFolded)) continue;

                var field = property.GetMemberInfo() as FieldInfo;
                bool draw = false;
                if (!Attribute.IsDefined(field, typeof(DisplayIfAttribute))) draw = true;
                if (!draw) try { draw = ConditionalFieldAttribute.IsTrue(node, field); } catch (Exception) { EditorGUILayout.LabelField(property.displayName, "DisplayIf attribute breaks, ask for help now"); continue; }
                if (!draw) continue;

                DrawProperty(property, field, node);
            }
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

                bool draw = false;
                if (!Attribute.IsDefined(field, typeof(DisplayIfAttribute))) draw = true;
                if (!draw)
                    try { draw = ConditionalFieldAttribute.IsTrue(node, field); }
                    catch (Exception) { EditorGUILayout.LabelField(labelName, "DisplayIf attribute breaks, ask for help now"); continue; }
                if (!draw) continue;

                DrawField(labelName, field, node);
            }
        }
    }
}