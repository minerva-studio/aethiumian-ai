using Minerva.Module;
using System;

namespace Amlos.AI
{
    /// <summary>
    /// Base class for all service node
    /// </summary>
    [Serializable]
    public abstract class Service : TreeNode
    {
        public int interval;
        public RangeInt randomDeviation;

        public Service() : base()
        {

        }

        /// <summary>
        /// <inheritdoc/>
        /// <br></br>
        /// Cannot override
        /// </summary>
        /// <param name="return"></param>
        public sealed override void End(bool @return)
        {
            //trying to end other node
            BehaviourTree.ServiceStack serviceStack = behaviourTree.ServiceStacks[this];
            if (serviceStack == null || serviceStack.Current != this) return;

            //end this service
            behaviourTree.EndService(this);
        }

        /// <summary>
        /// <inheritdoc/>
        /// <br></br>
        /// Cannot override
        /// </summary>
        /// <param name="return"></param>
        public sealed override void Stop()
        {
            base.Stop();
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
