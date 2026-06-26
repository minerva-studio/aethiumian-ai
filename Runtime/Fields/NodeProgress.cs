#nullable enable
using Aethiumian.AI.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Aethiumian.AI.References
{
    /// <summary>
    /// class representing the progress of ai
    /// </summary>
    public class NodeProgress : IDisposable
    {
        public readonly struct TimeEnumerator : IAsyncEnumerator<float>, IAsyncEnumerable<float>
        {
            private readonly NodeProgress nodeProgress;
            private readonly CancellationToken cancellationToken;

            public TimeEnumerator(NodeProgress nodeProgress, CancellationToken cancellationToken)
            {
                this.nodeProgress = nodeProgress;
                this.cancellationToken = cancellationToken;
            }

            public readonly float Current => nodeProgress.node.behaviourTree.CurrentStageDuration;

            public readonly ValueTask DisposeAsync() => default;

            public IAsyncEnumerator<float> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            {
                return new TimeEnumerator(nodeProgress, CancellationTokenSource.CreateLinkedTokenSource(this.cancellationToken, cancellationToken).Token);
            }

            public async readonly ValueTask<bool> MoveNextAsync()
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return false;
                }
                if (nodeProgress.IsComplete)
                {
                    return false;
                }
                await FrameAwait.NextFrameAsync();
                return true;
            }
        }


        /// <summary>
        /// Action node representing
        /// </summary>
        readonly Nodes.Action node;

        /// <summary>
        /// action will execute when the node is forced to stop
        /// </summary>
        public event System.Action InterruptStopAction { add => node.OnInterrupted += value; remove => node.OnInterrupted -= value; }
        /// <summary>
        /// Have not returned and not disposed
        /// </summary>
        public bool IsComplete => node.IsComplete;
        public TimeEnumerator Timer => new(this, CancellationToken);
        /// <summary>
        /// Cancellation token of an action, raised when the action is stopped by AI (by either completion or forced stop)
        /// </summary>
        public CancellationToken CancellationToken => node.CancellationToken;


        /// <summary>
        /// waiting coroutine for script
        /// </summary>
        Coroutine? coroutine;
        MonoBehaviour? behaviour;

        public NodeProgress(Nodes.Action node)
        {
            this.node = node;
            this.InterruptStopAction += InvokeEndEvents;
        }


        /// <summary>
        /// pause the behaviour tree
        /// </summary>
        public void Pause()
        {
            node.behaviourTree.Pause();
        }

        /// <summary>
        /// resume ai running
        /// </summary>
        public void Resume()
        {
            node.behaviourTree.Resume();
        }

        /// <summary>
        /// Complete this node with the given boolean result.
        /// </summary>
        /// <param name="result">The result returned by the node.</param>
        [CompletesAction]
        public bool Complete(bool result)
        {
            // Avoid completing the same action twice when multiple async sources finish.
            if (IsComplete)
                return false;

            return node.ReceiveEndSignal(result);
        }

        /// <summary>
        /// Complete this node with an exception.
        /// </summary>
        /// <param name="exception">The exception returned by the node.</param>
        [CompletesAction]
        public bool CompleteWithException(Exception exception)
        {
            // Avoid completing the same action twice when multiple async sources finish.
            if (IsComplete)
                return false;

            return node.ReceiveEndSignal(exception);
        }

        /// <summary>
        /// Complete this node when the condition becomes true.
        /// </summary>
        /// <param name="condition">The condition that triggers completion.</param>
        /// <param name="resultProvider">The result provider evaluated when the condition is true.</param>
        [CompletesAction]
        public void CompleteWhen(Func<bool> condition, ResultProvider resultProvider = default)
        {
            var aiComponent = node.behaviourTree?.AIComponent;
            if (aiComponent == null)
            {
                CompleteWhenAsync(condition, resultProvider);
                return;
            }

            this.coroutine = aiComponent.StartCoroutine(Wait());

            IEnumerator Wait()
            {
                yield return new WaitUntil(condition);
                Complete(resultProvider.GetResult());
            }
        }

        private async void CompleteWhenAsync(Func<bool> condition, ResultProvider resultProvider)
        {
            try
            {
                while (!IsComplete && !condition())
                {
                    await FrameAwait.NextFrameAsync();
                }

                if (!IsComplete)
                {
                    Complete(resultProvider.GetResult());
                }
            }
            catch (Exception exception)
            {
                CompleteWithException(exception);
            }
        }

        /// <summary>
        /// Complete this node when the Unity object is destroyed.
        /// </summary>
        /// <param name="obj">The Unity object watched for destruction.</param>
        /// <param name="result">The result returned when the object is destroyed.</param>
        [CompletesAction]
        public void CompleteWhenDestroyed(UnityEngine.Object obj, ResultProvider result = default)
        {
            if (!obj)
            {
                Complete(result.GetResult());
                return;
            }
            if (obj is MonoBehaviour monoBehaviour)
            {
                CompleteWhenCanceled(monoBehaviour.destroyCancellationToken, result);
                return;
            }

            CompleteWhen(() => !obj, result);
        }


        /// <summary>
        /// Complete this node when the cancellation token is raised.
        /// </summary>
        /// <param name="token">The token watched for cancellation.</param>
        /// <param name="result">The result returned when the token is canceled.</param>
        [CompletesAction]
        public void CompleteWhenCanceled(CancellationToken token, ResultProvider result = default)
        {
            if (token.IsCancellationRequested)
            {
                Complete(result.GetResult());
                return;
            }

            if (!token.CanBeCanceled)
            {
                return;
            }

            CancellationTokenRegistration registration = default;

            void CompleteAndDispose()
            {
                registration.Dispose();
                Complete(result.GetResult());
            }

            registration = token.Register(CompleteAndDispose);
            InterruptStopAction += () => registration.Dispose();
        }

        /// <summary>
        /// Complete this node when the task completes.
        /// </summary>
        /// <param name="task">The task watched for completion.</param>
        /// <param name="result">The result returned when the task completes successfully.</param>
        /// <param name="canceledResult">The result returned when the task is canceled.</param>
        [CompletesAction]
        public void CompleteWhenCompleted(Task task, ResultProvider result = default, ResultProvider canceledResult = default)
        {
            CompleteWhenCompletedAsync(task, result, canceledResult);
        }

        /// <summary>
        /// Complete this node when the boolean task completes.
        /// </summary>
        /// <param name="task">The task watched for completion.</param>
        /// <param name="canceledResult">The result returned when the task is canceled.</param>
        [CompletesAction]
        public void CompleteWhenCompleted(Task<bool> task, ResultProvider canceledResult = default) => CompleteWhenCompletedAsync(task, canceledResult);

        /// <summary>
        /// Complete this node when the condition becomes false.
        /// </summary>
        /// <param name="condition">The condition watched while it remains true.</param>
        /// <param name="result">The result provider evaluated when the condition becomes false.</param>
        [CompletesAction]
        public void CompleteWhenFalse(Func<bool> condition, ResultProvider result) => CompleteWhen(() => !condition(), result);

        private async void CompleteWhenCompletedAsync(Task task, ResultProvider result, ResultProvider canceledResult)
        {
            try
            {
                await task;
                Complete(result.GetResult());
            }
            catch (OperationCanceledException)
            {
                Complete(canceledResult.GetResult(false));
            }
            catch (Exception exception)
            {
                CompleteWithException(task.Exception ?? exception);
            }
        }

        private async void CompleteWhenCompletedAsync(Task<bool> task, ResultProvider canceledResult)
        {
            try
            {
                Complete(await task);
            }
            catch (OperationCanceledException)
            {
                Complete(canceledResult.GetResult(false));
            }
            catch (Exception exception)
            {
                CompleteWithException(task.Exception ?? exception);
            }
        }

        /// <summary>
        /// End this node with the given boolean result.
        /// </summary>
        /// <param name="return">The result returned by the node.</param>
        [Obsolete("Use Complete(bool) instead.")]
        [CompletesAction]
        public bool End(bool @return) => Complete(@return);

        /// <summary>
        /// End the node with an exception.
        /// </summary>
        /// <param name="e">The exception returned by the node.</param>
        [Obsolete("Use CompleteWithException(Exception) instead.")]
        [CompletesAction]
        public bool SetException(Exception e) => CompleteWithException(e);

        /// <summary>
        /// Set the current running behaviour to this node progress
        /// </summary>
        /// <param name="behaviour"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public void Run(MonoBehaviour behaviour)
        {
            if (behaviour == null)
                throw new ArgumentNullException(nameof(behaviour));

            this.behaviour = behaviour;
        }




