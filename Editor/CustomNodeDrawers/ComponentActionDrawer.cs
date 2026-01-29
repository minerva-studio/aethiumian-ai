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

            if (!DrawComponent())
                return;
            if (!DrawReferType(INSTANCE_MEMBER))
                return;

            DrawActionData();
            DrawActionMethodData();
        }

        protected override bool IsValidMethod(MethodInfo m) => IsValidActionMethod(m);
    }
}
