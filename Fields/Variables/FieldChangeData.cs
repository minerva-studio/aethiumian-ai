using System;

namespace Amlos.AI
{
    [Serializable]
    public class FieldChangeData : ICloneable
    {
        public string name;
        public VariableField data;

        public object Clone()
        {
            return new FieldChangeData()
            {
                name = name,
                data = data.Clone() as VariableField
            };
        }
    }

    [Serializable]
    public class FieldPointer : ICloneable
    {
        public string name;
        public VariableReference data;

        public object Clone()
        {
            return new FieldPointer()
            {
                name = name,
                data = data.Clone() as VariableReference
            };
        }
    }
}
