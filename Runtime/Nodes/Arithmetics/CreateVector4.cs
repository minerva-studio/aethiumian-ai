using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [Serializable]
    [NodeTip("create a Vector4")]
    public sealed class CreateVector4 : Arithmetic
    {
        [NumericOrVector]
        [Readable]
        public VariableField x;
        public VectorLane xLane;

        [NumericOrVector]
        [Readable]
        public VariableField y;
        public VectorLane yLane;

        [NumericOrVector]
        [Readable]
        public VariableField z;
        public VectorLane zLane;

        [NumericOrVector]
        [Readable]
        public VariableField w;
        public VectorLane wLane;

        [Writable]
        public VariableReference<Vector4> vector;

        public override State Execute()
        {
            if (vector == null || !vector.IsVector)
            {
                return State.Failed;
            }

            try
            {
                if (!TryReadVectorLane(x, xLane, out float vx)
                    || !TryReadVectorLane(y, yLane, out float vy)
                    || !TryReadVectorLane(z, zLane, out float vz)
                    || !TryReadVectorLane(w, wLane, out float vw))
                {
                    return State.Failed;
                }

                return vector.SetValue(new Vector4(vx, vy, vz, vw), failOnNaN)
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
