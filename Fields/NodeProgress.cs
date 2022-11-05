using System.Collections;
using UnityEngine;

namespace Amlos.AI
{
    /// <summary>
    /// class representing the progress of ai
    /// </summary>
    public class NodeProgress
    {
        TreeNode node;
        bool hasReturned;

        /// <summary>
        /// action will execute when the node is forced to stop
        /// </summary>
        public System.Action InterruptStopAction;

        public NodeProgress(TreeNode scriptCall)
        {
            this.node = scriptCall;
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
        public void End(bool @return)
        {
            //do not return again if has returned
            if (hasReturned)
            {
                return;
            }
            Debug.Log("Return");
            hasReturned = true;
            node.End(@return);
        }

        public void RunAndReturn(MonoBehaviour monoBehaviour, bool ret = true)
        {
            node.Script.StartCoroutine(Wait());

            IEnumerator Wait()
            {
                while (monoBehaviour)
                {
                    yield return new WaitForFixedUpdate();
                }
                Debug.Log("move roll end");
                if (!hasReturned) End(ret);
            }
        }
    }
}