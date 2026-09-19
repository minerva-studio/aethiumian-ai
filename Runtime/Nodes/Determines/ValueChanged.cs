using Aethiumian.AI.Attributes;
using Aethiumian.AI.Variables;
using System;

namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// Returns true once when the observed value differs from the previous observation.
    /// The first observation establishes the baseline and returns false.
    /// </summary>
    [NodeTip("Returns true once when a value changes.")]
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class ValueChanged : Determine
    {
        /// <summary>The runtime variable reference whose current value is compared with the previous observation.</summary>
        [Readable]
        public VariableReference value;

        [NonSerialized] private bool hasPreviousValue;
        [NonSerialized] private VariableValueSnapshot previousValue;

        public override void Initialize()
        {
            hasPreviousValue = false;
            previousValue = default;
        }

        public override Exception IsValidNode()
        {
            if (value == null || !value.HasValue)
            {
                return InvalidNodeException.VariableIsRequired(nameof(value), this);
            }

            return null;
        }

        public override bool GetValue()
        {
            VariableValueSnapshot currentValue = VariableValueSnapshot.Capture(value);
            if (!hasPreviousValue)
            {
                previousValue = currentValue;
                hasPreviousValue = true;
                return false;
            }

            bool changed = !previousValue.Equals(currentValue);
            previousValue = currentValue;
            return changed;
        }

    }
}
