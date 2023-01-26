using Amlos.AI.Variables;
using System;
using UnityEngine;

namespace Amlos.AI
{
    [Serializable]
    [NodeTip("create a Vector2")]
    public sealed class CreateVector2 : Arithmetic
    {
        [NumericTypeLimit]
        public VariableField x;
        [NumericTypeLimit]
        public VariableField y;

        public VariableReference<Vector2> vector;

        public override void Execute()
        {
            if (!vector.IsVector)
            {
                End(false);
            }
            try
            {
                var vx = x.HasValue ? x.NumericValue : 0;
                var vy = y.HasValue ? y.NumericValue : 0;

                vector.Value = new Vector2(vx, vy);

            }
            catch (Exception)
            {
                End(false);
                throw;
            }
        }

    }
}