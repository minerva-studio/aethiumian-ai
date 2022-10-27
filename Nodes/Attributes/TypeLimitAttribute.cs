using System;

namespace Amlos.AI
{
    /// <summary>
    /// Attribute that set a generic variable with special type limit
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class TypeLimitAttribute : Attribute
    {
        readonly VariableType[] variableTypes;

        // This is a positional argument
        public TypeLimitAttribute(params VariableType[] varType)
        {
            this.variableTypes = varType;
        }

        public VariableType[] VariableTypes
        {
            get { return variableTypes; }
        }
    }

    /**
     * - Sequence
     *   - store enemyCount from GetEnemyCount(); [Node]
     *   - condition
     *     - if enemyCount > 3
     *     - true: ()
     */
}
