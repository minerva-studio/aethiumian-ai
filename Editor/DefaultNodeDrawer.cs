using Minerva.Module;
using System;
using System.Reflection;
using UnityEditor;

namespace Amlos.AI.Editor
{
    /// <summary>
    /// Default implementation of node drawer
    /// </summary>
    public class DefaultNodeDrawer : NodeDrawerBase
    {
        public override void Draw()
        {
            var type = node.GetType();
            var fields = type.GetFields();
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
                if (!Attribute.IsDefined(field, typeof(DisplayIfAttribute)))
                {
                    DrawField(node, field, labelName);
                }
                else
                {
                    try
                    {
                        if (DisplayIfAttribute.IsTrue(node, field))
                        {
                            DrawField(node, field, labelName);
                        }
                    }
                    catch (Exception)
                    {
                        EditorGUILayout.LabelField(labelName, "DisplayIf attribute breaks, ask for help now");
                    }
                }
            }
        }
    }
}