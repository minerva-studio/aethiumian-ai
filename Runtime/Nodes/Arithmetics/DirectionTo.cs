using Aethiumian.AI.Attributes;
using Aethiumian.AI.Variables;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Calculates the direction from one position to another.")]
    [System.Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public class DirectionTo : Arithmetic
    {
        public bool overrideCenter;

        [DisplayIf(nameof(overrideCenter))]
        [Constraint(VariableType.Vector2, VariableType.Vector3, VariableType.UnityObject)]
        [Readable]
        public VariableReference center;

        [Readable]
        public VariableReference target;

        [Constraint(VariableType.Vector2, VariableType.Vector3)]
        [Writable]
        public VariableReference result;

        public override State Execute()
        {
            if (!target.HasValue)
            {
                return HandleException(InvalidNodeException.VariableIsRequired(nameof(target), this));
            }

            if (overrideCenter && !center.HasValue)
            {
                return HandleException(InvalidNodeException.VariableIsRequired(nameof(center), this));
            }

            if (target.IsNull || (overrideCenter && center.IsNull))
            {
                return State.Failed;
            }

            Vector3 position = target.PositionValue;
            Vector3 source = overrideCenter ? center.PositionValue : transform.position;

            if (HasNaN(position) || HasNaN(source))
            {
                return State.Failed;
            }

            var displacement = position - source;
            Vector3 value = displacement.normalized;
            return result.SetValue(value, failOnNaN) ? State.Success : State.Failed;

        }
    }
}
