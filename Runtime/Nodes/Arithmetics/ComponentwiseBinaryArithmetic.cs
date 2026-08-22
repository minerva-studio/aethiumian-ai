using System;
using Aethiumian.AI.Variables;
using UnityEngine;
using UnityEngine.Serialization;

namespace Aethiumian.AI.Nodes
{
    /// <summary>Executes binary numeric operations using the destination component shape.</summary>
    [Serializable]
    public abstract class ComponentwiseBinaryArithmetic : Arithmetic
    {
        [Readable, NumericOrVector]
        [FormerlySerializedAs("y")]
        public VariableField a;

        [Readable, NumericOrVector]
        [FormerlySerializedAs("x")]
        public VariableField b;

        [Writable]
        public VariableReference result;

        /// <summary>Applies the operation to scalar operands.</summary>
        protected abstract float Operation(float a, float b);

        /// <summary>Applies the operation to two-lane operands.</summary>
        protected abstract Vector2 Operation(Vector2 a, Vector2 b);

        /// <summary>Applies the operation to three-lane operands.</summary>
        protected abstract Vector3 Operation(Vector3 a, Vector3 b);

        /// <summary>Applies the operation to four-lane operands.</summary>
        protected abstract Vector4 Operation(Vector4 a, Vector4 b);

        /// <summary>Applies an integer operation to the active lanes.</summary>
        protected virtual ComponentwiseInt4 Operation(ComponentwiseInt4 a, ComponentwiseInt4 b, int componentCount)
        {
            throw new InvalidOperationException("This operation does not support integer dispatch.");
        }

        /// <summary>Resolves the operation domain independently from the destination shape.</summary>
        protected virtual bool TryResolveOperationDomain(out bool useIntegerDomain)
        {
            useIntegerDomain = false;
            return true;
        }

        /// <summary>Validates normalized operands before the typed operation is dispatched.</summary>
        /// <param name="a">The normalized first operand.</param>
        /// <param name="b">The normalized second operand.</param>
        /// <param name="componentCount">The number of active destination lanes.</param>
        /// <returns><see langword="true"/> when the operands are valid.</returns>
        protected virtual bool ValidateInput(Vector4 a, Vector4 b, int componentCount)
        {
            return true;
        }

        /// <summary>Executes the operation using the destination variable's shape.</summary>
        public override State Execute()
        {
            if (a == null || b == null || result == null
                || !SupportsComponentwiseOperands(a, b)
                || result.Type.ComponentCount() == 0)
            {
                return State.Failed;
            }

            try
            {
                int componentCount = result.Type.ComponentCount();
                if (!TryResolveOperationDomain(out bool useIntegerDomain)
                    || HasNaNOperands(a, b, componentCount))
                {
                    return State.Failed;
                }

                if (useIntegerDomain)
                {
                    result.SetComponentwiseValue(
                        Operation(a.IntComponentwiseValue, b.IntComponentwiseValue, componentCount));
                    return State.Success;
                }

                Vector4 normalizedA = a.ComponentwiseValue;
                Vector4 normalizedB = b.ComponentwiseValue;
                if (!ValidateInput(normalizedA, normalizedB, componentCount))
                {
                    return State.Failed;
                }

                switch (result.Type)
                {
                    case VariableType.Int:
                    case VariableType.Float:
                    case VariableType.Bool:
                        return result.SetValue(Operation(a.ScalarValue, b.ScalarValue), failOnNaN)
                            ? State.Success
                            : State.Failed;
                    case VariableType.Vector2:
                        return result.SetValue(
                                Operation(a.ComponentwiseVector2Value, b.ComponentwiseVector2Value),
                                failOnNaN)
                            ? State.Success
                            : State.Failed;
                    case VariableType.Vector3:
                        return result.SetValue(
                                Operation(a.ComponentwiseVector3Value, b.ComponentwiseVector3Value),
                                failOnNaN)
                            ? State.Success
                            : State.Failed;
                    case VariableType.Vector4:
                        return result.SetValue(Operation(normalizedA, normalizedB), failOnNaN)
                            ? State.Success
                            : State.Failed;
                    default:
                        return State.Failed;
                }
            }
            catch (Exception e)
            {
                return HandleException(e);
            }
        }

        /// <summary>Checks both operands within the destination shape.</summary>
        protected bool HasNaNOperands([Readable] VariableField a, [Readable] VariableField b, int componentCount)
        {
            return HasNaN(a, componentCount) || HasNaN(b, componentCount);
        }

        /// <summary>Determines whether both operands can participate in component-wise math.</summary>
        protected static bool SupportsComponentwiseOperands([Readable] VariableField a, [Readable] VariableField b)
        {
            return a.Type.ComponentCount() != 0 && b.Type.ComponentCount() != 0;
        }

        /// <summary>Determines whether a value has a naturally discrete component representation.</summary>
        protected static bool HasIntegerComponents(VariableField value)
        {
            return value.Type == VariableType.Int || value.Type == VariableType.Bool;
        }
    }
}
