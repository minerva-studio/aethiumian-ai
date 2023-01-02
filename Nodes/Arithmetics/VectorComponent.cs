using System;

namespace Amlos.AI
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

        public override void Execute()
        {
            if (!vector.IsVector)
            {
                End(false);
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
            catch (Exception)
            {
                End(false);
                throw;
            }
        }
    }
}
