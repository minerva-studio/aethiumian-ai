using Amlos.AI.Variables;
using System;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    [Serializable]
    public sealed class SetVector : Arithmetic
    {
        [Flags]
        public enum Element
        {
            x = 1,
            y = 2,
            z = 4
        }

        public VariableReference vector;
        public Element setTo;
        public VariableField x;
        public VariableField y;
        public VariableField z;


        public override State Execute()
        {
            if (!vector.IsVector)
            {
                return State.Failed;
            }
            Vector3 vector3 = vector.VectorValue;
            foreach (Element item in Enum.GetValues(typeof(Element)))
            {
                switch (item & setTo)
                {
                    case Element.x:
                        if (!x.IsNumericLike)
                        {
                            return State.Failed;
                        }
                        vector3.x = x.NumericValue;
                        break;
                    case Element.y:
                        if (!y.IsNumericLike)
                        {
                            return State.Failed;
                        }
                        vector3.y = y.NumericValue;
                        break;
                    case Element.z:
                        if (!z.IsNumericLike)
                        {
                            return State.Failed;
                        }
                        vector3.z = z.NumericValue;
                        break;
                    default:
                        break;
                }
            }
            if (vector.Type == VariableType.Vector2)
                vector.Value = (Vector2)vector3;
            if (vector.Type == VariableType.Vector3)
                vector.Value = vector3;

            return State.Success;
        }
    }
}
