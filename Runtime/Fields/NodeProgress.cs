using Amlos.AI.Nodes;
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
                if (!nodeProgress.isValid)
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
        bool hasReturned;
        bool returnVal;
        bool disposed;

        /// <summary>
        /// action will execute when the node is forced to stop
        /// </summary>
        public event System.Action InterruptStopAction { add => node.OnInterrupted += value; remove => node.OnInterrupted -= value; }
        public bool isValid => !hasReturned && !disposed;
        public bool IsCancellationRequested { get; private set; }

        /// <summary>
        /// waiting coroutine for script
        /// </summary>
        Coroutine coroutine;
        MonoBehaviour behaviour;

        public NodeProgress(TreeNode node)
        {
            this.node = node;
            this.node.OnInterrupted += Dispose;
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
            if (!isValid)
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
            coroutine = node.AIComponent.StartCoroutine(Wait());
            behaviour = monoBehaviour;
            InterruptStopAction += BreakRunAndReturn;
            returnVal = ret;

            IEnumerator Wait()
            {
                yield return new UnityEngine.WaitWhile(() => monoBehaviour);
                if (!hasReturned) End(returnVal);
            }
        }

        /// <summary>
        /// Set the return value of the node progress
        /// </summary>
        /// <param name="returnVal"></param>
        public void SetReturnVal(bool returnVal)
        {
            if (!isValid)
            {
                Debug.LogWarning("Setting return value to node progress that is already returned.");
                return;
            }
            this.returnVal = returnVal;
        }

        private void BreakRunAndReturn()
        {
            IsCancellationRequested = true;
            if (coroutine == null)
            {
                return;
            }
            node.AIComponent.StopCoroutine(coroutine);
            UnityEngine.Object.Destroy(behaviour);
        }

        public void Dispose()
        {
            disposed = true;
        }

        public IAsyncEnumerator<float> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new Node(this, cancellationToken);
        }
    }
}