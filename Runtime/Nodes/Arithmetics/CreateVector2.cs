using Amlos.AI.Variables;
using System;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    [Serializable]
    [NodeTip("create a Vector2")]
    public sealed class CreateVector2 : Arithmetic
    {
        [Numeric]
        [Readable]
        public VariableField x;
        [Numeric]
        [Readable]
        public VariableField y;

        [Writable]
        public VariableReference<Vector2> vector;

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

                vector.SetValue(new Vector2(vx, vy));
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