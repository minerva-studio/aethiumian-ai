﻿using Amlos.AI.Variables;
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
            Vector3 position = target.PositionValue;
            Vector3 source = overrideCenter ? center.PositionValue : transform.position;

            var displacement = position - source;
            result.SetValue(displacement.normalized);
            return State.Success;
        }
    }
}