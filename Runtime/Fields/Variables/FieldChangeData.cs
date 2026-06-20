using System;

using Amlos.AI.Accessors;

namespace Amlos.AI.Variables
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
                data = global::Amlos.AI.Accessors.Duplicate.Value(data)
            };
        }
    }
}
