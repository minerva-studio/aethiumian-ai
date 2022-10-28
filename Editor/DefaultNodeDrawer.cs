using Amlos.Core;
using Minerva.Module;
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Amlos.Editor
{

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
                        if (IsDisplayIfTrue(type, field))
                        {
                            DrawField(node, field, labelName);
                        }
                    }
                    catch (Exception)
                    {
                        EditorGUILayout.LabelField(labelName, "Display when attribute breaks, ask for help now");
                    }
                }
            }
        }

        public bool IsDisplayIfTrue(Type type, FieldInfo field)
        {
            if (Attribute.IsDefined(field, typeof(DisplayIfAttribute)))
            {
                var attrs = (DisplayIfAttribute[])Attribute.GetCustomAttributes(field, typeof(DisplayIfAttribute));
                foreach (var attr in attrs)
                {
                    string dependent = attr.name;
                    if (!attr.EqualsAny(type.GetField(dependent).GetValue(node)))
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }
}