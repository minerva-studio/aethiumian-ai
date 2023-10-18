using Amlos.AI.References;
using System;
using System.Threading.Tasks;
using UnityEditor.VersionControl;

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
        protected bool isBeforeOrInStart;
        private TaskCompletionSource<State> task;

        public override void Initialize() { }


        public sealed override State Execute()
        {
            isReturned = false;
            isBeforeOrInStart = true;
            exeResult = State.Wait;
            task = null;

            Awake(); if (IsReturnValue(exeResult)) { isReturned = true; return exeResult; }
            Start(); if (IsReturnValue(exeResult)) { isReturned = true; return exeResult; }

            isBeforeOrInStart = false;
            return exeResult;
        }

        protected sealed override void OnStop()
        {
            //Debug.Log("Node " + name + "Stoped"); 
            if (task != null && !task.Task.IsCanceled && !task.Task.IsCompleted)
            {
                task.SetCanceled();
            }
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

            if (isBeforeOrInStart)
            {
                exeResult = StateOf(@return);
            }
            else
            {
                task.SetResult(StateOf(@return));
            }
            return true;
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



        internal void SetRunningTask(TaskCompletionSource<State> state)
        {
            this.task = state;
        }
    }

    public interface IActionScript
    {
        NodeProgress Progress { get; set; }
    }
}