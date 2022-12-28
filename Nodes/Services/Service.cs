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
        public abstract bool IsReady { get; }


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
        /// Call when service is registered
        /// </summary>
        public virtual void OnRegistered()
        {
        }

        /// <summary>
        /// Call when service is unregistered
        /// </summary>
        public virtual void OnUnregistered()
        {
        }

        /// <summary>
        /// Timer method called every frame
        /// </summary>
        public abstract void UpdateTimer();
    }

    public abstract class RepeatService : Service
    {
        public int interval;
        public RangeInt randomDeviation;

        private int currentFrame;

        public override bool IsReady => currentFrame >= interval;
        public override void UpdateTimer()
        {
            currentFrame++;
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
