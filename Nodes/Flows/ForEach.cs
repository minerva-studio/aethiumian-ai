using Amlos.AI.References;
using Amlos.AI.Variables;
using System.Collections;

namespace Amlos.AI.Nodes
{
    [NodeTip("A For-Each loop")]
    public sealed class ForEach : Flow
    {
        public VariableReference enumerable;
        public VariableReference item;
        public NodeReference @event;

        private IEnumerator enumerator;

        public override State Execute()
        {
            if (!this.enumerable.HasReference)
            {
                return State.Failed;
            }

            if (this.enumerable.Value is not IEnumerable enumerable)
            {
                return State.Failed;
            }

            enumerator = enumerable.GetEnumerator();
            return TryMoveNext();
        }

        public override State ReceiveReturnFromChild(bool @return)
        {
            return TryMoveNext();
        }

        private State TryMoveNext()
        {
            // enumerator reach end
            if (!enumerator.MoveNext())
            {
                return State.Success;
            }

            if (item.HasReference) item.Value = enumerator.Current;
            return SetNextExecute(@event);
        }




        public override void Initialize()
        {
            @event = behaviourTree.References[@event];
        }
    }
}