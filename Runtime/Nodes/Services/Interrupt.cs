using Amlos.AI.References;
using System;
using System.Collections.Generic;

namespace Amlos.AI.Nodes
{
    [Serializable]
    [NodeTip("Interrupt the host node by condition and return the configured result")]
    public sealed class Interrupt : RepeatService
    {
        public enum ReturnResult
        {
            Failed,
            Success
        }

        public NodeReference condition;
        public List<RawNodeReference> ignoredChildren;
        public ReturnResult result = ReturnResult.Failed;

        public override void ReceiveReturn(bool @return)
        {
            if (!@return) return;

            var targetStack = TargetStack;
            var targetNode = targetStack?.Current ?? targetStack?.Peek();
            if (ignoredChildren != null)
            {
                foreach (var item in ignoredChildren)
                {
                    if (item.Node == targetNode) return;
                }
            }

            // End the service stack before changing the host stack.
            End();

            var host = behaviourTree.GetNode(parent);
            targetStack?.Interrupt(host, result == ReturnResult.Success);
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
