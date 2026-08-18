using Aethiumian.AI.References;
using System;
using UnityEngine;
using static Aethiumian.AI.Variables.VariableUtility;

namespace Aethiumian.AI.Variables
{
    /// <summary>
    /// Base class for fields that can either contain a constant value or reference a tree variable.
    /// </summary>
    [Serializable]
    public abstract class VariableFieldBase : VariableBase
    {
        public override void SetValue<T>(T newValue)
        {
            if (IsConstant) throw new InvalidOperationException("Cannot set value to constant.");
            Variable.SetValue(newValue);
        }

    }
}
