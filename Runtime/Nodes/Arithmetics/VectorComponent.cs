using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// author: Kevin Zhou
    /// </summary>
    [Serializable]
    [NodeTip("get a single component of a vector")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class VectorComponent : Arithmetic
    {
        [Readable]
        [Constraint(VariableType.Vector2, VariableType.Vector3, VariableType.Vector4)]
        public VariableField vector;

        //public enum Component
        //{
        //    X = 1,
        //    Y = 2,
        //    Z = 4,
        //}

        //public Component componentToGet;
        [Writable]
        public VariableReference x;
        [Writable]
        public VariableReference y;
        [Writable]
        public VariableReference z;
        [Writable]
        public VariableReference w;

        public override State Execute()
        {
            if (!vector.IsVector)
            {
                return State.Failed;
            }
            try
            {
                Vector4 value = vector.Type switch
                {
                    VariableType.Vector2 => vector.Vector2Value,
                    VariableType.Vector3 => vector.Vector3Value,
                    VariableType.Vector4 => vector.Vector4Value,
                    _ => default,
                };

                if (x.HasReference)
                {
                    x.SetValue(value.x);
                }
                if (y.HasReference)
                {
                    y.SetValue(value.y);
                }
                if (z.HasReference)
                {
                    z.SetValue(value.z);
                }
                if (w.HasReference)
                {
                    w.SetValue(value.w);
                }
            }
            catch (Exception e)
            {
                return HandleException(e);
            }

            return State.Success;
        }
    }
}
