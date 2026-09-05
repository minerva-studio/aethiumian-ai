using Aethiumian.AI.Variables;
using System;

namespace Aethiumian.AI.Editor
{
    /// <summary>Applies one declared variable usage without owning UI, Undo, or asset persistence.</summary>
    internal static class VariableAuthoring
    {
        /// <summary>Configures an authored value variable with a concrete type and initial value.</summary>
        internal static void ConfigureValue(VariableData variable, VariableType type, object initialValue)
        {
            variable.SetScript(false);
            variable.Path = null;
            variable.Flags &= ~VariableFlag.Timer;
            variable.SetType(type);
            variable.SetDefaultValue(initialValue);
        }

        /// <summary>Configures a local Float timer that starts inactive until explicitly written.</summary>
        internal static void ConfigureTimer(VariableData variable)
        {
            variable.SetScript(false);
            variable.Path = null;
            variable.SetType(VariableType.Float);
            variable.SetDefaultValue(0f);
            variable.Flags &= ~(VariableFlag.Static | VariableFlag.Global | VariableFlag.FromScript | VariableFlag.FromAttribute);
            variable.Flags |= VariableFlag.Timer;
        }

        /// <summary>Configures a script member binding after the caller has selected a compatible member.</summary>
        internal static void ConfigureScriptBinding(VariableData variable, string path, Type memberType)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A script member path is required.", nameof(path));
            if (memberType == null) throw new ArgumentNullException(nameof(memberType));

            VariableType type = VariableUtility.GetVariableType(memberType);
            variable.SetType(type);
            if (type is VariableType.Generic or VariableType.UnityObject)
            {
                variable.SetBaseType(memberType);
            }
            variable.Flags &= ~VariableFlag.Timer;
            variable.SetScript(true);
            variable.Path = path;
        }
    }
}
