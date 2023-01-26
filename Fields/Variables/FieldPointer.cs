using System;

namespace Amlos.AI.Variables
{
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
