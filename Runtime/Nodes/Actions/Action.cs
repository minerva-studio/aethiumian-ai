using Amlos.AI.References;
using System;
using System.Threading;
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
        private bool isReturnValueSet;
        /// <summary>
        /// task (if action is really in action
        /// </summary>
        private TaskCompletionSource<State> task;
        /// <summary>
        /// Cancellation token source for the action, used to cancel the action if needed
        /// </summary>
        private CancellationTokenSource cancellationTokenSource;


        public Task<State> Task => this.task.Task;
        protected CancellationToken CancellationToken => GetCancellationTokenSource().Token;

        public override void Initialize() { }




        public sealed override State Execute()
        {
            isReturnValueSet = false;
            task = null;
            cancellationTokenSource = null;

            Awake(); if (task != null) return task.Task.Result;
            Start(); if (task != null) return task.Task.Result;

            task = new TaskCompletionSource<State>();
            return State.WaitAction;
        }

        protected sealed override void OnStop()
        {
            isReturnValueSet = true;
            task?.TrySetCanceled(); // could happen due to interrupts
            cancellationTokenSource?.Cancel();
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
        /// End the action with failure, and return the exception
        /// </summary>
        /// <returns></returns>
        protected bool Exception(Exception e)
        {
            // cannot return twice
            if (isReturnValueSet) return false;
            isReturnValueSet = true;

            return SetException(e);
        }

        /// <summary>
        /// return node, back to its parent
        /// </summary>
        /// <returns> Whether the node has succesfully returned </returns>
        /// <param name="return"></param>
        protected bool End(bool @return)
        {
            // cannot return twice
            if (isReturnValueSet) return false;
            isReturnValueSet = true;

            return SetResult(@return);
        }

        /// <summary>
        /// Return the state of the action to tree based on the task completion state
        /// </summary>
        /// <param name="task"></param>
        protected bool Return(Task task)
        {
            // cannot return twice
            if (isReturnValueSet) return false;
            isReturnValueSet = true;

            task.ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully)
                {
                    SetResult(true);
                }
                else HandleFailedTask(t);
            }, TaskScheduler.FromCurrentSynchronizationContext());

            return true;
        }

        /// <summary>
        /// Return the state of the action to tree based on the task completion state
        /// </summary>
        /// <param name="task"></param>
        protected bool Return(Task<bool> task)
        {
            // cannot return twice
            if (isReturnValueSet) return false;
            isReturnValueSet = true;

            task.ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully)
                {
                    SetResult(t.Result);
                }
                else HandleFailedTask(t);
            }, TaskScheduler.FromCurrentSynchronizationContext());
            return true;
        }

        /// <summary>
        /// Handle the failed task, log the error and return failure
        /// </summary>
        /// <param name="t"></param>
        private void HandleFailedTask(Task t)
        {
            if (t.IsFaulted)
            {
                SetException(t.Exception);
            }
            else
            {
                SetResult(false);
            }
        }

        private bool SetResult(bool @return)
        {
            task ??= new TaskCompletionSource<State>();
            if (task.TrySetResult(StateOf(@return)))
            {
                cancellationTokenSource?.Cancel();
                return true;
            }
            return false;
        }

        private bool SetException(Exception e)
        {
            task ??= new TaskCompletionSource<State>();
            if (task.TrySetException(e))
            {
                cancellationTokenSource?.Cancel();
                return true;
            }
            return false;
        }

        /// <summary>
        /// End call from outside of the node, typically NodeProgress
        /// </summary>
        /// <param name="return"></param>
        /// <returns></returns>
        public bool ReceiveEndSignal(bool @return) => End(@return);

        /// <summary>
        /// End call from outside of the node, typically NodeProgress
        /// </summary>
        /// <param name="return"></param>
        /// <returns></returns>
        public bool ReceiveEndSignal(Exception e) => Exception(e);




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


        /// <summary>
        /// Get the cancellation token source for the action, used to cancel the action if needed
        /// </summary>
        /// <returns></returns>
        protected CancellationTokenSource GetCancellationTokenSource() => cancellationTokenSource ??= new CancellationTokenSource();
    }

    public interface IActionScript
    {
        NodeProgress Progress { get; set; }
    }
}