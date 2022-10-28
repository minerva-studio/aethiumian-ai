using Amlos.AI;
using Minerva.Module;
using UnityEditor;

namespace Amlos.Editor
{

    [CustomNodeDrawer(typeof(Always))]
    public class AlwaysDrawer : NodeDrawerBase
    {
        public override void Draw()
        {
            if (this.node is not Always always) return;
            always.node ??= new NodeReference();
            DrawNodeSelection("Next", always.node);
            DrawVariable(nameof(always.returnValue).ToTitleCase(), always.returnValue);
        }
    }
}