using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [Serializable]
    [NodeTip("create a Vector2")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class CreateVector2 : Arithmetic
    {
        [NumericOrVector]
        [Readable]
        public VariableField x;
        public VectorLane xLane;

        [NumericOrVector]
        [Readable]
        public VariableField y;
        public VectorLane yLane;

        [Writable]
        public VariableReference<Vector2> vector;

        public override State Execute()
        {
            if (vector == null || !vector.IsVector)
            {
                return State.Failed;
            }

            try
            {
                if (!TryReadVectorLane(x, xLane, out float vx)
                    || !TryReadVectorLane(y, yLane, out float vy))
                {
                    return State.Failed;
                }

                return vector.SetValue(new Vector2(vx, vy), failOnNaN)
                    ? State.Success
                    : State.Failed;
            }
            catch (Exception e)
            {
                return HandleException(e);
            }
        }
    }
}
