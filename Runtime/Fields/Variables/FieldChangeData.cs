using System;

namespace Amlos.AI.Variables
{
    [Serializable]
    public class FieldChangeData : ICloneable
    {
        public string name;
        public Parameter data;

        public object Clone()
        {
            return new FieldChangeData()
            {
                name = name,
                data = data.Clone() as Parameter
            };
        }
    }
}
