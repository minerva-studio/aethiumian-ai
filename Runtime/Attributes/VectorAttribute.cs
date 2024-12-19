using System;

namespace Amlos.AI
{
    /// <summary>
    /// Attribute that set a generic variable with numeric type limit
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class VectorAttribute : ConstraintAttribute
    {
        // This is a positional argument
        public VectorAttribute() : base(VariableType.Vector3, VariableType.Vector2)
        {
        }
    }
}
