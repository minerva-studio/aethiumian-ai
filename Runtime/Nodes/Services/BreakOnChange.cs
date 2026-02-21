using Amlos.AI.References;

namespace Amlos.AI.Nodes
{
    [NodeTip("Break the ongoing process by listening the return value of given event")]
    public sealed class BreakOnChange : RepeatService
    {
        public enum ReturnType
        {
            Self,
            Parent,
        }

        public ReturnType returnTo;
        public NodeReference @event;
        bool firstValue;
        int iteration;

        public override State Execute()
        {
            ResetTimer();
            return SetNextExecute(@event);
        }

        public override void Initialize()
        {
            @event = behaviourTree.References[@event];
        }

        public override void OnRegistered()
        {
            iteration = 0;
        }

        public override void ReceiveReturn(bool @return)
        {
            if (iteration++ == 0)
            {
                firstValue = @return;
                return;
            }

            if (firstValue == @return) return;
            // change  
            // end current service first then jump
            End();

            TreeNode until = behaviourTree.GetNode(parent);
            // set to return parent
            if (returnTo == ReturnType.Parent) until = behaviourTree.GetNode(until?.parent);
            // set to return self
            else
            {
                // ensure iteration flag is reset
                iteration = 0;
            }

            behaviourTree.Break(until);
        }
    }
}
