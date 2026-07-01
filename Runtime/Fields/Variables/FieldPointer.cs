using System;

using Aethiumian.AI.Accessors;

namespace Aethiumian.AI.Variables
{
    [Serializable]
    public class FieldPointer : ICloneable, IDuplicable, IVariableField
    {
        public string name;
        public VariableReference data;

        public VariableType Type => data?.Type ?? VariableType.Invalid;

        public UUID UUID => data?.UUID ?? UUID.Empty;

        public bool IsConstant => data?.IsConstant ?? true;

        public Variable Variable => data?.Variable;

        public object Value => data?.Value;

        public void SetReference(VariableData variable)
        {
            data ??= new VariableReference();
            data.SetReference(variable);
        }

        public void SetRuntimeReference(Variable variable)
        {
            if (variable == null)
            {
                return;
            }

            data ??= new VariableReference();
            data.SetRuntimeReference(variable);
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
