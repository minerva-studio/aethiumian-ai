using Aethiumian.AI.Variables;
using System;

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
        [Constraint(VariableType.Vector2, VariableType.Vector3)]
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
                    x.SetValue(vector.Vector3Value.x);
                }
                if (y.HasReference)
                {
                    y.SetValue(vector.Vector3Value.x);
                }
                if (z.HasReference)
                {
                    z.SetValue(vector.Vector3Value.x);
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
