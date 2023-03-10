using Amlos.AI.Variables;
using System;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// author: Kevin Zhou
    /// </summary>
    [Serializable]
    [NodeTip("get a single component of a vector")]
    public sealed class VectorComponent : Arithmetic
    {
        [TypeLimit(VariableType.Vector2, VariableType.Vector3)]
        public VariableField vector;

        //public enum Component
        //{
        //    X = 1,
        //    Y = 2,
        //    Z = 4,
        //}

        //public Component componentToGet;

        public VariableReference x;
        public VariableReference y;
        public VariableReference z;

        public override State Execute()
        {
            if (!vector.IsVector)
            {
                return State.Failed;
            }
            try
            {
                if (x.HasReference)
                {
                    x.Value = vector.Vector3Value.x;
                }
                if (y.HasReference)
                {
                    y.Value = vector.Vector3Value.x;
                }
                if (z.HasReference)
                {
                    z.Value = vector.Vector3Value.x;
                }
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
