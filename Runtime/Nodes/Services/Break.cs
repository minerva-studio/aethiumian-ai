using Amlos.AI.References;
using System;
using System.Collections.Generic;
using UnityEngine;

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

        public override void ReceiveReturn(bool @return)
        {
            if (!@return) return;
            foreach (var item in ignoredChildren)
            {
                if (item.Node == behaviourTree.CurrentStage) return;
            }

            // end current service first then jump
            End();

            TreeNode until = parent;
            if (returnTo == ReturnType.parent) until = until?.parent;
            behaviourTree.Break(until);
        }

        public override State Execute()
        {
            ResetTimer();
            return SetNextExecute(condition);
        }

        public override void Initialize()
        {
            behaviourTree.GetNode(ref condition);
        }
    }
}
