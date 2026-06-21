using System;

using Aethiumian.AI.Accessors;

namespace Aethiumian.AI.Variables
{
    [Serializable]
    public class FieldPointer : ICloneable, IDuplicable
    {
        public string name;
        public VariableReference data;

        public object Clone()
        {
            return Duplicate();
        }

        public object Duplicate()
        {
            return new FieldPointer()
            {
                name = name,
                data = global::Aethiumian.AI.Accessors.Duplicate.Value(data)
            };
        }
    }
}
