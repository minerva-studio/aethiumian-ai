using Amlos.AI.Nodes;
using Amlos.AI.References;
using Minerva.Module;

namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(Always))]
    public class AlwaysDrawer : NodeDrawerBase
    {
        public override void Draw()
        {
            if (node is not Always always)
                return;
            always.node ??= new NodeReference();
            DrawNodeReference("Next", always.node);
            DrawVariable(nameof(always.returnValue).ToTitleCase(), always.returnValue);
        }
    }
}
