using System;

using Aethiumian.AI.Accessors;

namespace Aethiumian.AI.Variables
{
    [Serializable]
    public class FieldChangeData : ICloneable, IDuplicable
    {
        public string name;
        public Parameter data;

        public object Clone()
        {
            return Duplicate();
        }

        public object Duplicate()
        {
            return new FieldChangeData()
            {
                name = name,
                data = global::Aethiumian.AI.Accessors.Duplicate.Value(data)
            };
        }
    }
}
