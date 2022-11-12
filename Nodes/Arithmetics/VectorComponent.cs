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

        public enum Component
        {
            X,
            Y,
            Z
        }

        public Component componentToGet;

        public VariableReference result;
        public override void Execute()
        {
            if (!vector.IsVector)
            {
                End(false);
            }
            try
            {
                switch (componentToGet)
                {
                    case Component.X:
                        result.Value = vector.Vector3Value.x;
                        break;
                    case Component.Y:
                        result.Value = vector.Vector3Value.y;
                        break;
                    case Component.Z:
                        result.Value = vector.Vector3Value.z;
                        break;
                }
                End(true);

            }
            catch (System.Exception)
            {
                End(false);
                throw;
            }


        }

    }
}