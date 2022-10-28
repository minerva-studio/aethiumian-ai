using Amlos.AI;
using System.Linq;

namespace Amlos.Editor
{
    [CustomNodeDrawer(typeof(Decision))]
    public class DecisionDrawer : NodeDrawerBase
    {
        public override void Draw()
        {
            if (this.node is not Decision decision) return;
            //DrawNodeList(nameof(Decision), decision.eventUUIDs, decision); 
            DrawNodeList(nameof(Decision), decision.events, decision);
        }
    }
}