using Amlos.AI.Nodes;
using System.Collections;
using UnityEngine;

namespace Amlos.AI.References
{
    /// <summary>
    /// class representing the progress of ai
    /// </summary>
    public class NodeProgress
    {
        readonly TreeNode node;
        bool hasReturned;
        bool returnVal;

        /// <summary>
        /// action will execute when the node is forced to stop
        /// </summary>
        public event System.Action InterruptStopAction { add => node.OnInterrupted += value; remove => node.OnInterrupted -= value; }

        /// <summary>
        /// waiting coroutine for script
        /// </summary>
        Coroutine coroutine;
        MonoBehaviour behaviour;

        public NodeProgress(TreeNode node)
        {
            this.node = node;
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
            if (hasReturned)
            {
                return false;
            }
            if (node is not Action action)
            {
                return false;
            }
            return hasReturned = action.End(@return);
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
                while (monoBehaviour)
                {
                    yield return new WaitForFixedUpdate();
                }
                if (!hasReturned) End(returnVal);
            }
        }

        /// <summary>
        /// Set the return value of the node progress
        /// </summary>
        /// <param name="returnVal"></param>
        public void SetReturnVal(bool returnVal)
        {
            if (hasReturned)
            {
                Debug.LogWarning("Setting return value to node progress that is already returned.");
                return;
            }
            this.returnVal = returnVal;
        }

        private void BreakRunAndReturn()
        {
            if (coroutine == null)
            {
                return;
            }
            node.AIComponent.StopCoroutine(coroutine);
            Object.Destroy(behaviour);
        }

    }
}