namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(Inverter))]
    public class InverterDrawer : NodeDrawerBase
    {
        public override void Draw()
        {
            var inverter = node as Inverter;
            DrawNodeSelection("Next", inverter.node);
        }
    }
}