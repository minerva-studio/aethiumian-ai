using Aethiumian.AI.References;
using System;

namespace Aethiumian.AI.Nodes
{
    [Serializable]
    [NodeTip("Rollback the service host stack when the condition succeeds")]
    public sealed class Break : RepeatService
    {
        public enum ReturnType
        {
            Self,
            Parent,
        }

        public ReturnType returnTo;
        public NodeReference condition;

        public override void ReceiveReturn(bool @return)
        {
            if (!@return) return;

            var targetStack = TargetStack;
            // end current service first then jump
            End();

            TreeNode until = behaviourTree.GetNode(parent);
            if (returnTo == ReturnType.Parent) until = behaviourTree.GetNode(until?.parent);
            targetStack?.Break(until);
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
