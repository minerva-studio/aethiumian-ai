using Amlos.AI.Nodes;

namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(ComponentCall))]
    public class ComponentCallDrawer : MethodCallerDrawerBase
    {
        public override void Draw()
        {
            if (!DrawComponent())
                return;
            var typeReferenceProperty = property.FindPropertyRelative(nameof(ComponentCall.type));
            if (!DrawReferType(typeReferenceProperty, INSTANCE_MEMBER))
                return;

            DrawCallMethodData();
        }
    }
}
