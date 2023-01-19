using System.Reflection;

namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(ObjectAction))]
    public class ObjectActionDrawer : MethodCallerDrawerBase
    {
        public override void Draw()
        {
            if (node is not ObjectAction action) return;

            if (!DrawObject(action, out _)) return;
            UpdateMethods();

            DrawActionData(action);
            DrawActionMethodData(action);
        }

        protected override bool IsValidMethod(MethodInfo m) => IsValidActionMethod(m);
    }
}