#if UNITY_2023_1_OR_NEWER
        /// <summary>
        /// Wait for the end of the monobehaviour execution then return. If the action ends early by interruption, it will throw <see cref="OperationCanceledException"/>
        /// </summary>
        /// <param name="monoBehaviour"></param>
        /// <returns></returns>
        public async Awaitable RunAsync(MonoBehaviour monoBehaviour)
        {
            Run(monoBehaviour);
            float duration = node.behaviourTree.CurrentStage.RemainingDuration;
            await WaitForSecondsAsync(duration, monoBehaviour.destroyCancellationToken);
        }

        public async Awaitable NextFrameAsync(CancellationToken softToken = default)
        {
            var hardToken = this.CancellationToken;

            if (hardToken.IsCancellationRequested)
                throw new OperationCanceledException(hardToken);
            if (softToken.IsCancellationRequested)
                return;

            await FrameAwait.NextFrameAsync();

            if (hardToken.IsCancellationRequested)
                throw new OperationCanceledException(hardToken);
        }

        public async Awaitable FixedUpdateAsync(CancellationToken softToken = default)
        {
            var hardToken = this.CancellationToken;
            var ct = GetCancellationTokenFrom(softToken, hardToken);

            try
            {
                await Awaitable.FixedUpdateAsync(ct);
            }
            catch (OperationCanceledException)
            {
                if (hardToken.IsCancellationRequested)
                    throw;
            }
        }

        public async Awaitable WaitForSecondsAsync(float delay, CancellationToken softToken = default)
        {
            var hardToken = this.CancellationToken;
            var ct = GetCancellationTokenFrom(softToken, hardToken);
            try
            {
                await Awaitable.WaitForSecondsAsync(delay, ct);
            }
            catch (OperationCanceledException)
            {
                if (hardToken.IsCancellationRequested)
                    throw;
            }
        }

        private CancellationToken GetCancellationTokenFrom(CancellationToken softToken, CancellationToken hardToken)
        {
            if (!softToken.CanBeCanceled) return hardToken;
            else return CancellationTokenSource.CreateLinkedTokenSource(softToken, hardToken).Token;
        }

