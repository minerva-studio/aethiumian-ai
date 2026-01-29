using Amlos.AI.Nodes;
using UnityEngine;
namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(GetObjectValue))]
    public class GetObjectValueDrawer : MethodCallerDrawerBase
    {
        public GetObjectValue Node => (GetObjectValue)node;

        public override void Draw()
        {
            UnityEditor.SerializedProperty objectProperty = FindRelativeProperty(nameof(GetObjectValue.@object));
            UnityEditor.SerializedProperty typeProperty = FindRelativeProperty(nameof(GetObjectValue.type));
            if (!DrawObject(objectProperty, typeProperty, out var objectType)) return;
            GUILayout.Space(10);
            UnityEditor.SerializedProperty entryListProperty = property.FindPropertyRelative(nameof(GetObjectValue.fieldPointers));
            DrawGetFields(Node, entryListProperty, null, objectType);
        }
    }
}
