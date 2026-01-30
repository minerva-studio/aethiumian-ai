using Amlos.AI.Nodes;
using Minerva.Module;
using Minerva.Module.Editor;
using System;
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
            var property = base.property.Copy();
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
                if (!draw) try { draw = ConditionalFieldAttribute.IsTrue(node, field); }
                    catch (Exception)
                    {
                        var name = new GUIContent(property.displayName);
                        var content = new GUIContent("Internal Error", "DisplayIf attribute breaks, ask for help now");
                        EditorGUILayout.LabelField(name, content);
                        continue;
                    }
                if (!draw) continue;

                DrawProperty(property.Copy());
            }
        }
    }
}
