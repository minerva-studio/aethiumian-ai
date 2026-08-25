using System;
using System.Collections;
using System.Threading.Tasks;
using Aethiumian.AI.Variables;

namespace Aethiumian.AI.Nodes
{
    /// <summary>Controls how a FunctionCall or FunctionAction maps completion to a tree state.</summary>
    public enum ReturnMode
    {
        /// <summary>Successful invocation completes successfully, except for an explicit NodeProgress result.</summary>
        Default,
        /// <summary>Maps a returned value to bool using the project's variable conversion rules.</summary>
        ReturnValue,
        /// <summary>Completes successfully after a normal invocation.</summary>
        AlwaysSuccess,
        /// <summary>Completes with failure after a normal invocation.</summary>
        AlwaysFailure,
    }

    /// <summary>Shared result mapping for reflected function nodes.</summary>
    internal static class FunctionResultUtility
    {
        /// <summary>Maps a reflected function result to a tree success value.</summary>
        internal static bool Resolve(ReturnMode mode, Type declaredReturnType, object value)
        {
            if (mode == ReturnMode.AlwaysSuccess)
            {
                return true;
            }

            if (mode == ReturnMode.AlwaysFailure)
            {
                return false;
            }

            if (mode != ReturnMode.ReturnValue || FunctionRegistry.GetReturnValueType(declaredReturnType) == typeof(void))
            {
                return true;
            }

            // Calls do not await external operations. Their container is not the operation's result.
            if (value is Task || value is IEnumerator)
            {
                return true;
            }

            if (ImplicitConverter<bool>.TryFrom(value, out bool converted))
            {
                return converted;
            }

            return value != null;
        }

        /// <summary>Returns whether a reflected method produces a value usable by ReturnValue mode.</summary>
        internal static bool HasReturnValue(Type declaredReturnType)
        {
            return FunctionRegistry.GetReturnValueType(declaredReturnType) != typeof(void);
        }
    }
}
