using Amlos.AI.Nodes;
using System.Reflection;

namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(ComponentAction))]
    public class ComponentActionDrawer : MethodCallerDrawerBase
    {
        protected override BindingFlags Binding => INSTANCE_MEMBER;

        public override void Draw()
        {
            if (node is not ComponentAction action)
                return;

            if (!DrawComponent())
                return;

            var typeReferenceProperty = property.FindPropertyRelative(nameof(ComponentAction.type));
            if (!DrawReferType(typeReferenceProperty, INSTANCE_MEMBER))
                return;

            DrawActionData();
            DrawActionMethodData();
        }

        protected override bool IsValidMethod(MethodInfo m) => IsValidActionMethod(m);
    }
}
