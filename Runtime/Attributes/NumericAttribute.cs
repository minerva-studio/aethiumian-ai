using System;

namespace Aethiumian.AI
{
    /// <summary>
    /// Attribute that set a generic variable with numeric type limit
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class NumericAttribute : ConstraintAttribute
    {
        // This is a positional argument
        public NumericAttribute() : base(VariableType.Int, VariableType.Float)
        {
        }
    }
}
