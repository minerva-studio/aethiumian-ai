using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [Serializable]
    [NodeTip("create a Vector3")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class CreateVector3 : Arithmetic
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

        [Writable]
        public VariableReference<Vector3> vector;

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
                    || !TryReadVectorLane(z, zLane, out float vz))
                {
                    return State.Failed;
                }

                return vector.SetValue(new Vector3(vx, vy, vz), failOnNaN)
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
