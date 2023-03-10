using Amlos.AI.Nodes;
using System.Reflection;

namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(ComponentAction))]
    public class ComponentActionDrawer : MethodCallerDrawerBase
    {
        public override void Draw()
        {
            if (node is not ComponentAction action)
                return;

            if (!DrawComponent(action))
                return;
            if (!DrawReferType(action, INSTANCE_MEMBER))
                return;

            DrawActionData(action);
            DrawActionMethodData(action);
        }

        protected override bool IsValidMethod(MethodInfo m) => IsValidActionMethod(m);
    }
}
