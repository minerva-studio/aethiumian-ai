using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Amlos.AI.References
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
#if UNITY_2023_1_OR_NEWER
                await Awaitable.NextFrameAsync();
#else
                await Task.Yield();
#endif
                return true;
            }
        }


        /// <summary>
        /// Action node representing
        /// </summary>
        readonly Nodes.Action node;
        bool? returnVal;

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
        Coroutine coroutine;
        MonoBehaviour behaviour;

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
        /// End this node
        /// </summary>
        /// <param name="return">the return value of the node</param>
        public bool End(bool @return)
        {
            //do not return again if has returned
            if (IsComplete)
                return false;

            this.returnVal = @return;
            return node.ReceiveEndSignal(@return);
        }

        /// <summary>
        /// End this node
        /// </summary>
        /// <param name="return">the return value of the node</param>
        public bool End()
        {
            //do not return again if has returned
            if (IsComplete)
                return false;

            this.returnVal ??= false;
            return node.ReceiveEndSignal(returnVal.Value);
        }

        /// <summary>
        /// End the node with an exception
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public bool SetException(Exception e)
        {
            //do not return again if has returned
            if (IsComplete)
                return false;

            return node.ReceiveEndSignal(e);
        }

        /// <summary>
        /// end the node execution when the mono behaviour is destroyed
        /// </summary>
        /// <param name="monoBehaviour"></param>
        /// <param name="ret"></param>
        public void RunAndReturn(MonoBehaviour monoBehaviour, bool ret = true)
        {
            Run(monoBehaviour);

            this.coroutine = node.AIComponent.StartCoroutine(Wait());
            this.returnVal = ret;

            IEnumerator Wait()
            {
                yield return new WaitWhile(() => monoBehaviour);
                End();
            }
        }

        /// <summary>
        /// Set the current running behaviour to this node progress
        /// </summary>
        /// <param name="behaviour"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public void Run(MonoBehaviour behaviour)
        {
            this.behaviour = behaviour ?? throw new ArgumentNullException(nameof(behaviour));
        }




#if UNITY_2023_1_OR_NEWER
        /// <summary>
        /// Wait for the end of the monobehaviour execution then return. If the action ends early by interruption, it will throw <see cref="OperationCanceledException"/>
        /// </summary>
        /// <param name="monoBehaviour"></param>
        /// <returns></returns>
        public async Task RunAsync(MonoBehaviour monoBehaviour)
        {
            Run(monoBehaviour);
            float duration = node.behaviourTree.CurrentStage.RemainingDuration;
            await WaitForSecondsAsync(duration, monoBehaviour.destroyCancellationToken);
        }

        public async Task NextFrameAsync(CancellationToken softToken = default)
        {
            var hardToken = this.CancellationToken;
            var ct = GetCancellationTokenFrom(softToken, hardToken);

            try
            {
                await Awaitable.NextFrameAsync(ct);
            }
            catch (OperationCanceledException)
            {
                if (hardToken.IsCancellationRequested)
                    throw;
            }
        }

        public async Task FixedUpdateAsync(CancellationToken softToken = default)
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

        public async Task WaitForSecondsAsync(float delay, CancellationToken softToken = default)
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




        /// <summary>
        /// Set the return value of the node progress
        /// </summary>
        /// <param name="returnVal"></param>
        public void SetReturnVal(bool returnVal)
        {
            if (IsComplete)
            {
                throw new InvalidOperationException("Setting return value to node progress that is already returned.");
            }
            this.returnVal = returnVal;
        }

        private void InvokeEndEvents()
        {
            if (behaviour != null)
                UnityEngine.Object.Destroy(behaviour);
            if (coroutine != null)
                node.AIComponent.StopCoroutine(coroutine);
        }

        public void Dispose()
        {
            if (IsComplete)
                return;

            End();
        }
    }
}