#endif
        private void InvokeEndEvents()
        {
            if (behaviour != null)
                UnityEngine.Object.Destroy(behaviour);
            if (coroutine != null && node.behaviourTree?.AIComponent != null)
                node.behaviourTree.AIComponent.StopCoroutine(coroutine);
        }

        public void Dispose()
        {
            if (IsComplete)
                return;

            Complete(false);
        }


        /// <summary>
        /// A discriminated union type that can either hold a boolean result or a function that provides a boolean result. 
        /// This allows for flexible result handling when completing a node.
        /// </summary>
        public readonly struct ResultProvider
        {
            readonly bool? result;
            readonly Func<bool>? resultProvider;


            public ResultProvider(bool result)
            {
                this.result = result;
                this.resultProvider = null;
            }

            public ResultProvider(Func<bool> resultProvider)
            {
                this.result = null;
                this.resultProvider = resultProvider;
            }


            public bool GetResult() => GetResult(true);

            public bool GetResult(bool defaultValue)
            {
                if (result.HasValue)
                    return result.Value;
                if (resultProvider != null)
                    return resultProvider();

                // default to true if no result or provider is given
                return defaultValue;
            }


            public static implicit operator ResultProvider(bool result) => new ResultProvider(result);
            public static implicit operator ResultProvider(Func<bool> resultProvider) => new ResultProvider(resultProvider);
        }
    }
}
