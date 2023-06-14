using Amlos.AI.Nodes;
using Amlos.AI.References;
using UnityEditor;

namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(Condition))]
    public class ConditionDrawer : NodeDrawerBase
    {
        public override void Draw()
        {
            if (node is not Condition condition) return;
            DrawNodeReference("Condition: ", condition.condition);
            DrawNodeReference("True: ", condition.trueNode);
            DrawNodeReference("False: ", condition.falseNode);

            NodeMustNotBeNull(condition.condition, nameof(condition));
        }
    }
}