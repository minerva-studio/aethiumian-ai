using System;

using Amlos.AI.Accessors;

namespace Amlos.AI.Variables
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
                data = global::Amlos.AI.Accessors.Duplicate.Value(data)
            };
        }
    }
}
