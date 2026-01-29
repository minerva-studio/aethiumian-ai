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
            if (node is not CallStatic call)
                return;
            if (!DrawReferType(STATIC_MEMBER))
                return;

            var method = SelectMethod(property.FindPropertyRelative(nameof(CallStatic.methodName)));
            if (method is null)
            {
                EditorGUILayout.LabelField("Cannot load method info");
                return;
            }

            DrawParameters(method);
            DrawResultField(property.FindPropertyRelative(nameof(CallStatic.result)), method);
        }
    }
}
