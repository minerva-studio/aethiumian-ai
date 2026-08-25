using Aethiumian.AI.References;
using System;
using System.Threading;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// Node that take like an action, behave like a <see cref="UnityEngine.MonoBehaviour"/>
    /// </summary>
    [Serializable]
    public abstract class Action : ServiceHostNode
    {
        /// <summary>
        /// Result produced by the current action execution.
        /// </summary>
        [AIInspectorIgnore]
        [NonSerialized]
        private State? completionState;
        /// <summary>
        /// Exception produced by the current action execution, if any.
        /// </summary>
        [AIInspectorIgnore]
        [NonSerialized]
        private Exception completionException;
        /// <summary>
        /// Cancellation token source for the action, used to cancel the action if needed
        /// </summary>
        [AIInspectorIgnore]
        private CancellationTokenSource cancellationTokenSource;



        /// <summary>
        /// has the action node returned
        /// </summary>
        public bool IsComplete => completionState.HasValue || completionException != null;
        /// <summary>
        /// Cancellation token of an action, raised when the action is stopped by AI (by either completion or forced stop)
        /// </summary>
        public CancellationToken CancellationToken => GetCancellationTokenSource().Token;
        public override void Initialize() { }




        public sealed override State Execute()
        {
            completionState = null;
            completionException = null;
            cancellationTokenSource = null;

            Awake(); if (IsComplete) return ResolveSynchronousCompletion();
            Start(); if (IsComplete) return ResolveSynchronousCompletion();

            return State.WaitAction;
        }

        protected sealed override void OnStop()
        {
            cancellationTokenSource?.Cancel();
            OnDestroy();
        }

        /// <summary>
        /// Resolves a completion observed directly by Execute.
        /// </summary>
        private State ResolveSynchronousCompletion()
        {
            if (completionException != null)
            {
                throw completionException;
            }

            return completionState!.Value;
        }

        /// <summary>
        /// Resolves a completion observed by the behaviour-tree Tick path.
        /// </summary>
        internal State ResolveCompletion()
        {
            if (completionException != null)
            {
                return HandleException(completionException);
            }

            return completionState ?? State.WaitAction;
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

        private void SetResult(bool @return)
        {
            if (IsComplete) return;

            completionState = StateOf(@return);
            cancellationTokenSource?.Cancel();
        }

        private void SetException(Exception e)
        {
            if (IsComplete) return;

            completionException = e;
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




        /// <summary>
        /// Get the cancellation token source for the action, used to cancel the action if needed
        /// </summary>
        /// <returns></returns>
        protected CancellationTokenSource GetCancellationTokenSource() => cancellationTokenSource ??= new CancellationTokenSource();

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
