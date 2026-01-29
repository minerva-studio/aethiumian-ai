using Amlos.AI.Nodes;
using Amlos.AI.Variables;
using UnityEngine;

namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(SetObjectValue))]
    public class SetObjectValueDrawer : MethodCallerDrawerBase
    {
        public SetObjectValue Node => (SetObjectValue)node;

        public override void Draw()
        {
            UnityEditor.SerializedProperty objectProperty = FindRelativeProperty(nameof(ObjectAction.@object));
            UnityEditor.SerializedProperty typeProperty = FindRelativeProperty(nameof(ObjectAction.type));
            if (!DrawObject(objectProperty, typeProperty, out var objectType, VariableAccessFlag.Write)) return;
            GUILayout.Space(10);
            UnityEditor.SerializedProperty entryListProperty = property.FindPropertyRelative(nameof(SetObjectValue.fieldData));
            DrawSetFields(entryListProperty, null, objectType);
        }
    }
}
