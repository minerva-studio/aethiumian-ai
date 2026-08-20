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
        [Numeric]
        [Readable]
        public VariableField x;
        [Numeric]
        [Readable]
        public VariableField y;
        [Numeric]
        [Readable]
        public VariableField z;

        [Writable]
        public VariableReference<Vector3> vector;

        public override State Execute()
        {
            if (!vector.IsVector)
            {
                return State.Failed;
            }
            try
            {
                var vx = x.HasValue ? x.FloatValue : 0;
                var vy = y.HasValue ? y.FloatValue : 0;
                var vz = z.HasValue ? z.FloatValue : 0;

                vector.SetValue(new Vector3(vx, vy, vz));

            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return State.Failed;
            }
            return State.Success;
        }

    }
}
