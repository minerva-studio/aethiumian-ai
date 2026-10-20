using Aethiumian.AI.Attributes;
using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// Measures target-minus-self displacement in world XY and requires every enabled axis comparison to pass.
    /// A valid target with no comparisons succeeds. Null or destroyed targets fail without writing outputs.
    /// An unbound target follows the tree's node exception rule.
    /// </summary>
    [Serializable]
    [NodeTip("Checks X/Y displacement to a target and optionally stores the original signed Vector2.")]
    public sealed class DisplacementTo : DetermineBase
    {
        /// <summary>The target whose Transform position is measured relative to this entity.</summary>
        [Readable] public VariableReference<GameObject> @object = new();

        /// <summary>Enables the X comparison; disabled axes do not read their thresholds.</summary>
        public bool compareX;
        /// <summary>Uses absolute X only for comparison; the stored displacement remains signed.</summary>
        [DisplayIf(nameof(compareX)), Tooltip("Compare absolute X; the stored vector remains signed.")]
        public bool absoluteX;
        /// <summary>The numeric operator applied to the selected X value and its threshold.</summary>
        [DisplayIf(nameof(compareX))] public CompareSign xMode = CompareSign.lessOrEquals;
        /// <summary>The constant or variable threshold sampled for the enabled X comparison.</summary>
        [DisplayIf(nameof(compareX)), Readable] public VariableField<float> xExpect = 0f;

        /// <summary>Enables the Y comparison, combined with the enabled X comparison using AND.</summary>
        public bool compareY;
        /// <summary>Uses absolute Y only for comparison; leave disabled when vertical direction matters.</summary>
        [DisplayIf(nameof(compareY)), Tooltip("Compare absolute Y; leave disabled when vertical direction matters.")]
        public bool absoluteY;
        /// <summary>The numeric operator applied to the selected Y value and its threshold.</summary>
        [DisplayIf(nameof(compareY))] public CompareSign yMode = CompareSign.lessOrEquals;
        /// <summary>The constant or variable threshold sampled for the enabled Y comparison.</summary>
        [DisplayIf(nameof(compareY)), Readable] public VariableField<float> yExpect = 0f;

        /// <summary>
        /// Receives the original signed XY displacement when storage is enabled and the target is valid,
        /// including when an axis comparison fails. It is never normalized or made absolute.
        /// </summary>
        [Writable, Tooltip("Stores raw signed world-XY displacement, even when a comparison fails.")]
        public VariableReference<Vector2> result = new();

        /// <inheritdoc />
        public override VariableReferenceBase Result => result;

        /// <inheritdoc />
        public override Exception IsValidNode()
        {
            return @object.HasValue ? null : InvalidNodeException.VariableIsRequired(nameof(@object), this);
        }

        /// <inheritdoc />
        public override State Execute()
        {
            Exception error = IsValidNode();
            if (error != null) return HandleException(error);
            GameObject target = @object.GameObjectValue;
            if (!target) return State.Failed;

            Vector2 displacement = target.transform.position - transform.position;
            float expectedX = compareX ? xExpect : 0f;
            float expectedY = compareY ? yExpect : 0f;
            bool passedX = !compareX || Compare.CompareNumeric(absoluteX ? Mathf.Abs(displacement.x) : displacement.x, xMode, expectedX);
            bool passedY = !compareY || Compare.CompareNumeric(absoluteY ? Mathf.Abs(displacement.y) : displacement.y, yMode, expectedY);

            if (storeResult && result.HasEditorReference) result.SetValue(displacement);
            return StateOf(passedX && passedY);
        }
    }
}
