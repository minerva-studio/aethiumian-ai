using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Constructs a vector from its components and writes the result.")]
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class SetVector : Arithmetic
    {
        [Flags]
        public enum Element
        {
            x = 1,
            y = 2,
            z = 4,
            w = 8,
        }

        [Writable]
        public VariableReference vector;
        public Element setTo;
        [Readable]
        public VariableField x;
        [Readable]
        public VariableField y;
        [Readable]
        public VariableField z;
        [Readable]
        public VariableField w;


        public override State Execute()
        {
            if (!vector.IsVector)
            {
                return State.Failed;
            }
            Vector4 vector4 = vector.Type switch
            {
                VariableType.Vector2 => vector.Vector2Value,
                VariableType.Vector3 => vector.Vector3Value,
                VariableType.Vector4 => vector.Vector4Value,
                _ => default,
            };

            if (vector.Type != VariableType.Vector2 && vector.Type != VariableType.Vector3 && vector.Type != VariableType.Vector4)
            {
                return State.Failed;
            }

            if ((setTo & Element.x) != 0)
            {
                if (!ArithmeticCompatibility.IsScalar(x.Type))
                {
                    return State.Failed;
                }
                vector4.x = x.FloatValue;
            }
            if ((setTo & Element.y) != 0)
            {
                if (!ArithmeticCompatibility.IsScalar(y.Type))
                {
                    return State.Failed;
                }
                vector4.y = y.FloatValue;
            }
            if ((setTo & Element.z) != 0)
            {
                if (!ArithmeticCompatibility.IsScalar(z.Type))
                {
                    return State.Failed;
                }
                vector4.z = z.FloatValue;
            }
            if ((setTo & Element.w) != 0)
            {
                if (vector.Type != VariableType.Vector4 || !ArithmeticCompatibility.IsScalar(w.Type))
                {
                    return State.Failed;
                }
                vector4.w = w.FloatValue;
            }

            switch (vector.Type)
            {
                case VariableType.Vector2:
                    vector.SetValue((Vector2)vector4);
                    break;
                case VariableType.Vector3:
                    vector.SetValue((Vector3)vector4);
                    break;
                case VariableType.Vector4:
                    vector.SetValue(vector4);
                    break;
            }

            return State.Success;
        }
    }
}
