using System;
using Aethiumian.AI.Variables;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    /// <summary>Provides target-shape dispatch for unary numeric and vector operations.</summary>
    [Serializable]
    public abstract class ComponentwiseUnaryArithmetic : Arithmetic
    {
        [Readable, NumericOrVector]
        public VariableField a;

        [Readable, Writable]
        public VariableReference result;

        /// <summary>Applies the operation to an integer scalar.</summary>
        protected abstract int Operation(int value);

        /// <summary>Applies the operation to a floating-point scalar.</summary>
        protected abstract float Operation(float value);

        /// <summary>Applies the operation to a two-lane floating-point vector.</summary>
        protected abstract Vector2 Operation(Vector2 value);

        /// <summary>Applies the operation to a three-lane floating-point vector.</summary>
        protected abstract Vector3 Operation(Vector3 value);

        /// <summary>Applies the operation to a four-lane floating-point vector.</summary>
        protected abstract Vector4 Operation(Vector4 value);

        /// <summary>Applies the operation to a four-lane integer value.</summary>
        protected abstract ComponentwiseInt4 Operation(ComponentwiseInt4 value);

        /// <summary>Executes the operation using the input's natural numeric domain.</summary>
        public override State Execute()
        {
            return ExecuteComponentwise(HasIntegerComponents());
        }

        /// <summary>Executes the operation using the destination variable's shape.</summary>
        protected State ExecuteComponentwise(bool useIntegerDomain)
        {
            if (!SupportsComponentwiseOperation())
            {
                return State.Failed;
            }

            try
            {
                int componentCount = result.Type.ComponentCount();
                if (HasNaN(a, componentCount))
                {
                    return State.Failed;
                }

                switch (result.Type)
                {
                    case VariableType.Int:
                    case VariableType.Float:
                    case VariableType.Bool:
                        if (useIntegerDomain)
                        {
                            result.SetValue(Operation(a.IntScalarValue));
                            return State.Success;
                        }

                        float scalarResult = Operation(a.ScalarValue);
                        return result.SetValue(scalarResult, failOnNaN)
                            ? State.Success
                            : State.Failed;
                    case VariableType.Vector2:
                        if (useIntegerDomain)
                        {
                            result.SetComponentwiseValue(Operation(a.IntComponentwiseValue));
                            return State.Success;
                        }

                        Vector2 vector2Result = Operation(a.ComponentwiseVector2Value);
                        return result.SetValue(vector2Result, failOnNaN)
                            ? State.Success
                            : State.Failed;
                    case VariableType.Vector3:
                        if (useIntegerDomain)
                        {
                            result.SetComponentwiseValue(Operation(a.IntComponentwiseValue));
                            return State.Success;
                        }

                        Vector3 vector3Result = Operation(a.ComponentwiseVector3Value);
                        return result.SetValue(vector3Result, failOnNaN)
                            ? State.Success
                            : State.Failed;
                    case VariableType.Vector4:
                        if (useIntegerDomain)
                        {
                            result.SetComponentwiseValue(Operation(a.IntComponentwiseValue));
                            return State.Success;
                        }

                        Vector4 vector4Result = Operation(a.ComponentwiseValue);
                        return result.SetValue(vector4Result, failOnNaN)
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

        /// <summary>Determines whether both input and destination belong to the component-wise domain.</summary>
        protected bool SupportsComponentwiseOperation()
        {
            return a != null && result != null
                && a.Type.ComponentCount() != 0
                && result.Type.ComponentCount() != 0;
        }

        /// <summary>Determines whether the input has a naturally discrete component representation.</summary>
        protected bool HasIntegerComponents()
        {
            return a.Type == VariableType.Int || a.Type == VariableType.Bool;
        }

    }
}
