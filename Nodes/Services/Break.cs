using Amlos.AI.References;
using System;

namespace Amlos.AI.Nodes
{

    [Serializable]
    [NodeTip("Break the ongoing process by given condition")]
    public sealed class Break : RepeatService
    {
        public enum ReturnType
        {
            self,
            parent,
        }

        public ReturnType returnTo;
        public NodeReference condition;

        public override void ReceiveReturnFromChild(bool @return)
        {
            //Debug.Log("Receive return " + @return);
            if (returnTo == ReturnType.parent)
            {
                End(@return);
                if (@return)
                {
                    TreeNode until = parent;
                    until = until?.parent;
                    behaviourTree.Break(until);
                }
                //No return, because this node will be removed from the service stack
            }
            else
            {
                if (@return)
                {
                    TreeNode until = parent;
                    behaviourTree.Break(until);
                }
                End(@return);
            }
        }

        public override void Execute()
        {
            SetNextExecute(condition);
        }

        public override void Initialize()
        {
            condition = behaviourTree.References[condition];
        }
    }
    /**
     * - Sequence
     *   - store enemyCount from GetEnemyCount(); [Node]
     *   - condition
     *     - if enemyCount > 3
     *     - true: ()
     */
}
