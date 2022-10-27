using System;

namespace Amlos.AI
{
    /// <summary>
    /// Allow a node to be called during service call phase
    /// </summary>
    [System.AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class AllowServiceCallAttribute : Attribute
    {

        public AllowServiceCallAttribute()
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
