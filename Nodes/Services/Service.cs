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
        /// End service
        /// <br/>
        /// Cannot override
        /// </summary> 
        protected void End()
        {
            //end this service
            behaviourTree.EndService(this);
        }

        /// <summary>
        /// <inheritdoc/>
        /// <br/>
        /// Service return state doesn't matter since it is the first node in the service stack
        /// </summary>
        /// <param name="return"></param>
        /// <returns></returns>
        public override sealed State ReceiveReturnFromChild(bool @return)
        {
            ReceiveReturn(@return);
            return State.Success;
        }

        /// <summary> 
        /// Service return state doesn't matter since it is the first node in the service stack
        /// </summary>
        /// <param name="return"></param>
        /// <returns></returns>
        public virtual void ReceiveReturn(bool @return) { }


        /// <summary>
        /// Call when service is registered
        /// </summary>
        public virtual void OnRegistered() { }

        /// <summary>
        /// Call when service is unregistered
        /// </summary>
        public virtual void OnUnregistered() { }

        /// <summary>
        /// Timer method called every frame
        /// </summary>
        public abstract void UpdateTimer();
    }
}
