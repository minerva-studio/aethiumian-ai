using Amlos.AI.References;

namespace Amlos.AI.Nodes
{
    [NodeTip("Break the ongoing process by listening the return value of given event")]
    public sealed class BreakOnChange : RepeatService
    {
        public NodeReference @event;
        bool firstValue;
        bool firstIteration;

        public override State Execute()
        {
            return SetNextExecute(@event);
        }

        public override void Initialize()
        {
            @event = behaviourTree.References[@event];
        }

        public override void OnRegistered()
        {
            firstIteration = true;
        }

        public override void ReceiveReturn(bool @return)
        {
            if (firstIteration)
            {
                firstValue = @return;
                firstIteration = false;
                return;
            }

            if (firstValue == @return) return;

            // change  
            // end current service first then jump
            End();

            // always break to grand parent of the service (parent of the node service attached)
            TreeNode until = parent;
            until = until?.parent;
            behaviourTree.Break(until);
        }
    }
}
