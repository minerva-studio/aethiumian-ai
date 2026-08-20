using System;

using Aethiumian.AI.Accessors;

namespace Aethiumian.AI.Variables
{
    [Serializable]
    public class FieldChangeData : ICloneable, IDuplicable, IVariableBinding
    {
        public string name;
        public Parameter data;

        public VariableType Type => data?.Type ?? VariableType.Invalid;

        public UUID UUID => data?.UUID ?? UUID.Empty;

        public bool IsConstant => data?.IsConstant ?? true;

        public RuntimeVariable RuntimeVariable => data?.RuntimeVariable;

        public object Value => data?.Value;

        public void SetReference(VariableData variable)
        {
            data ??= new Parameter(variable?.Type ?? VariableType.Invalid);
            data.SetReference(variable);
        }

        public void SetRuntimeReference(RuntimeVariable variable)
        {
            if (variable != null) data ??= new Parameter(variable.Type);
            if (data != null) data.SetRuntimeReference(variable);
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
