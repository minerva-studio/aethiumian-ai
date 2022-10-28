using Minerva.Module;

namespace Amlos.AI.Editor
{

    [CustomNodeDrawer(typeof(Always))]
    public class AlwaysDrawer : NodeDrawerBase
    {
        public override void Draw()
        {
            if (node is not Always always) return;
            always.node ??= new NodeReference();
            DrawNodeSelection("Next", always.node);
            DrawVariable(nameof(always.returnValue).ToTitleCase(), always.returnValue);
        }
    }
}