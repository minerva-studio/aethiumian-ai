using Amlos.AI.References;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// Node that take like an action, behave like a <see cref="UnityEngine.MonoBehaviour"/>
    /// </summary>
    [Serializable]
    public abstract class Action : TreeNode
    {
        /// <summary>
        /// has the action node returned
        /// </summary>
        private bool isReturned;
        /// <summary>
        /// task (if action is really in action
        /// </summary>
        private TaskCompletionSource<State> task;

        public override void Initialize() { }


        public sealed override State Execute()
        {
            isReturned = false;
            task = null;

            Awake(); if (task != null) { isReturned = true; return task.Task.Result; }
            Start(); if (task != null) { isReturned = true; return task.Task.Result; }

            task = new TaskCompletionSource<State>();
            return State.WaitAction;
        }

        protected sealed override void OnStop()
        {
            //Debug.Log("Node " + name + "Stoped"); 
            if (task != null && !task.Task.IsCanceled && !task.Task.IsCompleted)
            {
                isReturned = true;
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

            // if before or in start, create a completed task
            if (task == null)
            {
                task = new TaskCompletionSource<State>();
                task.SetResult(StateOf(@return));
            }
            else
            {
                task.TrySetResult(StateOf(@return));
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



        public Task<State> AsTask()
        {
            this.task ??= new TaskCompletionSource<State>();
            return this.task.Task;
        }

        public TaskAwaiter<State> GetAwaiter() => AsTask().GetAwaiter();
    }

    public interface IActionScript
    {
        NodeProgress Progress { get; set; }
    }
}