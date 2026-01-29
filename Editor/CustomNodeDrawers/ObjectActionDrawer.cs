using Amlos.AI.Nodes;
using System.Reflection;

namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(ObjectAction))]
    public class ObjectActionDrawer : MethodCallerDrawerBase
    {
        public override void Draw()
        {
            UnityEditor.SerializedProperty objectProperty = FindRelativeProperty(nameof(ObjectAction.@object));
            UnityEditor.SerializedProperty typeProperty = FindRelativeProperty(nameof(ObjectAction.type));
            if (!DrawObject(objectProperty, typeProperty, out var objectType)) return;
            UpdateMethods();

            DrawActionData();
            DrawActionMethodData();
        }

        protected override bool IsValidMethod(MethodInfo m) => IsValidActionMethod(m);
    }
}
