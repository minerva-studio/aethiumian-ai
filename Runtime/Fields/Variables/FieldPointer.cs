using System;

using Aethiumian.AI.Accessors;

namespace Aethiumian.AI.Variables
{
    [Serializable]
    public class FieldPointer : ICloneable, IDuplicable, IVariableBinding
    {
        public string name;
        public VariableReference data;

        public UUID UUID => data?.UUID ?? UUID.Empty;

        public RuntimeVariable RuntimeVariable => data?.RuntimeVariable;

        public void SetReference(VariableData variable)
        {
            data ??= new VariableReference();
            data.SetReference(variable);
        }

        public void SetRuntimeReference(RuntimeVariable variable)
        {
            if (variable != null) data ??= new VariableReference();
            data?.SetRuntimeReference(variable);
        }

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
