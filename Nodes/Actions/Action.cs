using Amlos.AI.References;
using System;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// Node that take like an action, behave like a <see cref="UnityEngine.MonoBehaviour"/>
    /// </summary>
    [Serializable]
    public abstract class Action : TreeNode
    {
        /// <summary>
        /// execution result
        /// </summary>
        private State exeResult;
        /// <summary>
        /// has the action node returned
        /// </summary>
        private bool isReturned;
        /// <summary>
        /// Is the action node not yet in update
        /// </summary>
        protected bool isInFirstExecution;

        public override void Initialize() { }


        public sealed override State Execute()
        {
            isReturned = false;
            isInFirstExecution = true;
            exeResult = State.Wait;

            Awake(); if (IsReturnValue(exeResult)) { OnDestroy(); isReturned = true; return exeResult; }
            Start(); if (IsReturnValue(exeResult)) { OnDestroy(); isReturned = true; return exeResult; }

            isInFirstExecution = false;
            return exeResult;
        }

        protected sealed override void OnStop()
        {
            //Debug.Log("Node " + name + "Stoped"); 
            OnDestroy();
        }

        /// <summary>
        /// Short for End(true)
        /// </summary>
        /// <returns></returns>
        protected bool Success() => End(true);

        /// <summary>
        /// Short for End(false)
        /// </summary>
        /// <returns></returns>
        protected bool Fail() => End(false);

        /// <summary>
        /// return node, back to its parent
        /// </summary>
        /// <returns> Whether the node has succesfully returned </returns>
        /// <param name="return"></param>
        protected bool End(bool @return)
        {
            // cannot return twice
            if (isReturned) return false;
            isReturned = true;
            if (isInFirstExecution)
            {
                exeResult = StateOf(@return);
                return true;
            }

            Stop();
            return behaviourTree.ReceiveReturn(this, @return);
        }

        /// <summary>
        /// End call from outside of the node, typically NodeProgress
        /// </summary>
        /// <param name="return"></param>
        /// <returns></returns>
        public bool ReceiveEndSignal(bool @return)
        {
            return End(@return);
        }




        /**
         * Consider the following method just like unity messages
         */


        /// <summary> Call before action start execute </summary>
        public virtual void Awake() { }
        /// <summary> Called only once when action executed </summary>
        public virtual void Start() { }
        public virtual void Update() { }
        public virtual void LateUpdate() { }
        public virtual void FixedUpdate() { }
        public virtual void OnDestroy() { }
    }

    public interface IActionScript
    {
        NodeProgress Progress { get; set; }
    }
}