namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(Sequence))]
    public class SequenceDrawer : NodeDrawerBase
    {
        public override void Draw()
        {
            if (node is not Sequence sequence) return;
            DrawNodeList(nameof(Sequence), sequence.events, sequence);
        }
    }
}
