using System;
using Amlos.AI.Variables;
using Minerva.Module;
using UnityEngine;

namespace Amlos.AI.Nodes
{
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

            if (target.IsNull)
            {
                return State.Failed;
            }

            Vector3 position;
            try
            {
                position = target.PositionValue;
            }
            catch (InvalidOperationException e)
            {
                return HandleException(InvalidNodeException.InvalidValue(e.Message, this));
            }
            Vector3 source = overrideCenter ? center.PositionValue : transform.position;

            var displacement = position - source;
            result.SetValue(displacement.normalized);

            return State.Success;
        }
    }
}