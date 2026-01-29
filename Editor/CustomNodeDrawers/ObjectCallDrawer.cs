using Amlos.AI.Nodes;
using UnityEditor;
namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(ObjectCall))]
    public class ObjectCallDrawer : MethodCallerDrawerBase
    {
        public override void Draw()
        {
            SerializedProperty objectProperty = FindRelativeProperty(nameof(ObjectCall.@object));
            SerializedProperty typeProperty = FindRelativeProperty(nameof(ObjectCall.type));
            if (!DrawObject(objectProperty, typeProperty, out _)) return;

            UpdateMethods();
            DrawCallMethodData();
        }
    }
}
