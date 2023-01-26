using Amlos.AI.Variables;
using System;
using UnityEngine;

namespace Amlos.AI
{
    [DoNotRelease]
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


        public override void Execute()
        {
            if (!vector.IsVector)
            {
                End(false);
                return;
            }
            Vector3 vector3 = vector.VectorValue;
            foreach (Element item in Enum.GetValues(typeof(Element)))
            {
                switch (item & setTo)
                {
                    case Element.x:
                        if (!x.IsNumeric)
                        {
                            End(false);
                            return;
                        }
                        vector3.x = x.NumericValue;
                        break;
                    case Element.y:
                        if (!y.IsNumeric)
                        {
                            End(false);
                            return;
                        }
                        vector3.y = y.NumericValue;
                        break;
                    case Element.z:
                        if (!z.IsNumeric)
                        {
                            End(false);
                            return;
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
        }
    }
}
