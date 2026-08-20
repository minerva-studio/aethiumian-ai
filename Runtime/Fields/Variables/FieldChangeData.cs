using System;

using Aethiumian.AI.Accessors;

namespace Aethiumian.AI.Variables
{
    [Serializable]
    public class FieldChangeData : ICloneable, IDuplicable, IVariableBinding
    {
        public string name;
        public Parameter data;

        public UUID UUID => data?.UUID ?? UUID.Empty;

        public RuntimeVariable RuntimeVariable => data?.RuntimeVariable;

        public void SetReference(VariableData variable)
        {
            data ??= new Parameter(variable?.Type ?? VariableType.Invalid);
            data.SetReference(variable);
        }

        public void SetRuntimeReference(RuntimeVariable variable)
        {
            if (variable != null) data ??= new Parameter(variable.Type);
            data?.SetRuntimeReference(variable);
        }

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
