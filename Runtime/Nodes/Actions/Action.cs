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
        /// task (if action is really in action
        /// </summary>
        private TaskCompletionSource<State> task;
        /// <summary>
        /// Cancellation token source for the action, used to cancel the action if needed
        /// </summary>
        private CancellationTokenSource cancellationTokenSource;



        /// <summary>
        /// has the action node returned
        /// </summary>
        public bool IsComplete => task?.Task.IsCompleted == true;
        /// <summary>
        /// Cancellation token of an action, raised when the action is stopped by AI (by either completion or forced stop)
        /// </summary>
        public CancellationToken CancellationToken => GetCancellationTokenSource().Token;
        /// <summary>
        /// Action as task
        /// </summary>
        internal Task<State> ActionTask => this.task.Task;




        public override void Initialize() { }




        public sealed override State Execute()
        {
            task = null;
            cancellationTokenSource = null;

            Awake(); if (IsComplete) return task.Task.Result;
            Start(); if (IsComplete) return task.Task.Result;

            task = new TaskCompletionSource<State>();
            return State.WaitAction;
        }

        protected sealed override void OnStop()
        {
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
            if (IsComplete)
            {
                Debug.LogException(e);
                return false;
            }
            SetException(e);
            return true;
        }

        /// <summary>
        /// return node, back to its parent
        /// </summary>
        /// <returns> Whether the node has succesfully returned </returns>
        /// <param name="return"></param>
        protected bool End(bool @return)
        {
            // cannot return twice
            if (IsComplete) return false;

            SetResult(@return);
            return true;
        }

        /// <summary>
        /// Return the state of the action to tree based on the task completion state
        /// </summary>
        /// <param name="task"></param>
        protected bool Return(Task task)
        {
            // cannot return twice
            if (IsComplete) return false;

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
            if (IsComplete) return false;

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

        private void SetResult(bool @return)
        {
            task ??= new TaskCompletionSource<State>();
            task.TrySetResult(StateOf(@return));
            cancellationTokenSource?.Cancel();
        }

        private void SetException(Exception e)
        {
            task ??= new TaskCompletionSource<State>();
            task.SetException(e);
            cancellationTokenSource?.Cancel();
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
         * Task control
         */


        /// <summary>
        /// Get the cancellation token source for the action, used to cancel the action if needed
        /// </summary>
        /// <returns></returns>
        protected CancellationTokenSource GetCancellationTokenSource() => cancellationTokenSource ??= new CancellationTokenSource();

        /// <summary>
        /// Task before action is complete or cancelled
        /// </summary>
        /// <returns></returns>
        protected Task BeforeTimeoutOrComplete()
        {
            task ??= new TaskCompletionSource<State>();
            var reg = CancellationToken.Register(() => task.TrySetCanceled(CancellationToken));
            return task.Task;
        }

        /// <summary>
        /// Task before action is complete or cancelled
        /// </summary>
        protected Task BeforeTimeout(Task task) => Task.WhenAny(task, BeforeTimeoutOrComplete());



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