using Amlos.AI;
using Minerva.Module;
using UnityEngine;

namespace Amlos.Editor
{
    [CustomNodeDrawer(typeof(Condition))]
    public class ConditionDrawer : NodeDrawerBase
    {
        public override void Draw()
        {
            if (this.node is not Condition condition) return; 
            DrawNodeSelection("Condition", condition.condition);
            DrawNodeSelection("True", condition.trueNode);
            DrawNodeSelection("False", condition.falseNode);

        }
    }
}