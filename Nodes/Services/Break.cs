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

        public override State ReceiveReturnFromChild(bool @return)
        {

            if (!@return)
            {
                return State.Failed;
            }

            // end current service first then jump
            behaviourTree.EndService(this);
            if (returnTo == ReturnType.parent)
            {
                TreeNode until = parent;
                until = until?.parent;
                behaviourTree.Break(until);
                //No return, because this node will be removed from the service stack
            }
            else
            {
                TreeNode until = parent;
                behaviourTree.Break(until);
            }
            return State.Success;
        }

        public override State Execute()
        {
            return SetNextExecute(condition);
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
