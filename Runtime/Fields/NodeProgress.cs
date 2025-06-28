using Amlos.AI.Nodes;
using Minerva.Module;
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
    public class NodeProgress : IDisposable, IAsyncEnumerable<float>
    {
        readonly struct Node : IAsyncEnumerator<float>
        {
            private readonly NodeProgress nodeProgress;
            private readonly CancellationToken cancellationToken;

            public Node(NodeProgress nodeProgress, CancellationToken cancellationToken)
            {
                this.nodeProgress = nodeProgress;
                this.cancellationToken = cancellationToken;
            }

            public readonly float Current => nodeProgress.node.behaviourTree.CurrentStageDuration;

            public readonly ValueTask DisposeAsync() => default;

            public async readonly ValueTask<bool> MoveNextAsync()
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return false;
                }
                if (!nodeProgress.IsValid)
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

        readonly TreeNode node;
        CancellationTokenSource cancellationTokenSource;
        bool hasReturned;
        bool returnVal;
        bool disposed;

        /// <summary>
        /// action will execute when the node is forced to stop
        /// </summary>
        public event System.Action InterruptStopAction { add => node.OnInterrupted += value; remove => node.OnInterrupted -= value; }
        public bool IsValid => !hasReturned && !disposed;
        public CancellationToken CancellationToken => CancellationTokenSource.Token;
        private CancellationTokenSource CancellationTokenSource
        {
            get
            {
                if (cancellationTokenSource == null)
                {
                    if (node.AIComponent == null)
                    {
                        throw new Exception("Node AIComponent is null, cannot create cancellation token source. Make sure the node is properly initialized.");
                    }
                    cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(node.AIComponent.destroyCancellationToken);
                }
                return cancellationTokenSource;
            }
        }


        /// <summary>
        /// waiting coroutine for script
        /// </summary>
        Coroutine coroutine;
        MonoBehaviour behaviour;

        public NodeProgress(TreeNode node)
        {
            this.node = node;
            this.InterruptStopAction += Dispose;
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
        /// end this node
        /// </summary>
        /// <param name="return">the return value of the node</param>
        public bool End(bool @return)
        {
            //do not return again if has returned
            if (!IsValid)
            {
                return false;
            }
            if (node is not Nodes.Action action)
            {
                return false;
            }
            //return hasReturned = action.behaviourTree.ReceiveReturn(node, @return);
            return hasReturned = action.ReceiveEndSignal(@return);
        }

        /// <summary>
        /// end the node execution when the mono behaviour is destroyed
        /// </summary>
        /// <param name="monoBehaviour"></param>
        /// <param name="ret"></param>
        public void RunAndReturn(MonoBehaviour monoBehaviour, bool ret = true)
        {
            Run(monoBehaviour);

            coroutine = node.AIComponent.StartCoroutine(Wait());
            returnVal = ret;

            IEnumerator Wait()
            {
                yield return new UnityEngine.WaitWhile(() => monoBehaviour);
                if (!hasReturned) End(returnVal);
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
            hardToken = this.CancellationToken;
            var cts = softToken.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(softToken, hardToken)
                : CancellationTokenSource.CreateLinkedTokenSource(hardToken);
            return cts.Token;
        }

#endif




        /// <summary>
        /// Set the return value of the node progress
        /// </summary>
        /// <param name="returnVal"></param>
        public void SetReturnVal(bool returnVal)
        {
            if (!IsValid)
            {
                Debug.LogWarning("Setting return value to node progress that is already returned.");
                return;
            }
            this.returnVal = returnVal;
        }


        public void Dispose()
        {
            disposed = true;
            try { cancellationTokenSource?.Cancel(); }
            catch (Exception e) { Debug.LogException(e); }

            if (behaviour != null)
                UnityEngine.Object.Destroy(behaviour);
            if (coroutine != null)
                node.AIComponent.StopCoroutine(coroutine);
        }

        public IAsyncEnumerator<float> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new Node(this, cancellationToken);
        }

        public async Task WaitForSecondsAsync(object kickDuration)
        {
            throw new NotImplementedException();
        }
    }
}