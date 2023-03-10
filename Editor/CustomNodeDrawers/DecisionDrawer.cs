using Amlos.AI.Nodes;
namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(Decision))]
    public class DecisionDrawer : NodeDrawerBase
    {
        public override void Draw()
        {
            if (node is not Decision decision) return;
            //DrawNodeList(nameof(Decision), decision.eventUUIDs, decision); 
            DrawNodeList(nameof(Decision), decision.events, decision);
        }
    }
}