﻿using System;

namespace Amlos.AI
{
    /// <summary>
    /// Attribute that set a generic variable with numeric type limit
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class NumericOrVectorAttribute : ConstraintAttribute
    {
        // This is a positional argument
        public NumericOrVectorAttribute() : base(VariableType.Int, VariableType.Float, VariableType.Vector2, VariableType.Vector3)
        {
        }
    }
}
