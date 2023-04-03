using Amlos.AI.References;
using System;
using System.Collections.Generic;

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
        public List<RawNodeReference> ignoredChildren;

        public override State ReceiveReturnFromChild(bool @return)
        {
            if (!@return)
            {
                return State.Failed;
            }
            foreach (var item in ignoredChildren)
            {
                if (item.Node == behaviourTree.CurrentStage)
                    return State.Failed;
            }

            // end current service first then jump
            behaviourTree.EndService(this);
            TreeNode until = parent;
            if (returnTo == ReturnType.parent) until = until?.parent;
            behaviourTree.Break(until);
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
}
