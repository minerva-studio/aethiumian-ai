using System.Linq;
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
            if (node is not CallStatic call) return;
            if (!DrawReferType(call, STATIC_MEMBER)) return;

            call.MethodName = SelectMethod(call.MethodName);
            var method = methods.FirstOrDefault(m => m.Name == call.MethodName);
            if (method is null)
            {
                EditorGUILayout.LabelField("Cannot load method info");
                return;
            }

            DrawParameters(call, method);
            DrawResultField(call.result, method);
        }
    }
}