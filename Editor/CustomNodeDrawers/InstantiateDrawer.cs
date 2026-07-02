using Aethiumian.AI.Nodes;

namespace Aethiumian.AI.Editor
{
    // Kept as disabled legacy drawer documentation. Instantiate is drawn by DefaultNodeDrawer.
    public class InstantiateDrawer : NodeDrawerBase
    {
        public override void Draw()
        {
            if (node is not Instantiate instantiate) return;

            DrawProperty(property.FindPropertyRelative(nameof(instantiate.original)));
            DrawProperty(property.FindPropertyRelative(nameof(instantiate.parentOfObject)));
            DrawProperty(property.FindPropertyRelative(nameof(instantiate.offsetMode)));
            DrawProperty(property.FindPropertyRelative(nameof(instantiate.offset)));
            DrawProperty(property.FindPropertyRelative(nameof(instantiate.result)));
        }
    }
}
