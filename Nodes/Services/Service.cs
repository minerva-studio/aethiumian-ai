using System;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// Base class for all service node
    /// </summary>
    [Serializable]
    public abstract class Service : Flow
    {
        public abstract bool IsReady { get; }


        public Service() : base()
        {

        }

        /// <summary>
        /// <inheritdoc/>
        /// <br/>
        /// Cannot override
        /// </summary> 
        public void End()
        {
            //trying to end other node
            BehaviourTree.ServiceStack serviceStack = behaviourTree.ServiceStacks[this];
            if (serviceStack == null || serviceStack.Current != this) return;

            //end this service
            behaviourTree.EndService(this);
        }

        public override abstract State ReceiveReturnFromChild(bool @return);


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
}
