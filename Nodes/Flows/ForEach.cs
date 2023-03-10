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

        public override void Execute()
        {
            if (!this.enumerable.HasReference)
            {
                End(false);
                return;
            }

            if (this.enumerable.Value is not IEnumerable enumerable)
            {
                End(false);
                return;
            }

            enumerator = enumerable.GetEnumerator();
            TryMoveNext();
        }

        public override void ReceiveReturnFromChild(bool @return)
        {
            TryMoveNext();
        }

        private void TryMoveNext()
        {
            if (!enumerator.MoveNext())
            {
                End(true);
                return;
            }

            if (item.HasReference) item.Value = enumerator.Current;
            SetNextExecute(@event);
        }




        public override void Initialize()
        {
            @event = behaviourTree.References[@event];
        }
    }
}