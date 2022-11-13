using System;

namespace Amlos.AI
{
    /// <summary>
    /// Attribute that set a generic variable with numeric type limit
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public class VectorTypeLimitAttribute : TypeLimitAttribute
    {
        // This is a positional argument
        public VectorTypeLimitAttribute() : base(VariableType.Vector3, VariableType.Vector3)
        {
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
