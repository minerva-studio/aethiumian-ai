using System;
using UnityEngine;

namespace Amlos.AI
{
    /// <summary>
    /// author: Kevin Zhou
    /// </summary>
    [Serializable]
    [NodeTip("get a single component of a vector")]
    public class VectorComponent : Arithmetic
    {
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
        public override void Execute()
        {
            if (!vector.IsVector)
            {
                End(false);
            }
            try
            {
                if (x.HasRuntimeReference)
                {
                    x.Value = vector.Vector3Value.x;
                }
                if (y.HasRuntimeReference)
                {
                    y.Value = vector.Vector3Value.x;
                }
                if (z.HasRuntimeReference)
                {
                    z.Value = vector.Vector3Value.x;
                }
            }
            catch (Exception)
            {
                End(false);
                throw;
            }
        }

    }
}