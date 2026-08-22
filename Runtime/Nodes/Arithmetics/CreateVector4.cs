using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    public sealed class CreateVector4 : Arithmetic
    {
        [Numeric]
        [Readable]
        public VariableField x;
        [Numeric]
        [Readable]
        public VariableField y;
        [Numeric]
        [Readable]
        public VariableField z;
        [Numeric]
        [Readable]
        public VariableField w;

        [Writable]
        public VariableReference<Vector4> vector;

        public override State Execute()
        {
            if (!vector.IsVector)
            {
                return State.Failed;
            }
            try
            {
                if (HasNaN(x) || HasNaN(y) || HasNaN(z) || HasNaN(w))
                {
                    return State.Failed;
                }

                var vx = x.HasValue ? x.FloatValue : 0;
                var vy = y.HasValue ? y.FloatValue : 0;
                var vz = z.HasValue ? z.FloatValue : 0;
                var vw = w.HasValue ? w.FloatValue : 0;

                Vector4 value = new(vx, vy, vz, vw);
                return vector.SetValue(value, failOnNaN) ? State.Success : State.Failed;

            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return State.Failed;
            }
        }
    }
}
