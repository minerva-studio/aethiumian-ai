using Amlos.AI.Variables;
using System;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    [Serializable]
    [NodeTip("create a Vector3")]
    public sealed class CreateVector3 : Arithmetic
    {
        [Numeric]
        public VariableField x;
        [Numeric]
        public VariableField y;
        [Numeric]
        public VariableField z;

        public VariableReference<Vector3> vector;

        public override State Execute()
        {
            if (!vector.IsVector)
            {
                return State.Failed;
            }
            try
            {
                var vx = x.HasValue ? x.NumericValue : 0;
                var vy = y.HasValue ? y.NumericValue : 0;
                var vz = z.HasValue ? z.NumericValue : 0;

                vector.SetValue(new Vector3(vx, vy, vz));

            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return State.Failed;
            }
            return State.Failed;
        }

    }
}