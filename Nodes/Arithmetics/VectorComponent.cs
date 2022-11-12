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

        ///"set to 0 to get x, 1 for y, 2 for z"
        public VariableField compompent;
        public VariableReference result;
        public override void Execute()
        {
            if (!vector.IsVector || compompent.Type != VariableType.Int
            || compompent.IntValue < 0 || compompent.IntValue > 3)
            {
                End(false);
            }
            try
            {
                switch (compompent.IntValue)
                {
                    case 0:
                        result.Value = vector.Vector3Value.x;
                        break;
                    case 1:
                        result.Value = vector.Vector3Value.y;
                        break;
                    case 2:
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