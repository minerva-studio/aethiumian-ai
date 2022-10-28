using Amlos.AI;
using System.Linq;

namespace Amlos.Editor
{
    [CustomNodeDrawer(typeof(Sequence))]
    public class SequenceDrawer : NodeDrawerBase
    {
        public override void Draw()
        {
            if (this.node is not Sequence sequence) return;
            DrawNodeList(nameof(Sequence), sequence.events, sequence);
        }
    }
}