using Amlos.AI.Nodes;
using System.Reflection;
using UnityEditor;

namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(CallStatic))]
    public class CallStaticDrawer : MethodCallerDrawerBase
    {
        protected override BindingFlags Binding => STATIC_MEMBER;

        public override void Draw()
        {
            if (node is not CallStatic)
                return;

            SerializedProperty typeReferenceProperty = property.FindPropertyRelative(nameof(CallStatic.type));
            if (!DrawReferType(typeReferenceProperty, STATIC_MEMBER))
                return;

            SerializedProperty methodNameProperty = property.FindPropertyRelative(nameof(CallStatic.methodName));
            var method = SelectMethod(methodNameProperty);
            if (method is null)
            {
                EditorGUILayout.LabelField("Cannot load method info");
                return;
            }

            DrawParameters(method);
            SerializedProperty resultProperty = property.FindPropertyRelative(nameof(CallStatic.result));
            DrawResultField(resultProperty, method);
        }
    }
}
