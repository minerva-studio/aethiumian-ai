using Amlos.AI.Nodes;
namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(Inverter))]
    public class InverterDrawer : NodeDrawerBase
    {
        public override void Draw()
        {
            var inverter = node as Inverter;
            DrawNodeReference("Next", inverter.node);
        }
    }
}